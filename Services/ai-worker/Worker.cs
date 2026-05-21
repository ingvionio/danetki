using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Danetka.AiWorker.Kafka;
using Danetka.AiWorker.Llm;
using Danetka.AiWorker.Logging;
using Danetka.Contracts.Puzzle;
using Grpc.Core;

namespace Danetka.AiWorker;

public class Worker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly ILlmClient _llm;
    private readonly DatasetLogger _datasetLogger;
    private readonly PuzzleService.PuzzleServiceClient _puzzleClient;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration config,
        ILlmClient llm,
        DatasetLogger datasetLogger,
        PuzzleService.PuzzleServiceClient puzzleClient)
    {
        _logger        = logger;
        _config        = config;
        _llm           = llm;
        _datasetLogger = datasetLogger;
        _puzzleClient  = puzzleClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = _config["KAFKA_BOOTSTRAP_SERVERS"] ?? "localhost:9092";
        var group     = _config["KAFKA_CONSUMER_GROUP"]    ?? "ai-worker-group";
        var topic     = _config["KAFKA_TOPIC_INPUT"]       ?? "stories.raw";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId          = group,
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            // ВАЖНО: ручной коммит. Коммитим offset ТОЛЬКО после успешного
            // SavePuzzle (или ALREADY_EXISTS — это идемпотентность).
            // Если упадём в середине — Kafka отдаст сообщение снова при перезапуске.
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka error: {Reason}", e.Reason))
            .Build();

        consumer.Subscribe(topic);
        _logger.LogInformation(
            "Subscribed: topic={Topic} bootstrap={Bootstrap} group={Group} autoCommit=false",
            topic, bootstrap, group);

        await Task.Yield();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result?.Message is null) continue;

                    var canCommit = await HandleMessageAsync(result, stoppingToken);
                    if (canCommit)
                    {
                        consumer.Commit(result);
                        _logger.LogInformation(
                            "Committed offset={Offset} partition={Partition}",
                            result.Offset.Value, result.Partition.Value);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "NOT committing offset={Offset} — message will be redelivered on next consume",
                            result.Offset.Value);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("Consume error: {Reason}", ex.Error.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) { /* graceful */ }
        finally
        {
            consumer.Close();
        }
    }

    // Возвращает true, если сообщение можно коммитить (обработка успешна
    // ИЛИ ошибка такого рода что повтор не поможет, например bad JSON).
    // Возвращает false если стоит попробовать сообщение ещё раз (LLM/gRPC
    // упали транзиентно).
    private async Task<bool> HandleMessageAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        // --- 1. Парсинг DTO ---
        StoryRawMessage msg;
        try
        {
            var raw = result.Message.Value.TrimStart('﻿');
            var parsed = JsonSerializer.Deserialize<StoryRawMessage>(raw, JsonOpts);
            if (parsed is null)
            {
                _logger.LogWarning("Deserialized to null at offset={Offset}", result.Offset.Value);
                return true; // нет смысла повторять — пропускаем
            }
            msg = parsed;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                "Bad JSON at offset={Offset}: {Reason}",
                result.Offset.Value, ex.Message);
            return true; // невалидное сообщение — пропускаем, не зацикливаемся
        }

        _logger.LogInformation(
            "Received story: id={StoryId} url={SourceUrl} textLen={TextLen}",
            msg.StoryId, msg.SourceUrl, msg.Text.Length);

        // --- 2. LLM ---
        var sw = Stopwatch.StartNew();
        PuzzleParts? parts = null;
        string? error = null;

        try
        {
            parts = await _llm.SplitStoryAsync(msg.Text, ct);
            sw.Stop();
            _logger.LogInformation(
                "LLM ok: story={StoryId} openLen={OpenLen} hiddenLen={HiddenLen} took={Ms}ms",
                msg.StoryId, parts.OpenPart.Length, parts.HiddenPart.Length, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            error = $"LLM: {ex.Message}";
            _logger.LogError(ex,
                "LLM failed: story={StoryId} took={Ms}ms",
                msg.StoryId, sw.ElapsedMilliseconds);

            await LogDatasetAsync(msg, parts, error, sw.ElapsedMilliseconds, ct);
            return false; // транзиентная LLM-ошибка — пробуем снова
        }

        // --- 3. gRPC → Puzzle Service ---
        bool puzzleSaved = false;

        try
        {
            var request = new SavePuzzleRequest
            {
                OpenPart   = parts!.OpenPart,
                HiddenPart = parts.HiddenPart,
                SourceUrl  = msg.SourceUrl,
                StoryId    = msg.StoryId.ToString(),
            };

            var resp = await _puzzleClient.SavePuzzleAsync(request, cancellationToken: ct);
            puzzleSaved = true;

            _logger.LogInformation(
                "Puzzle saved: puzzleId={PuzzleId} story={StoryId}",
                resp.PuzzleId, msg.StoryId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Идемпотентность: если Puzzle уже видел этот source_url —
            // считаем что обработали успешно. Это нормальный кейс после
            // повтора сообщения из Kafka.
            puzzleSaved = true;
            _logger.LogInformation(
                "Puzzle ALREADY_EXISTS for source_url={Url} — treating as success",
                msg.SourceUrl);
        }
        catch (RpcException ex)
        {
            error = $"gRPC {ex.StatusCode}: {ex.Status.Detail}";
            _logger.LogError(ex,
                "Puzzle save failed: status={Status} story={StoryId}",
                ex.StatusCode, msg.StoryId);
            // puzzleSaved остаётся false → не коммитим
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = $"Unexpected: {ex.Message}";
            _logger.LogError(ex,
                "Unexpected error saving puzzle for story={StoryId}",
                msg.StoryId);
        }

        await LogDatasetAsync(msg, parts, error, sw.ElapsedMilliseconds, ct);

        return puzzleSaved;
    }

    private async Task LogDatasetAsync(
        StoryRawMessage msg, PuzzleParts? parts, string? error, long durationMs, CancellationToken ct)
    {
        await _datasetLogger.LogAsync(new DatasetEntry(
            Timestamp:  DateTimeOffset.UtcNow,
            StoryId:    msg.StoryId,
            Model:      _llm.ModelName,
            InputText:  msg.Text,
            OpenPart:   parts?.OpenPart,
            HiddenPart: parts?.HiddenPart,
            Error:      error,
            DurationMs: durationMs), ct);
    }
}

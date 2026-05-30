using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Danetka.AiWorker.Kafka;
using Danetka.AiWorker.Llm;
using Danetka.AiWorker.Logging;
using Danetka.Contracts.Puzzle;
using Grpc.Core;

namespace Danetka.AiWorker;

public class Worker : BackgroundService
{
    // Если retry_count >= MaxRetries — сообщение уходит в DLQ и не обрабатывается.
    // Значение зафиксировано схемой contracts/story.raw.json.
    private const int MaxRetries = 3;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Не сериализовать null-поля (например, SourceTitle если его не было).
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
        var topicMain = _config["KAFKA_TOPIC_INPUT"]       ?? "stories.raw";
        var topicDlq  = _config["KAFKA_TOPIC_DLQ"]         ?? "stories.dlq";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId          = group,
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            // Ручной коммит. Коммитим после того как разобрались с сообщением
            // (обработка успешна / отправили в DLQ / положили обратно с retry_count+1).
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka error: {Reason}", e.Reason))
            .Build();

        // Producer для retry-публикаций и DLQ.
        // Идемпотентный режим защищает от дублей при ретраях внутри librdkafka.
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrap,
            Acks = Acks.All,
            EnableIdempotence = true,
        };
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        consumer.Subscribe(topicMain);
        _logger.LogInformation(
            "Subscribed: topic={Topic} dlq={Dlq} bootstrap={Bootstrap} group={Group} maxRetries={Max}",
            topicMain, topicDlq, bootstrap, group, MaxRetries);

        await Task.Yield();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result?.Message is null) continue;

                    var canCommit = await HandleMessageAsync(
                        result, producer, topicMain, topicDlq, stoppingToken);

                    if (canCommit)
                    {
                        consumer.Commit(result);
                    }
                    else
                    {
                        // Сюда попадаем только если republish/DLQ-публикация сама упала.
                        // На следующем consume Kafka отдаст это же сообщение снова.
                        _logger.LogWarning(
                            "Skip commit for offset={Offset} — will be redelivered",
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
            // Дожидаемся отправки всех pending-publish, иначе можем потерять
            // ретраи / DLQ-сообщения при остановке.
            producer.Flush(TimeSpan.FromSeconds(5));
            consumer.Close();
        }
    }

    // Возвращает true если можно коммитить offset входящего сообщения.
    // false возвращается только при сбое самого producer'а — в этом случае
    // сообщение придёт ещё раз и попытка повторится.
    private async Task<bool> HandleMessageAsync(
        ConsumeResult<string, string> result,
        IProducer<string, string> producer,
        string topicMain,
        string topicDlq,
        CancellationToken ct)
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
                return true; // нечего повторять
            }
            msg = parsed;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                "Bad JSON at offset={Offset}: {Reason}",
                result.Offset.Value, ex.Message);
            return true; // невалидное сообщение — пропускаем
        }

        _logger.LogInformation(
            "Received story: id={StoryId} url={SourceUrl} textLen={TextLen} retryCount={Retry}",
            msg.StoryId, msg.SourceUrl, msg.Text.Length, msg.RetryCount);

        // --- 2. Проверка retry-лимита ДО обработки ---
        if (msg.RetryCount >= MaxRetries)
        {
            _logger.LogWarning(
                "Retry limit ({Max}) reached, moving to DLQ: story={StoryId}",
                MaxRetries, msg.StoryId);
            try
            {
                await PublishStoryAsync(producer, topicDlq, msg, ct);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Failed to publish to DLQ for story={StoryId}",
                    msg.StoryId);
                return false; // не коммитим, повторим
            }
        }

        // --- 3. LLM ---
        var sw = Stopwatch.StartNew();
        PuzzleParts? parts = null;
        EvaluationResult? eval = null;
        string? error = null;

        try
        {
            parts = await _llm.SplitStoryAsync(msg.Text, ct);

            eval = await _llm.EvaluatePuzzleAsync(msg.Text, parts, ct);
            _logger.LogInformation(
                "Puzzle evaluated: score={Score} reason={Reason}",
                eval.Score, eval.Reason);

            if (eval.Score < 7)
            {
                throw new InvalidOperationException(
                    $"LLM Quality too low ({eval.Score}/10): {eval.Reason}");
            }

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

            await LogDatasetAsync(msg, parts, eval, error, sw.ElapsedMilliseconds, ct);
            return await TryRequeueAsync(producer, topicMain, msg, ct);
        }

        // --- 4. gRPC → Puzzle Service ---
        bool puzzleSaved = false;

        try
        {
            var request = new SavePuzzleRequest
            {
                OpenPart   = parts!.OpenPart,
                HiddenPart = parts.HiddenPart,
                SourceUrl  = msg.SourceUrl,
                StoryId    = msg.StoryId.ToString(),
                JobId      = msg.JobId.ToString(),
            };
            var resp = await _puzzleClient.SavePuzzleAsync(request, cancellationToken: ct);
            puzzleSaved = true;
            _logger.LogInformation(
                "Puzzle saved: puzzleId={PuzzleId} story={StoryId}",
                resp.PuzzleId, msg.StoryId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = $"Unexpected: {ex.Message}";
            _logger.LogError(ex,
                "Unexpected error saving puzzle for story={StoryId}",
                msg.StoryId);
        }

        await LogDatasetAsync(msg, parts, eval, error, sw.ElapsedMilliseconds, ct);

        if (puzzleSaved)
        {
            return true;
        }

        // SavePuzzle упал — отправляем в очередь на повтор.
        return await TryRequeueAsync(producer, topicMain, msg, ct);
    }

    // Публикует сообщение обратно в основной топик с retry_count+1.
    // Возвращает true если получилось (можно коммитить оригинал).
    private async Task<bool> TryRequeueAsync(
        IProducer<string, string> producer, string topic, StoryRawMessage msg, CancellationToken ct)
    {
        var next = new StoryRawMessage
        {
            StoryId     = msg.StoryId,
            JobId       = msg.JobId,
            Text        = msg.Text,
            SourceUrl   = msg.SourceUrl,
            SourceTitle = msg.SourceTitle,
            ParsedAt    = msg.ParsedAt,
            RetryCount  = msg.RetryCount + 1,
        };

        try
        {
            await PublishStoryAsync(producer, topic, next, ct);
            _logger.LogInformation(
                "Requeued: story={StoryId} newRetryCount={Retry}",
                next.StoryId, next.RetryCount);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to requeue story={StoryId}", msg.StoryId);
            return false;
        }
    }

    private static async Task PublishStoryAsync(
        IProducer<string, string> producer, string topic, StoryRawMessage msg, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(msg, JsonOpts);
        await producer.ProduceAsync(
            topic,
            new Message<string, string> { Value = json },
            ct);
    }

    private async Task LogDatasetAsync(
        StoryRawMessage msg,
        PuzzleParts? parts,
        EvaluationResult? eval,
        string? error,
        long durationMs,
        CancellationToken ct)
    {
        await _datasetLogger.LogAsync(new DatasetEntry(
            Timestamp:  DateTimeOffset.UtcNow,
            StoryId:    msg.StoryId,
            Model:      _llm.ModelName,
            InputText:  msg.Text,
            OpenPart:   parts?.OpenPart,
            HiddenPart: parts?.HiddenPart,
            Error:      error,
            DurationMs: durationMs,
            Score:      eval?.Score,
            EvalReason: eval?.Reason), ct);
    }
}

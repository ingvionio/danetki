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
    private const int MaxRetries = 3;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
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
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka error: {Reason}", e.Reason))
            .Build();

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
            producer.Flush(TimeSpan.FromSeconds(5));
            consumer.Close();
        }
    }

    private async Task<bool> HandleMessageAsync(
        ConsumeResult<string, string> result,
        IProducer<string, string> producer,
        string topicMain,
        string topicDlq,
        CancellationToken ct)
    {
        StoryRawMessage rawMessage;
        try
        {
            var raw = result.Message.Value.TrimStart('﻿');
            var parsed = JsonSerializer.Deserialize<StoryRawMessage>(raw, JsonOpts);
            if (parsed is null)
            {
                _logger.LogWarning("Deserialized to null at offset={Offset}", result.Offset.Value);
                return true;
            }
            rawMessage = parsed;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                "Bad JSON at offset={Offset}: {Reason}",
                result.Offset.Value, ex.Message);
            return true;
        }

        _logger.LogInformation(
            "Received story: id={StoryId} url={SourceUrl} textLen={TextLen} retryCount={Retry}",
            rawMessage.StoryId, rawMessage.SourceUrl, rawMessage.Text.Length, rawMessage.RetryCount);

        if (rawMessage.RetryCount >= MaxRetries)
        {
            _logger.LogWarning(
                "Retry limit ({Max}) reached, moving to DLQ: story={StoryId}",
                MaxRetries, rawMessage.StoryId);
            try
            {
                await PublishStoryAsync(producer, topicDlq, rawMessage, ct);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Failed to publish to DLQ for story={StoryId}",
                    rawMessage.StoryId);
                return false;
            }
        }

        var stopwatch = Stopwatch.StartNew();
        PuzzleParts? puzzleParts = null;
        EvaluationResult? evaluationResult = null;
        string? error = null;

        try
        {
            puzzleParts = await _llm.SplitStoryAsync(rawMessage.Text, ct);

            evaluationResult = await _llm.EvaluatePuzzleAsync(rawMessage.Text, puzzleParts, ct);
            _logger.LogInformation(
                "Puzzle evaluated: score={Score} reason={Reason}",
                evaluationResult.Score, evaluationResult.Reason);

            if (evaluationResult.Score < 7)
            {
                throw new InvalidOperationException(
                    $"LLM Quality too low ({evaluationResult.Score}/10): {evaluationResult.Reason}");
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "LLM ok: story={StoryId} openLen={OpenLen} hiddenLen={HiddenLen} took={Ms}ms",
                rawMessage.StoryId, puzzleParts.OpenPart.Length, puzzleParts.HiddenPart.Length, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            error = $"LLM: {ex.Message}";
            _logger.LogError(ex,
                "LLM failed: story={StoryId} took={Ms}ms",
                rawMessage.StoryId, stopwatch.ElapsedMilliseconds);

            await LogDatasetAsync(rawMessage, puzzleParts, evaluationResult, error, stopwatch.ElapsedMilliseconds, ct);
            return await TryRequeueAsync(producer, topicMain, rawMessage, ct);
        }

        var puzzleSaved = false;

        try
        {
            var request = new SavePuzzleRequest
            {
                OpenPart   = puzzleParts!.OpenPart,
                HiddenPart = puzzleParts.HiddenPart,
                SourceUrl  = rawMessage.SourceUrl,
                StoryId    = rawMessage.StoryId.ToString(),
                JobId      = rawMessage.JobId.ToString(),
            };
            var resp = await _puzzleClient.SavePuzzleAsync(request, cancellationToken: ct);
            puzzleSaved = true;
            _logger.LogInformation(
                "Puzzle saved: puzzleId={PuzzleId} story={StoryId}",
                resp.PuzzleId, rawMessage.StoryId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            puzzleSaved = true;
            _logger.LogInformation(
                "Puzzle ALREADY_EXISTS for source_url={Url} — treating as success",
                rawMessage.SourceUrl);
        }
        catch (RpcException ex)
        {
            error = $"gRPC {ex.StatusCode}: {ex.Status.Detail}";
            _logger.LogError(ex,
                "Puzzle save failed: status={Status} story={StoryId}",
                ex.StatusCode, rawMessage.StoryId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = $"Unexpected: {ex.Message}";
            _logger.LogError(ex,
                "Unexpected error saving puzzle for story={StoryId}",
                rawMessage.StoryId);
        }

        await LogDatasetAsync(rawMessage, puzzleParts, evaluationResult, error, stopwatch.ElapsedMilliseconds, ct);

        if (puzzleSaved)
        {
            return true;
        }

        return await TryRequeueAsync(producer, topicMain, rawMessage, ct);
    }

    private async Task<bool> TryRequeueAsync(
        IProducer<string, string> producer, string topic, StoryRawMessage rawMessage, CancellationToken ct)
    {
        var next = new StoryRawMessage
        {
            StoryId     = rawMessage.StoryId,
            JobId       = rawMessage.JobId,
            Text        = rawMessage.Text,
            SourceUrl   = rawMessage.SourceUrl,
            SourceTitle = rawMessage.SourceTitle,
            ParsedAt    = rawMessage.ParsedAt,
            RetryCount  = rawMessage.RetryCount + 1,
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
                "Failed to requeue story={StoryId}", rawMessage.StoryId);
            return false;
        }
    }

    private static async Task PublishStoryAsync(
        IProducer<string, string> producer, string topic, StoryRawMessage rawMessage, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(rawMessage, JsonOpts);
        await producer.ProduceAsync(
            topic,
            new Message<string, string> { Value = json },
            ct);
    }

    private async Task LogDatasetAsync(
        StoryRawMessage rawMessage,
        PuzzleParts? puzzleParts,
        EvaluationResult? evaluationResult,
        string? error,
        long durationMs,
        CancellationToken ct)
    {
        await _datasetLogger.LogAsync(new DatasetEntry(
            Timestamp:  DateTimeOffset.UtcNow,
            StoryId:    rawMessage.StoryId,
            Model:      _llm.ModelName,
            InputText:  rawMessage.Text,
            OpenPart:   puzzleParts?.OpenPart,
            HiddenPart: puzzleParts?.HiddenPart,
            Error:      error,
            DurationMs: durationMs,
            Score:      evaluationResult?.Score,
            EvalReason: evaluationResult?.Reason), ct);
    }
}

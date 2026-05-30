using System.Text.Json;

namespace Danetka.AiWorker.Logging;

public class DatasetLogger
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<DatasetLogger> _logger;

    public DatasetLogger(IConfiguration config, ILogger<DatasetLogger> logger)
    {
        _filePath = config["DATASET_LOG_PATH"] ?? "logs/dataset.jsonl";
        _logger   = logger;

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public async Task LogAsync(DatasetEntry entry, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(entry, JsonOpts);

        await _lock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append to dataset log at {Path}", _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }
}

public record DatasetEntry(
    DateTimeOffset Timestamp,
    Guid StoryId,
    string Model,
    string InputText,
    string? OpenPart,
    string? HiddenPart,
    string? Error,
    long DurationMs,
    int? Score,
    string? EvalReason);

namespace Danetka.AiWorker.Kafka;

public class StoryRawMessage
{
    public Guid StoryId { get; set; }
    public Guid JobId { get; set; }
    public string Text { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string? SourceTitle { get; set; }
    public DateTime ParsedAt { get; set; }
    public int RetryCount { get; set; }
}

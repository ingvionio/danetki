namespace PuzzleService.Models

{
    public class PuzzleEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OpenPart { get; set; } = string.Empty;
        public string HiddenPart { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string StoryId { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
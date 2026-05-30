namespace Danetka.AiWorker.Llm;

public class PuzzleParts
{
    public string OpenPart   { get; init; } = "";
    public string HiddenPart { get; init; } = "";

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(OpenPart) &&
        !string.IsNullOrWhiteSpace(HiddenPart);
}

public record EvaluationResult(int Score, string Reason);

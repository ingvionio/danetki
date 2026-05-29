namespace Danetka.AiWorker.Llm;

// Результат разбиения истории на две части.
// open_part   — то что видит игрок (загадка без развязки)
// hidden_part — то до чего он должен догадаться (объяснение)
public class PuzzleParts
{
    public string OpenPart   { get; init; } = "";
    public string HiddenPart { get; init; } = "";

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(OpenPart) &&
        !string.IsNullOrWhiteSpace(HiddenPart);
}

public record EvaluationResult(int Score, string Reason);

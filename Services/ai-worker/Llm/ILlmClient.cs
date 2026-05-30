namespace Danetka.AiWorker.Llm;

public interface ILlmClient
{
    string ModelName { get; }

    Task<PuzzleParts> SplitStoryAsync(string storyText, CancellationToken ct);

    Task<EvaluationResult> EvaluatePuzzleAsync(string storyText, PuzzleParts parts, CancellationToken ct);
}

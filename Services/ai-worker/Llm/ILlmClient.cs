namespace Danetka.AiWorker.Llm;

public interface ILlmClient
{
    // Имя модели, которую реально использует клиент.
    // Нужно для записи в датасет — иначе разные провайдеры
    // покажут одно и то же имя в логе.
    string ModelName { get; }

    Task<PuzzleParts> SplitStoryAsync(string storyText, CancellationToken ct);

    Task<EvaluationResult> EvaluatePuzzleAsync(string storyText, PuzzleParts parts, CancellationToken ct);
}

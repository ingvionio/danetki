using System.Net.Http.Json;
using System.Text.Json;

namespace Danetka.AiWorker.Llm;

public class OllamaLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // System-prompt вынесен в Llm/Prompts.cs — общий для всех провайдеров.

    private readonly HttpClient _http;
    private readonly ILogger<OllamaLlmClient> _logger;
    private readonly string _model;

    public OllamaLlmClient(HttpClient http, ILogger<OllamaLlmClient> logger, IConfiguration config)
    {
        _http   = http;
        _logger = logger;
        _model  = config["OLLAMA_MODEL"] ?? "qwen2.5:3b";
    }

    public string ModelName => $"ollama/{_model}";

    public async Task<PuzzleParts> SplitStoryAsync(string storyText, CancellationToken ct)
    {
        var request = new OllamaChatRequest(
            Model: _model,
            Messages:
            [
                new OllamaMessage("system", Prompts.SplitStorySystemPrompt),
                new OllamaMessage("user",   storyText),
            ],
            Stream: false,
            // format:"json" заставляет Ollama выдавать строго валидный JSON
            // на уровне токенизации — невалидный JSON физически не появится.
            Format: "json",
            Options: new OllamaRequestOptions(Temperature: 0.2));

        _logger.LogDebug("Sending to Ollama: model={Model} textLen={Len}", _model, storyText.Length);

        var response = await _http.PostAsJsonAsync("/api/chat", request, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        var chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Ollama returned null response body");

        // chat.Message.Content — это уже строка с JSON внутри
        // (format:"json" гарантирует валидность, но не наличие наших полей).
        var parts = JsonSerializer.Deserialize<PuzzleParts>(chat.Message.Content, JsonOpts)
            ?? throw new InvalidOperationException("Failed to deserialize PuzzleParts from Ollama content");

        if (!parts.IsComplete)
            throw new InvalidOperationException(
                $"Ollama returned incomplete puzzle (open_part='{parts.OpenPart}', hidden_part='{parts.HiddenPart}')");

        return parts;
    }

    // ----- DTOs для запроса/ответа Ollama API -----

    private record OllamaChatRequest(
        string Model,
        OllamaMessage[] Messages,
        bool Stream,
        string Format,
        OllamaRequestOptions Options);

    private record OllamaMessage(string Role, string Content);

    private record OllamaRequestOptions(double Temperature);

    private record OllamaChatResponse(OllamaMessage Message);
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Danetka.AiWorker.Llm;

// Универсальный клиент под OpenAI-совместимый API:
//   DeepSeek, OpenRouter, Mistral, Groq, Together AI, локальные vLLM, etc.
// Все эти провайдеры реализуют один и тот же эндпоинт /chat/completions
// в стиле OpenAI. Меняется только BASE_URL, API_KEY и MODEL.
//
// Конфигурация:
//   OPENAI_BASE_URL  — корневой URL без /chat/completions (default DeepSeek).
//   OPENAI_API_KEY   — ключ; идёт в заголовок Authorization: Bearer.
//   OPENAI_MODEL     — имя модели у выбранного провайдера.
public class OpenAiCompatibleLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<OpenAiCompatibleLlmClient> _logger;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiCompatibleLlmClient(
        HttpClient http,
        ILogger<OpenAiCompatibleLlmClient> logger,
        IConfiguration config)
    {
        _http   = http;
        _logger = logger;
        _baseUrl = (config["OPENAI_BASE_URL"] ?? "https://api.deepseek.com/v1").TrimEnd('/');
        _apiKey  = config["OPENAI_API_KEY"]
            ?? throw new InvalidOperationException("OPENAI_API_KEY is not configured");
        _model   = config["OPENAI_MODEL"]  ?? "deepseek-chat";
    }

    public string ModelName => $"openai/{_model}";

    public async Task<PuzzleParts> SplitStoryAsync(string storyText, CancellationToken ct)
    {
        var request = new ChatRequest(
            Model: _model,
            Messages:
            [
                new ChatMessage("system", Prompts.SplitStorySystemPrompt),
                new ChatMessage("user", storyText),
            ],
            Temperature: 0.2,
            MaxTokens: 2048,
            // response_format: json_object — на всех OpenAI-совместимых провайдерах
            // это «принудительно валидный JSON на выходе». Промпт уже содержит
            // слово JSON, что является требованием OpenAI для этого режима.
            ResponseFormat: new ResponseFormat("json_object"));

        _logger.LogDebug("Sending to {Url}: model={Model} textLen={Len}", _baseUrl, _model, storyText.Length);

        using var msg = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = JsonContent.Create(request, options: JsonOpts),
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var resp = await _http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();

        var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("OpenAI-compatible API returned null body");

        if (chat.Choices is null || chat.Choices.Length == 0)
            throw new InvalidOperationException("Response contains no choices");

        var content = chat.Choices[0].Message.Content;
        var json    = ExtractJson(content);

        var parts = JsonSerializer.Deserialize<PuzzleParts>(json, JsonOpts)
            ?? throw new InvalidOperationException(
                $"Could not deserialize PuzzleParts. Raw: {content}");

        if (!parts.IsComplete)
            throw new InvalidOperationException(
                $"Incomplete puzzle (open='{parts.OpenPart}', hidden='{parts.HiddenPart}')");

        return parts;
    }

    private static string ExtractJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0)
                text = text[(firstNewline + 1)..];
            if (text.EndsWith("```"))
                text = text[..^3].TrimEnd();
        }
        var first = text.IndexOf('{');
        var last  = text.LastIndexOf('}');
        if (first >= 0 && last > first)
            return text.Substring(first, last - first + 1);
        return text;
    }

    // ----- DTOs запроса/ответа -----

    private record ChatRequest(
        string Model,
        ChatMessage[] Messages,
        double Temperature,
        int MaxTokens,
        ResponseFormat? ResponseFormat);

    private record ChatMessage(string Role, string Content);

    private record ResponseFormat(string Type);

    private record ChatResponse(ChatChoice[] Choices);

    private record ChatChoice(ChatMessage Message);
}

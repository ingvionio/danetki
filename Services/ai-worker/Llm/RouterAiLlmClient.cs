using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Danetka.AiWorker.Llm;

public class RouterAiLlmClient : ILlmClient
{
    private const string BaseUrl = "https://routerai.ru/api/v1";
    private const string Model = "deepseek/deepseek-v4-flash";
    private const double DefaultTemperature = 0.2;
    private const int DefaultMaxTokens = 2048;
    private const int EvaluationMaxTokens = 512;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<RouterAiLlmClient> _logger;
    private readonly string _apiKey;

    public RouterAiLlmClient(
        HttpClient http,
        ILogger<RouterAiLlmClient> logger,
        IConfiguration config)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["ROUTER_AI_KEY"]
            ?? throw new InvalidOperationException("ROUTER_AI_KEY is not configured");
    }

    public string ModelName => $"routerai/{Model}";

    public async Task<PuzzleParts> SplitStoryAsync(string storyText, CancellationToken ct)
    {
        var request = new ChatRequest(
            Model: Model,
            Messages:
            [
                new ChatMessage("system", Prompts.SplitStorySystemPrompt),
                new ChatMessage("user", storyText),
            ],
            Temperature: DefaultTemperature,
            MaxTokens: DefaultMaxTokens,
            ResponseFormat: new ResponseFormat("json_object"));

        _logger.LogDebug("Sending to RouterAI: model={Model} textLen={Len}", Model, storyText.Length);

        var content = await PostChatCompletionAsync(request, ct);
        var json = ExtractJson(content);

        var parts = JsonSerializer.Deserialize<PuzzleParts>(json, JsonOpts)
            ?? throw new InvalidOperationException(
                $"Could not deserialize PuzzleParts. Raw: {content}");

        if (!parts.IsComplete)
        {
            throw new InvalidOperationException(
                $"Incomplete puzzle (open='{parts.OpenPart}', hidden='{parts.HiddenPart}')");
        }

        return parts;
    }

    public async Task<EvaluationResult> EvaluatePuzzleAsync(
        string storyText,
        PuzzleParts parts,
        CancellationToken ct)
    {
        var userMessage = BuildEvaluationUserMessage(storyText, parts);

        var request = new ChatRequest(
            Model: Model,
            Messages:
            [
                new ChatMessage("system", Prompts.EvaluatePuzzleSystemPrompt),
                new ChatMessage("user", userMessage),
            ],
            Temperature: DefaultTemperature,
            MaxTokens: EvaluationMaxTokens,
            ResponseFormat: new ResponseFormat("json_object"));

        _logger.LogDebug("Evaluating puzzle via RouterAI: model={Model}", Model);

        var content = await PostChatCompletionAsync(request, ct);
        var json = ExtractJson(content);

        return JsonSerializer.Deserialize<EvaluationResult>(json, JsonOpts)
            ?? throw new InvalidOperationException(
                $"Could not deserialize EvaluationResult. Raw: {content}");
    }

    private async Task<string> PostChatCompletionAsync(ChatRequest request, CancellationToken ct)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
        {
            Content = JsonContent.Create(request, options: JsonOpts),
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var resp = await _http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();

        var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("RouterAI returned null body");

        if (chat.Choices is null || chat.Choices.Length == 0)
        {
            throw new InvalidOperationException("RouterAI response contains no choices");
        }

        return chat.Choices[0].Message.Content;
    }

    private static string BuildEvaluationUserMessage(string storyText, PuzzleParts parts) =>
        $"Исходный текст: {storyText}\n\nСгенерированная данетка: {JsonSerializer.Serialize(parts, JsonOpts)}";

    private static string ExtractJson(string text)
    {
        text = text.Trim();

        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0)
            {
                text = text[(firstNewline + 1)..];
            }

            if (text.EndsWith("```"))
            {
                text = text[..^3].TrimEnd();
            }
        }

        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        if (first >= 0 && last > first)
        {
            return text.Substring(first, last - first + 1);
        }

        return text;
    }

    private record ChatRequest(
        string Model,
        ChatMessage[] Messages,
        double Temperature,
        int MaxTokens,
        ResponseFormat ResponseFormat);

    private record ChatMessage(string Role, string Content);

    private record ResponseFormat(string Type);

    private record ChatResponse(ChatChoice[] Choices);

    private record ChatChoice(ChatMessage Message);
}

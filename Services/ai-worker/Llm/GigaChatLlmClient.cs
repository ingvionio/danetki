using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Danetka.AiWorker.Llm;

public class GigaChatLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private const string OauthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    private const string ChatUrl  = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";
    private const double DefaultTemperature = 0.2;
    private const int DefaultMaxTokens = 2048;
    private const int EvaluationMaxTokens = 512;

    private readonly HttpClient _http;
    private readonly ILogger<GigaChatLlmClient> _logger;
    private readonly string _authKey;
    private readonly string _model;
    private readonly string _scope;

    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public GigaChatLlmClient(HttpClient http, ILogger<GigaChatLlmClient> logger, IConfiguration config)
    {
        _http   = http;
        _logger = logger;
        _authKey = config["GIGACHAT_AUTH_KEY"]
            ?? throw new InvalidOperationException("GIGACHAT_AUTH_KEY is not configured");
        _model = config["GIGACHAT_MODEL"] ?? "GigaChat";
        _scope = config["GIGACHAT_SCOPE"] ?? "GIGACHAT_API_PERS";
    }

    public string ModelName => $"gigachat/{_model}";

    public async Task<PuzzleParts> SplitStoryAsync(string storyText, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);

        var request = new GigaChatRequest(
            Model: _model,
            Messages:
            [
                new GigaChatMessage("system", Prompts.SplitStorySystemPrompt),
                new GigaChatMessage("user", storyText),
            ],
            Temperature: DefaultTemperature,
            MaxTokens: DefaultMaxTokens);

        _logger.LogDebug("Sending to GigaChat: model={Model} textLen={Len}", _model, storyText.Length);

        using var msg = new HttpRequestMessage(HttpMethod.Post, ChatUrl)
        {
            Content = JsonContent.Create(request, options: JsonOpts),
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();

        var chat = await resp.Content.ReadFromJsonAsync<GigaChatResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("GigaChat returned null response body");

        if (chat.Choices is null || chat.Choices.Length == 0)
            throw new InvalidOperationException("GigaChat returned no choices");

        var content = chat.Choices[0].Message.Content;
        var json    = ExtractJson(content);

        var parts = JsonSerializer.Deserialize<PuzzleParts>(json, JsonOpts)
            ?? throw new InvalidOperationException(
                $"Could not deserialize PuzzleParts from GigaChat content. Raw: {content}");

        if (!parts.IsComplete)
            throw new InvalidOperationException(
                $"GigaChat returned incomplete puzzle (open_part='{parts.OpenPart}', hidden_part='{parts.HiddenPart}')");

        return parts;
    }

    public async Task<EvaluationResult> EvaluatePuzzleAsync(string storyText, PuzzleParts parts, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var userMessage = BuildEvaluationUserMessage(storyText, parts);

        var request = new GigaChatRequest(
            Model: _model,
            Messages:
            [
                new GigaChatMessage("system", Prompts.EvaluatePuzzleSystemPrompt),
                new GigaChatMessage("user", userMessage),
            ],
            Temperature: DefaultTemperature,
            MaxTokens: EvaluationMaxTokens);

        _logger.LogDebug("Evaluating puzzle via GigaChat: model={Model}", _model);

        using var msg = new HttpRequestMessage(HttpMethod.Post, ChatUrl)
        {
            Content = JsonContent.Create(request, options: JsonOpts),
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();

        var chat = await resp.Content.ReadFromJsonAsync<GigaChatResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("GigaChat returned null evaluation response body");

        if (chat.Choices is null || chat.Choices.Length == 0)
            throw new InvalidOperationException("GigaChat evaluation returned no choices");

        var content = chat.Choices[0].Message.Content;
        var json    = ExtractJson(content);

        return JsonSerializer.Deserialize<EvaluationResult>(json, JsonOpts)
            ?? throw new InvalidOperationException(
                $"Could not deserialize EvaluationResult from GigaChat content. Raw: {content}");
    }

    private static string BuildEvaluationUserMessage(string storyText, PuzzleParts parts) =>
        $"Исходный текст: {storyText}\n\nСгенерированная данетка: {JsonSerializer.Serialize(parts, JsonOpts)}";

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && _tokenExpiry - TimeSpan.FromMinutes(1) > DateTimeOffset.UtcNow)
            return _accessToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && _tokenExpiry - TimeSpan.FromMinutes(1) > DateTimeOffset.UtcNow)
                return _accessToken;

            _logger.LogInformation("Requesting new GigaChat access token");

            using var msg = new HttpRequestMessage(HttpMethod.Post, OauthUrl);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", _authKey);
            msg.Headers.Add("RqUID", Guid.NewGuid().ToString());
            msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            msg.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("scope", _scope),
            });

            var resp = await _http.SendAsync(msg, ct);
            resp.EnsureSuccessStatusCode();

            var tokenResp = await resp.Content.ReadFromJsonAsync<GigaChatTokenResponse>(JsonOpts, ct)
                ?? throw new InvalidOperationException("GigaChat token response was null");

            _accessToken = tokenResp.AccessToken;
            _tokenExpiry = DateTimeOffset.FromUnixTimeMilliseconds(tokenResp.ExpiresAt);

            _logger.LogInformation("Got GigaChat token, expires at {Expiry}", _tokenExpiry);
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
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

    private record GigaChatRequest(
        string Model,
        GigaChatMessage[] Messages,
        double Temperature,
        int MaxTokens);

    private record GigaChatMessage(string Role, string Content);

    private record GigaChatResponse(GigaChatChoice[] Choices);

    private record GigaChatChoice(GigaChatMessage Message);

    private record GigaChatTokenResponse(string AccessToken, long ExpiresAt);
}

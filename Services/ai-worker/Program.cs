using Danetka.AiWorker;
using Danetka.AiWorker.Llm;
using Danetka.AiWorker.Logging;
using Danetka.Contracts.Puzzle;
using Grpc.Net.Client;

// HTTP/2 без TLS (plaintext) — это разрешено только при явной опции.
// Внутри docker-сети мы по HTTPS не ходим, всё в открытом виде.
// В проде между датацентрами здесь был бы Mutual TLS.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Загружаем .env из корня репо (см. DotNetEnv).
DotNetEnv.Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

// ----- LLM provider switch -----
var llmProvider = builder.Configuration["LLM_PROVIDER"]?.ToLowerInvariant() ?? "ollama";

switch (llmProvider)
{
    case "gigachat":
        builder.Services.AddHttpClient<ILlmClient, GigaChatLlmClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (string.Equals(
                    builder.Configuration["GIGACHAT_TRUST_ALL_CERTS"],
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }
            return handler;
        });
        break;

    case "openai":
        builder.Services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        break;

    case "ollama":
    default:
        builder.Services.AddHttpClient<ILlmClient, OllamaLlmClient>(client =>
        {
            var baseUrl = builder.Configuration["OLLAMA_URL"] ?? "http://localhost:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(6);
        });
        break;
}

// ----- gRPC client to Puzzle Service -----
// Канал и клиент thread-safe и долгоживущие — оба singleton.
// Адрес из env (внутри docker → puzzle-service:50052), для локального
// запуска дефолт localhost:50052 (там ничего нет, gRPC упадёт с
// Unavailable — это ожидаемое поведение пока Puzzle не запущен).
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var addr = config["PUZZLE_SERVICE_ADDR"] ?? "http://localhost:50052";
    if (!addr.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
        !addr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        addr = "http://" + addr;
    }
    return GrpcChannel.ForAddress(addr);
});

builder.Services.AddSingleton<PuzzleService.PuzzleServiceClient>(sp =>
    new PuzzleService.PuzzleServiceClient(sp.GetRequiredService<GrpcChannel>()));

builder.Services.AddSingleton<DatasetLogger>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

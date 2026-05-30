using Danetka.AiWorker;
using Danetka.AiWorker.Llm;
using Danetka.AiWorker.Logging;
using Danetka.Contracts.Puzzle;
using Grpc.Net.Client;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

DotNetEnv.Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

var llmProvider = builder.Configuration["LLM_PROVIDER"]?.ToLowerInvariant() ?? "routerai";

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

    case "routerai":
    default:
        builder.Services.AddHttpClient<ILlmClient, RouterAiLlmClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        break;
}

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

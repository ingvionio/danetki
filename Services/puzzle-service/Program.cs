using Microsoft.EntityFrameworkCore;
using PuzzleService.Data;
using PuzzleService.Services;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

var grpcPort = Environment.GetEnvironmentVariable("GRPC_PORT") ?? "50052";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(grpcPort));
});

// Добавляем поддержку gRPC в контейнер зависимостей
builder.Services.AddGrpc();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var dbHost = builder.Configuration["DB_HOST"];
if (!string.IsNullOrEmpty(dbHost))
{
    var dbName = builder.Configuration["DB_NAME"];
    var dbPort = builder.Configuration["DB_PORT"] ?? "5432";
    var dbUser = builder.Configuration["DB_USER"];
    var dbPassword = builder.Configuration["DB_PASSWORD"];
    
    connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";
}

builder.Services.AddDbContext<PuzzleDbContext>(options =>
    options.UseNpgsql(connectionString));
var app = builder.Build();

app.MapGrpcService<PuzzleGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
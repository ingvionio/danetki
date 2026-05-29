using Microsoft.EntityFrameworkCore;
using PuzzleService.Data;
using PuzzleService.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var dbHost = builder.Configuration["DB_HOST"];
if (!string.IsNullOrEmpty(dbHost))
{
    var dbName = builder.Configuration["DB_NAME"];
    var dbPort = builder.Configuration["DB_PORT"];
    var dbUser = builder.Configuration["DB_USER"];
    var dbPassword = builder.Configuration["DB_PASSWORD"];

    connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";
}

builder.Services.AddDbContext<PuzzleDbContext>(options =>
    options.UseNpgsql(connectionString));

var redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "Puzzle_";
});

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnection));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PuzzleDbContext>();
    db.Database.Migrate();
}

app.MapGrpcService<PuzzleGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();

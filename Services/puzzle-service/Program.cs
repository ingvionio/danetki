using Microsoft.EntityFrameworkCore;
using PuzzleService.Data;
using PuzzleService.Services;

var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку gRPC в контейнер зависимостей
builder.Services.AddGrpc();

// Регистрируем наш контекст базы данных и настраиваем его на использование PostgreSQL
builder.Services.AddDbContext<PuzzleDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.MapGrpcService<PuzzleGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
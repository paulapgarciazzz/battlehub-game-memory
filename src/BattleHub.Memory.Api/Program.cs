using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Data.Services;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// Entity Framework Core
builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MemoryDatabase")
    ));

// Servicios de la aplicación
builder.Services.AddScoped<IGameResultService, GameResultService>();
builder.Services.AddScoped<IGameHistoryService, GameHistoryService>();
builder.Services.AddSingleton<MemoryGameService>();
builder.Services.AddSingleton<TurnTimerService>();
builder.Services.AddSingleton<MatchResultRecorder>();

// Controllers (sin esto, todo /api/games/memory responde 404)
builder.Services.AddControllers();


// OpenAPI
builder.Services.AddOpenApi();

// SignalR
builder.Services.AddSignalR();

const string DevCorsPolicy = "AllowAureliaDevClient";

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
    {
        // 8080: microfrontend en modo independiente.
        // 4000: Shell de BattleHub, que carga el juego por Module Federation
        // (el código del juego corre con el origen del Shell).
        policy.WithOrigins("http://localhost:8080", "http://localhost:4000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(DevCorsPolicy);

app.UseHttpsRedirection();

app.MapHub<MemoryHub>("/hubs/memory");

app.MapControllers();

app.Run();

// Permite usar WebApplicationFactory<Program> en pruebas de integración.
public partial class Program;

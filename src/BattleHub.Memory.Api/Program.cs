
using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Data.Seed;
using BattleHub.Memory.Data.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Entity Framework Core (SQL Server, ADR-001)
builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MemoryDatabase")));

// Servicios de la aplicación
builder.Services.AddScoped<IGameResultService, GameResultService>();
builder.Services.AddScoped<IGameHistoryService, GameHistoryService>();
builder.Services.AddScoped<SampleDataSeeder>();

// Partidas competitivas de BattleHub
builder.Services.AddSingleton<MemoryGameService>();
builder.Services.AddSingleton<TurnTimerService>();
builder.Services.AddSingleton<MatchResultRecorder>();
builder.Services.AddSingleton(TimeProvider.System);

// Servicios aislados para las partidas locales.
// No comparten las sesiones de las partidas competitivas.
builder.Services.AddSingleton<MemoryLocalGameService>();
builder.Services.AddSingleton<MemoryLocalTurnTimerService>();

// Reintenta guardar los resultados que fallaron al terminar la partida
builder.Services.AddHostedService<ResultRetryWorker>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// CORS: Shell y microfrontend de Memory. Los orígenes salen solo de la
// configuración (appsettings.json o variables Cors__Origins__0, __1...).
var corsOrigins = builder.Configuration
    .GetSection("Cors:Origins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray();

if (corsOrigins is not { Length: > 0 })
{
    throw new InvalidOperationException(
        "Configurar Cors:Origins con las direcciones del Shell y del microfrontend.");
}

builder.Services.AddCors(options =>
    options.AddPolicy("MemoryCors", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// Aplicar migraciones pendientes si está habilitado
if (builder.Configuration.GetValue<bool>(
        "Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();

    await scope.ServiceProvider
        .GetRequiredService<MemoryDbContext>()
        .Database.MigrateAsync();
}

// Cargar datos de ejemplo si está habilitado
if (builder.Configuration.GetValue<bool>("SampleData:Enabled"))
{
    var players = builder.Configuration
        .GetSection("SampleData:Players")
        .Get<SamplePlayer[]>();

    using var scope = app.Services.CreateScope();

    await scope.ServiceProvider
        .GetRequiredService<SampleDataSeeder>()
        .SeedAsync(
            players is { Length: >= 2 }
                ? players
                : SampleDataSeeder.DefaultPlayers);
}

// OpenAPI y Hub local solo durante desarrollo
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Modo local: no guarda resultados.
    // No debe exponerse como endpoint de producción.
    app.MapHub<MemoryLocalHub>("/hubs/memory-local");
}

app.UseCors("MemoryCors");

app.MapGet("/health", () =>
    Results.Ok(new { status = "Healthy" }));

// Hub de las partidas de BattleHub. El juego no usa Auth0: el Shell
// entrega el usuario y el juego avisa al Shell cuando termina la partida.
app.MapHub<MemoryHub>("/hubs/memory");

app.MapControllers();

app.Run();

// Permite usar WebApplicationFactory<Program> en pruebas
public partial class Program;

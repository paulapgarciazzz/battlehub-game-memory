
using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Api.Auth;
using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Api.Matchmaking;
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

// Cliente HTTP hacia Matchmaking
builder.Services
    .AddHttpClient("matchmaking", client =>
        client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddSingleton<IMatchmakingGateway>(services =>
    new MatchmakingGateway(
        services.GetRequiredService<IHttpClientFactory>()
            .CreateClient("matchmaking"),
        builder.Configuration,
        services.GetRequiredService<TimeProvider>(),
        services.GetRequiredService<ILogger<MatchmakingGateway>>()));

// Tareas en segundo plano de las partidas competitivas
builder.Services.AddHostedService<FinishWorker>();
builder.Services.AddHostedService<ResultRetryWorker>();

// Auth0: autenticación y autorización para BattleHub
builder.Services.AddMemoryAuth(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// CORS: Shell y microfrontend de Memory
var corsOrigins = builder.Configuration
    .GetSection("Cors:Origins")
    .Get<string[]>()
    ?? [
        "http://localhost:4000",
        "http://localhost:4003"
    ];

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

    // Modo local: no usa Auth0 ni Matchmaking.
    // No debe exponerse como endpoint de producción.
    app.MapHub<MemoryLocalHub>("/hubs/memory-local");
}

// CORS debe ir antes de autenticación y autorización
app.UseCors("MemoryCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new { status = "Healthy" }));

// Hub competitivo original: conserva la autorización de BattleHub
app.MapHub<MemoryHub>("/hubs/memory")
    .RequireAuthorization(MemoryAuth.Play);

app.MapControllers();

app.Run();

// Permite usar WebApplicationFactory<Program> en pruebas
public partial class Program;

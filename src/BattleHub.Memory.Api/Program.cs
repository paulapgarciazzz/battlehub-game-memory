using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Api.Auth;
using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Api.Matchmaking;
using BattleHub.Memory.Data.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Entity Framework Core (SQL Server, ADR-001)
builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MemoryDatabase")));

// Servicios de la aplicación
builder.Services.AddScoped<IGameResultService, GameResultService>();
builder.Services.AddScoped<IGameHistoryService, GameHistoryService>();

// Las partidas en curso viven en memoria: estos servicios son singleton y
// la API debe correr en una sola instancia.
builder.Services.AddSingleton<MemoryGameService>();
builder.Services.AddSingleton<TurnTimerService>();
builder.Services.AddSingleton<MatchResultRecorder>();
builder.Services.AddSingleton(TimeProvider.System);

// Cliente HTTP hacia Matchmaking. Sin redirecciones automáticas, para que el
// token del usuario no viaje a otra dirección que la configurada.
builder.Services
    .AddHttpClient("matchmaking", client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddSingleton<IMatchmakingGateway>(services =>
    new MatchmakingGateway(
        services.GetRequiredService<IHttpClientFactory>().CreateClient("matchmaking"),
        builder.Configuration,
        services.GetRequiredService<TimeProvider>(),
        services.GetRequiredService<ILogger<MatchmakingGateway>>()));

// Tareas en segundo plano: avisar a Matchmaking el fin de partida (ADR-004)
// y reintentar los resultados que no se pudieron guardar.
builder.Services.AddHostedService<FinishWorker>();
builder.Services.AddHostedService<ResultRetryWorker>();

// Auth0: validación del JWT y políticas de autorización.
builder.Services.AddMemoryAuth(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// CORS leído de Cors:Origins (mismo patrón que el ADR-006). Por defecto,
// el Shell (4000) y el microfrontend de Memory (4003).
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:4000", "http://localhost:4003"];

builder.Services.AddCors(options =>
    options.AddPolicy("MemoryCors", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// Database:MigrateOnStartup=true (lo usa scripts/Start-Integration.ps1)
// aplica las migraciones pendientes al arrancar.
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<MemoryDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// CORS va antes de la autenticación para que el preflight no exija token.
app.UseCors("MemoryCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapHub<MemoryHub>("/hubs/memory").RequireAuthorization(MemoryAuth.Play);
app.MapControllers();

app.Run();

// Permite usar WebApplicationFactory<Program> en pruebas de integración.
public partial class Program;

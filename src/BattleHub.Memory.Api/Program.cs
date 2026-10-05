using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Api.Auth;
using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Api.Matchmaking;
using BattleHub.Memory.Data.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MemoryDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("MemoryDatabase")));
builder.Services.AddScoped<IGameResultService, GameResultService>();
builder.Services.AddScoped<IGameHistoryService, GameHistoryService>();
builder.Services.AddSingleton<MemoryGameService>();
builder.Services.AddSingleton<TurnTimerService>();
builder.Services.AddSingleton<MatchResultRecorder>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient("matchmaking", client => client.Timeout = TimeSpan.FromSeconds(10)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IMatchmakingGateway>(services => new MatchmakingGateway(services.GetRequiredService<IHttpClientFactory>().CreateClient("matchmaking"), builder.Configuration, services.GetRequiredService<TimeProvider>(), services.GetRequiredService<ILogger<MatchmakingGateway>>()));
builder.Services.AddHostedService<FinishWorker>();
builder.Services.AddHostedService<ResultRetryWorker>();
builder.Services.AddMemoryAuth(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddPolicy("MemoryCors", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4000", "http://localhost:4003"]).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
var app = builder.Build();
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<MemoryDbContext>().Database.MigrateAsync();
}
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseCors("MemoryCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapHub<MemoryHub>("/hubs/memory").RequireAuthorization(MemoryAuth.Play);
app.MapControllers();
app.Run();
public partial class Program;

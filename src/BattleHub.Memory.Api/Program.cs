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
        policy.WithOrigins("http://localhost:8080")
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

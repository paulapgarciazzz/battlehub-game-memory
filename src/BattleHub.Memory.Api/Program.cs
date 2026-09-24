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


// OpenAPI
builder.Services.AddOpenApi();

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHub<MemoryHub>("/hubs/memory");

var summaries = new[]
{
    "Freezing",
    "Bracing",
    "Chilly",
    "Cool",
    "Mild",
    "Warm",
    "Balmy",
    "Hot",
    "Sweltering",
    "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF =>
        32 + (int)(TemperatureC / 0.5556);
}
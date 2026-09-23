using BattleHub.Memory.Data.DbContext;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Servicios de Entity Framework Core
builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MemoryDatabase")
    ));

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Configurar OpenAPI solamente en Development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoint de prueba
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
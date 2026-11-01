using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.IntegrationTests.Data.Seed;

// Script que ejecuta el servicio sample-data de docker-compose.yml. Si una
// migración cambia las tablas y el script deja de funcionar, falla aquí.
[Collection(DataDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SampleDataScriptTests(MemoryDatabaseFixture database)
{
    private static readonly string Script =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "datos-ejemplo.sql"));

    private static readonly List<string> MatchIds =
        Enumerable.Range(1, 18).Select(i => $"ejemplo-{i:D3}").ToList();

    [Fact]
    public async Task LoadsHistoryOnce()
    {
        await using var db = database.CreateContext();

        // SampleDataSeederTests usa los mismos MatchId en esta base.
        await DeleteSampleGamesAsync(db);
        try
        {
            await db.Database.ExecuteSqlRawAsync(Script);
            await db.Database.ExecuteSqlRawAsync(Script);

            var results = await db.GameResults.AsNoTracking().Include(r => r.Players)
                .Where(r => MatchIds.Contains(r.MatchId))
                .ToListAsync();

            Assert.Equal(18, results.Count);
            Assert.All(results, r =>
            {
                Assert.Equal("memory", r.GameType);
                Assert.Equal(2, r.Players.Count);
                Assert.Equal(8, r.Players.Sum(p => p.Score));
                Assert.True(r.StartedAt < r.FinishedAt);
                Assert.True(r.FinishedAt > DateTimeOffset.UtcNow.AddDays(-30));
                Assert.Equal(r.IsDraw, r.WinnerUserId is null);
            });

            // Cada jugador juega contra los otros tres: 9 partidas en su historial.
            var history = new GameHistoryService(db);
            var stats = await history.GetStatsByPlayerAsync("demo|ana");
            Assert.Equal(9, stats.GamesPlayed);
            Assert.Equal(3, stats.Draws);
            Assert.Equal(9, (await history.GetHistoryByPlayerAsync("demo|ana")).Count);
        }
        finally
        {
            await DeleteSampleGamesAsync(db);
        }
    }

    // Los jugadores se borran en cascada con la partida.
    private static Task<int> DeleteSampleGamesAsync(MemoryDbContext db) =>
        db.GameResults.Where(r => MatchIds.Contains(r.MatchId)).ExecuteDeleteAsync();
}

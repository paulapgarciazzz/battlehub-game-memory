using BattleHub.Memory.Data.Entities;
using BattleHub.Memory.Data.Seed;
using BattleHub.Memory.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.Data.Tests.Seed;

[Collection(MemoryDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SampleDataSeederTests(MemoryDatabaseFixture database)
{
    [Fact]
    public async Task LoadsHistoryOnceWithoutNotifyingMatchmaking()
    {
        await using var db = database.CreateContext();
        var seeder = new SampleDataSeeder(db, TimeProvider.System);
        var players = SampleDataSeeder.DefaultPlayers;

        // Una partida guardada antes con un nombre parecido no debe impedir la carga.
        var manualId = SampleDataSeeder.MatchPrefix + "manual";
        db.GameResults.Add(new MemoryGameResult { Id = Guid.NewGuid(), MatchId = manualId, IsDraw = true });
        await db.SaveChangesAsync();

        // 4 jugadores forman 6 parejas, con 3 partidas cada una.
        Assert.Equal(18, await seeder.SeedAsync(players));
        Assert.Equal(0, await seeder.SeedAsync(players));

        var results = await db.GameResults.AsNoTracking().Include(r => r.Players)
            .Where(r => r.MatchId.StartsWith(SampleDataSeeder.MatchPrefix) && r.MatchId != manualId)
            .ToListAsync();

        Assert.Equal(18, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal(2, r.Players.Count);
            Assert.Equal(8, r.Players.Sum(p => p.Score));
            Assert.True(r.StartedAt < r.FinishedAt);
            Assert.Equal(r.IsDraw, r.WinnerUserId is null);
        });
        Assert.False(await db.FinishNotifications.AnyAsync(n => n.MatchId.StartsWith(SampleDataSeeder.MatchPrefix)));

        // Cada jugador juega contra los otros tres: 9 partidas en su historial.
        var history = new GameHistoryService(db);
        var stats = await history.GetStatsByPlayerAsync(players[0].UserId);
        Assert.Equal(9, stats.GamesPlayed);
        Assert.Equal(3, stats.Draws);
        Assert.Equal(9, (await history.GetHistoryByPlayerAsync(players[0].UserId)).Count);
    }

    [Fact]
    public async Task RequiresAtLeastTwoPlayers()
    {
        await using var db = database.CreateContext();
        var seeder = new SampleDataSeeder(db, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() => seeder.SeedAsync([new("demo|solo", "Solo")]));
    }
}

using BattleHub.Memory.Data.Services;
using BattleHub.Memory.Domain;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.IntegrationTests.Data.Services;

[Collection(DataDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class GameResultServiceTests(MemoryDatabaseFixture database)
{
    private static GameSession CreateFinishedSession(
        string matchId,
        string firstUserId,
        int firstScore,
        string secondUserId,
        int secondScore)
    {
        var board = new Board(["A", "B", "C", "D", "E", "F", "G", "H"]);
        foreach (var card in board.Cards)
            card.MarkAsMatched();

        var first = new Player(firstUserId, "Primero");
        var second = new Player(secondUserId, "Segundo");
        for (var pair = 0; pair < firstScore; pair++)
            first.AddMatchedPair();
        for (var pair = 0; pair < secondScore; pair++)
            second.AddMatchedPair();

        return new GameSession(matchId, board, [first, second]);
    }

    [Fact]
    public async Task SaveResult_RequiresFinishedGame()
    {
        await using var db = database.CreateContext();
        var service = new GameResultService(db);
        var session = new GameSession(
            "unfinished-" + Guid.NewGuid().ToString("N"),
            new Board(["A", "B", "C", "D", "E", "F", "G", "H"]),
            [new Player("u1", "Ana"), new Player("u2", "Beto")]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveResultAsync(session, session.StartedAt));
    }

    [Fact]
    public async Task SaveResult_IsIdempotentAndStoresPlayers()
    {
        await using var db = database.CreateContext();
        var service = new GameResultService(db);
        var matchId = "saved-" + Guid.NewGuid().ToString("N");
        var session = CreateFinishedSession(matchId, "u1", 5, "u2", 3);

        var firstId = await service.SaveResultAsync(session, session.StartedAt);
        var secondId = await service.SaveResultAsync(session, session.StartedAt);
        var result = await service.GetByMatchIdAsync(matchId);

        Assert.Equal(firstId, secondId);
        Assert.NotNull(result);
        Assert.Equal("u1", result.WinnerUserId);
        Assert.False(result.IsDraw);
        Assert.Equal(2, result.Players.Count);
        Assert.Equal(5, result.Players.Single(player => player.UserId == "u1").Score);
        Assert.Null(await service.GetByMatchIdAsync("missing-" + matchId));
    }

    [Fact]
    public async Task SaveResult_PersistsDrawWithoutWinner()
    {
        await using var db = database.CreateContext();
        var service = new GameResultService(db);
        var matchId = "draw-" + Guid.NewGuid().ToString("N");
        var session = CreateFinishedSession(matchId, "u1", 4, "u2", 4);

        await service.SaveResultAsync(session, session.StartedAt);
        var result = await service.GetByMatchIdAsync(matchId);

        Assert.NotNull(result);
        Assert.True(result.IsDraw);
        Assert.Null(result.WinnerUserId);
    }

    [Fact]
    public async Task HistoryComputesWinsLossesDrawsAndEmptyPlayerStats()
    {
        await using var db = database.CreateContext();
        var resultService = new GameResultService(db);
        var userId = "history-" + Guid.NewGuid().ToString("N");

        foreach (var (matchId, firstScore, secondScore) in new[]
                 {
                     ("win", 5, 3),
                     ("loss", 2, 6),
                     ("draw", 4, 4)
                 })
        {
            var session = CreateFinishedSession(
                $"{matchId}-{userId}",
                userId,
                firstScore,
                "opponent-" + matchId,
                secondScore);
            await resultService.SaveResultAsync(session, session.StartedAt);
        }

        var history = new GameHistoryService(db);
        var stats = await history.GetStatsByPlayerAsync(userId);
        var games = await history.GetHistoryByPlayerAsync(userId);

        Assert.Equal(3, stats.GamesPlayed);
        Assert.Equal(1, stats.Wins);
        Assert.Equal(1, stats.Losses);
        Assert.Equal(1, stats.Draws);
        Assert.Equal(11, stats.TotalPairs);
        Assert.Equal(5, stats.BestScore);
        Assert.Equal(3.67, stats.AverageScore);
        Assert.Equal(3, games.Count);

        var empty = await history.GetStatsByPlayerAsync("nobody-" + userId);
        Assert.Equal(0, empty.GamesPlayed);
        Assert.Equal(0, empty.BestScore);
        Assert.Equal(0, empty.AverageScore);
        Assert.Empty(await history.GetHistoryByPlayerAsync("nobody-" + userId));
    }
}

using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Domain;

namespace BattleHub.Memory.Api.Tests.Services;

public sealed class MemoryGameServiceTests
{
    private static Player FirstPlayer() => new("u1", "Ana");
    private static Player SecondPlayer() => new("u2", "Beto");

    private static Board CreateBoard() =>
        new(["A", "B", "C", "D", "E", "F", "G", "H"]);

    private static void Finish(GameSession session)
    {
        foreach (var pair in session.Board.Cards.GroupBy(card => card.Value))
        {
            session.FlipCard(session.CurrentPlayer.UserId, pair.First().Id);
            session.FlipCard(session.CurrentPlayer.UserId, pair.Last().Id);
        }
    }

    [Fact]
    public void AddPlayer_WaitsForSecondDistinctPlayerAndCreatesOneSession()
    {
        var service = new MemoryGameService();

        var first = service.AddPlayer("m1", FirstPlayer(), out var firstCreated);
        var duplicate = service.AddPlayer("m1", FirstPlayer(), out var duplicateCreated);
        var second = service.AddPlayer("m1", SecondPlayer(), out var secondCreated);

        Assert.Null(first);
        Assert.False(firstCreated);
        Assert.Null(duplicate);
        Assert.False(duplicateCreated);
        Assert.NotNull(second);
        Assert.True(secondCreated);
        Assert.Equal(2, second.Players.Count);
        Assert.True(service.IsPreview("m1"));

        var reconnect = service.AddPlayer("m1", FirstPlayer(), out var reconnectCreated);
        Assert.Same(second, reconnect);
        Assert.False(reconnectCreated);
        Assert.Throws<InvalidOperationException>(
            () => service.AddPlayer("m1", new Player("intruder", "Intruso"), out _));
    }

    [Fact]
    public void SessionLookupsAndRemovalTrackActiveAndWaitingGames()
    {
        var service = new MemoryGameService();
        service.AddPlayer("waiting", FirstPlayer(), out _);
        var session = service.CreateSession("active", [FirstPlayer(), SecondPlayer()]);

        Assert.True(service.HasSession("active"));
        Assert.True(service.TryGetSession("active", out var found));
        Assert.Same(session, found);
        Assert.False(service.TryGetSession("missing", out _));
        Assert.Same(session, service.GetSession("active"));
        Assert.Throws<InvalidOperationException>(() => service.GetSession("missing"));

        service.RemoveSession("active");
        service.RemoveSession("waiting");

        Assert.False(service.HasSession("active"));
        Assert.False(service.TryGetSession("active", out _));
        Assert.Null(service.AddPlayer("waiting", SecondPlayer(), out var created));
        Assert.False(created);
    }

    [Fact]
    public void CreateSessionRequiresExactlyTwoDifferentPlayers()
    {
        var service = new MemoryGameService();

        Assert.Throws<InvalidOperationException>(
            () => service.CreateSession("one", [FirstPlayer()]));
        Assert.Throws<InvalidOperationException>(
            () => service.CreateSession("same", [FirstPlayer(), FirstPlayer()]));
    }

    [Fact]
    public void PreviewAndSavedStateCanBeStartedAndEnded()
    {
        var service = new MemoryGameService();
        service.AddPlayer("m1", FirstPlayer(), out _);
        service.AddPlayer("m1", SecondPlayer(), out _);

        Assert.True(service.IsPreview("m1"));
        service.EndPreview("m1");
        Assert.False(service.IsPreview("m1"));
        Assert.False(service.ResultSaved("m1"));

        service.MarkSaved("m1");
        Assert.True(service.ResultSaved("m1"));
    }

    [Fact]
    public void UnsavedResultsReturnsOnlyFinishedUnmarkedSessions()
    {
        var service = new MemoryGameService();
        var unfinished = service.CreateSession("unfinished", [FirstPlayer(), SecondPlayer()]);
        var finished = service.CreateSession("finished", [FirstPlayer(), SecondPlayer()]);
        Finish(finished);

        Assert.False(unfinished.IsFinished);
        Assert.Collection(
            service.UnsavedResults(),
            result => Assert.Same(finished, result));

        service.MarkSaved("finished");
        Assert.Empty(service.UnsavedResults());
    }
}

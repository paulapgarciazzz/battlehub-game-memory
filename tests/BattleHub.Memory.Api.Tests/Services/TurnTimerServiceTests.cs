using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BattleHub.Memory.Api.Tests.Services;

public sealed class TurnTimerServiceTests
{
    [Fact]
    public void StartSetsDeadlineAndStopClearsIt()
    {
        var service = CreateService();
        var session = CreateSession("timer-stop");
        var beforeStart = DateTimeOffset.UtcNow;

        service.Start(session);

        var deadline = service.Deadline(session.MatchId);
        Assert.NotNull(deadline);
        Assert.InRange(
            deadline.Value,
            beforeStart.AddSeconds(GameSession.TurnTimeoutSeconds),
            DateTimeOffset.UtcNow.AddSeconds(GameSession.TurnTimeoutSeconds));

        service.Stop(session.MatchId);

        Assert.Null(service.Deadline(session.MatchId));
    }

    [Fact]
    public void StartReplacesPreviousTimerForTheSameMatch()
    {
        var service = CreateService();
        var session = CreateSession("timer-replace");

        service.Start(session);
        service.Start(session);

        Assert.NotNull(service.Deadline(session.MatchId));
        service.Stop(session.MatchId);
        Assert.Null(service.Deadline(session.MatchId));
    }

    [Fact]
    public void StartOnFinishedSessionStopsExistingTimer()
    {
        var service = CreateService();
        var session = CreateSession("timer-finished");
        service.Start(session);
        Finish(session);

        service.Start(session);

        Assert.Null(service.Deadline(session.MatchId));
    }

    private static TurnTimerService CreateService() =>
        new(
            Mock.Of<IHubContext<MemoryHub>>(),
            NullLogger<TurnTimerService>.Instance);

    private static GameSession CreateSession(string matchId) =>
        new(
            matchId,
            new Board(["A", "B", "C", "D", "E", "F", "G", "H"]),
            [new Player("u1", "Ana"), new Player("u2", "Beto")]);

    private static void Finish(GameSession session)
    {
        foreach (var pair in session.Board.Cards.GroupBy(card => card.Value))
        {
            session.FlipCard(session.CurrentPlayer.UserId, pair.First().Id);
            session.FlipCard(session.CurrentPlayer.UserId, pair.Last().Id);
        }
    }
}

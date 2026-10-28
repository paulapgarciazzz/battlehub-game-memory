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

    [Fact]
    public async Task ExpiredTurnPassesToTheOtherPlayerAndNotifiesTheGroup()
    {
        var group = new Mock<IClientProxy>();
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        group.Setup(client => client.SendCoreAsync("TurnTimeout", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback(notified.SetResult)
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group("timer-timeout")).Returns(group.Object);
        var hub = new Mock<IHubContext<MemoryHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        var service = new TurnTimerService(hub.Object, NullLogger<TurnTimerService>.Instance);
        var session = CreateSession("timer-timeout");

        service.Start(session);
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(GameSession.TurnTimeoutSeconds + 10));

        Assert.Equal("u2", session.CurrentPlayer.UserId);
        Assert.NotNull(service.Deadline(session.MatchId));
        service.Stop(session.MatchId);
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

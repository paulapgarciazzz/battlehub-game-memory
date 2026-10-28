using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BattleHub.Memory.Api.Tests.Services;

public sealed class MemoryLocalTurnTimerServiceTests
{
    [Fact]
    public void StartSetsDeadlineAndStopClearsIt()
    {
        var (service, games, _) = CreateService();
        var session = CreateSession(games, "local-timer-stop");

        service.Start(session);
        service.Start(session);

        Assert.NotNull(service.Deadline(session.MatchId));
        service.Stop(session.MatchId);
        Assert.Null(service.Deadline(session.MatchId));
    }

    [Fact]
    public void StartOnFinishedSessionStopsExistingTimer()
    {
        var (service, games, _) = CreateService();
        var session = CreateSession(games, "local-timer-finished");
        service.Start(session);
        foreach (var pair in session.Board.Cards.GroupBy(card => card.Value))
        {
            session.FlipCard(session.CurrentPlayer.UserId, pair.First().Id);
            session.FlipCard(session.CurrentPlayer.UserId, pair.Last().Id);
        }

        service.Start(session);

        Assert.Null(service.Deadline(session.MatchId));
    }

    [Fact]
    public async Task ExpiredTurnPassesToTheOtherPlayerAndNotifiesTheGroup()
    {
        var (service, games, group) = CreateService();
        var session = CreateSession(games, "local-timer-timeout");
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        group.Setup(client => client.SendCoreAsync("TurnTimeout", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback(notified.SetResult)
            .Returns(Task.CompletedTask);

        service.Start(session);
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(GameSession.TurnTimeoutSeconds + 10));

        Assert.Equal("u2", session.CurrentPlayer.UserId);
        Assert.NotNull(service.Deadline(session.MatchId));
        service.Stop(session.MatchId);
    }

    private static (MemoryLocalTurnTimerService Service, MemoryLocalGameService Games, Mock<IClientProxy> Group) CreateService()
    {
        var games = new MemoryLocalGameService();
        var group = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        var hub = new Mock<IHubContext<MemoryLocalHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        return (new MemoryLocalTurnTimerService(games, hub.Object, NullLogger<MemoryLocalTurnTimerService>.Instance), games, group);
    }

    private static GameSession CreateSession(MemoryLocalGameService games, string matchId) =>
        games.CreateSession(matchId, [new Player("u1", "Ana"), new Player("u2", "Beto")]);
}

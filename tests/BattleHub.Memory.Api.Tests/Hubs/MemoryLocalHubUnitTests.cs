using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BattleHub.Memory.Api.Tests.Hubs;

public sealed class MemoryLocalHubUnitTests
{
    private sealed record HubHarness(
        MemoryLocalHub Hub,
        MemoryLocalGameService Games,
        MemoryLocalTurnTimerService Timers,
        Mock<IClientProxy> Group,
        Mock<ISingleClientProxy> Caller,
        Mock<IGroupManager> Groups,
        IDictionary<object, object?> Items);

    private static HubHarness CreateHub()
    {
        var games = new MemoryLocalGameService();
        var group = new Mock<IClientProxy>();
        group.Setup(proxy => proxy.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var callerClients = new Mock<IHubCallerClients>();
        callerClients.Setup(item => item.Group(It.IsAny<string>())).Returns(group.Object);
        var caller = new Mock<ISingleClientProxy>();
        caller.Setup(proxy => proxy.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        callerClients.SetupGet(item => item.Caller).Returns(caller.Object);
        var contextClients = new Mock<IHubClients>();
        contextClients.Setup(item => item.Group(It.IsAny<string>())).Returns(group.Object);

        var groups = new Mock<IGroupManager>();
        groups.Setup(item => item.AddToGroupAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hubContext = new Mock<IHubContext<MemoryLocalHub>>();
        hubContext.SetupGet(item => item.Clients).Returns(contextClients.Object);
        var timers = new MemoryLocalTurnTimerService(
            games,
            hubContext.Object,
            NullLogger<MemoryLocalTurnTimerService>.Instance);

        var items = new Dictionary<object, object?>();
        var context = new Mock<HubCallerContext>();
        context.SetupGet(item => item.ConnectionId).Returns("connection-1");
        context.SetupGet(item => item.Items).Returns(items);

        var hub = new MemoryLocalHub(
            games,
            timers,
            NullLogger<MemoryLocalHub>.Instance)
        {
            Context = context.Object,
            Clients = callerClients.Object,
            Groups = groups.Object
        };

        return new HubHarness(hub, games, timers, group, caller, groups, items);
    }

    [Fact]
    public async Task JoinMatchValidatesInputAndAddsFirstPlayerToNormalizedRoom()
    {
        var harness = CreateHub();

        await Assert.ThrowsAsync<HubException>(
            () => harness.Hub.JoinMatch("  ", "u1", "Ana"));
        await Assert.ThrowsAsync<HubException>(
            () => harness.Hub.JoinMatch("room", "", "Ana"));

        await harness.Hub.JoinMatch(" room ", " u1 ", " Ana ");

        Assert.Equal("ROOM", harness.Items["match"]);
        Assert.Equal("u1", harness.Items["userId"]);
        harness.Groups.Verify(item => item.AddToGroupAsync(
            "connection-1",
            "ROOM",
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Group.Verify(proxy => proxy.SendCoreAsync(
            "PlayerJoined",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(harness.Games.HasSession("ROOM"));
    }

    [Fact]
    public async Task SecondPlayerStartsPreviewAndReceivesSnapshot()
    {
        var harness = CreateHub();

        await harness.Hub.JoinMatch("room", "u1", "Ana");
        await harness.Hub.JoinMatch("room", "u2", "Beto");

        Assert.True(harness.Games.HasSession("ROOM"));
        Assert.False(harness.Games.IsPreview("ROOM"));
        Assert.NotNull(harness.Timers.Deadline("ROOM"));
        harness.Group.Verify(proxy => proxy.SendCoreAsync(
            "GameReady",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Group.Verify(proxy => proxy.SendCoreAsync(
            "PreviewFinished",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Caller.Verify(proxy => proxy.SendCoreAsync(
            "StateSnapshot",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        harness.Timers.Stop("ROOM");
    }

    [Fact]
    public async Task FlipCardRejectsUnjoinedMissingAndPreviewMatches()
    {
        var harness = CreateHub();

        harness.Items["match"] = "OTHER";
        harness.Items["userId"] = "u1";
        await Assert.ThrowsAsync<HubException>(
            () => harness.Hub.FlipCard("room", 0));

        harness.Items["match"] = "ROOM";
        harness.Items["userId"] = "u1";
        await Assert.ThrowsAsync<HubException>(
            () => harness.Hub.FlipCard("room", 0));

        harness.Games.AddPlayer("ROOM", new Player("u1", "Ana"), out _);
        harness.Games.AddPlayer("ROOM", new Player("u2", "Beto"), out _);
        await Assert.ThrowsAsync<HubException>(
            () => harness.Hub.FlipCard("room", 0));

        harness.Timers.Stop("ROOM");
    }

    [Fact]
    public async Task FlipCardBroadcastsResolvedTurnAndRejectsAnInvalidCard()
    {
        var harness = CreateHub();
        var session = harness.Games.CreateSession(
            "ROOM",
            [new Player("u1", "Ana"), new Player("u2", "Beto")]);
        harness.Games.EndPreview("ROOM");
        harness.Items["match"] = "ROOM";
        harness.Items["userId"] = "u1";

        await Assert.ThrowsAsync<HubException>(
            () => harness.Hub.FlipCard("room", 99));

        var first = session.Board.Cards[0];
        var different = session.Board.Cards.First(card => card.Value != first.Value);
        await harness.Hub.FlipCard(" room ", first.Id);
        await harness.Hub.FlipCard("ROOM", different.Id);

        harness.Group.Verify(proxy => proxy.SendCoreAsync(
            "CardFlipped",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal("u2", session.CurrentPlayer.UserId);
        Assert.NotNull(harness.Timers.Deadline("ROOM"));

        harness.Timers.Stop("ROOM");
    }
}

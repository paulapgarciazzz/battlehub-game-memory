using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Data.Services;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BattleHub.Memory.UnitTests.Api.Services;

public sealed class ResultRetryWorkerTests
{
    [Fact]
    public async Task SavesFinishedResultsAndKeepsFailedResultsPending()
    {
        var games = new MemoryGameService();
        var failedSession = CreateFinishedSession("retry-failure");
        var savedSession = CreateFinishedSession("retry-success");
        games.CreateSession(failedSession.MatchId, failedSession.Players);
        games.CreateSession(savedSession.MatchId, savedSession.Players);
        Finish(games.GetSession(failedSession.MatchId));
        Finish(games.GetSession(savedSession.MatchId));

        var failedResultAttempted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var savedResultId = Guid.NewGuid();
        var resultService = new Mock<IGameResultService>();
        resultService
            .Setup(service => service.SaveResultAsync(
                It.IsAny<GameSession>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Returns((GameSession session, DateTimeOffset _, CancellationToken _) =>
            {
                if (session.MatchId == failedSession.MatchId)
                {
                    failedResultAttempted.TrySetResult();
                    return Task.FromException<Guid>(new InvalidOperationException("Database unavailable."));
                }

                return Task.FromResult(savedResultId);
            });

        using var provider = new ServiceCollection()
            .AddScoped<IGameResultService>(_ => resultService.Object)
            .BuildServiceProvider();
        var recorder = new MatchResultRecorder(
            provider.GetRequiredService<IServiceScopeFactory>());

        var resultSaved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var group = new Mock<IClientProxy>();
        group.Setup(proxy => proxy.SendCoreAsync(
                "ResultSaved",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => resultSaved.TrySetResult())
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(item => item.Group(savedSession.MatchId)).Returns(group.Object);
        var hub = new Mock<IHubContext<MemoryHub>>();
        hub.SetupGet(item => item.Clients).Returns(clients.Object);

        var worker = new ResultRetryWorker(
            games,
            recorder,
            hub.Object,
            NullLogger<ResultRetryWorker>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));

        await worker.StartAsync(timeout.Token);
        await Task.WhenAll(
            failedResultAttempted.Task.WaitAsync(timeout.Token),
            resultSaved.Task.WaitAsync(timeout.Token));
        await worker.StopAsync(timeout.Token);

        Assert.False(games.ResultSaved(failedSession.MatchId));
        Assert.True(games.ResultSaved(savedSession.MatchId));
        group.Verify(proxy => proxy.SendCoreAsync(
            "ResultSaved",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GameSession CreateFinishedSession(string matchId) =>
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

using BattleHub.Memory.Api.Matchmaking;
using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Domain;
using BattleHub.Memory.Data.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BattleHub.Memory.Api.Tests.Matchmaking;

[Collection(MemoryDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class FinishWorkerTests(MemoryApiFactory factory)
{
    // Matchmaking simulado: responde "outcome" para la partida de la prueba
    // y confirma cualquier otro aviso que quede en la cola compartida.
    private static Mock<IMatchmakingGateway> Gateway(string target, FinishOutcome outcome)
    {
        var gateway = new Mock<IMatchmakingGateway>();
        gateway.Setup(g => g.FinishAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(FinishOutcome.Confirmed);
        gateway.Setup(g => g.FinishAsync(target, It.IsAny<CancellationToken>())).ReturnsAsync(outcome);
        return gateway;
    }

    [Theory]
    [InlineData(FinishOutcome.Pending, false)]
    [InlineData(FinishOutcome.Rejected, true)]
    public async Task QueueSurvivesWorkerRecreationAndDoesNotRetryBlocked(FinishOutcome outcome, bool blocked)
    {
        var id = "retry-" + Guid.NewGuid().ToString("N");
        using var scope = factory.Services.CreateScope();
        var session = new GameSession(id, new Board(["A", "B", "C", "D", "E", "F", "G", "H"]), [new Player("auth0|one", "Uno"), new Player("auth0|two", "Dos")]);
        foreach (var pair in session.Board.Cards.GroupBy(c => c.Value))
        {
            session.FlipCard("auth0|one", pair.First().Id);
            session.FlipCard("auth0|one", pair.Last().Id);
        }
        await scope.ServiceProvider.GetRequiredService<IGameResultService>().SaveResultAsync(session, session.StartedAt);
        factory.Clock.Advance(2);
        var gateway = Gateway(id, outcome);
        var worker = CreateWorker(gateway.Object);
        for (var i = 0; i < 6; i++) await worker.DispatchAsync(default);
        gateway.Verify(g => g.FinishAsync(id, It.IsAny<CancellationToken>()), Times.Once());
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var pending = await db.FinishNotifications.AsNoTracking().SingleAsync(item => item.MatchId == id);
        Assert.Equal(blocked, pending.Blocked);
        Assert.False(pending.Delivered);
        factory.Clock.Advance(31);
        var restoredGateway = Gateway(id, FinishOutcome.Confirmed);
        var restored = CreateWorker(restoredGateway.Object);
        for (var i = 0; i < 6; i++) await restored.DispatchAsync(default);
        restoredGateway.Verify(g => g.FinishAsync(id, It.IsAny<CancellationToken>()), blocked ? Times.Never() : Times.Once());
        Assert.Equal(!blocked, (await db.FinishNotifications.AsNoTracking().SingleAsync(item => item.MatchId == id)).Delivered);
    }

    private FinishWorker CreateWorker(IMatchmakingGateway gateway) => new(factory.Services.GetRequiredService<IServiceScopeFactory>(),
        gateway, factory.Clock, NullLogger<FinishWorker>.Instance);
}
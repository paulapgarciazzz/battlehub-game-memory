using BattleHub.Memory.Api.Matchmaking;
using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Domain;
using BattleHub.Memory.Data.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BattleHub.Memory.IntegrationTests;

[Collection(MemoryDatabaseCollection.Name)]
public sealed class FinishWorkerTests(MemoryApiFactory factory)
{
    private sealed class Gateway(string target, FinishOutcome outcome) : IMatchmakingGateway
    {
        public int Calls;
        public Task<MatchRoom> ValidateAsync(string id, string user, string token, CancellationToken ct, bool allowFinished = false) => throw new NotSupportedException();
        public Task<FinishOutcome> FinishAsync(string id, CancellationToken ct)
        {
            if (id == target) { Calls++; return Task.FromResult(outcome); }
            return Task.FromResult(FinishOutcome.Confirmed);
        }
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
        var gateway = new Gateway(id, outcome);
        var worker = CreateWorker(gateway);
        for (var i = 0; i < 6 && gateway.Calls == 0; i++) await worker.DispatchAsync(default);
        Assert.Equal(1, gateway.Calls);
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var pending = await db.FinishNotifications.AsNoTracking().SingleAsync(item => item.MatchId == id);
        Assert.Equal(blocked, pending.Blocked);
        Assert.False(pending.Delivered);
        factory.Clock.Advance(31);
        var restoredGateway = new Gateway(id, FinishOutcome.Confirmed);
        var restored = CreateWorker(restoredGateway);
        for (var i = 0; i < 6; i++) await restored.DispatchAsync(default);
        Assert.Equal(blocked ? 0 : 1, restoredGateway.Calls);
        Assert.Equal(!blocked, (await db.FinishNotifications.AsNoTracking().SingleAsync(item => item.MatchId == id)).Delivered);
    }

    private FinishWorker CreateWorker(IMatchmakingGateway gateway) => new(factory.Services.GetRequiredService<IServiceScopeFactory>(),
        gateway, factory.Clock, NullLogger<FinishWorker>.Instance);
}
using BattleHub.Memory.Data.DbContext;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.Api.Matchmaking;

public sealed class FinishWorker(IServiceScopeFactory scopes, IMatchmakingGateway gateway, TimeProvider clock, ILogger<FinishWorker> logger) : BackgroundService
{
    public async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var now = clock.GetUtcNow();
        var pending = await db.FinishNotifications.AsNoTracking().Where(item => !item.Delivered && !item.Blocked && item.NextAttemptAt <= now)
            .OrderBy(item => item.NextAttemptAt).Take(4).Select(item => item.MatchId).ToArrayAsync(ct);
        await Task.WhenAll(pending.Select(async id =>
        {
            using var itemScope = scopes.CreateScope();
            var itemDb = itemScope.ServiceProvider.GetRequiredService<MemoryDbContext>();
            var item = await itemDb.FinishNotifications.SingleAsync(row => row.MatchId == id, ct);
            var outcome = await gateway.FinishAsync(id, ct);
            item.Delivered = outcome == FinishOutcome.Confirmed;
            item.Blocked = outcome == FinishOutcome.Rejected;
            item.Attempts++;
            item.NextAttemptAt = clock.GetUtcNow().AddSeconds(30);
            await itemDb.SaveChangesAsync(ct);
            if (!item.Delivered) logger.LogWarning("Callback pendiente para {MatchId}", id);
        }));
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await DispatchAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) { logger.LogWarning("No se pudo procesar la cola de finalización; se reintentará."); await Task.Delay(TimeSpan.FromSeconds(5), ct); }
        }
    }
}

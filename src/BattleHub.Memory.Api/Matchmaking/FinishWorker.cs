using BattleHub.Memory.Data.DbContext;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.Api.Matchmaking;

/// <summary>
/// Cola persistente de avisos de fin de partida (ADR-004).
///
/// Cada resultado guardado deja una fila en FinishNotifications. Este worker
/// la manda a Matchmaking y anota el resultado:
/// - Delivered = true: Matchmaking confirmó el cierre.
/// - Blocked = true: Matchmaking lo rechazó de forma permanente; hay que
///   revisar la causa (ver docs/integracion-shell-auth0.md) antes de reactivarla.
/// - En otro caso se reintenta 30 segundos después.
///
/// Como los avisos están en la BD, sobreviven a un reinicio de la API.
/// </summary>
public sealed class FinishWorker(
    IServiceScopeFactory scopes,
    IMatchmakingGateway gateway,
    TimeProvider clock,
    ILogger<FinishWorker> logger) : BackgroundService
{
    /// <summary>
    /// Procesa hasta 4 avisos pendientes cuyo próximo intento ya llegó.
    /// Es público para poder probarlo sin esperar al temporizador.
    /// </summary>
    public async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var now = clock.GetUtcNow();

        var pending = await db.FinishNotifications
            .AsNoTracking()
            .Where(item => !item.Delivered && !item.Blocked && item.NextAttemptAt <= now)
            .OrderBy(item => item.NextAttemptAt)
            .Take(4)
            .Select(item => item.MatchId)
            .ToArrayAsync(ct);

        // Cada aviso usa su propio DbContext porque se procesan en paralelo.
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

            if (!item.Delivered)
                logger.LogWarning("Callback pendiente para {MatchId}", id);
        }));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Revisa la cola cada segundo.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await DispatchAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Por ejemplo, la BD no está disponible: se espera un poco y se sigue.
                logger.LogWarning(ex, "No se pudo procesar la cola de finalización; se reintentará.");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}

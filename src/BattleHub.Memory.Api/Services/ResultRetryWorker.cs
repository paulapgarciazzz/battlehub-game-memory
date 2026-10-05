using BattleHub.Memory.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Services;

/// <summary>
/// Reintenta guardar los resultados de partidas terminadas que no se
/// pudieron guardar al finalizar (por ejemplo, porque la BD no respondía).
///
/// Solo funciona mientras la partida siga en memoria: si la API se reinicia
/// antes de guardar, ese resultado se pierde.
/// </summary>
public sealed class ResultRetryWorker(
    MemoryGameService games,
    MatchResultRecorder recorder,
    IHubContext<MemoryHub> hub,
    ILogger<ResultRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(ct))
        {
            foreach (var session in games.UnsavedResults())
            {
                try
                {
                    await recorder.RecordAsync(session, ct);
                    games.MarkSaved(session.MatchId);

                    // La pantalla solo dice "guardado" cuando recibe este evento.
                    await hub.Clients.Group(session.MatchId).SendAsync("ResultSaved", new { session.MatchId }, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Resultado pendiente de guardar para {MatchId}.", session.MatchId);
                }
            }
        }
    }
}

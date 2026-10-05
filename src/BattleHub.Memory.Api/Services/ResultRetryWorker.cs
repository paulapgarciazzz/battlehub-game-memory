using BattleHub.Memory.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
namespace BattleHub.Memory.Api.Services;
public sealed class ResultRetryWorker(MemoryGameService games, MatchResultRecorder recorder, IHubContext<MemoryHub> hub, ILogger<ResultRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(ct))
            foreach (var session in games.UnsavedResults())
                try
                {
                    await recorder.RecordAsync(session, ct);
                    games.MarkSaved(session.MatchId);
                    await hub.Clients.Group(session.MatchId).SendAsync("ResultSaved", new { session.MatchId }, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception) { logger.LogWarning("Resultado pendiente de guardar para {MatchId}.", session.MatchId); }
    }
}

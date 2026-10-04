using System.Collections.Concurrent;
using BattleHub.Memory.Data.Services;
using BattleHub.Memory.Domain;

namespace BattleHub.Memory.Api.Services;

/// <summary>
/// Guarda el resultado de una partida terminada. Según el contrato 04, el
/// resultado lo guarda el backend del juego (no el microfrontend). Es
/// singleton y usa un SemaphoreSlim por partida para que dos llamadas casi
/// simultáneas (el Hub y un POST /results) no inserten el resultado dos veces.
/// </summary>
public class MatchResultRecorder
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ConcurrentDictionary<
        string,
        SemaphoreSlim> _locks = new();

    public MatchResultRecorder(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Guarda el resultado y devuelve su Id. Si ya estaba guardado,
    /// devuelve el Id existente sin insertar otro.
    /// </summary>
    public async Task<Guid> RecordAsync(
        GameSession session,
        CancellationToken ct = default)
    {
        var matchLock = _locks.GetOrAdd(
            session.MatchId,
            _ => new SemaphoreSlim(1, 1));

        await matchLock.WaitAsync(ct);

        try
        {
            // IGameResultService es scoped (usa el DbContext), así que se
            // resuelve en un scope propio en vez de inyectarlo al singleton.
            await using var scope = _scopeFactory.CreateAsyncScope();

            var gameResultService = scope.ServiceProvider
                .GetRequiredService<IGameResultService>();

            return await gameResultService.SaveResultAsync(
                session,
                session.StartedAt,
                ct);
        }
        finally
        {
            matchLock.Release();
        }
    }
}

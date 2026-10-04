using System.Collections.Concurrent;
using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Services;

/// <summary>
/// Temporizador de turno de cada partida. Es singleton porque el Hub es
/// transient: un diccionario de timers guardado en el Hub arranca vacío en
/// cada llamada (los timers viejos nunca se cancelaban), y el Hub no puede
/// usar Clients después de que terminó la llamada (TurnTimeout nunca
/// llegaba a los clientes). Por eso se usa IHubContext.
/// </summary>
public class TurnTimerService
{
    private readonly IHubContext<MemoryHub> _hubContext;
    private readonly ILogger<TurnTimerService> _logger;

    // Guarda un temporizador por cada partida.
    private readonly ConcurrentDictionary<
        string,
        CancellationTokenSource> _turnTimers = new();

    public TurnTimerService(
        IHubContext<MemoryHub> hubContext,
        ILogger<TurnTimerService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Inicia el temporizador de 10 segundos para el turno actual,
    /// reemplazando (y cancelando) el temporizador anterior de la partida.
    /// </summary>
    public void Start(GameSession session)
    {
        if (session.IsFinished)
        {
            Stop(session.MatchId);
            return;
        }

        // Se guarda el número de turno al armar el timer: si cuando se
        // cumplen los 10 segundos el turno ya cambió, no se quita nada.
        var expectedTurnNumber = session.TurnNumber;

        var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationTokenSource? oldTimer = null;

        _turnTimers.AddOrUpdate(
            session.MatchId,
            cancellationTokenSource,
            (_, existing) =>
            {
                oldTimer = existing;
                return cancellationTokenSource;
            });

        // El Dispose lo hace RunTurnTimer del timer viejo al terminar.
        if (oldTimer is not null)
        {
            CancelTimer(oldTimer);
        }

        _ = RunTurnTimer(
            session,
            expectedTurnNumber,
            cancellationTokenSource);
    }

    /// <summary>
    /// Detiene el temporizador de la partida, si existe.
    /// </summary>
    public void Stop(string matchId)
    {
        if (_turnTimers.TryRemove(
            matchId,
            out var timer))
        {
            CancelTimer(timer);
        }
    }

    private static void CancelTimer(
        CancellationTokenSource timer)
    {
        try
        {
            timer.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // El timer ya había terminado y se liberó: no hay nada que cancelar.
        }
    }

    /// <summary>
    /// Espera 10 segundos y cambia el turno
    /// si el jugador no realizó la jugada.
    /// </summary>
    private async Task RunTurnTimer(
        GameSession session,
        int expectedTurnNumber,
        CancellationTokenSource cancellationTokenSource)
    {
        var matchId = session.MatchId;

        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(
                    GameSession.TurnTimeoutSeconds),
                cancellationTokenSource.Token);

            // Si el turno ya cambió o la partida terminó, este timer
            // quedó viejo y no debe tocar nada.
            if (!session.TryForfeitTurnByTimeout(
                expectedTurnNumber,
                out var timeout))
            {
                return;
            }

            await _hubContext.Clients.Group(matchId).SendAsync(
                "TurnTimeout",
                new
                {
                    MatchId = matchId,
                    timeout.PreviousPlayerId,
                    timeout.CurrentPlayerId,
                    TurnTimeoutSeconds =
                        GameSession.TurnTimeoutSeconds
                });

            // Iniciamos los 10 segundos para el siguiente jugador.
            Start(session);
        }
        catch (OperationCanceledException)
        {
            // El temporizador fue cancelado porque
            // el jugador realizó una jugada válida.
        }
        catch (Exception ex)
        {
            // Corre fuera de un request: si no se captura,
            // la excepción se pierde sin dejar rastro.
            _logger.LogError(
                ex,
                "Error en el temporizador de turno de la partida {MatchId}.",
                matchId);
        }
        finally
        {
            // Solo se quita del diccionario si sigue siendo este timer
            // (Start pudo haberlo reemplazado por uno nuevo).
            _turnTimers.TryRemove(
                new KeyValuePair<string, CancellationTokenSource>(
                    matchId,
                    cancellationTokenSource));

            cancellationTokenSource.Dispose();
        }
    }
}

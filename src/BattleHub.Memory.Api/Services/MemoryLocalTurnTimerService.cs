
using System.Collections.Concurrent;
using BattleHub.Memory.Api.Hubs;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Services;

public sealed class MemoryLocalTurnTimerService
{
    private readonly MemoryLocalGameService _games;
    private readonly IHubContext<MemoryLocalHub> _hub;
    private readonly ILogger<MemoryLocalTurnTimerService> _logger;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _deadlines = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _timers = new();

    public MemoryLocalTurnTimerService(
        MemoryLocalGameService games,
        IHubContext<MemoryLocalHub> hub,
        ILogger<MemoryLocalTurnTimerService> logger)
    {
        _games = games;
        _hub = hub;
        _logger = logger;
    }

    public DateTimeOffset? Deadline(string matchId) =>
        _deadlines.TryGetValue(matchId, out var deadline) ? deadline : null;

    public void Start(GameSession session)
    {
        if (session.IsFinished)
        {
            Stop(session.MatchId);
            return;
        }

        var matchId = session.MatchId;
        var expectedTurn = session.TurnNumber;
        var cts = new CancellationTokenSource();

        _deadlines[matchId] =
            DateTimeOffset.UtcNow.AddSeconds(GameSession.TurnTimeoutSeconds);

        var previous = _timers.AddOrUpdate(
            matchId,
            cts,
            (_, old) => cts);

        if (previous != cts)
        {
            try { previous.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        _ = RunTimer(session, expectedTurn, cts);
    }

    public void Stop(string matchId)
    {
        _deadlines.TryRemove(matchId, out _);

        if (_timers.TryRemove(matchId, out var cts))
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    private async Task RunTimer(
        GameSession session,
        int expectedTurn,
        CancellationTokenSource cts)
    {
        var matchId = session.MatchId;

        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(GameSession.TurnTimeoutSeconds),
                cts.Token);

            if (!_games.TryGetSession(matchId, out var currentSession) ||
                !ReferenceEquals(currentSession, session) ||
                !session.TryForfeitTurnByTimeout(expectedTurn, out var timeout))
            {
                return;
            }

            Start(session);

            await _hub.Clients.Group(matchId).SendAsync(
                "TurnTimeout",
                new
                {
                    MatchId = matchId,
                    TurnDeadline = Deadline(matchId),
                    timeout.PreviousPlayerId,
                    timeout.CurrentPlayerId,
                    TurnTimeoutSeconds = GameSession.TurnTimeoutSeconds
                });
        }
        catch (OperationCanceledException)
        {
            // El turno cambió antes de que venciera el temporizador.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el temporizador local {MatchId}.", matchId);
        }
        finally
        {
            _timers.TryRemove(
                new KeyValuePair<string, CancellationTokenSource>(matchId, cts));

            cts.Dispose();
        }
    }
}

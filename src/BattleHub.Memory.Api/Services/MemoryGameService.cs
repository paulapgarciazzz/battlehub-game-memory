using System.Diagnostics.CodeAnalysis;
using BattleHub.Memory.Domain;

namespace BattleHub.Memory.Api.Services;

public class MemoryGameService
{
    private readonly object _lock = new();

    // Partidas que están en la previsualización inicial (no se puede jugar).
    private readonly HashSet<string> _preview = new();

    // Partidas terminadas cuyo resultado ya quedó guardado en la BD.
    private readonly HashSet<string> _saved = new();

    private readonly Dictionary<string, GameSession> _sessions = new();
    private readonly Dictionary<string, List<Player>> _waitingPlayers = new();

    /// <summary>
    /// Agrega un jugador a la partida. sessionCreated es true solo cuando
    /// esta llamada fue la que creó la partida (llegó el segundo jugador);
    /// si la partida ya existía (ej. alguien se reconecta) es false, para
    /// que el Hub no repita la previsualización.
    /// </summary>
    public GameSession? AddPlayer(
        string matchId,
        Player player,
        out bool sessionCreated)
    {
        sessionCreated = false;

        // Dos jugadores pueden llamar a JoinMatch casi al mismo tiempo;
        // sin lock, dos hilos podrían leer _waitingPlayers a la vez y
        // ninguno vería al otro jugador recién agregado.
        lock (_lock)
        {
            // Si la partida ya existe, no necesitamos crearla otra vez.
            if (_sessions.TryGetValue(matchId, out var existingSession))
            {
                // Solo pueden volver a entrar los dos jugadores de la partida.
                if (!existingSession.Players.Any(p => p.UserId == player.UserId))
                    throw new InvalidOperationException("PLAYER_NOT_IN_MATCH");

                return existingSession;
            }

            // Si todavía no existe una lista de jugadores para este match,
            // la creamos.
            if (!_waitingPlayers.TryGetValue(matchId, out var players))
            {
                players = new List<Player>();
                _waitingPlayers[matchId] = players;
            }

            // Evitar agregar dos veces al mismo jugador.
            if (players.All(p => p.UserId != player.UserId))
            {
                players.Add(player);
            }

            // GameSession necesita mínimo 2 jugadores.
            if (players.Count < 2)
            {
                return null;
            }

            // Ya tenemos los 2 jugadores, entonces creamos la partida.
            var session = CreateSession(matchId, players);

            // Ya no necesitamos mantenerlos como jugadores esperando.
            _waitingPlayers.Remove(matchId);

            // La partida arranca en previsualización; el Hub la termina.
            _preview.Add(matchId);
            sessionCreated = true;

            return session;
        }
    }

    public bool IsPreview(string id)
    {
        lock (_lock) return _preview.Contains(id);
    }

    public void EndPreview(string id)
    {
        lock (_lock) _preview.Remove(id);
    }

    public bool ResultSaved(string id)
    {
        lock (_lock) return _saved.Contains(id);
    }

    public void MarkSaved(string id)
    {
        lock (_lock) _saved.Add(id);
    }

    /// <summary>
    /// Partidas terminadas cuyo resultado todavía no se guardó. Las usa
    /// ResultRetryWorker para reintentar el guardado.
    /// </summary>
    public GameSession[] UnsavedResults()
    {
        lock (_lock)
        {
            return _sessions.Values
                .Where(s => s.IsFinished && !_saved.Contains(s.MatchId))
                .ToArray();
        }
    }

    public GameSession CreateSession(
        string matchId,
        IReadOnlyList<Player> players)
    {
        // Memory es de exactamente 2 jugadores distintos.
        if (players.Count != 2 || players.Select(p => p.UserId).Distinct().Count() != 2)
            throw new InvalidOperationException("Memory requiere dos participantes distintos.");

        var cardValues = new[]
        {
            "A", "B", "C", "D",
            "E", "F", "G", "H"
        };

        var board = new Board(cardValues);

        var session = new GameSession(
            matchId,
            board,
            players);

        lock (_lock)
        {
            _sessions[matchId] = session;
        }

        return session;
    }

    public GameSession GetSession(string matchId)
    {
        if (!TryGetSession(matchId, out var session))
        {
            throw new InvalidOperationException(
                $"No existe una partida activa con el matchId '{matchId}'.");
        }

        return session;
    }

    public bool TryGetSession(
        string matchId,
        [NotNullWhen(true)] out GameSession? session)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(matchId, out session);
        }
    }

    public bool HasSession(string matchId)
    {
        lock (_lock)
        {
            return _sessions.ContainsKey(matchId);
        }
    }

    public void RemoveSession(string matchId)
    {
        lock (_lock)
        {
            _sessions.Remove(matchId);
            _waitingPlayers.Remove(matchId);
        }
    }
}

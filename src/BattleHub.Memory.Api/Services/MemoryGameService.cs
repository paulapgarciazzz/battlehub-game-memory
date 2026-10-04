using System.Diagnostics.CodeAnalysis;
using BattleHub.Memory.Domain;

namespace BattleHub.Memory.Api.Services;

public class MemoryGameService
{
    private readonly object _lock = new();
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

            sessionCreated = true;

            return session;
        }
    }

    public GameSession CreateSession(
        string matchId,
        IReadOnlyList<Player> players)
    {
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

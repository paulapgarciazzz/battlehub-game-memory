using BattleHub.Memory.Domain;

namespace BattleHub.Memory.Api.Services;

public class MemoryGameService
{
    private readonly Dictionary<string, GameSession> _sessions = new();
    private readonly Dictionary<string, List<Player>> _waitingPlayers = new();

    public GameSession? AddPlayer(
        string matchId,
        Player player)
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

        return session;
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

        _sessions[matchId] = session;

        return session;
    }

    public GameSession GetSession(string matchId)
    {
        if (!_sessions.TryGetValue(matchId, out var session))
        {
            throw new InvalidOperationException(
                $"No existe una partida activa con el matchId '{matchId}'.");
        }

        return session;
    }

    public bool HasSession(string matchId)
    {
        return _sessions.ContainsKey(matchId);
    }

    public void RemoveSession(string matchId)
    {
        _sessions.Remove(matchId);
        _waitingPlayers.Remove(matchId);
    }
}
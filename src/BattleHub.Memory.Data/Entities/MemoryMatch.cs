namespace BattleHub.Memory.Data.Entities;

public class MemoryMatch
{
    public Guid Id { get; set; }

    // Identificador de la partida que viene de Matchmaking
    public string MatchId { get; set; } = string.Empty;

    // Fecha y hora de inicio de la partida
    public DateTimeOffset StartedAt { get; set; }

    // Fecha y hora de finalización
    public DateTimeOffset? FinishedAt { get; set; }

    // Usuario ganador
    public string? WinnerUserId { get; set; }

    // Jugadores de la partida
    public List<MemoryPlayer> Players { get; set; } = new();

    // Cartas de la partida
    public List<MemoryCard> Cards { get; set; } = new();
}
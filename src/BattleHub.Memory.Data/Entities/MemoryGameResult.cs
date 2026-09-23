namespace BattleHub.Memory.Data.Entities;

public class MemoryGameResult
{
    public Guid Id { get; set; }

    // Identificador de la partida
    public string MatchId { get; set; } = string.Empty;

    // Tipo de juego
    public string GameType { get; set; } = "memory";

    // Fecha y hora de inicio
    public DateTimeOffset StartedAt { get; set; }

    // Fecha y hora de finalización
    public DateTimeOffset FinishedAt { get; set; }

    // Usuario ganador
    public string? WinnerUserId { get; set; }

    // Información adicional específica de Memory
    public string? Metadata { get; set; }

    // Jugadores que participaron
    public List<MemoryGameResultPlayer> Players { get; set; } = new();
}
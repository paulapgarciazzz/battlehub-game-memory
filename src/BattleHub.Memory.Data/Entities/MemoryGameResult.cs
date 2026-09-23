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

    // Usuario ganador (null si IsDraw es true, o si la partida no tuvo ganador por otra razón)
    public string? WinnerUserId { get; set; }

    // Explícito en vez de inferirlo de WinnerUserId == null,
    // para distinguir empate de otros casos futuros ( partida cancelada).
    public bool IsDraw { get; set; }

    // Información adicional específica de Memory
    public string? Metadata { get; set; }

    // Jugadores que participaron
    public List<MemoryGameResultPlayer> Players { get; set; } = new();
}
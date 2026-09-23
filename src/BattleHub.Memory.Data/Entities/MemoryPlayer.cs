namespace BattleHub.Memory.Data.Entities;

public class MemoryPlayer
{
    public Guid Id { get; set; }

    // Partida a la que pertenece el jugador
    public string MatchId { get; set; } = string.Empty;

    // Identificador del usuario de BattleHub
    public string UserId { get; set; } = string.Empty;

    // Nombre que se muestra en el juego
    public string DisplayName { get; set; } = string.Empty;

    // Puntaje obtenido
    public int Score { get; set; }

    // Relación con la partida
    public MemoryMatch? Match { get; set; }
}
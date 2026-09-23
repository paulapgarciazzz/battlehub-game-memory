namespace BattleHub.Memory.Data.Entities;

public class MemoryCard
{
    public Guid Id { get; set; }

    // Partida a la que pertenece la carta
    public string MatchId { get; set; } = string.Empty;

    // Posición de la carta en el tablero
    public int Position { get; set; }

    // Identificador de la pareja a la que pertenece
    public int PairId { get; set; }

    // Indica si la carta está actualmente descubierta
    public bool IsRevealed { get; set; }

    // Indica si la pareja de esta carta ya fue encontrada
    public bool IsMatched { get; set; }

    // Relación con la partida
    public MemoryMatch? Match { get; set; }
}
namespace BattleHub.Memory.Data.Entities;

/// <summary>
/// Aviso pendiente para Matchmaking de que una partida terminó (ADR-004).
/// Se guarda en la misma transacción que el resultado, así que nunca queda
/// un resultado sin su aviso ni un aviso sin resultado.
/// </summary>
public sealed class FinishNotification
{
    // Partida a la que corresponde el aviso (una fila por partida).
    public string MatchId { get; set; } = "";

    // Matchmaking confirmó el cierre de la sala.
    public bool Delivered { get; set; }

    // Matchmaking lo rechazó de forma permanente; requiere revisión manual.
    public bool Blocked { get; set; }

    // Cantidad de veces que se intentó enviar.
    public int Attempts { get; set; }

    // Cuándo se puede volver a intentar (UTC).
    public DateTimeOffset NextAttemptAt { get; set; }
}

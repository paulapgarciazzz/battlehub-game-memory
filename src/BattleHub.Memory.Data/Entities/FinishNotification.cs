namespace BattleHub.Memory.Data.Entities;
public sealed class FinishNotification
{
    public string MatchId { get; set; } = "";
    public bool Delivered { get; set; }
    public bool Blocked { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
}

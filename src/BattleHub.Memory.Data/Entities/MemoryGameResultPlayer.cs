namespace BattleHub.Memory.Data.Entities;

public class MemoryGameResultPlayer
{
    public Guid Id { get; set; }

    public Guid GameResultId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int Score { get; set; }

    public MemoryGameResult? GameResult { get; set; }
}
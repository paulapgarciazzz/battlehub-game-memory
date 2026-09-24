namespace BattleHub.Memory.Api.DTOs;

public class MemoryGameHistoryDto
{
    public string MatchId { get; set; } = string.Empty;

    public string GameType { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public string? WinnerUserId { get; set; }

    public bool IsDraw { get; set; }

    public int Score { get; set; }
}
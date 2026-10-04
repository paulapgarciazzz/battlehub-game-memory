namespace BattleHub.Memory.Api.DTOs;

public class MemoryGameResultDto
{
    public Guid ResultId { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public string GameType { get; set; } = string.Empty;

    public List<MemoryGameResultPlayerDto> Players { get; set; } = new();

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public string? WinnerUserId { get; set; }

    public bool IsDraw { get; set; }

    public string? Metadata { get; set; }
}

public class MemoryGameResultPlayerDto
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int Score { get; set; }
}

namespace BattleHub.Memory.Api.DTOs;

public class MemoryPlayerStatsDto
{
    public string UserId { get; set; } = string.Empty;

    public int GamesPlayed { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Draws { get; set; }

    // Total de parejas encontradas en todas las partidas
    public int TotalPairs { get; set; }

    public int BestScore { get; set; }

    public double AverageScore { get; set; }
}

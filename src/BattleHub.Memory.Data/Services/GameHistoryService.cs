using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.Data.Services;

/// <summary>
/// Estadísticas agregadas de un jugador en Memory.
/// </summary>
public record MemoryPlayerStats(
    string UserId,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Draws,
    int TotalPairs,
    int BestScore,
    double AverageScore);

public interface IGameHistoryService
{
    Task<List<MemoryGameResultPlayer>> GetHistoryByPlayerAsync(
        string userId,
        CancellationToken ct = default);

    Task<MemoryPlayerStats> GetStatsByPlayerAsync(
        string userId,
        CancellationToken ct = default);
}

public class GameHistoryService : IGameHistoryService
{
    private readonly MemoryDbContext _db;

    public GameHistoryService(MemoryDbContext db)
    {
        _db = db;
    }

    public async Task<List<MemoryGameResultPlayer>> GetHistoryByPlayerAsync(
        string userId,
        CancellationToken ct = default)
    {
        return await _db.GameResultPlayers
            .AsNoTracking()
            .Include(p => p.GameResult)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.GameResult!.FinishedAt)
            .ToListAsync(ct);
    }

    public async Task<MemoryPlayerStats> GetStatsByPlayerAsync(
        string userId,
        CancellationToken ct = default)
    {
        // Solo se traen las columnas necesarias; el cálculo se hace en memoria.
        var games = await _db.GameResultPlayers
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new
            {
                p.Score,
                p.GameResult!.IsDraw,
                p.GameResult.WinnerUserId
            })
            .ToListAsync(ct);

        var gamesPlayed = games.Count;
        var wins = games.Count(g => !g.IsDraw && g.WinnerUserId == userId);
        var draws = games.Count(g => g.IsDraw);

        return new MemoryPlayerStats(
            userId,
            gamesPlayed,
            wins,
            gamesPlayed - wins - draws,
            draws,
            games.Sum(g => g.Score),
            gamesPlayed == 0 ? 0 : games.Max(g => g.Score),
            gamesPlayed == 0 ? 0 : Math.Round(games.Average(g => g.Score), 2));
    }
}

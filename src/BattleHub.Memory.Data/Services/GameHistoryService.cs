using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.Data.Services;

public interface IGameHistoryService
{
    Task<List<MemoryGameResultPlayer>> GetHistoryByPlayerAsync(
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
            .Include(p => p.GameResult)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.GameResult!.FinishedAt)
            .ToListAsync(ct);
    }
}
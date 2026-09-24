using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Data.Entities;
using BattleHub.Memory.Domain;

namespace BattleHub.Memory.Data.Services;

public interface IGameResultService
{
    Task<Guid> SaveResultAsync(GameSession session, DateTimeOffset startedAt, CancellationToken ct = default);
}

public class GameResultService : IGameResultService
{
    private readonly MemoryDbContext _db;

    public GameResultService(MemoryDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> SaveResultAsync(GameSession session, DateTimeOffset startedAt, CancellationToken ct = default)
    {
        if (!session.IsFinished)
            throw new InvalidOperationException("Solo se puede guardar el resultado de una partida finalizada.");

        var winners = session.GetWinners();
        var isDraw = winners.Count > 1;
        var winnerUserId = isDraw ? null : winners[0].UserId;

        var result = new MemoryGameResult
        {
            Id = Guid.NewGuid(),
            MatchId = session.MatchId,
            GameType = "memory",
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            WinnerUserId = winnerUserId,
            IsDraw = isDraw,
            Players = session.Players.Select(p => new MemoryGameResultPlayer
            {
                Id = Guid.NewGuid(),
                UserId = p.UserId,
                DisplayName = p.DisplayName,
                Score = p.MatchedPairs
            }).ToList()
        };

        _db.GameResults.Add(result);
        await _db.SaveChangesAsync(ct);

        return result.Id;
    }
}
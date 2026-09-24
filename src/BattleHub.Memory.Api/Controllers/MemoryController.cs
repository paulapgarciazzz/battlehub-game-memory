using BattleHub.Memory.Api.DTOs;
using BattleHub.Memory.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace BattleHub.Memory.Api.Controllers;

[ApiController]
[Route("api/games/memory")]
public class MemoryController : ControllerBase
{
    private readonly IGameHistoryService _historyService;

    public MemoryController(IGameHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet("history/{userId}")]
    public async Task<ActionResult<List<MemoryGameHistoryDto>>> GetHistory(
        string userId,
        CancellationToken ct)
    {
        var history = await _historyService.GetHistoryByPlayerAsync(userId, ct);

        var result = history.Select(p => new MemoryGameHistoryDto
        {
            MatchId = p.GameResult!.MatchId,
            GameType = p.GameResult.GameType,
            StartedAt = p.GameResult.StartedAt,
            FinishedAt = p.GameResult.FinishedAt,
            WinnerUserId = p.GameResult.WinnerUserId,
            IsDraw = p.GameResult.IsDraw,
            Score = p.Score
        }).ToList();

        return Ok(result);
    }
}
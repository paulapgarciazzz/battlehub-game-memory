using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace BattleHub.Memory.Api.Controllers;

[ApiController]
[Route("api/games/memory")]
public class MemoryGameResultsController : ControllerBase
{
    private readonly MemoryGameService _gameService;
    private readonly IGameResultService _gameResultService;

    public MemoryGameResultsController(
        MemoryGameService gameService,
        IGameResultService gameResultService)
    {
        _gameService = gameService;
        _gameResultService = gameResultService;
    }

    [HttpPost("results")]
    public async Task<IActionResult> SaveResult(
        [FromBody] SaveMemoryResultRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.MatchId))
        {
            return BadRequest(new
            {
                Message = "El matchId es obligatorio."
            });
        }

        var session = _gameService.GetSession(request.MatchId);

        if (!session.IsFinished)
        {
            return BadRequest(new
            {
                Message = "La partida todavía no ha terminado."
            });
        }

        var resultId = await _gameResultService.SaveResultAsync(
            session,
            request.StartedAt,
            ct);

        return Ok(new
        {
            ResultId = resultId,
            MatchId = session.MatchId,
            Message = "Resultado de la partida guardado correctamente."
        });
    }
}

public class SaveMemoryResultRequest
{
    public string MatchId { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }
}
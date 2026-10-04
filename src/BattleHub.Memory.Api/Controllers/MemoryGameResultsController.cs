using BattleHub.Memory.Api.DTOs;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace BattleHub.Memory.Api.Controllers;

[ApiController]
[Route("api/games/memory")]
public class MemoryGameResultsController : ControllerBase
{
    private readonly MemoryGameService _gameService;
    private readonly MatchResultRecorder _matchResultRecorder;
    private readonly IGameResultService _gameResultService;

    public MemoryGameResultsController(
        MemoryGameService gameService,
        MatchResultRecorder matchResultRecorder,
        IGameResultService gameResultService)
    {
        _gameService = gameService;
        _matchResultRecorder = matchResultRecorder;
        _gameResultService = gameResultService;
    }

    /// <summary>
    /// Registra el resultado de una partida finalizada. El Hub ya lo guarda
    /// al terminar la partida; este endpoint es idempotente, así que llamarlo
    /// de nuevo devuelve el mismo resultado sin duplicarlo.
    /// </summary>
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

        if (!_gameService.TryGetSession(request.MatchId, out var session))
        {
            // La partida ya no está en memoria (ej. el servicio se
            // reinició), pero su resultado pudo haberse guardado antes.
            var savedResult = await _gameResultService.GetByMatchIdAsync(
                request.MatchId,
                ct);

            if (savedResult is null)
            {
                return NotFound(new
                {
                    Message = $"No existe una partida con el matchId '{request.MatchId}'."
                });
            }

            return Ok(new
            {
                ResultId = savedResult.Id,
                MatchId = savedResult.MatchId,
                Message = "El resultado de la partida ya estaba guardado."
            });
        }

        if (!session.IsFinished)
        {
            return BadRequest(new
            {
                Message = "La partida todavía no ha terminado."
            });
        }

        var resultId = await _matchResultRecorder.RecordAsync(
            session,
            ct);

        return Ok(new
        {
            ResultId = resultId,
            MatchId = session.MatchId,
            Message = "Resultado de la partida guardado correctamente."
        });
    }

    [HttpGet("results/{matchId}")]
    public async Task<ActionResult<MemoryGameResultDto>> GetResult(
        string matchId,
        CancellationToken ct)
    {
        var result = await _gameResultService.GetByMatchIdAsync(matchId, ct);

        if (result is null)
        {
            return NotFound(new
            {
                Message = $"No existe un resultado para el matchId '{matchId}'."
            });
        }

        return Ok(new MemoryGameResultDto
        {
            ResultId = result.Id,
            MatchId = result.MatchId,
            GameType = result.GameType,
            Players = result.Players.Select(p => new MemoryGameResultPlayerDto
            {
                UserId = p.UserId,
                DisplayName = p.DisplayName,
                Score = p.Score
            }).ToList(),
            StartedAt = result.StartedAt,
            FinishedAt = result.FinishedAt,
            WinnerUserId = result.WinnerUserId,
            IsDraw = result.IsDraw,
            Metadata = result.Metadata
        });
    }
}

public class SaveMemoryResultRequest
{
    public string MatchId { get; set; } = string.Empty;

    // La hora de inicio ya no viene del cliente: la registra el servidor
    // en GameSession.StartedAt. Si el cliente la manda, se ignora.
}

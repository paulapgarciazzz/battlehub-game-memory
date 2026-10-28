using BattleHub.Memory.Api.Controllers;
using BattleHub.Memory.Api.DTOs;
using BattleHub.Memory.Data.Entities;
using BattleHub.Memory.Data.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BattleHub.Memory.Api.Tests.Controllers;

public sealed class MemoryControllerUnitTests
{
    [Fact]
    public async Task GetHistoryMapsResults()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var finishedAt = startedAt.AddMinutes(1);
        var gameResult = new MemoryGameResult
        {
            MatchId = "match-1",
            GameType = "memory",
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            WinnerUserId = "player-1",
            IsDraw = false
        };
        var playerResult = new MemoryGameResultPlayer
        {
            UserId = "player-1",
            Score = 4,
            GameResult = gameResult
        };
        var historyService = new Mock<IGameHistoryService>();
        historyService.Setup(service =>
                service.GetHistoryByPlayerAsync("player-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([playerResult]);
        var controller = CreateController(historyService.Object);

        var response = await controller.GetHistory("player-1", default);

        var history = Assert.IsType<OkObjectResult>(response.Result).Value
            as List<MemoryGameHistoryDto>;
        var item = Assert.Single(history!);
        Assert.Equal("match-1", item.MatchId);
        Assert.Equal("memory", item.GameType);
        Assert.Equal(startedAt, item.StartedAt);
        Assert.Equal(finishedAt, item.FinishedAt);
        Assert.Equal("player-1", item.WinnerUserId);
        Assert.False(item.IsDraw);
        Assert.Equal(4, item.Score);
    }

    [Fact]
    public async Task GetStatsMapsAllStats()
    {
        var stats = new MemoryPlayerStats(
            "player-1",
            GamesPlayed: 3,
            Wins: 2,
            Losses: 1,
            Draws: 0,
            TotalPairs: 11,
            BestScore: 5,
            AverageScore: 3.67);
        var historyService = new Mock<IGameHistoryService>();
        historyService.Setup(service =>
                service.GetStatsByPlayerAsync("player-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        var controller = CreateController(historyService.Object);

        var response = await controller.GetStats("player-1", default);

        var result = Assert.IsType<OkObjectResult>(response.Result).Value
            as MemoryPlayerStatsDto;
        Assert.NotNull(result);
        Assert.Equal("player-1", result.UserId);
        Assert.Equal(3, result.GamesPlayed);
        Assert.Equal(2, result.Wins);
        Assert.Equal(1, result.Losses);
        Assert.Equal(0, result.Draws);
        Assert.Equal(11, result.TotalPairs);
        Assert.Equal(5, result.BestScore);
        Assert.Equal(3.67, result.AverageScore);
    }

    private static MemoryController CreateController(IGameHistoryService historyService) =>
        new(historyService);
}

using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Data.Services;
using BattleHub.Memory.Domain;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BattleHub.Memory.Api.Tests.Services;

public sealed class MatchResultRecorderTests
{
    [Fact]
    public async Task RecordAsync_SavesResultWithSessionStartTime()
    {
        // Arrange
        var session = new GameSession("match1", new Board(["A", "B", "C", "D", "E", "F", "G", "H"]),
            [new Player("user1", "Jugador 1"), new Player("user2", "Jugador 2")]);
        var resultId = Guid.NewGuid();
        var results = new Mock<IGameResultService>();
        results.Setup(r => r.SaveResultAsync(session, session.StartedAt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultId);
        var scopes = new ServiceCollection()
            .AddScoped<IGameResultService>(_ => results.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        // Act
        var id = await new MatchResultRecorder(scopes).RecordAsync(session);

        // Assert
        Assert.Equal(resultId, id);
        results.Verify(r => r.SaveResultAsync(session, session.StartedAt, It.IsAny<CancellationToken>()), Times.Once());
    }
}

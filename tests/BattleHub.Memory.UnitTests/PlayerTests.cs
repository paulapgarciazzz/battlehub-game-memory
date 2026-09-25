using BattleHub.Memory.Domain;

namespace BattleHub.Memory.UnitTests;

public class PlayerTests
{
    [Fact]
    public void AddMatchedPair_ShouldIncreaseScore()
    {
        // Arrange
        var player = new Player("user1", "Wilmer");

        // Act
        player.AddMatchedPair();

        // Assert
        Assert.Equal(1, player.MatchedPairs);
    }
}
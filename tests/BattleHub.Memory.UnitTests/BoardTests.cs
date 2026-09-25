using BattleHub.Memory.Domain;

namespace BattleHub.Memory.UnitTests;

public class BoardTests
{
    [Fact]
    public void Board_ShouldHave16Cards()
    {
        // Arrange
        var values = new[]
        {
            "A", "B", "C", "D",
            "E", "F", "G", "H"
        };

        // Act
        var board = new Board(values);

        // Assert
        Assert.Equal(16, board.Cards.Count);
    }
}
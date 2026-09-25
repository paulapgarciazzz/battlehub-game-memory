using BattleHub.Memory.Domain;

namespace BattleHub.Memory.UnitTests;

public class GameSessionTests
{
    [Fact]
    public void GameSession_ShouldRequireAtLeastTwoPlayers()
    {
        // Arrange
        var board = new Board(new[]
        {
            "A", "B", "C", "D",
            "E", "F", "G", "H"
        });

        var players = new List<Player>
        {
            new Player("user1", "Jugador 1")
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new GameSession("match1", board, players));
    }

    [Fact]
    public void FlipCard_ShouldRejectPlayerWhoDoesNotHaveTurn()
    {
        // Arrange
        var board = new Board(new[]
        {
            "A", "B", "C", "D",
            "E", "F", "G", "H"
        });

        var players = new List<Player>
        {
            new Player("user1", "Jugador 1"),
            new Player("user2", "Jugador 2")
        };

        var session = new GameSession("match1", board, players);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            session.FlipCard("user2", 0));
    }
    //esta prueba buscaa automaticamente una pareja
    [Fact]
    public void FlipCard_WhenCardsMatch_ShouldIncreasePlayerScore()
    {
        // Arrange
        var board = new Board(new[]
        {
        "A", "B", "C", "D",
        "E", "F", "G", "H"
    });

        var players = new List<Player>
    {
        new Player("user1", "Jugador 1"),
        new Player("user2", "Jugador 2")
    };

        var session = new GameSession("match1", board, players);

        var pair = board.Cards
            .GroupBy(card => card.Value)
            .First(group => group.Count() == 2)
            .ToList();

        // Act
        session.FlipCard("user1", pair[0].Id);
        var isMatch = session.FlipCard("user1", pair[1].Id);

        // Assert
        Assert.True(isMatch);
        Assert.Equal(1, players[0].MatchedPairs);
    }
    //comprobamos que el jugador con mas parejas sea el ganador
    [Fact]
    public void GetWinners_ShouldReturnPlayerWithMostMatchedPairs()
    {
        // Arrange
        var board = new Board(new[]
        {
        "A", "B", "C", "D",
        "E", "F", "G", "H"
    });

        var player1 = new Player("user1", "Jugador 1");
        var player2 = new Player("user2", "Jugador 2");

        var players = new List<Player>
    {
        player1,
        player2
    };

        var session = new GameSession("match1", board, players);

        // Simulamos que el jugador 1 consiguió 2 parejas
        player1.AddMatchedPair();
        player1.AddMatchedPair();

        // El jugador 2 consiguió 1 pareja
        player2.AddMatchedPair();

        // Act
        var winners = session.GetWinners();

        // Assert
        Assert.Single(winners);
        Assert.Equal("user1", winners[0].UserId);
    }
}


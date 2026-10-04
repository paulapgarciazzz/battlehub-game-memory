using BattleHub.Memory.Domain;

namespace BattleHub.Memory.UnitTests;

public class GameSessionTurnTests
{
    private static GameSession CreateSession()
    {
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

        return new GameSession("match1", board, players);
    }

    // Busca dos cartas con el mismo valor (el tablero se mezcla al azar).
    private static (int First, int Second) FindPair(GameSession session)
    {
        var pair = session.Board.Cards
            .GroupBy(card => card.Value)
            .First()
            .ToList();

        return (pair[0].Id, pair[1].Id);
    }

    // Busca dos cartas con distinto valor.
    private static (int First, int Second) FindNonPair(GameSession session)
    {
        var first = session.Board.Cards[0];
        var second = session.Board.Cards.First(card => card.Value != first.Value);

        return (first.Id, second.Id);
    }

    [Fact]
    public void TurnNumber_ShouldIncrease_WhenTwoCardTurnIsResolvedWithMatch()
    {
        // Arrange
        var session = CreateSession();
        var (first, second) = FindPair(session);
        var initialTurn = session.TurnNumber;

        // Act
        session.FlipCard("user1", first);
        var result = session.FlipCard("user1", second);

        // Assert
        Assert.True(result.IsMatch);
        Assert.Equal(initialTurn + 1, session.TurnNumber);
        Assert.Equal(session.TurnNumber, result.TurnNumber);
    }

    [Fact]
    public void TurnNumber_ShouldIncrease_WhenTwoCardTurnIsResolvedWithoutMatch()
    {
        // Arrange
        var session = CreateSession();
        var (first, second) = FindNonPair(session);
        var initialTurn = session.TurnNumber;

        // Act
        session.FlipCard("user1", first);
        var result = session.FlipCard("user1", second);

        // Assert
        Assert.False(result.IsMatch);
        Assert.Equal(initialTurn + 1, session.TurnNumber);
        Assert.Equal(session.TurnNumber, result.TurnNumber);
    }

    [Fact]
    public void TurnNumber_ShouldNotIncrease_WhenOnlyFirstCardIsFlipped()
    {
        // Arrange
        var session = CreateSession();
        var initialTurn = session.TurnNumber;

        // Act
        session.FlipCard("user1", 0);

        // Assert
        Assert.Equal(initialTurn, session.TurnNumber);
    }

    [Fact]
    public void TurnNumber_ShouldIncrease_WhenTurnIsForfeitedByTimeout()
    {
        // Arrange
        var session = CreateSession();
        var initialTurn = session.TurnNumber;

        // Act
        session.ForfeitTurnByTimeout();

        // Assert
        Assert.Equal(initialTurn + 1, session.TurnNumber);
    }

    [Fact]
    public void TryForfeitTurnByTimeout_ShouldReturnFalse_WhenTurnAlreadyChanged()
    {
        // Arrange
        var session = CreateSession();
        var (first, second) = FindNonPair(session);

        // El temporizador se armó para este turno...
        var expectedTurn = session.TurnNumber;

        // ...pero el jugador jugó antes de que se cumpliera el tiempo.
        session.FlipCard("user1", first);
        session.FlipCard("user1", second);

        // Act
        var forfeited = session.TryForfeitTurnByTimeout(expectedTurn, out _);

        // Assert: el timer viejo no le quita el turno a user2.
        Assert.False(forfeited);
        Assert.Equal("user2", session.CurrentPlayer.UserId);
        Assert.Equal(expectedTurn + 1, session.TurnNumber);
    }

    [Fact]
    public void TryForfeitTurnByTimeout_ShouldChangePlayer_WhenTurnIsStillCurrent()
    {
        // Arrange
        var session = CreateSession();
        var expectedTurn = session.TurnNumber;

        // Act
        var forfeited = session.TryForfeitTurnByTimeout(expectedTurn, out var result);

        // Assert
        Assert.True(forfeited);
        Assert.Equal("user1", result.PreviousPlayerId);
        Assert.Equal("user2", result.CurrentPlayerId);
        Assert.Equal(expectedTurn + 1, result.TurnNumber);
        Assert.Equal("user2", session.CurrentPlayer.UserId);
    }

    [Fact]
    public void FlipCard_WhenCardsDoNotMatch_ShouldReturnOpponentAsCurrentPlayer()
    {
        // Arrange
        var session = CreateSession();
        var (first, second) = FindNonPair(session);

        // Act
        var firstResult = session.FlipCard("user1", first);
        var secondResult = session.FlipCard("user1", second);

        // Assert
        Assert.Equal("user1", firstResult.CurrentPlayerId);
        Assert.Equal("user2", secondResult.CurrentPlayerId);
        Assert.False(secondResult.IsFinished);
    }
}

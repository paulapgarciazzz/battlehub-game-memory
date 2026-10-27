using BattleHub.Memory.Domain;
using System.Text.Json;

namespace BattleHub.Memory.Domain.Tests;

public class GameSessionTests
{
    private static Board CreateBoard() => new(new[]
    {
        "A", "B", "C", "D",
        "E", "F", "G", "H"
    });

    private static List<Player> CreatePlayers() =>
    [
        new Player("user1", "Jugador 1"),
        new Player("user2", "Jugador 2")
    ];

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
        var result = session.FlipCard("user1", pair[1].Id);

        // Assert
        Assert.True(result.IsMatch);
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

    [Fact]
    public void Snapshot_HidesFaceDownValuesAndRevealsAllCardsDuringPreview()
    {
        var board = CreateBoard();
        var session = new GameSession("match1", board, CreatePlayers());
        var visibleCard = board.Cards[0];
        session.FlipCard("user1", visibleCard.Id);

        using var playing = JsonDocument.Parse(
            JsonSerializer.Serialize(session.Snapshot(false, null, false)));
        var visible = playing.RootElement.GetProperty("Cards")
            .EnumerateArray()
            .Single(card => card.GetProperty("Id").GetInt32() == visibleCard.Id);
        var hidden = playing.RootElement.GetProperty("Cards")
            .EnumerateArray()
            .First(card => card.GetProperty("Id").GetInt32() != visibleCard.Id);

        Assert.Equal("playing", playing.RootElement.GetProperty("Phase").GetString());
        Assert.Equal(visibleCard.Value, visible.GetProperty("Value").GetString());
        Assert.True(visible.GetProperty("FaceUp").GetBoolean());
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("Value").ValueKind);
        Assert.False(hidden.GetProperty("FaceUp").GetBoolean());

        using var preview = JsonDocument.Parse(
            JsonSerializer.Serialize(session.Snapshot(true, null, false)));
        Assert.Equal("preview", preview.RootElement.GetProperty("Phase").GetString());
        Assert.All(
            preview.RootElement.GetProperty("Cards").EnumerateArray(),
            card => Assert.NotEqual(JsonValueKind.Null, card.GetProperty("Value").ValueKind));
    }

    [Fact]
    public void ForfeitTurnByTimeout_HidesFirstCardAndAdvancesPlayer()
    {
        var session = new GameSession("match1", CreateBoard(), CreatePlayers());
        var firstCard = session.Board.Cards[0];

        session.FlipCard("user1", firstCard.Id);
        session.ForfeitTurnByTimeout();

        Assert.Null(session.FirstFlippedCard);
        Assert.Equal(CardState.FaceDown, firstCard.State);
        Assert.Equal("user2", session.CurrentPlayer.UserId);
        Assert.Equal(1, session.TurnNumber);
    }

    [Fact]
    public void FlipCard_RejectsAnAlreadyFlippedOrUnknownCard()
    {
        var session = new GameSession("match1", CreateBoard(), CreatePlayers());

        session.FlipCard("user1", 0);

        Assert.Throws<InvalidOperationException>(() => session.FlipCard("user1", 0));
        Assert.Throws<ArgumentException>(() => session.FlipCard("user1", 99));
    }

    [Fact]
    public void FinishedSession_RejectsMovesAndTimeouts()
    {
        var board = CreateBoard();
        var players = CreatePlayers();
        foreach (var card in board.Cards)
            card.MarkAsMatched();

        var session = new GameSession("match1", board, players);

        Assert.True(session.IsFinished);
        Assert.Throws<InvalidOperationException>(() => session.FlipCard("user1", 0));
        Assert.Throws<InvalidOperationException>(session.ForfeitTurnByTimeout);
        Assert.False(session.TryForfeitTurnByTimeout(0, out _));
    }

    [Fact]
    public void Snapshot_ReportsDrawWhenFinishedPlayersAreTied()
    {
        var board = CreateBoard();
        var players = CreatePlayers();
        foreach (var card in board.Cards)
            card.MarkAsMatched();
        players[0].AddMatchedPair();
        players[1].AddMatchedPair();

        var session = new GameSession("match1", board, players);
        using var snapshot = JsonDocument.Parse(
            JsonSerializer.Serialize(session.Snapshot(false, null, true)));

        Assert.Equal("finished", snapshot.RootElement.GetProperty("Phase").GetString());
        Assert.True(snapshot.RootElement.GetProperty("IsDraw").GetBoolean());
        Assert.Equal(JsonValueKind.Null, snapshot.RootElement.GetProperty("WinnerUserId").ValueKind);
        Assert.True(snapshot.RootElement.GetProperty("ResultSaved").GetBoolean());
        Assert.Equal(2, session.GetWinners().Count);
    }
}

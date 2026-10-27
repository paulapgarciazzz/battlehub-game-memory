using BattleHub.Memory.Domain;

namespace BattleHub.Memory.Domain.Tests;

public class BoardTests
{
    private static readonly string[] Values =
    [
        "A", "B", "C", "D",
        "E", "F", "G", "H"
    ];

    [Fact]
    public void Board_ShouldHave16Cards()
    {
        var board = new Board(Values);

        Assert.Equal(16, board.Cards.Count);
    }

    [Fact]
    public void Board_RejectsInvalidCardCountAndPreviewDuration()
    {
        Assert.Throws<ArgumentException>(() => new Board(Values[..7]));
        Assert.Throws<ArgumentException>(() => new Board(Values, -1));
    }

    [Fact]
    public void GetCard_ThrowsForUnknownId()
    {
        var board = new Board(Values);

        Assert.Throws<ArgumentException>(() => board.GetCard(-1));
    }

    [Fact]
    public void RevealAllAndHideAll_ChangeOnlyUnmatchedCardVisibility()
    {
        var board = new Board(Values);
        var matched = board.Cards[0];
        matched.MarkAsMatched();

        board.RevealAll();
        Assert.All(
            board.Cards.Where(card => card != matched),
            card => Assert.Equal(CardState.FaceUp, card.State));
        Assert.Equal(CardState.Matched, matched.State);
        Assert.False(board.AllMatched);

        board.HideAll();
        Assert.All(
            board.Cards.Where(card => card != matched),
            card => Assert.Equal(CardState.FaceDown, card.State));
        Assert.Equal(CardState.Matched, matched.State);
    }
}
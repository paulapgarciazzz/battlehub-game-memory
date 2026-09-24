namespace BattleHub.Memory.Domain;

public class Board
{
    public const int CardCount = 16;

    public IReadOnlyList<Card> Cards { get; }
    public int PreviewSeconds { get; }

    public Board(IEnumerable<string> cardValues, int previewSeconds = 5)
    {
        if (previewSeconds < 0)
            throw new ArgumentException("El tiempo de previsualización no puede ser negativo.");

        PreviewSeconds = previewSeconds;

        var pairedValues = cardValues.SelectMany(v => new[] { v, v }).ToList();

        if (pairedValues.Count != CardCount)
        {
            throw new ArgumentException(
                $"Se esperaban {CardCount / 2} valores únicos ({CardCount} cartas en total), " +
                $"pero se recibieron {pairedValues.Count / 2}.");
        }

        var shuffled = pairedValues.OrderBy(_ => Random.Shared.Next()).ToList();

        Cards = shuffled
            .Select((value, index) => new Card(index, value))
            .ToList();
    }

    public Card GetCard(int cardId)
    {
        var card = Cards.FirstOrDefault(c => c.Id == cardId);
        if (card is null)
            throw new ArgumentException($"No existe una carta con id {cardId}.");

        return card;
    }

    public bool AllMatched => Cards.All(c => c.State == CardState.Matched);

    public void RevealAll()
    {
        foreach (var card in Cards.Where(c => c.State == CardState.FaceDown))
            card.Flip();
    }

    public void HideAll()
    {
        foreach (var card in Cards.Where(c => c.State == CardState.FaceUp))
            card.Flip();
    }
}

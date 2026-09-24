namespace BattleHub.Memory.Domain;

public enum CardState
{
    FaceDown,
    FaceUp,
    Matched
}

public class Card
{
    public int Id { get; }
    public string Value { get; }
    public CardState State { get; private set; } = CardState.FaceDown;

    public Card(int id, string value)
    {
        Id = id;
        Value = value;
    }

    public void Flip()
    {
        if (State == CardState.Matched)
            throw new InvalidOperationException("No se puede voltear una carta ya emparejada.");

        State = State == CardState.FaceDown ? CardState.FaceUp : CardState.FaceDown;
    }

    public void MarkAsMatched()
    {
        State = CardState.Matched;
    }
}

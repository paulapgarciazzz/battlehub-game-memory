namespace BattleHub.Memory.Domain;

public class GameSession
{
    public const int TurnTimeoutSeconds = 10;

    public string MatchId { get; }
    public Board Board { get; }
    public IReadOnlyList<Player> Players { get; }

    private int _currentPlayerIndex;
    private Card? _firstFlippedCard;

    public Player CurrentPlayer => Players[_currentPlayerIndex];
    public bool IsFinished => Board.AllMatched;

    public GameSession(string matchId, Board board, IReadOnlyList<Player> players)
    {
        if (players.Count < 2)
            throw new ArgumentException("Se necesitan al menos 2 jugadores.");

        MatchId = matchId;
        Board = board;
        Players = players;
        _currentPlayerIndex = 0;
    }

    public bool FlipCard(string playerId, int cardId)
    {
        if (IsFinished)
            throw new InvalidOperationException("La partida ya terminó.");

        if (playerId != CurrentPlayer.UserId)
            throw new InvalidOperationException(
                $"No es el turno de este jugador. Turno actual: {CurrentPlayer.UserId}.");

        var card = Board.GetCard(cardId);

        if (card.State != CardState.FaceDown)
            throw new InvalidOperationException("Esa carta ya está volteada o emparejada.");

        card.Flip();

        if (_firstFlippedCard is null)
        {
            _firstFlippedCard = card;
            return false;
        }

        var isMatch = _firstFlippedCard.Value == card.Value;

        if (isMatch)
        {
            _firstFlippedCard.MarkAsMatched();
            card.MarkAsMatched();
            CurrentPlayer.AddMatchedPair();
        }
        else
        {
            _firstFlippedCard.Flip();
            card.Flip();
            AdvanceTurn();
        }

        _firstFlippedCard = null;
        return isMatch;
    }

    private void AdvanceTurn()
    {
        _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Count;
    }

    public void ForfeitTurnByTimeout()
    {
        if (IsFinished)
            throw new InvalidOperationException("La partida ya terminó.");

        if (_firstFlippedCard is not null)
        {
            _firstFlippedCard.Flip();
            _firstFlippedCard = null;
        }

        AdvanceTurn();
    }

    public IReadOnlyList<Player> GetWinners()
    {
        var maxPairs = Players.Max(p => p.MatchedPairs);
        return Players.Where(p => p.MatchedPairs == maxPairs).ToList();
    }
}

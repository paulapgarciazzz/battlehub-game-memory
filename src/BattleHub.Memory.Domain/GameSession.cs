namespace BattleHub.Memory.Domain;

/// <summary>
/// Snapshot inmutable de una carta, tomado atómicamente dentro del lock de
/// GameSession.FlipCard, para que el Hub no tenga que leer el estado del
/// dominio por su cuenta (esas lecturas externas no sincronizadas eran la
/// causa de que, ante dos llamadas casi simultáneas, se armara y emitiera
/// un evento con una sola carta cuando en realidad el turno se había
/// resuelto con dos, dejando una carta volteada para siempre).
/// </summary>
public readonly record struct CardSnapshot(int Id, string Value);

public readonly record struct FlipCardResult(
    IReadOnlyList<CardSnapshot> Cards,
    bool IsMatch);

public class GameSession
{
    public const int TurnTimeoutSeconds = 10;

    public string MatchId { get; }
    public Board Board { get; }
    public IReadOnlyList<Player> Players { get; }

    private readonly object _lock = new();
    private int _currentPlayerIndex;
    private Card? _firstFlippedCard;

    public Player CurrentPlayer => Players[_currentPlayerIndex];

    // Permite consultar la primera carta desde el Hub.
    public Card? FirstFlippedCard => _firstFlippedCard;

    public bool IsFinished => Board.AllMatched;

    public GameSession(
        string matchId,
        Board board,
        IReadOnlyList<Player> players)
    {
        if (players.Count < 2)
            throw new ArgumentException(
                "Se necesitan al menos 2 jugadores.");

        MatchId = matchId;
        Board = board;
        Players = players;
        _currentPlayerIndex = 0;
    }

    public FlipCardResult FlipCard(string playerId, int cardId)
    {
        // GameSession es compartido por todas las conexiones de la partida,
        // y dos jugadores (o dos conexiones del mismo jugador, ej. tras un
        // reconnect) pueden invocar FlipCard casi simultáneamente. Todo el
        // cálculo de qué cartas quedaron involucradas en la jugada se hace
        // ACÁ ADENTRO, en el mismo lock que muta el estado, y se devuelve ya
        // armado: si el Hub arma ese snapshot leyendo el dominio por su
        // cuenta (antes o después de este método), una carrera entre dos
        // llamadas puede hacer que arme un evento con una sola carta cuando
        // en realidad el turno se resolvió con dos, dejando una carta
        // volteada para siempre porque nunca se emite el evento de 2 cartas
        // que la oculta.
        lock (_lock)
        {
            if (IsFinished)
                throw new InvalidOperationException(
                    "La partida ya terminó.");

            if (playerId != CurrentPlayer.UserId)
                throw new InvalidOperationException(
                    $"No es el turno de este jugador. " +
                    $"Turno actual: {CurrentPlayer.UserId}.");

            var card = Board.GetCard(cardId);

            if (card.State != CardState.FaceDown)
                throw new InvalidOperationException(
                    "Esa carta ya está volteada o emparejada.");

            card.Flip();

            if (_firstFlippedCard is null)
            {
                _firstFlippedCard = card;

                return new FlipCardResult(
                    new[] { new CardSnapshot(card.Id, card.Value) },
                    IsMatch: false);
            }

            var previousCard = _firstFlippedCard;

            var isMatch =
                previousCard.Value == card.Value;

            if (isMatch)
            {
                previousCard.MarkAsMatched();
                card.MarkAsMatched();

                CurrentPlayer.AddMatchedPair();
            }
            else
            {
                previousCard.Flip();
                card.Flip();

                AdvanceTurn();
            }

            _firstFlippedCard = null;

            return new FlipCardResult(
                new[]
                {
                    new CardSnapshot(previousCard.Id, previousCard.Value),
                    new CardSnapshot(card.Id, card.Value)
                },
                isMatch);
        }
    }

    private void AdvanceTurn()
    {
        _currentPlayerIndex =
            (_currentPlayerIndex + 1) % Players.Count;
    }

    public void ForfeitTurnByTimeout()
    {
        lock (_lock)
        {
            if (IsFinished)
                throw new InvalidOperationException(
                    "La partida ya terminó.");

            if (_firstFlippedCard is not null)
            {
                _firstFlippedCard.Flip();
                _firstFlippedCard = null;
            }

            AdvanceTurn();
        }
    }

    public IReadOnlyList<Player> GetWinners()
    {
        var maxPairs =
            Players.Max(p => p.MatchedPairs);

        return Players
            .Where(p => p.MatchedPairs == maxPairs)
            .ToList();
    }
}

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

/// <summary>
/// Resultado de una jugada. CurrentPlayerId, TurnNumber e IsFinished se
/// calculan dentro del mismo lock que la jugada, así el Hub informa a los
/// clientes de quién es el turno sin volver a leer el dominio.
/// </summary>
public readonly record struct FlipCardResult(
    IReadOnlyList<CardSnapshot> Cards,
    bool IsMatch,
    string CurrentPlayerId,
    int TurnNumber,
    bool IsFinished);

/// <summary>
/// Resultado de quitar un turno por tiempo agotado.
/// </summary>
public readonly record struct TurnTimeoutResult(
    string PreviousPlayerId,
    string CurrentPlayerId,
    int TurnNumber);

public class GameSession
{
    public const int TurnTimeoutSeconds = 10;

    public string MatchId { get; }
    public Board Board { get; }
    public IReadOnlyList<Player> Players { get; }

    // La hora de inicio la registra el servidor, no el cliente.
    public DateTimeOffset StartedAt { get; }

    private readonly object _lock = new();
    private int _currentPlayerIndex;
    private Card? _firstFlippedCard;

    // Aumenta cada vez que empieza un turno nuevo. Sirve para que un
    // temporizador viejo no le quite el turno a un jugador que ya jugó.
    private int _turnNumber;

    public Player CurrentPlayer => Players[_currentPlayerIndex];

    // Permite consultar la primera carta desde el Hub.
    public Card? FirstFlippedCard => _firstFlippedCard;

    public bool IsFinished => Board.AllMatched;

    public int TurnNumber
    {
        get
        {
            lock (_lock)
            {
                return _turnNumber;
            }
        }
    }

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
        StartedAt = DateTimeOffset.UtcNow;
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

                // Primera carta del turno: el turno sigue siendo el mismo.
                return new FlipCardResult(
                    new[] { new CardSnapshot(card.Id, card.Value) },
                    IsMatch: false,
                    CurrentPlayerId: CurrentPlayer.UserId,
                    TurnNumber: _turnNumber,
                    IsFinished: false);
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

            // Con o sin pareja, se resolvió una jugada de 2 cartas y empieza
            // un turno nuevo (del rival, o uno extra si acertó la pareja).
            _turnNumber++;

            return new FlipCardResult(
                new[]
                {
                    new CardSnapshot(previousCard.Id, previousCard.Value),
                    new CardSnapshot(card.Id, card.Value)
                },
                isMatch,
                CurrentPlayer.UserId,
                _turnNumber,
                IsFinished);
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

            ForfeitTurn();
        }
    }

    /// <summary>
    /// Quita el turno por tiempo agotado solo si el turno que se esperaba
    /// sigue siendo el actual y la partida no terminó. Así, un temporizador
    /// que se armó para un turno ya jugado no le quita el turno al jugador.
    /// </summary>
    public bool TryForfeitTurnByTimeout(
        int expectedTurnNumber,
        out TurnTimeoutResult result)
    {
        lock (_lock)
        {
            if (IsFinished || expectedTurnNumber != _turnNumber)
            {
                result = default;
                return false;
            }

            var previousPlayerId = CurrentPlayer.UserId;

            ForfeitTurn();

            result = new TurnTimeoutResult(
                previousPlayerId,
                CurrentPlayer.UserId,
                _turnNumber);

            return true;
        }
    }

    // Se llama siempre dentro del lock.
    private void ForfeitTurn()
    {
        if (_firstFlippedCard is not null)
        {
            _firstFlippedCard.Flip();
            _firstFlippedCard = null;
        }

        AdvanceTurn();
        _turnNumber++;
    }

    /// <summary>
    /// Estado completo de la partida para quien entra o vuelve a entrar
    /// (evento StateSnapshot del Hub). Se arma dentro del lock para que sea
    /// una foto consistente.
    ///
    /// El valor de una carta solo se manda si está boca arriba o emparejada
    /// (o durante la previsualización): las cartas ocultas no revelan nada.
    /// </summary>
    public object Snapshot(bool preview, DateTimeOffset? deadline, bool resultSaved)
    {
        lock (_lock)
        {
            var winners = GetWinners();

            return new
            {
                MatchId,
                Phase = IsFinished ? "finished" : preview ? "preview" : "playing",
                StartedAt,
                CurrentPlayerId = CurrentPlayer.UserId,
                TurnDeadline = deadline,
                IsDraw = IsFinished && winners.Count > 1,
                WinnerUserId = IsFinished && winners.Count == 1 ? winners[0].UserId : null,
                ResultSaved = resultSaved,
                Players = Players
                    .Select(p => new { p.UserId, p.DisplayName, p.MatchedPairs })
                    .ToArray(),
                Cards = Board.Cards
                    .Select(c => new
                    {
                        c.Id,
                        Value = preview || c.State != CardState.FaceDown ? c.Value : null,
                        FaceUp = preview || c.State != CardState.FaceDown,
                        Matched = c.State == CardState.Matched
                    })
                    .ToArray()
            };
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

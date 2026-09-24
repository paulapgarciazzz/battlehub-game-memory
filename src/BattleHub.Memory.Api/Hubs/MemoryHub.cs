using System.Collections.Concurrent;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Hubs;

/// <summary>
/// Hub propio del juego de memoria, montado en /hubs/memory.
/// Es independiente del Lobby Hub de Matchmaking.
/// </summary>
public class MemoryHub : Hub
{
    private readonly MemoryGameService _gameService;

    // Guarda un temporizador por cada partida.
    private readonly ConcurrentDictionary<
        string,
        CancellationTokenSource> _turnTimers = new();

    public MemoryHub(MemoryGameService gameService)
    {
        _gameService = gameService;
    }

    /// <summary>
    /// Conecta a un jugador con una partida.
    /// </summary>
    public async Task JoinMatch(
        string matchId,
        string userId,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new HubException(
                "El matchId es obligatorio.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException(
                "El userId es obligatorio.");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new HubException(
                "El displayName es obligatorio.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            matchId);

        var player = new Player(
            userId,
            displayName);

        var session = _gameService.AddPlayer(
            matchId,
            player);

        await Clients.Group(matchId).SendAsync(
            "PlayerJoined",
            new
            {
                UserId = userId,
                DisplayName = displayName
            });

        if (session is not null)
        {
            await StartPreview(
                matchId,
                session);
        }
    }

    /// <summary>
    /// Muestra las cartas durante la previsualización inicial
    /// y las oculta después de 5 segundos.
    /// </summary>
    private async Task StartPreview(
        string matchId,
        GameSession session)
    {
        session.Board.RevealAll();

        await Clients.Group(matchId).SendAsync(
            "GameReady",
            new
            {
                MatchId = matchId,
                PreviewSeconds =
                    session.Board.PreviewSeconds
            });

        await Task.Delay(
            TimeSpan.FromSeconds(
                session.Board.PreviewSeconds));

        session.Board.HideAll();

        await Clients.Group(matchId).SendAsync(
            "PreviewFinished",
            new
            {
                MatchId = matchId
            });

        // Comienza el temporizador del primer turno.
        StartTurnTimer(
            matchId,
            session);
    }

    /// <summary>
    /// Voltea una carta y sincroniza el movimiento
    /// con los jugadores de la partida.
    /// </summary>
    public async Task FlipCard(
        string matchId,
        string userId,
        int cardId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new HubException(
                "El matchId es obligatorio.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException(
                "El userId es obligatorio.");

        var session =
            _gameService.GetSession(matchId);

        // Guardamos la primera carta antes de ejecutar
        // la lógica de FlipCard().
        var firstCard =
            session.FirstFlippedCard;

        // Guardamos la segunda carta antes de ejecutar
        // la lógica de FlipCard().
        var secondCard =
            session.Board.GetCard(cardId);

        var cards = new List<object>();

        // Si ya había una primera carta,
        // guardamos sus datos también.
        if (firstCard is not null)
        {
            cards.Add(new
            {
                Id = firstCard.Id,
                Value = firstCard.Value
            });
        }

        // Agregamos la carta seleccionada.
        cards.Add(new
        {
            Id = secondCard.Id,
            Value = secondCard.Value
        });

        bool isMatch;

        try
        {
            isMatch = session.FlipCard(
                userId,
                cardId);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }

        // Enviamos a todos los jugadores las cartas
        // involucradas en el movimiento.
        await Clients.Group(matchId).SendAsync(
            "CardFlipped",
            new
            {
                UserId = userId,
                Cards = cards,
                IsMatch = isMatch
            });

        // Si cambió el turno, iniciamos los 10 segundos
        // para el nuevo jugador.
        if (session.CurrentPlayer.UserId != userId
            && !session.IsFinished)
        {
            StartTurnTimer(
                matchId,
                session);
        }
    }

    /// <summary>
    /// Inicia el temporizador de 10 segundos
    /// para el turno actual.
    /// </summary>
    private void StartTurnTimer(
        string matchId,
        GameSession session)
    {
        // Cancelamos el temporizador anterior de esta partida.
        if (_turnTimers.TryRemove(
            matchId,
            out var oldTimer))
        {
            oldTimer.Cancel();
            oldTimer.Dispose();
        }

        var cancellationTokenSource =
            new CancellationTokenSource();

        _turnTimers[matchId] =
            cancellationTokenSource;

        _ = RunTurnTimer(
            matchId,
            session,
            cancellationTokenSource.Token);
    }

    /// <summary>
    /// Espera 10 segundos y cambia el turno
    /// si el jugador no realizó la jugada.
    /// </summary>
    private async Task RunTurnTimer(
        string matchId,
        GameSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(
                    GameSession.TurnTimeoutSeconds),
                cancellationToken);

            if (session.IsFinished)
                return;

            var previousPlayer =
                session.CurrentPlayer;

            session.ForfeitTurnByTimeout();

            await Clients.Group(matchId).SendAsync(
                "TurnTimeout",
                new
                {
                    MatchId = matchId,
                    PreviousPlayerId =
                        previousPlayer.UserId,
                    CurrentPlayerId =
                        session.CurrentPlayer.UserId,
                    TurnTimeoutSeconds =
                        GameSession.TurnTimeoutSeconds
                });

            // Iniciamos los 10 segundos para el siguiente jugador.
            StartTurnTimer(
                matchId,
                session);
        }
        catch (TaskCanceledException)
        {
            // El temporizador fue cancelado porque
            // el jugador realizó una jugada válida.
        }
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

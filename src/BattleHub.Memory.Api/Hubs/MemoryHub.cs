using System.Collections.Concurrent;
using System.Linq;
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
                    session.Board.PreviewSeconds,
                Cards = session.Board.Cards.Select(
                    c => new { c.Id, c.Value }),
                // Se manda el roster completo acá porque es el primer evento que
                // llega garantizado a AMBOS jugadores ya conectados al grupo. El
                // evento "PlayerJoined" de un jugador solo llega a quienes ya
                // estaban en el grupo en ese momento, así que el jugador que se
                // conectó primero nunca se entera de sí mismo por ese camino.
                Players = session.Players.Select(
                    p => new { p.UserId, p.DisplayName })
            });

        await Task.Delay(
            TimeSpan.FromSeconds(
                session.Board.PreviewSeconds));

        session.Board.HideAll();

        await Clients.Group(matchId).SendAsync(
            "PreviewFinished",
            new
            {
                MatchId = matchId,
                // El cliente ya no adivina quién arranca en base al orden local
                // de "players" (que puede diferir entre pantallas): el servidor
                // es la única fuente de verdad del turno.
                CurrentPlayerId = session.CurrentPlayer.UserId
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

        FlipCardResult result;

        try
        {
            // GameSession.FlipCard arma el snapshot de las cartas
            // involucradas atómicamente, dentro del mismo lock que muta el
            // estado. No se debe reconstruir ese snapshot leyendo el
            // dominio antes/después de esta llamada: eso reintroduce la
            // carrera que dejaba cartas volteadas para siempre.
            result = session.FlipCard(
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
                Cards = result.Cards.Select(
                    c => new { c.Id, c.Value }),
                IsMatch = result.IsMatch
            });

        // Dos cartas en el resultado significa que se resolvió un turno
        // completo (hubo pareja o no la hubo). En ambos casos empieza un
        // turno nuevo (el del rival, o uno extra para el mismo jugador si
        // acertó la pareja), así que hay que reiniciar los 10 segundos.
        // Si en cambio esta fue solo la primera carta del turno, el
        // temporizador original sigue corriendo sin tocarse.
        if (!session.IsFinished
            && result.Cards.Count == 2)
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

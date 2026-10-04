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

    // El Hub es transient: todo lo que tiene que sobrevivir a la llamada
    // (como el temporizador de turno) vive en servicios singleton.
    private readonly TurnTimerService _turnTimerService;

    public MemoryHub(
        MemoryGameService gameService,
        TurnTimerService turnTimerService)
    {
        _gameService = gameService;
        _turnTimerService = turnTimerService;
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
            player,
            out var sessionCreated);

        await Clients.Group(matchId).SendAsync(
            "PlayerJoined",
            new
            {
                UserId = userId,
                DisplayName = displayName
            });

        // Solo se hace la previsualización cuando esta llamada creó la
        // partida. Si alguien vuelve a hacer JoinMatch (ej. al reconectarse),
        // la partida ya existía y la previsualización no se repite.
        if (sessionCreated && session is not null)
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
        _turnTimerService.Start(session);
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

        if (!_gameService.TryGetSession(
            matchId,
            out var session))
        {
            throw new HubException(
                $"No existe una partida activa con el matchId '{matchId}'.");
        }

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

        // Enviamos a todos los jugadores las cartas involucradas en el
        // movimiento, junto con el turno ya resuelto por el servidor.
        await Clients.Group(matchId).SendAsync(
            "CardFlipped",
            new
            {
                UserId = userId,
                Cards = result.Cards.Select(
                    c => new { c.Id, c.Value }),
                IsMatch = result.IsMatch,
                CurrentPlayerId = result.CurrentPlayerId,
                IsFinished = result.IsFinished
            });

        // Dos cartas en el resultado significa que se resolvió un turno
        // completo (hubo pareja o no la hubo). En ambos casos empieza un
        // turno nuevo (el del rival, o uno extra para el mismo jugador si
        // acertó la pareja), así que hay que reiniciar los 10 segundos.
        // Si en cambio esta fue solo la primera carta del turno, el
        // temporizador original sigue corriendo sin tocarse.
        if (!result.IsFinished
            && result.Cards.Count == 2)
        {
            _turnTimerService.Start(session);
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


using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Hubs;

/// <summary>
/// Hub sin Auth0 ni Matchmaking, exclusivamente para desarrollo local.
/// No debe habilitarse en producción.
/// </summary>
public sealed class MemoryLocalHub(
    MemoryLocalGameService games,
    MemoryLocalTurnTimerService timers,
    ILogger<MemoryLocalHub> logger) : Hub
{
    public async Task JoinMatch(
        string matchId,
        string userId,
        string displayName)
    {
        matchId = Normalize(matchId);
        userId = (userId ?? "").Trim();
        displayName = (displayName ?? "").Trim();

        if (matchId.Length is < 3 or > 30 ||
            userId.Length is < 1 or > 50 ||
            displayName.Length is < 1 or > 50)
        {
            throw new HubException(
                "Revisá el código, el usuario y el nombre.");
        }

        var player = new Player(userId, displayName);

        // Guardar la identidad y la partida para esta conexión.
        Context.Items["userId"] = userId;
        Context.Items["match"] = matchId;

        GameSession? session;

        try
        {
            session = games.AddPlayer(
                matchId,
                player,
                out var created);

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                matchId);

            await Clients.Group(matchId).SendAsync(
                "PlayerJoined",
                new
                {
                    UserId = userId,
                    DisplayName = displayName
                });

            if (session is null)
            {
                logger.LogInformation(
                    "Jugador {UserId} esperando rival en partida {MatchId}.",
                    userId,
                    matchId);

                return;
            }

            if (created)
            {
                await RunPreview(matchId, session);
            }

            await Clients.Caller.SendAsync(
                "StateSnapshot",
                session.Snapshot(
                    games.IsPreview(matchId),
                    timers.Deadline(matchId),
                    false));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogInformation(
                ex,
                "No se pudo unir el jugador {UserId} a la partida {MatchId}.",
                userId,
                matchId);

            throw new HubException(
                "Ese usuario no puede unirse a esta partida.");
        }
    }

    public async Task FlipCard(string matchId, int cardId)
    {
        matchId = Normalize(matchId);

        var connectedMatch = Context.Items["match"] as string;
        var userId = Context.Items["userId"] as string ?? "";

        if (connectedMatch != matchId)
        {
            throw new HubException(
                "Primero tenés que unirte a la partida.");
        }

        if (!games.TryGetSession(matchId, out var session) ||
            session is null)
        {
            throw new HubException("No se encontró la partida.");
        }

        if (games.IsPreview(matchId))
        {
            throw new HubException(
                "Esperá a que termine la vista previa.");
        }

        // Diagnóstico para descubrir por qué se rechaza el turno.
        var currentPlayerId = session.CurrentPlayer.UserId;

        logger.LogInformation(
            "FLIP DEBUG | Match: {MatchId} | Usuario de conexión: '{UserId}' | Turno actual: '{CurrentPlayerId}' | Carta: {CardId} | Conexión: {ConnectionId}",
            matchId,
            userId,
            currentPlayerId,
            cardId,
            Context.ConnectionId);

        FlipCardResult result;

        try
        {
            result = session.FlipCard(userId, cardId);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or ArgumentException)
        {
            logger.LogWarning(
                "FLIP RECHAZADO | Match: {MatchId} | Usuario: '{UserId}' | Turno: '{CurrentPlayerId}' | Error: {Error}",
                matchId,
                userId,
                currentPlayerId,
                ex.Message);

            throw new HubException(ex.Message);
        }

        if (result.Cards.Count == 2 && !result.IsFinished)
        {
            timers.Start(session);
        }

        if (result.IsFinished)
        {
            timers.Stop(matchId);
        }

        await Clients.Group(matchId).SendAsync(
            "CardFlipped",
            new
            {
                MatchId = matchId,
                UserId = userId,
                Cards = result.Cards.Select(
                    c => new { c.Id, c.Value }),
                result.IsMatch,
                result.CurrentPlayerId,
                result.IsFinished,
                TurnDeadline = timers.Deadline(matchId)
            });
    }

    private async Task RunPreview(
        string matchId,
        GameSession session)
    {
        session.Board.RevealAll();

        await Clients.Group(matchId).SendAsync(
            "GameReady",
            new
            {
                MatchId = matchId,
                PreviewSeconds = session.Board.PreviewSeconds,
                Cards = session.Board.Cards.Select(
                    c => new { c.Id, c.Value }),
                Players = session.Players.Select(
                    p => new
                    {
                        p.UserId,
                        p.DisplayName
                    })
            });

        await Task.Delay(
            TimeSpan.FromSeconds(session.Board.PreviewSeconds));

        session.Board.HideAll();
        games.EndPreview(matchId);
        timers.Start(session);

        await Clients.Group(matchId).SendAsync(
            "PreviewFinished",
            new
            {
                MatchId = matchId,
                CurrentPlayerId = session.CurrentPlayer.UserId,
                TurnDeadline = timers.Deadline(matchId)
            });
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    private static string Normalize(string matchId) =>
        (matchId ?? "").Trim().ToUpperInvariant();
}

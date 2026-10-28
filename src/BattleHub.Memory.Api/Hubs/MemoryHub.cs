using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Hubs;

/// <summary>
/// Hub propio del juego de memoria, montado en /hubs/memory.
/// Es independiente del Lobby Hub de Matchmaking.
///
/// El juego no tiene contacto con Auth0 ni con Matchmaking: el Shell ya
/// autenticó al usuario y le entrega al juego el matchId y el currentUser.
/// Al terminar, el microfrontend avisa al Shell y el Shell avisa a Matchmaking.
/// </summary>
public sealed class MemoryHub(
    MemoryGameService games,
    TurnTimerService timers,
    MatchResultRecorder recorder,
    ILogger<MemoryHub> logger) : Hub
{
    /// <summary>
    /// Une al jugador a la partida. Los dos primeros jugadores distintos que
    /// entran con el mismo matchId forman la partida. También sirve para
    /// volver a entrar después de recargar la página: en ese caso recibe el
    /// estado actual (StateSnapshot) y la previsualización no se repite.
    /// </summary>
    public async Task JoinMatch(string matchId, string userId, string displayName)
    {
        matchId = (matchId ?? "").Trim();
        userId = (userId ?? "").Trim();
        displayName = (displayName ?? "").Trim();

        if (matchId.Length is < 1 or > 64
            || userId.Length is < 1 or > 128
            || displayName.Length is < 1 or > 50)
        {
            throw new HubException("INVALID_JOIN");
        }

        GameSession? session;
        bool created;

        try
        {
            session = games.AddPlayer(
                matchId,
                new Player(userId, displayName),
                out created);
        }
        catch (InvalidOperationException ex)
        {
            // La partida ya tiene a sus dos jugadores.
            throw new HubException(ex.Message);
        }

        // Se recuerda quién es esta conexión y en qué partida está:
        // FlipCard solo se acepta para esa partida y ese jugador.
        Context.Items["match"] = matchId;
        Context.Items["userId"] = userId;

        await Groups.AddToGroupAsync(Context.ConnectionId, matchId);

        await Clients.Group(matchId).SendAsync(
            "PlayerJoined",
            new { UserId = userId, DisplayName = displayName });

        // Todavía falta el segundo jugador.
        if (session is null)
            return;

        // Solo la llamada que creó la partida hace la previsualización.
        if (created)
            await RunPreview(matchId, session);

        // Quien entra (o vuelve a entrar) recibe el estado completo: cartas
        // visibles, marcador, turno y vencimiento del turno.
        await Clients.Caller.SendAsync(
            "StateSnapshot",
            session.Snapshot(
                games.IsPreview(matchId),
                timers.Deadline(matchId),
                games.ResultSaved(matchId)));
    }

    /// <summary>
    /// Muestra todas las cartas durante la previsualización inicial, las
    /// oculta y arranca el temporizador del primer turno.
    /// </summary>
    private async Task RunPreview(string matchId, GameSession session)
    {
        session.Board.RevealAll();

        await Clients.Group(matchId).SendAsync(
            "GameReady",
            new
            {
                MatchId = matchId,
                PreviewSeconds = session.Board.PreviewSeconds,
                Cards = session.Board.Cards.Select(c => new { c.Id, c.Value }),
                Players = session.Players.Select(p => new { p.UserId, p.DisplayName })
            });

        await Task.Delay(TimeSpan.FromSeconds(session.Board.PreviewSeconds));

        session.Board.HideAll();

        // Durante la previsualización FlipCard se rechaza (PREVIEW_ACTIVE).
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

    /// <summary>
    /// Voltea una carta del jugador de esta conexión y sincroniza la jugada
    /// con los dos jugadores.
    /// </summary>
    public async Task FlipCard(string matchId, int cardId)
    {
        // La conexión tiene que haber hecho JoinMatch a esta misma partida.
        if (Context.Items["match"] as string != matchId)
            throw new HubException("JOIN_REQUIRED");

        if (!games.TryGetSession(matchId, out var session))
            throw new HubException("MATCH_NOT_FOUND");

        if (games.IsPreview(matchId))
            throw new HubException("PREVIEW_ACTIVE");

        var userId = Context.Items["userId"] as string ?? "";

        FlipCardResult result;

        try
        {
            // GameSession arma el resultado dentro de su lock: no se debe
            // reconstruir leyendo el dominio antes o después de esta llamada.
            result = session.FlipCard(userId, cardId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // Turno equivocado, carta ya volteada o carta inexistente.
            throw new HubException(ex.Message);
        }

        // Una jugada de 2 cartas empieza un turno nuevo: se reinician los 10 s.
        if (result.Cards.Count == 2 && !result.IsFinished)
            timers.Start(session);

        if (result.IsFinished)
            timers.Stop(matchId);

        await Clients.Group(matchId).SendAsync(
            "CardFlipped",
            new
            {
                MatchId = matchId,
                UserId = userId,
                Cards = result.Cards.Select(c => new { c.Id, c.Value }),
                result.IsMatch,
                result.CurrentPlayerId,
                result.IsFinished,
                TurnDeadline = timers.Deadline(matchId)
            });

        if (!result.IsFinished)
            return;

        // El resultado lo guarda el backend (contrato 04). Si la BD falla,
        // ResultRetryWorker lo vuelve a intentar mientras la partida siga en
        // memoria. El aviso de fin de partida al Shell lo da el microfrontend.
        try
        {
            await recorder.RecordAsync(session);
            games.MarkSaved(matchId);
            await Clients.Group(matchId).SendAsync("ResultSaved", new { MatchId = matchId });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Resultado pendiente de guardar para {MatchId}.", matchId);
            await Clients.Group(matchId).SendAsync("ResultSaveFailed", new { MatchId = matchId });
        }
    }
}

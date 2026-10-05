using BattleHub.Memory.Api.Auth;
using BattleHub.Memory.Api.Matchmaking;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Hubs;

/// <summary>
/// Hub propio del juego de memoria, montado en /hubs/memory.
/// Es independiente del Lobby Hub de Matchmaking.
///
/// Solo pueden conectarse jugadores autenticados con Auth0 (política Play):
/// la identidad sale del claim "sub" del token, nunca de un userId enviado
/// por el navegador.
/// </summary>
[Authorize(Policy = MemoryAuth.Play)]
public sealed class MemoryHub(
    MemoryGameService games,
    TurnTimerService timers,
    MatchResultRecorder recorder,
    IMatchmakingGateway matchmaking,
    ILogger<MemoryHub> logger) : Hub
{
    /// <summary>
    /// Une al jugador autenticado a la partida. También sirve para volver a
    /// entrar después de recargar la página: en ese caso recibe el estado
    /// actual (StateSnapshot) y la previsualización no se repite.
    /// </summary>
    /// <param name="matchmakingAccessToken">
    /// Token del usuario para Matchmaking. Se usa solo para consultar la sala.
    /// </param>
    public async Task JoinMatch(string matchId, string matchmakingAccessToken)
    {
        var userId = MemoryAuth.UserId(Context.User!);

        // Si la partida ya terminó en memoria, se acepta que la sala de
        // Matchmaking esté en Finished (el jugador vuelve a ver el resultado).
        var completed = games.TryGetSession(matchId, out var restored) && restored.IsFinished;

        // Matchmaking es la fuente de verdad: la sala debe existir, ser de
        // memory, estar iniciada, tener 2 jugadores y el usuario debe estar en ella.
        var room = await matchmaking.ValidateAsync(
            matchId,
            userId,
            matchmakingAccessToken,
            Context.ConnectionAborted,
            completed);

        // Si la partida ya existe, los jugadores de Matchmaking tienen que ser
        // los mismos con los que se creó.
        if (games.TryGetSession(matchId, out var existing)
            && !existing.Players.Select(p => p.UserId).Order()
                .SequenceEqual(room.Participants.Select(p => p.UserId).Order()))
        {
            throw new HubException("MATCH_ROSTER_CHANGED");
        }

        // El nombre que se muestra sale del roster de Matchmaking.
        var player = room.Participants.Single(p => p.UserId == userId);

        var session = games.AddPlayer(
            matchId,
            new Player(userId, player.DisplayName),
            out var created);

        // Se recuerda en qué partida está esta conexión: FlipCard solo se
        // acepta para esa partida.
        Context.Items["match"] = matchId;

        await Groups.AddToGroupAsync(Context.ConnectionId, matchId);

        await Clients.Group(matchId).SendAsync(
            "PlayerJoined",
            new { UserId = userId, player.DisplayName });

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
    /// Voltea una carta del jugador autenticado y sincroniza la jugada con
    /// los dos jugadores.
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

        var userId = MemoryAuth.UserId(Context.User!);

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

        // El resultado lo guarda el backend (contrato 04), junto con el aviso
        // pendiente para Matchmaking. Si la BD falla, ResultRetryWorker lo
        // vuelve a intentar mientras la partida siga en memoria.
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

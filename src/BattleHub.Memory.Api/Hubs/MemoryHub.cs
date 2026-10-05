using BattleHub.Memory.Api.Auth;
using BattleHub.Memory.Api.Matchmaking;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace BattleHub.Memory.Api.Hubs;
[Authorize(Policy = MemoryAuth.Play)]
public sealed class MemoryHub(MemoryGameService games, TurnTimerService timers, MatchResultRecorder recorder, IMatchmakingGateway matchmaking, ILogger<MemoryHub> logger) : Hub
{
    public async Task JoinMatch(string matchId, string matchmakingAccessToken)
    {
        var userId = MemoryAuth.UserId(Context.User!);
        var completed = games.TryGetSession(matchId, out var restored) && restored.IsFinished;
        var room = await matchmaking.ValidateAsync(matchId, userId, matchmakingAccessToken, Context.ConnectionAborted, completed);
        if (games.TryGetSession(matchId, out var existing) && !existing.Players.Select(p => p.UserId).Order().SequenceEqual(room.Participants.Select(p => p.UserId).Order()))
            throw new HubException("MATCH_ROSTER_CHANGED");
        var player = room.Participants.Single(p => p.UserId == userId);
        var session = games.AddPlayer(matchId, new Player(userId, player.DisplayName), out var created);
        Context.Items["match"] = matchId;
        await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
        await Clients.Group(matchId).SendAsync("PlayerJoined", new { UserId = userId, player.DisplayName });
        if (session is null) return;
        if (created)
        {
            session.Board.RevealAll();
            await Clients.Group(matchId).SendAsync("GameReady", new { MatchId = matchId, PreviewSeconds = session.Board.PreviewSeconds,
                Cards = session.Board.Cards.Select(c => new { c.Id, c.Value }), Players = session.Players.Select(p => new { p.UserId, p.DisplayName }) });
            await Task.Delay(TimeSpan.FromSeconds(session.Board.PreviewSeconds));
            session.Board.HideAll();
            games.EndPreview(matchId);
            timers.Start(session);
            await Clients.Group(matchId).SendAsync("PreviewFinished", new { MatchId = matchId, CurrentPlayerId = session.CurrentPlayer.UserId, TurnDeadline = timers.Deadline(matchId) });
        }
        await Clients.Caller.SendAsync("StateSnapshot", session.Snapshot(games.IsPreview(matchId), timers.Deadline(matchId), games.ResultSaved(matchId)));
    }
    public async Task FlipCard(string matchId, int cardId)
    {
        if (Context.Items["match"] as string != matchId) throw new HubException("JOIN_REQUIRED");
        if (!games.TryGetSession(matchId, out var session)) throw new HubException("MATCH_NOT_FOUND");
        if (games.IsPreview(matchId)) throw new HubException("PREVIEW_ACTIVE");
        var userId = MemoryAuth.UserId(Context.User!);
        FlipCardResult result;
        try { result = session.FlipCard(userId, cardId); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { throw new HubException(ex.Message); }
        if (result.Cards.Count == 2 && !result.IsFinished) timers.Start(session);
        if (result.IsFinished) timers.Stop(matchId);
        await Clients.Group(matchId).SendAsync("CardFlipped", new { MatchId = matchId, UserId = userId,
            Cards = result.Cards.Select(c => new { c.Id, c.Value }), result.IsMatch, result.CurrentPlayerId, result.IsFinished,
            TurnDeadline = timers.Deadline(matchId) });
        if (!result.IsFinished) return;
        try
        {
            await recorder.RecordAsync(session);
            games.MarkSaved(matchId);
            await Clients.Group(matchId).SendAsync("ResultSaved", new { MatchId = matchId });
        }
        catch (Exception)
        {
            logger.LogWarning("Resultado pendiente de guardar para {MatchId}.", matchId);
            await Clients.Group(matchId).SendAsync("ResultSaveFailed", new { MatchId = matchId });
        }
    }
}

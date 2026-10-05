using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BattleHub.Memory.Api.Auth;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Matchmaking;

public sealed record RoomPlayer(string UserId, string DisplayName);
public sealed record MatchRoom(string Id, string GameType, string Status, RoomPlayer[] Participants);
public enum FinishOutcome { Confirmed, Pending, Rejected }
public interface IMatchmakingGateway
{
    Task<MatchRoom> ValidateAsync(string matchId, string userId, string token, CancellationToken ct, bool allowFinished = false);
    Task<FinishOutcome> FinishAsync(string matchId, CancellationToken ct);
}

public sealed class MatchmakingGateway(HttpClient http, IConfiguration config, TimeProvider clock, ILogger<MatchmakingGateway> logger) : IMatchmakingGateway
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? machineToken;
    private DateTimeOffset expires;
    private Uri Url(string path)
    {
        var origin = config["Matchmaking:BaseUrl"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException("Configurar Matchmaking:BaseUrl.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + path);
    }
    public async Task<MatchRoom> ValidateAsync(string matchId, string userId, string token, CancellationToken ct, bool allowFinished = false)
    {
        if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 64 || string.IsNullOrWhiteSpace(token) || token.Length > 16000)
            throw new HubException("UNAUTHORIZED");
        using var request = new HttpRequestMessage(HttpMethod.Get, Url("/api/matches/" + Uri.EscapeDataString(matchId)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new HubException("MATCH_NOT_FOUND");
        var room = await response.Content.ReadFromJsonAsync<MatchRoom>(cancellationToken: ct);
        if (room is null || room.Id != matchId || room.GameType != "memory" || (room.Status != "Started" && !(allowFinished && room.Status == "Finished"))) throw new HubException("MATCH_NOT_FOUND");
        if (room.Participants is null || room.Participants.Length != 2
            || room.Participants.Any(p => p is null || string.IsNullOrWhiteSpace(p.UserId) || p.UserId.Length > 64 || string.IsNullOrWhiteSpace(p.DisplayName) || p.DisplayName.Length > 100)
            || room.Participants.Select(p => p.UserId).Distinct(StringComparer.Ordinal).Count() != room.Participants.Length
            || !room.Participants.Any(p => p.UserId == userId))
            throw new HubException("PLAYER_NOT_IN_MATCH");
        return room;
    }
    private async Task<string> TokenAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (machineToken is not null && clock.GetUtcNow() < expires) return machineToken;
            var domain = config["Matchmaking:Auth0:Domain"];
            var id = config["Matchmaking:Auth0:ClientId"];
            var secret = config["Matchmaking:Auth0:ClientSecret"];
            var audience = config["Matchmaking:Auth0:Audience"];
            if (string.IsNullOrWhiteSpace(domain) || domain.Contains('/') || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(audience))
                throw new InvalidOperationException("Configurar las credenciales M2M de Memory para Matchmaking.");
            using var response = await http.PostAsJsonAsync($"https://{domain}/oauth/token", new
            { grant_type = "client_credentials", client_id = id, client_secret = secret, audience }, ct);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            if (data is null || string.IsNullOrWhiteSpace(data.access_token) || data.expires_in <= 0) throw new InvalidOperationException("Token M2M inválido.");
            machineToken = data.access_token;
            expires = clock.GetUtcNow().AddSeconds(Math.Max(1, data.expires_in - 60));
            return machineToken;
        }
        finally { gate.Release(); }
    }
    public async Task<FinishOutcome> FinishAsync(string matchId, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/matches/" + Uri.EscapeDataString(matchId) + "/finish"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode) return FinishOutcome.Confirmed;
                if (response.StatusCode != HttpStatusCode.RequestTimeout && (int)response.StatusCode != 429 && (int)response.StatusCode < 500)
                {
                    logger.LogWarning("Matchmaking rechazó finalizar {MatchId}: {Status}", matchId, (int)response.StatusCode);
                    return FinishOutcome.Rejected;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
            { logger.LogWarning("No se confirmó la finalización de {MatchId}, intento {Attempt}", matchId, attempt); }
            if (attempt < 3) await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), clock, ct);
        }
        return FinishOutcome.Pending;
    }
    private sealed record TokenResponse(string access_token, int expires_in);
}

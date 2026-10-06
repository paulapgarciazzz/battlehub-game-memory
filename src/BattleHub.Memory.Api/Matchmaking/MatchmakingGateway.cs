using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BattleHub.Memory.Api.Auth;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Matchmaking;

/// <summary>Participante de una sala, tal como lo devuelve Matchmaking.</summary>
public sealed record RoomPlayer(string UserId, string DisplayName);

/// <summary>Respuesta de GET /api/matches/{matchId} (solo los campos que usa Memory).</summary>
public sealed record MatchRoom(string Id, string GameType, string Status, RoomPlayer[] Participants);

/// <summary>Resultado de avisar el fin de partida a Matchmaking.</summary>
public enum FinishOutcome
{
    /// <summary>Matchmaking respondió 2xx: la sala quedó en Finished.</summary>
    Confirmed,

    /// <summary>Falla temporal (red, 408, 429 o 5xx): se vuelve a intentar más tarde.</summary>
    Pending,

    /// <summary>Rechazo permanente (401, 403, 404, 409...): queda bloqueado para revisión.</summary>
    Rejected
}

public interface IMatchmakingGateway
{
    Task<MatchRoom> ValidateAsync(string matchId, string userId, string token, CancellationToken ct, bool allowFinished = false);

    Task<FinishOutcome> FinishAsync(string matchId, CancellationToken ct);
}

/// <summary>
/// Comunicación con Matchmaking (Equipo 2):
/// - Validar la sala con el token del jugador (GET /api/matches/{id}).
/// - Avisar el fin de partida con un token M2M (POST /api/matches/{id}/finish, ADR-004).
/// </summary>
public sealed class MatchmakingGateway(
    HttpClient http,
    IConfiguration config,
    TimeProvider clock,
    ILogger<MatchmakingGateway> logger) : IMatchmakingGateway
{
    // El token M2M se pide una vez y se reutiliza hasta que vence. El plan
    // gratuito de Auth0 limita los tokens M2M por mes.
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? machineToken;
    private DateTimeOffset expires;

    /// <summary>
    /// Arma la URL a partir de Matchmaking:BaseUrl. Se valida que sea una URL
    /// http(s) limpia, porque el token del usuario solo debe viajar ahí.
    /// </summary>
    private Uri Url(string path)
    {
        var origin = config["Matchmaking:BaseUrl"];

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https")
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0)
        {
            throw new InvalidOperationException("Configurar Matchmaking:BaseUrl.");
        }

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + path);
    }

    /// <summary>
    /// Consulta la sala en Matchmaking con el token del jugador y comprueba
    /// que se pueda jugar: juego memory, estado Started (o Finished si se está
    /// volviendo a una partida terminada), exactamente 2 participantes
    /// distintos y que el jugador sea uno de ellos.
    /// </summary>
    public async Task<MatchRoom> ValidateAsync(
        string matchId,
        string userId,
        string token,
        CancellationToken ct,
        bool allowFinished = false)
    {
        if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 64
            || string.IsNullOrWhiteSpace(token) || token.Length > 16000)
        {
            throw new HubException("UNAUTHORIZED");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            Url("/api/matches/" + Uri.EscapeDataString(matchId)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
            throw new HubException("MATCH_NOT_FOUND");

        var room = await response.Content.ReadFromJsonAsync<MatchRoom>(cancellationToken: ct);

        var playableStatus = room?.Status == "Started"
            || (allowFinished && room?.Status == "Finished");

        if (room is null || room.Id != matchId || room.GameType != "memory" || !playableStatus)
            throw new HubException("MATCH_NOT_FOUND");

        var validRoster = room.Participants is not null
            && room.Participants.Length == 2
            && room.Participants.All(p => p is not null
                && !string.IsNullOrWhiteSpace(p.UserId) && p.UserId.Length <= 64
                && !string.IsNullOrWhiteSpace(p.DisplayName) && p.DisplayName.Length <= 100)
            && room.Participants.Select(p => p.UserId).Distinct(StringComparer.Ordinal).Count() == room.Participants.Length
            && room.Participants.Any(p => p.UserId == userId);

        if (!validRoster)
            throw new HubException("PLAYER_NOT_IN_MATCH");

        return room;
    }

    /// <summary>
    /// Devuelve el token M2M (client credentials) para Matchmaking. Lo renueva
    /// un minuto antes de que venza. El Client Secret viene de user-secrets o
    /// de variables de entorno, nunca del repo.
    /// </summary>
    private async Task<string> TokenAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);

        try
        {
            if (machineToken is not null && clock.GetUtcNow() < expires)
                return machineToken;

            var domain = config["Matchmaking:Auth0:Domain"];
            var id = config["Matchmaking:Auth0:ClientId"];
            var secret = config["Matchmaking:Auth0:ClientSecret"];
            var audience = config["Matchmaking:Auth0:Audience"];

            if (string.IsNullOrWhiteSpace(domain) || domain.Contains('/')
                || string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(secret)
                || string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException("Configurar las credenciales M2M de Memory para Matchmaking.");
            }

            using var response = await http.PostAsJsonAsync(
                $"https://{domain}/oauth/token",
                new { grant_type = "client_credentials", client_id = id, client_secret = secret, audience },
                ct);

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);

            if (data is null || string.IsNullOrWhiteSpace(data.access_token) || data.expires_in <= 0)
                throw new InvalidOperationException("Token M2M inválido.");

            machineToken = data.access_token;
            expires = clock.GetUtcNow().AddSeconds(Math.Max(1, data.expires_in - 60));

            return machineToken;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Avisa a Matchmaking que la partida terminó. Hace hasta 3 intentos por
    /// llamada para las fallas temporales; los rechazos permanentes no se
    /// reintentan.
    /// </summary>
    public async Task<FinishOutcome> FinishAsync(string matchId, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    Url("/api/matches/" + Uri.EscapeDataString(matchId) + "/finish"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));

                using var response = await http.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                    return FinishOutcome.Confirmed;

                var status = (int)response.StatusCode;
                var transient = response.StatusCode == HttpStatusCode.RequestTimeout
                    || status == 429
                    || status >= 500;

                if (!transient)
                {
                    // Por ejemplo 403 si el Client ID de Memory no está
                    // registrado en GameServices:Clients de Matchmaking.
                    logger.LogWarning("Matchmaking rechazó finalizar {MatchId}: {Status}", matchId, status);
                    return FinishOutcome.Rejected;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
            {
                logger.LogWarning(ex, "No se confirmó la finalización de {MatchId}, intento {Attempt}", matchId, attempt);
            }

            if (attempt < 3)
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), clock, ct);
        }

        return FinishOutcome.Pending;
    }

    // Respuesta de /oauth/token de Auth0 (los nombres siguen el JSON).
    private sealed record TokenResponse(string access_token, int expires_in);
}

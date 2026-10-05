using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BattleHub.Memory.Api.Auth;

/// <summary>
/// Autenticación con Auth0 y políticas de autorización de Memory.
/// </summary>
public static class MemoryAuth
{
    /// <summary>Jugador autenticado (no un cliente M2M): hub, historial y estadísticas.</summary>
    public const string Play = "MemoryPlay";

    /// <summary>Lectura de resultados: un jugador o un servicio con permiso de escritura.</summary>
    public const string Read = "MemoryRead";

    /// <summary>Escritura de resultados: solo servicios M2M con games.memory.results.write.</summary>
    public const string Write = "MemoryWrite";

    /// <summary>Identificador estable del usuario (claim "sub" del token).</summary>
    public static string UserId(ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value ?? "";

    /// <summary>
    /// true si el token es de un cliente máquina a máquina (client credentials).
    /// Un servicio no puede jugar como si fuera un usuario.
    /// </summary>
    public static bool IsMachine(ClaimsPrincipal user) =>
        user.FindFirst("gty")?.Value == "client-credentials"
        || UserId(user).EndsWith("@clients", StringComparison.Ordinal);

    /// <summary>Busca el permiso en "permissions" (RBAC de Auth0) o en "scope".</summary>
    public static bool HasPermission(ClaimsPrincipal user, string permission) =>
        user.FindAll("permissions").Any(c => c.Value == permission)
        || user.FindFirst("scope")?.Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(permission) == true;

    public static bool IsWriter(ClaimsPrincipal user) =>
        IsMachine(user) && HasPermission(user, "games.memory.results.write");

    public static IServiceCollection AddMemoryAuth(this IServiceCollection services, IConfiguration config)
    {
        // Auth:Domain es solo el hostname del tenant (sin https:// ni barras).
        var domain = config["Auth:Domain"]?.Trim();
        var audience = config["Auth:Audience"]?.Trim();

        if (string.IsNullOrWhiteSpace(domain) || domain.Contains('/') || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Configurar Auth:Domain (hostname) y Auth:Audience de Memory.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
        {
            jwt.Authority = $"https://{domain}/";
            jwt.Audience = audience;

            // Se conservan los nombres de claims de Auth0 ("sub", "permissions").
            jwt.MapInboundClaims = false;
            jwt.RequireHttpsMetadata = true;

            // Firma RS256 del tenant, emisor, audiencia de Memory y vigencia,
            // con 30 segundos de tolerancia de reloj.
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://{domain}/",
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                NameClaimType = "name",
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            jwt.Events = new JwtBearerEvents
            {
                // SignalR (WebSockets) no puede mandar el encabezado
                // Authorization, así que el token llega por ?access_token=.
                OnMessageReceived = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/hubs/memory"))
                        ctx.Token = ctx.Request.Query["access_token"];

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Play, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(c => !IsMachine(c.User) && UserId(c.User).Length > 0));

            options.AddPolicy(Read, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(c => (!IsMachine(c.User) && UserId(c.User).Length > 0) || IsWriter(c.User)));

            options.AddPolicy(Write, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(c => IsWriter(c.User)));
        });

        return services;
    }
}

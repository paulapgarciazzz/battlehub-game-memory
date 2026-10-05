using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BattleHub.Memory.Api.Auth;

public static class MemoryAuth
{
    public const string Play = "MemoryPlay";
    public const string Read = "MemoryRead";
    public const string Write = "MemoryWrite";
    public static string UserId(ClaimsPrincipal user) => user.FindFirst("sub")?.Value ?? "";
    public static bool IsMachine(ClaimsPrincipal user) => user.FindFirst("gty")?.Value == "client-credentials" || UserId(user).EndsWith("@clients", StringComparison.Ordinal);
    public static bool HasPermission(ClaimsPrincipal user, string permission) => user.FindAll("permissions").Any(c => c.Value == permission)
        || user.FindFirst("scope")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(permission) == true;
    public static bool IsWriter(ClaimsPrincipal user) => IsMachine(user) && HasPermission(user, "games.memory.results.write");

    public static IServiceCollection AddMemoryAuth(this IServiceCollection services, IConfiguration config)
    {
        var domain = config["Auth:Domain"]?.Trim();
        var audience = config["Auth:Audience"]?.Trim();
        if (string.IsNullOrWhiteSpace(domain) || domain.Contains('/') || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Configurar Auth:Domain (hostname) y Auth:Audience de Memory.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
        {
            jwt.Authority = $"https://{domain}/";
            jwt.Audience = audience;
            jwt.MapInboundClaims = false;
            jwt.RequireHttpsMetadata = true;
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = $"https://{domain}/", ValidateAudience = true,
                ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                NameClaimType = "name", ClockSkew = TimeSpan.FromSeconds(30)
            };
            jwt.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/hubs/memory")) ctx.Token = ctx.Request.Query["access_token"];
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Play, p => p.RequireAuthenticatedUser().RequireAssertion(c => !IsMachine(c.User) && UserId(c.User).Length > 0));
            options.AddPolicy(Read, p => p.RequireAuthenticatedUser().RequireAssertion(c => (!IsMachine(c.User) && UserId(c.User).Length > 0) || IsWriter(c.User)));
            options.AddPolicy(Write, p => p.RequireAuthenticatedUser().RequireAssertion(c => IsWriter(c.User)));
        });
        return services;
    }
}

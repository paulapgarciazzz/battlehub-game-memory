using System.Security.Claims;
using BattleHub.Memory.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BattleHub.Memory.Api.Tests.Auth;

public sealed class MemoryAuthTests
{
    [Fact]
    public void IdentifiesUsersMachinesAndPermissions()
    {
        var user = Principal(new Claim("sub", "auth0|player"));
        var machineByGrant = Principal(
            new Claim("sub", "service@clients"),
            new Claim("gty", "client-credentials"));
        var machineBySubject = Principal(new Claim("sub", "fallback@clients"));
        var permissionClaim = Principal(new Claim("permissions", "games.memory.results.write"));
        var scopeClaim = Principal(new Claim("scope", "profile games.memory.results.write"));

        Assert.Equal("auth0|player", MemoryAuth.UserId(user));
        Assert.Equal(string.Empty, MemoryAuth.UserId(Principal()));
        Assert.False(MemoryAuth.IsMachine(user));
        Assert.True(MemoryAuth.IsMachine(machineByGrant));
        Assert.True(MemoryAuth.IsMachine(machineBySubject));
        Assert.True(MemoryAuth.HasPermission(permissionClaim, "games.memory.results.write"));
        Assert.True(MemoryAuth.HasPermission(scopeClaim, "games.memory.results.write"));
        Assert.False(MemoryAuth.HasPermission(user, "games.memory.results.write"));
        Assert.True(MemoryAuth.IsWriter(machineByGrantWithPermission()));
        Assert.False(MemoryAuth.IsWriter(permissionClaim));
    }

    [Fact]
    public void AddMemoryAuthRequiresAValidDomainAndAudience()
    {
        var invalidDomain = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            invalidDomain.AddMemoryAuth(Configuration("auth0.example/path", "audience")));

        var missingAudience = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            missingAudience.AddMemoryAuth(Configuration("auth0.example", "")));
    }

    [Fact]
    public async Task JwtBearerReadsQueryTokensOnlyForMemoryHubRequests()
    {
        var services = new ServiceCollection();
        services.AddMemoryAuth(Configuration("auth0.example", "memory-api"));
        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        var hubContext = new MessageReceivedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme,
                typeof(JwtBearerHandler)),
            options);
        hubContext.Request.Path = "/hubs/memory";
        hubContext.Request.QueryString = new QueryString("?access_token=hub-token");
        await options.Events.OnMessageReceived(hubContext);

        var httpContext = new MessageReceivedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme,
                typeof(JwtBearerHandler)),
            options);
        httpContext.Request.Path = "/api/games/memory";
        httpContext.Request.QueryString = new QueryString("?access_token=api-token");
        await options.Events.OnMessageReceived(httpContext);

        Assert.Equal("hub-token", hubContext.Token);
        Assert.Null(httpContext.Token);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static ClaimsPrincipal machineByGrantWithPermission() =>
        Principal(
            new Claim("sub", "service@clients"),
            new Claim("permissions", "games.memory.results.write"));

    private static IConfiguration Configuration(string domain, string audience) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Domain"] = domain,
                ["Auth:Audience"] = audience
            })
            .Build();
}

using System.Net;
using System.Net.Http.Json;
using BattleHub.Memory.Api.Matchmaking;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BattleHub.Memory.IntegrationTests;

public sealed class MatchmakingGatewayTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
    private static IConfiguration Config => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["Matchmaking:BaseUrl"] = "http://localhost:5211", ["Matchmaking:Auth0:Domain"] = "test.example",
        ["Matchmaking:Auth0:Audience"] = "https://api.battlehub.local/profile", ["Matchmaking:Auth0:ClientId"] = "test-client",
        ["Matchmaking:Auth0:ClientSecret"] = "test-only-not-a-real-secret"
    }).Build();
    [Fact]
    public async Task ForwardsRoomTokenAndRejectsNonParticipant()
    {
        using var http = new HttpClient(new Handler(request => {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("room-token", request.Headers.Authorization.Parameter);
            Assert.Equal("/api/matches/room", request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new MatchRoom("room", "memory", "Started", [new("auth0|one", "Uno"), new("auth0|two", "Dos")])) };
        }));
        var gateway = new MatchmakingGateway(http, Config, TimeProvider.System, NullLogger<MatchmakingGateway>.Instance);
        Assert.Equal("room", (await gateway.ValidateAsync("room", "auth0|one", "room-token", default)).Id);
        await Assert.ThrowsAsync<HubException>(() => gateway.ValidateAsync("room", "auth0|intruder", "room-token", default));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task RequiresExactlyTwoParticipants(int count)
    {
        var players = Enumerable.Range(0, count).Select(i => new RoomPlayer("auth0|" + i, "Jugador " + i)).ToArray();
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new MatchRoom("room", "memory", "Started", players)) }));
        var gateway = new MatchmakingGateway(http, Config, TimeProvider.System, NullLogger<MatchmakingGateway>.Instance);
        await Assert.ThrowsAsync<HubException>(() => gateway.ValidateAsync("room", "auth0|0", "room-token", default));
    }
    [Theory]
    [InlineData("typing", "Started")]
    [InlineData("memory", "Waiting")]
    [InlineData("memory", "Cancelled")]
    public async Task RejectsWrongGameOrStatus(string game, string status)
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new MatchRoom("room", game, status, [new("auth0|one", "Uno"), new("auth0|two", "Dos")])) }));
        var gateway = new MatchmakingGateway(http, Config, TimeProvider.System, NullLogger<MatchmakingGateway>.Instance);
        await Assert.ThrowsAsync<HubException>(() => gateway.ValidateAsync("room", "auth0|one", "room-token", default));
    }
    [Theory]
    [InlineData(204, FinishOutcome.Confirmed)]
    [InlineData(401, FinishOutcome.Rejected)]
    [InlineData(403, FinishOutcome.Rejected)]
    [InlineData(409, FinishOutcome.Rejected)]
    public async Task FinishUsesMachineTokenAndDoesNotRetryPermanentRejection(int code, FinishOutcome expected)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(request => {
            if (request.RequestUri!.AbsolutePath == "/oauth/token") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "machine-token", expires_in = 3600 }) };
            calls++; Assert.Equal(HttpMethod.Post, request.Method); Assert.Null(request.Content);
            Assert.Equal("/api/matches/room/finish", request.RequestUri.AbsolutePath);
            Assert.Equal("machine-token", request.Headers.Authorization!.Parameter);
            return new((HttpStatusCode)code);
        }));
        var gateway = new MatchmakingGateway(http, Config, TimeProvider.System, NullLogger<MatchmakingGateway>.Instance);
        Assert.Equal(expected, await gateway.FinishAsync("room", default));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task FinishRetriesTransientFailuresOnlyThreeTimes()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(request => {
            if (request.RequestUri!.AbsolutePath == "/oauth/token") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "machine-token", expires_in = 3600 }) };
            calls++; return new(HttpStatusCode.ServiceUnavailable);
        }));
        var gateway = new MatchmakingGateway(http, Config, TimeProvider.System, NullLogger<MatchmakingGateway>.Instance);
        Assert.Equal(FinishOutcome.Pending, await gateway.FinishAsync("room", default));
        Assert.Equal(3, calls);
    }
}

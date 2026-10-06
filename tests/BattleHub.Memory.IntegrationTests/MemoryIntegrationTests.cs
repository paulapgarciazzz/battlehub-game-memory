using System.Net;
using System.Text.Json;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Api.Matchmaking;
using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Data.Services;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
namespace BattleHub.Memory.IntegrationTests;
[Collection(MemoryDatabaseCollection.Name)]
public sealed class MemoryIntegrationTests(MemoryApiFactory factory)
{
    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("audience", 401)]
    [InlineData("issuer", 401)]
    [InlineData("expired", 401)]
    [InlineData("machine", 403)]
    [InlineData("player", 200)]
    public async Task HubValidatesPlayerCredentials(string scenario, int status)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = scenario == "anonymous" ? null : new("Bearer", factory.Token(
            scenario == "machine" ? "service@clients" : "auth0|one",
            audience: scenario == "audience" ? "https://api.battlehub.local/profile" : MemoryApiFactory.Audience,
            issuer: scenario == "issuer" ? "https://wrong.example/" : MemoryApiFactory.Issuer,
            expires: scenario == "expired" ? DateTime.UtcNow.AddMinutes(-10) : null));
        Assert.Equal(status, (int)(await client.PostAsync("/hubs/memory/negotiate?negotiateVersion=1", null)).StatusCode);
    }
    [Fact]
    public async Task CorsPermitsShellPreflight()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/memory/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "http://localhost:4000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:4000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }
    [Fact]
    public async Task HistoryIsPrivateAndRestDoesNotAcceptQueryToken()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("auth0|one"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/games/memory/players/auth0%7Ctwo/history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/games/memory/players/auth0%7Cone/history")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/games/memory/players/auth0%7Cone/history?access_token=" + factory.Token("auth0|one"))).StatusCode);
    }
    [Fact]
    public async Task TwoJwtClientsPlayReconnectPersistAndNotify()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token; var id = "live-" + Guid.NewGuid().ToString("N");
        await using var one = Connection("auth0|one");
        await using var two = Connection("auth0|two");
        await using var intruder = Connection("auth0|intruder");
        await one.StartAsync(ct); await two.StartAsync(ct); await intruder.StartAsync(ct);
        await Assert.ThrowsAsync<HubException>(() => intruder.InvokeAsync("JoinMatch", id, "room-token", ct));
        await Assert.ThrowsAsync<HubException>(() => one.InvokeAsync("FlipCard", id, 0, ct));
        await one.InvokeAsync("JoinMatch", id, "room-token", ct);
        await two.InvokeAsync("JoinMatch", id, "room-token", ct);
        var game = factory.Services.GetRequiredService<MemoryGameService>().GetSession(id);
        Assert.Equal(2, game.Players.Count);
        var pairGroups = game.Board.Cards.GroupBy(c => c.Value).ToArray();
        await one.InvokeAsync("FlipCard", id, pairGroups[0].First().Id, ct);
        await one.InvokeAsync("FlipCard", id, pairGroups[0].Last().Id, ct);
        await one.StopAsync(ct); await one.StartAsync(ct);
        var restored = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        one.On<JsonElement>("StateSnapshot", value => restored.TrySetResult(value));
        await one.InvokeAsync("JoinMatch", id, "room-token", ct);
        var snapshot = await restored.Task.WaitAsync(ct);
        Assert.Equal(1, snapshot.GetProperty("players")[0].GetProperty("matchedPairs").GetInt32());
        Assert.Equal(2, snapshot.GetProperty("cards").EnumerateArray().Count(c => c.GetProperty("matched").GetBoolean()));
        Assert.Contains(snapshot.GetProperty("cards").EnumerateArray(), c => c.GetProperty("value").ValueKind == JsonValueKind.Null);
        foreach (var pair in pairGroups.Skip(1))
        {
            await one.InvokeAsync("FlipCard", id, pair.First().Id, ct);
            await one.InvokeAsync("FlipCard", id, pair.Last().Id, ct);
        }
        using var scope = factory.Services.CreateScope();
        var resultService = scope.ServiceProvider.GetRequiredService<IGameResultService>();
        var saved = await resultService.GetByMatchIdAsync(id, ct);
        Assert.NotNull(saved); Assert.Equal("auth0|one", saved.WinnerUserId);
        Assert.Equal(8, saved.Players.Single(p => p.UserId == "auth0|one").Score);
        var same = await factory.Services.GetRequiredService<MatchResultRecorder>().RecordAsync(game, ct);
        Assert.Equal(saved.Id, same);
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        Assert.Single(await db.GameResults.Where(r => r.MatchId == id).ToListAsync(ct));
        Assert.False((await db.FinishNotifications.AsNoTracking().SingleAsync(r => r.MatchId == id, ct)).Delivered);
        var worker = new FinishWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Services.GetRequiredService<IMatchmakingGateway>(), factory.Clock, NullLogger<FinishWorker>.Instance);
        factory.Clock.Advance(2);
        await worker.DispatchAsync(ct);
        Assert.True((await db.FinishNotifications.AsNoTracking().SingleAsync(r => r.MatchId == id, ct)).Delivered);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("auth0|intruder"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/games/memory/results/" + id, ct)).StatusCode);
    }
    private HubConnection Connection(string user) => new HubConnectionBuilder().WithUrl("http://localhost/hubs/memory", options =>
    {
        options.Transports = HttpTransportType.LongPolling;
        options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        options.AccessTokenProvider = () => Task.FromResult<string?>(factory.Token(user));
    }).Build();
}

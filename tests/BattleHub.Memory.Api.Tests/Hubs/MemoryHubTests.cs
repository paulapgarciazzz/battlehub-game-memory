using System.Net;
using System.Text.Json;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Data.Services;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace BattleHub.Memory.Api.Tests.Hubs;
[Collection(MemoryDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MemoryHubTests(MemoryApiFactory factory)
{
    [Fact]
    public async Task HubAcceptsConnectionsWithoutToken()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/memory/negotiate?negotiateVersion=1", null)).StatusCode);
    }
    [Fact]
    public async Task CorsPermitsShellPreflight()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/memory/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "http://localhost:4000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-requested-with");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:4000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }
    [Theory]
    [InlineData("", "user-one", "Uno")]
    [InlineData("match", "", "Uno")]
    [InlineData("match", "user-one", "")]
    public async Task JoinMatchRejectsMissingData(string matchId, string userId, string displayName)
    {
        await using var connection = Connection();
        await connection.StartAsync();
        var error = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinMatch", matchId, userId, displayName));
        Assert.Contains("INVALID_JOIN", error.Message);
    }
    [Fact]
    public async Task TwoClientsPlayReconnectAndPersist()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token; var id = "live-" + Guid.NewGuid().ToString("N");
        await using var one = Connection();
        await using var two = Connection();
        await using var third = Connection();
        await one.StartAsync(ct); await two.StartAsync(ct); await third.StartAsync(ct);
        await Assert.ThrowsAsync<HubException>(() => one.InvokeAsync("FlipCard", id, 0, ct));
        await one.InvokeAsync("JoinMatch", id, "user-one", "Uno", ct);
        await two.InvokeAsync("JoinMatch", id, "user-two", "Dos", ct);
        // La partida ya tiene a sus dos jugadores: un tercero no puede entrar.
        await Assert.ThrowsAsync<HubException>(() => third.InvokeAsync("JoinMatch", id, "user-three", "Tres", ct));
        var game = factory.Services.GetRequiredService<MemoryGameService>().GetSession(id);
        Assert.Equal(2, game.Players.Count);
        var pairGroups = game.Board.Cards.GroupBy(c => c.Value).ToArray();
        await one.InvokeAsync("FlipCard", id, pairGroups[0].First().Id, ct);
        await one.InvokeAsync("FlipCard", id, pairGroups[0].Last().Id, ct);
        await one.StopAsync(ct); await one.StartAsync(ct);
        var restored = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        one.On<JsonElement>("StateSnapshot", value => restored.TrySetResult(value));
        await one.InvokeAsync("JoinMatch", id, "user-one", "Uno", ct);
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
        Assert.NotNull(saved); Assert.Equal("user-one", saved.WinnerUserId);
        Assert.Equal(8, saved.Players.Single(p => p.UserId == "user-one").Score);
        var same = await factory.Services.GetRequiredService<MatchResultRecorder>().RecordAsync(game, ct);
        Assert.Equal(saved.Id, same);
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        Assert.Single(await db.GameResults.Where(r => r.MatchId == id).ToListAsync(ct));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/games/memory/results/" + id, ct)).StatusCode);
    }
    private HubConnection Connection() => new HubConnectionBuilder().WithUrl("http://localhost/hubs/memory", options =>
    {
        options.Transports = HttpTransportType.LongPolling;
        options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
    }).Build();
}

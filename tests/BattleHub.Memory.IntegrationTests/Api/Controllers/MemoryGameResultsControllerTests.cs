using System.Net;
using System.Net.Http.Json;
using BattleHub.Memory.Api.Services;
using BattleHub.Memory.Data.Services;
using BattleHub.Memory.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.Memory.IntegrationTests.Api.Controllers;

[Collection(ApiDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MemoryGameResultsControllerTests(MemoryApiFactory factory)
{
    private static GameSession CreateSession(string matchId)
    {
        var players = new[]
        {
            new Player("user-one", "Uno"),
            new Player("user-two", "Dos")
        };
        var session = new GameSession(
            matchId,
            new Board(["A", "B", "C", "D", "E", "F", "G", "H"]),
            players);

        return session;
    }

    private static void Finish(GameSession session)
    {
        foreach (var pair in session.Board.Cards.GroupBy(card => card.Value))
        {
            session.FlipCard(session.CurrentPlayer.UserId, pair.First().Id);
            session.FlipCard(session.CurrentPlayer.UserId, pair.Last().Id);
        }
    }

    [Fact]
    public async Task GetResultReturnsNotFoundForMissingAndTheSavedResult()
    {
        var matchId = "result-" + Guid.NewGuid().ToString("N");
        var session = CreateSession(matchId);
        Finish(session);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<IGameResultService>()
                .SaveResultAsync(session, session.StartedAt);
        }

        using var player = factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await player.GetAsync("/api/games/memory/results/missing-" + matchId)).StatusCode);

        var response = await player.GetAsync("/api/games/memory/results/" + matchId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(matchId, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveResultValidatesCompletionAndExistingResults()
    {
        using var player = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await player.PostAsJsonAsync(
                "/api/games/memory/results",
                new {matchId = " "})).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await player.PostAsJsonAsync(
                "/api/games/memory/results",
                new {matchId = "missing-" + Guid.NewGuid().ToString("N")})).StatusCode);

        var games = factory.Services.GetRequiredService<MemoryGameService>();
        var unfinishedId = "unfinished-" + Guid.NewGuid().ToString("N");
        games.CreateSession(unfinishedId, [
            new Player("user-one", "Uno"),
            new Player("user-two", "Dos")
        ]);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await player.PostAsJsonAsync(
                "/api/games/memory/results",
                new {matchId = unfinishedId})).StatusCode);

        var matchId = "save-" + Guid.NewGuid().ToString("N");
        var session = CreateSession(matchId);
        games.CreateSession(matchId, session.Players);
        foreach (var pair in games.GetSession(matchId).Board.Cards.GroupBy(card => card.Value))
        {
            games.GetSession(matchId).FlipCard(games.GetSession(matchId).CurrentPlayer.UserId, pair.First().Id);
            games.GetSession(matchId).FlipCard(games.GetSession(matchId).CurrentPlayer.UserId, pair.Last().Id);
        }

        var saved = await player.PostAsJsonAsync(
            "/api/games/memory/results",
            new {matchId});
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("guardado correctamente", await saved.Content.ReadAsStringAsync());

        games.RemoveSession(matchId);
        var alreadySaved = await player.PostAsJsonAsync(
            "/api/games/memory/results",
            new {matchId});
        Assert.Equal(HttpStatusCode.OK, alreadySaved.StatusCode);
        Assert.Contains("ya estaba guardado", await alreadySaved.Content.ReadAsStringAsync());
    }
}

using System.Net;

namespace BattleHub.Memory.Api.Tests.Controllers;

[Collection(MemoryDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MemoryControllerTests(MemoryApiFactory factory)
{
    [Fact]
    public async Task HistoryIsPrivateAndRestDoesNotAcceptQueryToken()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("auth0|one"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/games/memory/players/auth0%7Ctwo/history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/games/memory/players/auth0%7Cone/history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/games/memory/players/auth0%7Cone/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/games/memory/players/auth0%7Ctwo/stats")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/games/memory/players/auth0%7Cone/history?access_token=" + factory.Token("auth0|one"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/games/memory/players/auth0%7Cone/stats")).StatusCode);
    }
}

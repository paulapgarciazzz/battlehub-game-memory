using System.Net;

namespace BattleHub.Memory.Api.Tests.Controllers;

[Collection(MemoryDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MemoryControllerTests(MemoryApiFactory factory)
{
    [Fact]
    public async Task HistoryAndStatsAnswerWithoutAuth0()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/games/memory/players/user-one/history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/games/memory/players/user-one/stats")).StatusCode);
    }
}

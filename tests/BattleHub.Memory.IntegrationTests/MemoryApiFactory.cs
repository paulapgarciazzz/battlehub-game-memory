using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Api.Matchmaking;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;

namespace BattleHub.Memory.IntegrationTests;

public sealed class MemoryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public sealed class TestClock : TimeProvider
    {
        private long ticks = DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(int seconds) => Interlocked.Add(ref ticks, TimeSpan.FromSeconds(seconds).Ticks);
    }
    private sealed class Rooms : IMatchmakingGateway
    {
        public Task<MatchRoom> ValidateAsync(string id, string user, string token, CancellationToken ct, bool allowFinished = false)
        {
            if (token != "room-token" || user is not ("auth0|one" or "auth0|two")) throw new Microsoft.AspNetCore.SignalR.HubException("PLAYER_NOT_IN_MATCH");
            return Task.FromResult(new MatchRoom(id, "memory", "Started", [new("auth0|one", "Uno"), new("auth0|two", "Dos")]));
        }
        public Task<FinishOutcome> FinishAsync(string id, CancellationToken ct) => Task.FromResult(FinishOutcome.Confirmed);
    }
    public TestClock Clock { get; } = new();
    public const string Issuer = "https://dev-jaii1peslxnejq0y.us.auth0.com/";
    public const string Audience = "https://api.battlehub.local/memory";
    private readonly RSA rsa = RSA.Create(2048);
    private MsSqlContainer? sql;
    private string? connectionString;
    private bool local;

    public string Token(string subject = "test-service@clients", string audience = Audience,
        string issuer = Issuer, bool writer = true, DateTime? expires = null)
    {
        var claims = new List<Claim> { new("sub", subject) };
        if (subject.EndsWith("@clients")) claims.Add(new("gty", "client-credentials"));
        if (writer) claims.Add(new("scope", "games.memory.results.write"));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, claims,
            DateTime.UtcNow.AddHours(-2), expires ?? DateTime.UtcNow.AddHours(1),
            new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "test-rsa" }, SecurityAlgorithms.RsaSha256)));
    }
    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        return client;
    }
    public async Task InitializeAsync()
    {
        Clock.Advance(120);
        var supplied = Environment.GetEnvironmentVariable("MEMORY_TEST_CONNECTION");
        local = !string.IsNullOrWhiteSpace(supplied);
        if (!local)
        {
            sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
            await sql.StartAsync();
            supplied = sql.GetConnectionString();
        }
        connectionString = new SqlConnectionStringBuilder(supplied!)
        { InitialCatalog = "BattleHubMemoryTests_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        await db.Database.MigrateAsync();
    }
    async Task IAsyncLifetime.DisposeAsync()
    {
        if (local && connectionString is not null)
        {
            var name = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            if (!name.StartsWith("BattleHubMemoryTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Base de prueba inválida.");
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MemoryDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
        if (sql is not null) await sql.DisposeAsync();
        rsa.Dispose();
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IMatchmakingGateway>();
            services.AddSingleton<IMatchmakingGateway, Rooms>();
            services.RemoveAll<DbContextOptions<MemoryDbContext>>();
            services.RemoveAll<MemoryDbContext>();
            services.AddDbContext<MemoryDbContext>(options => options.UseSqlServer(connectionString!));
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(new RsaSecurityKey(rsa) { KeyId = "test-rsa" });
                jwt.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }
}

[CollectionDefinition(Name)]
public sealed class MemoryDatabaseCollection : ICollectionFixture<MemoryApiFactory>
{
    public const string Name = "MemoryDatabase";
}

using BattleHub.Memory.Data.DbContext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace BattleHub.Memory.Api.Tests;

public sealed class MemoryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public sealed class TestClock : TimeProvider
    {
        private long ticks = DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(int seconds) => Interlocked.Add(ref ticks, TimeSpan.FromSeconds(seconds).Ticks);
    }
    public TestClock Clock { get; } = new();
    private MsSqlContainer? sql;
    private string? connectionString;
    private bool local;

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
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<DbContextOptions<MemoryDbContext>>();
            services.RemoveAll<MemoryDbContext>();
            services.AddDbContext<MemoryDbContext>(options => options.UseSqlServer(connectionString!));
        });
    }
}

[CollectionDefinition(Name)]
public sealed class MemoryDatabaseCollection : ICollectionFixture<MemoryApiFactory>
{
    public const string Name = "MemoryDatabase";
}

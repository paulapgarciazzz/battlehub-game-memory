using BattleHub.Memory.Data.DbContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace BattleHub.Memory.Data.Tests;

// Base SQL Server con las migraciones aplicadas. Igual que MemoryApiFactory:
// usa MEMORY_TEST_CONNECTION si existe y, si no, un contenedor.
public sealed class MemoryDatabaseFixture : IAsyncLifetime
{
    private MsSqlContainer? sql;
    private string connectionString = "";

    public MemoryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MemoryDbContext>().UseSqlServer(connectionString).Options);

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("MEMORY_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(supplied))
        {
            sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
            await sql.StartAsync();
            supplied = sql.GetConnectionString();
        }
        connectionString = new SqlConnectionStringBuilder(supplied)
        { InitialCatalog = "BattleHubMemoryTests_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // En la máquina local se borra la base de prueba; el contenedor se elimina entero.
        if (sql is null)
        {
            await using var db = CreateContext();
            await db.Database.EnsureDeletedAsync();
        }
        else await sql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MemoryDatabaseCollection : ICollectionFixture<MemoryDatabaseFixture>
{
    public const string Name = "MemoryDatabase";
}

using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.IntegrationTests.Data.DbContext;

[Collection(DataDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MemoryDbContextTests(MemoryDatabaseFixture database)
{
    [Fact]
    public async Task SchemaKeepsOnlyUsedTablesWithGuidPrimaryKeys()
    {
        await using var db = database.CreateContext();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT t.name, c.name, TYPE_NAME(c.user_type_id)
                FROM sys.key_constraints pk
                JOIN sys.tables t ON t.object_id = pk.parent_object_id
                JOIN sys.index_columns ic ON ic.object_id = t.object_id AND ic.index_id = pk.unique_index_id
                JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
                WHERE pk.type = 'PK' AND t.name <> '__EFMigrationsHistory';
                """;
            var primaryKeys = new Dictionary<string, (string Column, string Type)>();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    primaryKeys.Add(reader.GetString(0), (reader.GetString(1), reader.GetString(2)));
            }

            Assert.DoesNotContain("Matches", primaryKeys.Keys);
            Assert.DoesNotContain("Players", primaryKeys.Keys);
            Assert.DoesNotContain("Cards", primaryKeys.Keys);
            Assert.Contains("GameResults", primaryKeys.Keys);
            Assert.Contains("GameResultPlayers", primaryKeys.Keys);
            Assert.DoesNotContain("FinishNotifications", primaryKeys.Keys);
            Assert.All(primaryKeys.Values, key => Assert.Equal("uniqueidentifier", key.Type));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}

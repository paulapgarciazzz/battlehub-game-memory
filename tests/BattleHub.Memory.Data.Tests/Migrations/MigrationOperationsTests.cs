using System.Reflection;
using BattleHub.Memory.Data.DbContext;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BattleHub.Memory.Data.Tests.Migrations;

public sealed class MigrationOperationsTests
{
    [Fact]
    public void AllMigrationsBuildTheirUpDownOperationsAndTargetModels()
    {
        var migrations = typeof(MemoryDbContext).Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == "BattleHub.Memory.Data.Migrations"
                && !type.IsAbstract
                && typeof(Migration).IsAssignableFrom(type))
            .OrderBy(type => type.Name)
            .ToArray();

        Assert.Equal(7, migrations.Length);

        foreach (var migrationType in migrations)
        {
            var migration = (Migration)Activator.CreateInstance(migrationType)!;
            var upBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            var downBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");

            GetMigrationMethod(migrationType, "Up").Invoke(migration, [upBuilder]);
            GetMigrationMethod(migrationType, "Down").Invoke(migration, [downBuilder]);

            Assert.NotEmpty(upBuilder.Operations);
            if (migrationType.Name == "RemoveUnusedMemoryTables")
                Assert.Empty(downBuilder.Operations);
            else
                Assert.NotEmpty(downBuilder.Operations);

            Assert.NotNull(migration.TargetModel);
        }
    }

    private static MethodInfo GetMigrationMethod(Type migrationType, string methodName) =>
        migrationType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            $"Migration method {migrationType.Name}.{methodName} was not found.");
}

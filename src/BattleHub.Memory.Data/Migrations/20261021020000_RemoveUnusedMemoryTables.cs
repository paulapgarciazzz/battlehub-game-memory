using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleHub.Memory.Data.Migrations;

public partial class RemoveUnusedMemoryTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Tablas existentes en bases que ya aplicaron la migración inicial antigua.
        // Se comprueba la existencia para que también funcione en instalaciones nuevas.
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[Cards]', N'U') IS NOT NULL
                DROP TABLE [Cards];
            IF OBJECT_ID(N'[Players]', N'U') IS NOT NULL
                DROP TABLE [Players];
            IF OBJECT_ID(N'[Matches]', N'U') IS NOT NULL
                DROP TABLE [Matches];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Las tablas eran almacenamiento no utilizado; no se recrean ni se restauran datos.
    }
}

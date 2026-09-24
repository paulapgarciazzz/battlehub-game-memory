using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleHub.Memory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIsDraw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDraw",
                table: "GameResults",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDraw",
                table: "GameResults");
        }
    }
}

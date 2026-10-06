using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleHub.Memory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAvisosMatchmaking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinishNotifications",
                columns: table => new
                {
                    MatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Delivered = table.Column<bool>(type: "bit", nullable: false),
                    Blocked = table.Column<bool>(type: "bit", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinishNotifications", x => x.MatchId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinishNotifications");
        }
    }
}

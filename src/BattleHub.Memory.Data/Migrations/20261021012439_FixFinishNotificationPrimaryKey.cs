using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleHub.Memory.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFinishNotificationPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FinishNotifications",
                table: "FinishNotifications");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "FinishNotifications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FinishNotifications",
                table: "FinishNotifications",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_FinishNotifications_MatchId",
                table: "FinishNotifications",
                column: "MatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FinishNotifications",
                table: "FinishNotifications");

            migrationBuilder.DropIndex(
                name: "IX_FinishNotifications_MatchId",
                table: "FinishNotifications");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "FinishNotifications");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FinishNotifications",
                table: "FinishNotifications",
                column: "MatchId");
        }
    }
}

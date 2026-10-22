using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleHub.Memory.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurarGameResultPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[GameResultPlayers]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [GameResultPlayers] (
                        [Id] uniqueidentifier NOT NULL,
                        [GameResultId] uniqueidentifier NOT NULL,
                        [UserId] nvarchar(max) NOT NULL,
                        [DisplayName] nvarchar(max) NOT NULL,
                        [Score] int NOT NULL,
                        CONSTRAINT [PK_GameResultPlayers] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_GameResultPlayers_GameResults_GameResultId]
                            FOREIGN KEY ([GameResultId]) REFERENCES [GameResults] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_GameResultPlayers_GameResultId]
                        ON [GameResultPlayers] ([GameResultId]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[GameResultPlayers]', N'U') IS NOT NULL
                    DROP TABLE [GameResultPlayers];
                """);
        }
    }
}

using BattleHub.Memory.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.Data.DbContext;

public class MemoryDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public MemoryDbContext(DbContextOptions<MemoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<MemoryGameResult> GameResults => Set<MemoryGameResult>();

    public DbSet<MemoryGameResultPlayer> GameResultPlayers => Set<MemoryGameResultPlayer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MemoryGameResultPlayer>()
            .HasOne(p => p.GameResult)
            .WithMany(r => r.Players)
            .HasForeignKey(p => p.GameResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

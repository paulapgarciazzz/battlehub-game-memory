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

    public DbSet<FinishNotification> FinishNotifications => Set<FinishNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FinishNotification>()
        .HasKey(p => p.Id);

        modelBuilder.Entity<FinishNotification>()
            .Property(p => p.MatchId)
            .HasMaxLength(64)
            .IsRequired();

        modelBuilder.Entity<FinishNotification>()
            .HasIndex(p => p.MatchId)
            .IsUnique();

        modelBuilder.Entity<MemoryGameResultPlayer>()
            .HasOne(p => p.GameResult)
            .WithMany(r => r.Players)
            .HasForeignKey(p => p.GameResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

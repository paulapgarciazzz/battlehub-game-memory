using BattleHub.Memory.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.Data.DbContext;

public class MemoryDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public MemoryDbContext(DbContextOptions<MemoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<MemoryMatch> Matches => Set<MemoryMatch>();

    public DbSet<MemoryPlayer> Players => Set<MemoryPlayer>();

    public DbSet<MemoryCard> Cards => Set<MemoryCard>();

    public DbSet<MemoryGameResult> GameResults => Set<MemoryGameResult>();

    public DbSet<MemoryGameResultPlayer> GameResultPlayers => Set<MemoryGameResultPlayer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MemoryMatch>()
            .HasAlternateKey(m => m.MatchId);

        modelBuilder.Entity<MemoryPlayer>()
            .HasOne(p => p.Match)
            .WithMany(m => m.Players)
            .HasForeignKey(p => p.MatchId)
            .HasPrincipalKey(m => m.MatchId);

        modelBuilder.Entity<MemoryCard>()
            .HasOne(c => c.Match)
            .WithMany(m => m.Cards)
            .HasForeignKey(c => c.MatchId)
            .HasPrincipalKey(m => m.MatchId);

        modelBuilder.Entity<MemoryGameResultPlayer>()
            .HasOne(p => p.GameResult)
            .WithMany(r => r.Players)
            .HasForeignKey(p => p.GameResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
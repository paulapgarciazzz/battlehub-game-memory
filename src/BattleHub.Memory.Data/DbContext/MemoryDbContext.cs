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


}
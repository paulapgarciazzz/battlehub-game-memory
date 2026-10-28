using BattleHub.Memory.Data.DbContext;
using BattleHub.Memory.Data.Entities;
using BattleHub.Memory.Domain;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Memory.Data.Seed;

/// <summary>
/// Jugador que aparece en las partidas de ejemplo.
/// </summary>
public record SamplePlayer(string UserId, string DisplayName);

/// <summary>
/// Carga partidas terminadas de ejemplo para mostrar el historial y las
/// estadísticas. Va aparte del DbContext: el DbContext solo describe las tablas.
/// </summary>
public class SampleDataSeeder
{
    /// <summary>Prefijo del MatchId de las partidas de ejemplo.</summary>
    public const string MatchPrefix = "ejemplo-";

    /// <summary>Partidas que se generan por cada pareja de jugadores.</summary>
    public const int GamesPerPair = 3;

    public static readonly IReadOnlyList<SamplePlayer> DefaultPlayers =
    [
        new("demo|ana", "Ana"),
        new("demo|bruno", "Bruno"),
        new("demo|carla", "Carla"),
        new("demo|diego", "Diego")
    ];

    private const int TotalPairs = Board.CardCount / 2;

    private readonly MemoryDbContext _db;
    private readonly TimeProvider _time;

    public SampleDataSeeder(MemoryDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <summary>
    /// Inserta las partidas de ejemplo y devuelve cuántas agregó.
    /// Si ya estaban cargadas no hace nada y devuelve 0.
    /// </summary>
    public async Task<int> SeedAsync(IReadOnlyList<SamplePlayer> players, CancellationToken ct = default)
    {
        if (players.Count < 2)
            throw new ArgumentException("Se necesitan al menos dos jugadores.", nameof(players));

        // Semilla fija: cada vez que se carga, los datos son los mismos.
        var random = new Random(2026);
        var now = _time.GetUtcNow();
        var results = new List<MemoryGameResult>();

        for (var i = 0; i < players.Count; i++)
        {
            for (var j = i + 1; j < players.Count; j++)
            {
                for (var game = 0; game < GamesPerPair; game++)
                {
                    // Una de cada tres partidas de la pareja termina en empate.
                    var firstScore = game == 2 ? TotalPairs / 2 : random.Next(0, TotalPairs + 1);
                    results.Add(CreateResult(
                        $"{MatchPrefix}{results.Count + 1:D3}",
                        players[i], firstScore,
                        players[j], TotalPairs - firstScore,
                        now.AddDays(-random.Next(1, 31)).AddMinutes(-random.Next(0, 24 * 60)),
                        random));
                }
            }
        }

        // Si alguna de estas mismas partidas ya está guardada, la carga ya se
        // hizo antes. Se buscan los MatchId exactos para no confundirlas con
        // otras partidas que tengan un nombre parecido.
        var matchIds = results.Select(r => r.MatchId).ToList();
        if (await _db.GameResults.AnyAsync(r => matchIds.Contains(r.MatchId), ct))
            return 0;

        _db.GameResults.AddRange(results);
        await _db.SaveChangesAsync(ct);

        return results.Count;
    }

    private static MemoryGameResult CreateResult(
        string matchId,
        SamplePlayer first, int firstScore,
        SamplePlayer second, int secondScore,
        DateTimeOffset finishedAt,
        Random random)
    {
        var isDraw = firstScore == secondScore;

        return new MemoryGameResult
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            GameType = "memory",
            StartedAt = finishedAt.AddSeconds(-random.Next(90, 301)),
            FinishedAt = finishedAt,
            WinnerUserId = isDraw ? null : (firstScore > secondScore ? first : second).UserId,
            IsDraw = isDraw,
            Players =
            [
                new MemoryGameResultPlayer
                {
                    Id = Guid.NewGuid(),
                    UserId = first.UserId,
                    DisplayName = first.DisplayName,
                    Score = firstScore
                },
                new MemoryGameResultPlayer
                {
                    Id = Guid.NewGuid(),
                    UserId = second.UserId,
                    DisplayName = second.DisplayName,
                    Score = secondScore
                }
            ]
        };
    }
}

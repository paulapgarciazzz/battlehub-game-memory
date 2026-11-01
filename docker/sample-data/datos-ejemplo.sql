-- Partidas de ejemplo para que el historial y las estadísticas tengan datos
-- después de "docker compose up" (R-28). Lo ejecuta el servicio sample-data
-- de docker-compose.yml con sqlcmd, cuando la API ya aplicó las migraciones.
-- No depende de la API ni de SampleDataSeeder, pero usa los mismos MatchId
-- (ejemplo-001 a ejemplo-018), así que no duplica lo que cargó el seeder.
--
-- 4 jugadores forman 6 parejas, con 3 partidas cada una. Los puntajes suman 8
-- (los pares del tablero) y la tercera partida de cada pareja es empate.
--
-- Sin GO ni variables de sqlcmd: las pruebas de integración también lo ejecutan.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @jugadores TABLE (
    Clave nvarchar(10) NOT NULL PRIMARY KEY,
    UserId nvarchar(50) NOT NULL,
    DisplayName nvarchar(50) NOT NULL);

INSERT INTO @jugadores (Clave, UserId, DisplayName) VALUES
    (N'ana',   N'demo|ana',   N'Ana'),
    (N'bruno', N'demo|bruno', N'Bruno'),
    (N'carla', N'demo|carla', N'Carla'),
    (N'diego', N'demo|diego', N'Diego');

-- FinishedAt = ahora - DiasAtras - MinutosAtras; StartedAt = FinishedAt - Duracion.
DECLARE @partidas TABLE (
    Id uniqueidentifier NOT NULL DEFAULT NEWID(),
    MatchId nvarchar(20) NOT NULL PRIMARY KEY,
    Jugador1 nvarchar(10) NOT NULL,
    Puntos1 int NOT NULL,
    Jugador2 nvarchar(10) NOT NULL,
    Puntos2 int NOT NULL,
    DiasAtras int NOT NULL,
    MinutosAtras int NOT NULL,
    DuracionSegundos int NOT NULL);

INSERT INTO @partidas (MatchId, Jugador1, Puntos1, Jugador2, Puntos2, DiasAtras, MinutosAtras, DuracionSegundos) VALUES
    (N'ejemplo-001', N'ana',   6, N'bruno', 2,  3,  120, 180),
    (N'ejemplo-002', N'ana',   3, N'bruno', 5, 11,   45, 240),
    (N'ejemplo-003', N'ana',   4, N'bruno', 4, 19,  600, 150),
    (N'ejemplo-004', N'ana',   5, N'carla', 3,  2,  300, 210),
    (N'ejemplo-005', N'ana',   2, N'carla', 6, 14,  900, 120),
    (N'ejemplo-006', N'ana',   4, N'carla', 4, 25,   60, 270),
    (N'ejemplo-007', N'ana',   7, N'diego', 1,  6,   30,  95),
    (N'ejemplo-008', N'ana',   3, N'diego', 5, 17,  480, 200),
    (N'ejemplo-009', N'ana',   4, N'diego', 4, 28,  720, 160),
    (N'ejemplo-010', N'bruno', 5, N'carla', 3,  1,  200, 230),
    (N'ejemplo-011', N'bruno', 1, N'carla', 7,  9, 1000, 110),
    (N'ejemplo-012', N'bruno', 4, N'carla', 4, 21,  330, 280),
    (N'ejemplo-013', N'bruno', 6, N'diego', 2,  4,   75, 190),
    (N'ejemplo-014', N'bruno', 2, N'diego', 6, 12,  540, 140),
    (N'ejemplo-015', N'bruno', 4, N'diego', 4, 23, 1200, 250),
    (N'ejemplo-016', N'carla', 5, N'diego', 3,  7,   15, 170),
    (N'ejemplo-017', N'carla', 8, N'diego', 0, 16,  660, 300),
    (N'ejemplo-018', N'carla', 4, N'diego', 4, 29,  400, 130);

-- Si alguna de estas partidas ya está guardada, la carga ya se hizo antes.
IF EXISTS (SELECT 1 FROM dbo.GameResults r JOIN @partidas p ON r.MatchId = p.MatchId)
BEGIN
    PRINT N'Las partidas de ejemplo ya estaban cargadas.';
    RETURN;
END;

DECLARE @ahora datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);

BEGIN TRANSACTION;

INSERT INTO dbo.GameResults (Id, MatchId, GameType, StartedAt, FinishedAt, WinnerUserId, IsDraw)
SELECT
    p.Id,
    p.MatchId,
    N'memory',
    DATEADD(SECOND, -p.DuracionSegundos, f.FinishedAt),
    f.FinishedAt,
    CASE
        WHEN p.Puntos1 > p.Puntos2 THEN j1.UserId
        WHEN p.Puntos2 > p.Puntos1 THEN j2.UserId
    END,
    CASE WHEN p.Puntos1 = p.Puntos2 THEN 1 ELSE 0 END
FROM @partidas p
JOIN @jugadores j1 ON j1.Clave = p.Jugador1
JOIN @jugadores j2 ON j2.Clave = p.Jugador2
CROSS APPLY (SELECT DATEADD(MINUTE, -p.MinutosAtras, DATEADD(DAY, -p.DiasAtras, @ahora)) AS FinishedAt) f;

INSERT INTO dbo.GameResultPlayers (Id, GameResultId, UserId, DisplayName, Score)
SELECT NEWID(), p.Id, j.UserId, j.DisplayName, p.Puntos1
FROM @partidas p
JOIN @jugadores j ON j.Clave = p.Jugador1
UNION ALL
SELECT NEWID(), p.Id, j.UserId, j.DisplayName, p.Puntos2
FROM @partidas p
JOIN @jugadores j ON j.Clave = p.Jugador2;

COMMIT TRANSACTION;

PRINT N'Partidas de ejemplo cargadas: 18.';

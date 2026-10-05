# battlehub-game-memory

Microservicio del **Juego de Memoria** dentro de la plataforma BattleHub (Equipo 6).
Las reglas del juego están en [`docs/reglas-del-juego.md`](docs/reglas-del-juego.md).

## Contenido de este repositorio

| Ruta | Qué es |
|---|---|
| `src/BattleHub.Memory.Api` | API REST y hub SignalR propio (`/hubs/memory`), .NET 10 |
| `src/BattleHub.Memory.Domain` | Reglas del juego (`GameSession`, `Board`, `Player`) |
| `src/BattleHub.Memory.Data` | Persistencia con EF Core y SQL Server ([ADR-001](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/adrs/ADR-001-motor-bd-juego-memoria.md)) |
| `src/memory-microfrontend/memory-game` | Microfrontend en Aurelia 2, cargado por el Shell con Module Federation ([README](src/memory-microfrontend/memory-game/README.md)) |
| `tests/BattleHub.Memory.UnitTests` | Pruebas unitarias del dominio |
| `tests/BattleHub.Memory.IntegrationTests` | Pruebas de integración: API, JWT, SignalR, SQL y Matchmaking |
| `scripts` | Guardar el secreto de Auth0 y arrancar con migraciones |
| `docs` | Reglas del juego y guía de integración con el Shell, Auth0 y Matchmaking |

## Requisitos

- .NET 10 SDK
- Node.js 24 LTS (`>=24.11.0 <25`)
- SQL Server LocalDB (viene con Visual Studio) u otro SQL Server

## Cómo correrlo localmente

### 1. Guardar el secreto de Auth0 (una vez por máquina)

El backend usa un cliente máquina a máquina de Auth0 para avisar a Matchmaking
cuando termina una partida. El **Client ID** ya está en `appsettings.json`; el
**Client Secret** se guarda con `dotnet user-secrets`, fuera del repo:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Set-MatchmakingCredentials.ps1
```

El script pide el secreto sin mostrarlo. Nunca lo pongan en `appsettings.json`
ni lo compartan por el grupo del curso. Sin el secreto el juego funciona igual;
solo queda pendiente el aviso a Matchmaking.

### 2. Backend (API y hub)

Con migraciones automáticas (recomendado la primera vez):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Integration.ps1
```

O desde Visual Studio / `dotnet run --project src/BattleHub.Memory.Api`.
Queda en `http://localhost:5095`.

### 3. Microfrontend

```bash
cd src/memory-microfrontend/memory-game
npm ci
npm start
```

Queda en `http://localhost:4003` (remote `memoryGame`, ADR-003).

### 4. Jugar

Las partidas se juegan **desde el Shell** (`http://localhost:4000`), entrando a
una sala real de Matchmaking con dos cuentas de Auth0. El Shell le entrega al
juego el `matchId` y los tokens del jugador; la página de `localhost:4003` ya no
permite jugar escribiendo un usuario.

Para probar con el Shell local mientras su soporte para Memory no esté en `main`,
usar la rama `feat/interfaz-battlehub` de `battlehub-shell` y configurar en su
`.env` la variable `AUTH0_MEMORY_AUDIENCE=https://api.battlehub.local/memory`.

## Pruebas

```bash
cd src
dotnet test BattleHub.Memory.slnx
```

Las pruebas de integración necesitan una base SQL Server:

- **En el CI (GitHub Actions)** levantan SQL Server en un contenedor (Testcontainers), sin configuración.
- **En su máquina** (sin Docker) usan LocalDB si definen esta variable una vez y reinician Visual Studio y la terminal:

  ```powershell
  setx MEMORY_TEST_CONNECTION "Server=(localdb)\mssqllocaldb;Integrated Security=true;TrustServerCertificate=true"
  ```

  Cada corrida crea una base `BattleHubMemoryTests_...` y la borra al terminar; no toca `BattleHubMemoryDb`.

Frontend: `npm test` en `src/memory-microfrontend/memory-game` (lint y Jest).

## Configuración

`src/BattleHub.Memory.Api/appsettings.json` (valores públicos, sin secretos):

| Clave | Para qué |
|---|---|
| `ConnectionStrings:MemoryDatabase` | Base de datos (por defecto LocalDB `BattleHubMemoryDb`) |
| `Auth:Domain`, `Auth:Audience` | Tenant de Auth0 y audiencia de la API de Memory |
| `Cors:Origins` | Orígenes permitidos: Shell (4000) y microfrontend (4003) |
| `Matchmaking:BaseUrl` | URL de Matchmaking (`http://localhost:5211`) |
| `Matchmaking:Auth0:*` | Cliente M2M para avisar el fin de partida; `ClientSecret` va vacío |
| `Database:MigrateOnStartup` | Aplicar migraciones al arrancar (lo activa `Start-Integration.ps1`) |

En producción, cualquier valor se reemplaza con variables de entorno, por ejemplo
`ConnectionStrings__MemoryDatabase` o `Matchmaking__Auth0__ClientSecret`.

## Contratos implementados

De [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts):

- Contrato de entrada al juego (`matchId`, `gameType`, `currentUser`) y ciclo de vida `GameModule` (contrato 03).
- API de resultados, historial y estadísticas (contrato 04).
- Integración con el Shell por Module Federation (ADR-003).
- Aviso de fin de partida a Matchmaking con `POST /api/matches/{id}/finish` (ADR-004).

Detalles de la integración con Auth0, Matchmaking y el Shell, y cómo revisar los
avisos pendientes: [`docs/integracion-shell-auth0.md`](docs/integracion-shell-auth0.md).

## Límites conocidos

- Las partidas en curso viven en memoria: la API debe correr en **una sola instancia**,
  y si se reinicia se pierden las partidas en curso (los resultados guardados no).

## Equipo

Equipo 6 — Juego de Memoria.

# battlehub-game-memory

Microservicio del **Juego de Memoria** dentro de la plataforma BattleHub (Equipo 6).

## Contenido de este repositorio

- `src/BattleHub.Memory.Api` — API REST + Hub SignalR propio (`/hubs/memory`), .NET 10
- `src/BattleHub.Memory.Domain` — lógica de dominio del juego
- `src/BattleHub.Memory.Data` — acceso a datos (motor de BD por definir, documentado con ADR en `battlehub-contracts`)
- `src/memory-microfrontend` — microfrontend en Aurelia, expuesto al Shell vía Module Federation
- `tests` — pruebas unitarias e integración
- `.github/workflows` — pipeline de CI/CD

## Requisitos previos

- .NET 10 SDK
- Node.js (versión: _completar_) y npm/pnpm
- (Motor de base de datos elegido: _completar cuando se decida_)

## Cómo correr el proyecto localmente

### Backend (API + Hub SignalR)

```bash
cd src/BattleHub.Memory.Api
dotnet restore
dotnet run
```

### Microfrontend (Aurelia)

```bash
cd src/memory-microfrontend
npm install
npm start
```

## Variables de entorno

_Completar: cadena de conexión a BD, configuración de Auth0 de la app registrada por este equipo, URL del Matchmaking, etc. No versionar secretos — usar `.env` (ver `.gitignore`)._

## Contratos implementados

Este repo implementa contratos definidos en `battlehub-contracts`:
- Contrato de entrada a un juego (`matchId`, `gameType`, `currentUser`)
- Ciclo de vida del microfrontend (`GameModule`: initialize / start / pause / dispose)
- Hub propio: `/hubs/memory`

## Equipo

Equipo 6 — Juego de Memoria.

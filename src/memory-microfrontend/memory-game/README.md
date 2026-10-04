# `memory-game`

Microfrontend del juego de Memoria (Equipo 6) en **Aurelia 2**, integrado al Shell de BattleHub
con **Webpack Module Federation**.

## Versiones (ADR-003 del Shell)

| Herramienta | Versión |
|---|---|
| Node.js | 24 LTS (`>=24.11.0 <25`, ver `.nvmrc`) |
| `aurelia` y `@aurelia/*` | `2.0.0-rc.2` exacta |
| Webpack | 5 |

`mf-shared.js` es una copia de `battlehub-shell/tooling/mf-shared.js`. Aurelia se comparte como
`singleton` con `strictVersion`: si la versión no coincide con la del Shell, el juego no carga.

## Dos formas de ejecutarlo

**Dentro del Shell (BattleHub).** El Shell carga `./GameModule` desde `remoteEntry.js` y le entrega
el contexto (`matchId`, `gameType`, `currentUser`), según el contrato 03 (secciones 5 y 6).
`GameModule` implementa `initialize`, `start`, `pause` y `dispose`. No hay pantalla de unión:
el `matchId` y el usuario vienen del Shell.

**Independiente (solo desarrollo local).** En `http://localhost:8080` aparece una pantalla para
escribir nombre, usuario y código de partida. El primer jugador genera un código y se lo pasa
al segundo, que lo escribe en el mismo campo (sin presionar "Generar").

## Cómo correrlo localmente

Necesita el backend corriendo en `http://localhost:5095` (`dotnet run --project src/BattleHub.Memory.Api`).

```bash
npm ci
npm start          # http://localhost:8080 (y http://localhost:8080/remoteEntry.js para el Shell)
```

### Probarlo dentro del Shell

1. En `battlehub-shell/config/remotes.local.json` agregar (o pedirle al Equipo 3 que agregue):
   ```json
   "memory": { "scope": "memoryGame", "url": "http://localhost:8080/remoteEntry.js", "module": "./GameModule" }
   ```
2. Levantar el Shell (`npm start`, puerto 4000). El backend acepta CORS desde `localhost:4000`.

## Scripts

| Script | Qué hace |
|---|---|
| `npm start` | Servidor de desarrollo en el puerto 8080 |
| `npm run build` | Build de producción en `dist/` (usa `config/environment.production.json`) |
| `npm run lint` | ESLint sobre `src` y `test` |
| `npm test` | Pruebas unitarias con Jest (`test/**/*.spec.ts`) |
| `npm run analyze` | Build de producción con Webpack Bundle Analyzer |

## Configuración

`config/environment.json` define `apiBaseUrl` y `hubBaseUrl` (por defecto `http://localhost:5095`).
En producción se reemplaza por `config/environment.production.json`.

# `memory-game`

Microfrontend del juego de Memoria (Equipo 6) en **Aurelia 2**, integrado al Shell de BattleHub
con **Webpack Module Federation**, según
[ADR-003](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/adrs/ADR-003-integracion-module-federation-aurelia-shell.md).

## Datos de integración (ADR-003)

| Dato | Valor |
|---|---|
| Node.js | 24 LTS (`>=24.11.0 <25`, ver `.nvmrc`) |
| `aurelia` y `@aurelia/*` | `2.0.0-rc.2` exacta |
| Webpack | 5 |
| Nombre del remote | `memoryGame` |
| Módulo expuesto | `./GameModule` (elemento `memory-game-module`) |
| Puerto local | `4003` → `http://localhost:4003/remoteEntry.js` |

`mf-shared.js` y `src/game-contracts.ts` son copias idénticas de la plantilla del ADR-003:
no se modifican. Aurelia se comparte como `singleton` con `strictVersion`, así que si la versión
no coincide con la del Shell, el juego no carga.

Todas las clases CSS llevan el prefijo `memory-game` (ADR-003 §8), con BEM:
`memory-game-bloque__elemento--modificador`. Stylelint lo verifica.

## Dos formas de ejecutarlo

**Dentro del Shell (BattleHub).** El Shell carga `./GameModule` desde `remoteEntry.js` y le entrega
el contexto (`matchId`, `gameType`, `currentUser`), según el contrato 03 (secciones 5 y 6).
`GameModule` implementa `initialize`, `start`, `pause` y `dispose`. No hay pantalla de unión:
el `matchId` y el usuario vienen del Shell.

Como la partida es en tiempo real, `pause()` no detiene el servidor (el turno y su temporizador
siguen corriendo): solo bloquea las jugadas de este jugador y muestra un aviso. `start()` quita
la pausa sin volver a unirse.

**Independiente (solo desarrollo local).** En `http://localhost:4003` aparece una pantalla para
escribir nombre, usuario y código de partida. El primer jugador genera un código y se lo pasa
al segundo, que lo escribe en el mismo campo (sin presionar "Generar").

## Cómo correrlo localmente

Necesita el backend corriendo en `http://localhost:5095` (`dotnet run --project src/BattleHub.Memory.Api`).

```bash
npm ci
npm start          # http://localhost:4003 (y http://localhost:4003/remoteEntry.js para el Shell)
```

### Probarlo dentro del Shell

1. En `battlehub-shell/config/remotes.local.json` debe estar (lo agrega el Equipo 3):
   ```json
   "memory": { "scope": "memoryGame", "url": "http://localhost:4003/remoteEntry.js", "module": "./GameModule" }
   ```
2. Levantar el Shell (`npm start`, puerto 4000). El backend acepta CORS desde `localhost:4000`
   y `localhost:4003`.

## Scripts

| Script | Qué hace |
|---|---|
| `npm start` | Servidor de desarrollo en el puerto 4003 |
| `npm run build` | Build de producción en `dist/` (usa `config/environment.production.json`) |
| `npm run lint` | ESLint (`src` y `test`) y Stylelint (`src/**/*.css`) |
| `npm test` | Lint y luego las pruebas unitarias con Jest (`test/**/*.spec.ts`) |
| `npm run analyze` | Build de producción con Webpack Bundle Analyzer |

## Configuración

`config/environment.json` define `apiBaseUrl` y `hubBaseUrl` (por defecto `http://localhost:5095`).
En producción se reemplaza por `config/environment.production.json`.

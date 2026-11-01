# Juego de Memoria — BattleHub

Microfrontend del juego de Memoria del Equipo 6, desarrollado con **Aurelia 2** y **Webpack Module Federation**, integrado al Shell de BattleHub.

## Requisitos previos

- Docker Desktop con Docker Compose.
- Node.js 22 LTS (`>=22 <23`) para ejecutar el microfrontend y sus herramientas de desarrollo.
- Git para obtener el código del proyecto.

## Arquitectura y puertos

| Componente         | Puerto | Descripción                                                   |
| ------------------ | -----: | ------------------------------------------------------------- |
| API                | `5095` | API del juego de Memoria y comunicación con el cliente.       |
| SQL Server         | `1433` | Base de datos utilizada por la API.                           |
| Microfrontend      | `4003` | Juego y archivo remoto de Module Federation.                  |
| Shell de BattleHub | `4000` | Aplicación externa que integra y muestra el juego.            |

El microfrontend expone el módulo `./GameModule` con el nombre remoto `memoryGame`. El archivo remoto se encuentra en:

`http://localhost:4003/remoteEntry.js`

**Importante:** Docker Compose inicia SQL Server, la API y el microfrontend. El Shell se ejecuta por separado y debe iniciarse cuando se necesite probar la integración completa.

## Cómo iniciarlo con Docker Compose

Desde la raíz del repositorio, ejecuta:

```bash
docker compose up
```

No hace falta crear un `.env`: la contraseña de SQL Server se genera sola y los demás valores tienen uno por defecto. Si quieres cambiarlos, consulta `.env.example` y el [README de la raíz](../../../README.md).

En el contenedor, el microfrontend es el build de producción (`npm run build`) servido con nginx. Usa la API en `http://localhost:5095`; para cambiarla, define `MEMORY_API_URL` en `.env` y reconstruye con `docker compose up --build`.

Para detener los servicios, presiona `Ctrl+C` o ejecuta:

```bash
docker compose down
```

## Ejecutar el microfrontend en modo desarrollo

Para trabajar con recarga automática, detén el microfrontend del compose (`docker compose stop frontend`) y abre una terminal en:

```text
src/memory-microfrontend/memory-game
```

Instala las dependencias y ejecuta el servidor de desarrollo:

```bash
npm ci
npm start
```

El microfrontend estará disponible en `http://localhost:4003`, y el archivo remoto en `http://localhost:4003/remoteEntry.js`.

La configuración de conexión del cliente se encuentra en `config/environment.json`. Esta configuración incluye `apiBaseUrl` y `hubBaseUrl`, utilizados para comunicarse con la API y el hub de tiempo real.

## Integración con el Shell

Para probar el juego dentro de BattleHub:

1. Inicia SQL Server, la API y el microfrontend con `docker compose up`.
2. Configura el Shell para cargar el remoto desde `http://localhost:4003/remoteEntry.js`.
3. Inicia el Shell en el puerto `4000`.

La configuración del remoto debe apuntar al módulo `./GameModule` y utilizar el nombre `memoryGame`.

La API debe permitir mediante CORS los orígenes configurados en `MEMORY_CORS_SHELL` y `MEMORY_CORS_FRONTEND`.

El Shell entrega al juego el contexto de la partida, incluido el identificador de partida, el tipo de juego y el usuario actual. No es necesario que el jugador vuelva a unirse desde una pantalla independiente cuando el juego se utiliza integrado en el Shell.

## Comandos de desarrollo

Estos comandos se ejecutan desde `src/memory-microfrontend/memory-game`; no son necesarios para iniciar la API y SQL Server con Docker Compose.

| Comando           | Descripción                                           |
| ----------------- | ----------------------------------------------------- |
| `npm ci`          | Instala las dependencias del proyecto.                |
| `npm start`       | Inicia el servidor de desarrollo en el puerto `4003`. |
| `npm run build`   | Genera el build de producción en `dist/`.             |
| `npm run lint`    | Ejecuta las comprobaciones de ESLint y Stylelint.     |
| `npm test`        | Ejecuta el lint y las pruebas unitarias configuradas. |
| `npm run analyze` | Genera el análisis del bundle de Webpack.             |

## Funcionamiento de la partida

El juego admite partidas en tiempo real. Cuando el Shell pausa el módulo, se bloquean las jugadas del jugador y se muestra el aviso correspondiente; el servidor mantiene el turno y su temporizador. Al reanudar, el juego continúa sin volver a unirse a la partida.

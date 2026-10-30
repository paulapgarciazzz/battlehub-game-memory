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
| Microfrontend      | `4003` | Servidor de desarrollo y archivo remoto de Module Federation. |
| Shell de BattleHub | `4000` | Aplicación externa que integra y muestra el juego.            |

El microfrontend expone el módulo `./GameModule` con el nombre remoto `memoryGame`. El archivo remoto se encuentra en:

`http://localhost:4003/remoteEntry.js`

**Importante:** Docker Compose inicia la API y SQL Server. El microfrontend y el Shell se ejecutan por separado, por lo que deben iniciarse cuando se necesite probar la integración completa.

## Configuración de variables de entorno

En la raíz del repositorio, crea un archivo `.env` a partir de `.env.example` y configura las siguientes variables:

| Variable               | Descripción                                                                                          | Valor de desarrollo     |
| ---------------------- | ---------------------------------------------------------------------------------------------------- | ----------------------- |
| `MSSQL_SA_PASSWORD`    | Contraseña del usuario `sa` de SQL Server. Debe cumplir los requisitos de complejidad de SQL Server. | `CambiarEsto_123`       |
| `MEMORY_CORS_SHELL`    | Origen del Shell autorizado para realizar solicitudes a la API.                                      | `http://localhost:4000` |
| `MEMORY_CORS_FRONTEND` | Origen del microfrontend autorizado para realizar solicitudes a la API.                              | `http://localhost:4003` |

La contraseña mostrada es únicamente un ejemplo para desarrollo. Utiliza una contraseña propia que cumpla los requisitos de SQL Server y no compartas credenciales reales ni subas el archivo `.env` al repositorio.

## Cómo iniciar la API y la base de datos

Desde la raíz del repositorio, ejecuta:

```bash
docker compose up
```

Docker Compose iniciará los servicios definidos en `docker-compose.yml`, correspondientes a SQL Server y la API.

Para detener los servicios, presiona `Ctrl+C`. Si necesitas detener también los contenedores, ejecuta:

```bash
docker compose down
```

La API estará disponible en `http://localhost:5095` y SQL Server escuchará en el puerto `1433`.

## Ejecutar el microfrontend

Una vez iniciados los servicios de la API y la base de datos, abre una terminal en:

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

1. Inicia la API y SQL Server con `docker compose up`.
2. Inicia el microfrontend en el puerto `4003`.
3. Configura el Shell para cargar el remoto desde `http://localhost:4003/remoteEntry.js`.
4. Inicia el Shell en el puerto `4000`.

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

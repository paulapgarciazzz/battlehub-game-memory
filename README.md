# battlehub-game-memory

Microservicio del **Juego de Memoria** dentro de la plataforma BattleHub (Equipo 6).

Las reglas del juego están en [ `docs/reglas-del-juego.md`](docs/reglas-del-juego.md).

## Contenido de este repositorio

| Ruta                                   | Qué es                                                                                                                                                         |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/BattleHub.Memory.Api`             | API REST y hub SignalR propio (`/hubs/memory`), .NET 10                                                                                                        |
| `src/BattleHub.Memory.Domain`          | Reglas del juego (`GameSession`, `Board`, `Player`)                                                                                                            |
| `src/BattleHub.Memory.Data`            | Persistencia con EF Core y SQL Server ([ADR-001](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/adrs/ADR-001-motor-bd-juego-memoria.md)) |
| `src/memory-microfrontend/memory-game` | Microfrontend en Aurelia 2, cargado por el Shell con Module Federation ([README](src/memory-microfrontend/memory-game/README.md))                              |
| `tests/BattleHub.Memory.UnitTests`     | Pruebas unitarias (Moq, sin base de datos) con las carpetas de `src`: `Domain`, `Api`, `Data`                                                                  |
| `tests/BattleHub.Memory.IntegrationTests` | Pruebas de integración con SQL Server real y la API levantada: `Api` y `Data`                                                                             |
| `docs`                                 | Reglas del juego, API y contrato con el Shell                                                                                                                  |
| `Dockerfile` de la API                 | Compilación y publicación de la API en una imagen multi-stage, ubicada en `src/BattleHub.Memory.Api/Dockerfile`                                                |
| `Dockerfile` del microfrontend         | Build de producción del microfrontend servido con nginx, ubicado en `src/memory-microfrontend/memory-game/Dockerfile`                                          |
| `docker-compose.yml`                   | Configuración para ejecutar SQL Server, la API y el microfrontend mediante Docker Compose                                                                       |

## Requisitos

Para ejecutar el proyecto con Docker solo se necesita Docker Desktop con Docker Compose.

Para ejecutar el proyecto directamente en la máquina:

- .NET 10 SDK.
- Node.js 22 LTS (`>=22 <23`, ver `.nvmrc`).
- SQL Server LocalDB u otra instancia compatible de SQL Server.

## Cómo ejecutar el proyecto con Docker Compose

Docker Compose inicia SQL Server, la API y el microfrontend sin instalar .NET SDK, Node.js ni SQL Server en la máquina. No hace falta ningún paso previo: en un clon limpio basta con un comando.

### 1. Iniciar los servicios

Ejecuta desde la raíz del repositorio:

```powershell
docker compose up
```

Para dejarlo en segundo plano, agrega `-d`. Si cambiaste el código y quieres reconstruir las imágenes, agrega `--build`.

Docker Compose realiza las siguientes operaciones:

1. Prepara la contraseña de `sa` de SQL Server (ver [Contraseña de SQL Server](#contraseña-de-sql-server)).
2. Construye la API y el microfrontend con sus Dockerfile multi-stage.
3. Inicia SQL Server y espera a que esté saludable mediante su comprobación de estado.
4. Inicia la API cuando la base de datos está disponible. La API aplica las migraciones de Entity Framework Core al arrancar (`Database__MigrateOnStartup=true`) y carga los datos de ejemplo (`SampleData__Enabled=true`).
5. Sirve el build de producción del microfrontend con nginx.

Las imágenes finales contienen solo lo necesario para ejecutar cada servicio, no el SDK de .NET, Node.js ni el código fuente completo.

### 2. Contraseña de SQL Server

La contraseña no está escrita en ningún archivo del repositorio:

- Si no existe `.env`, el servicio `sql-password` genera una contraseña al azar la primera vez y la guarda en el volumen `memory-secrets`. SQL Server y la API la leen de ahí, y se reutiliza en cada `docker compose up` hasta que se borren los volúmenes.
- Si quieres usar una propia, copia `.env.example` a `.env` y define `MSSQL_SA_PASSWORD`. Debe cumplir los requisitos de complejidad de SQL Server y no llevar `;`.

Para consultar la contraseña generada (por ejemplo, para conectarte desde SQL Server Management Studio):

```powershell
docker compose exec sql cat /run/memory-secrets/sa_password
```

**Importante:** SQL Server solo toma la contraseña al crear su volumen. Si cambias `MSSQL_SA_PASSWORD` con la base ya creada, ejecuta `docker compose down -v` para empezar de cero. El archivo `.env` no se sube al repositorio.

### 3. Comprobar el estado de los servicios

Para consultar el estado de los contenedores:

```powershell
docker compose ps
```

Para consultar los registros de la API:

```powershell
docker compose logs api
```

Para consultar los registros de SQL Server:

```powershell
docker compose logs sql
```

Para revisar los últimos registros de la API:

```powershell
docker compose logs api --tail 100
```

Si hay un error de conexión, configuración o migración, revisa los registros antes de continuar.

### 4. Direcciones de acceso

| Servicio                        | Dirección                              |
| ------------------------------- | -------------------------------------- |
| Microfrontend                   | `http://localhost:4003`                |
| Remote de Module Federation     | `http://localhost:4003/remoteEntry.js` |
| API de Memory                   | `http://localhost:5095`                |
| Hub SignalR                     | `http://localhost:5095/hubs/memory`    |
| SQL Server                      | `localhost,1433`                       |

La API utiliza el puerto `8080` dentro del contenedor y se publica en el puerto `5095` de la máquina. El microfrontend usa el puerto `80` de nginx y se publica en el `4003`.

### 5. Detener los servicios

Para detener los contenedores sin eliminar los datos persistentes:

```powershell
docker compose down
```

Para detenerlos y eliminar también los volúmenes (base de datos y contraseña generada):

```powershell
docker compose down -v
```

Después se puede volver a levantar con `docker compose up`: se genera una contraseña nueva, se crea la base, se aplican las migraciones y se cargan los datos de ejemplo.

**Advertencia:** `docker compose down -v` elimina los volúmenes de este proyecto y, con ellos, las partidas guardadas. Úsalo solamente cuando quieras borrar esa información.

## Ejecución local sin Docker

También es posible ejecutar la API directamente con .NET durante el desarrollo. En este caso se necesita una instancia de SQL Server accesible desde la máquina.

Desde la raíz del repositorio:

```powershell
dotnet run --project src/BattleHub.Memory.Api
```

La dirección y el perfil de ejecución pueden depender de la configuración local de `launchSettings.json`.

La configuración de conexión a SQL Server debe coincidir con la instancia disponible. Para aplicar las migraciones al arrancar, configura `Database:MigrateOnStartup` según el mecanismo admitido por la aplicación.

## Cómo ejecutar el microfrontend sin Docker

`docker compose up` ya sirve el microfrontend en el puerto `4003`. Para desarrollarlo con recarga automática, detén ese servicio (`docker compose stop frontend`) y en otra terminal ejecuta:

```powershell
cd src/memory-microfrontend/memory-game
npm ci
npm start
```

El microfrontend queda disponible en `http://localhost:4003`, con el remote `memoryGame`, según la configuración de Module Federation (ADR-003).

Se requiere Node.js 22 LTS (`>=22 <23`).

## Cómo jugar

Las partidas competitivas se juegan **desde el Shell** (`http://localhost:4000`). El Shell inicia sesión con Auth0, obtiene la sala de Matchmaking y le entrega al juego el `matchId` y el `currentUser`.

El juego **no tiene contacto con Auth0 ni con Matchmaking**:

- La API y el hub no piden token. El jugador es el `currentUser` que entrega el Shell.
- Cuando la partida termina, el microfrontend emite el evento DOM `battlehub:game-finished` con el ganador y los puntajes. El Shell lo escucha y avisa a Matchmaking. El juego nunca llama a Matchmaking.

El detalle del evento está en [`docs/api-y-contrato-shell.md`](docs/api-y-contrato-shell.md).

## Pruebas

### Ejecutar las pruebas de .NET

Desde la raíz del repositorio:

```powershell
dotnet test src/BattleHub.Memory.slnx
```

Las pruebas están en dos proyectos separados, con las mismas carpetas que `src`:

- `tests/BattleHub.Memory.UnitTests`: pruebas unitarias. Las dependencias se reemplazan con Moq y no necesitan base de datos.
- `tests/BattleHub.Memory.IntegrationTests`: pruebas de integración con SQL Server real y la API levantada en memoria.

Para ejecutar solamente las pruebas unitarias:

```powershell
dotnet test tests/BattleHub.Memory.UnitTests
```

Para ejecutar solamente las de integración:

```powershell
dotnet test tests/BattleHub.Memory.IntegrationTests
```

### Pruebas de integración

Las pruebas de integración necesitan una instancia de SQL Server.

- **En CI (GitHub Actions):** se utiliza SQL Server mediante Testcontainers, de acuerdo con la configuración de las pruebas.
- **En la máquina local con LocalDB:** configura la variable de conexión para las pruebas y reinicia la terminal si utilizas `setx`.

```powershell
setx MEMORY_TEST_CONNECTION "Server=(localdb)\mssqllocaldb;Integrated Security=true;TrustServerCertificate=true"
```

- **Con Docker:** si quieres utilizar el SQL Server del archivo `docker-compose.yml`, define la variable de conexión en la terminal actual. La contraseña se lee del contenedor, así que no hace falta escribirla:

```powershell
$sa = docker compose exec sql cat /run/memory-secrets/sa_password
$env:MEMORY_TEST_CONNECTION = "Server=localhost,1433;User Id=sa;Password=$sa;TrustServerCertificate=True"
```

Sin esa variable, las pruebas pueden crear su propio contenedor de SQL Server mediante Testcontainers, siempre que Docker esté disponible y la configuración de pruebas lo permita.

Cada ejecución de las pruebas de integración crea su propia base de datos de pruebas, con un nombre similar a `BattleHubMemoryTests_...`, y la elimina al terminar. No debería modificar `BattleHubMemoryDb`.

### Pruebas del frontend

Para ejecutar las pruebas del microfrontend:

```powershell
cd src/memory-microfrontend/memory-game
npm test
```

Este comando ejecuta las comprobaciones configuradas en el proyecto, incluidos lint y Jest si están definidos en los scripts de `package.json`.

## Configuración

Los valores predeterminados de la API se encuentran en `src/BattleHub.Memory.Api/appsettings.json`. Los secretos y valores específicos de cada entorno deben proporcionarse por un mecanismo externo, como variables de entorno o .NET User Secrets.

| Clave                              | Para qué sirve                                                                                                  |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:MemoryDatabase` | Cadena de conexión a SQL Server; en el desarrollo local puede utilizar LocalDB con la base `BattleHubMemoryDb`. |
| `Cors:Origins`                     | Orígenes permitidos (Shell y microfrontend). Obligatorio: la API no arranca si está vacío.                      |
| `Database:MigrateOnStartup`        | Indica si la API aplica las migraciones al arrancar. En Docker Compose está habilitado.                         |
| `SampleData:Enabled`               | Indica si se cargan partidas de ejemplo al arrancar.                                                            |
| `SampleData:Players`               | Configuración opcional de los jugadores para las partidas de ejemplo.                                           |

En la configuración de Docker Compose se utilizan variables de entorno de .NET con doble guion bajo para representar secciones anidadas. Algunos ejemplos son:

- `ConnectionStrings__MemoryDatabase`
- `Database__MigrateOnStartup`
- `SampleData__Enabled`
- `Cors__Origins__0` y `Cors__Origins__1`: en Docker Compose salen de `MEMORY_CORS_SHELL` y `MEMORY_CORS_FRONTEND` del `.env`, con `http://localhost:4000` y `http://localhost:4003` por defecto.

El microfrontend de Docker Compose usa la API en `http://localhost:5095`. Para cambiarla, define `MEMORY_API_URL` en `.env` y reconstruye con `docker compose up --build`.

Los valores de conexión y credenciales dependen del entorno de ejecución.

## Datos de ejemplo

Cuando `SampleData:Enabled=true`, la API puede cargar 18 partidas terminadas de los últimos 30 días para que el historial y las estadísticas tengan datos sin necesidad de jugar.

Los jugadores predeterminados son Ana, Bruno, Carla y Diego (`demo|ana`, `demo|bruno`, `demo|carla`, `demo|diego`). Cada uno tiene nueve partidas, con victorias, derrotas y tres empates.

- La carga se implementa en `src/BattleHub.Memory.Data/Seed/SampleDataSeeder.cs`, fuera del `DbContext`.
- La carga está diseñada para ejecutarse una sola vez: si ya existen las partidas `ejemplo-001` a `ejemplo-018`, no vuelve a insertarlas.

Para asociar partidas de ejemplo a una cuenta real, puedes configurar el identificador del usuario (el `currentUser.id` que entrega el Shell) mediante variables de entorno. Por ejemplo:

```powershell
$env:SampleData__Players__0__UserId = "auth0|su-id"
$env:SampleData__Players__0__DisplayName = "Su nombre"
$env:SampleData__Players__1__UserId = "demo|bruno"
$env:SampleData__Players__1__DisplayName = "Bruno"
```

Si ya se cargaron partidas de ejemplo con otros jugadores, puede ser necesario eliminarlas de la base de datos antes de volver a cargar los datos. Hazlo únicamente en una base de datos de desarrollo y verifica primero qué registros vas a eliminar.

## Contratos implementados

A partir de [ `battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts), el proyecto implementa:

- Contrato de entrada al juego (`matchId`, `gameType`, `currentUser`) y ciclo de vida de `GameModule` (contrato 03).
- API de resultados, historial y estadísticas (contrato 04).
- Integración con el Shell mediante Module Federation (ADR-003).
- Aviso de fin de partida al Shell con el evento DOM `battlehub:game-finished`. El Shell avisa a Matchmaking.

Para más información sobre la API y el contrato con el Shell, consulta [`docs/api-y-contrato-shell.md`](docs/api-y-contrato-shell.md).

## Límites conocidos

- Las partidas en curso se mantienen en memoria. La API debe ejecutarse en **una sola instancia** para evitar inconsistencias entre sesiones.
- Si la API se reinicia, las partidas que estaban en curso se pierden, aunque los resultados previamente guardados en la base de datos permanecen.
- Como el juego no usa Auth0, la API confía en el `userId` que entrega el Shell y no restringe quién consulta el historial, las estadísticas o los resultados.

## Equipo

Equipo 6 — Juego de Memoria.

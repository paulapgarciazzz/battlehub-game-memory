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
| `tests/BattleHub.Memory.Domain.Tests`  | Pruebas unitarias del dominio                                                                                                                                  |
| `tests/BattleHub.Memory.Data.Tests`    | Pruebas de integración de Data: esquema y datos de ejemplo en SQL Server                                                                                       |
| `tests/BattleHub.Memory.Api.Tests`     | Pruebas de la API: unitarias con Moq e integración de SignalR y SQL                                                                                            |
| `scripts`                              | Script auxiliar para el desarrollo local                                                                                                                       |
| `docs`                                 | Reglas del juego, API y contrato con el Shell                                                                                                                  |
| `Dockerfile` de la API                 | Compilación y publicación de la API en una imagen multi-stage, ubicada en `src/BattleHub.Memory.Api/Dockerfile`                                                |
| `docker-compose.yml`                   | Configuración para ejecutar la API y SQL Server mediante Docker Compose                                                                                        |

## Requisitos

Para ejecutar el proyecto con Docker:

- Docker Desktop con Docker Compose.
- Un archivo `.env` con las variables necesarias, creado a partir de `.env.example`.

Para ejecutar el proyecto directamente en la máquina:

- .NET 10 SDK.
- Node.js 24 LTS (`>=24.11.0 <25`).
- SQL Server LocalDB u otra instancia compatible de SQL Server.

## Cómo ejecutar el backend con Docker Compose

Docker Compose permite iniciar la API y SQL Server sin instalar .NET SDK ni SQL Server directamente en la máquina. La imagen final de la API utiliza el runtime de .NET, mientras que la compilación y publicación se realizan en una etapa independiente del Dockerfile.

### 1. Configurar las variables de entorno

Desde la raíz del repositorio, ejecuta en PowerShell:

```powershell
Copy-Item .env.example .env
```

Si ya tienes un archivo `.env`, no lo sobrescribas. Ábrelo y modifica las variables necesarias.

Edita `.env` y establece una contraseña segura para `MSSQL_SA_PASSWORD`. Debe cumplir los requisitos de SQL Server y no ser una contraseña real compartida públicamente.

**Importante:** el archivo `.env` contiene configuración local y secretos. No lo subas al repositorio ni compartas su contenido.

### 2. Construir e iniciar los servicios

Ejecuta desde la raíz del repositorio:

```powershell
docker compose up --build -d
```

Docker Compose realiza las siguientes operaciones:

1. Construye la API utilizando el Dockerfile multi-stage.
2. Inicia SQL Server.
3. Espera a que SQL Server esté saludable mediante su comprobación de estado.
4. Inicia la API cuando la base de datos está disponible.
5. La API aplica las migraciones de Entity Framework Core al arrancar, mediante `Database__MigrateOnStartup=true`, configurado en `docker-compose.yml`.
6. La API carga los datos de ejemplo si `SampleData__Enabled` está habilitado.

La imagen final contiene los archivos publicados necesarios para ejecutar la API, no el SDK de compilación ni el código fuente completo.

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

| Servicio      | Dirección                           |
| ------------- | ----------------------------------- |
| API de Memory | `http://localhost:5095`             |
| SQL Server    | `localhost,1433`                    |
| Hub SignalR   | `http://localhost:5095/hubs/memory` |

La API utiliza el puerto `8080` dentro del contenedor y se publica en el puerto `5095` de la máquina.

### 5. Detener los servicios

Para detener los contenedores sin eliminar los datos persistentes:

```powershell
docker compose down
```

Para detenerlos y eliminar también el volumen de SQL Server:

```powershell
docker compose down -v
```

**Advertencia:** `docker compose down -v` elimina el volumen de la base de datos de este proyecto y, con él, sus datos persistentes. Úsalo solamente cuando quieras borrar esa información.

## Ejecución local sin Docker

También es posible ejecutar la API directamente con .NET durante el desarrollo. En este caso se necesita una instancia de SQL Server accesible desde la máquina.

Desde la raíz del repositorio:

```powershell
dotnet run --project src/BattleHub.Memory.Api
```

La dirección y el perfil de ejecución pueden depender de la configuración local de `launchSettings.json`.

La configuración de conexión a SQL Server debe coincidir con la instancia disponible. Para aplicar las migraciones al arrancar, configura `Database:MigrateOnStartup` según el mecanismo admitido por la aplicación.

## Cómo ejecutar el microfrontend

Abre una terminal y ejecuta:

```powershell
cd src/memory-microfrontend/memory-game
npm ci
npm start
```

El microfrontend queda disponible en `http://localhost:4003`, con el remote `memoryGame`, según la configuración de Module Federation (ADR-003).

Se requiere Node.js 24 LTS (`>=24.11.0 <25`).

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

Para ejecutar solamente las pruebas unitarias excluyendo las marcadas como integración:

```powershell
dotnet test src/BattleHub.Memory.slnx --filter "Category!=Integration"
```

### Pruebas de integración

Las pruebas de integración necesitan una instancia de SQL Server.

- **En CI (GitHub Actions):** se utiliza SQL Server mediante Testcontainers, de acuerdo con la configuración de las pruebas.
- **En la máquina local con LocalDB:** configura la variable de conexión para las pruebas y reinicia la terminal si utilizas `setx`.

```powershell
setx MEMORY_TEST_CONNECTION "Server=(localdb)\mssqllocaldb;Integrated Security=true;TrustServerCertificate=true"
```

- **Con Docker:** si quieres utilizar el SQL Server del archivo `docker-compose.yml`, define la variable de conexión en la terminal actual. Sustituye el marcador por la contraseña configurada en `.env`; no escribas la contraseña real en el README.

```powershell
$env:MEMORY_TEST_CONNECTION = "Server=localhost,1433;User Id=sa;Password=<MSSQL_SA_PASSWORD>;TrustServerCertificate=True"
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
| `Cors:Origins`                     | Orígenes permitidos, incluidos el Shell y el microfrontend.                                                     |
| `Database:MigrateOnStartup`        | Indica si la API aplica las migraciones al arrancar. En Docker Compose está habilitado.                         |
| `SampleData:Enabled`               | Indica si se cargan partidas de ejemplo al arrancar.                                                            |
| `SampleData:Players`               | Configuración opcional de los jugadores para las partidas de ejemplo.                                           |

En la configuración de Docker Compose se utilizan variables de entorno de .NET con doble guion bajo para representar secciones anidadas. Algunos ejemplos son:

- `ConnectionStrings__MemoryDatabase`
- `Database__MigrateOnStartup`
- `SampleData__Enabled`

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

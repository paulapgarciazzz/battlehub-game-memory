# BattleHub - Juego de Memoria

Microservicio del **Juego de Memoria** de la plataforma BattleHub, desarrollado por el Equipo 6 como parte de la Etapa 2 del proyecto final.

El proyecto implementa un juego de memoria multijugador mediante una API REST, comunicación en tiempo real con SignalR y un microfrontend desarrollado en Aurelia 2. Su integración con la plataforma se realiza mediante el Shell de BattleHub.

Las reglas del juego se encuentran en [`docs/reglas-del-juego.md`](docs/reglas-del-juego.md).

## Contenido del repositorio

| Ruta                                      | Descripción                                                                             |
| ----------------------------------------- | --------------------------------------------------------------------------------------- |
| `src/BattleHub.Memory.Api`                | API REST y hub SignalR (`/hubs/memory`), desarrollado en .NET 10.                       |
| `src/BattleHub.Memory.Domain`             | Entidades y reglas principales del juego: `GameSession`, `Board` y `Player`.            |
| `src/BattleHub.Memory.Data`               | Persistencia mediante Entity Framework Core y SQL Server.                               |
| `src/memory-microfrontend/memory-game`    | Microfrontend en Aurelia 2, integrado con el Shell mediante Module Federation.          |
| `tests/BattleHub.Memory.UnitTests`        | Pruebas unitarias con Moq, organizadas en carpetas `Domain`, `Api` y `Data`.            |
| `tests/BattleHub.Memory.IntegrationTests` | Pruebas de integración con SQL Server y la API.                                         |
| `docs`                                    | Reglas del juego, documentación de la API y contrato de integración con el Shell.       |
| `docker-compose.yml`                      | Configuración para iniciar SQL Server, la API, los datos de ejemplo y el microfrontend. |
| `docker/sample-data`                      | Script SQL para cargar datos de ejemplo.                                                |

## Tecnologías utilizadas

| Tecnología              | Uso                                               |
| ----------------------- | ------------------------------------------------- |
| .NET 10                 | Desarrollo del backend.                           |
| C#                      | Lenguaje del backend.                             |
| Aurelia 2               | Desarrollo del microfrontend.                     |
| TypeScript              | Lenguaje del frontend.                            |
| Node.js 22 LTS          | Entorno de ejecución y herramientas del frontend. |
| SQL Server              | Motor de base de datos.                           |
| Entity Framework Core   | Acceso a datos y migraciones.                     |
| SignalR                 | Comunicación en tiempo real.                      |
| Module Federation       | Integración del microfrontend con el Shell.       |
| Docker y Docker Compose | Contenedores y ejecución del entorno.             |
| GitHub Actions          | Integración continua y automatización de pruebas. |

## Requisitos previos

Para ejecutar el proyecto se necesita:

- Docker Desktop con Docker Compose.
- Acceso al repositorio.
- Conexión a Internet para descargar las imágenes y dependencias necesarias durante la construcción inicial.

Para trabajar directamente en el código también se utilizan:

- .NET 10 SDK.
- Node.js 22 LTS (`>=22 <23`).
- Visual Studio 2026 Community.
- Una solución en formato `.slnx`.

## Ejecución del proyecto con Docker Compose

Docker Compose permite iniciar el entorno completo sin instalar .NET, Node.js ni SQL Server directamente en la máquina.

### 1. Iniciar los servicios

Desde la raíz del repositorio, ejecutar:

```powershell
docker compose up
```

Para reconstruir las imágenes antes de iniciar los servicios:

```powershell
docker compose up --build
```

Para ejecutarlos en segundo plano:

```powershell
docker compose up -d
```

Docker Compose realiza las siguientes operaciones:

1. Prepara la contraseña de SQL Server.
2. Construye las imágenes de la API y del microfrontend.
3. Inicia SQL Server y espera a que esté disponible.
4. Inicia la API y aplica las migraciones de Entity Framework Core.
5. Espera a que la API esté saludable.
6. Ejecuta el servicio independiente `sample-data`, que carga los datos de ejemplo.
7. Inicia el microfrontend mediante nginx.

El servicio `sample-data` es independiente de la API y de su mecanismo de seed. Ejecuta el script `docker/sample-data/datos-ejemplo.sql` después de que la API haya aplicado las migraciones.

### 2. Contraseña de SQL Server

La contraseña de SQL Server no está escrita directamente en el código fuente.

Se puede utilizar una contraseña generada automáticamente, almacenada en el volumen `memory-secrets`, o definir una propia mediante el archivo `.env`.

Para utilizar una contraseña propia:

1. Copiar `.env.example` y crear un archivo `.env`.
2. Definir la variable `MSSQL_SA_PASSWORD`.
3. Verificar que la contraseña cumpla los requisitos de complejidad de SQL Server y que no contenga `;`.

Para consultar la contraseña generada:

```powershell
docker compose exec sql cat /run/memory-secrets/sa_password
```

**Importante:** el archivo `.env` no debe subirse al repositorio. Si se cambia la contraseña con el volumen de SQL Server ya creado, será necesario recrear los volúmenes.

### 3. Comprobar los servicios

Consultar el estado de los contenedores:

```powershell
docker compose ps
```

Consultar los registros de la API:

```powershell
docker compose logs api
```

Consultar los registros de SQL Server:

```powershell
docker compose logs sql
```

Consultar los registros de la carga de datos:

```powershell
docker compose logs sample-data
```

El contenedor `sample-data` puede aparecer como `exited (0)` cuando la carga termina correctamente.

### 4. Direcciones y puertos

| Servicio                    | Dirección desde la máquina             | Puerto del contenedor |
| --------------------------- | -------------------------------------- | --------------------- |
| Microfrontend               | `http://localhost:4003`                | `80`                  |
| Remote de Module Federation | `http://localhost:4003/remoteEntry.js` | `80`                  |
| API de Memory               | `http://localhost:5095`                | `8080`                |
| Hub SignalR                 | `http://localhost:5095/hubs/memory`    | `8080`                |
| SQL Server                  | `localhost,1433`                       | `1433`                |

La API utiliza el puerto `8080` dentro del contenedor y se publica en el puerto `5095` de la máquina. El microfrontend utiliza nginx en el puerto `80` y se publica en el puerto `4003`.

### 5. Detener los servicios

Para detener los contenedores sin eliminar los datos persistentes:

```powershell
docker compose down
```

Para detenerlos y eliminar también los volúmenes:

```powershell
docker compose down -v
```

**Advertencia:** `docker compose down -v` elimina los volúmenes del proyecto, incluyendo la base de datos y la contraseña generada. También se pierden las partidas guardadas.

## Integración con el Shell de BattleHub

El microfrontend se integra con el Shell mediante **Module Federation**, utilizando el remote `memoryGame` y el archivo `remoteEntry.js`.

El Shell es responsable de la autenticación y de proporcionar al juego la información necesaria para iniciar una partida, incluyendo:

- `matchId`: identificador de la partida.
- `currentUser`: información del usuario actual.

El juego no tiene contacto directo con Auth0 ni con Matchmaking.

### Flujo de finalización de partida

Cuando termina una partida, el microfrontend emite el evento `battlehub:game-finished` mediante `context.events`.

El Shell escucha este evento y notifica a Matchmaking. Por lo tanto, el juego no realiza llamadas directas a Matchmaking.

El contrato de integración se encuentra documentado en [`docs/api-y-contrato-shell.md`](docs/api-y-contrato-shell.md).

### Modo práctica

El modo práctica se encuentra integrado con el Shell y permite iniciar una partida individual sin crear una sala de Matchmaking ni registrar resultados.

Se identifica mediante `mode: "practice"` y no utiliza la API para guardar la partida ni notificar su finalización.

### Modo playground

El requisito de playground establece que el juego debe poder ejecutarse independientemente del Shell, Auth0 y otros servicios.

**La forma de iniciar este modo debe documentarse de acuerdo con la implementación disponible en el repositorio.** No debe confundirse con el modo práctica integrado en el Shell.

## Persistencia y base de datos

El proyecto utiliza exclusivamente **SQL Server** como motor de base de datos.

La persistencia se administra mediante Entity Framework Core, utilizando migraciones incrementales para mantener la estructura de la base de datos.

Las entidades principales incluyen:

- `GameSession`: información de las partidas.
- `Board`: información del tablero.
- `Player`: información de los jugadores.

La configuración de conexión se administra mediante variables de entorno y configuración externa.

### Datos de ejemplo

Docker Compose incluye un servicio independiente llamado `sample-data`, encargado de cargar datos de ejemplo.

Se incluyen 18 partidas terminadas, correspondientes a los últimos 30 días, con cuatro jugadores de demostración:

- Ana (`demo|ana`).
- Bruno (`demo|bruno`).
- Carla (`demo|carla`).
- Diego (`demo|diego`).

El script utilizado es:

```text
docker/sample-data/datos-ejemplo.sql
```

La carga es idempotente: si las partidas ya existen, no se insertan nuevamente.

El servicio se ejecuta después de que la API haya aplicado las migraciones, pero no depende del mecanismo de seed de la aplicación.

## Pruebas

El proyecto separa las pruebas unitarias de las pruebas de integración.

### Pruebas unitarias

Se encuentran en:

```text
tests/BattleHub.Memory.UnitTests
```

Utilizan Moq para reemplazar dependencias y probar componentes de manera aislada, sin requerir una base de datos real.

Para ejecutarlas:

```powershell
dotnet test tests/BattleHub.Memory.UnitTests
```

### Pruebas de integración

Se encuentran en:

```text
tests/BattleHub.Memory.IntegrationTests
```

Comprueban la interacción entre los componentes utilizando SQL Server y la API.

En GitHub Actions se utiliza SQL Server mediante Testcontainers, de acuerdo con la configuración de las pruebas.

Para ejecutarlas:

```powershell
dotnet test tests/BattleHub.Memory.IntegrationTests
```

### Pruebas del frontend

Desde la carpeta del microfrontend:

```powershell
cd src/memory-microfrontend/memory-game
npm ci
npm test
```

Las pruebas se organizan en:

| Carpeta            | Propósito                                                       |
| ------------------ | --------------------------------------------------------------- |
| `test/unit`        | Pruebas individuales de componentes y servicios mediante fakes. |
| `test/integration` | Pruebas de integración del `GameModule`.                        |

También se dispone de comandos independientes:

```powershell
npm run test:unit
npm run test:integration
```

Para ejecutar las pruebas de .NET de la solución:

```powershell
dotnet test src/BattleHub.Memory.slnx
```

## Calidad de código e integración continua

El repositorio utiliza GitHub Actions para automatizar la validación del código.

Los flujos de trabajo contemplan:

- Restauración e instalación de dependencias.
- Construcción del backend y frontend.
- Ejecución de linters.
- Pruebas unitarias y de integración.
- Medición de cobertura.
- Construcción de imágenes Docker.
- Validación independiente de los componentes cuando corresponda.

El workflow completo se denomina `CI`. También existe un flujo adicional para ejecutar compilación y pruebas unitarias.

Los workflows de backend y frontend se ejecutan de manera independiente cuando hay cambios en sus respectivas rutas, mediante filtros de rutas (`paths`).

La cobertura mínima exigida es del 85 %. Los resultados se publican automáticamente como comentarios en los Pull Requests y el CI falla cuando no se alcanza el mínimo establecido.

### Cobertura alcanzada

Los resultados registrados en las ejecuciones de GitHub Actions son:

| Componente | Cobertura |
| ---------- | --------: |
| Backend    |      86 % |
| Frontend   |   89,56 % |

Estos porcentajes corresponden a las ejecuciones de cobertura registradas en los Pull Requests del proyecto.

## Seguridad y configuración

La autenticación se administra desde el Shell de BattleHub mediante Auth0. El Shell obtiene la información del usuario y la proporciona al microfrontend.

El juego no se conecta directamente a Auth0. La API y el hub SignalR no solicitan ni validan tokens de Auth0; utilizan la identidad proporcionada por el Shell.

Esta separación permite mantener la autenticación fuera del componente del juego, pero también implica que la API confía en la información de identidad que recibe y no realiza por sí misma la validación de permisos del usuario.

Las credenciales y los valores configurables deben mantenerse fuera del código fuente y proporcionarse mediante variables de entorno o archivos locales excluidos del control de versiones.

El archivo `.env.example` sirve como referencia para configurar el entorno. Los archivos `.env` no deben subirse al repositorio.

### Configuración de Docker Compose

| Variable               | Descripción                                    |
| ---------------------- | ---------------------------------------------- |
| `MSSQL_SA_PASSWORD`    | Contraseña del usuario `sa` de SQL Server.     |
| `MEMORY_CORS_SHELL`    | Origen del Shell permitido por CORS.           |
| `MEMORY_CORS_FRONTEND` | Origen del microfrontend permitido por CORS.   |
| `MEMORY_API_URL`       | Dirección de la API utilizada por el frontend. |

Los valores predeterminados de los orígenes son `http://localhost:4000` y `http://localhost:4003`, respectivamente.

### Configuración de la API

La configuración predeterminada se encuentra en:

```text
src/BattleHub.Memory.Api/appsettings.json
```

Entre las principales opciones se encuentran:

| Configuración                           | Descripción                                              |
| --------------------------------------- | -------------------------------------------------------- |
| `ConnectionStrings:MemoryDatabase`      | Cadena de conexión a SQL Server.                         |
| `Cors:Origins`                          | Orígenes permitidos para las solicitudes.                |
| `Database:MigrateOnStartup`             | Habilita la aplicación de migraciones al iniciar.        |
| `GameTiming:TurnTimeoutSeconds`         | Duración del turno de cada jugador.                      |
| `GameTiming:PreviewSeconds`             | Tiempo de visualización inicial de las cartas.           |
| `GameTiming:ResultRetryIntervalSeconds` | Intervalo para reintentar guardar resultados pendientes. |

Las variables de entorno de .NET pueden representar las secciones anidadas mediante doble guion bajo, por ejemplo:

```text
ConnectionStrings__MemoryDatabase
Database__MigrateOnStartup
GameTiming__TurnTimeoutSeconds
```

## Decisiones técnicas

### SQL Server y Entity Framework Core

Se seleccionó SQL Server como motor de base de datos por su integración con el ecosistema .NET y Entity Framework Core.

Esta combinación permite administrar la persistencia mediante entidades, consultas y migraciones incrementales.

### Arquitectura por capas

El backend se organiza en tres proyectos principales:

- **Domain:** contiene las entidades y reglas del juego.
- **Data:** administra el acceso a datos y la persistencia.
- **Api:** expone los endpoints REST y el hub SignalR.

Esta separación facilita el mantenimiento, las pruebas y la evolución de cada componente.

### Comunicación en tiempo real

Se utiliza SignalR para mantener la comunicación entre los jugadores y el servidor durante las partidas multijugador.

### Microfrontend y Module Federation

El juego se desarrolla como un microfrontend independiente en Aurelia 2. Module Federation permite que el Shell cargue el componente sin integrarlo directamente en su código fuente.

### Docker Compose

Docker Compose facilita la ejecución del entorno completo mediante un único comando, incluyendo la base de datos, la API, el microfrontend y la generación independiente de datos de ejemplo.

## Contratos implementados

A partir del repositorio [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts), el proyecto implementa:

- Contrato de entrada al juego mediante `matchId`, `gameType` y `currentUser`.
- Ciclo de vida de `GameModule`, correspondiente al contrato 03.
- API de resultados, historial y estadísticas, correspondiente al contrato 04.
- Integración con el Shell mediante Module Federation.
- Notificación del final de partida mediante `battlehub:game-finished`.
- Modo práctica integrado con el Shell, sin sala ni registro de resultados.

Para consultar los detalles, revisar [`docs/api-y-contrato-shell.md`](docs/api-y-contrato-shell.md).

## Ejemplos de consumo de la API

El documento [`docs/api-y-contrato-shell.md`](docs/api-y-contrato-shell.md) contiene ejemplos de solicitudes HTTP mediante `curl` para probar los endpoints de la API, consultar resultados, historial y estadísticas, y revisar la integración con el Shell.

## Limitaciones conocidas y aspectos por verificar

- Las partidas en curso se mantienen en memoria. Esta implementación debe revisarse frente al requisito R-12, que permite el uso de memoria únicamente en pruebas. No debe considerarse cumplido hasta resolver la persistencia de las partidas activas.
- Si la API se reinicia, las partidas que estaban en curso se pierden, aunque los resultados previamente guardados permanecen en SQL Server.
- La API no valida tokens de Auth0 ni restringe por sí misma las consultas de historial, estadísticas y resultados. La identidad se recibe desde el Shell.
- Debe verificarse que el modo playground permita jugar sin Shell, Auth0 ni otros servicios, conforme al requisito R-34.

## Referencias

- [Reglas del juego](docs/reglas-del-juego.md).
- [Documentación de la API y contrato con el Shell](docs/api-y-contrato-shell.md).
- [Contratos oficiales de BattleHub](https://github.com/javiercoulon-public/battlehub-contracts).

## Equipo

**Equipo 6 — Juego de Memoria**

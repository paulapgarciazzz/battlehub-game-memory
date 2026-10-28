# API de Memory y contrato de integración con Shell

## 1. Objetivo

Este documento describe los endpoints REST, la comunicación en tiempo real mediante SignalR y el contrato de integración entre el microfrontend Memory y el Shell de BattleHub.

Incluye ejemplos ejecutables con `curl`, parámetros, encabezados, cuerpos de solicitud, respuestas esperadas y códigos HTTP. Los ejemplos utilizan datos ficticios. El juego no tiene contacto con Auth0: la API y el hub no piden token.

## 2. Configuración general

### URLs de desarrollo

| Recurso                             | URL                                       |
| ----------------------------------- | ----------------------------------------- |
| API REST                            | `http://localhost:5095`                   |
| Health check                        | `http://localhost:5095/health`            |
| Documento OpenAPI (solo desarrollo) | `http://localhost:5095/openapi/v1.json`   |
| Hub competitivo de SignalR          | `http://localhost:5095/hubs/memory`       |
| Hub local de desarrollo             | `http://localhost:5095/hubs/memory-local` |
| Shell                               | `http://localhost:4000`                   |
| Microfrontend Memory                | `http://localhost:4003`                   |
| Remote Entry de Module Federation   | `http://localhost:4003/remoteEntry.js`    |

El hub `/hubs/memory-local` y el documento OpenAPI se habilitan únicamente cuando la API se ejecuta en el entorno de desarrollo. El hub local no guarda resultados y no debe exponerse en producción.

### Requisitos

- Backend de Memory ejecutándose en el puerto 5095.
- Base de datos SQL Server configurada y accesible.
- Para jugar una partida competitiva, el Shell entrega el `matchId` de la sala y el `currentUser`.

Los ejemplos siguientes utilizan la sintaxis de Bash, compatible con Git Bash y WSL. Se puede sustituir la dirección base si la API se ejecuta en otro entorno.

### Variables de entorno usadas en los ejemplos

```bash
export API_URL="http://localhost:5095"
export USER_ID="auth0|usuario-de-prueba"
export MATCH_ID="MEMORY-TEST-001"
```

`USER_ID` y `MATCH_ID` son valores de ejemplo. Deben reemplazarse por los de una partida real.

## 3. Identidad del jugador

Según los requisitos de BattleHub, **ningún juego tiene contacto con Auth0**. El Shell inicia sesión, consulta al Profile Service qué juegos puede ver el usuario y le entrega al juego el `matchId` y el `currentUser` (`id` y `displayName`).

- La API REST y el hub `/hubs/memory` no piden token ni encabezado `Authorization`.
- El jugador es el `currentUser.id` que entrega el Shell; el hub lo recibe en `JoinMatch`.
- El juego no llama a Matchmaking. Al terminar la partida avisa al Shell con un evento (sección 5.5) y el Shell avisa a Matchmaking.

### Códigos HTTP generales

| Código                      | Significado                                                                              |
| --------------------------- | ---------------------------------------------------------------------------------------- |
| `200 OK`                    | Solicitud procesada correctamente.                                                       |
| `400 Bad Request`           | Los datos enviados no cumplen las condiciones de la operación.                           |
| `404 Not Found`             | No se encontró la partida o el resultado solicitado.                                     |
| `500 Internal Server Error` | Error interno no controlado.                                                             |

Los códigos exactos de errores internos pueden variar según la causa y el manejo de excepciones del servidor.

### Resumen de endpoints

| #   | Método | Ruta                                         | Disponibilidad  |
| --- | ------ | -------------------------------------------- | --------------- |
| 4.1 | `GET`  | `/health`                                    | Siempre         |
| 4.2 | `GET`  | `/api/games/memory/players/{userId}/history` | Siempre         |
| 4.3 | `GET`  | `/api/games/memory/players/{userId}/stats`   | Siempre         |
| 4.4 | `POST` | `/api/games/memory/results`                  | Siempre         |
| 4.5 | `GET`  | `/api/games/memory/results/{matchId}`        | Siempre         |
| 8   | `GET`  | `/openapi/v1.json`                           | Solo desarrollo |

## 4. Endpoints REST

### 4.1. Verificar el estado de la API

- **Método:** `GET`
- **Ruta:** `/health`
- **Autenticación:** No requerida.
- **Parámetros de ruta:** ninguno.
- **Parámetros de consulta:** ninguno.
- **Encabezados:** ninguno obligatorio.
- **Cuerpo de la solicitud:** no aplica.

Este endpoint permite comprobar si la API responde.

#### Ejemplo curl

```bash
curl -i "$API_URL/health"
```

#### Respuesta esperada

- **Código HTTP:** `200 OK`
- **Content-Type:** `text/plain`

```
Healthy
```

> Nota: el health check por defecto de ASP.NET Core responde con texto plano. Si la implementación del proyecto lo personaliza para devolver JSON, ajustar este ejemplo al formato real.

#### Códigos HTTP

- `200 OK`: la API está operativa.
- `503 Service Unavailable`: la API responde pero alguna comprobación de salud falla (comportamiento estándar de ASP.NET Core).

### 4.2. Consultar el historial de partidas de un jugador

- **Método:** `GET`
- **Ruta:** `/api/games/memory/players/{userId}/history`
- **Autenticación:** no requerida.
- **Cuerpo de la solicitud:** no aplica.

#### Parámetros

| Parámetro | Ubicación | Tipo   | Obligatorio | Descripción                                               |
| --------- | --------- | ------ | ----------- | --------------------------------------------------------- |
| `userId`  | Ruta      | string | Sí          | Identificador del usuario (`currentUser.id` del Shell).   |

#### Encabezados

| Encabezado      | Valor              | Obligatorio |
| --------------- | ------------------ | ----------- |
| `Accept`        | `application/json` | Recomendado |

#### Ejemplo curl

```bash
curl -i \
  -H "Accept: application/json" \
  "$API_URL/api/games/memory/players/$USER_ID/history"
```

El valor de `USER_ID` es ilustrativo. Debe sustituirse por el identificador real del usuario.

#### Respuesta esperada

- **Código HTTP:** `200 OK`

La respuesta es un arreglo de partidas. Si el usuario no tiene historial, puede devolver un arreglo vacío (`[]`).

```json
[
  {
    "matchId": "MEMORY-TEST-001",
    "gameType": "memory",
    "startedAt": "2026-10-20T15:00:00Z",
    "finishedAt": "2026-10-20T15:03:00Z",
    "winnerUserId": "auth0|usuario-de-prueba",
    "isDraw": false,
    "score": 4
  }
]
```

Los valores son ficticios y representan la estructura del DTO. Las fechas, el ganador y el puntaje dependen de los datos almacenados.

#### Códigos HTTP

- `200 OK`: historial consultado correctamente.

### 4.3. Consultar estadísticas de un jugador

- **Método:** `GET`
- **Ruta:** `/api/games/memory/players/{userId}/stats`
- **Autenticación:** no requerida.
- **Cuerpo de la solicitud:** no aplica.

#### Parámetros

| Parámetro | Ubicación | Tipo   | Obligatorio | Descripción                            |
| --------- | --------- | ------ | ----------- | -------------------------------------- |
| `userId`  | Ruta      | string | Sí          | Identificador del usuario.             |

#### Encabezados

| Encabezado      | Valor              | Obligatorio |
| --------------- | ------------------ | ----------- |
| `Accept`        | `application/json` | Recomendado |

#### Ejemplo curl

```bash
curl -i \
  -H "Accept: application/json" \
  "$API_URL/api/games/memory/players/$USER_ID/stats"
```

#### Respuesta esperada

- **Código HTTP:** `200 OK`

```json
{
  "userId": "auth0|usuario-de-prueba",
  "gamesPlayed": 10,
  "wins": 6,
  "losses": 3,
  "draws": 1,
  "totalPairs": 48,
  "bestScore": 8,
  "averageScore": 4.8
}
```

Los números son ejemplos ilustrativos. La API calcula las estadísticas a partir del historial almacenado.

| Campo          | Descripción                          |
| -------------- | ------------------------------------ |
| `userId`       | Identificador del jugador.           |
| `gamesPlayed`  | Cantidad de partidas contabilizadas. |
| `wins`         | Partidas ganadas.                    |
| `losses`       | Partidas perdidas.                   |
| `draws`        | Partidas empatadas.                  |
| `totalPairs`   | Total de parejas encontradas.        |
| `bestScore`    | Mejor puntaje registrado.            |
| `averageScore` | Puntaje promedio.                    |

#### Códigos HTTP

- `200 OK`: estadísticas consultadas correctamente.

### 4.4. Guardar o confirmar el resultado de una partida

- **Método:** `POST`
- **Ruta:** `/api/games/memory/results`
- **Autenticación:** no requerida.
- **Parámetros de ruta y de consulta:** ninguno.

Este endpoint registra el resultado de una partida finalizada. Normalmente, el backend lo guarda automáticamente cuando la partida termina a través de SignalR. El endpoint permite volver a solicitar el guardado de forma idempotente, sin duplicar el resultado.

El cliente no determina el ganador ni envía los puntajes de los jugadores. El servidor utiliza la sesión de juego y sus datos autoritativos.

#### Encabezados

| Encabezado      | Valor              | Obligatorio |
| --------------- | ------------------ | ----------- |
| `Content-Type`  | `application/json` | Sí          |
| `Accept`        | `application/json` | Recomendado |

#### Cuerpo de la solicitud

| Campo     | Tipo   | Obligatorio | Descripción                  |
| --------- | ------ | ----------- | ---------------------------- |
| `matchId` | string | Sí          | Identificador de la partida. |

```json
{
  "matchId": "MEMORY-TEST-001"
}
```

#### Ejemplo curl

```bash
curl -i \
  -X POST \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{"matchId":"MEMORY-TEST-001"}' \
  "$API_URL/api/games/memory/results"
```

#### Respuesta esperada

- **Código HTTP:** `200 OK`

```json
{
  "resultId": "00000000-0000-0000-0000-000000000001",
  "matchId": "MEMORY-TEST-001",
  "message": "Resultado de la partida guardado correctamente."
}
```

El `resultId` es un UUID ilustrativo.

Si el resultado ya estaba guardado y la sesión ya no está en memoria, la API puede responder igualmente con `200 OK`:

```json
{
  "resultId": "00000000-0000-0000-0000-000000000001",
  "matchId": "MEMORY-TEST-001",
  "message": "El resultado de la partida ya estaba guardado."
}
```

#### Códigos HTTP

- `200 OK`: resultado guardado o confirmado.
- `400 Bad Request`: `matchId` vacío o la partida todavía no ha terminado.
- `404 Not Found`: no existe una sesión ni un resultado almacenado para el `matchId`.

> Importante: un `200 OK` indica que el resultado fue guardado o que ya existía. Si el guardado automático falla durante la finalización de la partida, el backend dispone de un mecanismo de reintento y emite `ResultSaveFailed` mientras el resultado no se confirme.

### 4.5. Consultar el resultado de una partida

- **Método:** `GET`
- **Ruta:** `/api/games/memory/results/{matchId}`
- **Autenticación:** no requerida.
- **Cuerpo de la solicitud:** no aplica.

Este endpoint devuelve el resultado almacenado.

#### Parámetros

| Parámetro | Ubicación | Tipo   | Obligatorio | Descripción                  |
| --------- | --------- | ------ | ----------- | ---------------------------- |
| `matchId` | Ruta      | string | Sí          | Identificador de la partida. |

#### Encabezados

| Encabezado      | Valor              | Obligatorio |
| --------------- | ------------------ | ----------- |
| `Accept`        | `application/json` | Recomendado |

#### Ejemplo curl

```bash
curl -i \
  -H "Accept: application/json" \
  "$API_URL/api/games/memory/results/$MATCH_ID"
```

#### Respuesta esperada

- **Código HTTP:** `200 OK`

```json
{
  "resultId": "00000000-0000-0000-0000-000000000001",
  "matchId": "MEMORY-TEST-001",
  "gameType": "memory",
  "players": [
    {
      "userId": "auth0|usuario-de-prueba",
      "displayName": "Jugador Uno",
      "score": 4
    },
    {
      "userId": "auth0|segundo-usuario",
      "displayName": "Jugador Dos",
      "score": 3
    }
  ],
  "startedAt": "2026-10-20T15:00:00Z",
  "finishedAt": "2026-10-20T15:03:00Z",
  "winnerUserId": "auth0|usuario-de-prueba",
  "isDraw": false,
  "metadata": null
}
```

Los valores del ejemplo no representan una partida real.

#### Códigos HTTP

- `200 OK`: resultado consultado correctamente.
- `404 Not Found`: no existe un resultado almacenado para el `matchId`.

### 4.6. Ejemplos de respuestas de error

Los siguientes ejemplos muestran cómo se ve un error al ejecutar los `curl` anteriores.

**404 Not Found** (no existe un resultado para el `matchId`):

```bash
curl -i "$API_URL/api/games/memory/results/no-existe"
```

```
HTTP/1.1 404 Not Found
```

> Nota: el formato exacto del cuerpo de los errores `400` y `404` depende del controlador (puede ser vacío o un objeto `ProblemDetails` de ASP.NET Core). Confirmarlo ejecutando la llamada contra la API local.

## 5. Contrato de integración con el Shell

### 5.1. Responsabilidades

El Shell es responsable de la autenticación del usuario con Auth0, la selección o creación de la sala de Matchmaking, la carga del microfrontend y el aviso de fin de partida a Matchmaking.

Memory es responsable de ejecutar la partida, validar las jugadas, sincronizar el estado de ambos jugadores, guardar el resultado y avisar al Shell cuando la partida termina. Memory no tiene contacto con Auth0 ni con Matchmaking.

El juego no expone un endpoint propio para crear o buscar salas competitivas. El identificador de la partida proviene de Matchmaking a través del Shell.

### 5.2. Carga del microfrontend

| Propiedad              | Valor                                  |
| ---------------------- | -------------------------------------- |
| Nombre del remote      | `memoryGame`                           |
| Módulo expuesto        | `./GameModule`                         |
| Elemento personalizado | `memory-game-module`                   |
| URL local del remote   | `http://localhost:4003/remoteEntry.js` |

El Shell carga el módulo y llama a su ciclo de vida en este orden:

1. `initialize(context)`: entrega el contexto del juego.
2. `start()`: inicia la conexión y solicita unirse a la partida.
3. `pause()`: pausa las jugadas locales, sin detener el servidor ni el temporizador del rival.
4. `dispose()`: libera la conexión y los recursos locales del juego.

### 5.3. Contexto que recibe Memory

El contrato `GameContext` es el del contrato 03 (sección 5), sin extensiones:

| Campo                     | Tipo   | Descripción                                      |
| ------------------------- | ------ | ------------------------------------------------ |
| `matchId`                 | string | Identificador de la sala creada por Matchmaking. |
| `gameType`                | string | Debe ser `"memory"`.                             |
| `currentUser.id`          | string | Identificador del usuario que entrega el Shell.  |
| `currentUser.displayName` | string | Nombre que se muestra en la partida.             |

Ejemplo del contexto:

```ts
const context = {
  matchId: "MEMORY-TEST-001",
  gameType: "memory",
  currentUser: {
    id: "auth0|usuario-de-prueba",
    displayName: "Jugador Uno",
  },
};
```

`initialize()` valida que el contexto contenga el tipo de juego `memory`, el identificador de partida y los datos del usuario. Si faltan datos obligatorios, la inicialización falla.

### 5.4. Entrada a la partida

Al ejecutar `start()`, el microfrontend invoca `JoinMatch(matchId, userId, displayName)` en el hub competitivo con los datos del contexto.

- Los dos primeros jugadores distintos que entran con el mismo `matchId` forman la partida.
- Un tercer jugador no puede entrar a una partida que ya tiene a sus dos jugadores.
- Un jugador de la partida puede volver a entrar (por ejemplo, al recargar) y recibe el estado actual.

Memory no consulta a Matchmaking: el Shell solo abre el juego cuando la sala ya está lista.

### 5.5. Evento de fin de partida

Cuando la partida termina, el elemento `memory-game-module` emite un evento DOM:

| Propiedad  | Valor                                             |
| ---------- | ------------------------------------------------- |
| Nombre     | `battlehub:game-finished`                         |
| Tipo       | `CustomEvent`                                     |
| `bubbles`  | `true` (se puede escuchar en el área o en window) |
| `composed` | `true`                                            |

Contenido de `event.detail`:

```json
{
  "matchId": "MEMORY-TEST-001",
  "gameType": "memory",
  "isDraw": false,
  "winnerUserId": "auth0|usuario-de-prueba",
  "players": [
    { "userId": "auth0|usuario-de-prueba", "displayName": "Jugador Uno", "score": 5 },
    { "userId": "auth0|segundo-usuario", "displayName": "Jugador Dos", "score": 3 }
  ]
}
```

- Se emite una sola vez por partida, en la pantalla de cada uno de los dos jugadores. El aviso del Shell a Matchmaking debe tolerar que llegue dos veces.
- `winnerUserId` es `null` cuando hay empate.
- Solo se emite dentro del Shell. El modo local, solo y práctica no lo emiten.

Ejemplo de cómo lo escucha el Shell:

```ts
window.addEventListener("battlehub:game-finished", (event) => {
  const { matchId } = (event as CustomEvent).detail;
  // El Shell avisa a Matchmaking que la partida terminó.
});
```

## 6. Contrato de SignalR

### 6.1. Dirección

- **Hub competitivo:** `http://localhost:5095/hubs/memory`

La conexión se realiza mediante SignalR y no requiere token.

#### Ejemplo de conexión (cliente `@microsoft/signalr`)

```ts
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5095/hubs/memory")
  .withAutomaticReconnect()
  .build();

connection.on("GameReady", (payload) => {
  /* ... */
});
connection.on("CardFlipped", (payload) => {
  /* ... */
});
connection.on("ResultSaved", (payload) => {
  /* ... */
});

await connection.start();
await connection.invoke(
  "JoinMatch",
  context.matchId,
  context.currentUser.id,
  context.currentUser.displayName,
);
```

### 6.2. Métodos del cliente

#### `JoinMatch(matchId, userId, displayName)`

Une al jugador a una partida.

| Parámetro     | Tipo   | Descripción                                     |
| ------------- | ------ | ----------------------------------------------- |
| `matchId`     | string | Identificador de la sala de Matchmaking.        |
| `userId`      | string | `currentUser.id` que entrega el Shell.          |
| `displayName` | string | `currentUser.displayName` que entrega el Shell. |

Este método se utiliza tanto para incorporarse a la partida como para recuperar el estado después de una reconexión. Si la sesión ya existe, el jugador recibe un `StateSnapshot` con el estado actual.

#### `FlipCard(matchId, cardId)`

Solicita voltear una carta.

| Parámetro | Tipo   | Descripción                                                 |
| --------- | ------ | ----------------------------------------------------------- |
| `matchId` | string | Identificador de la partida a la que se unió esta conexión. |
| `cardId`  | int    | Identificador de la carta.                                  |

El servidor comprueba que la conexión se haya unido a esa partida, que la previsualización haya terminado y que la jugada sea válida para el turno actual. El jugador que voltea es el que se unió con esta conexión.

Ejemplo:

```ts
await connection.invoke("FlipCard", "MEMORY-TEST-001", 1);
```

### 6.3. Eventos enviados por el servidor

| Evento             | Cuándo se emite                                                 | Datos principales                                                                              |
| ------------------ | --------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `PlayerJoined`     | Un jugador se une a la sala.                                    | `userId`, `displayName`.                                                                       |
| `GameReady`        | Ambos jugadores están presentes y comienza la previsualización. | `matchId`, `previewSeconds`, `cards`, `players`.                                               |
| `PreviewFinished`  | Termina la previsualización inicial.                            | `matchId`, `currentPlayerId`, `turnDeadline`.                                                  |
| `StateSnapshot`    | El jugador recibe el estado actual de la partida.               | Estado de las cartas, jugadores, marcador, turno, vencimiento y estado del resultado guardado. |
| `CardFlipped`      | El servidor procesa una jugada válida.                          | `matchId`, `userId`, `cards`, `isMatch`, `currentPlayerId`, `isFinished`, `turnDeadline`.      |
| `ResultSaved`      | El resultado quedó guardado.                                    | `matchId`.                                                                                     |
| `ResultSaveFailed` | Falló el intento de guardar el resultado.                       | `matchId`.                                                                                     |

### 6.4. Ejemplos de mensajes

Evento `PlayerJoined`:

```json
{
  "userId": "auth0|usuario-de-prueba",
  "displayName": "Jugador Uno"
}
```

Evento `GameReady`:

```json
{
  "matchId": "MEMORY-TEST-001",
  "previewSeconds": 5,
  "cards": [
    { "id": 1, "value": "A" },
    { "id": 2, "value": "B" }
  ],
  "players": [
    { "userId": "auth0|usuario-de-prueba", "displayName": "Jugador Uno" },
    { "userId": "auth0|segundo-usuario", "displayName": "Jugador Dos" }
  ]
}
```

Las cartas y los jugadores de este mensaje son datos ilustrativos; el tablero real contiene las cartas definidas por la implementación del juego.

Evento `PreviewFinished`:

```json
{
  "matchId": "MEMORY-TEST-001",
  "currentPlayerId": "auth0|usuario-de-prueba",
  "turnDeadline": "2026-10-20T15:00:15Z"
}
```

Evento `CardFlipped`:

```json
{
  "matchId": "MEMORY-TEST-001",
  "userId": "auth0|usuario-de-prueba",
  "cards": [{ "id": 1, "value": "A" }],
  "isMatch": false,
  "currentPlayerId": "auth0|segundo-usuario",
  "isFinished": false,
  "turnDeadline": "2026-10-20T15:00:25Z"
}
```

Eventos `ResultSaved` y `ResultSaveFailed`:

```json
{
  "matchId": "MEMORY-TEST-001"
}
```

Los valores y las fechas de estos ejemplos son ficticios. La respuesta real depende de la jugada y del estado de la sesión.

### 6.5. Errores del hub

SignalR comunica los errores de las invocaciones mediante errores de invocación, no mediante códigos HTTP independientes para cada jugada.

Algunos errores definidos por el hub competitivo son:

| Error                  | Significado                                                                     |
| ---------------------- | ------------------------------------------------------------------------------- |
| `JOIN_REQUIRED`        | La conexión no se ha unido a la partida indicada.                               |
| `MATCH_NOT_FOUND`      | No existe una sesión activa correspondiente a la partida.                       |
| `PREVIEW_ACTIVE`       | Todavía se está mostrando la previsualización de las cartas.                    |
| `INVALID_JOIN`         | Falta el `matchId`, el `userId` o el `displayName`, o son demasiado largos.     |
| `PLAYER_NOT_IN_MATCH`  | La partida ya tiene a sus dos jugadores y este usuario no es uno de ellos.      |

También pueden rechazarse jugadas por turno incorrecto, carta inexistente o carta ya volteada. En estos casos el servidor comunica el error de la operación mediante una excepción de SignalR.

### 6.6. Flujo de una partida competitiva

1. El usuario inicia sesión en el Shell.
2. El Shell obtiene o crea una sala de Matchmaking.
3. El Shell carga el módulo `memoryGame`.
4. El Shell entrega el `GameContext` a `initialize()`.
5. `start()` establece la conexión con `/hubs/memory` y ejecuta `JoinMatch` con el `currentUser`.
6. Cuando los dos jugadores están conectados, el servidor envía `GameReady`.
7. Al finalizar la previsualización, envía `PreviewFinished` y comienza el turno.
8. Los jugadores invocan `FlipCard`; el servidor valida cada jugada y comunica el resultado mediante `CardFlipped`.
9. Cuando la partida termina, el backend guarda el resultado y el hub emite `ResultSaved`, o `ResultSaveFailed` si el intento falla (el backend reintenta).
10. El microfrontend emite `battlehub:game-finished` y el Shell avisa a Matchmaking.
11. Los jugadores pueden consultar el resultado guardado mediante `GET /api/games/memory/results/{matchId}`.

El cliente debe considerar al servidor como la fuente autoritativa del estado, del turno, del resultado y de la confirmación del guardado.

## 7. Matchmaking

Memory no se comunica con Matchmaking. El Shell crea o elige la sala, abre el juego con su `matchId` y, cuando recibe `battlehub:game-finished`, avisa a Matchmaking que la partida terminó.

## 8. OpenAPI durante desarrollo

- **Método:** `GET`
- **Ruta:** `/openapi/v1.json`
- **Autenticación:** No requerida.
- **Parámetros, encabezados y cuerpo:** no aplican.

En el entorno de desarrollo, la API habilita OpenAPI mediante `MapOpenApi()`.

```bash
curl -i "$API_URL/openapi/v1.json"
```

- **Respuesta esperada:** `200 OK` con el documento OpenAPI en formato JSON.
- **Códigos HTTP:** `200 OK` en desarrollo; `404 Not Found` si la aplicación no se ejecuta en el entorno `Development`.

Esta ruta está disponible solamente cuando la aplicación se ejecuta en el entorno `Development`. No debe asumirse que está habilitada en producción.

## 9. Buenas prácticas de seguridad

- No incluir secretos ni contraseñas reales en los ejemplos o en el repositorio.
- Como el juego no usa Auth0, la API confía en el `userId` que entrega el Shell. No exponer la API fuera de la red de BattleHub.
- No considerar una partida guardada hasta recibir `ResultSaved`.
- No exponer `/hubs/memory-local` fuera del entorno de desarrollo.
- Configurar HTTPS, CORS y las direcciones reales de los servicios antes de desplegar.

## 10. Referencias de implementación

La documentación se basa en los siguientes archivos del repositorio:

- `src/BattleHub.Memory.Api/Program.cs`: configuración de la API, OpenAPI, CORS y hubs.
- `src/BattleHub.Memory.Api/Controllers/MemoryController.cs`: historial y estadísticas.
- `src/BattleHub.Memory.Api/Controllers/MemoryGameResultsController.cs`: guardado y consulta de resultados.
- `src/BattleHub.Memory.Api/Hubs/MemoryHub.cs`: comunicación competitiva mediante SignalR.
- `src/BattleHub.Memory.Api/Hubs/MemoryLocalHub.cs`: juego local exclusivo para desarrollo.
- `src/memory-microfrontend/memory-game/src/game-module.ts`: ciclo de vida e integración con el Shell.
- `src/memory-microfrontend/memory-game/src/game-contracts.ts`: contrato de integración del microfrontend.
- `src/memory-microfrontend/memory-game/README.md`: configuración y ejecución del microfrontend.

## 11. Verificación de los ejemplos

Los ejemplos `curl` deben probarse con la API en funcionamiento y una base de datos configurada. Lista de verificación:

- [ ] `GET /health` responde `200 OK`.
- [ ] `GET .../history` responde `200 OK`.
- [ ] `GET .../stats` responde `200 OK`.
- [ ] `POST /api/games/memory/results` con una partida terminada responde `200 OK`.
- [ ] `GET .../results/{matchId}` de una partida guardada responde `200 OK`; de una que no existe, `404 Not Found`.
- [ ] SignalR: conexión, incorporación de dos jugadores, sincronización de cartas, errores de turno y confirmación del guardado.
- [ ] El Shell recibe `battlehub:game-finished` al terminar la partida.

Los ejemplos documentan el contrato esperado. Marcar cada casilla al ejecutarlo contra un entorno real.

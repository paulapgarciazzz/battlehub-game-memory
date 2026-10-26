# API de Memory y contrato de integración con Shell

## 1. Objetivo

Este documento describe los endpoints REST, la comunicación en tiempo real mediante SignalR y el contrato de integración entre el microfrontend Memory y el Shell de BattleHub.

Incluye ejemplos ejecutables con `curl`, parámetros, encabezados, cuerpos de solicitud, respuestas esperadas y códigos HTTP. Los ejemplos utilizan datos ficticios y no incluyen credenciales, tokens ni secretos reales.

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

El hub `/hubs/memory-local` y el documento OpenAPI se habilitan únicamente cuando la API se ejecuta en el entorno de desarrollo. El hub local no utiliza Auth0 ni Matchmaking y no debe exponerse en producción.

### Requisitos

- Backend de Memory ejecutándose en el puerto 5095.
- Base de datos SQL Server configurada y accesible.
- Configuración válida de Auth0.
- Un token de acceso de usuario válido para la audiencia de Memory para los endpoints protegidos.
- Para jugar una partida competitiva, una sala válida de Matchmaking con dos participantes.

Los ejemplos siguientes utilizan la sintaxis de Bash, compatible con Git Bash y WSL. Se puede sustituir la dirección base si la API se ejecuta en otro entorno.

### Variables de entorno usadas en los ejemplos

```bash
export API_URL="http://localhost:5095"
export MEMORY_TOKEN="REEMPLAZAR_POR_UN_TOKEN_VALIDO_DE_PRUEBA"
export USER_ID="auth0|usuario-de-prueba"
export MATCH_ID="MEMORY-TEST-001"
```

`MEMORY_TOKEN` y `USER_ID` son marcadores de posición. Deben reemplazarse localmente por valores reales de prueba y nunca publicarse en el repositorio.

## 3. Autenticación y autorización

La API utiliza Auth0 y tokens JWT firmados con RS256.

El backend valida la firma, el emisor, la audiencia y la vigencia del token. El identificador del usuario se obtiene de la propiedad `sub` del JWT; no se confía en un identificador de usuario enviado por el cliente para determinar la identidad autenticada.

### Token de usuario

Los endpoints protegidos reciben el encabezado:

```
Authorization: Bearer <token>
```

El token de usuario debe corresponder a la audiencia configurada para Memory. Un token de otra audiencia no es intercambiable con este token.

### Políticas de autorización

| Política      | Uso                                                                                                                         |
| ------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `MemoryPlay`  | Permite a un usuario autenticado jugar, conectarse al hub competitivo, consultar su historial y consultar sus estadísticas. |
| `MemoryRead`  | Define permisos de lectura para usuarios y servicios autorizados.                                                           |
| `MemoryWrite` | Define permisos de escritura para clientes máquina a máquina con el permiso `games.memory.results.write`.                   |

Los endpoints documentados en este archivo están protegidos por `MemoryPlay`, excepto `/health`. Por lo tanto, no deben invocarse con un token máquina a máquina como si fuera un usuario.

Además, un jugador solamente puede consultar su propio historial y sus propias estadísticas. La consulta de un resultado individual también verifica que el usuario autenticado haya participado en esa partida.

### Códigos HTTP generales

| Código                      | Significado                                                                              |
| --------------------------- | ---------------------------------------------------------------------------------------- |
| `200 OK`                    | Solicitud procesada correctamente.                                                       |
| `400 Bad Request`           | Los datos enviados no cumplen las condiciones de la operación.                           |
| `401 Unauthorized`          | No se proporcionó un token válido o la autenticación falló.                              |
| `403 Forbidden`             | El usuario autenticado no tiene permiso para realizar la operación o acceder al recurso. |
| `404 Not Found`             | No se encontró la partida o el resultado solicitado.                                     |
| `500 Internal Server Error` | Error interno no controlado.                                                             |

Los códigos exactos de errores internos pueden variar según la causa y el manejo de excepciones del servidor.

### Resumen de endpoints

| #   | Método | Ruta                                         | Autenticación                  |
| --- | ------ | -------------------------------------------- | ------------------------------ |
| 4.1 | `GET`  | `/health`                                    | No requerida                   |
| 4.2 | `GET`  | `/api/games/memory/players/{userId}/history` | `MemoryPlay`                   |
| 4.3 | `GET`  | `/api/games/memory/players/{userId}/stats`   | `MemoryPlay`                   |
| 4.4 | `POST` | `/api/games/memory/results`                  | `MemoryPlay`                   |
| 4.5 | `GET`  | `/api/games/memory/results/{matchId}`        | `MemoryPlay`                   |
| 8   | `GET`  | `/openapi/v1.json`                           | No requerida (solo desarrollo) |

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
- **Autenticación:** Requerida mediante `MemoryPlay`.
- **Cuerpo de la solicitud:** no aplica.

El parámetro `userId` debe coincidir con el identificador `sub` del usuario autenticado. Si no coincide, la API devuelve `403 Forbidden`.

#### Parámetros

| Parámetro | Ubicación | Tipo   | Obligatorio | Descripción                                               |
| --------- | --------- | ------ | ----------- | --------------------------------------------------------- |
| `userId`  | Ruta      | string | Sí          | Identificador del usuario autenticado, obtenido de Auth0. |

#### Encabezados

| Encabezado      | Valor              | Obligatorio |
| --------------- | ------------------ | ----------- |
| `Authorization` | `Bearer <token>`   | Sí          |
| `Accept`        | `application/json` | Recomendado |

#### Ejemplo curl

```bash
curl -i \
  -H "Authorization: Bearer $MEMORY_TOKEN" \
  -H "Accept: application/json" \
  "$API_URL/api/games/memory/players/$USER_ID/history"
```

El valor de `USER_ID` es ilustrativo. Debe sustituirse por el identificador real del usuario asociado al token de prueba.

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
- `401 Unauthorized`: token ausente, inválido o vencido.
- `403 Forbidden`: el `userId` solicitado no coincide con el usuario autenticado.

### 4.3. Consultar estadísticas de un jugador

- **Método:** `GET`
- **Ruta:** `/api/games/memory/players/{userId}/stats`
- **Autenticación:** Requerida mediante `MemoryPlay`.
- **Cuerpo de la solicitud:** no aplica.

El jugador solamente puede consultar sus propias estadísticas.

#### Parámetros

| Parámetro | Ubicación | Tipo   | Obligatorio | Descripción                            |
| --------- | --------- | ------ | ----------- | -------------------------------------- |
| `userId`  | Ruta      | string | Sí          | Identificador del usuario autenticado. |

#### Encabezados

| Encabezado      | Valor              | Obligatorio |
| --------------- | ------------------ | ----------- |
| `Authorization` | `Bearer <token>`   | Sí          |
| `Accept`        | `application/json` | Recomendado |

#### Ejemplo curl

```bash
curl -i \
  -H "Authorization: Bearer $MEMORY_TOKEN" \
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
- `401 Unauthorized`: token ausente o inválido.
- `403 Forbidden`: el identificador solicitado pertenece a otro usuario.

### 4.4. Guardar o confirmar el resultado de una partida

- **Método:** `POST`
- **Ruta:** `/api/games/memory/results`
- **Autenticación:** Requerida mediante `MemoryPlay`.
- **Parámetros de ruta y de consulta:** ninguno.

Este endpoint registra el resultado de una partida finalizada. Normalmente, el backend lo guarda automáticamente cuando la partida termina a través de SignalR. El endpoint permite volver a solicitar el guardado de forma idempotente, sin duplicar el resultado.

El cliente no determina el ganador ni envía los puntajes de los jugadores. El servidor utiliza la sesión de juego y sus datos autoritativos.

#### Encabezados

| Encabezado      | Valor              | Obligatorio |
| --------------- | ------------------ | ----------- |
| `Authorization` | `Bearer <token>`   | Sí          |
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
  -H "Authorization: Bearer $MEMORY_TOKEN" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{"matchId":"MEMORY-TEST-001"}' \
  "$API_URL/api/games/memory/results"
```

El identificador debe corresponder a una partida en la que haya participado el usuario autenticado.

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
- `401 Unauthorized`: token ausente o inválido.
- `403 Forbidden`: el usuario no participó en la partida.
- `404 Not Found`: no existe una sesión ni un resultado almacenado para el `matchId`.

> Importante: un `200 OK` indica que el resultado fue guardado o que ya existía. Si el guardado automático falla durante la finalización de la partida, el backend dispone de un mecanismo de reintento y emite `ResultSaveFailed` mientras el resultado no se confirme.

### 4.5. Consultar el resultado de una partida

- **Método:** `GET`
- **Ruta:** `/api/games/memory/results/{matchId}`
- **Autenticación:** Requerida mediante `MemoryPlay`.
- **Cuerpo de la solicitud:** no aplica.

Este endpoint devuelve el resultado almacenado. Solo los participantes de la partida pueden consultarlo.

#### Parámetros

| Parámetro | Ubicación | Tipo   | Obligatorio | Descripción                  |
| --------- | --------- | ------ | ----------- | ---------------------------- |
| `matchId` | Ruta      | string | Sí          | Identificador de la partida. |

#### Encabezados

| Encabezado      | Valor              | Obligatorio |
| --------------- | ------------------ | ----------- |
| `Authorization` | `Bearer <token>`   | Sí          |
| `Accept`        | `application/json` | Recomendado |

#### Ejemplo curl

```bash
curl -i \
  -H "Authorization: Bearer $MEMORY_TOKEN" \
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
- `401 Unauthorized`: token ausente o inválido.
- `403 Forbidden`: el usuario autenticado no participó en la partida.
- `404 Not Found`: no existe un resultado almacenado para el `matchId`.

### 4.6. Ejemplos de respuestas de error

Los siguientes ejemplos muestran cómo se ve un error al ejecutar los `curl` anteriores.

**401 Unauthorized** (token ausente, inválido o vencido):

```bash
curl -i "$API_URL/api/games/memory/players/$USER_ID/history"
```

```
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Bearer
```

La respuesta no incluye cuerpo.

**403 Forbidden** (consultar el historial de otro usuario):

```bash
curl -i \
  -H "Authorization: Bearer $MEMORY_TOKEN" \
  "$API_URL/api/games/memory/players/auth0|otro-usuario/history"
```

```
HTTP/1.1 403 Forbidden
```

> Nota: el formato exacto del cuerpo de los errores `400` y `404` depende del controlador (puede ser vacío o un objeto `ProblemDetails` de ASP.NET Core). Confirmarlo ejecutando la llamada contra la API local.

## 5. Contrato de integración con el Shell

### 5.1. Responsabilidades

El Shell es responsable de la autenticación del usuario, la selección o creación de la sala de Matchmaking y la carga del microfrontend.

Memory es responsable de ejecutar la partida, validar las jugadas, sincronizar el estado de ambos jugadores y guardar el resultado.

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

El contrato `GameContext` incluye los siguientes datos:

| Campo                         | Tipo              | Descripción                                                       |
| ----------------------------- | ----------------- | ----------------------------------------------------------------- |
| `matchId`                     | string            | Identificador de la sala creada por Matchmaking.                  |
| `gameType`                    | string            | Debe ser `"memory"`.                                              |
| `currentUser.id`              | string            | Identificador del usuario; debe coincidir con el `sub` del token. |
| `currentUser.displayName`     | string            | Nombre que se muestra en la partida.                              |
| `getAccessToken()`            | función asíncrona | Obtiene el token del usuario para la audiencia de Memory.         |
| `getMatchmakingAccessToken()` | función asíncrona | Obtiene el token delegado del usuario para consultar Matchmaking. |

Ejemplo conceptual del contexto:

```ts
const context = {
  matchId: "MEMORY-TEST-001",
  gameType: "memory",
  currentUser: {
    id: "auth0|usuario-de-prueba",
    displayName: "Jugador Uno",
  },
  getAccessToken: async () => {
    // El Shell obtiene un token válido para la audiencia de Memory.
  },
  getMatchmakingAccessToken: async () => {
    // El Shell obtiene un token válido para la audiencia de Matchmaking.
  },
};
```

Este fragmento ilustra la estructura del contrato. No es una implementación completa de las funciones de autenticación.

`initialize()` valida que el contexto contenga el tipo de juego, el identificador de partida, los datos del usuario y ambas funciones de tokens. Si faltan datos obligatorios, la inicialización falla.

### 5.4. Validación de la sala

Al ejecutar `start()`, el microfrontend invoca el método `JoinMatch` del hub competitivo.

El backend utiliza el token de usuario de Memory para identificar al jugador y el token delegado de Matchmaking para consultar la sala. La API valida que:

- La sala exista.
- El tipo de juego sea `memory`.
- La sala esté iniciada y tenga dos participantes.
- El usuario autenticado forme parte de la sala.
- Los participantes correspondan al grupo de jugadores con el que se creó la sesión.

El nombre mostrado se obtiene del registro de participantes de Matchmaking. El identificador enviado por el Shell no sustituye la validación de identidad del JWT.

## 6. Contrato de SignalR

### 6.1. Dirección y autenticación

- **Hub competitivo:** `http://localhost:5095/hubs/memory`

La conexión se realiza mediante SignalR. Para la conexión competitiva se requiere un token de usuario válido de Auth0 para Memory.

En el transporte WebSocket, SignalR transmite el token mediante el parámetro `access_token` de la URL porque el navegador no puede agregar libremente el encabezado `Authorization` al establecimiento de WebSocket.

Los tokens deben tratarse como secretos y nunca registrarse ni compartirse en capturas o URLs públicas.

#### Ejemplo de conexión (cliente `@microsoft/signalr`)

```ts
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5095/hubs/memory", {
    accessTokenFactory: () => context.getAccessToken(),
  })
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

const matchmakingToken = await context.getMatchmakingAccessToken();
await connection.invoke("JoinMatch", context.matchId, matchmakingToken);
```

El fragmento es ilustrativo: el token se obtiene del `GameContext` entregado por el Shell y nunca se escribe en el código.

### 6.2. Métodos del cliente

#### `JoinMatch(matchId, matchmakingAccessToken)`

Une al jugador autenticado a una partida existente.

| Parámetro                | Tipo   | Descripción                                                   |
| ------------------------ | ------ | ------------------------------------------------------------- |
| `matchId`                | string | Identificador de la sala de Matchmaking.                      |
| `matchmakingAccessToken` | string | Token delegado utilizado por el backend para validar la sala. |

Este método se utiliza tanto para incorporarse a la partida como para recuperar el estado después de una reconexión. Si la sesión ya existe, el jugador recibe un `StateSnapshot` con el estado actual.

#### `FlipCard(matchId, cardId)`

Solicita voltear una carta.

| Parámetro | Tipo   | Descripción                                                 |
| --------- | ------ | ----------------------------------------------------------- |
| `matchId` | string | Identificador de la partida a la que se unió esta conexión. |
| `cardId`  | int    | Identificador de la carta.                                  |

El servidor comprueba que la conexión se haya unido a esa partida, que la previsualización haya terminado y que la jugada sea válida para el turno actual.

El cliente no envía un `userId` para decidir quién realiza la jugada. La identidad procede del token autenticado.

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
| `MATCH_ROSTER_CHANGED` | Los participantes actuales no coinciden con los registrados al crear la sesión. |

También pueden rechazarse jugadas por turno incorrecto, carta inexistente o carta ya volteada. En estos casos el servidor comunica el error de la operación mediante una excepción de SignalR.

### 6.6. Flujo de una partida competitiva

1. El usuario inicia sesión en el Shell.
2. El Shell obtiene o crea una sala de Matchmaking.
3. El Shell carga el módulo `memoryGame`.
4. El Shell entrega el `GameContext` a `initialize()`.
5. `start()` establece la conexión con `/hubs/memory` y ejecuta `JoinMatch`.
6. El backend valida la sala y los participantes mediante Matchmaking.
7. Cuando los dos jugadores están conectados, el servidor envía `GameReady`.
8. Al finalizar la previsualización, envía `PreviewFinished` y comienza el turno.
9. Los jugadores invocan `FlipCard`; el servidor valida cada jugada y comunica el resultado mediante `CardFlipped`.
10. Cuando la partida termina, el backend guarda el resultado e intenta completar el flujo de finalización de Matchmaking.
11. El hub emite `ResultSaved` si confirma el guardado, o `ResultSaveFailed` si el intento falla.
12. Los jugadores pueden consultar el resultado guardado mediante `GET /api/games/memory/results/{matchId}`.

El cliente debe considerar al servidor como la fuente autoritativa del estado, del turno, del resultado y de la confirmación del guardado.

## 7. Integración con Matchmaking

Memory consulta el servicio de Matchmaking para validar la sala. La creación de salas y el flujo de búsqueda de partidas pertenecen al Shell y a Matchmaking, no a la API REST de Memory.

Según el contrato de integración, el backend de Memory utiliza estas operaciones externas de Matchmaking:

| Método | Ruta externa               | Finalidad                                                             |
| ------ | -------------------------- | --------------------------------------------------------------------- |
| `GET`  | `/api/matches/{id}`        | Consultar y validar la sala, su tipo, estado y participantes.         |
| `POST` | `/api/matches/{id}/finish` | Solicitar que Matchmaking finalice la sala cuando termina la partida. |

Estas son llamadas del backend de Memory hacia Matchmaking; no son endpoints REST implementados por este repositorio.

La llamada de finalización utiliza credenciales de servicio y el permiso correspondiente. El backend mantiene un mecanismo de reintento para el aviso de finalización, por lo que el cliente no debe intentar cerrar Matchmaking directamente.

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

- No incluir tokens de Auth0, secretos de clientes, contraseñas ni credenciales reales en los ejemplos o en el repositorio.
- No confiar en un `userId` enviado por el navegador para autenticar al jugador.
- Utilizar la audiencia correcta para cada token: Memory para la API y el hub del juego; Matchmaking para la consulta delegada de la sala.
- No permitir que un usuario consulte el historial, las estadísticas o los resultados de otros jugadores.
- No considerar una partida guardada hasta recibir la confirmación correspondiente.
- No exponer `/hubs/memory-local` fuera del entorno de desarrollo.
- Configurar HTTPS, CORS y las direcciones reales de los servicios antes de desplegar.
- Mantener los secretos de servicio en mecanismos seguros de configuración y nunca en el código del microfrontend.

## 10. Referencias de implementación

La documentación se basa en los siguientes archivos del repositorio:

- `src/BattleHub.Memory.Api/Program.cs`: configuración de la API, OpenAPI, CORS y hubs.
- `src/BattleHub.Memory.Api/Auth/MemoryAuth.cs`: validación JWT y políticas de autorización.
- `src/BattleHub.Memory.Api/Controllers/MemoryController.cs`: historial y estadísticas.
- `src/BattleHub.Memory.Api/Controllers/MemoryGameResultsController.cs`: guardado y consulta de resultados.
- `src/BattleHub.Memory.Api/Hubs/MemoryHub.cs`: comunicación competitiva mediante SignalR.
- `src/BattleHub.Memory.Api/Hubs/MemoryLocalHub.cs`: juego local exclusivo para desarrollo.
- `src/memory-microfrontend/memory-game/src/game-module.ts`: ciclo de vida e integración con el Shell.
- `src/memory-microfrontend/memory-game/src/game-contracts.ts`: contrato de integración del microfrontend.
- `docs/integracion-shell-auth0.md`: detalles adicionales de integración con Auth0 y Matchmaking.
- `src/memory-microfrontend/memory-game/README.md`: configuración y ejecución del microfrontend.

## 11. Verificación de los ejemplos

Los ejemplos `curl` deben probarse con la API en funcionamiento, una base de datos configurada y un token válido de prueba. Lista de verificación:

- [ ] `GET /health` responde `200 OK`.
- [ ] `GET .../history` con el usuario propio responde `200 OK`.
- [ ] `GET .../history` con un `userId` ajeno responde `403 Forbidden`.
- [ ] `GET .../stats` con el usuario propio responde `200 OK`.
- [ ] `POST /api/games/memory/results` con una partida terminada responde `200 OK`.
- [ ] `GET .../results/{matchId}` con un participante responde `200 OK`; con un no participante, `403 Forbidden`.
- [ ] SignalR: conexión, incorporación de dos jugadores, sincronización de cartas, errores de turno y confirmación del guardado.

Los ejemplos documentan el contrato esperado. Marcar cada casilla al ejecutarlo contra un entorno real.

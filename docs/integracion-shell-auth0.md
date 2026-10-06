# Integración de Memory con Shell, Auth0 y Matchmaking

Base: paulapgarciazzz/battlehub-game-memory, main, c2ac891. Actualizada desde GitHub el 2026-10-04; no había commits posteriores en main. Estos ajustes son locales para revisión del Equipo 6, sin commits ni push.

## Resultado de la implementación

- API y hub validan JWT RS256 de jugadores con audiencia Memory, issuer y vigencia. El backend obtiene la identidad de sub y rechaza clientes M2M como jugadores.
- JoinMatch(matchId, matchmakingAccessToken) consulta el detalle de la sala en Matchmaking con el token delegado. Comprueba el juego memory, estado Started, exactamente dos participantes distintos y membresía del sub autenticado. Los nombres salen del roster de Matchmaking.
- FlipCard(matchId, cardId) ya no recibe userId del navegador. Requiere haber entrado a esa sala en la conexión actual y bloquea jugadas durante la previsualización.
- Se conservan ocho parejas, cinco segundos de previsualización, turnos de diez segundos, punto por pareja y turno extra al acertar.
- La reconexión vuelve a validar la sala y recupera cartas visibles, parejas, marcador, turno y fecha UTC de vencimiento desde StateSnapshot. Puede restaurar una sesión terminada existente si Matchmaking ya está Finished. Nunca crea una partida nueva desde una sala Finished.
- Resultado y FinishNotifications se guardan en un mismo SaveChanges/transaction de EF. El cliente confirma el guardado, no fabrica resultados. POST results queda como reintento del guardado autoritativo para participantes de una sesión ya terminada.
- La cola persistente avisa a POST /api/matches/{id}/finish con client_credentials y matches.finish. Cachea el token M2M. Cada despacho reintenta hasta tres veces fallos de red, 408, 429 y 5xx; otro despacho ocurre treinta segundos después. 401/403/404/409 y otros rechazos permanentes bloquean el aviso para revisión. Un 2xx confirma el cierre.
- Si SQL falla al finalizar, un worker reintenta el guardado mientras la sesión siga en memoria. La pantalla no afirma que el resultado está guardado hasta confirmarlo.
- Historial y estadísticas requieren el mismo sub de la ruta; resultados solo se leen por participantes. CORS permite Shell 4000 y remote 4003.
- dispose elimina listeners y temporizadores, desconecta y elimina proveedores. Pause bloquea solo esta vista; el servidor y el rival continúan.

## Configuración pública

Domain: dev-jaii1peslxnejq0y.us.auth0.com
Issuer: https://dev-jaii1peslxnejq0y.us.auth0.com/
Audience jugadores: https://api.battlehub.local/memory
Audience callback M2M: https://api.battlehub.local/profile
Client ID Memory Service: lfcBlgOCs9N0w6F6AWl4FqOs4xF9AQiN
Game type: memory
API: http://localhost:5095
Hub: http://localhost:5095/hubs/memory
Remote: http://localhost:4003/remoteEntry.js
Module Federation: memoryGame, ./GameModule

El Client ID se copió del texto facilitado por el administrador. No se realizó una solicitud con ese cliente contra Auth0 real: confirmar que el ID coincida exactamente y que API Access > Profile > Client Access conceda solamente matches.finish. La API de jugadores debe usar RS256 y autorizar acceso delegado para BattleHub Shell.

El Client Secret no está en el código, la configuración pública ni el ZIP. Su responsable debe guardarlo localmente o en las variables privadas del servidor. El frontend no usa credenciales M2M.

## Configurar credenciales y arrancar sin Docker

Requisitos: .NET 10, Node >=24.11 <25 y SQL Server LocalDB o SQL Server. Este juego usa SQL Server, no MySQL.
Desde la raíz del repo:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Set-MatchmakingCredentials.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Integration.ps1
```

El primer script solicita el secreto de forma oculta y guarda las credenciales en dotnet user-secrets. El segundo aplica migraciones, incluidas las nuevas de FinishNotifications, y arranca en el puerto 5095. No borra bases ni resultados. La conexión por defecto es LocalDB/BattleHubMemoryDb. Si usan otro SQL Server:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Integration.ps1 -ConnectionString 'Server=localhost;Database=BattleHubMemoryDb;Integrated Security=true;TrustServerCertificate=true'
```

En otra terminal desde la raíz:

```powershell
cd src\memory-microfrontend\memory-game
npm.cmd ci
npm.cmd start
```

El arnés de 4003 conserva su vista de desarrollo, pero ya no permite autenticar jugadores escribiendo un userId. Para jugar, abrir una sala real desde Shell 4000. config/environment.json debe apuntar a 5095. Para despliegue configurar environment.production.json con URLs reales; la API y el frontend verifican esa configuración. Usar HTTPS y ajustar CORS y los remotes del Shell al desplegar.

## Contexto del Shell

GameContext conserva matchId, gameType y currentUser. Además requiere las dos funciones opcionales del acuerdo local:
- getAccessToken(): token de jugador para audiencia Memory.
- getMatchmakingAccessToken(): token de usuario para audiencia Profile/Matchmaking, que el backend reenvía únicamente a su BaseUrl configurada.

El Shell local ya registra Memory en 4003 y proporciona ambas funciones. currentUser.id coincide con sub, pero no sustituye la validación del JWT en el backend. Las funciones de tokens y el roster extendido siguen siendo acuerdos de integración que deben coordinarse con el profesor. ADR-007 no está aprobado por esta entrega.

## Equipo 2 — Matchmaking

Agregar en GameServices:Clients de su appsettings.json, conservando Typing, Trivia y cualquier otro cliente:

```json
"lfcBlgOCs9N0w6F6AWl4FqOs4xF9AQiN": "memory"
```

O establecer en la terminal que arranca Matchmaking:

```powershell
$env:GameServices__Clients__lfcBlgOCs9N0w6F6AWl4FqOs4xF9AQiN='memory'
```

Reiniciar Matchmaking. Nunca necesita el secreto de Memory.
GET /api/matches/{id} debe devolver id, gameType, status y participants [{userId, displayName}], y aceptar token de usuario de audiencia Profile. POST /api/matches/{id}/finish debe exigir M2M y matches.finish, comprobar que ese cliente corresponde a memory y ser idempotente (204 para cierre confirmado).
El Shell crea Memory con dos plazas y el backend del juego exige dos participantes. Es recomendable que Matchmaking también limite memory a dos plazas al crear/iniciar la sala. Este ZIP no modifica el repo del Equipo 2.

## Equipo 1 — Profile

Confirmar memory habilitado, games.memory.play para ambos jugadores y matches.create para el anfitrión en los datos reales de Profile. No son credenciales M2M ni roles que esta entrega cree en Auth0. Compartir tenant y audiencia con Matchmaking según su configuración local; ADR-007 sigue siendo propuesta.

## Prueba conjunta pendiente

1. Levantar Profile, Matchmaking con su MongoDB, backend/remote Memory y Shell.
2. Iniciar sesión con dos cuentas, en perfiles o navegadores separados.
3. Crear sala memory de dos plazas, entrar ambos e iniciar.
4. Abrir Memory desde Salas. Autorizar audiencia Memory con la misma cuenta si lo pide el Shell.
5. Comprobar previsualización, turno, primera/segunda carta, error, pareja, vencimiento y empate/ganador.
6. Reconectar durante la partida y comprobar que no se repita la previsualización ni se pierda el marcador.
7. Completar las ocho parejas: resultado e historial guardados, Matchmaking Finished y FinishNotifications.Delivered=1.
8. Intentos anónimos, usuario ajeno y audiencia incorrecta deben rechazarse.

Si una notificación queda Blocked, revisar permisos, Client ID, mapa del Equipo 2 y respuesta de finish antes de reactivarla. En la base de Memory:

```sql
SELECT MatchId, Delivered, Blocked, Attempts, NextAttemptAt FROM FinishNotifications;
UPDATE FinishNotifications SET Blocked=0, NextAttemptAt=SYSDATETIMEOFFSET()
WHERE MatchId='PARTIDA_REVISADA' AND Delivered=0;
```

Nunca reactivar todas las filas sin comprobar la causa.

## Pruebas y límites

Se ejecutaron las pruebas originales del dominio, las del frontend y las nuevas de API/SignalR/SQL/HTTP de Matchmaking. La prueba completa usa dos clientes SignalR reales, JWT RS256 con clave temporal y una base aislada de SQL LocalDB; el gateway de Matchmaking se sustituye por uno controlado. Sus solicitudes HTTP/M2M/reintentos se prueban por separado. No demuestra una partida con Auth0 y Matchmaking reales.

Las sesiones activas siguen en memoria: un reinicio pierde partidas activas y resultados que aún no pudieron guardarse. Los resultados confirmados y avisos SQL pendientes sobreviven. Usar una sola instancia de Memory; compartir estado y escalar requiere trabajo adicional. La integración local no equivale a despliegue para el profesor.

npm ci reportó 42 hallazgos en las dependencias existentes (3 moderados, 39 altos). No se aplicaron actualizaciones masivas que alteren Aurelia o las versiones compartidas.

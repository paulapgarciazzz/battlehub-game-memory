# Reglas del juego — Memoria (Equipo 6)

Este documento es la fuente única de verdad de las reglas del juego. Backend, Frontend y Base de Datos deben implementar exactamente estos valores — no son negociables por área, cualquier cambio se discute y se actualiza aquí primero.

> **Actualizado** tras feedback del profesor: se simplificó el juego. Se eliminó el sistema de vidas y, por ahora, el tablero tiene un solo tamaño fijo (sin selección de dificultad). Se puede volver a ampliar más adelante si el equipo lo decide.

## Objetivo

Encontrar más parejas de cartas que el rival antes de que se acaben las cartas del tablero.

## Configuración de la partida

### Tablero

Tamaño fijo por ahora: **16 cartas (8 parejas)**. No hay selección de dificultad en esta versión.

### Tiempo límite por turno

Fijo por ahora: **10 segundos**. No hay selección de opciones en esta versión.

## Mecánica de juego

1. Al iniciar, **todas las cartas se muestran boca arriba durante 5 segundos** (previsualización), luego se ocultan.
2. En su turno, el jugador voltea **dos cartas**, una a la vez.
   - Solo el jugador con el turno actual puede voltear — un intento fuera de turno se rechaza.
3. **Si coinciden:** quedan boca arriba permanentemente, el jugador suma 1 pareja, y **juega de nuevo** (turno extra).
4. **Si no coinciden:** ambas cartas vuelven a ocultarse, y el turno pasa al rival.
5. **Si el jugador no actúa dentro del tiempo límite de turno:** cualquier carta que hubiera quedado volteada se oculta de nuevo, y el turno pasa al rival automáticamente.
6. La partida termina cuando **todas las parejas del tablero han sido encontradas**.

## Determinar el ganador

Gana quien encontró **más parejas**. Si ambos jugadores terminan con la misma cantidad de parejas, es **empate**.

## Pendiente de definir por el equipo

- **Modo Classic vs Battle**: no se ha definido si el juego tendrá un solo modo o dos modos con reglas distintas. Mientras no se decida, todo lo anterior aplica como único modo del juego.
- **Selección de dificultad**: descartada por ahora para simplificar el MVP. Puede reincorporarse más adelante si el tiempo lo permite.
- **Selección de tiempo límite de turno**: descartada por la misma razón — queda fijo en 10 segundos por ahora.

## Qué debe saber cada área

- **Backend/API**: implementa estas reglas a través de la clase `GameSession` en `BattleHub.Memory.Domain` — no reinventar la lógica, ya existe.
- **Frontend**: debe mostrar el tablero de 16 cartas fijo, la fase de previsualización, el turno actual, y la cuenta regresiva de 10 segundos por turno. No debe incluir pantalla de selección de dificultad, vidas, ni tiempo límite — no hay opciones que elegir en esta versión.
- **Base de datos**: al guardar el resultado de una partida, persistir como mínimo: parejas encontradas por cada jugador, y quién ganó (o si fue empate). No es necesario un campo de vidas ni de dificultad por ahora.

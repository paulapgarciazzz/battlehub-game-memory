# Reglas del juego — Memoria (Equipo 6)

Este documento es la fuente única de verdad de las reglas del juego. Backend, Frontend y Base de Datos deben implementar exactamente estos valores — no son negociables por área, cualquier cambio se discute y se actualiza aquí primero.

## Objetivo

Encontrar más parejas de cartas que el rival antes de que se acaben las cartas del tablero.

## Configuración de la partida

El jugador que crea la partida elige 3 opciones, de forma independiente entre sí:

### Dificultad (tamaño del tablero)

| Opción | Cartas totales | Parejas |
|---|---|---|
| Fácil | 16 | 8 |
| Medio | 24 | 12 |
| Difícil | 36 | 18 |

### Vidas iniciales (independiente de la dificultad)

| Opción | Vidas |
|---|---|
| Pocas | 3 |
| Normal | 5 |
| Muchas | 10 |

### Tiempo límite por turno

| Opción | Segundos |
|---|---|
| Corto | 10 |
| Medio | 15 |
| Largo | 30 |

## Mecánica de juego

1. Al iniciar, **todas las cartas se muestran boca arriba durante 5 segundos** (previsualización), luego se ocultan.
2. En su turno, el jugador voltea **dos cartas**, una a la vez.
   - Solo el jugador con el turno actual puede voltear — un intento fuera de turno se rechaza.
3. **Si coinciden:** quedan boca arriba permanentemente, el jugador suma 1 pareja, y **juega de nuevo** (turno extra).
4. **Si no coinciden:** ambas cartas vuelven a ocultarse, el jugador **pierde 1 vida**, y el turno pasa al rival.
5. **Si el jugador no actúa dentro del tiempo límite de turno:** cualquier carta que hubiera quedado volteada se oculta de nuevo, y el turno pasa al rival automáticamente — **sin restar vida** (no fue un fallo, fue falta de acción).
6. Llegar a 0 vidas **no termina la partida** — el juego continúa igual.
7. La partida termina cuando **todas las parejas del tablero han sido encontradas**.

## Determinar el ganador

1. Gana quien encontró **más parejas**.
2. Si hay empate en parejas, desempata quien tenga **más vidas restantes**.
3. Si también empatan en vidas, la partida queda en empate.

## Pendiente de definir por el equipo

- **Modo Classic vs Battle**: no se ha definido si el juego tendrá un solo modo o dos modos con reglas distintas. Mientras no se decida, todo lo anterior aplica como único modo del juego.

## Qué debe saber cada área

- **Backend/API**: implementa estas reglas a través de la clase `GameSession` en `BattleHub.Memory.Domain` — no reinventar la lógica, ya existe.
- **Frontend**: debe mostrar la pantalla de selección con exactamente estas 3 dificultades, 3 opciones de vidas y 3 de tiempo límite (9 combinaciones posibles), y reflejar visualmente el turno, la previsualización, la cuenta regresiva del tiempo límite, y las vidas restantes.
- **Base de datos**: al guardar el resultado de una partida, persistir como mínimo: dificultad elegida, parejas encontradas por cada jugador, vidas restantes de cada jugador, y quién ganó (o si fue empate).

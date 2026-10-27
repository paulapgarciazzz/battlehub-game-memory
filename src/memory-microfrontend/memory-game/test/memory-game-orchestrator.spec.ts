import { IEventAggregator } from 'aurelia';
import { MemoryGameOrchestrator } from '../src/services/memory-game-orchestrator';
import { MemoryHubEvents } from '../src/services/memory-hub.service';
import { MemoryGameState } from '../src/state/memory-game-state';
import { createFakeHub, createFakeHttp, createTestContainer, type FakeHub } from './fakes';

function setup() {
  const hub = createFakeHub();
  const container = createTestContainer(hub, createFakeHttp());
  const orchestrator = container.get(MemoryGameOrchestrator);
  const state = container.get(MemoryGameState);
  const ea = container.get(IEventAggregator);
  return { hub, orchestrator, state, ea };
}

// Deja la partida en juego con Ana (u1) contra Beto (u2), turno de Ana.
function startPlaying(state: MemoryGameState, myUserId = 'u1') {
  state.myUserId = myUserId;
  state.matchId = 'm1';
  state.phase = 'playing';
  state.players = [
    { userId: 'u1', displayName: 'Ana', matchedPairs: 0 },
    { userId: 'u2', displayName: 'Beto', matchedPairs: 0 }
  ];
  state.currentPlayerId = 'u1';
}

describe('MemoryGameOrchestrator', () => {
  afterEach(() => jest.useRealTimers());

  it('al fallar una pareja toma el turno del servidor de inmediato y oculta las cartas después', () => {
    jest.useFakeTimers();
    const { state, ea } = setup();
    startPlaying(state);

    ea.publish(MemoryHubEvents.CardFlipped, {
      userId: 'u1',
      cards: [{ id: 0, value: 'A' }, { id: 1, value: 'H' }],
      isMatch: false,
      currentPlayerId: 'u2'
    });

    // El turno cambia en el mismo evento, no 900 ms después.
    expect(state.currentPlayerId).toBe('u2');
    expect(state.isMyTurn).toBe(false);
    expect(state.cards[0].faceUp).toBe(true);
    expect(state.cards[1].faceUp).toBe(true);

    jest.advanceTimersByTime(900);

    expect(state.cards[0].faceUp).toBe(false);
    expect(state.cards[1].faceUp).toBe(false);
  });

  it('suma la pareja al jugador que acertó', () => {
    const { state, ea } = setup();
    startPlaying(state);

    ea.publish(MemoryHubEvents.CardFlipped, {
      userId: 'u1',
      cards: [{ id: 2, value: 'B' }, { id: 5, value: 'B' }],
      isMatch: true,
      currentPlayerId: 'u1'
    });

    expect(state.players.find(p => p.userId === 'u1')?.matchedPairs).toBe(1);
    expect(state.cards[2].matched).toBe(true);
    expect(state.cards[5].matched).toBe(true);
  });

  it('no envía jugadas cuando no es mi turno', async () => {
    const { hub, orchestrator, state } = setup();
    startPlaying(state, 'u2');

    await orchestrator.flipCard(0);

    expect(hub.flipCard).not.toHaveBeenCalled();
  });

  it('no envía jugadas cuando el juego está en pausa', async () => {
    const { hub, orchestrator, state } = setup();
    startPlaying(state);
    state.paused = true;

    await orchestrator.flipCard(0);

    expect(hub.flipCard).not.toHaveBeenCalled();
  });

  it('envía la jugada al hub cuando es mi turno', async () => {
    const { hub, orchestrator, state } = setup();
    startPlaying(state);

    await orchestrator.flipCard(3);

    expect((hub as FakeHub).flipCard).toHaveBeenCalledWith('m1', 3);
  });

  it('resuelve una partida de práctica y declara al ganador', async () => {
    jest.useFakeTimers();
    const { orchestrator, state, hub } = setup();

    orchestrator.startPractice(' Ana ', 'Beto');
    expect(state.phase).toBe('preview');
    expect(state.players.map(player => player.displayName)).toEqual(['Ana', 'Beto']);
    expect(state.cards.every(card => card.faceUp)).toBe(true);

    jest.advanceTimersByTime(5_000);
    expect(state.phase).toBe('playing');
    expect(state.cards.every(card => !card.faceUp)).toBe(true);

    state.players[0].matchedPairs = 7;
    const first = state.cards[0];
    const second = state.cards.find(card =>
      card.id !== first.id && card.value === first.value
    )!;

    await orchestrator.flipCard(first.id);
    await orchestrator.flipCard(second.id);

    expect(state.phase).toBe('finished');
    expect(state.players[0].matchedPairs).toBe(8);
    expect(state.winnerUserId).toBe('local-1');
    expect(state.isDraw).toBe(false);
    expect(hub.flipCard).not.toHaveBeenCalled();
  });

  it('cambia el turno después de una pareja incorrecta en práctica', async () => {
    jest.useFakeTimers();
    const { orchestrator, state } = setup();

    orchestrator.startPractice('Ana', 'Beto');
    jest.advanceTimersByTime(5_000);

    const first = state.cards[0];
    const second = state.cards.find(card => card.value !== first.value)!;
    await orchestrator.flipCard(first.id);
    await orchestrator.flipCard(second.id);

    expect(state.practiceResolving).toBe(true);
    expect(state.currentPlayerId).toBe('local-1');

    jest.advanceTimersByTime(900);

    expect(state.cards[first.id].faceUp).toBe(false);
    expect(state.cards[second.id].faceUp).toBe(false);
    expect(state.currentPlayerId).toBe('local-2');
    expect(state.practiceResolving).toBe(false);
  });

  it('permite jugar solo y termina al encontrar todas las parejas', async () => {
    jest.useFakeTimers();
    const { orchestrator, state, hub } = setup();

    orchestrator.startSolo(' ');
    expect(state.myDisplayName).toBe('Jugador');
    expect(state.phase).toBe('playing');
    expect(state.cards.every(card => !card.faceUp)).toBe(true);

    const first = state.cards[0];
    const second = state.cards.find(card =>
      card.id !== first.id && card.value === first.value
    )!;
    state.players[0].matchedPairs = 7;

    await orchestrator.flipCard(first.id);
    await orchestrator.flipCard(second.id);

    expect(state.phase).toBe('finished');
    expect(state.players[0].matchedPairs).toBe(8);
    expect(state.cards[first.id].matched).toBe(true);
    expect(state.cards[second.id].matched).toBe(true);
    expect(hub.flipCard).not.toHaveBeenCalled();
  });

  it('aplica eventos de jugadores, inicio de partida, timeout y snapshot', async () => {
    const { orchestrator, state, ea } = setup();
    startPlaying(state);

    ea.publish(MemoryHubEvents.PlayerJoined, {
      userId: 'u2',
      displayName: 'Beto'
    });
    ea.publish(MemoryHubEvents.PlayerJoined, {
      userId: 'u3',
      displayName: 'Caro'
    });
    expect(state.players.map(player => player.userId)).toEqual(['u1', 'u2', 'u3']);

    ea.publish(MemoryHubEvents.GameReady, {
      cards: [{id: 0, value: 'A'}, {id: 1, value: 'A'}],
      players: [{userId: 'u1', displayName: 'Ana'}, {userId: 'u2', displayName: 'Beto'}]
    });
    expect(state.phase).toBe('preview');
    expect(state.cards[0].faceUp).toBe(true);

    ea.publish(MemoryHubEvents.PreviewFinished, {
      currentPlayerId: 'u1',
      turnDeadline: '2026-10-04T12:00:10Z'
    });
    expect(state.phase).toBe('playing');
    expect(state.turnDeadline).toBe(Date.parse('2026-10-04T12:00:10Z'));

    state.cards[0].matched = true;
    state.cards[0].faceUp = true;
    state.cards[1].faceUp = true;
    ea.publish(MemoryHubEvents.TurnTimeout, {
      currentPlayerId: 'u2',
      turnDeadline: null
    });
    expect(state.cards[0].faceUp).toBe(true);
    expect(state.cards[1].faceUp).toBe(false);
    expect(state.currentPlayerId).toBe('u2');

    ea.publish(MemoryHubEvents.StateSnapshot, {
      matchId: 'stale',
      phase: 'finished',
      startedAt: '2026-10-04T12:00:00Z',
      currentPlayerId: 'u1',
      turnDeadline: null,
      resultSaved: false,
      players: [],
      cards: [],
      isDraw: false,
      winnerUserId: null
    });
    expect(state.phase).toBe('playing');

    ea.publish(MemoryHubEvents.StateSnapshot, {
      matchId: 'm1',
      phase: 'finished',
      startedAt: '2026-10-04T12:00:00Z',
      currentPlayerId: 'u1',
      turnDeadline: null,
      resultSaved: false,
      players: [{userId: 'u1', displayName: 'Ana', matchedPairs: 8}],
      cards: [{id: 0, value: 'A', matched: true, faceUp: true}],
      isDraw: false,
      winnerUserId: 'u1'
    });
    expect(state.phase).toBe('finished');
    expect(state.resultSaveError).toBe('El servidor reintentará guardar el resultado.');
  });

  it('reintenta guardar resultados y conserva el error de la API', async () => {
    const http = createFakeHttp();
    const container = createTestContainer(createFakeHub(), http);
    const retryOrchestrator = container.get(MemoryGameOrchestrator);
    const retryState = container.get(MemoryGameState);
    retryState.matchId = 'retry';
    retryState.matchStartedAt = new Date('2026-10-04T12:00:00Z');
    http.saveResult.mockRejectedValueOnce(new Error('API no disponible'));

    await retryOrchestrator.retrySaveResult();
    expect(retryState.resultSaveError).toBe('API no disponible');

    await retryOrchestrator.retrySaveResult();
    expect(http.saveResult).toHaveBeenCalledTimes(2);
    expect(retryState.resultSaveError).toBeNull();

    retryState.practiceMode = true;
    await retryOrchestrator.retrySaveResult();
    expect(http.saveResult).toHaveBeenCalledTimes(2);
  });
});

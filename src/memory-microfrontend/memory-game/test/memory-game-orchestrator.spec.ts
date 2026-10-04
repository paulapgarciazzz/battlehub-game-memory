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

  it('envía la jugada al hub cuando es mi turno', async () => {
    const { hub, orchestrator, state } = setup();
    startPlaying(state);

    await orchestrator.flipCard(3);

    expect((hub as FakeHub).flipCard).toHaveBeenCalledWith('m1', 'u1', 3);
  });
});

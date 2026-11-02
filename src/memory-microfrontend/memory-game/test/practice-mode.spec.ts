import {IEventAggregator} from 'aurelia';
import {GameModule} from '../src/game-module';
import {GAME_FINISHED_EVENT, type GameFinishedDetail, type PracticeContext} from '../src/game-contracts';
import {MemoryGameOrchestrator} from '../src/services/memory-game-orchestrator';
import {MemoryHubEvents} from '../src/services/memory-hub.service';
import {MemoryGameState} from '../src/state/memory-game-state';
import {createFakeHub, createFakeHttp, createTestContainer} from './fakes';

const practice = (): PracticeContext => ({
  mode: 'practice', gameType: 'memory', currentUser: {id: 'u1', displayName: 'Ana'}, events: new EventTarget()
});

function setup() {
  const hub = createFakeHub(); const http = createFakeHttp();
  const container = createTestContainer(hub, http);
  return {
    hub, http, game: container.invoke(GameModule),
    state: container.get(MemoryGameState), orchestrator: container.get(MemoryGameOrchestrator),
    ea: container.get(IEventAggregator)
  };
}

/** Juega las 8 parejas del modo solo, cada una de una vez. */
async function finishSolo(orchestrator: MemoryGameOrchestrator, state: MemoryGameState): Promise<void> {
  for (const value of [...new Set(state.cards.map(card => card.value))]) {
    const [first, second] = state.cards.filter(card => card.value === value);
    await orchestrator.flipCard(first.id);
    await orchestrator.flipCard(second.id);
  }
}

describe('Modo práctica dentro del Shell', () => {
  it('acepta la práctica sin matchId y juega el modo solo sin conectarse al servidor', async () => {
    const {hub, game, state} = setup();

    await game.initialize(practice());
    await game.start();

    expect(hub.connect).not.toHaveBeenCalled();
    expect(hub.joinMatch).not.toHaveBeenCalled();
    expect(state.soloMode).toBe(true);
    expect(state.embedded).toBe(true);
    expect(state.phase).toBe('playing');
    expect(state.players).toEqual([{userId: 'solo-1', displayName: 'Ana', matchedPairs: 0}]);
  });

  it('una partida normal sigue necesitando matchId y no acepta otros modos', async () => {
    const {game} = setup();
    const user = {id: 'u1', displayName: 'Ana'};

    await expect(game.initialize({matchId: '', gameType: 'memory', currentUser: user})).rejects.toThrow('matchId');
    await expect(game.initialize({...practice(), mode: 'otro'} as unknown as PracticeContext))
      .rejects.toThrow('Memory no conoce el modo "otro"');
  });

  it('al terminar la práctica no guarda nada ni avisa el fin de partida', async () => {
    const {http, game, state, orchestrator} = setup();
    const context = practice();
    const enCanal = jest.fn(); const enWindow = jest.fn();
    context.events!.addEventListener(GAME_FINISHED_EVENT, enCanal);
    window.addEventListener(GAME_FINISHED_EVENT, enWindow);
    try {
      await game.initialize(context);
      await game.start();
      await finishSolo(orchestrator, state);
    } finally {
      window.removeEventListener(GAME_FINISHED_EVENT, enWindow);
    }

    expect(state.phase).toBe('finished');
    expect(state.players[0].matchedPairs).toBe(8);
    expect(enCanal).not.toHaveBeenCalled();
    expect(enWindow).not.toHaveBeenCalled();
    expect(http.saveResult).not.toHaveBeenCalled();
  });

  it('"Jugar de nuevo" reinicia la práctica sin salir del Shell', async () => {
    const {game, state, orchestrator} = setup();
    await game.initialize(practice());
    await game.start();
    await finishSolo(orchestrator, state);

    await orchestrator.playAgain();

    expect(state.phase).toBe('playing');
    expect(state.embedded).toBe(true);
    expect(state.soloMode).toBe(true);
    expect(state.players[0]).toEqual({userId: 'solo-1', displayName: 'Ana', matchedPairs: 0});
  });

  it('dispose() cierra la práctica sin desconectar un hub que nunca se usó', async () => {
    const {hub, game, state} = setup();
    await game.initialize(practice());
    await game.start();

    await game.dispose();

    expect(hub.disconnect).not.toHaveBeenCalled();
    expect(state.soloMode).toBe(false);
    expect(state.embedded).toBe(false);
  });
});

describe('Fin de una partida normal en context.events', () => {
  it('emite battlehub:game-finished en el canal del Shell con matchId y gameType', async () => {
    const {game, ea} = setup();
    const events = new EventTarget();
    const received: GameFinishedDetail[] = [];
    events.addEventListener(GAME_FINISHED_EVENT, event => received.push((event as CustomEvent<GameFinishedDetail>).detail));

    await game.initialize({matchId: 'room', gameType: 'memory', currentUser: {id: 'u1', displayName: 'Ana'}, events});
    await game.start();
    ea.publish(MemoryHubEvents.StateSnapshot, {matchId: 'room', phase: 'finished', startedAt: '2026-10-04T12:00:00Z',
      currentPlayerId: 'u1', turnDeadline: null, resultSaved: true,
      players: [{userId: 'u1', displayName: 'Ana', matchedPairs: 4}, {userId: 'u2', displayName: 'Beto', matchedPairs: 4}],
      cards: [], isDraw: true, winnerUserId: null});

    expect(received).toHaveLength(1);
    expect(received[0]).toMatchObject({matchId: 'room', gameType: 'memory', isDraw: true, winnerUserId: null});
  });
});

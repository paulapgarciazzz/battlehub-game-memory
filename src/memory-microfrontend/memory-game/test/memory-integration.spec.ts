import {IEventAggregator} from 'aurelia';
import {MemoryGameOrchestrator} from '../src/services/memory-game-orchestrator';
import {MemoryHubEvents} from '../src/services/memory-hub.service';
import {MemoryGameState} from '../src/state/memory-game-state';
import {GameModule} from '../src/game-module';
import {GAME_FINISHED_EVENT, type GameFinishedDetail} from '../src/game-contracts';
import {createFakeHub, createFakeHttp, createTestContainer} from './fakes';

const finishedSnapshot = {matchId: 'room', phase: 'finished', startedAt: '2026-10-04T12:00:00Z',
  currentPlayerId: 'u1', turnDeadline: null, resultSaved: true,
  players: [{userId: 'u1', displayName: 'Ana', matchedPairs: 5}, {userId: 'u2', displayName: 'Beto', matchedPairs: 3}],
  cards: [], isDraw: false, winnerUserId: 'u1'};

describe('Integración de Memory con el Shell', () => {
  it('rechaza el contexto de otro juego o sin datos, sin pedir tokens', async () => {
    const container = createTestContainer(); const game = container.invoke(GameModule);
    const context = {matchId: 'room', gameType: 'memory' as const, currentUser: {id: 'u1', displayName: 'Ana'}};
    await expect(game.initialize({...context, gameType: 'trivia'})).rejects.toThrow('matchId');
    await expect(game.initialize({...context, currentUser: {id: 'u1', displayName: ''}})).rejects.toThrow('matchId');
    await expect(game.initialize(context)).resolves.toBeUndefined();
  });
  it('restaura el marcador y las cartas desde el servidor y elimina listeners al salir', async () => {
    const hub = createFakeHub(); const http = createFakeHttp(); const container = createTestContainer(hub, http);
    const orchestrator = container.get(MemoryGameOrchestrator); const state = container.get(MemoryGameState);
    const ea = container.get(IEventAggregator);
    orchestrator.configure(jest.fn());
    await orchestrator.joinMatch('room', 'u1', 'Ana');
    expect(hub.joinMatch).toHaveBeenCalledWith('room', 'u1', 'Ana');
    state.paused = true;
    ea.publish(MemoryHubEvents.StateSnapshot, {matchId: 'room', phase: 'playing', startedAt: '2026-10-04T12:00:00Z',
      currentPlayerId: 'u2', turnDeadline: '2026-10-04T12:00:10Z', resultSaved: false,
      players: [{userId: 'u1', displayName: 'Ana', matchedPairs: 2}, {userId: 'u2', displayName: 'Beto', matchedPairs: 1}],
      cards: [{id: 0, value: 'A', matched: true, faceUp: true}], isDraw: false, winnerUserId: null});
    expect(state.paused).toBe(true);
    expect(state.players[0].matchedPairs).toBe(2); expect(state.currentPlayerId).toBe('u2');
    expect(state.turnDeadline).toBe(Date.parse('2026-10-04T12:00:10Z'));
    expect(http.saveResult).not.toHaveBeenCalled();
    await orchestrator.leaveMatch();
    ea.publish(MemoryHubEvents.PlayerJoined, {userId: 'late', displayName: 'Tarde'});
    expect(state.players).toEqual([]); expect(hub.clearPlayer).toHaveBeenCalled();
  });
  it('emite battlehub:game-finished una sola vez cuando termina la partida', async () => {
    const container = createTestContainer(); const game = container.invoke(GameModule);
    const ea = container.get(IEventAggregator);
    const received: GameFinishedDetail[] = [];
    const listener = (event: Event) => received.push((event as CustomEvent<GameFinishedDetail>).detail);
    window.addEventListener(GAME_FINISHED_EVENT, listener);
    try {
      await game.initialize({matchId: 'room', gameType: 'memory', currentUser: {id: 'u1', displayName: 'Ana'}});
      await game.start();
      ea.publish(MemoryHubEvents.StateSnapshot, finishedSnapshot);
      ea.publish(MemoryHubEvents.StateSnapshot, finishedSnapshot);
    } finally {
      window.removeEventListener(GAME_FINISHED_EVENT, listener);
    }
    expect(received).toEqual([{matchId: 'room', gameType: 'memory', isDraw: false, winnerUserId: 'u1',
      players: [{userId: 'u1', displayName: 'Ana', score: 5}, {userId: 'u2', displayName: 'Beto', score: 3}]}]);
  });
  it('emite el evento desde el elemento del juego cuando ya está en pantalla', () => {
    const container = createTestContainer(); const game = container.invoke(GameModule);
    const host = document.createElement('div'); const listener = jest.fn();
    host.addEventListener(GAME_FINISHED_EVENT, listener);
    Object.defineProperty(game, '$controller', {value: {host}});
    (game as unknown as {emitGameFinished(detail: GameFinishedDetail): void}).emitGameFinished(
      {matchId: 'room', gameType: 'memory', isDraw: true, winnerUserId: null, players: []});
    expect(listener).toHaveBeenCalledTimes(1);
  });
  it('no avisa al Shell fuera del modo integrado', async () => {
    const container = createTestContainer();
    const orchestrator = container.get(MemoryGameOrchestrator); const ea = container.get(IEventAggregator);
    const finished = jest.fn();
    orchestrator.configure(finished);
    await orchestrator.joinMatch('room', 'u1', 'Ana');
    ea.publish(MemoryHubEvents.StateSnapshot, finishedSnapshot);
    expect(finished).not.toHaveBeenCalled();
  });
});

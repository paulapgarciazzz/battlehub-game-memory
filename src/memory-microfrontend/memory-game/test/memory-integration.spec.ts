import {IEventAggregator} from 'aurelia';
import {MemoryGameOrchestrator} from '../src/services/memory-game-orchestrator';
import {MemoryHubEvents} from '../src/services/memory-hub.service';
import {MemoryGameState} from '../src/state/memory-game-state';
import {GameModule} from '../src/game-module';
import {createFakeHub, createFakeHttp, createTestContainer} from './fakes';

describe('Integración autenticada de Memory', () => {
  it('rechaza el contexto parcial o de otro juego', async () => {
    const container = createTestContainer(); const game = container.invoke(GameModule);
    const context = {matchId: 'room', gameType: 'memory' as const, currentUser: {id: 'u1', displayName: 'Ana'}};
    await expect(game.initialize(context)).rejects.toThrow('proveedores');
    await expect(game.initialize({...context, getAccessToken: async () => 'memory-token'})).rejects.toThrow('proveedores');
  });
  it('restaura el marcador y las cartas desde el servidor y elimina listeners al salir', async () => {
    const hub = createFakeHub(); const http = createFakeHttp(); const container = createTestContainer(hub, http);
    const orchestrator = container.get(MemoryGameOrchestrator); const state = container.get(MemoryGameState);
    const ea = container.get(IEventAggregator);
    orchestrator.configure(async () => 'memory-token', async () => 'room-token');
    await orchestrator.joinMatch('room', 'u1', 'Ana');
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
    expect(state.players).toEqual([]); expect(hub.clearCredentials).toHaveBeenCalled();
  });
});

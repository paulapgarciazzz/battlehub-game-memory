import {ResultScreen} from '../../../../src/resources/elements/result-screen';
import {MemoryGameOrchestrator} from '../../../../src/services/memory-game-orchestrator';
import {MemoryGameState} from '../../../../src/state/memory-game-state';
import {createFakeHttp, createFakeHub, createTestContainer} from '../../../fakes';

describe('ResultScreen', () => {
  it('shows the winner and delegates result-screen actions', async () => {
    const http = createFakeHttp();
    const container = createTestContainer(createFakeHub(), http);
    const screen = container.invoke(ResultScreen);
    const state = container.get(MemoryGameState);
    const orchestrator = container.get(MemoryGameOrchestrator);
    const playAgain = jest.spyOn(orchestrator, 'playAgain');
    const retry = jest.spyOn(orchestrator, 'retrySaveResult');

    state.players = [
      {userId: 'u1', displayName: 'Ana', matchedPairs: 4},
      {userId: 'u2', displayName: 'Beto', matchedPairs: 3}
    ];
    state.myUserId = 'u1';
    state.winnerUserId = 'u1';
    expect(screen.winner?.displayName).toBe('Ana');
    expect(screen.amIWinner).toBe(true);

    screen.retrySave();
    await Promise.resolve();
    expect(retry).toHaveBeenCalled();

    await screen.playAgain();
    expect(playAgain).toHaveBeenCalled();
    expect(state.phase).toBe('join');
  });
});

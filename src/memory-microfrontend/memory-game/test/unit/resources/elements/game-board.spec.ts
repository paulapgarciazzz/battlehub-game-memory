import {GameBoard} from '../../../../src/resources/elements/game-board';
import {MemoryGameState} from '../../../../src/state/memory-game-state';
import {createFakeHub, createTestContainer} from '../../../fakes';

describe('GameBoard', () => {
  it('disables board actions according to game mode and turn state', async () => {
    const hub = createFakeHub();
    const container = createTestContainer(hub);
    const board = container.invoke(GameBoard);
    const state = container.get(MemoryGameState);

    expect(board.isCardDisabled).toBe(true);
    state.practiceMode = true;
    state.phase = 'playing';
    expect(board.isCardDisabled).toBe(false);
    state.practiceResolving = true;
    expect(board.isCardDisabled).toBe(true);

    state.practiceMode = false;
    state.practiceResolving = false;
    state.myUserId = 'u1';
    state.currentPlayerId = 'u1';
    state.players = [
      {userId: 'u1', displayName: 'Ana', matchedPairs: 0},
      {userId: 'u2', displayName: 'Beto', matchedPairs: 0}
    ];
    expect(board.myPlayer?.userId).toBe('u1');
    expect(board.opponentPlayer?.userId).toBe('u2');
    expect(board.isMyTrayActive).toBe(true);
    expect(board.isOpponentTrayActive).toBe(false);
    expect(board.isCardDisabled).toBe(false);

    board.onFlip(new CustomEvent('flip', {detail: {cardId: 3}}));
    await Promise.resolve();
    expect(hub.flipCard).toHaveBeenCalledWith('', 3);

    state.currentPlayerId = 'u2';
    expect(board.isCardDisabled).toBe(true);
    expect(board.isMyTrayActive).toBe(false);
    expect(board.isOpponentTrayActive).toBe(true);

    state.soloMode = true;
    state.currentPlayerId = null;
    expect(board.isCardDisabled).toBe(false);
    state.paused = true;
    expect(board.isCardDisabled).toBe(true);
  });
});

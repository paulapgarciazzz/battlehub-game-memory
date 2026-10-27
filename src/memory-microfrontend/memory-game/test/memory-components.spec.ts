import {GameBoard} from '../src/resources/elements/game-board';
import {JoinScreen} from '../src/resources/elements/join-screen';
import {ResultScreen} from '../src/resources/elements/result-screen';
import {TurnIndicator} from '../src/resources/elements/turn-indicator';
import {MemoryGameOrchestrator} from '../src/services/memory-game-orchestrator';
import {MemoryGameState} from '../src/state/memory-game-state';
import {createFakeHttp, createFakeHub, createTestContainer} from './fakes';

describe('Memory game components', () => {
  afterEach(() => jest.useRealTimers());

  it('validates, submits and reports failures from the join screen', async () => {
    const hub = createFakeHub();
    const container = createTestContainer(hub);
    const screen = container.invoke(JoinScreen);
    const preventDefault = jest.fn();

    await screen.join({preventDefault} as unknown as Event);
    expect(preventDefault).toHaveBeenCalled();
    expect(screen.error).toBe('Completá tu usuario, tu nombre y el código de partida.');
    expect(hub.connect).not.toHaveBeenCalled();

    screen.userId = ' player ';
    screen.displayName = ' Ana ';
    screen.matchId = ' room ';
    await screen.join();
    expect(hub.joinLocalMatch).toHaveBeenCalledWith('ROOM', 'player', 'Ana');
    expect(screen.joining).toBe(false);
    expect(screen.error).toBeNull();

    hub.connect.mockRejectedValueOnce(new Error('Sin conexión'));
    await screen.join();
    expect(screen.error).toBe('Sin conexión');
    expect(screen.joining).toBe(false);
  });

  it('generates a match id and starts solo mode from the join screen', () => {
    const container = createTestContainer();
    const screen = container.invoke(JoinScreen);
    screen.displayName = 'Ana';
    screen.generateMatchId();
    expect(screen.matchId).toMatch(/^[A-Z0-9]+$/);

    screen.error = 'error anterior';
    screen.startSolo();
    expect(screen.error).toBeNull();
    expect(container.get(MemoryGameState).soloMode).toBe(true);
  });

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

  it('updates and stops the turn countdown', () => {
    jest.useFakeTimers();
    const container = createTestContainer();
    const indicator = container.invoke(TurnIndicator);
    const state = container.get(MemoryGameState);
    const now = new Date('2026-10-04T12:00:00Z').getTime();
    jest.setSystemTime(now);

    indicator.attached();
    expect(indicator.remainingSeconds).toBe(10);
    expect(indicator.remainingRatio).toBe(1);

    state.turnDeadline = now + 6_000;
    jest.advanceTimersByTime(200);
    expect(indicator.remainingSeconds).toBe(6);
    expect(indicator.remainingRatio).toBeCloseTo(0.58);

    state.turnDeadline = now - 1_000;
    jest.advanceTimersByTime(200);
    expect(indicator.remainingSeconds).toBe(0);
    expect(indicator.remainingRatio).toBe(0);

    indicator.detaching();
    jest.advanceTimersByTime(1_000);
    expect(indicator.remainingSeconds).toBe(0);
  });
});

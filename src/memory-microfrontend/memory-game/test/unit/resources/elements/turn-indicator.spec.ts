import {TurnIndicator} from '../../../../src/resources/elements/turn-indicator';
import {MemoryGameState} from '../../../../src/state/memory-game-state';
import {createTestContainer} from '../../../fakes';

describe('TurnIndicator', () => {
  afterEach(() => jest.useRealTimers());

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

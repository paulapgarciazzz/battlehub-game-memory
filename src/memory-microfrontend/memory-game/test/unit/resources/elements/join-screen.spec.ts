import {JoinScreen} from '../../../../src/resources/elements/join-screen';
import {MemoryGameState} from '../../../../src/state/memory-game-state';
import {createFakeHub, createTestContainer} from '../../../fakes';

describe('JoinScreen', () => {
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
});

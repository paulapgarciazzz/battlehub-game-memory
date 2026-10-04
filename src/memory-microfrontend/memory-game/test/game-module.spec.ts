import { createFixture } from '@aurelia/testing';
import { GameModule } from '../src/game-module';
import { MemoryStandalone } from '../src/memory-standalone';
import { MemoryGameState } from '../src/state/memory-game-state';
import { createFakeHub, createFakeHttp, createTestContainer, fakeRegistrations } from './fakes';

const context = { matchId: 'match-001', gameType: 'memory', currentUser: { id: 'user-001', displayName: 'Ana' } };

function setup() {
  const hub = createFakeHub();
  const container = createTestContainer(hub, createFakeHttp());
  // Igual que el Shell: crea el módulo con container.invoke(GameModule).
  const gameModule = container.invoke(GameModule);
  const state = container.get(MemoryGameState);
  return { hub, gameModule, state };
}

describe('GameModule (contrato con el Shell)', () => {
  it('se une a la partida del contexto al llamar a start()', async () => {
    const { hub, gameModule, state } = setup();

    await gameModule.initialize(context);
    await gameModule.start();

    expect(state.embedded).toBe(true);
    expect(hub.connect).toHaveBeenCalledTimes(1);
    expect(hub.joinMatch).toHaveBeenCalledWith('match-001', 'user-001', 'Ana');
  });

  it('al reanudar (start después de pause) no vuelve a unirse', async () => {
    const { hub, gameModule, state } = setup();
    await gameModule.initialize(context);
    await gameModule.start();

    await gameModule.pause();
    expect(state.paused).toBe(true);

    await gameModule.start();

    expect(state.paused).toBe(false);
    expect(hub.joinMatch).toHaveBeenCalledTimes(1);
  });

  it('dispose() desconecta del hub y limpia el estado', async () => {
    const { hub, gameModule, state } = setup();
    await gameModule.initialize(context);
    await gameModule.start();

    await gameModule.dispose();

    expect(hub.disconnect).toHaveBeenCalledTimes(1);
    expect(state.matchId).toBe('');
    expect(state.phase).toBe('join');
    expect(state.embedded).toBe(false);
  });

  it('rechaza un contexto sin matchId o sin usuario', async () => {
    const { gameModule } = setup();

    await expect(gameModule.initialize({ ...context, matchId: '' })).rejects.toThrow();
    await expect(gameModule.initialize({ ...context, currentUser: { id: '', displayName: 'Ana' } })).rejects.toThrow();
  });

  it('si no se puede unir, start() falla y se puede reintentar', async () => {
    const { hub, gameModule } = setup();
    hub.joinMatch.mockRejectedValueOnce(new Error('sin conexión'));
    await gameModule.initialize(context);

    await expect(gameModule.start()).rejects.toThrow('sin conexión');
    await gameModule.start();

    expect(hub.joinMatch).toHaveBeenCalledTimes(2);
  });

  it('muestra la espera del rival sin pantalla de unión', async () => {
    const hub = createFakeHub();
    const { appHost, component, startPromise } = createFixture(
      '<game-module component.ref="game"></game-module>',
      class { game!: GameModule; },
      [GameModule, ...fakeRegistrations(hub, createFakeHttp())]);
    await startPromise;

    await component.game.initialize(context);
    await component.game.start();
    component.game.state.phase = 'waiting-for-opponent';
    await Promise.resolve();

    expect(appHost.textContent).toContain('Esperando al rival para la partida match-001');
    expect(appHost.querySelector('form')).toBeNull();
  });
});

describe('MemoryStandalone (modo de desarrollo local)', () => {
  it('muestra la pantalla de unión con código de partida', async () => {
    const { appHost, startPromise } = createFixture(
      '<memory-standalone></memory-standalone>',
      class {},
      [MemoryStandalone, ...fakeRegistrations(createFakeHub(), createFakeHttp())]);
    await startPromise;

    expect(appHost.querySelector('form')).not.toBeNull();
    expect(appHost.textContent).toContain('Código de partida');
  });
});

import { createFixture } from '@aurelia/testing';
import { MemoryStandalone } from '../../src/memory-standalone';
import { createFakeHub, createFakeHttp, fakeRegistrations } from '../fakes';

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

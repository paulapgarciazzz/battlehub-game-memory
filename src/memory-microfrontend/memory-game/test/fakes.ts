import { DI, IContainer, Registration } from 'aurelia';
import { MemoryHubService } from '../src/services/memory-hub.service';
import { MemoryHttpService } from '../src/services/memory-http.service';

// Reemplazos de los servicios que hablan con el backend, para probar sin red.
export function createFakeHub() {
  return {
    connect: jest.fn().mockResolvedValue(undefined),
    joinMatch: jest.fn().mockResolvedValue(undefined),
    flipCard: jest.fn().mockResolvedValue(undefined),
    disconnect: jest.fn().mockResolvedValue(undefined)
  };
}

export function createFakeHttp() {
  return {
    getHistory: jest.fn().mockResolvedValue([]),
    saveResult: jest.fn().mockResolvedValue({ resultId: 'r1', matchId: 'm1', message: 'ok' })
  };
}

export type FakeHub = ReturnType<typeof createFakeHub>;
export type FakeHttp = ReturnType<typeof createFakeHttp>;

export function fakeRegistrations(hub: FakeHub, http: FakeHttp) {
  return [
    Registration.instance(MemoryHubService, hub),
    Registration.instance(MemoryHttpService, http)
  ];
}

export function createTestContainer(hub: FakeHub = createFakeHub(), http: FakeHttp = createFakeHttp()): IContainer {
  const container = DI.createContainer();
  container.register(...fakeRegistrations(hub, http));
  return container;
}

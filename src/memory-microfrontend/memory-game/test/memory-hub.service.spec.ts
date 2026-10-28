import {DI, IEventAggregator} from 'aurelia';
import {MemoryHubEvents, MemoryHubService} from '../src/services/memory-hub.service';

jest.mock('@microsoft/signalr', () => {
  const connection = {
    state: 'Disconnected',
    on: jest.fn(),
    onreconnecting: jest.fn(),
    onreconnected: jest.fn(),
    onclose: jest.fn(),
    start: jest.fn(),
    stop: jest.fn(),
    invoke: jest.fn()
  };
  const builder = {
    withUrl: jest.fn().mockReturnThis(),
    withAutomaticReconnect: jest.fn().mockReturnThis(),
    build: jest.fn(() => connection)
  };

  return {
    HubConnectionBuilder: jest.fn(() => builder),
    HubConnectionState: {Connected: 'Connected'},
    mockConnection: connection,
    mockBuilder: builder
  };
});

type MockConnection = {
  state: string;
  on: jest.Mock;
  onreconnecting: jest.Mock;
  onreconnected: jest.Mock;
  onclose: jest.Mock;
  start: jest.Mock;
  stop: jest.Mock;
  invoke: jest.Mock;
};

type MockBuilder = {
  withUrl: jest.Mock;
  withAutomaticReconnect: jest.Mock;
  build: jest.Mock;
};

const mocks = jest.requireMock('@microsoft/signalr') as {
  mockConnection: MockConnection;
  mockBuilder: MockBuilder;
};

function setup() {
  const container = DI.createContainer();
  const service = container.invoke(MemoryHubService);
  const events = container.get(IEventAggregator);
  return {service, events};
}

describe('MemoryHubService', () => {
  beforeEach(() => {
    const connection = mocks.mockConnection;
    connection.state = 'Disconnected';
    connection.on.mockReset();
    connection.onreconnecting.mockReset();
    connection.onreconnected.mockReset();
    connection.onclose.mockReset();
    connection.start.mockReset().mockImplementation(async () => {
      connection.state = 'Connected';
    });
    connection.stop.mockReset().mockImplementation(async () => {
      connection.state = 'Disconnected';
    });
    connection.invoke.mockReset().mockResolvedValue(undefined);
    mocks.mockBuilder.withUrl.mockClear();
    mocks.mockBuilder.withAutomaticReconnect.mockClear();
    mocks.mockBuilder.build.mockClear();
  });

  it('requires a connection before joining or flipping cards', async () => {
    const {service} = setup();
    await expect(service.joinMatch('m1', 'u1', 'Ana')).rejects.toThrow(
      'Memory no está conectado al modo competitivo.'
    );
    await expect(service.joinLocalMatch('m1', 'u1', 'Ana')).rejects.toThrow(
      'Memory no está conectado al modo local.'
    );
    await expect(service.flipCard('m1', 0)).rejects.toThrow(
      'Memory no está conectado.'
    );
  });

  it('connects competitively without tokens, publishes hub events and joins with the Shell user', async () => {
    const {service, events} = setup();
    const published = jest.fn();
    events.subscribe(MemoryHubEvents.PlayerJoined, published);
    service.configure();

    await service.connect();

    expect(mocks.mockBuilder.withUrl).toHaveBeenCalledWith(
      'http://localhost:5095/hubs/memory'
    );
    expect(mocks.mockConnection.on).toHaveBeenCalledTimes(8);

    const playerJoined = mocks.mockConnection.on.mock.calls.find(
      ([name]) => name === 'PlayerJoined'
    )?.[1] as (message: unknown) => void;
    playerJoined({userId: 'u2'});
    expect(published).toHaveBeenCalledWith(
      {userId: 'u2'},
      MemoryHubEvents.PlayerJoined
    );

    await service.joinMatch('m1', 'u1', 'Ana');
    expect(mocks.mockConnection.invoke).toHaveBeenCalledWith(
      'JoinMatch',
      'm1',
      'u1',
      'Ana'
    );
    await service.flipCard('m1', 4);
    expect(mocks.mockConnection.invoke).toHaveBeenCalledWith('FlipCard', 'm1', 4);

    await service.connect();
    expect(mocks.mockBuilder.build).toHaveBeenCalledTimes(1);
  });

  it('connects locally, rejoins after reconnect and clears the player on disconnect', async () => {
    const {service} = setup();
    service.configureLocal();

    await service.connect();
    expect(mocks.mockBuilder.withUrl).toHaveBeenCalledWith(
      'http://localhost:5095/hubs/memory-local'
    );

    await service.joinLocalMatch('room', 'u1', 'Ana');
    expect(mocks.mockConnection.invoke).toHaveBeenCalledWith(
      'JoinMatch',
      'room',
      'u1',
      'Ana'
    );

    const reconnected = mocks.mockConnection.onreconnected.mock.calls[0][0] as () => Promise<void>;
    await reconnected();
    expect(mocks.mockConnection.invoke).toHaveBeenLastCalledWith(
      'JoinMatch',
      'room',
      'u1',
      'Ana'
    );

    await service.disconnect();
    expect(mocks.mockConnection.stop).toHaveBeenCalledTimes(1);
    await expect(service.joinLocalMatch('room', 'u1', 'Ana')).rejects.toThrow(
      'Memory no está conectado al modo local.'
    );
    service.clearPlayer();
  });

  it('publishes connection status and stops a connection when reconnecting fails', async () => {
    const {service, events} = setup();
    const published = jest.fn();
    events.subscribe(MemoryHubEvents.ConnectionClosed, published);
    service.configure();
    await service.connect();

    const reconnecting = mocks.mockConnection.onreconnecting.mock.calls[0][0] as () => void;
    reconnecting();
    expect(published).toHaveBeenCalledWith(null, MemoryHubEvents.ConnectionClosed);

    await service.joinMatch('m1', 'u1', 'Ana');
    mocks.mockConnection.invoke.mockRejectedValueOnce(new Error('join failed'));
    const reconnected = mocks.mockConnection.onreconnected.mock.calls[0][0] as () => Promise<void>;
    await reconnected();
    expect(mocks.mockConnection.stop).toHaveBeenCalled();

    const onclose = mocks.mockConnection.onclose.mock.calls[0][0] as () => void;
    onclose();
    expect(published).toHaveBeenCalledTimes(3);
  });

  it('stops and rethrows when the initial connection fails', async () => {
    const {service} = setup();
    service.configure();
    mocks.mockConnection.start.mockRejectedValueOnce(new Error('offline'));

    await expect(service.connect()).rejects.toThrow('offline');
    expect(mocks.mockConnection.stop).toHaveBeenCalledTimes(1);
  });
});

import {MemoryHttpService} from '../src/services/memory-http.service';

describe('MemoryHttpService', () => {
  const service = new MemoryHttpService();
  const originalFetch = globalThis.fetch;
  const fetchMock = jest.fn<ReturnType<typeof fetch>, Parameters<typeof fetch>>();

  beforeAll(() => {
    Object.defineProperty(globalThis, 'fetch', {
      configurable: true,
      writable: true,
      value: fetchMock
    });
  });

  beforeEach(() => {
    fetchMock.mockReset();
  });

  afterAll(() => {
    if (originalFetch) {
      globalThis.fetch = originalFetch;
    } else {
      Reflect.deleteProperty(globalThis, 'fetch');
    }
  });

  function response(body: unknown, ok = true): Response {
    return {
      ok,
      json: jest.fn().mockResolvedValue(body)
    } as unknown as Response;
  }

  it('gets an encoded match result without an Auth0 token', async () => {
    const result = {matchId: 'room/one'};
    fetchMock.mockResolvedValue(response(result));

    await expect(service.getResult('room/one')).resolves.toEqual(result);

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5095/api/games/memory/results/room%2Fone',
      {headers: {
        'Content-Type': 'application/json'
      }}
    );
  });

  it('gets the encoded player history', async () => {
    const history = [{matchId: 'm1'}];
    fetchMock.mockResolvedValue(response(history));

    await expect(service.getHistory('auth0|player')).resolves.toEqual(history);

    expect(fetchMock.mock.calls[0][0]).toBe(
      'http://localhost:5095/api/games/memory/players/auth0%7Cplayer/history'
    );
  });

  it('posts the match id and start time when saving a result', async () => {
    const startedAt = new Date('2026-10-04T12:00:00.000Z');
    const result = {resultId: 'r1', matchId: 'm1', message: 'saved'};
    fetchMock.mockResolvedValue(response(result));

    await expect(service.saveResult('m1', startedAt)).resolves.toEqual(result);

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5095/api/games/memory/results',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({matchId: 'm1', startedAt: startedAt.toISOString()})
      })
    );
  });

  it('reports an API error when getting a result or history', async () => {
    fetchMock.mockResolvedValue(response(null, false));

    await expect(service.getResult('missing')).rejects.toThrow(
      'El resultado todavía no está confirmado en el servidor.'
    );
    await expect(service.getHistory('u1')).rejects.toThrow(
      'No se pudo obtener el historial de partidas.'
    );
  });

  it('uses the server message when saving fails', async () => {
    fetchMock.mockResolvedValue(response({message: 'Resultado rechazado'}, false));

    await expect(service.saveResult('m1', new Date())).rejects.toThrow(
      'Resultado rechazado'
    );
  });

  it('uses a fallback when a failed save has no JSON error body', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      json: jest.fn().mockRejectedValue(new Error('Invalid JSON'))
    } as unknown as Response);

    await expect(service.saveResult('m1', new Date())).rejects.toThrow(
      'No se pudo guardar el resultado de la partida.'
    );
  });
});

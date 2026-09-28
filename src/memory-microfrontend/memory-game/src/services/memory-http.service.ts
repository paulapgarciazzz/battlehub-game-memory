import {singleton} from 'aurelia-framework';
import environment from '../../config/environment.json';
import {MemoryGameHistoryDto} from '../models/memory-game-history.dto';

export interface SaveResultResponse {
  resultId: string;
  matchId: string;
  message: string;
}

@singleton()
export class MemoryHttpService {
  private readonly baseUrl = environment.apiBaseUrl;

  public async getHistory(userId: string): Promise<MemoryGameHistoryDto[]> {
    const response = await fetch(
      `${this.baseUrl}/api/games/memory/history/${encodeURIComponent(userId)}`);

    if (!response.ok) {
      throw new Error('No se pudo obtener el historial de partidas.');
    }

    return response.json();
  }

  public async saveResult(matchId: string, startedAt: Date): Promise<SaveResultResponse> {
    const response = await fetch(`${this.baseUrl}/api/games/memory/results`, {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({
        matchId: matchId,
        startedAt: startedAt.toISOString()
      })
    });

    if (!response.ok) {
      const body = await response.json().catch(() => null);
      throw new Error(body?.message ?? 'No se pudo guardar el resultado de la partida.');
    }

    return response.json();
  }
}

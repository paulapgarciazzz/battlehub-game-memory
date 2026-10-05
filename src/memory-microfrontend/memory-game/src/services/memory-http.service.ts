import {singleton} from 'aurelia';
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
  private token: (() => Promise<string>) | null = null;
  public configure(token: (() => Promise<string>) | null): void { this.token = token; }
  private async headers(): Promise<Record<string, string>> {
    if (!this.baseUrl) throw new Error('Configurar la URL de la API de Memory.');
    if (!this.token) throw new Error('Abrir Memory desde el Shell.');
    return {Authorization: `Bearer ${await this.token()}`, 'Content-Type': 'application/json'};
  }
  public async getResult(matchId: string): Promise<unknown> {
    const response = await fetch(`${this.baseUrl}/api/games/memory/results/${encodeURIComponent(matchId)}`, {headers: await this.headers()});
    if (!response.ok) throw new Error('El resultado todavía no está confirmado en el servidor.');
    return response.json();
  }

  public async getHistory(userId: string): Promise<MemoryGameHistoryDto[]> {
    const response = await fetch(
      `${this.baseUrl}/api/games/memory/players/${encodeURIComponent(userId)}/history`, {headers: await this.headers()});

    if (!response.ok) {
      throw new Error('No se pudo obtener el historial de partidas.');
    }

    return response.json();
  }

  public async saveResult(matchId: string, startedAt: Date): Promise<SaveResultResponse> {
    const response = await fetch(`${this.baseUrl}/api/games/memory/results`, {
      method: 'POST',
      headers: await this.headers(),
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

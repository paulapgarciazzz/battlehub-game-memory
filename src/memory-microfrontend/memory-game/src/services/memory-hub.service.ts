import {IEventAggregator, resolve, singleton} from 'aurelia';
import * as signalR from '@microsoft/signalr';
import environment from '../../config/environment.json';
export const MemoryHubEvents = {
  PlayerJoined: 'memory:player-joined', GameReady: 'memory:game-ready',
  PreviewFinished: 'memory:preview-finished', CardFlipped: 'memory:card-flipped',
  TurnTimeout: 'memory:turn-timeout', ConnectionClosed: 'memory:connection-closed',
  StateSnapshot: 'memory:state-snapshot', ResultSaved: 'memory:result-saved', ResultSaveFailed: 'memory:result-save-failed'
} as const;
@singleton()
export class MemoryHubService {
  private readonly ea = resolve(IEventAggregator);
  private connection: signalR.HubConnection | null = null;
  private token: (() => Promise<string>) | null = null;
  private roomToken: (() => Promise<string>) | null = null;
  private matchId: string | null = null;
  public configure(token: () => Promise<string>, roomToken: () => Promise<string>): void {
    this.token = token; this.roomToken = roomToken;
  }
  public async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) return;
    if (!environment.hubBaseUrl) throw new Error('Configurar la URL del hub de Memory.');
    if (!this.token || !this.roomToken) throw new Error('Abrir Memory desde una sala del Shell.');
    if (this.connection) await this.disconnect();
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubBaseUrl}/hubs/memory`, {accessTokenFactory: () => {
        if (!this.token) return Promise.reject(new Error('La partida se cerró.'));
        return this.token();
      }})
      .withAutomaticReconnect().build();
    this.connection = connection;
    for (const event of ['PlayerJoined', 'GameReady', 'PreviewFinished', 'CardFlipped', 'TurnTimeout', 'StateSnapshot', 'ResultSaved', 'ResultSaveFailed'] as const) {
      connection.on(event, msg => { if (this.connection === connection) this.ea.publish(MemoryHubEvents[event], msg); });
    }
    connection.onreconnecting(() => this.ea.publish(MemoryHubEvents.ConnectionClosed, null));
    connection.onreconnected(async () => {
      try { if (this.connection === connection && this.matchId) await this.joinMatch(this.matchId); }
      catch { this.ea.publish(MemoryHubEvents.ConnectionClosed, null); await connection.stop(); }
    });
    connection.onclose(() => { if (this.connection === connection) this.ea.publish(MemoryHubEvents.ConnectionClosed, null); });
    try { await connection.start(); }
    catch (error) { if (this.connection === connection) this.connection = null; await connection.stop(); throw error; }
  }
  public async joinMatch(matchId: string): Promise<void> {
    if (!this.connection || !this.roomToken) throw new Error('Memory no está conectado.');
    this.matchId = matchId;
    await this.connection.invoke('JoinMatch', matchId, await this.roomToken());
  }
  public async flipCard(matchId: string, cardId: number): Promise<void> {
    if (!this.connection) throw new Error('Memory no está conectado.');
    await this.connection.invoke('FlipCard', matchId, cardId);
  }
  public async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null; this.matchId = null;
    await connection?.stop();
  }
  public clearCredentials(): void { this.token = null; this.roomToken = null; }
}

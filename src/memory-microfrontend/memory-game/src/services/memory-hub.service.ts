import {IEventAggregator, resolve, singleton} from 'aurelia';
import * as signalR from '@microsoft/signalr';
import environment from '../../config/environment.json';

// Canales del EventAggregator: cada evento del hub se republica con este nombre.
export const MemoryHubEvents = {
  PlayerJoined: 'memory:player-joined',
  GameReady: 'memory:game-ready',
  PreviewFinished: 'memory:preview-finished',
  CardFlipped: 'memory:card-flipped',
  TurnTimeout: 'memory:turn-timeout',
  ConnectionClosed: 'memory:connection-closed',
  // Estado completo de la partida al entrar o al reconectar.
  StateSnapshot: 'memory:state-snapshot',
  // El backend confirma (o no) que guardó el resultado.
  ResultSaved: 'memory:result-saved',
  ResultSaveFailed: 'memory:result-save-failed'
} as const;

// Eventos que el hub de Memory envía y que se reenvían al EventAggregator.
const HUB_EVENTS = [
  'PlayerJoined',
  'GameReady',
  'PreviewFinished',
  'CardFlipped',
  'TurnTimeout',
  'StateSnapshot',
  'ResultSaved',
  'ResultSaveFailed'
] as const;

/**
 * Conexión SignalR con /hubs/memory.
 *
 * Los tokens los entrega el Shell por GameContext (ver GameModule):
 * - token: token del jugador para la API de Memory (audiencia de Memory).
 * - roomToken: token del jugador para Matchmaking; el backend lo usa solo
 *   para validar la sala.
 */
@singleton()
export class MemoryHubService {
  private readonly ea = resolve(IEventAggregator);
  private connection: signalR.HubConnection | null = null;
  private token: (() => Promise<string>) | null = null;
  private roomToken: (() => Promise<string>) | null = null;

  // Partida actual: se usa para volver a unirse después de una reconexión.
  private matchId: string | null = null;

  public configure(token: () => Promise<string>, roomToken: () => Promise<string>): void {
    this.token = token;
    this.roomToken = roomToken;
  }

  public async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (!environment.hubBaseUrl) {
      throw new Error('Configurar la URL del hub de Memory.');
    }

    // Sin tokens no se puede jugar: el modo independiente ya no autentica
    // escribiendo un userId, hay que entrar desde una sala del Shell.
    if (!this.token || !this.roomToken) {
      throw new Error('Abrir Memory desde una sala del Shell.');
    }

    if (this.connection) {
      await this.disconnect();
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubBaseUrl}/hubs/memory`, {
        // SignalR pide el token en cada conexión y reconexión.
        accessTokenFactory: () => {
          if (!this.token) {
            return Promise.reject(new Error('La partida se cerró.'));
          }

          return this.token();
        }
      })
      .withAutomaticReconnect()
      .build();

    this.connection = connection;

    // Solo se publican los eventos de la conexión vigente; si hubo una
    // conexión anterior, sus eventos tardíos se ignoran.
    for (const event of HUB_EVENTS) {
      connection.on(event, msg => {
        if (this.connection === connection) {
          this.ea.publish(MemoryHubEvents[event], msg);
        }
      });
    }

    connection.onreconnecting(() => this.ea.publish(MemoryHubEvents.ConnectionClosed, null));

    // Al reconectar se vuelve a hacer JoinMatch: el backend responde con un
    // StateSnapshot y la pantalla recupera la partida.
    connection.onreconnected(async () => {
      try {
        if (this.connection === connection && this.matchId) {
          await this.joinMatch(this.matchId);
        }
      } catch {
        this.ea.publish(MemoryHubEvents.ConnectionClosed, null);
        await connection.stop();
      }
    });

    connection.onclose(() => {
      if (this.connection === connection) {
        this.ea.publish(MemoryHubEvents.ConnectionClosed, null);
      }
    });

    try {
      await connection.start();
    } catch (error) {
      if (this.connection === connection) {
        this.connection = null;
      }

      await connection.stop();
      throw error;
    }
  }

  public async joinMatch(matchId: string): Promise<void> {
    if (!this.connection || !this.roomToken) {
      throw new Error('Memory no está conectado.');
    }

    this.matchId = matchId;
    await this.connection.invoke('JoinMatch', matchId, await this.roomToken());
  }

  // El backend toma el jugador del token: ya no se envía userId.
  public async flipCard(matchId: string, cardId: number): Promise<void> {
    if (!this.connection) {
      throw new Error('Memory no está conectado.');
    }

    await this.connection.invoke('FlipCard', matchId, cardId);
  }

  public async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.matchId = null;
    await connection?.stop();
  }

  // Lo usa GameModule.dispose() para no conservar los proveedores de tokens.
  public clearCredentials(): void {
    this.token = null;
    this.roomToken = null;
  }
}

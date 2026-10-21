
import { IEventAggregator, resolve, singleton } from 'aurelia';
import * as signalR from '@microsoft/signalr';
import environment from '../../config/environment.json';

export const MemoryHubEvents = {
  PlayerJoined: 'memory:player-joined',
  GameReady: 'memory:game-ready',
  PreviewFinished: 'memory:preview-finished',
  CardFlipped: 'memory:card-flipped',
  TurnTimeout: 'memory:turn-timeout',
  ConnectionClosed: 'memory:connection-closed',
  StateSnapshot: 'memory:state-snapshot',
  ResultSaved: 'memory:result-saved',
  ResultSaveFailed: 'memory:result-save-failed'
} as const;

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

@singleton()
export class MemoryHubService {
  private readonly ea = resolve(IEventAggregator);

  private connection: signalR.HubConnection | null = null;

  private token: (() => Promise<string>) | null = null;
  private roomToken: (() => Promise<string>) | null = null;

  private matchId: string | null = null;

  // Modo local independiente del Shell.
  private localMode = false;
  private localUserId: string | null = null;
  private localDisplayName: string | null = null;

  // Se utiliza en el modo competitivo de BattleHub.
  public configure(
    token: () => Promise<string>,
    roomToken: () => Promise<string>
  ): void {
    this.localMode = false;
    this.localUserId = null;
    this.localDisplayName = null;

    this.token = token;
    this.roomToken = roomToken;
  }

  // Se utiliza cuando se entra directamente a localhost:4003.
  public configureLocal(): void {
    this.localMode = true;
    this.token = null;
    this.roomToken = null;
  }

  public async connect(): Promise<void> {
    if (
      this.connection?.state ===
      signalR.HubConnectionState.Connected
    ) {
      return;
    }

    if (!this.localMode && !environment.hubBaseUrl) {
      throw new Error('Configurar la URL del hub de Memory.');
    }

    if (!this.localMode && (!this.token || !this.roomToken)) {
      throw new Error(
        'Faltan los proveedores de tokens del Shell de BattleHub.'
      );
    }

    if (this.connection) {
      await this.disconnect();
    }

    // En local se usa el mismo equipo que sirve el frontend.
    // Así también funciona al abrir la web mediante la IP LAN
    // de la computadora que ejecuta el backend.
    const hubUrl = this.localMode
      ? `http://${window.location.hostname}:5095/hubs/memory-local`
      : `${environment.hubBaseUrl}/hubs/memory`;

    const options: signalR.IHttpConnectionOptions = this.localMode
      ? {}
      : {
          accessTokenFactory: () => {
            if (!this.token) {
              return Promise.reject(
                new Error('La partida se cerró.')
              );
            }

            return this.token();
          }
        };

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, options)
      .withAutomaticReconnect()
      .build();

    this.connection = connection;

    for (const event of HUB_EVENTS) {
      connection.on(event, msg => {
        if (this.connection === connection) {
          this.ea.publish(MemoryHubEvents[event], msg);
        }
      });
    }

    connection.onreconnecting(() => {
      this.ea.publish(MemoryHubEvents.ConnectionClosed, null);
    });

    connection.onreconnected(async () => {
      try {
        if (this.connection !== connection || !this.matchId) {
          return;
        }

        if (this.localMode) {
          if (!this.localUserId || !this.localDisplayName) {
            throw new Error('Faltan los datos del jugador local.');
          }

          await this.joinLocalMatch(
            this.matchId,
            this.localUserId,
            this.localDisplayName
          );
        } else {
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

  // Conexión competitiva: conserva los tokens de Matchmaking.
  public async joinMatch(matchId: string): Promise<void> {
    if (
      !this.connection ||
      !this.roomToken ||
      this.localMode
    ) {
      throw new Error(
        'Memory no está conectado al modo competitivo.'
      );
    }

    this.matchId = matchId;

    await this.connection.invoke(
      'JoinMatch',
      matchId,
      await this.roomToken()
    );
  }

  // Conexión local: no necesita tokens del Shell.
  public async joinLocalMatch(
    matchId: string,
    userId: string,
    displayName: string
  ): Promise<void> {
    if (!this.connection || !this.localMode) {
      throw new Error('Memory no está conectado al modo local.');
    }

    this.matchId = matchId;
    this.localUserId = userId;
    this.localDisplayName = displayName;

    await this.connection.invoke(
      'JoinMatch',
      matchId,
      userId,
      displayName
    );
  }

  public async flipCard(
    matchId: string,
    cardId: number
  ): Promise<void> {
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

  public clearCredentials(): void {
    this.token = null;
    this.roomToken = null;

    this.localMode = false;
    this.localUserId = null;
    this.localDisplayName = null;
  }
}

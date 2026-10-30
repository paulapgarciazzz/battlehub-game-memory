
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

  private matchId: string | null = null;

  // Jugador de esta conexión: se usa para volver a entrar al reconectar.
  private userId: string | null = null;
  private displayName: string | null = null;

  // Modo local independiente del Shell.
  private localMode = false;

  // Se utiliza en el modo competitivo de BattleHub (dentro del Shell).
  public configure(): void {
    this.localMode = false;
    this.userId = null;
    this.displayName = null;
  }

  // Se utiliza cuando se entra directamente a localhost:4003.
  public configureLocal(): void {
    this.localMode = true;
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

    if (this.localMode && !environment.localHubPort) {
      throw new Error('Configurar el puerto del hub local de Memory.');
    }

    if (this.connection) {
      await this.disconnect();
    }

    // En local se usa el mismo equipo que sirve el frontend, con el puerto
    // de config/environment.json. Así también funciona al abrir la web
    // mediante la IP LAN de la computadora que ejecuta el backend.
    const {protocol, hostname} = window.location;
    const hubUrl = this.localMode
      ? `${protocol}//${hostname}:${environment.localHubPort}/hubs/memory-local`
      : `${environment.hubBaseUrl}/hubs/memory`;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
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

        if (!this.userId || !this.displayName) {
          throw new Error('Faltan los datos del jugador.');
        }

        await this.invokeJoin(this.matchId, this.userId, this.displayName);
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

  // Conexión competitiva: el usuario es el currentUser que entrega el Shell.
  public async joinMatch(
    matchId: string,
    userId: string,
    displayName: string
  ): Promise<void> {
    if (!this.connection || this.localMode) {
      throw new Error(
        'Memory no está conectado al modo competitivo.'
      );
    }

    await this.invokeJoin(matchId, userId, displayName);
  }

  // Conexión local: se juega sin el Shell.
  public async joinLocalMatch(
    matchId: string,
    userId: string,
    displayName: string
  ): Promise<void> {
    if (!this.connection || !this.localMode) {
      throw new Error('Memory no está conectado al modo local.');
    }

    await this.invokeJoin(matchId, userId, displayName);
  }

  private async invokeJoin(
    matchId: string,
    userId: string,
    displayName: string
  ): Promise<void> {
    if (!this.connection) {
      throw new Error('Memory no está conectado.');
    }

    this.matchId = matchId;
    this.userId = userId;
    this.displayName = displayName;

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

  public clearPlayer(): void {
    this.localMode = false;
    this.userId = null;
    this.displayName = null;
  }
}


// Prueba de ejecución del CI del frontend. IGNORAR
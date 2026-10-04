import {IEventAggregator, resolve, singleton} from 'aurelia';
import * as signalR from '@microsoft/signalr';
import environment from '../../config/environment.json';
import {
  PlayerJoinedMessage,
  GameReadyMessage,
  PreviewFinishedMessage,
  CardFlippedMessage,
  TurnTimeoutMessage
} from '../models/memory-hub.messages';

export const MemoryHubEvents = {
  PlayerJoined: 'memory:player-joined',
  GameReady: 'memory:game-ready',
  PreviewFinished: 'memory:preview-finished',
  CardFlipped: 'memory:card-flipped',
  TurnTimeout: 'memory:turn-timeout',
  ConnectionClosed: 'memory:connection-closed'
} as const;

@singleton()
export class MemoryHubService {
  private readonly ea = resolve(IEventAggregator);
  private connection: signalR.HubConnection | null = null;

  public async connect(): Promise<void> {
    if (this.connection) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubBaseUrl}/hubs/memory`)
      .withAutomaticReconnect()
      .build();

    this.connection.on('PlayerJoined', (msg: PlayerJoinedMessage) =>
      this.ea.publish(MemoryHubEvents.PlayerJoined, msg));

    this.connection.on('GameReady', (msg: GameReadyMessage) =>
      this.ea.publish(MemoryHubEvents.GameReady, msg));

    this.connection.on('PreviewFinished', (msg: PreviewFinishedMessage) =>
      this.ea.publish(MemoryHubEvents.PreviewFinished, msg));

    this.connection.on('CardFlipped', (msg: CardFlippedMessage) =>
      this.ea.publish(MemoryHubEvents.CardFlipped, msg));

    this.connection.on('TurnTimeout', (msg: TurnTimeoutMessage) =>
      this.ea.publish(MemoryHubEvents.TurnTimeout, msg));

    this.connection.onclose(() =>
      this.ea.publish(MemoryHubEvents.ConnectionClosed, null));

    await this.connection.start();
  }

  public async joinMatch(matchId: string, userId: string, displayName: string): Promise<void> {
    await this.connection?.invoke('JoinMatch', matchId, userId, displayName);
  }

  public async flipCard(matchId: string, userId: string, cardId: number): Promise<void> {
    await this.connection?.invoke('FlipCard', matchId, userId, cardId);
  }

  public async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = null;
  }
}

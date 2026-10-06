import {resolve} from 'aurelia';
import {MemoryGameOrchestrator} from '../../services/memory-game-orchestrator';
import {MemoryGameState, PlayerViewModel} from '../../state/memory-game-state';

export class GameBoard {
  private readonly orchestrator = resolve(MemoryGameOrchestrator);
  public readonly state = resolve(MemoryGameState);

  public get isCardDisabled(): boolean {
    return this.state.phase !== 'playing' || !this.state.isMyTurn || this.state.paused || !this.state.connectionReady;
  }

  public get myPlayer(): PlayerViewModel | null {
    return this.state.players.find(p => p.userId === this.state.myUserId) ?? null;
  }

  public get opponentPlayer(): PlayerViewModel | null {
    return this.state.players.find(p => p.userId !== this.state.myUserId) ?? null;
  }

  public get isMyTrayActive(): boolean {
    return this.myPlayer !== null && this.state.currentPlayerId === this.myPlayer.userId;
  }

  public get isOpponentTrayActive(): boolean {
    return this.opponentPlayer !== null && this.state.currentPlayerId === this.opponentPlayer.userId;
  }

  public onFlip(event: CustomEvent<{cardId: number}>): void {
    void this.orchestrator.flipCard(event.detail.cardId);
  }
}

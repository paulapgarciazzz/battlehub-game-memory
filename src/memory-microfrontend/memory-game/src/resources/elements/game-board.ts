import {autoinject} from 'aurelia-framework';
import {MemoryGameOrchestrator} from '../../services/memory-game-orchestrator';
import {MemoryGameState, PlayerViewModel} from '../../state/memory-game-state';

@autoinject()
export class GameBoard {
  constructor(private orchestrator: MemoryGameOrchestrator, public state: MemoryGameState) {}

  public get isCardDisabled(): boolean {
    return this.state.phase !== 'playing' || !this.state.isMyTurn;
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

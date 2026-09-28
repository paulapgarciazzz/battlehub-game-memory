import {autoinject} from 'aurelia-framework';
import {MemoryGameOrchestrator} from '../../services/memory-game-orchestrator';
import {MemoryGameState, PlayerViewModel} from '../../state/memory-game-state';

@autoinject()
export class ResultScreen {
  constructor(private orchestrator: MemoryGameOrchestrator, public state: MemoryGameState) {}

  public get winner(): PlayerViewModel | null {
    return this.state.players.find(p => p.userId === this.state.winnerUserId) ?? null;
  }

  public get amIWinner(): boolean {
    return this.state.winnerUserId === this.state.myUserId;
  }

  public async playAgain(): Promise<void> {
    await this.orchestrator.playAgain();
  }

  public retrySave(): void {
    void this.orchestrator.retrySaveResult();
  }
}

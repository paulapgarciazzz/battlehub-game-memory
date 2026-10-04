import {resolve} from 'aurelia';
import {MemoryGameOrchestrator} from '../../services/memory-game-orchestrator';
import {MemoryGameState, PlayerViewModel} from '../../state/memory-game-state';

export class ResultScreen {
  private readonly orchestrator = resolve(MemoryGameOrchestrator);
  public readonly state = resolve(MemoryGameState);

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

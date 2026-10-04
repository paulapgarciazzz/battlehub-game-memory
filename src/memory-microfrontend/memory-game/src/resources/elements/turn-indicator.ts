import {resolve} from 'aurelia';
import {MemoryGameState} from '../../state/memory-game-state';

const TICK_MS = 200;
const TURN_TOTAL_MS = 10_000;

export class TurnIndicator {
  public readonly state = resolve(MemoryGameState);
  public remainingSeconds = 10;
  public remainingRatio = 1;
  private intervalId: number | null = null;

  public attached(): void {
    this.intervalId = window.setInterval(() => this.tick(), TICK_MS);
    this.tick();
  }

  public detaching(): void {
    if (this.intervalId !== null) {
      window.clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }

  private tick(): void {
    if (!this.state.turnDeadline) {
      this.remainingSeconds = 10;
      this.remainingRatio = 1;
      return;
    }

    const remainingMs = Math.max(0, this.state.turnDeadline - Date.now());
    this.remainingSeconds = Math.ceil(remainingMs / 1000);
    this.remainingRatio = Math.max(0, Math.min(1, remainingMs / TURN_TOTAL_MS));
  }
}

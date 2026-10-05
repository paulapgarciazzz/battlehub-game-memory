import {resolve} from 'aurelia';
import {MemoryGameOrchestrator} from '../../services/memory-game-orchestrator';
import {MemoryGameState} from '../../state/memory-game-state';

export class JoinScreen {
  private readonly orchestrator = resolve(MemoryGameOrchestrator);
  public readonly state = resolve(MemoryGameState);

  public userId = '';
  public displayName = '';
  public matchId = '';
  public joining = false;
  public error: string | null = null;

  public generateMatchId(): void {
    this.matchId = Math.random().toString(36).slice(2, 8).toUpperCase();
  }

  public async join(event?: Event): Promise<void> {
    // Aurelia 2 no cancela el submit del formulario por su cuenta.
    event?.preventDefault();

    if (!this.userId.trim() || !this.displayName.trim() || !this.matchId.trim()) {
      this.error = 'Completá tu usuario, tu nombre y el código de partida.';
      return;
    }

    this.error = null;
    this.joining = true;

    try {
      await this.orchestrator.joinMatch(this.matchId.trim(), this.userId.trim(), this.displayName.trim());
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'No se pudo conectar con el servidor.';
    } finally {
      this.joining = false;
    }
  }
}

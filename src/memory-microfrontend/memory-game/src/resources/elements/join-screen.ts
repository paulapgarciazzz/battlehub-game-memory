import {autoinject} from 'aurelia-framework';
import {MemoryGameOrchestrator} from '../../services/memory-game-orchestrator';
import {MemoryGameState} from '../../state/memory-game-state';

@autoinject()
export class JoinScreen {
  public userId = '';
  public displayName = '';
  public matchId = '';
  public joining = false;
  public error: string | null = null;

  constructor(private orchestrator: MemoryGameOrchestrator, public state: MemoryGameState) {}

  public generateMatchId(): void {
    this.matchId = Math.random().toString(36).slice(2, 8).toUpperCase();
  }

  public async join(): Promise<void> {
    if (!this.userId.trim() || !this.displayName.trim() || !this.matchId.trim()) {
      this.error = 'Completá tu usuario, tu nombre y el código de partida.';
      return;
    }

    this.error = null;
    this.joining = true;

    try {
      await this.orchestrator.joinMatch(this.matchId.trim(), this.userId.trim(), this.displayName.trim());
    } catch {
      this.error = 'No se pudo conectar con el servidor. Intentá de nuevo.';
    } finally {
      this.joining = false;
    }
  }
}

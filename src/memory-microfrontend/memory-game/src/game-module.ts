import {resolve} from 'aurelia';
import type {GameContext, GameModule as IGameModule} from './game-contracts';
import {MemoryGameOrchestrator} from './services/memory-game-orchestrator';
import {MemoryGameState} from './state/memory-game-state';
import backgroundImage from './assets/backgrounds/background.webp';

/**
 * Punto de entrada del juego dentro del Shell (contrato 03, sección 6).
 * Se expone por Module Federation como "./GameModule". El Shell lo crea con
 * su contenedor, llama a initialize(context) y luego a start(), y lo muestra
 * con <au-compose>.
 *
 * El matchId y el usuario vienen del Shell (que los recibe de Matchmaking y
 * Auth0): aquí no hay pantalla de unión ni código de partida.
 */
export class GameModule implements IGameModule {
  public readonly state = resolve(MemoryGameState);
  public readonly backgroundStyle = `background-image: url(${backgroundImage})`;

  private readonly orchestrator = resolve(MemoryGameOrchestrator);
  private context: GameContext | null = null;
  private joined = false;

  public async initialize(context: GameContext): Promise<void> {
    if (!context?.matchId || !context.currentUser?.id || !context.currentUser.displayName) {
      throw new Error('El contexto del juego necesita matchId y currentUser (id y displayName).');
    }

    // Los servicios son singleton: se limpia lo que pudo quedar de una partida anterior.
    this.state.reset();
    this.state.embedded = true;
    this.context = context;
  }

  public async start(): Promise<void> {
    // El Shell también llama a start() al reanudar después de pause().
    if (this.joined) {
      this.state.paused = false;
      return;
    }

    if (!this.context) {
      throw new Error('Hay que llamar a initialize() antes de start().');
    }

    this.joined = true;

    try {
      await this.orchestrator.joinMatch(
        this.context.matchId,
        this.context.currentUser.id,
        this.context.currentUser.displayName);
    } catch (error) {
      this.joined = false;
      throw error;
    }
  }

  public async pause(): Promise<void> {
    // El juego es en tiempo real: el servidor no se detiene (el turno y su
    // temporizador siguen corriendo). Solo se bloquean las jugadas locales.
    this.state.paused = true;
  }

  public async dispose(): Promise<void> {
    this.joined = false;
    this.context = null;
    await this.orchestrator.leaveMatch();
    this.state.embedded = false;
  }
}

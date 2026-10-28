import {customElement, resolve} from 'aurelia';
import template from './game-module.html';
import './memory-app.css';
import {
  GAME_FINISHED_EVENT,
  type GameContext,
  type GameFinishedDetail,
  type GameModule as IGameModule
} from './game-contracts';
import {GameView} from './resources/elements/game-view';
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
 * Auth0): aquí no hay pantalla de unión ni código de partida. El juego no
 * tiene contacto con Auth0 ni con Matchmaking. Al terminar la partida emite
 * el evento DOM "battlehub:game-finished" y el Shell avisa a Matchmaking.
 *
 * El nombre del elemento lleva el prefijo del juego (ADR-003, sección 3). Con
 * @customElement explícito, las dependencias se declaran aquí y no con
 * <import> en la plantilla.
 */
@customElement({name: 'memory-game-module', template, dependencies: [GameView]})
export class GameModule implements IGameModule {
  // Aurelia lo asigna al mostrar el componente; da acceso al elemento host.
  // "declare": no crea el campo, para no pisar el que asigna Aurelia.
  declare public readonly $controller?: {host: HTMLElement};
  public readonly state = resolve(MemoryGameState);
  public readonly backgroundStyle = `background-image: url(${backgroundImage})`;

  private readonly orchestrator = resolve(MemoryGameOrchestrator);
  private context: GameContext | null = null;
  private joined = false;

  public async initialize(context: GameContext): Promise<void> {
    if (context?.gameType !== 'memory'
      || !context?.matchId
      || !context.currentUser?.id
      || !context.currentUser.displayName) {
      throw new Error('El contexto del juego necesita matchId y currentUser (id y displayName).');
    }

    this.orchestrator.configure(detail => this.emitGameFinished(detail));

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

  /**
   * Avisa al Shell que la partida terminó. El evento sube desde el elemento
   * del juego; si todavía no hay elemento, se emite en window.
   */
  private emitGameFinished(detail: GameFinishedDetail): void {
    const target: EventTarget = this.$controller?.host ?? window;

    target.dispatchEvent(new CustomEvent<GameFinishedDetail>(GAME_FINISHED_EVENT, {
      detail,
      bubbles: true,
      composed: true
    }));
  }

  public async dispose(): Promise<void> {
    this.joined = false;
    this.context = null;
    await this.orchestrator.leaveMatch();
    this.state.embedded = false;
  }
}

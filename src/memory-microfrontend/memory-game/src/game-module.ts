import {customElement, resolve} from 'aurelia';
import template from './game-module.html';
import './memory-app.css';
import {
  GAME_FINISHED_EVENT,
  type GameFinishedDetail,
  type GameLaunchContext,
  type GameModule as IGameModule,
  type PracticeContext
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
 * "battlehub:game-finished" en context.events y el Shell avisa a Matchmaking.
 *
 * Con mode: 'practice' el Shell abre el juego sin sala: se juega el modo solo
 * en el navegador, sin servidor, sin guardar nada y sin avisar el fin.
 *
 // El nombre del elemento utiliza el prefijo memory-game para evitar
// conflictos con otros componentes del Shell. Con @customElement
// explícito, las dependencias se declaran aquí.
 */
@customElement({name: 'memory-game-module', template, dependencies: [GameView]})
export class GameModule implements IGameModule {
  // Aurelia lo asigna al mostrar el componente; da acceso al elemento host.
  // "declare": no crea el campo, para no pisar el que asigna Aurelia.
  declare public readonly $controller?: {host: HTMLElement};
  public readonly state = resolve(MemoryGameState);
  public readonly backgroundStyle = `background-image: url(${backgroundImage})`;

  private readonly orchestrator = resolve(MemoryGameOrchestrator);
  private context: GameLaunchContext | null = null;
  private joined = false;

  public async initialize(context: GameLaunchContext): Promise<void> {
    if (context?.gameType !== 'memory'
      || !context.currentUser?.id
      || !context.currentUser.displayName) {
      throw new Error('El contexto del juego necesita gameType memory y currentUser (id y displayName).');
    }

    // Rechaza modos no definidos, incluso si el contexto llega desde JavaScript.
    const mode: unknown = 'mode' in context ? context.mode : undefined;

    if (mode !== undefined && mode !== 'practice') {
      throw new Error(`Memory no conoce el modo "${String(mode)}".`);
    }

    // Una partida normal necesita la sala; la práctica no tiene matchId.
    const isPractice = mode === 'practice';
    if (!isPractice && !context.matchId) {
      throw new Error('El contexto del juego necesita matchId y currentUser (id y displayName).');
    }

    if (!isPractice) {
      this.orchestrator.configure(detail => this.emitGameFinished(detail));
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

    if (isPracticeContext(this.context)) {
      // Sin sala ni servidor: el modo solo corre completo en el navegador.
      this.orchestrator.startSolo(this.context.currentUser.displayName, true);
      return;
    }

    try {
      // initialize() ya exigió matchId en una partida normal.
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
   * Avisa al Shell que la partida terminó. Se emite en context.events, que es
   * donde escucha el Shell, y también desde el elemento del juego (sube hasta
   * window) para el Shell que todavía no entrega el canal.
   */
  private emitGameFinished(detail: GameFinishedDetail): void {
    this.context?.events?.dispatchEvent(
      new CustomEvent<GameFinishedDetail>(GAME_FINISHED_EVENT, {detail}));

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

function isPracticeContext(context: GameLaunchContext): context is PracticeContext {
  return 'mode' in context && context.mode === 'practice';
}

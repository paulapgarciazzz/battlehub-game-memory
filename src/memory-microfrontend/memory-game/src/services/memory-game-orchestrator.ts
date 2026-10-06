import {IEventAggregator, resolve, singleton} from 'aurelia';
import {MemoryHubService, MemoryHubEvents} from './memory-hub.service';
import {MemoryHttpService} from './memory-http.service';
import {MemoryGameState} from '../state/memory-game-state';
import {
  PlayerJoinedMessage,
  GameReadyMessage,
  PreviewFinishedMessage,
  CardFlippedMessage,
  TurnTimeoutMessage,
  StateSnapshotMessage
} from '../models/memory-hub.messages';

const NO_MATCH_REVEAL_DELAY_MS = 900;
const TURN_TIMEOUT_MS = 10_000;

@singleton()
export class MemoryGameOrchestrator {
  private readonly ea = resolve(IEventAggregator);
  private readonly hub = resolve(MemoryHubService);
  private readonly http = resolve(MemoryHttpService);
  private readonly state = resolve(MemoryGameState);

  // Suscripciones al EventAggregator; se liberan en leaveMatch().
  private subscriptions: {dispose(): void}[] = [];

  // Temporizadores que ocultan las cartas falladas; se cancelan al salir.
  private revealTimers = new Set<number>();

  // Token para Matchmaking que entrega el Shell (ver GameModule.initialize).
  private roomToken: (() => Promise<string>) | null = null;

  /** Recibe los proveedores de tokens del Shell y se los pasa al hub y a la API. */
  public configure(token: () => Promise<string>, roomToken: () => Promise<string>): void {
    this.roomToken = roomToken;
    this.hub.configure(token, roomToken);
    this.http.configure(token);
  }

  constructor() {
    this.subscribe();
  }

  /** Se suscribe a los eventos del hub, una sola vez por partida. */
  private subscribe(): void {
    if (this.subscriptions.length) {
      return;
    }

    this.subscriptions.push(
      this.ea.subscribe(MemoryHubEvents.PlayerJoined, (msg: PlayerJoinedMessage) => this.onPlayerJoined(msg)),
      this.ea.subscribe(MemoryHubEvents.GameReady, (msg: GameReadyMessage) => this.onGameReady(msg)),
      this.ea.subscribe(MemoryHubEvents.PreviewFinished, (msg: PreviewFinishedMessage) => this.onPreviewFinished(msg)),
      this.ea.subscribe(MemoryHubEvents.CardFlipped, (msg: CardFlippedMessage) => this.onCardFlipped(msg)),
      this.ea.subscribe(MemoryHubEvents.TurnTimeout, (msg: TurnTimeoutMessage) => this.onTurnTimeout(msg)),
      this.ea.subscribe(MemoryHubEvents.StateSnapshot, (msg: StateSnapshotMessage) => this.onSnapshot(msg)),

      // El backend avisa que guardó el resultado: se confirma consultándolo.
      this.ea.subscribe(MemoryHubEvents.ResultSaved, (msg: {matchId: string}) => {
        if (msg.matchId === this.state.matchId) {
          void this.confirmResult();
        }
      }),

      this.ea.subscribe(MemoryHubEvents.ResultSaveFailed, (msg: {matchId: string}) => {
        if (msg.matchId === this.state.matchId) {
          this.state.resultSaveError = 'El servidor reintentará guardar el resultado.';
        }
      }),

      // Sin conexión no se permiten jugadas hasta recibir un StateSnapshot.
      this.ea.subscribe(MemoryHubEvents.ConnectionClosed, () => {
        this.state.connectionReady = false;
      })
    );
  }

  public async joinMatch(matchId: string, userId: string, displayName: string): Promise<void> {
    if (!this.roomToken) throw new Error('Abrir Memory desde el Shell con sus proveedores de tokens.');
    this.subscribe();
    this.state.matchId = matchId;
    this.state.myUserId = userId;
    this.state.myDisplayName = displayName;
    this.state.phase = 'waiting-for-opponent';
    this.state.paused = false;

    // JoinMatch en el servidor no resuelve hasta que termina la previsualización
    // completa (espera 5s internamente antes de retornar). Para cuando este await
    // se resuelve, la fase ya pudo haber avanzado a 'preview'/'playing' vía los
    // eventos del hub (GameReady/PreviewFinished) — por eso no se toca `phase`
    // después de este punto, para no pisar ese avance.
    await this.hub.connect();
    await this.hub.joinMatch(matchId);
  }

  public async flipCard(cardId: number): Promise<void> {
    const card = this.state.cards[cardId];

    if (this.state.paused || !this.state.connectionReady || !this.state.isMyTurn || !card || card.matched || card.faceUp) {
      return;
    }

    try {
      await this.hub.flipCard(this.state.matchId, cardId);
    } catch {
      // El backend es la autoridad: si rechaza la jugada, no hay nada más
      // que hacer del lado del cliente más que ignorar el intento inválido.
    }
  }

  public async playAgain(): Promise<void> {
    await this.leaveMatch();
  }

  /**
   * Sale de la partida: corta la conexión con el hub y limpia el estado.
   * La usa "Jugar de nuevo" (modo independiente) y GameModule.dispose()
   * (dentro del Shell), porque los servicios son singleton y sobreviven
   * entre una partida y la siguiente.
   */
  public async leaveMatch(): Promise<void> {
    for (const subscription of this.subscriptions) {
      subscription.dispose();
    }
    this.subscriptions = [];

    this.clearRevealTimers();

    await this.hub.disconnect();

    // No se conservan los proveedores de tokens de esta partida.
    this.hub.clearCredentials();
    this.http.configure(null);
    this.roomToken = null;

    this.state.reset();
  }

  private clearRevealTimers(): void {
    for (const timer of this.revealTimers) {
      window.clearTimeout(timer);
    }
    this.revealTimers.clear();
  }

  public async retrySaveResult(): Promise<void> {
    await this.saveResult();
  }

  private onPlayerJoined(msg: PlayerJoinedMessage): void {
    if (this.state.players.some(p => p.userId === msg.userId)) {
      return;
    }

    this.state.players = [
      ...this.state.players,
      {userId: msg.userId, displayName: msg.displayName, matchedPairs: 0}
    ];
  }

  private onGameReady(msg: GameReadyMessage): void {
    this.state.phase = 'preview';
    this.state.matchStartedAt = new Date();

    const valuesById = new Map(msg.cards.map(c => [c.id, c.value]));

    this.state.cards = this.state.cards.map(card => ({
      ...card,
      value: valuesById.get(card.id) ?? card.value,
      faceUp: true
    }));

    // Roster autoritativo del servidor: reemplaza lo que se haya acumulado
    // vía "PlayerJoined", que puede estar incompleto (ver comentario del
    // Hub) y llevaba a que cada pantalla calculara un turno inicial distinto.
    this.state.players = msg.players.map(p => ({
      userId: p.userId,
      displayName: p.displayName,
      matchedPairs: this.state.players.find(existing => existing.userId === p.userId)?.matchedPairs ?? 0
    }));
  }

  private onPreviewFinished(msg: PreviewFinishedMessage): void {
    this.state.phase = 'playing';
    this.state.cards = this.state.cards.map(card => ({...card, faceUp: card.matched}));
    this.state.currentPlayerId = msg.currentPlayerId;
    this.resetTurnDeadline(msg.turnDeadline);
  }

  private onCardFlipped(msg: CardFlippedMessage): void {
    const flippedIds = new Set(msg.cards.map(c => c.id));

    this.state.cards = this.state.cards.map(card => {
      const flipped = msg.cards.find(c => c.id === card.id);

      if (flipped) {
        return {...card, value: flipped.value, faceUp: true};
      }

      // El servidor nunca deja más de una carta sin pareja boca arriba a la
      // vez (la del turno anterior ya se resolvió, si no esta jugada no
      // podría estar pasando). Si acá aparece otra carta boca arriba y sin
      // matchear, es una huérfana de un evento que el cliente nunca terminó
      // de procesar (ej. una reconexión que se perdió el "CardFlipped" que
      // la tenía que ocultar). Se autolimpia para que el tablero no quede
      // trabado esperando un evento que ya no va a llegar.
      if (card.faceUp && !card.matched) {
        return {...card, faceUp: false};
      }

      return card;
    });

    if (msg.isMatch) {
      this.applyMatch(msg.userId, flippedIds);
      this.resetTurnDeadline(msg.turnDeadline);
      this.checkForGameOver();
      return;
    }

    if (msg.cards.length === 2) {
      // El turno lo decide el servidor y se aplica de una vez, para que el
      // contador no quede corriendo con el jugador anterior durante la animación.
      this.state.currentPlayerId = msg.currentPlayerId;
      this.resetTurnDeadline(msg.turnDeadline);

      // El setTimeout queda solo para dejar ver las cartas antes de ocultarlas.
      const timer = window.setTimeout(() => {
        this.revealTimers.delete(timer);
        this.hideUnmatchedCards(flippedIds);
      }, NO_MATCH_REVEAL_DELAY_MS);
      this.revealTimers.add(timer);
    }
  }

  private onTurnTimeout(msg: TurnTimeoutMessage): void {
    this.state.cards = this.state.cards.map(card =>
      card.matched ? card : {...card, faceUp: false});

    this.state.currentPlayerId = msg.currentPlayerId;
    this.resetTurnDeadline(msg.turnDeadline);
  }

  private applyMatch(userId: string, cardIds: Set<number>): void {
    this.state.cards = this.state.cards.map(card =>
      cardIds.has(card.id) ? {...card, matched: true, matchedByUserId: userId} : card);

    this.state.players = this.state.players.map(player =>
      player.userId === userId
        ? {...player, matchedPairs: player.matchedPairs + 1}
        : player);
  }

  private hideUnmatchedCards(cardIds: Set<number>): void {
    this.state.cards = this.state.cards.map(card =>
      cardIds.has(card.id) && !card.matched ? {...card, faceUp: false} : card);
  }

  // Usa el vencimiento que manda el servidor (TurnDeadline, UTC) para que el
  // contador coincida en las dos pantallas; si no viene, cuenta 10 s locales.
  private resetTurnDeadline(deadline?: string | null): void {
    this.state.turnDeadline = deadline ? Date.parse(deadline) : Date.now() + TURN_TIMEOUT_MS;
  }

  private checkForGameOver(): void {
    if (!this.state.isGameOver) {
      return;
    }

    this.state.phase = 'finished';

    const [first, second] = this.state.players;
    this.state.isDraw = first.matchedPairs === second.matchedPairs;
    this.state.winnerUserId = this.state.isDraw
      ? null
      : (first.matchedPairs > second.matchedPairs ? first.userId : second.userId);

    this.state.resultSaveError = 'Esperando confirmación del guardado del servidor.';
  }

  /**
   * Estado completo que manda el servidor al entrar o al reconectar. Reemplaza
   * todo el estado local: el servidor es la única fuente de verdad.
   */
  private onSnapshot(msg: StateSnapshotMessage): void {
    if (msg.matchId !== this.state.matchId) {
      return;
    }

    this.clearRevealTimers();

    this.state.phase = msg.phase;
    this.state.connectionReady = true;
    this.state.players = msg.players;
    this.state.cards = msg.cards.map(card => ({...card, matchedByUserId: null}));
    this.state.currentPlayerId = msg.currentPlayerId;
    this.state.matchStartedAt = new Date(msg.startedAt);
    this.state.turnDeadline = msg.turnDeadline ? Date.parse(msg.turnDeadline) : null;
    this.state.isDraw = msg.isDraw;
    this.state.winnerUserId = msg.winnerUserId;

    if (msg.phase === 'finished') {
      this.state.resultSaveError = msg.resultSaved ? null : 'El servidor reintentará guardar el resultado.';

      if (msg.resultSaved) {
        void this.confirmResult();
      }
    }
  }

  /**
   * La pantalla solo deja de mostrar el aviso cuando el resultado se puede
   * leer desde la API: no se da por guardado algo que no está confirmado.
   */
  private async confirmResult(): Promise<void> {
    const id = this.state.matchId;

    try {
      await this.http.getResult(id);

      if (this.state.matchId === id) {
        this.state.resultSaveError = null;
      }
    } catch {
      if (this.state.matchId === id) {
        this.state.resultSaveError = 'El resultado todavía no está confirmado en el servidor.';
      }
    }
  }

  private async saveResult(): Promise<void> {
    if (!this.state.matchStartedAt) {
      return;
    }

    try {
      await this.http.saveResult(this.state.matchId, this.state.matchStartedAt);
      this.state.resultSaveError = null;
    } catch (err) {
      this.state.resultSaveError = err instanceof Error ? err.message : 'No se pudo guardar el resultado.';
    }
  }
}

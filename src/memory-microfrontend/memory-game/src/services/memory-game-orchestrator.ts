
import {IEventAggregator, resolve, singleton} from 'aurelia';
import {MemoryHubService, MemoryHubEvents} from './memory-hub.service';
import {MemoryHttpService} from './memory-http.service';
import {MemoryGameState} from '../state/memory-game-state';
import type {GameFinishedDetail} from '../game-contracts';
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
const PRACTICE_PREVIEW_MS = 5_000;

@singleton()
export class MemoryGameOrchestrator {
  private readonly ea = resolve(IEventAggregator);
  private readonly hub = resolve(MemoryHubService);
  private readonly http = resolve(MemoryHttpService);
  private readonly state = resolve(MemoryGameState);

  // Suscripciones a eventos del servidor.
  private subscriptions: {dispose(): void}[] = [];

  // Temporizadores que se cancelan al salir o reiniciar.
  private revealTimers = new Set<number>();

  private localMode = false;

  // Aviso de fin de partida para el Shell (lo configura el GameModule).
  private gameFinished: ((detail: GameFinishedDetail) => void) | null = null;
  private notifiedMatchId: string | null = null;

  /** Configura el modo competitivo dentro del Shell. */
  public configure(onGameFinished: (detail: GameFinishedDetail) => void): void {
    this.localMode = false;
    this.gameFinished = onGameFinished;
    this.notifiedMatchId = null;

    this.hub.configure();
  }

  constructor() {
    this.subscribe();
  }

  /** Se suscribe a los eventos del servidor una sola vez. */
  private subscribe(): void {
    if (this.subscriptions.length) {
      return;
    }

    this.subscriptions.push(
      this.ea.subscribe(
        MemoryHubEvents.PlayerJoined,
        (msg: PlayerJoinedMessage) => this.onPlayerJoined(msg)
      ),
      this.ea.subscribe(
        MemoryHubEvents.GameReady,
        (msg: GameReadyMessage) => this.onGameReady(msg)
      ),
      this.ea.subscribe(
        MemoryHubEvents.PreviewFinished,
        (msg: PreviewFinishedMessage) => this.onPreviewFinished(msg)
      ),
      this.ea.subscribe(
        MemoryHubEvents.CardFlipped,
        (msg: CardFlippedMessage) => this.onCardFlipped(msg)
      ),
      this.ea.subscribe(
        MemoryHubEvents.TurnTimeout,
        (msg: TurnTimeoutMessage) => this.onTurnTimeout(msg)
      ),
      this.ea.subscribe(
        MemoryHubEvents.StateSnapshot,
        (msg: StateSnapshotMessage) => this.onSnapshot(msg)
      ),

      this.ea.subscribe(
        MemoryHubEvents.ResultSaved,
        (msg: {matchId: string}) => {
          if (!this.state.practiceMode && msg.matchId === this.state.matchId) {
            void this.confirmResult();
          }
        }
      ),

      this.ea.subscribe(
        MemoryHubEvents.ResultSaveFailed,
        (msg: {matchId: string}) => {
          if (!this.state.practiceMode && msg.matchId === this.state.matchId) {
            this.state.resultSaveError =
              'El servidor reintentará guardar el resultado.';
          }
        }
      ),

      this.ea.subscribe(MemoryHubEvents.ConnectionClosed, () => {
        if (!this.state.practiceMode) {
          this.state.connectionReady = false;
        }
      })
    );
  }

  // =========================================================
  // MODO COMPETITIVO
  // =========================================================

  public async joinMatch(
    matchId: string,
    userId: string,
    displayName: string
  ): Promise<void> {
    this.subscribe();

    this.state.practiceMode = false;
    this.state.practiceResolving = false;
    this.state.matchId = matchId;
    this.state.myUserId = userId;
    this.state.myDisplayName = displayName;
    this.state.phase = 'waiting-for-opponent';
    this.state.paused = false;
    this.state.connectionReady = true;
    

    // El servidor controla el comienzo y las fases de la partida.
    await this.hub.connect();
    await this.hub.joinMatch(matchId, userId, displayName);
  }

  // =========================================================
// MODO MULTIJUGADOR LOCAL: DOS COMPUTADORAS
// =========================================================

public async joinLocalMatch(
  matchId: string,
  userId: string,
  displayName: string
): Promise<void> {
  if (!matchId.trim() || !userId.trim() || !displayName.trim()) {
    throw new Error(
      'Completá tu nombre, tu usuario y el código de partida.'
    );
  }

  this.subscribe();

  this.clearRevealTimers();
  this.localMode = true;

  this.state.reset();

  this.state.practiceMode = false;
  this.state.practiceResolving = false;
  this.state.embedded = false;

  this.state.matchId = matchId.trim().toUpperCase();
  this.state.myUserId = userId.trim();
  this.state.myDisplayName = displayName.trim();

  this.state.phase = 'waiting-for-opponent';
  this.state.paused = false;
  this.state.connectionReady = false;
  this.state.resultSaveError = null;

  this.hub.configureLocal();

  await this.hub.connect();

  await this.hub.joinLocalMatch(
    this.state.matchId,
    this.state.myUserId,
    this.state.myDisplayName
  );
}

  // =========================================================
  // MODO SOLO
  // =========================================================

  /**
   * Inicia una partida individual sin servidor ni temporizador de turnos.
   */
  public startSolo(displayName: string): void {
    this.clearRevealTimers();
    this.state.reset();

    this.localMode = false;
    this.state.practiceMode = false;
    this.state.practiceResolving = false;
    this.state.soloMode = true;
    this.state.embedded = false;

    this.state.matchId = 'SOLO';
    this.state.myUserId = 'solo-1';
    this.state.myDisplayName = displayName.trim() || 'Jugador';

    this.state.players = [
      {
        userId: 'solo-1',
        displayName: this.state.myDisplayName,
        matchedPairs: 0
      }
    ];

    const values = [
      'A', 'A', 'B', 'B', 'C', 'C', 'D', 'D',
      'E', 'E', 'F', 'F', 'G', 'G', 'H', 'H'
    ];

    for (let i = values.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [values[i], values[j]] = [values[j], values[i]];
    }

    this.state.cards = values.map((value, id) => ({
      id,
      value,
      faceUp: false,
      matched: false,
      matchedByUserId: null
    }));

    this.state.currentPlayerId = 'solo-1';
    this.state.connectionReady = true;
    this.state.paused = false;
    this.state.turnDeadline = null;
    this.state.matchStartedAt = new Date();
    this.state.winnerUserId = null;
    this.state.isDraw = false;
    this.state.resultSaveError = null;
    this.state.phase = 'playing';
  }

  /**
   * Voltea cartas y busca parejas en el modo individual.
   */
  private flipSoloCard(cardId: number): void {
    if (
      !this.state.soloMode ||
      this.state.phase !== 'playing' ||
      this.state.paused ||
      this.state.practiceResolving
    ) {
      return;
    }

    const card = this.state.cards.find(item => item.id === cardId);

    if (!card || card.matched || card.faceUp) {
      return;
    }

    const firstCard = this.state.cards.find(
      item => item.faceUp && !item.matched
    );

    this.state.cards = this.state.cards.map(item =>
      item.id === cardId ? {...item, faceUp: true} : item
    );

    if (!firstCard) {
      return;
    }

    if (firstCard.value === card.value) {
      this.state.cards = this.state.cards.map(item =>
        item.id === firstCard.id || item.id === cardId
          ? {
              ...item,
              faceUp: true,
              matched: true,
              matchedByUserId: 'solo-1'
            }
          : item
      );

      this.state.players = this.state.players.map(player =>
        player.userId === 'solo-1'
          ? {...player, matchedPairs: player.matchedPairs + 1}
          : player
      );

      if (this.state.isGameOver) {
        this.state.phase = 'finished';
        this.state.turnDeadline = null;
        this.state.practiceResolving = false;
      }

      return;
    }

    this.state.practiceResolving = true;

    const timer = window.setTimeout(() => {
      this.revealTimers.delete(timer);

      if (
        !this.state.soloMode ||
        this.state.phase !== 'playing'
      ) {
        return;
      }

      this.state.cards = this.state.cards.map(item =>
        item.id === firstCard.id || item.id === cardId
          ? {...item, faceUp: false}
          : item
      );

      this.state.practiceResolving = false;
    }, NO_MATCH_REVEAL_DELAY_MS);

    this.revealTimers.add(timer);
  }
  // =========================================================
  // MODO PRÁCTICA LOCAL
  // =========================================================

  /**
   * Inicia una partida local entre dos jugadores.
   * No requiere Shell, tokens, Hub ni API.
   */
  public startPractice(firstName: string, secondName: string): void {
    this.clearRevealTimers();
    this.state.reset();

    this.state.practiceMode = true;
    this.state.practiceResolving = false;
    this.state.embedded = false;

    this.state.matchId = 'PRACTICA';
    this.state.myUserId = 'local-1';
    this.state.myDisplayName = firstName.trim() || 'Jugador 1';

    this.state.players = [
      {
        userId: 'local-1',
        displayName: this.state.myDisplayName,
        matchedPairs: 0
      },
      {
        userId: 'local-2',
        displayName: secondName.trim() || 'Jugador 2',
        matchedPairs: 0
      }
    ];

    // Ocho parejas: A-A, B-B, hasta H-H.
    const values = [
      'A', 'A', 'B', 'B', 'C', 'C', 'D', 'D',
      'E', 'E', 'F', 'F', 'G', 'G', 'H', 'H'
    ];

    // Mezclar las cartas aleatoriamente.
    for (let i = values.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [values[i], values[j]] = [values[j], values[i]];
    }

    this.state.cards = values.map((value, id) => ({
      id,
      value,
      faceUp: true,
      matched: false,
      matchedByUserId: null
    }));

    this.state.currentPlayerId = 'local-1';
    this.state.connectionReady = true;
    this.state.paused = false;
    this.state.turnDeadline = null;
    this.state.matchStartedAt = new Date();
    this.state.winnerUserId = null;
    this.state.isDraw = false;
    this.state.resultSaveError = null;
    this.state.phase = 'preview';

    // Mostrar las cartas durante cinco segundos.
    const timer = window.setTimeout(() => {
      this.revealTimers.delete(timer);

      if (
        !this.state.practiceMode ||
        this.state.phase !== 'preview'
      ) {
        return;
      }

      this.state.cards = this.state.cards.map(card => ({
        ...card,
        faceUp: false
      }));

      this.state.phase = 'playing';
    }, PRACTICE_PREVIEW_MS);

    this.revealTimers.add(timer);
  }

  /**
   * Procesa un clic de una carta en modo práctica.
   * En este modo no se envía ninguna jugada al servidor.
   */
  private flipPracticeCard(cardId: number): void {
    if (
      !this.state.practiceMode ||
      this.state.phase !== 'playing' ||
      this.state.paused ||
      this.state.practiceResolving
    ) {
      return;
    }

    const card = this.state.cards.find(item => item.id === cardId);

    if (!card || card.matched || card.faceUp) {
      return;
    }

    const firstCard = this.state.cards.find(
      item => item.faceUp && !item.matched
    );

    // Mostrar la carta seleccionada.
    this.state.cards = this.state.cards.map(item =>
      item.id === cardId
        ? {...item, faceUp: true}
        : item
    );

    // Es la primera carta del turno.
    if (!firstCard) {
      return;
    }

    const currentPlayerId = this.state.currentPlayerId;

    if (!currentPlayerId) {
      return;
    }

    const isMatch = firstCard.value === card.value;

    if (isMatch) {
      // Pareja correcta: se marca y se asigna al jugador actual.
      this.state.cards = this.state.cards.map(item =>
        item.id === firstCard.id || item.id === cardId
          ? {
              ...item,
              faceUp: true,
              matched: true,
              matchedByUserId: currentPlayerId
            }
          : item
      );

      this.state.players = this.state.players.map(player =>
        player.userId === currentPlayerId
          ? {...player, matchedPairs: player.matchedPairs + 1}
          : player
      );

      // Quien encuentra pareja conserva el turno.
      if (this.state.isGameOver) {
        this.finishPractice();
      }

      return;
    }

    // Pareja incorrecta: bloquear clics mientras se muestran las cartas.
    this.state.practiceResolving = true;

    const timer = window.setTimeout(() => {
      this.revealTimers.delete(timer);

      if (
        !this.state.practiceMode ||
        this.state.phase !== 'playing'
      ) {
        return;
      }

      // Ocultar las dos cartas que no coinciden.
      this.state.cards = this.state.cards.map(item =>
        item.id === firstCard.id || item.id === cardId
          ? {...item, faceUp: false}
          : item
      );

      // Cambiar al otro jugador.
      this.state.currentPlayerId =
        currentPlayerId === 'local-1'
          ? 'local-2'
          : 'local-1';

      this.state.practiceResolving = false;
    }, NO_MATCH_REVEAL_DELAY_MS);

    this.revealTimers.add(timer);
  }

  /** Finaliza la partida local sin guardar resultados en la API. */
  private finishPractice(): void {
    const [first, second] = this.state.players;

    if (!first || !second) {
      return;
    }

    this.state.phase = 'finished';
    this.state.turnDeadline = null;
    this.state.practiceResolving = false;
    this.state.resultSaveError = null;

    this.state.isDraw =
      first.matchedPairs === second.matchedPairs;

    this.state.winnerUserId = this.state.isDraw
      ? null
      : first.matchedPairs > second.matchedPairs
        ? first.userId
        : second.userId;
  }

  // =========================================================
  // JUGADAS: PRÁCTICA O COMPETITIVO
  // =========================================================

 public async flipCard(cardId: number): Promise<void> {
    if (this.state.soloMode) {
      this.flipSoloCard(cardId);
      return;
    }

    if (this.state.practiceMode) {
      this.flipPracticeCard(cardId);
      return;
    }

    // En competitivo, el servidor sigue siendo la autoridad.
    const card = this.state.cards[cardId];

    if (
      this.state.paused ||
      !this.state.connectionReady ||
      !this.state.isMyTurn ||
      !card ||
      card.matched ||
      card.faceUp
    ) {
      return;
    }

   try {
  console.log('[MEMORY] Enviando clic:', {
    cardId,
    matchId: this.state.matchId,
    phase: this.state.phase,
    practiceMode: this.state.practiceMode,
    myUserId: this.state.myUserId,
    currentPlayerId: this.state.currentPlayerId,
    isMyTurn: this.state.isMyTurn,
    connectionReady: this.state.connectionReady
  });

  await this.hub.flipCard(this.state.matchId, cardId);

  console.log('[MEMORY] Solicitud enviada correctamente.');
} catch (error) {
  console.error('[MEMORY] Error al voltear la carta:', error);
}
  }

  public async playAgain(): Promise<void> {
    await this.leaveMatch();
  }

  /**
   * Sale de la partida y limpia el estado.
   * Una práctica local no desconecta ni modifica las credenciales del Hub.
   */
 public async leaveMatch(): Promise<void> {
    const wasPractice =
      this.state.practiceMode || this.state.soloMode;

    for (const subscription of this.subscriptions) {
      subscription.dispose();
    }

    this.subscriptions = [];
    this.clearRevealTimers();

    if (!wasPractice) {
      await this.hub.disconnect();

      this.hub.clearPlayer();
      this.gameFinished = null;
      this.notifiedMatchId = null;
    }

    this.state.reset();
  }

  private clearRevealTimers(): void {
    for (const timer of this.revealTimers) {
      window.clearTimeout(timer);
    }

    this.revealTimers.clear();
  }

  // =========================================================
  // RESULTADOS COMPETITIVOS
  // =========================================================

  public async retrySaveResult(): Promise<void> {
    if (this.state.practiceMode) {
      return;
    }

    await this.saveResult();
  }

  private onPlayerJoined(msg: PlayerJoinedMessage): void {
    if (this.state.practiceMode) {
      return;
    }

    if (this.state.players.some(p => p.userId === msg.userId)) {
      return;
    }

    this.state.players = [
      ...this.state.players,
      {
        userId: msg.userId,
        displayName: msg.displayName,
        matchedPairs: 0
      }
    ];
  }

  private onGameReady(msg: GameReadyMessage): void {
    if (this.state.practiceMode) {
      return;
    }

    this.state.phase = 'preview';
    this.state.matchStartedAt = new Date();

    const valuesById = new Map(
      msg.cards.map(card => [card.id, card.value])
    );

    this.state.cards = this.state.cards.map(card => ({
      ...card,
      value: valuesById.get(card.id) ?? card.value,
      faceUp: true
    }));

    // Lista de jugadores oficial del servidor.
    this.state.players = msg.players.map(player => ({
      userId: player.userId,
      displayName: player.displayName,
      matchedPairs:
        this.state.players.find(
          existing => existing.userId === player.userId
        )?.matchedPairs ?? 0
    }));
  }

 private onPreviewFinished(msg: PreviewFinishedMessage): void {
  if (this.state.practiceMode) {
    return;
  }

  this.state.phase = 'playing';
  this.state.connectionReady = true;

  this.state.cards = this.state.cards.map(card => ({
    ...card,
    faceUp: card.matched
  }));

  this.state.currentPlayerId = msg.currentPlayerId;
  this.resetTurnDeadline(msg.turnDeadline);
}

  private onCardFlipped(msg: CardFlippedMessage): void {
    if (this.state.practiceMode) {
      return;
    }

    const flippedIds = new Set(msg.cards.map(card => card.id));

    this.state.cards = this.state.cards.map(card => {
      const flipped = msg.cards.find(item => item.id === card.id);

      if (flipped) {
        return {
          ...card,
          value: flipped.value,
          faceUp: true
        };
      }

      // Limpiar cartas no emparejadas que hayan quedado descubiertas.
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
      this.state.currentPlayerId = msg.currentPlayerId;
      this.resetTurnDeadline(msg.turnDeadline);

      const timer = window.setTimeout(() => {
        this.revealTimers.delete(timer);
        this.hideUnmatchedCards(flippedIds);
      }, NO_MATCH_REVEAL_DELAY_MS);

      this.revealTimers.add(timer);
    }
  }

  private onTurnTimeout(msg: TurnTimeoutMessage): void {
    if (this.state.practiceMode) {
      return;
    }

    this.state.cards = this.state.cards.map(card =>
      card.matched
        ? card
        : {...card, faceUp: false}
    );

    this.state.currentPlayerId = msg.currentPlayerId;
    this.resetTurnDeadline(msg.turnDeadline);
  }

  private applyMatch(userId: string, cardIds: Set<number>): void {
    this.state.cards = this.state.cards.map(card =>
      cardIds.has(card.id)
        ? {
            ...card,
            matched: true,
            matchedByUserId: userId
          }
        : card
    );

    this.state.players = this.state.players.map(player =>
      player.userId === userId
        ? {...player, matchedPairs: player.matchedPairs + 1}
        : player
    );
  }

  private hideUnmatchedCards(cardIds: Set<number>): void {
    this.state.cards = this.state.cards.map(card =>
      cardIds.has(card.id) && !card.matched
        ? {...card, faceUp: false}
        : card
    );
  }

  private resetTurnDeadline(deadline?: string | null): void {
    this.state.turnDeadline = deadline
      ? Date.parse(deadline)
      : Date.now() + TURN_TIMEOUT_MS;
  }

  private checkForGameOver(): void {
    if (!this.state.isGameOver) {
      return;
    }

    this.state.phase = 'finished';

    const [first, second] = this.state.players;

    if (!first || !second) {
      return;
    }

    this.state.isDraw =
      first.matchedPairs === second.matchedPairs;

    this.state.winnerUserId = this.state.isDraw
      ? null
      : first.matchedPairs > second.matchedPairs
        ? first.userId
        : second.userId;

  this.state.resultSaveError = this.localMode
  ? null
  : 'Esperando confirmación del guardado del servidor.';

    this.notifyGameFinished();
  }

  /** Actualiza el estado con la información oficial del servidor. */
 private onSnapshot(msg: StateSnapshotMessage): void {
  if (
    this.state.practiceMode ||
    msg.matchId !== this.state.matchId
  ) {
    return;
  }
    

    this.clearRevealTimers();

    this.state.phase = msg.phase;
    this.state.connectionReady = true;
    this.state.players = msg.players;

    this.state.cards = msg.cards.map(card => ({
      ...card,
      matchedByUserId: null
    }));

    this.state.currentPlayerId = msg.currentPlayerId;
    this.state.matchStartedAt = new Date(msg.startedAt);
    this.state.turnDeadline = msg.turnDeadline
      ? Date.parse(msg.turnDeadline)
      : null;

    this.state.isDraw = msg.isDraw;
    this.state.winnerUserId = msg.winnerUserId;

    if (msg.phase === 'finished') {
      this.state.resultSaveError = msg.resultSaved
        ? null
        : 'El servidor reintentará guardar el resultado.';

      if (msg.resultSaved) {
        void this.confirmResult();
      }

      this.notifyGameFinished();
    }
  }

  /**
   * Avisa al Shell que la partida terminó, una sola vez por partida. Solo
   * aplica dentro del Shell: el modo local, solo y práctica no avisan.
   */
  private notifyGameFinished(): void {
    if (
      !this.gameFinished ||
      !this.state.embedded ||
      this.localMode ||
      this.state.practiceMode ||
      this.notifiedMatchId === this.state.matchId
    ) {
      return;
    }

    this.notifiedMatchId = this.state.matchId;

    this.gameFinished({
      matchId: this.state.matchId,
      gameType: 'memory',
      isDraw: this.state.isDraw,
      winnerUserId: this.state.winnerUserId,
      players: this.state.players.map(player => ({
        userId: player.userId,
        displayName: player.displayName,
        score: player.matchedPairs
      }))
    });
  }

  /** Comprueba que el resultado competitivo exista en la API. */
  private async confirmResult(): Promise<void> {
    if (this.localMode || this.state.practiceMode) {
  return;
}
    const id = this.state.matchId;

    try {
      await this.http.getResult(id);

      if (
        !this.state.practiceMode &&
        this.state.matchId === id
      ) {
        this.state.resultSaveError = null;
      }
    } catch {
      if (
        !this.state.practiceMode &&
        this.state.matchId === id
      ) {
        this.state.resultSaveError =
          'El resultado todavía no está confirmado en el servidor.';
      }
    }
  }

  private async saveResult(): Promise<void> {
    if (
      this.state.practiceMode ||
      !this.state.matchStartedAt
    ) {
      return;
    }

    try {
      await this.http.saveResult(
        this.state.matchId,
        this.state.matchStartedAt
      );

      this.state.resultSaveError = null;
    } catch (err) {
      this.state.resultSaveError =
        err instanceof Error
          ? err.message
          : 'No se pudo guardar el resultado.';
    }
  }
}

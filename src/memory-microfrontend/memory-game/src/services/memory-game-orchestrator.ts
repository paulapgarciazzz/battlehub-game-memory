import {IEventAggregator, resolve, singleton} from 'aurelia';
import {MemoryHubService, MemoryHubEvents} from './memory-hub.service';
import {MemoryHttpService} from './memory-http.service';
import {MemoryGameState} from '../state/memory-game-state';
import {
  PlayerJoinedMessage,
  GameReadyMessage,
  PreviewFinishedMessage,
  CardFlippedMessage,
  TurnTimeoutMessage
} from '../models/memory-hub.messages';

const NO_MATCH_REVEAL_DELAY_MS = 900;
const TURN_TIMEOUT_MS = 10_000;

@singleton()
export class MemoryGameOrchestrator {
  private readonly ea = resolve(IEventAggregator);
  private readonly hub = resolve(MemoryHubService);
  private readonly http = resolve(MemoryHttpService);
  private readonly state = resolve(MemoryGameState);

  constructor() {
    this.ea.subscribe(MemoryHubEvents.PlayerJoined, (msg: PlayerJoinedMessage) => this.onPlayerJoined(msg));
    this.ea.subscribe(MemoryHubEvents.GameReady, (msg: GameReadyMessage) => this.onGameReady(msg));
    this.ea.subscribe(MemoryHubEvents.PreviewFinished, (msg: PreviewFinishedMessage) => this.onPreviewFinished(msg));
    this.ea.subscribe(MemoryHubEvents.CardFlipped, (msg: CardFlippedMessage) => this.onCardFlipped(msg));
    this.ea.subscribe(MemoryHubEvents.TurnTimeout, (msg: TurnTimeoutMessage) => this.onTurnTimeout(msg));
  }

  public async joinMatch(matchId: string, userId: string, displayName: string): Promise<void> {
    this.state.matchId = matchId;
    this.state.myUserId = userId;
    this.state.myDisplayName = displayName;
    this.state.phase = 'waiting-for-opponent';

    // JoinMatch en el servidor no resuelve hasta que termina la previsualización
    // completa (espera 5s internamente antes de retornar). Para cuando este await
    // se resuelve, la fase ya pudo haber avanzado a 'preview'/'playing' vía los
    // eventos del hub (GameReady/PreviewFinished) — por eso no se toca `phase`
    // después de este punto, para no pisar ese avance.
    await this.hub.connect();
    await this.hub.joinMatch(matchId, userId, displayName);
  }

  public async flipCard(cardId: number): Promise<void> {
    const card = this.state.cards[cardId];

    if (!this.state.isMyTurn || !card || card.matched || card.faceUp) {
      return;
    }

    try {
      await this.hub.flipCard(this.state.matchId, this.state.myUserId, cardId);
    } catch {
      // El backend es la autoridad: si rechaza la jugada, no hay nada más
      // que hacer del lado del cliente más que ignorar el intento inválido.
    }
  }

  public async playAgain(): Promise<void> {
    await this.hub.disconnect();
    this.state.reset();
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
    this.resetTurnDeadline();
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
      this.resetTurnDeadline();
      this.checkForGameOver();
      return;
    }

    if (msg.cards.length === 2) {
      // El turno lo decide el servidor y se aplica de una vez, para que el
      // contador no quede corriendo con el jugador anterior durante la animación.
      this.state.currentPlayerId = msg.currentPlayerId;
      this.resetTurnDeadline();

      // El setTimeout queda solo para dejar ver las cartas antes de ocultarlas.
      window.setTimeout(() => this.hideUnmatchedCards(flippedIds), NO_MATCH_REVEAL_DELAY_MS);
    }
  }

  private onTurnTimeout(msg: TurnTimeoutMessage): void {
    this.state.cards = this.state.cards.map(card =>
      card.matched ? card : {...card, faceUp: false});

    this.state.currentPlayerId = msg.currentPlayerId;
    this.resetTurnDeadline();
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

  private resetTurnDeadline(): void {
    this.state.turnDeadline = Date.now() + TURN_TIMEOUT_MS;
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

    this.saveResult();
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

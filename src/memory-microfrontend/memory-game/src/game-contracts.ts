// Contrato 03 y contrato de integración acordado con el Shell en el PR #6
// (commit 829d9bc). El juego no tiene contacto con Auth0.
export interface GameContext {
  matchId: string;
  gameType: string;
  currentUser: { id: string; displayName: string };
  /** Canal local opcional de eventos; no contiene credenciales. */
  events?: EventTarget;
}

/** Contexto de práctica: no representa una sala ni una partida persistida. */
export interface PracticeContext {
  mode: 'practice';
  matchId?: never;
  gameType: string;
  currentUser: { id: string; displayName: string };
  events: EventTarget;
}

/** Contexto que initialize() puede recibir desde el Shell. */
export type GameLaunchContext = GameContext | PracticeContext;

export interface GameModule {
  initialize(context: GameLaunchContext): Promise<void>;
  start(): Promise<void>;
  pause(): Promise<void>;
  dispose(): Promise<void>;
}

/**
 * Evento que el juego emite al terminar una partida normal (nunca en práctica).
 * Se emite en context.events, que es donde lo escucha el Shell, y también desde
 * el elemento del juego (sube hasta window). El Shell es quien avisa a
 * Matchmaking; el juego no lo llama.
 */
export const GAME_FINISHED_EVENT = 'battlehub:game-finished';

/**
 * El Shell solo usa matchId y gameType para saber de qué partida es el aviso.
 * El resto son datos extra del juego: el Shell no los procesa ni los guarda.
 */
export interface GameFinishedDetail {
  matchId: string;
  gameType: 'memory';
  isDraw: boolean;
  winnerUserId: string | null;
  players: { userId: string; displayName: string; score: number }[];
}

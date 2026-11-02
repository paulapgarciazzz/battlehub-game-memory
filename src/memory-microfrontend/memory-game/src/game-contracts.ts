// Tipos del contrato con el Shell: contrato 03 (secciones 5 y 6) más lo acordado
// con el Equipo 3 en el PR #6 del Shell (commit 829d9bc): canal events y modo
// práctica. El juego no tiene contacto con Auth0: el Shell ya autenticó al
// usuario y entrega su id.

/**
 * Contexto que entrega el Shell. Partida normal: trae matchId y no trae mode.
 * Práctica: trae mode: 'practice' y no trae matchId (no hay sala).
 */
export interface GameContext {
  /** Sala de Matchmaking. Obligatorio en una partida; no viene en práctica. */
  matchId?: string;
  gameType: 'typing' | 'trivia' | 'memory';
  currentUser: { id: string; displayName: string };
  /** Canal que el Shell crea para esta partida. No contiene tokens. */
  events?: EventTarget;
  /** Solo llega con 'practice'. Sin mode es una partida normal. */
  mode?: 'practice';
}

/** Práctica: el Shell abre el juego sin sala. No se guarda nada. */
export type PracticeContext = GameContext & { mode: 'practice' };

/** Mismo nombre que usa el Shell para el contexto que recibe initialize(). */
export type GameLaunchContext = GameContext;

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

// Tipos del contrato 03-contratos-tecnicos.md (secciones 5 y 6). El juego no
// tiene contacto con Auth0: el Shell ya autenticó al usuario y entrega su id.
export interface GameContext {
  matchId: string;
  gameType: 'typing' | 'trivia' | 'memory';
  currentUser: { id: string; displayName: string };
}

export interface GameModule {
  initialize(context: GameContext): Promise<void>;
  start(): Promise<void>;
  pause(): Promise<void>;
  dispose(): Promise<void>;
}

/**
 * Evento DOM que el juego emite al terminar una partida. Sube (bubbles) desde
 * el elemento del juego, así que el Shell puede escucharlo en el área del
 * juego o en window. El Shell es quien avisa a Matchmaking; el juego no lo llama.
 */
export const GAME_FINISHED_EVENT = 'battlehub:game-finished';

export interface GameFinishedDetail {
  matchId: string;
  gameType: 'memory';
  isDraw: boolean;
  winnerUserId: string | null;
  players: { userId: string; displayName: string; score: number }[];
}

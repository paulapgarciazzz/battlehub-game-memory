// Tipos del contrato 03-contratos-tecnicos.md (secciones 5 y 6), iguales a los
// de battlehub-shell/src/games/game-contracts.ts.
export interface GameContext {
  matchId: string;
  gameType: 'typing' | 'trivia' | 'memory' | string;
  currentUser: { id: string; displayName: string };
}

export interface GameModule {
  initialize(context: GameContext): Promise<void>;
  start(): Promise<void>;
  pause(): Promise<void>;
  dispose(): Promise<void>;
}

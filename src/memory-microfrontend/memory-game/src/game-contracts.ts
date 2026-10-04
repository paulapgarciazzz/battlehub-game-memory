// Tipos del contrato 03-contratos-tecnicos.md (secciones 5 y 6). Copia idéntica en el Shell y en los juegos.
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

// Tipos del contrato 03-contratos-tecnicos.md (secciones 5 y 6). Las funciones de tokens son extensiones locales acordadas para integración.
export interface GameContext {
  matchId: string;
  gameType: 'typing' | 'trivia' | 'memory';
  currentUser: { id: string; displayName: string };
  getAccessToken?: () => Promise<string>;
  getMatchmakingAccessToken?: () => Promise<string>;
}

export interface GameModule {
  initialize(context: GameContext): Promise<void>;
  start(): Promise<void>;
  pause(): Promise<void>;
  dispose(): Promise<void>;
}

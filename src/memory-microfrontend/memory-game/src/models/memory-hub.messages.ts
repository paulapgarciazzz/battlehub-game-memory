export interface PlayerJoinedMessage {
  userId: string;
  displayName: string;
}

export interface CardSnapshot {
  id: number;
  value: string;
}

export interface GameReadyMessage {
  matchId: string;
  previewSeconds: number;
  cards: CardSnapshot[];
  players: PlayerJoinedMessage[];
}

export interface PreviewFinishedMessage {
  matchId: string;
  currentPlayerId: string;
  turnDeadline?: string | null;
}

export interface CardFlippedMessage {
  userId: string;
  cards: CardSnapshot[];
  isMatch: boolean;
  currentPlayerId: string;
  turnDeadline?: string | null;
}

export interface TurnTimeoutMessage {
  matchId: string;
  previousPlayerId: string;
  currentPlayerId: string;
  turnDeadline?: string | null;
  turnTimeoutSeconds: number;
}

export interface StateSnapshotMessage {
  matchId: string;
  phase: 'preview' | 'playing' | 'finished';
  startedAt: string;
  currentPlayerId: string;
  turnDeadline: string | null;
  players: {userId: string; displayName: string; matchedPairs: number}[];
  cards: {id: number; value: string | null; faceUp: boolean; matched: boolean}[];
  resultSaved: boolean;
  isDraw: boolean;
  winnerUserId: string | null;
}

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
}

export interface CardFlippedMessage {
  userId: string;
  cards: CardSnapshot[];
  isMatch: boolean;
  currentPlayerId: string;
}

export interface TurnTimeoutMessage {
  matchId: string;
  previousPlayerId: string;
  currentPlayerId: string;
  turnTimeoutSeconds: number;
}

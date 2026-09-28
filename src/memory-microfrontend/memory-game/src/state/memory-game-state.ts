import {singleton} from 'aurelia-framework';

export type GamePhase = 'join' | 'waiting-for-opponent' | 'preview' | 'playing' | 'finished';

export interface CardViewModel {
  id: number;
  value: string | null;
  faceUp: boolean;
  matched: boolean;
  matchedByUserId: string | null;
}

export interface PlayerViewModel {
  userId: string;
  displayName: string;
  matchedPairs: number;
}

export const TOTAL_PAIRS = 8;

@singleton()
export class MemoryGameState {
  public phase: GamePhase = 'join';
  public matchId = '';
  public myUserId = '';
  public myDisplayName = '';
  public cards: CardViewModel[] = MemoryGameState.emptyBoard();
  public players: PlayerViewModel[] = [];
  public currentPlayerId: string | null = null;
  public turnDeadline: number | null = null;
  public matchStartedAt: Date | null = null;
  public winnerUserId: string | null = null;
  public isDraw = false;
  public resultSaveError: string | null = null;

  public get isMyTurn(): boolean {
    return this.currentPlayerId === this.myUserId;
  }

  public get totalPairsFound(): number {
    return this.players.reduce((sum, p) => sum + p.matchedPairs, 0);
  }

  public get isGameOver(): boolean {
    return this.totalPairsFound >= TOTAL_PAIRS;
  }

  public get currentPlayer(): PlayerViewModel | null {
    return this.players.find(p => p.userId === this.currentPlayerId) ?? null;
  }

  public get opponentOf(): (userId: string) => PlayerViewModel | null {
    return (userId: string) => this.players.find(p => p.userId !== userId) ?? null;
  }

  public reset(): void {
    this.phase = 'join';
    this.matchId = '';
    this.cards = MemoryGameState.emptyBoard();
    this.players = [];
    this.currentPlayerId = null;
    this.turnDeadline = null;
    this.matchStartedAt = null;
    this.winnerUserId = null;
    this.isDraw = false;
    this.resultSaveError = null;
  }

  private static emptyBoard(): CardViewModel[] {
    return Array.from({length: 16}, (_, id) => ({
      id,
      value: null,
      faceUp: false,
      matched: false,
      matchedByUserId: null
    }));
  }
}

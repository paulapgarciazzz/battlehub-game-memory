export interface MemoryGameHistoryDto {
  matchId: string;
  gameType: string;
  startedAt: string;
  finishedAt: string;
  winnerUserId: string | null;
  isDraw: boolean;
  score: number;
}

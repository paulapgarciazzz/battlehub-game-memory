import {resolve} from 'aurelia';
import {MemoryGameState} from '../../state/memory-game-state';

/**
 * Tablero o resultado según la fase de la partida. Lo usan tanto la app
 * independiente (memory-standalone) como GameModule dentro del Shell.
 */
export class GameView {
  public readonly state = resolve(MemoryGameState);
}

import {resolve} from 'aurelia';
import {MemoryGameState} from './state/memory-game-state';
import backgroundImage from './assets/backgrounds/background.webp';

/**
 * Raíz del modo independiente (solo para desarrollo local en
 * http://localhost:8080): se entra con un código de partida escrito a mano.
 * Dentro de BattleHub, el Shell carga GameModule y le pasa el matchId.
 */
export class MemoryStandalone {
  public readonly state = resolve(MemoryGameState);
  public readonly backgroundStyle = `background-image: url(${backgroundImage})`;
}

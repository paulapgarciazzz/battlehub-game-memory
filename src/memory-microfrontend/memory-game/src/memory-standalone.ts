
import { resolve } from 'aurelia';
import { MemoryGameState } from './state/memory-game-state';
import backgroundImage from './assets/backgrounds/background.webp';

/**
 * Pantalla independiente para jugar desde el formulario de partida.
 * No modifica la integración con el Shell de BattleHub.
 */
export class MemoryStandalone {
  public readonly state = resolve(MemoryGameState);

  public readonly backgroundStyle =
    `background-image: url(${backgroundImage})`;
}

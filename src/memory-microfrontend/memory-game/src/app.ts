import {autoinject} from 'aurelia-framework';
import {MemoryGameState} from './state/memory-game-state';
import backgroundImage from './assets/backgrounds/background.webp';

@autoinject()
export class App {
  public backgroundStyle = `background-image: url(${backgroundImage})`;

  constructor(public state: MemoryGameState) {}
}

import {bindable} from 'aurelia';
import {PlayerViewModel} from '../../state/memory-game-state';

export class PlayerTray {
  @bindable public player: PlayerViewModel | null = null;
  @bindable public isCurrentTurn = false;
  @bindable public mirrored = false;
}

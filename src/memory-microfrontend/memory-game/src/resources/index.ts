import {FrameworkConfiguration} from 'aurelia-framework';
import {PLATFORM} from 'aurelia-pal';

export function configure(config: FrameworkConfiguration): void {
  config.globalResources([
    PLATFORM.moduleName('resources/elements/join-screen'),
    PLATFORM.moduleName('resources/elements/game-board'),
    PLATFORM.moduleName('resources/elements/memory-card'),
    PLATFORM.moduleName('resources/elements/turn-indicator'),
    PLATFORM.moduleName('resources/elements/player-tray'),
    PLATFORM.moduleName('resources/elements/result-screen')
  ]);
}

// Ejecución independiente del juego (solo para desarrollo local del equipo).
// Dentro de BattleHub no se usa: el Shell carga ./GameModule por Module Federation.
import Aurelia from 'aurelia';
import {MemoryStandalone} from './memory-standalone';

void Aurelia
  .app({
    host: document.querySelector('memory-standalone') as HTMLElement,
    component: MemoryStandalone
  })
  .start();

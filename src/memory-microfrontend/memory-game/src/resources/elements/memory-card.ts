import {autoinject, bindable} from 'aurelia-framework';
import {CardViewModel} from '../../state/memory-game-state';
import {CARD_BACK_IMAGE, imageForValue} from '../../services/card-image.map';

@autoinject()
export class MemoryCard {
  @bindable public card!: CardViewModel;
  @bindable public disabled = false;

  constructor(private element: Element) {}

  public get imageSrc(): string {
    if (this.card.faceUp && this.card.value) {
      return imageForValue(this.card.value);
    }

    return CARD_BACK_IMAGE;
  }

  public onClick(): void {
    if (this.disabled || this.card.matched || this.card.faceUp) {
      return;
    }

    this.element.dispatchEvent(new CustomEvent('flip', {
      detail: {cardId: this.card.id},
      bubbles: true
    }));
  }
}

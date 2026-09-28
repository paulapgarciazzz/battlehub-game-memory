import back from '../assets/cards/back.webp';
import card01 from '../assets/cards/card-01.webp';
import card02 from '../assets/cards/card-02.webp';
import card03 from '../assets/cards/card-03.webp';
import card04 from '../assets/cards/card-04.webp';
import card05 from '../assets/cards/card-05.webp';
import card06 from '../assets/cards/card-06.webp';
import card07 from '../assets/cards/card-07.webp';
import card08 from '../assets/cards/card-08.webp';

export const CARD_BACK_IMAGE = back;

const VALUE_TO_IMAGE: Record<string, string> = {
  A: card01,
  B: card02,
  C: card03,
  D: card04,
  E: card05,
  F: card06,
  G: card07,
  H: card08
};

export function imageForValue(value: string): string {
  const image = VALUE_TO_IMAGE[value];

  if (!image) {
    throw new Error(`Valor de carta desconocido: ${value}`);
  }

  return image;
}

import development from '../../../config/environment.json';
import production from '../../../config/environment.production.json';

// Jest solo carga environment.json: el de producción lo elige webpack. Si le
// faltara un tiempo, setTimeout(undefined) ocultaría las cartas al instante.
describe.each([
  ['environment.json', development],
  ['environment.production.json', production]
])('%s', (_name, environment) => {
  it.each(['turnTimeoutMs', 'noMatchRevealDelayMs', 'practicePreviewMs'] as const)(
    'define %s como un número positivo',
    key => {
      expect(environment[key]).toBeGreaterThan(0);
    }
  );
});

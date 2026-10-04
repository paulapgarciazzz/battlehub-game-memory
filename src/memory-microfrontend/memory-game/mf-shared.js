// Dependencias compartidas con el Shell (copia de battlehub-shell/tooling/mf-shared.js, ADR-003).
// Si el Shell cambia esta lista o la versión, hay que actualizarla aquí también:
// con strictVersion, una versión distinta de Aurelia impide cargar el juego en el Shell.
const AURELIA_VERSION = '2.0.0-rc.2';
const pkgs = [
  'aurelia',
  '@aurelia/kernel',
  '@aurelia/metadata',
  '@aurelia/platform',
  '@aurelia/platform-browser',
  '@aurelia/expression-parser',
  '@aurelia/template-compiler',
  '@aurelia/runtime',
  '@aurelia/runtime-html',
];
module.exports = Object.fromEntries(pkgs.map(p => [p, {
  singleton: true,
  strictVersion: true,
  requiredVersion: AURELIA_VERSION,
}]));

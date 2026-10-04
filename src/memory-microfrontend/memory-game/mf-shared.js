// ADR-003 §3 — Dependencias compartidas. Este archivo es IDÉNTICO en el Shell y en los 3 juegos: no lo modifiquen.
// Si usan otro paquete @aurelia/* (router, fetch-client, validation...), agréguenlo a la lista y avisen al Equipo 3.
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

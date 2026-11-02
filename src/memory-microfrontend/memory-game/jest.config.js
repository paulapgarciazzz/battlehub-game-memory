// Pruebas del frontend separadas en dos proyectos de Jest:
// - unit: test/unit, cada componente o servicio por separado, con fakes.
// - integration: test/integration, el GameModule completo como lo usa el Shell.
// "npm test" corre los dos y mide la cobertura total.
const shared = {
  testEnvironment: 'jsdom',
  setupFilesAfterEnv: ['<rootDir>/test/setup.ts'],
  transform: {
    '\\.(css|sass|scss|styl|jpg|jpeg|png|gif|eot|otf|webp|svg|ttf|woff|woff2|mp4|webm|wav|mp3|m4a|aac|oga)$': 'jest-transform-stub',
    '\\.(ts|html)$': '@aurelia/ts-jest'
  }
};

module.exports = {
  projects: [
    {...shared, displayName: 'unit', testMatch: ['<rootDir>/test/unit/**/*.spec.ts']},
    {...shared, displayName: 'integration', testMatch: ['<rootDir>/test/integration/**/*.spec.ts']}
  ],
  coverageReporters: ['text'],
  collectCoverageFrom: ['src/**/*.ts', '!src/**/*.d.ts']
};

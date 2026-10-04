import { BrowserPlatform } from '@aurelia/platform-browser';
import { setPlatform, onFixtureCreated, type IFixture } from '@aurelia/testing';

// Prepara el entorno de Aurelia para las pruebas (igual que battlehub-shell).
function bootstrapTextEnv() {
  const platform = new BrowserPlatform(window);
  setPlatform(platform);
  BrowserPlatform.set(globalThis, platform);
}

const fixtures: IFixture<object>[] = [];
beforeAll(() => {
  bootstrapTextEnv();
  onFixtureCreated(fixture => {
    fixtures.push(fixture);
  });
});

afterEach(async () => {
  for (const fixture of fixtures.splice(0)) {
    await fixture.stop(true);
  }
});

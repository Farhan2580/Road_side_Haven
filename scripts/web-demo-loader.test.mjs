import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const source = await readFile(new URL('../Assets/WebGLTemplates/RoadsideHaven/demo.js', import.meta.url), 'utf8');
function page({ webgl = true, protocol = 'https:', bootError = false, dpr = 3, touch = false, mobileOk = true } = {}) {
  const elements = new Map();
  let loader;
  let starts = 0;
  let fullscreens = 0;
  let touchAttached = false;
  const element = id => {
    if (!elements.has(id)) elements.set(id, { hidden: false, disabled: true, listeners: {},
      addEventListener(event, callback) { this.listeners[event] = callback; }, focus() { this.focused = true; },
      getBoundingClientRect() { return { width: 800, height: 450 }; } });
    return elements.get(id);
  };
  const scope = {
    window: { devicePixelRatio: dpr, addEventListener() {}, RoadsideTouchControls: { isTouchDevice: () => touch, fullscreen() {}, attach() { touchAttached = true; } }, havenBuild: { loaderUrl: 'Build/game.loader.js', config: { dataUrl: 'Build/game.data.unityweb' } } },
    document: { getElementById: element, querySelector: element, addEventListener() {}, createElement: type => type === 'canvas' ? { getContext: () => webgl ? { getExtension: () => null } : null } : {}, body: { classList: { add() {} }, appendChild: script => { loader = script; } } },
    fetch: async () => ({ ok: mobileOk, json: async () => ({ loaderUrl: 'Mobile/Build/android.loader.js', config: { dataUrl: 'Build/android.data.unityweb' } }) }),
    location: { protocol, reload() {} }, WebAssembly: {}, console: { error() {}, warn() {} },
    createUnityInstance: async (canvas, config, progress) => {
      starts++;
      assert.equal(config.devicePixelRatio, touch ? Math.max(.5, Math.min(dpr * .67, 1)) : Math.min(dpr, 2));
      assert.equal(config.matchWebGLToCanvasSize, false);
      assert.equal(config.cacheControl('Build/game.data.unityweb'), 'must-revalidate');
      if (bootError) throw new Error('WASM failed');
      progress(0.5);
      return { SetFullscreen: value => { fullscreens += value; } };
    }
  };
  vm.runInNewContext(source, scope);
  return { element, get loader() { return loader; }, get starts() { return starts; }, get fullscreens() { return fullscreens; }, get touchAttached() { return touchAttached; } };
}
const settle = () => new Promise(resolve => setImmediate(resolve));

test('downloads the game only after Play, then hands focus and fullscreen to Unity', async () => {
  const p = page();
  assert.equal(p.starts, 0);
  assert.equal(p.loader, undefined);
  p.element('play').listeners.click();
  assert.equal(p.loader.src, 'Build/game.loader.js');
  p.loader.onload();
  await settle();
  assert.equal(p.starts, 1);
  assert.equal(p.element('welcome').hidden, true);
  assert.equal(p.element('unity-canvas').focused, true);
  assert.equal(p.element('fullscreen').disabled, false);
  p.element('fullscreen').listeners.click();
  assert.equal(p.fullscreens, 1);
});

test('unsupported browsers receive a recovery message instead of a blank canvas', () => {
  const p = page({ webgl: false });
  p.element('play').listeners.click();
  assert.equal(p.loader, undefined);
  assert.equal(p.element('error').hidden, false);
  assert.equal(p.element('retry').hidden, false);
});

test('a failed loader download leaves the recovery button visible', () => {
  const p = page();
  p.element('play').listeners.click();
  p.loader.onerror();
  assert.equal(p.element('loading').hidden, true);
  assert.equal(p.element('error').hidden, false);
});

test('WebAssembly initialization failure keeps the welcome page and recovery controls', async () => {
  const p = page({ bootError: true });
  p.element('play').listeners.click();
  p.loader.onload();
  await settle();
  assert.equal(p.element('welcome').hidden, false);
  assert.equal(p.element('error').hidden, false);
  assert.equal(p.element('retry').hidden, false);
  assert.equal(p.element('fullscreen').disabled, true);
});

test('opening the export directly from disk reports the need for a server', () => {
  const p = page({ protocol: 'file:' });
  p.element('play').listeners.click();
  assert.equal(p.loader, undefined);
  assert.equal(p.element('error').hidden, false);
});

test('Sharp uses the native canvas resolution on standard and high-DPI screens', () => {
  for (const dpr of [1, 1.25, 1.5, 2]) {
    const p = page({ dpr });
    assert.equal(p.element('unity-canvas').width, Math.round(800 * dpr));
    assert.equal(p.element('unity-canvas').height, Math.round(450 * dpr));
  }
});

test('very high-DPI screens stay within the two-times pixel budget', () => {
  const p = page({ dpr: 3 });
  assert.equal(p.element('unity-canvas').width, 1600);
  assert.equal(p.element('unity-canvas').height, 900);
});

test('quality changes resize the render target without downloading or restarting the game', async () => {
  const p = page({ dpr: 2 });
  p.element('play').listeners.click(); p.loader.onload(); await settle();
  const quality = p.element('visual-quality');
  quality.value = 'balanced'; quality.listeners.change();
  assert.equal(p.element('unity-canvas').width, 1200);
  quality.value = 'performance'; quality.listeners.change();
  assert.equal(p.element('unity-canvas').width, 800);
  quality.value = 'sharp'; quality.listeners.change();
  assert.equal(p.element('unity-canvas').width, 1600);
  assert.equal(p.starts, 1);
});

test('phones select the Android export and attach touch controls after Unity is ready', async () => {
  const p = page({ touch: true });
  assert.equal(p.element('visual-quality').value, 'performance');
  assert.equal(p.element('unity-canvas').width, 800);
  await p.element('play').listeners.click();
  assert.equal(p.loader.src, 'Mobile/Build/android.loader.js');
  assert.equal(p.touchAttached, false);
  p.loader.onload(); await settle();
  assert.equal(p.touchAttached, true); assert.equal(p.starts, 1);
});

test('a missing phone export gives a recovery message', async () => {
  const p = page({ touch: true, mobileOk: false });
  await p.element('play').listeners.click();
  assert.equal(p.loader, undefined); assert.equal(p.element('error').hidden, false);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const source = await readFile(new URL('../Assets/WebGLTemplates/RoadsideHaven/touch-controls.js', import.meta.url), 'utf8');
function fixture({ search = '', touchPoints = 5, coarse = true, userAgent = 'Android' } = {}) {
  const elements = new Map(), calls = [], frames = [], windowEvents = {}, documentEvents = {};
  function element(id, dataset = {}) {
    if (!elements.has(id)) {
      const classes = new Set();
      elements.set(id, { dataset, style: {}, hidden: true, listeners: {},
        classList: { add: c => classes.add(c), remove: c => classes.delete(c), toggle: (c, enabled) => enabled ? classes.add(c) : classes.delete(c) },
        addEventListener(name, callback) { (this.listeners[name] ||= []).push(callback); },
        getBoundingClientRect() { return { left: 0, top: 0, width: 120, height: 120 }; },
        setPointerCapture() {}, focus() {},
        dispatch(name, event = {}) { for (const callback of this.listeners[name] || []) callback({ preventDefault() {}, stopPropagation() {}, ...event }); }
      });
    }
    return elements.get(id);
  }
  const held = ['gas', 'reverse', 'brake', 'sprint'].map(name => element(name, { held: name }));
  const actions = ['vehicle', 'use', 'map', 'menu', 'back', 'jump', 'talk', 'reset'].map(name => element(name, { action: name }));
  const overlay = element('touch-controls'); overlay.querySelectorAll = selector => selector === '[data-held]' ? held : actions;
  const scope = {
    URLSearchParams, location: { search }, navigator: { maxTouchPoints: touchPoints, userAgent },
    matchMedia: () => ({ matches: coarse }), requestAnimationFrame: callback => frames.push(callback), screen: {},
    window: { addEventListener: (name, callback) => { windowEvents[name] = callback; } },
    document: { getElementById: element, body: element('body'), addEventListener: (name, callback) => { documentEvents[name] = callback; } }
  };
  vm.runInNewContext(source, scope);
  const controls = scope.window.RoadsideTouchControls;
  const attach = () => controls.attach({ SendMessage: (...args) => calls.push(args) }, element('canvas'));
  return { controls, attach, element, calls, window: scope.window, windowEvents, step: () => frames.shift()?.() };
}

test('detects phones while leaving normal desktop controls enabled', () => {
  assert.equal(fixture().controls.isTouchDevice(), true);
  assert.equal(fixture({ touchPoints: 0, coarse: false, userAgent: 'Windows' }).controls.isTouchDevice(), false);
  assert.equal(fixture({ search: '?touch=0' }).controls.isTouchDevice(), false);
});

test('joystick clamps its movement and releases on pointer cancellation', () => {
  const f = fixture(); f.attach(); const stick = f.element('touch-stick');
  stick.dispatch('pointerdown', { pointerId: 1, clientX: 60, clientY: -200 });
  assert.deepEqual(f.calls.at(-1), ['Roadside Touch Controls', 'SetMove', '0.000,1.000']);
  stick.dispatch('pointercancel', { pointerId: 1 });
  assert.deepEqual(f.calls.at(-1), ['Roadside Touch Controls', 'SetMove', '0,0']);
});

test('independent fingers can steer, hold gas and swipe the camera', () => {
  const f = fixture(); f.attach();
  f.element('touch-stick').dispatch('pointerdown', { pointerId: 1, clientX: 90, clientY: 60 });
  f.element('gas').dispatch('pointerdown', { pointerId: 2 });
  f.element('touch-look').dispatch('pointerdown', { pointerId: 3, clientX: 200, clientY: 200 });
  f.element('touch-look').dispatch('pointermove', { pointerId: 3, clientX: 220, clientY: 180 }); f.step();
  assert.ok(f.calls.some(call => call[1] === 'SetMove' && call[2].startsWith('0.781')));
  assert.ok(f.calls.some(call => call[1] === 'SetHeld' && call[2] === 'gas:1'));
  assert.ok(f.calls.some(call => call[1] === 'AddLook' && call[2] === '20.00,20.00'));
  f.element('gas').dispatch('pointerup', { pointerId: 2 });
  assert.deepEqual(f.calls.at(-1), ['Roadside Touch Controls', 'SetHeld', 'gas:0']);
});

test('focus loss clears all controls and stops stale pointer events', () => {
  const f = fixture(); f.attach(); const stick = f.element('touch-stick');
  stick.dispatch('pointerdown', { pointerId: 1, clientX: 60, clientY: 10 }); f.element('gas').dispatch('pointerdown', { pointerId: 2 });
  f.windowEvents.blur(); const count = f.calls.length;
  stick.dispatch('pointermove', { pointerId: 1, clientX: 80, clientY: 10 });
  assert.equal(f.calls.length, count);
  assert.deepEqual(f.calls.at(-1), ['Roadside Touch Controls', 'ResetInput', '']);
});

test('vehicle and modal state select the correct controls without reentering Unity during its callback', () => {
  const f = fixture(); f.attach(); const count = f.calls.length;
  f.window.roadsideTouchStatus(2, 0, 24);
  assert.equal(f.element('touch-controls').dataset.mode, 'drive'); assert.equal(f.element('touch-mode').textContent, 'Driving · 24 km/h');
  assert.equal(f.element('touch-map').disabled, true); assert.equal(f.calls.length, count);
  f.step(); assert.deepEqual(f.calls.at(-1), ['Roadside Touch Controls', 'ResetInput', '']);
  f.window.roadsideTouchStatus(2, 1, 0); assert.equal(f.element('touch-back').hidden, false);
  const before = f.calls.length; f.element('gas').dispatch('pointerdown', { pointerId: 2 }); assert.equal(f.calls.length, before);
});

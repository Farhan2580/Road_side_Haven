(() => {
  'use strict';
  const target = 'Roadside Touch Controls';
  function isTouchDevice() {
    const override = new URLSearchParams(location.search).get('touch');
    if (override !== null) return override === '1';
    return navigator.maxTouchPoints > 0 && (matchMedia('(pointer: coarse)').matches || /Android|iPhone|iPad/i.test(navigator.userAgent));
  }
  function fullscreen(container) {
    if (!container.requestFullscreen) return;
    container.requestFullscreen({ navigationUI: 'hide' }).then(() => {
      if (screen.orientation && screen.orientation.lock) return screen.orientation.lock('landscape');
    }).catch(() => {});
  }
  function attach(game, canvas) {
    const overlay = document.getElementById('touch-controls');
    const stick = document.getElementById('touch-stick');
    const knob = document.getElementById('touch-stick-knob');
    const pad = document.getElementById('touch-look');
    const qualityLabel = document.getElementById('visual-quality')?.closest?.('label');
    if (qualityLabel) overlay.querySelector('.touch-topbar').appendChild(qualityLabel);
    const held = new Map();
    let stickId = null, lookId = null, lastX = 0, lastY = 0, dx = 0, dy = 0, resetPending = false;
    let mode = 1, modal = false;
    const send = (method, value) => game.SendMessage(target, method, value);
    function clearLocal() {
      stickId = lookId = null; dx = dy = 0;
      knob.style.transform = 'translate(0px,0px)';
      for (const button of held.values()) button.classList.remove('pressed');
      held.clear();
    }
    function clear() { clearLocal(); send('ResetInput', ''); }
    function capture(event, element) {
      event.preventDefault(); event.stopPropagation(); canvas.focus();
      if (element.setPointerCapture) element.setPointerCapture(event.pointerId);
    }
    function moveStick(event) {
      const rect = stick.getBoundingClientRect();
      const radius = rect.width * 0.32;
      let x = (event.clientX - rect.left - rect.width / 2) / radius;
      let y = (rect.top + rect.height / 2 - event.clientY) / radius;
      const length = Math.hypot(x, y); if (length > 1) { x /= length; y /= length; }
      if (length < 0.12) x = y = 0;
      knob.style.transform = `translate(${x * radius}px,${-y * radius}px)`;
      send('SetMove', `${x.toFixed(3)},${y.toFixed(3)}`);
    }
    stick.addEventListener('pointerdown', event => { if (stickId !== null || modal) return; capture(event, stick); stickId = event.pointerId; moveStick(event); });
    stick.addEventListener('pointermove', event => { if (event.pointerId === stickId) { event.preventDefault(); moveStick(event); } });
    const releaseStick = event => { if (event.pointerId === stickId) { stickId = null; knob.style.transform = 'translate(0px,0px)'; send('SetMove', '0,0'); } };
    for (const event of ['pointerup', 'pointercancel', 'lostpointercapture']) stick.addEventListener(event, releaseStick);
    pad.addEventListener('pointerdown', event => { if (lookId !== null || modal) return; capture(event, pad); lookId = event.pointerId; lastX = event.clientX; lastY = event.clientY; });
    pad.addEventListener('pointermove', event => { if (event.pointerId === lookId) { event.preventDefault(); dx += event.clientX - lastX; dy += lastY - event.clientY; lastX = event.clientX; lastY = event.clientY; } });
    for (const event of ['pointerup', 'pointercancel', 'lostpointercapture']) pad.addEventListener(event, eventData => { if (eventData.pointerId === lookId) lookId = null; });
    for (const button of overlay.querySelectorAll('[data-held]')) {
      button.addEventListener('pointerdown', event => {
        if (modal) return; capture(event, button); held.set(event.pointerId, button); button.classList.add('pressed'); send('SetHeld', `${button.dataset.held}:1`);
      });
      const release = event => { if (held.get(event.pointerId) === button) { held.delete(event.pointerId); button.classList.remove('pressed'); send('SetHeld', `${button.dataset.held}:0`); } };
      for (const event of ['pointerup', 'pointercancel', 'lostpointercapture']) button.addEventListener(event, release);
    }
    for (const button of overlay.querySelectorAll('[data-action]')) button.addEventListener('pointerdown', event => {
      capture(event, button); send('Action', button.dataset.action);
    });
    function frame() {
      if (resetPending) { send('ResetInput', ''); resetPending = false; }
      if ((dx || dy) && !modal) { send('AddLook', `${dx.toFixed(2)},${dy.toFixed(2)}`); dx = dy = 0; }
      requestAnimationFrame(frame);
    }
    window.roadsideTouchStatus = (nextMode, nextModal, speed) => {
      if (nextMode !== mode || Boolean(nextModal) !== modal) { clearLocal(); resetPending = true; }
      mode = nextMode; modal = Boolean(nextModal);
      overlay.dataset.mode = mode === 2 ? 'drive' : mode === 1 ? 'walk' : 'map';
      overlay.classList.toggle('modal-open', modal);
      document.getElementById('touch-back').hidden = !modal;
      document.getElementById('touch-map').textContent = mode === 0 ? 'Walk' : 'Map';
      document.getElementById('touch-map').disabled = mode === 2;
      document.getElementById('touch-vehicle').textContent = mode === 2 ? 'Exit car' : 'Enter car';
      document.getElementById('touch-mode').textContent = mode === 2 ? `Driving · ${speed} km/h` : mode === 1 ? 'On foot' : 'Management map';
    };
    window.addEventListener('blur', clear);
    window.addEventListener('pagehide', clear);
    window.addEventListener('resize', clear);
    document.addEventListener('visibilitychange', () => { if (document.hidden) clear(); });
    overlay.addEventListener('contextmenu', event => event.preventDefault());
    overlay.hidden = false;
    document.body.classList.add('touch-playing');
    send('EnableTouch', '1'); requestAnimationFrame(frame);
    return { clear };
  }
  window.RoadsideTouchControls = { isTouchDevice, attach, fullscreen };
})();

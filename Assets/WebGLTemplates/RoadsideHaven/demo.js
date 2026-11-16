(() => {
  'use strict';
  const byId = id => document.getElementById(id);
  const canvas = byId('unity-canvas');
  const play = byId('play');
  const progress = byId('progress');
  const status = byId('status');
  const fullscreen = byId('fullscreen');
  const quality = byId('visual-quality');
  let instance;

  function pixelRatio() {
    const native = window.devicePixelRatio || 1;
    if (quality.value === 'performance') return Math.max(0.5, Math.min(native * 0.67, 1));
    if (quality.value === 'balanced') return Math.max(0.5, Math.min(native * 0.85, 1.5));
    return Math.min(native, 2);
  }

  function resizeCanvas() {
    const rect = canvas.getBoundingClientRect();
    const width = Math.max(1, Math.round(rect.width * pixelRatio()));
    const height = Math.max(1, Math.round(rect.height * pixelRatio()));
    if (canvas.width !== width) canvas.width = width;
    if (canvas.height !== height) canvas.height = height;
  }

  quality.value = 'sharp';
  quality.addEventListener('change', resizeCanvas);
  window.addEventListener('resize', resizeCanvas);
  document.addEventListener('fullscreenchange', resizeCanvas);
  if (typeof ResizeObserver !== 'undefined') new ResizeObserver(resizeCanvas).observe(canvas);
  resizeCanvas();

  function fail(error) {
    console.error('Roadside Haven could not start:', error);
    byId('loading').hidden = true;
    byId('error').textContent = 'The game could not start. Reload the demo, or try the latest desktop Chrome, Edge or Firefox with hardware acceleration enabled.';
    byId('error').hidden = false;
    byId('retry').hidden = false;
    play.hidden = true;
  }

  play.addEventListener('click', () => {
    play.disabled = true;
    play.hidden = true;
    try {
      if (location.protocol === 'file:') throw new Error('Serve the demo through HTTP or HTTPS.');
      const probe = document.createElement('canvas').getContext('webgl2');
      if (!probe || typeof WebAssembly === 'undefined') throw new Error('WebGL 2 and WebAssembly are required.');
      const release = probe.getExtension('WEBGL_lose_context');
      if (release) release.loseContext();
      byId('loading').hidden = false;
      const build = window.havenBuild;
      const script = document.createElement('script');
      script.src = build.loaderUrl;
      script.onerror = () => fail(new Error('The game loader could not be downloaded.'));
      script.onload = () => {
        const config = {
          ...build.config,
          devicePixelRatio: pixelRatio(),
          matchWebGLToCanvasSize: false,
          cacheControl: () => 'must-revalidate',
          showBanner: (message, type) => {
            if (type === 'error') fail(message);
            else console.warn(message);
          }
        };
        Promise.resolve().then(() => createUnityInstance(canvas, config, value => {
          progress.value = value;
          status.textContent = value >= 0.9 ? 'Opening Roadside Haven…' : `Loading your world… ${Math.round(value * 100)}%`;
        })).then(game => {
          instance = game;
          byId('welcome').hidden = true;
          fullscreen.disabled = false;
          canvas.focus();
        }).catch(fail);
      };
      document.body.appendChild(script);
    } catch (error) { fail(error); }
  });
  fullscreen.addEventListener('click', () => { if (instance) instance.SetFullscreen(1); });
  byId('retry').addEventListener('click', () => location.reload());
  canvas.addEventListener('click', () => canvas.focus());
})();

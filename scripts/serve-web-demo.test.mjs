import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { createDemoServer } from './serve-web-demo.mjs';

test('local preview serves Unity files without assuming gzip response headers', async t => {
  const root = await mkdtemp(path.join(tmpdir(), 'haven-preview-'));
  const server = createDemoServer(root);
  t.after(async () => { await new Promise(resolve => server.close(resolve)); await rm(root, { recursive: true, force: true }); });
  await mkdir(path.join(root, 'Build'));
  await writeFile(path.join(root, 'index.html'), '<canvas></canvas>');
  await writeFile(path.join(root, 'Build/test.loader.js'), '/* loader */');
  await writeFile(path.join(root, 'Build/test.data.unityweb'), Buffer.from([31, 139, 8]));
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  assert.equal(await (await fetch(origin)).text(), '<canvas></canvas>');
  const loader = await fetch(origin + '/Build/test.loader.js?cache=1');
  assert.match(loader.headers.get('content-type'), /javascript/);
  const data = await fetch(origin + '/Build/test.data.unityweb');
  assert.equal(data.headers.get('content-encoding'), null);
  assert.equal((await data.arrayBuffer()).byteLength, 3);
  assert.equal((await fetch(origin + '/missing.wasm')).status, 404);
  assert.equal((await fetch(origin + '/%2e%2e%2foutside.txt')).status, 403);
  assert.equal((await fetch(origin + '/%ZZ')).status, 400);
});

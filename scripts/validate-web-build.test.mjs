import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, rm, unlink, symlink } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { validateWebBuild } from './validate-web-build.mjs';

async function fixture(t) {
  const root = await mkdtemp(path.join(tmpdir(), 'haven-web-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  await mkdir(path.join(root, 'Build'));
  for (const name of ['game.loader.js', 'a.framework.js.unityweb', 'b.wasm.unityweb', 'c.data.unityweb'])
    await writeFile(path.join(root, 'Build', name), 'fixture');
  await writeFile(path.join(root, 'index.html'), '<script src="Build/game.loader.js"></script>');
  return root;
}

test('accepts a compressed Unity build in a project-site subdirectory', async t => {
  const result = await validateWebBuild(await fixture(t));
  assert.equal(result.runtimeFiles, 4);
  assert.ok(result.bytes > 0);
});

test('rejects a failed build without WebAssembly', async t => {
  const root = await fixture(t);
  await unlink(path.join(root, 'Build/b.wasm.unityweb'));
  await assert.rejects(validateWebBuild(root), /Missing Unity runtime/);
});

test('rejects a template accidentally uploaded instead of a build', async t => {
  const root = await fixture(t);
  await writeFile(path.join(root, 'index.html'), '{{{ LOADER_FILENAME }}}');
  await assert.rejects(validateWebBuild(root), /unprocessed Unity template/);
});

test('rejects stale HTML pointing at a missing loader', async t => {
  const root = await fixture(t);
  await writeFile(path.join(root, 'index.html'), '<script src="Build/old.loader.js"></script>');
  await assert.rejects(validateWebBuild(root), /references a missing file/);
});

test('checks the Pages size limit before deployment', async t => {
  await assert.rejects(validateWebBuild(await fixture(t), { maxBytes: 1 }), /Pages limit/);
});

test('rejects links in a Pages artifact', { skip: process.platform === 'win32' }, async t => {
  const root = await fixture(t);
  await symlink(path.join(root, 'index.html'), path.join(root, 'link.html'));
  await assert.rejects(validateWebBuild(root), /cannot contain links/);
});

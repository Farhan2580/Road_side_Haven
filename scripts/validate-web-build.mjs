import { readdir, readFile, lstat } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

// Fail before deployment; a broken build must never replace the last working demo.
export async function validateWebBuild(directory, { maxBytes = 1_000_000_000 } = {}) {
  const root = path.resolve(directory);
  let bytes = 0;
  async function inspect(folder) {
    for (const name of await readdir(folder)) {
      const file = path.join(folder, name);
      const stat = await lstat(file);
      if (stat.isSymbolicLink()) throw new Error(`Pages artifacts cannot contain links: ${name}`);
      if (stat.isDirectory()) await inspect(file);
      else bytes += stat.size;
    }
  }
  await inspect(root);
  if (bytes > maxBytes) throw new Error(`The demo is ${bytes} bytes; the Pages limit is ${maxBytes}. Reduce the build before publishing.`);
  const html = await readFile(path.join(root, 'index.html'), 'utf8');
  if (/\{\{\{|^\s*#(?:if|else|endif)\b/m.test(html)) throw new Error('index.html is an unprocessed Unity template, not a playable build.');
  const build = await readdir(path.join(root, 'Build'));
  for (const ending of [/\.loader\.js$/, /\.framework\.js(?:\.unityweb)?$/, /\.wasm(?:\.unityweb)?$/, /\.data(?:\.unityweb)?$/]) {
    if (!build.some(name => ending.test(name))) throw new Error(`Missing Unity runtime file matching ${ending}`);
  }
  for (const match of html.matchAll(/\bBuild\/([a-zA-Z0-9_.-]+)/g)) {
    if (!build.includes(match[1])) throw new Error(`index.html references a missing file: ${match[0]}`);
  }
  if (html.includes('touch-controls.js')) {
    const mobile = JSON.parse(await readFile(path.join(root, 'mobile-build.json'), 'utf8'));
    for (const relative of [mobile.loaderUrl, mobile.config?.dataUrl, mobile.config?.codeUrl, mobile.config?.frameworkUrl, 'touch-controls.js']) {
      if (typeof relative !== 'string' || !path.resolve(root, relative).startsWith(root + path.sep)) throw new Error('Invalid phone build manifest.');
      if (!(await lstat(path.join(root, relative))).isFile()) throw new Error(`Missing phone runtime file: ${relative}`);
    }
  }
  return { bytes, runtimeFiles: build.length };
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  try {
    if (!process.argv[2]) throw new Error('Usage: node scripts/validate-web-build.mjs <build-directory>');
    const result = await validateWebBuild(process.argv[2]);
    console.log(`Web demo checked: ${(result.bytes / 1048576).toFixed(1)} MiB, ${result.runtimeFiles} runtime files.`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

import { copyFileSync, cpSync, existsSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const azmaWebTsc = resolve(root, '..', 'azma-web', 'node_modules', 'typescript', 'lib', 'tsc.js');

if (!existsSync(azmaWebTsc)) {
  throw new Error(`TypeScript compiler not found at ${azmaWebTsc}. Run npm install in src/Web/azma-web first.`);
}

rmSync(resolve(root, '.build'), { recursive: true, force: true });
rmSync(resolve(root, 'dist'), { recursive: true, force: true });

const tsc = spawnSync(process.execPath, [azmaWebTsc, '-p', resolve(root, 'tsconfig.json')], {
  cwd: root,
  stdio: 'inherit',
});

if (tsc.status !== 0) {
  process.exit(tsc.status ?? 1);
}

for (const browser of ['chrome', 'firefox']) {
  const outDir = resolve(root, 'dist', browser);
  mkdirSync(outDir, { recursive: true });

  for (const file of ['azmaBridge.js', 'background.js', 'targetRecorder.js', 'pageHook.js']) {
    copyFileSync(resolve(root, '.build', file), resolve(outDir, file));
  }

  copyFileSync(resolve(root, 'overlay.css'), resolve(outDir, 'overlay.css'));
  copyFileSync(resolve(root, `manifest.${browser}.json`), resolve(outDir, 'manifest.json'));
}

rmSync(resolve(root, '.build'), { recursive: true, force: true });

const powershell = process.platform === 'win32' ? 'powershell.exe' : 'pwsh';
const packageResult = spawnSync(powershell, [
  '-NoProfile',
  '-ExecutionPolicy',
  'Bypass',
  '-File',
  resolve(root, 'scripts', 'package.ps1'),
], {
  cwd: root,
  stdio: 'inherit',
});

if (packageResult.status !== 0) {
  process.exit(packageResult.status ?? 1);
}

console.log('Azma Recorder extension built:');
console.log(' - dist/chrome');
console.log(' - dist/firefox');

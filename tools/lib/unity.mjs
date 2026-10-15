// Finding and running the Unity editor from Node (the checks and every tool use this).
//
// findUnity() looks, in order, at:
//   1. the UNITY environment variable (the editor binary, or a Unity.app bundle on macOS)
//   2. the Unity CLI's list of installed editors (`unity editors --installed --json`)
//   3. Unity Hub's default and secondary install folders on Windows, macOS and Linux
// and accepts only the version pinned in ProjectSettings/ProjectVersion.txt.
//
// runUnity() starts the editor in batch mode with a log file and a timeout,
// without Git Bash's MSYSTEM variable, and explains the common failures
// (another editor has the project open, the license, the compiler running
// out of memory) instead of leaving you to read a 5000-line log.
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { PROJECT } from '../../checks/layout.mjs';

// The Unity project folder (found by checks/layout.mjs: the folder holding Assets/ and ProjectSettings/).
export const ROOT = PROJECT;
export const LOG_DIR = join(ROOT, 'Logs', 'codeplate');

export function pinnedVersion() {
  const text = readFileSync(join(ROOT, 'ProjectSettings', 'ProjectVersion.txt'), 'utf8');
  return text.match(/^m_EditorVersion:\s*(\S+)/m)?.[1] ?? '';
}

// The editor binary inside an install folder (the folder that holds Editor/ or Unity.app).
function binaryIn(folder) {
  const candidates = process.platform === 'win32'
    ? [join(folder, 'Editor', 'Unity.exe'), join(folder, 'Unity.exe')]
    : process.platform === 'darwin'
      ? [join(folder, 'Unity.app', 'Contents', 'MacOS', 'Unity'), join(folder, 'Contents', 'MacOS', 'Unity')]
      : [join(folder, 'Editor', 'Unity'), join(folder, 'Unity')];
  return candidates.find((c) => existsSync(c)) ?? null;
}

// Normalizes what UNITY points at to the editor binary.
function fromEnv(value) {
  if (!value || !existsSync(value)) return null;
  return statSync(value).isDirectory() ? binaryIn(value) : value;
}

// Version of an editor binary, read from its install path (Hub folders are named by version)
// or, failing that, from the editor's own files.
export function versionOf(binary) {
  const m = binary.replace(/\\/g, '/').match(/\/(\d{4}\.\d+\.\d+[abfp]\d+)(?:[^/]*)\//);
  if (m) return m[1];
  // A custom folder: Unity writes its version into the Windows file version, or the macOS Info.plist.
  if (process.platform === 'darwin') {
    const plist = join(binary, '..', '..', 'Info.plist');
    if (existsSync(plist)) return readFileSync(plist, 'utf8').match(/<key>CFBundleVersion<\/key>\s*<string>([^<]+)</)?.[1] ?? '';
  }
  return '';
}

function hubFolders() {
  const home = homedir();
  const roots = [];
  if (process.platform === 'win32') {
    roots.push('C:\\Program Files\\Unity\\Hub\\Editor', 'C:\\Program Files\\Unity Hub\\Editor');
    const secondary = join(process.env.APPDATA ?? '', 'UnityHub', 'secondaryInstallPath.json');
    if (existsSync(secondary)) {
      try {
        const p = JSON.parse(readFileSync(secondary, 'utf8'));
        if (p) roots.unshift(p);
      } catch { /* ignore */ }
    }
  } else if (process.platform === 'darwin') {
    roots.push('/Applications/Unity/Hub/Editor', join(home, 'Applications', 'Unity', 'Hub', 'Editor'));
    const secondary = join(home, 'Library', 'Application Support', 'UnityHub', 'secondaryInstallPath.json');
    if (existsSync(secondary)) {
      try {
        const p = JSON.parse(readFileSync(secondary, 'utf8'));
        if (p) roots.unshift(p);
      } catch { /* ignore */ }
    }
  } else {
    roots.push(join(home, 'Unity', 'Hub', 'Editor'), '/opt/unity/editors');
  }
  return roots;
}

function cliEditors() {
  const names = process.platform === 'win32'
    ? [join(process.env.LOCALAPPDATA ?? '', 'Unity', 'bin', 'unity.exe'), 'unity']
    : [join(homedir(), '.local', 'bin', 'unity'), '/usr/local/bin/unity', 'unity'];
  for (const cli of names) {
    if (cli.includes('/') || cli.includes('\\')) {
      if (!existsSync(cli)) continue;
    }
    const r = spawnSync(cli, ['editors', '--installed', '--json', '--no-banner'], { encoding: 'utf8', timeout: 20000, shell: false });
    if (r.status !== 0 || !r.stdout) continue;
    try {
      const data = JSON.parse(r.stdout);
      const list = Array.isArray(data) ? data : data.editors ?? data.data ?? [];
      return list.map((e) => ({ version: e.version ?? e.Version, path: e.path ?? e.location ?? e.Path })).filter((e) => e.version && e.path);
    } catch {
      continue;
    }
  }
  return [];
}

// Returns { binary, version, tried: [..] }. binary is null when the pinned version isn't installed.
export function findUnity() {
  const want = pinnedVersion();
  const tried = [];
  const env = process.env.UNITY;
  if (env) {
    const bin = fromEnv(env);
    if (!bin) {
      tried.push(`UNITY=${env} (not found)`);
    } else {
      const v = versionOf(bin);
      tried.push(`UNITY=${bin}${v ? ` (${v})` : ''}`);
      if (!v || v === want) return { binary: bin, version: v || want, tried };
    }
  }
  for (const e of cliEditors()) {
    const bin = fromEnv(e.path);
    tried.push(`unity CLI: ${e.version} at ${e.path}`);
    if (e.version === want && bin && existsSync(bin)) return { binary: bin, version: e.version, tried };
  }
  for (const root of hubFolders()) {
    const folder = join(root, want);
    const bin = binaryIn(folder);
    if (bin) return { binary: bin, version: want, tried };
    if (existsSync(root)) {
      const others = readdirSync(root).filter((d) => /^\d{4}\./.test(d));
      if (others.length) tried.push(`${root}: ${others.join(', ')}`);
    }
  }
  return { binary: null, version: want, tried };
}

export function installHelp(version) {
  return [
    `Unity ${version} is needed (the version pinned in ProjectSettings/ProjectVersion.txt), and it wasn't found.`,
    'Install exactly that version, then run this again:',
    `  - Unity Hub: Installs > Install Editor > Archive, pick ${version}`,
    `  - or the Unity CLI: unity install ${version}`,
    'Already installed somewhere else? Set UNITY to the editor binary, e.g.',
    `  Windows: set UNITY=C:\\Program Files\\Unity\\Hub\\Editor\\${version}\\Editor\\Unity.exe`,
    `  macOS:   export UNITY=/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/MacOS/Unity`,
    'Don\'t open the project in a different Unity version: it upgrades files the checks expect unchanged.',
  ].join('\n');
}

// Known failure patterns in a Unity log, with the explanation to print.
const KNOWN = [
  [/It looks like another Unity instance is running with this project open|Multiple Unity instances cannot open the same project/,
    'Another Unity editor has this project open. Close it (or use `unity mcp` to drive the open editor) and run again.'],
  [/GC heap initialization failed|0x80004005.*compiler|Unhandled Exception: System.OutOfMemoryException/,
    "Unity's compiler couldn't start (out of memory?): close other builds and re-run."],
  [/No valid Unity Editor license found|LICENSE SYSTEM .* No ULF license found|Licensing::Module.*(?:No valid|Failed to)/,
    'Unity has no active license on this machine. Open Unity Hub, sign in and activate a (free) Personal license, then run again.'],
  [/Scripts have compiler errors/, 'The C# code has compile errors (listed above).'],
];

export function explain(log) {
  return KNOWN.filter(([re]) => re.test(log)).map(([, msg]) => msg);
}

// `error CS…` lines from a Unity log, de-duplicated, as "file(line,col): error CSxxxx: message".
export function compileErrors(log) {
  const out = new Set();
  for (const m of log.matchAll(/^(.*?\.cs)\((\d+),(\d+)\): error (CS\d+): (.*)$/gm)) {
    out.add(`${m[1].replace(/\\/g, '/')}:${m[2]}:${m[3]}: ${m[4]} ${m[5].trim()}`);
  }
  return [...out];
}

// Runs the editor in batch mode. `args` are extra Unity arguments.
// Returns { code, log, timedOut, seconds }.
export async function runUnity(binary, args, { name, timeoutMs = 15 * 60_000, graphics = false, echo = false } = {}) {
  mkdirSync(LOG_DIR, { recursive: true });
  const logFile = join(LOG_DIR, `${name}.log`);
  rmSync(logFile, { force: true });
  const full = ['-batchmode', ...(graphics ? [] : ['-nographics']), '-projectPath', ROOT, '-logFile', logFile, ...args];
  const env = { ...process.env };
  delete env.MSYSTEM; // Git Bash sets it; Unity's tools don't need it
  const start = Date.now();
  let timedOut = false;
  const code = await new Promise((done) => {
    const child = spawn(binary, full, { env, stdio: ['ignore', echo ? 'inherit' : 'ignore', echo ? 'inherit' : 'ignore'] });
    const timer = setTimeout(() => {
      timedOut = true;
      child.kill();
    }, timeoutMs);
    child.on('error', (e) => {
      clearTimeout(timer);
      console.log(`could not start Unity: ${e.message}`);
      done(-1);
    });
    child.on('exit', (c) => {
      clearTimeout(timer);
      done(c ?? -1);
    });
  });
  // Unity writes CRLF on Windows; drop the CRs so line-based parsing works everywhere.
  const log = existsSync(logFile) ? readFileSync(logFile, 'utf8').split(String.fromCharCode(13)).join('') : '';
  return { code, log, logFile, timedOut, seconds: (Date.now() - start) / 1000 };
}

// Runs Unity and retries once when its compiler failed to start for lack of memory.
export async function runUnityRetrying(binary, args, opts) {
  let r = await runUnity(binary, args, opts);
  if (r.code !== 0 && /GC heap initialization failed/.test(r.log)) {
    console.log("Unity's compiler couldn't start (out of memory?): retrying once. Close other builds if it happens again.");
    r = await runUnity(binary, args, opts);
  }
  return r;
}

// Lines from the log written by our own [CodePlate] Debug.Log calls (tools print through these).
export function codeplateLines(log) {
  return log.split(/\r?\n/).filter((l) => l.startsWith('[CodePlate]')).map((l) => l.slice('[CodePlate] '.length));
}

// Project rules the C# tests can't see: files on disk against assets.json, Unity's
// .meta files, the one-scene rule, the engine-free simulation, the pinned editor
// and package versions, the web page's colors and saves, the font's glyphs against every text
// the game shows, pickups that look alike, the credit the free license asks for. Pure Node, no
// Unity needed: node checks/rules.mjs
// Returns problems as { file?, message, warn? }; a warn doesn't fail the checks.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { decodePng } from '../tools/lib/png.mjs';
import { fontCharacters } from './font.mjs';
import { KIT, LAYOUT, PATHS, PROJECT } from './layout.mjs';

// Every file under these folders is an asset slot and must be listed in assets.json.
export const ASSET_ROOTS = [`${PATHS.art}/`, `${PATHS.audio}/`];
// The only scene. Everything else is built from code (Boot.cs).
export const SCENES = [PATHS.bootScene];
// The credit the CodePlate Free License asks for (LICENSE.md, "Credit").
export const CREDIT = 'Built with CodePlate (codeplate.io)';

function walk(root, dir = root, out = []) {
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    const rel = relative(root, p).replace(/\\/g, '/');
    out.push({ rel, dir: e.isDirectory() });
    if (e.isDirectory()) walk(root, p, out);
  }
  return out;
}

function pngSize(buf) {
  const sig = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
  if (buf.length < 24 || !sig.every((b, i) => buf[i] === b)) return null;
  return { width: buf.readUInt32BE(16), height: buf.readUInt32BE(20) };
}

// `root` is the Unity project folder, `kit` the folder holding codeplate.json, LICENSE.md and checks/.
export function checkProject(root = PROJECT, kit = KIT) {
  const problems = [];
  const fail = (message, file) => problems.push({ message, ...(file ? { file } : {}) });
  const warn = (message, file) => problems.push({ message, warn: true, ...(file ? { file } : {}) });
  const all = existsSync(join(root, 'Assets')) ? walk(join(root, 'Assets')).map((e) => ({ ...e, rel: `Assets/${e.rel}` })) : [];
  const files = all.filter((e) => !e.dir).map((e) => e.rel);
  const fileSet = new Set(files);

  // assets.json
  let manifest = null;
  try {
    manifest = JSON.parse(readFileSync(join(root, LAYOUT.assets), 'utf8'));
  } catch (e) {
    fail(`assets.json is missing or not valid JSON (${e.message})`, 'assets.json');
  }
  if (manifest) {
    const listed = new Set();
    for (const a of manifest.assets ?? []) {
      if (listed.has(a.path)) fail(`listed twice: ${a.path}`, 'assets.json');
      listed.add(a.path);
      // Any owner is fine (the template's placeholders, CC0, the user's own art, their team's name),
      // but every asset must say whose it is and how it may be used: AGENTS.md rule 10.
      for (const field of ['license', 'credit']) {
        if (typeof a[field] !== 'string' || !a[field].trim()) {
          fail(`needs a "${field}" (who owns it and under what terms, e.g. "CC0-1.0", or the user's game or team name: AGENTS.md rule 10)`, a.path ?? 'assets.json');
        }
      }
      if (!ASSET_ROOTS.some((r) => a.path.startsWith(r))) fail(`asset slots live under ${ASSET_ROOTS.join(' or ')}`, a.path);
      if (!existsSync(join(root, a.path))) {
        fail('listed in assets.json but missing on disk', a.path);
        continue;
      }
      if (a.kind === 'image' && a.path.endsWith('.png')) {
        const size = pngSize(readFileSync(join(root, a.path)));
        if (!size) fail('not a valid PNG', a.path);
        else if (a.width && a.height && (size.width !== a.width * (a.frames ?? 1) || size.height !== a.height)) {
          fail(`PNG is ${size.width}x${size.height}, but assets.json says ${a.width * (a.frames ?? 1)}x${a.height} (${a.frames ?? 1} frame(s) of ${a.width}x${a.height})`, a.path);
        }
      }
    }
    // The frame sizes the game cuts sprites at (ArtSlots in Art.cs) must match assets.json: change
    // a slot's size in both (docs/recipes/reskin-and-rebrand.md).
    const artCs = `${LAYOUT.game}/Game/Runtime/Art.cs`;
    if (existsSync(join(root, artCs))) {
      const byPath = new Map((manifest.assets ?? []).map((a) => [a.path, a]));
      for (const m of readFileSync(join(root, artCs), 'utf8').matchAll(/new Slot\("([^"]+)",\s*(\d+),\s*(\d+)/g)) {
        const a = byPath.get(`${PATHS.art}/${m[1]}.png`);
        const [w, h] = [Number(m[2]), Number(m[3])];
        if (!a) fail(`Art.cs has a slot "${m[1]}" that assets.json doesn't list`, artCs);
        else if (a.width !== w || a.height !== h) {
          fail(`Art.cs cuts "${m[1]}" into ${w}x${h} frames, but assets.json says ${a.width}x${a.height}: make them agree`, artCs);
        }
      }
    }
    // Each pickup kind needs art of its own, or players can't tell them apart
    // (docs/recipes/add-collectible-and-unlock.md): two pickup strips with the same pixels, or two
    // kinds pointed at one strip in ActorView.ART, only warn (stand-in art is fine while it lasts).
    const pickups = new Map();
    for (const f of files.filter((f) => f.startsWith(`${PATHS.art}/pickups/`) && f.endsWith('.png'))) {
      try {
        const img = decodePng(readFileSync(join(root, f)));
        const key = createHash('sha256').update(`${img.width}x${img.height}`).update(img.data).digest('hex');
        pickups.set(key, [...(pickups.get(key) ?? []), f]);
      } catch { /* the size rule above reports a broken PNG */ }
    }
    for (const same of pickups.values()) {
      if (same.length > 1) warn(`these pickup strips look exactly alike: ${same.join(', ')}. Give each pickup its own art, or tell the user which ones share a look`, same[0]);
    }
    const actorView = `${LAYOUT.game}/Game/Runtime/Level/ActorView.cs`;
    if (existsSync(join(root, actorView))) {
      const byStrip = new Map();
      for (const m of readFileSync(join(root, actorView), 'utf8').matchAll(/\["(\w+)"\]\s*=\s*new Dictionary<string, string>\s*\{\s*\["\*"\]\s*=\s*"(pickups\/[^"]+)"/g)) {
        byStrip.set(m[2], [...(byStrip.get(m[2]) ?? []), m[1]]);
      }
      for (const [strip, kinds] of byStrip) {
        if (kinds.length > 1) warn(`pickups ${kinds.join(' and ')} share the strip ${strip} (ActorView.ART), so they look the same in the game: give each its own, or tell the user`, actorView);
      }
    }
    for (const f of files) {
      if (ASSET_ROOTS.some((r) => f.startsWith(r)) && !f.endsWith('.meta') && !listed.has(f)) {
        fail('file is not listed in assets.json (add an entry with its size, frames and use)', f);
      }
    }
  }

  // .meta files: Unity creates one per file and folder; they hold the GUIDs the Boot scene and
  // the import settings depend on, so they ship with the project.
  for (const e of all) {
    if (e.rel.endsWith('.meta')) {
      const target = e.rel.slice(0, -'.meta'.length);
      if (!fileSet.has(target) && !all.some((x) => x.dir && x.rel === target)) {
        warn('orphan .meta (its file is gone); Unity deletes it on the next import', e.rel);
      }
    } else if (!fileSet.has(`${e.rel}.meta`)) {
      fail("has no .meta file: Unity creates it when it next imports the project (run the checks again); don't write .meta files by hand", e.rel);
    }
  }

  // One scene, no prefabs.
  for (const f of files) {
    if (f.endsWith('.unity') && !SCENES.includes(f)) fail(`only ${SCENES.join(', ')} may be a scene: build things from code (see AGENTS.md rule 3)`, f);
    if (f.endsWith('.prefab')) fail('no prefabs: build objects from code (see AGENTS.md rule 3)', f);
  }
  for (const s of SCENES) if (!fileSet.has(s)) fail('the Boot scene is missing', s);

  // Game.Core stays engine-free, so it runs anywhere, deterministically.
  const coreAsmdef = PATHS.coreAsmdef;
  if (!fileSet.has(coreAsmdef)) {
    fail('missing', coreAsmdef);
  } else {
    const a = JSON.parse(readFileSync(join(root, coreAsmdef), 'utf8'));
    if (a.noEngineReferences !== true) fail('Game.Core must keep "noEngineReferences": true: the simulation may not use UnityEngine (AGENTS.md rule 1)', coreAsmdef);
    if ((a.references ?? []).length) fail('Game.Core must not reference other assemblies', coreAsmdef);
  }
  for (const f of files) {
    if (f.startsWith(PATHS.core) && f.endsWith('.cs')) {
      // Code only: comments may mention UnityEngine or System.Random (to say not to use them).
      const text = readFileSync(join(root, f), 'utf8')
        .replace(/\/\*[\s\S]*?\*\//g, '')
        .replace(/\/\/.*$/gm, '')
        .replace(/"(?:[^"\\\n]|\\.)*"/g, '""');
      const m = text.match(/^\s*using\s+(UnityEngine|UnityEditor)[\w.]*\s*;/m) ?? text.match(/\b(UnityEngine|UnityEditor)\.\w+/);
      if (m) fail(`the simulation may not use ${m[1]} (AGENTS.md rule 1): keep engine code in ${LAYOUT.game}/Game/Runtime`, f);
      if (/\bSystem\.Random\b|new Random\(|DateTime\.Now|Stopwatch|Environment\.TickCount/.test(text)) {
        fail('no real time or System.Random in the simulation: use Rng and tick counts (AGENTS.md rule 4)', f);
      }
    }
  }

  // The pinned editor.
  let pin = '';
  try {
    pin = JSON.parse(readFileSync(join(kit, 'codeplate.json'), 'utf8')).stack.find((s) => s.name === 'unity')?.version ?? '';
  } catch { /* reported by the validator */ }
  const pv = existsSync(join(root, 'ProjectSettings/ProjectVersion.txt'))
    ? readFileSync(join(root, 'ProjectSettings/ProjectVersion.txt'), 'utf8').match(/^m_EditorVersion:\s*(\S+)/m)?.[1] : '';
  if (pin && pv !== pin) {
    fail(`the project was saved by Unity ${pv}, but codeplate.json pins ${pin}. Open it only with ${pin}; if the user chose to upgrade, update the pin too and say so.`, 'ProjectSettings/ProjectVersion.txt');
  }

  // Packages: the manifest stays minimal. To add a package, follow AGENTS.md "Adding a package".
  const allowedFile = join(kit, 'checks', 'packages.json');
  if (existsSync(allowedFile) && existsSync(join(root, 'Packages/manifest.json'))) {
    const allowed = JSON.parse(readFileSync(allowedFile, 'utf8'));
    const deps = JSON.parse(readFileSync(join(root, 'Packages/manifest.json'), 'utf8')).dependencies ?? {};
    for (const [name, version] of Object.entries(deps)) {
      if (!(name in allowed)) fail(`package ${name} isn't in checks/packages.json: add packages only as AGENTS.md "Adding a package" says`, 'Packages/manifest.json');
      else if (allowed[name] !== version) fail(`${name} is ${version}, checks/packages.json pins ${allowed[name]}`, 'Packages/manifest.json');
    }
    for (const name of Object.keys(allowed)) if (!(name in deps)) fail(`package ${name} was removed (checks/packages.json lists it)`, 'Packages/manifest.json');
  }

  // The C# paths (ProjectPaths.cs) and the Node ones (checks/layout.mjs) must agree.
  if (fileSet.has(PATHS.projectPaths)) {
    const cs = readFileSync(join(root, PATHS.projectPaths), 'utf8');
    const constant = (name) => cs.match(new RegExp(`const string ${name} = "([^"]*)"`))?.[1];
    for (const [name, key] of [['Game', 'game'], ['Replays', 'replays'], ['Docs', 'docs'], ['Shots', 'shots'], ['AssetsManifest', 'assets']]) {
      if (constant(name) !== LAYOUT[key]) {
        fail(`ProjectPaths.${name} is "${constant(name)}" but checks/layout.mjs says "${LAYOUT[key]}": change both together`, PATHS.projectPaths);
      }
    }
  } else {
    fail('missing: every path to a project file comes from here', PATHS.projectPaths);
  }

  // The web page around the web build repeats the game's palette (its :root variables), so a
  // reskin that changes Game.uss must change it too, or the loading screen keeps the old colors.
  // Web saves: without autoSyncPersistentDataPath the save file stays in the page's memory and
  // every reload starts a fresh game (docs/recipes/save-data.md).
  if (fileSet.has(PATHS.webPage) && !/^\s*autoSyncPersistentDataPath:\s*true\s*,/m.test(readFileSync(join(root, PATHS.webPage), 'utf8'))) {
    fail('the web page must pass autoSyncPersistentDataPath: true to createUnityInstance, or web saves and settings are lost on every reload', PATHS.webPage);
  }
  if (fileSet.has(PATHS.styles) && fileSet.has(PATHS.webPage)) {
    const vars = (file) => new Map([...(readFileSync(join(root, file), 'utf8').match(/:root\s*\{[^}]*\}/)?.[0] ?? '')
      .matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)].map((m) => [m[1], m[2].trim().toLowerCase()]));
    const game = vars(PATHS.styles);
    for (const [name, value] of vars(PATHS.webPage)) {
      if (game.has(name) && game.get(name) !== value) {
        fail(`${name} is ${value} here but ${game.get(name)} in ${PATHS.styles}: the web page repeats the game's palette, change both (docs/recipes/reskin-and-rebrand.md)`, PATHS.webPage);
      }
    }
  }

  // The font must have a glyph for every character the game shows, in every language, or those
  // characters come out as boxes (docs/recipes/add-language.md, reskin-and-rebrand.md).
  if (fileSet.has(PATHS.font)) {
    let glyphs = null;
    try {
      glyphs = fontCharacters(readFileSync(join(root, PATHS.font)));
    } catch (e) {
      fail(`can't read the font's character map (${e.message})`, PATHS.font);
    }
    if (glyphs) {
      const missing = new Map(); // character -> files
      for (const { text, file } of shownTexts(root, files)) {
        for (const ch of text) {
          const c = ch.codePointAt(0);
          if (c < 32 || glyphs.has(c)) continue;
          if (!missing.has(ch)) missing.set(ch, new Set());
          missing.get(ch).add(file);
        }
      }
      for (const [ch, where] of missing) {
        const code = `U+${ch.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')}`;
        fail(`the font has no glyph for "${ch}" (${code}), used in ${[...where].join(', ')}: use another character there, or a font that has it (docs/recipes/add-language.md)`, PATHS.font);
      }
    }
  }

  // The CodePlate Free License asks games made from the template to show the credit. The
  // in-game credits page shows Content/credits.txt (or credits_<code>.txt in another language),
  // so each must keep the line. Under the Pro License, or any other terms, the credit is optional
  // and this rule is off.
  const license = existsSync(join(kit, 'LICENSE.md')) ? readFileSync(join(kit, 'LICENSE.md'), 'utf8') : '';
  if (/^# CodePlate Free License\b/.test(license)) {
    const creditFiles = [PATHS.credits, ...files.filter((f) => /\/credits_[\w-]+\.txt$/.test(f) && f.startsWith(`${PATHS.resources}/Content/`))];
    for (const file of creditFiles) {
      const credits = existsSync(join(root, file)) ? readFileSync(join(root, file), 'utf8') : '';
      if (!credits.includes(CREDIT)) {
        fail(`the in-game credits must keep the line "${CREDIT}": LICENSE.md (the CodePlate Free License) asks for it in anything you share`, file);
      }
    }
  }
  return problems;
}

const unXml = (s) => s.replace(/&#10;/g, '\n').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');

/**
 * Every text the game can show, with the file it comes from: UXML texts, strings.json (every
 * language), worlds.json names and subtitles, level titles and boss names, Loc.T("...") literals
 * and boss name defaults in the code, and the credits files.
 */
export function shownTexts(root, files) {
  const out = [];
  const read = (f) => readFileSync(join(root, f), 'utf8');
  const name = (f) => f.split('/').pop();
  for (const f of files) {
    if (f.startsWith(`${PATHS.resources}/UI/`) && f.endsWith('.uxml')) {
      for (const m of read(f).matchAll(/\btext="([^"]*)"/g)) out.push({ text: unXml(m[1]), file: name(f) });
    } else if (f === `${PATHS.resources}/Content/strings.json` || f === `${PATHS.resources}/Content/worlds.json`) {
      try {
        const json = JSON.parse(read(f));
        const walk = (v, key) => {
          if (typeof v === 'string') {
            if (key !== '_comment' && !/^(id|music|bossMusic|accent|sky|tint)$/.test(key)) out.push({ text: v, file: name(f) });
          } else if (Array.isArray(v)) v.forEach((x) => walk(x, key));
          else if (v && typeof v === 'object') for (const [k, x] of Object.entries(v)) walk(x, k);
        };
        walk(json, '');
      } catch { /* the game's own content check reports broken JSON */ }
    } else if (f.startsWith(`${PATHS.resources}/Levels/`) && f.endsWith('.txt')) {
      for (const line of read(f).split(/\r?\n/)) {
        if (line.startsWith('---')) break;
        const title = line.match(/^title:\s*(.*)$/);
        if (title) out.push({ text: title[1], file: name(f) });
        const boss = line.match(/\bname=(\S+)/);
        if (boss) out.push({ text: boss[1].replace(/_/g, ' '), file: name(f) });
      }
    } else if (f.startsWith(`${PATHS.resources}/Content/`) && /\/credits(_[\w-]+)?\.txt$/.test(f)) {
      out.push({ text: read(f), file: name(f) });
    } else if (f.startsWith(`${LAYOUT.game}/Game/`) && f.endsWith('.cs')) {
      const cs = read(f).split('\n').filter((l) => !l.trim().startsWith('//')).join('\n'); // not the comments' examples
      for (const m of cs.matchAll(/Loc\.T\(\s*"((?:[^"\\]|\\.)*)"/g)) out.push({ text: m[1].replace(/\\n/g, '\n').replace(/\\(.)/g, '$1'), file: name(f) });
      if (f.startsWith(`${PATHS.core}Bosses/`)) for (const m of cs.matchAll(/\bName\s*=.*?"([^"]+)"\s*;/g)) out.push({ text: m[1], file: name(f) });
    }
  }
  return out;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const problems = checkProject();
  for (const p of problems) console.log(`${p.warn ? 'warn' : 'FAIL'}  ${p.file ? `${p.file}: ` : ''}${p.message}`);
  console.log(problems.some((p) => !p.warn) ? 'rules: FAIL' : 'rules: ok');
  process.exitCode = problems.some((p) => !p.warn) ? 1 : 0;
}

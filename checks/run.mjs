// The checks: one command that tells you whether the game still works.
//   npm run check
// Runs, in order:
//   unity      finds the pinned Unity editor (UNITY env, the Unity CLI, Unity Hub's folders)
//   editmode   compiles everything, then runs the EditMode tests (Assets/Tests/EditMode):
//              the simulation, levels, reach, every replay, bosses, content, saves
//   playmode   the PlayMode tests (Assets/Tests/PlayMode): the real Boot scene, screens,
//              input, touch controls, pause, finishing a level through the scene
//   rules      project rules: assets.json against the files, .meta files, one scene,
//              no engine code in Game.Core, the pinned Unity version and packages
// A failing step doesn't stop the later ones (except where one needs another),
// so one run shows every problem. The last line is
// CODEPLATE CHECKS: PASS or FAIL (<n> failed).
//   --skip=playmode       skip steps (for a quick loop; always finish with a full run)
//   --filter=Reach        only run tests whose full name contains this text
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { LOG_DIR, ROOT, compileErrors, explain, findUnity, installHelp, runUnityRetrying } from '../tools/lib/unity.mjs';
import { checkProject } from './rules.mjs';

const arg = (name) => process.argv.find((a) => a.startsWith(`--${name}=`))?.slice(name.length + 3);
const skip = new Set((arg('skip') ?? '').split(',').filter(Boolean));
const filter = arg('filter');
const results = [];
let stepOutput = '';
let unity = null;

async function step(name, fn, needs = []) {
  const missing = needs.filter((n) => results.find((r) => r.name === n && !r.ok));
  if (skip.has(name) || missing.length) {
    const why = skip.has(name) ? 'skipped' : `skipped (${missing.join(', ')} failed)`;
    console.log(`\n--- ${name}: ${why}`);
    results.push({ name, ok: !missing.length, skipped: true });
    return;
  }
  console.log(`\n--- ${name}`);
  const start = Date.now();
  let ok = false;
  stepOutput = '';
  const log = console.log;
  console.log = (...a) => {
    stepOutput += `${a.join(' ')}\n`;
    log(...a);
  };
  try {
    ok = await fn();
  } catch (e) {
    console.log(e?.stack ?? String(e));
  } finally {
    console.log = log;
  }
  const seconds = (Date.now() - start) / 1000;
  console.log(`--- ${name}: ${ok ? 'pass' : 'FAIL'} (${seconds.toFixed(1)}s)`);
  results.push({ name, ok, seconds, tail: ok ? '' : stepOutput.trim().split('\n').slice(-30).join('\n') });
}

// Reads an NUnit 3 result file into { total, passed, failed: [{name, message}], skipped }.
function readResults(file) {
  if (!existsSync(file)) return null;
  const xml = readFileSync(file, 'utf8');
  const cases = [...xml.matchAll(/<test-case\b([^>]*?)(\/>|>([\s\S]*?)<\/test-case>)/g)];
  const attr = (s, n) => s.match(new RegExp(`\\b${n}="([^"]*)"`))?.[1] ?? '';
  const unxml = (s) => s.replace(/<!\[CDATA\[([\s\S]*?)\]\]>/g, '$1').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');
  const out = { total: cases.length, passed: 0, failed: [], skipped: 0 };
  for (const c of cases) {
    const result = attr(c[1], 'result');
    if (result === 'Passed') out.passed++;
    else if (result === 'Failed') {
      const body = c[3] ?? '';
      const message = unxml(body.match(/<message>([\s\S]*?)<\/message>/)?.[1] ?? '').trim();
      const stack = unxml(body.match(/<stack-trace>([\s\S]*?)<\/stack-trace>/)?.[1] ?? '').trim();
      const where = stack.split('\n').map((l) => l.trim()).find((l) => /Assets\/.*\.cs:\d+/.test(l));
      out.failed.push({ name: attr(c[1], 'fullname'), message, where: where?.match(/(Assets\/[^)\s]*\.cs:\d+)/)?.[1] ?? '' });
    } else out.skipped++;
  }
  return out;
}

async function runTests(platform) {
  const xml = join(LOG_DIR, `${platform.toLowerCase()}.xml`);
  // Only this game's tests (not the packages' own, which "testables" makes visible).
  const args = ['-runTests', '-testPlatform', platform, '-testResults', xml, '-assemblyNames', `Game.Tests.${platform}`];
  if (filter) args.push('-testFilter', filter);
  const r = await runUnityRetrying(unity.binary, args, { name: platform.toLowerCase(), timeoutMs: 15 * 60_000 });
  const errors = compileErrors(r.log);
  if (errors.length) {
    console.log(`Compile errors (${errors.length}):`);
    for (const e of errors.slice(0, 40)) console.log(`  ${e}`);
  }
  for (const why of explain(r.log)) console.log(why);
  if (errors.length) {
    console.log('Fix the compile errors first: Unity ran the tests of the last version that compiled, so their results are left out.');
    return false;
  }
  if (r.timedOut) console.log(`Unity didn't finish within 15 minutes. Its log: ${r.logFile}`);
  const res = readResults(xml);
  if (!res) {
    console.log(`No test results were written (Unity exit code ${r.code}). Read the log: ${r.logFile}`);
    if (!errors.length) console.log(r.log.split(/\r?\n/).filter((l) => /error|exception/i.test(l)).slice(-15).join('\n'));
    return false;
  }
  for (const f of res.failed) {
    console.log(`FAIL ${f.name}${f.where ? `  (${f.where})` : ''}`);
    if (f.message) console.log(`     ${f.message.split('\n').join('\n     ')}`);
  }
  // Unity errors logged during the run that no test expected also fail it (LogAssert);
  // they show up as failures above.
  console.log(`${platform}: ${res.passed}/${res.total} passed${res.skipped ? `, ${res.skipped} skipped` : ''} in ${r.seconds.toFixed(0)}s`);
  if (res.total === 0 && !filter) {
    console.log('No tests ran: the test assemblies probably failed to compile.');
    return false;
  }
  return res.failed.length === 0 && (r.code === 0 || r.code === 2) && res.passed + res.skipped === res.total;
}

await step('unity', () => {
  unity = findUnity();
  if (!unity.binary) {
    console.log(installHelp(unity.version));
    if (unity.tried.length) console.log(`Looked at:\n  ${unity.tried.join('\n  ')}`);
    return false;
  }
  console.log(`Unity ${unity.version}: ${unity.binary}`);
  return true;
});

await step('editmode', () => runTests('EditMode'), ['unity']);
await step('playmode', () => runTests('PlayMode'), ['unity', 'editmode']);
await step('rules', () => {
  const problems = checkProject(ROOT);
  for (const p of problems) console.log(`${p.warn ? 'warn' : 'FAIL'}  ${p.file ? `${p.file}: ` : ''}${p.message}`);
  const failed = problems.filter((p) => !p.warn);
  console.log(failed.length ? `${failed.length} rule(s) broken` : 'project rules hold');
  return failed.length === 0;
});

const failed = results.filter((r) => !r.ok);
for (const r of failed.filter((f) => f.tail)) console.log(`\n=== ${r.name} failed; the end of its output:\n${r.tail}`);
console.log(`\n${results.map((r) => `${r.ok ? (r.skipped ? 'skip' : 'pass') : 'FAIL'}  ${r.name}${r.seconds ? `  ${r.seconds.toFixed(0)}s` : ''}`).join('\n')}`);
console.log(failed.length ? `CODEPLATE CHECKS: FAIL (${failed.length} failed)` : 'CODEPLATE CHECKS: PASS');
process.exitCode = failed.length ? 1 : 0;

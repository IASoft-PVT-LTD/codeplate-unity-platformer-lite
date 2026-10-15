// Runs the project's editor tools from the command line (Unity in batch mode).
//   npm run shots -- screen=level level=3 x=40          a screenshot in shots/ (see ShotDirector.cs)
//   npm run shots -- level=5 replay=900 frames=6 every=8  a contact sheet mid boss fight
//   npm run reach -- level=3                            where the player can get to (all levels without level=)
//   npm run replay -- level=3 trace=30                  play a replay, print the state every 30 ticks
//   npm run autoplay -- level=3                         the bot plays and writes replays/level_03.replay
//   npm run feel                                        jump height, distance, air time for config.json
//   npm run build:web | build:windows | build:macos     player builds into Builds/
// Close the Unity editor first if it has this project open (or use `unity mcp` to drive it).
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT, codeplateLines, compileErrors, explain, findUnity, installHelp, runUnityRetrying } from './lib/unity.mjs';

const [tool, ...rest] = process.argv.slice(2);
const args = rest.filter((a) => /^[\w-]+=/.test(a) || a === '--touch').map((a) => (a === '--touch' ? 'touch=1' : a)).join(' ');

const METHODS = {
  shots: { method: 'CodePlate.Shots.Run', flag: '-codeplateShot', graphics: true, quit: false },
  reach: { method: 'CodePlate.Tools.Reach', flag: '-codeplateArgs' },
  replay: { method: 'CodePlate.Tools.Replay', flag: '-codeplateArgs' },
  autoplay: { method: 'CodePlate.Tools.Autoplay', flag: '-codeplateArgs' },
  feel: { method: 'CodePlate.Tools.Feel', flag: '-codeplateArgs' },
  'build web': { method: 'CodePlate.Builds.Web', timeout: 60 * 60_000 },
  'build windows': { method: 'CodePlate.Builds.Windows', timeout: 60 * 60_000 },
  'build macos': { method: 'CodePlate.Builds.MacOS', timeout: 60 * 60_000 },
};

const key = tool === 'build' ? `build ${rest[0]}` : tool;
const spec = METHODS[key];
if (!spec) {
  console.error(`usage: node tools/unity.mjs <${Object.keys(METHODS).join('|')}> [key=value ...]`);
  process.exit(2);
}

const unity = findUnity();
if (!unity.binary) {
  console.error(installHelp(unity.version));
  process.exit(1);
}

const unityArgs = ['-executeMethod', spec.method];
if (spec.quit !== false && !key.startsWith('build')) unityArgs.push('-quit');
if (spec.flag) unityArgs.push(spec.flag, args || 'none=0');
console.log(`Unity ${unity.version}: ${spec.method} ${args}`);
const r = await runUnityRetrying(unity.binary, unityArgs, {
  name: key.replace(' ', '-'),
  graphics: !!spec.graphics,
  timeoutMs: spec.timeout ?? 10 * 60_000,
});
// The WebGL build leaves Burst's intermediate output in Data/ at the project root; it's not part of the project.
if (key.startsWith('build')) rmSync(join(ROOT, 'Data'), { recursive: true, force: true });
for (const line of codeplateLines(r.log)) console.log(line);
const errors = compileErrors(r.log);
if (errors.length) console.log(`Compile errors:\n  ${errors.join('\n  ')}`);
for (const why of explain(r.log)) console.log(why);
if (r.code !== 0) {
  const logged = r.log.split(/\r?\n/).filter((l) => /^\[CodePlate\].*(ERROR|failed)|Exception|error CS/.test(l)).slice(-10);
  if (logged.length) console.log(logged.join('\n'));
  console.log(`failed (exit ${r.code}${r.timedOut ? ', timed out' : ''}). Full log: ${r.logFile}`);
}
process.exitCode = r.code === 0 ? 0 : 1;

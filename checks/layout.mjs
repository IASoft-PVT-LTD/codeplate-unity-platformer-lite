// Where the project's files are, for the checks and tools/. The C# side has the same paths in
// Assets/Game/Core/ProjectPaths.cs; a project rule (checks/rules.mjs) fails when the two differ.
//   KIT      this kit's folder: checks/, tools/, package.json, assets.json, codeplate.json
//   PROJECT  the Unity project folder (the one holding Assets/ and ProjectSettings/). It's the
//            same folder as KIT here; the tools look for it rather than assume it.
// Paths in LAYOUT are relative to PROJECT, with forward slashes.
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const LAYOUT = {
  game: 'Assets', // holds Game/, Resources/, Scenes/, Settings/ and Tests/
  replays: 'replays',
  docs: 'docs',
  shots: 'shots',
  assets: 'assets.json', // the asset manifest
};

export const KIT = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/** The nearest folder at or above `from` that holds ProjectSettings/ and Assets/. */
export function findProject(from = KIT) {
  for (let dir = resolve(from); ; dir = dirname(dir)) {
    if (existsSync(join(dir, 'ProjectSettings')) && existsSync(join(dir, 'Assets'))) return dir;
    if (dirname(dir) === dir) return resolve(from);
  }
}

export const PROJECT = findProject();

/** Project-relative paths derived from LAYOUT. */
export const PATHS = {
  resources: `${LAYOUT.game}/Resources`,
  art: `${LAYOUT.game}/Resources/Art`,
  audio: `${LAYOUT.game}/Resources/Audio`,
  credits: `${LAYOUT.game}/Resources/Content/credits.txt`,
  bootScene: `${LAYOUT.game}/Scenes/Boot.unity`,
  coreAsmdef: `${LAYOUT.game}/Game/Core/Game.Core.asmdef`,
  core: `${LAYOUT.game}/Game/Core/`,
  projectPaths: `${LAYOUT.game}/Game/Core/ProjectPaths.cs`,
};

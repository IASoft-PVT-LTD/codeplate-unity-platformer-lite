# Unity Platformer: agent brief

You are building a game for the user, starting from this template. This brief tells you how the project works, what to ask before changing anything, the rules that keep it working, and how to check every change. Read it all once. Then follow **Interview** before your first edit.

## Start here

This folder is a complete pixel-art 2D platformer in **Unity 6 (6000.6.5f1) and C#**: "Sprocket", a wind-up robot climbing a clockwork tower. It renders 480×270 game pixels scaled up crisply, and plays with keyboard, gamepad or touch. It includes:
- **Movement:** tight running and jumping (coyote time, jump buffering, variable height, apex hang, corner and ledge correction) and a **dash** that breaks dash walls. Double jump and wall jump are ready behind config switches, off by default.
- **Levels:** 5 levels in one world, written as **plain-text ASCII maps**, each with coins, 3 hidden gems, hearts and checkpoints. The last level is a **boss** fight with a charging beetle.
- **Hazards and enemies:** spikes, pits, crumbling blocks, one-way ledges, springs, moving platforms, dash walls; walkers, flyers, spikers (don't stomp) and shooters (dodge their shots).
- **Screens:** title, world map, HUD, pause, results (time, gems, best), settings (volumes, shake, fullscreen, touch layout, reset progress) and credits. Progress and settings are saved.
- **Touch controls** that appear on touch screens, music for the world and the boss, and sound effects.

**Verify every change** with one command, run from this folder. It must end with `CODEPLATE CHECKS: PASS`:

```
npm run check
```

It needs Node 20+ and **Unity 6000.6.5f1 exactly** (with a license activated in Unity Hub; the free Personal license works). It finds the editor itself: the `UNITY` environment variable first (the editor binary), then the Unity CLI's installed editors (`unity editors --installed`), then Unity Hub's folders (`C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe`, `/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity`). If it's missing, the check says how to install it: Unity Hub (Installs > Install Editor > Archive) or `unity install 6000.6.5f1`. Write the editor path in `docs/project.md` once you know it.

A warm run takes under a minute (two Unity launches: EditMode, then PlayMode). The first run on a fresh copy imports everything and takes a few minutes.

## Interview

Before you change anything, read `docs/project.md`.

**If it still contains `<!-- codeplate:project-stub -->`, interview the user first.** Ask only what you need, in one short message, and suggest defaults so they can just say "yes":
1. **Concept, hero and setting.** What's the game about? What's it called? Example: "a cat climbing a cat tree", "a diver in a sunken city".
2. **Assets.** Do they have art and sound, and where? Pixel art at what tile size (16, 32…), or high resolution? If they have none, the placeholders are free to ship.
3. **Movement.** Floaty or snappy? Keep the dash? Double jump or wall jump from the start?
4. **Content.** How many worlds and levels? Bosses? New enemies, hazards or powers?
5. **Rules.** Health, lives, checkpoints; what gems or coins unlock.
6. **Platforms.** Desktop (Windows, macOS), web, phones? Touch buttons are built in.

Then replace the stub in `docs/project.md` with their answers as short decisions, and remove the marker.

**Otherwise, the project has started.** `docs/project.md` holds the decisions so far. Follow them, and add new decisions as the user makes them. This file is how the next session knows what was agreed.

Work in small steps: make one change, run the checks, look at a screenshot (**Seeing your work**), tell the user, continue.

Once the user has named their game, rewrite `README.md` for their game, change the product name, and rename the worlds, levels and bosses players see (Recipe `reskin-and-rebrand.md`, **Names players see**). Keep `codeplate.json`, `LICENSE.md` and `CREDITS.md`.

## Architecture

A **pure C# simulation plus Unity views**. Details: `docs/architecture.md`.

| Folder | What lives there |
|---|---|
| `Assets/Game/Core/` | **The simulation** (`Game.Core.asmdef`, `noEngineReferences: true`: the compiler refuses UnityEngine here). `World.Step(input)` runs 60 times a second: `TileGrid`, `Collide`, `Player` (all movement), actors in `Actors/` (Walker, Spiker, Flyer, Shooter + Projectile, MovingPlatform, Spring, Pickup, Checkpoint) and `Bosses/` (Beetle). Also `LevelData` (level files), `GameConfig`/`EnemyConfig` (the JSON numbers), `Reach` (level linter), `Replay`, `Autopilot` (bot), `Progress` (save format + migrations). Same level, config and inputs always give the same run. |
| `Assets/Game/Runtime/` | **The game in Unity** (`Game.Runtime.asmdef`). `Boot` builds everything from code; `App` holds the services; `LevelScreen` ticks the World in `FixedUpdate` and turns its events into sound, effects and HUD; views in `Level/` only draw; screens in `UI/` (UI Toolkit). `Art.cs` lists every sprite strip; `InputSource` reads the controls. |
| `Assets/Game/Editor/` | Editor-only tools: screenshots (`Shots`), `Tools` (reach, replay, autoplay, feel), `Builds`, `ImportSettings` (pixel-art import), `ProjectSetup`. |
| `Assets/Resources/Content/` | `config.json` (**every movement, combat, camera and feel number**), `enemies.json`, `worlds.json`, `controls.inputactions` (key bindings), `credits.txt`. |
| `Assets/Resources/Levels/` | `level_NN.txt` ASCII maps and `legend.json`. Format: `docs/levels.md`. |
| `Assets/Resources/Art/`, `Audio/` | Sprite strips, backgrounds, UI images, the pixel font, sounds and music. Every file is listed in `assets.json`. |
| `Assets/Resources/UI/` | `*.uxml` screens and `Game.uss` (all styles and colors), `Theme.tss`. |
| `Assets/Scenes/Boot.unity` | The only scene: one object with `Boot`. |
| `Assets/Tests/EditMode/` | Pure tests: movement, collision, actors, bosses, every level (reach + replays), content, saves. |
| `Assets/Tests/PlayMode/` | The real game: boot, menus, keyboard and touch input, pause, finishing a level, settings, a boss fight, art. |
| `replays/` | Recorded runs (text). The checks replay each one: proof every level can still be finished. |
| `Assets/WebGLTemplates/Game/` | The web page around the web build: full-window canvas, loading bar, "turn your phone sideways" in portrait. |
| `checks/`, `tools/` | `checks/run.mjs` (the checks), `checks/rules.mjs` (project rules), `checks/layout.mjs` (where files are; the same paths as `Assets/Game/Core/ProjectPaths.cs`), `tools/unity.mjs` (runs the editor tools), `tools/lib/unity.mjs` (finds and runs Unity). |

## Rules

1. **Rules live in `Game.Core`, drawing lives in `Game.Runtime`.** Core never uses UnityEngine (no `Vector2`, `Mathf`, `Debug`, `Time`, `Random`), never reads files, and views never write to the simulation. That's what makes runs deterministic and testable without the editor.
2. **Keep the custom movement.** Don't replace it with `Rigidbody2D`, `CharacterController2D`, colliders or Unity physics. Replays, Reach, the bot and the tests all depend on the deterministic tile simulation. Change how it feels in `config.json`; change what's possible in `Player.cs`.
3. **One scene, built from code.** Never add objects to `Boot.unity`, never create prefabs or new scenes, and never hand-edit `.unity`, `.prefab`, `.asset` or `.meta` YAML (GUIDs and fileIDs break silently). Create GameObjects in code (`LevelScreen`, `Boot`, the views), UI in UXML/USS, data in JSON/text.
4. **No randomness or real time in Core.** Count ticks (`GameConfig.Ticks(seconds)`), use `World.Rng` if you need randomness. The rules check fails on `System.Random` or `DateTime` in Core.
5. **Every tunable is data.** Movement and feel: `Content/config.json` (add a field with a default to `GameConfig.cs` too). Enemies and bosses: `Content/enemies.json` + `EnemyConfig.cs`. Worlds: `worlds.json`. Keys: `controls.inputactions`. Movement is in **tiles** and **seconds**.
6. **Levels are text.** Edit `Assets/Resources/Levels/level_NN.txt` and `legend.json`. Number levels with no gaps. Each needs exactly one `P`, one `G` and 3 gems `*`; a world's last level should have a boss (a world marked `"bonus": true` in `worlds.json` may skip it).
7. **Every file under `Assets/Resources/Art/` and `Audio/` is listed in `assets.json`** with its real frame size and count, and every sprite strip is in the art table in `Assets/Game/Runtime/Art.cs` with the same frame size. A wrong number shows the wrong frames.
8. **Let Unity make `.meta` files.** New files get their `.meta` when Unity next imports (any `npm run check` does that); ship them with the files. Moving a file? Move its `.meta` with it. Deleting one? Delete its `.meta` too.
9. **Keep the pinned editor and the packages.** Open the project only with 6000.6.5f1 (another version rewrites project files and the checks refuse it). Add a package only as **Adding a package** below says.
10. **Keep the template's licensing clean.** Use only the user's own assets, assets they give you, or CC0. Record each new asset's `license` and `credit` in `assets.json`, and credit it in `Content/credits.txt`. Don't paste code from tutorials or other projects. `CodePlate-original` marks only the template's own placeholders; for art or sound you make for the user, put their game or team name in `license` and `credit`.
11. **Never weaken a test, and never delete a replay to make the checks pass.** If the user deliberately changed a rule a test checks, update that test and say so. If a movement change breaks a replay, see **Verify**, step 3.
12. **Add tests for new rules.** Pure tests go in `Assets/Tests/EditMode/` on small rooms (`Kit.Room("..P..", "#####")`), using only Game.Core (so CI can run them without Unity). Copy the style of `WorldTests.cs` and `ActorTests.cs`. A test that needs a movement switch sets it (`Kit.Room(Kit.Moves(wallJump: true), ...)`) instead of relying on what `config.json` ships with. Scene behaviour goes in `Assets/Tests/PlayMode/GameTests.cs`.
13. **Keep the CodePlate credit.** This template's free license (`LICENSE.md`) asks that anything the user shares shows "Built with CodePlate (codeplate.io)" where people can see it. It's a line in `Assets/Resources/Content/credits.txt`, which players see on title > Credits, and a check fails if it goes missing. Keep it when you rebrand. The credit is optional only if `LICENSE.md` is the **CodePlate Pro License**: a copy downloaded with an active CodePlate paid plan comes with it. If the user asks to remove the credit and `LICENSE.md` is still the free license, tell them that.

## Verify

After every change:

1. **Run `npm run check`.** It reports compile errors with file:line, failed tests by name with their message, and broken project rules. Errors Unity logs during a PlayMode test fail that test. To run a subset while iterating: `npm run check -- --filter=Reach` (test names containing "Reach"), `--skip=playmode`. Always finish with a full run.
2. **Look at it.** Take a screenshot (**Seeing your work**), then open the PNG and look. Taking it isn't enough. Check that sprites sit on the tiles, text is readable, nothing is cut off, colors fit.
3. **After any movement, level or physics change:**
   - **Reach** (in `LevelTests`) proves every gem, checkpoint and goal is still reachable with the current settings (double and wall jumps included when they're on). If it fails, see the map: `npm run reach -- level=N`.
   - **Replays** prove every level can still be finished (and its boss beaten). If one breaks, see where: `npm run replay -- level=N trace=30`. Then fix the level, or make a new run: `npm run autoplay -- level=N`. Tell the user what happened. `npm run replay` also lists which enemies each run meets and defeats: a new enemy no replay meets isn't tested by them.
4. **Web and phones.** If `docs/project.md` says web or phones, check them as you go, not only at the end: after a change to screens, text, colors or controls, take a touch screenshot (`npm run shots -- screen=level level=1 x=8 touch=1`); after a milestone (a reskin, a new world, a new HUD element), make a web build (`npm run build:web`, Recipe `builds.md`) and open it in a browser, also at a phone's landscape size. Can't build or open it here? Tell the user, and how to try it.
5. **Tell the user** what changed, show the screenshot, and suggest they play it (open the folder in Unity Hub with 6000.6.5f1 and press Play, or `npm run build:web`).

The checks cover: movement against config (speeds, heights, coyote, buffer, corner correction, dash, double and wall jump), collision edge cases, every actor and the boss, every level's format, reachability and replay, determinism, the save format and its migration, every screen, real keyboard input and the touch buttons, pause, results and saving, the art table against `assets.json`, `.meta` files, the one-scene rule, the engine-free Core, and the pinned editor and packages.

## Recipes

Step-by-step guides for common requests, in `docs/recipes/`. Follow the matching one when it fits; each ends with its own verification.

- `reskin-and-rebrand.md`: the hero's sprites (any size or frame count), tiles, backgrounds, UI colors and font, the game's name and icon.
- `tune-the-feel.md`: floatier, snappier, faster, higher; dash distance; camera and screen shake.
- `add-level.md`: a new level, reachable and finishable, with a replay.
- `add-world.md`: a second world with its own art, music and map page.
- `add-enemy.md`: a new enemy (logic, legend letter, art, tests).
- `add-hazard.md`: lava, ice, conveyors, saws, new block types.
- `add-boss.md`: a new boss with its own state machine, beatable by the bot.
- `add-ability.md`: wall climb, ground pound, glide; or turn on double jump / wall jump, also as unlockables.
- `add-collectible-and-unlock.md`: new pickups, and things gems or coins unlock.
- `change-health-and-lives.md`: one-hit mode, lives and game over, checkpoint rules.
- `touch-controls-and-mobile-web.md`: touch button layout, size, visibility; playing on phones in the browser.
- `music-and-sound.md`: replace or add sounds and music, volumes.
- `save-data.md`: change what's saved without breaking old saves (versions and migrations).
- `builds.md`: build for the web, Windows and macOS.

`docs/customization-map.md` answers "I want to change X — which file?" for everything else. `docs/movement.md` explains every movement number. `docs/levels.md` is the level format.

## Seeing your work

Screenshots run the real game in a hidden editor (about 15–25 s each) and save a PNG in `shots/`:

```
npm run shots -- screen=level level=3 x=40          # level 3, player standing at column 40
npm run shots -- screen=level level=2 x=10 ticks=45 hold=R   # then run right for 45 ticks
npm run shots -- screen=level level=5 replay=900     # 900 ticks into the level's replay (mid boss fight)
npm run shots -- screen=level level=1 x=20 frames=6 every=8 hold=RJ   # a contact sheet: 6 shots, 8 ticks apart
npm run shots -- screen=title | screen=map world=1 progress=demo | screen=settings | screen=pause level=1 | screen=results level=1
npm run shots -- screen=level level=1 x=8 touch=1    # with the touch buttons
```

Open the PNG it prints and look at it. Contact sheets show motion and animation. **After a feel or movement change** (jump height, speed, dash, double or wall jump), make a contact sheet of the changed move and look at it before telling the user; numbers alone don't show how it plays. These run without graphics and print text:
- `npm run reach -- level=3`: the map with everywhere the player can reach.
- `npm run feel`: jump height and distance, air time, dash, for the current `config.json`.
- `npm run replay -- level=3 trace=30` (also lists the enemies each replay meets and defeats), `npm run autoplay -- level=3`.

## Unity pitfalls

- **Unity's editor is open on this project?** Batch runs fail with "another Unity instance is running with this project open". Ask the user to close it, or drive the open editor with the Unity CLI's MCP server (`unity mcp`) instead.
- **"Unity's compiler couldn't start (out of memory?)"**: other builds are using the memory. Close them and run again; the check retries once by itself.
- **A new file has no `.meta`:** run the checks once (Unity imports and creates it), then ship both. Never write `.meta` files or GUIDs by hand.
- **`Resources.Load` paths have no extension and start under `Resources/`**: `Resources.Load<TextAsset>("Levels/level_03")`, `App.Art.Strip("player/run")`.
- **Domain reload:** static fields reset when scripts recompile or play mode starts. Keep state in `App` and the screens, not in statics you expect to survive.
- **Input:** this project uses the **Input System** package only (`activeInputHandler` = Input System). `UnityEngine.Input.GetKey` throws here. Read `App.Input` (or add an action to `controls.inputactions`).
- **Rendering:** URP with the 2D renderer and a Pixel Perfect Camera. Snippets for the Built-in pipeline (`OnRenderImage`, `Shader "Standard"`) don't apply. Sprites use the default 2D sprite material, lit by one global 2D light.
- **UI is UI Toolkit** (UXML + USS), not uGUI: no `Canvas`, `Text` or `Image` components. Style in `Assets/Resources/UI/Game.uss`.
- **Name clashes:** `Game.Core` has `World`, `Player`, `Actor`, `SimEvent`; Unity has `UnityEngine.EventType`, `Random`, `Object`. Inside `Game.Runtime`, write `Game.Core.X` or `UnityEngine.X` when the compiler says "ambiguous reference".
- **The Unity CLI** (`unity`, beta) installs editors (`unity install 6000.6.5f1`, `unity install-modules`), lists them (`unity editors --installed`) and can drive a running editor (`unity mcp`). The checks don't need it.

## Adding a package

The manifest is minimal on purpose (fast imports and web builds). If a feature truly needs a package: tell the user which and why, add it with its exact version to both `Packages/manifest.json` and `checks/packages.json`, run the checks (Unity resolves it and updates `Packages/packages-lock.json`; ship that too), and note it in `docs/project.md`.

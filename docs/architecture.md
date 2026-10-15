# Architecture

## One tick

```
Input System actions ─┐
Touch buttons ────────┼─► InputSource.Read() ──► InputFrame bits (L R U D J X)
                      │                               │
LevelScreen.FixedUpdate (60/s) ───────────────────────┤
                                                      ▼
                                  World.Step(bits)   Game.Core: pure C#, deterministic
                                      1. actors move (platforms carry the player; shooters spawn projectiles)
                                      2. Player.Step: run, jump, dash, gravity, Collide
                                      3. crumbling, touching actors (stomp / hurt / collect / spring)
                                      4. spikes, pits, death and respawn (bosses reset)
                                                      │
                                                      ▼
                                  List<SimEvent>  (Jump, Land, Coin, Gem, Stomp, Hurt, Die,
                                                   BossWake, BossHit, GoalOpen, Goal ...)
                                                      │
LevelScreen.OnEvent ──► AudioDirector, Fx, CameraRig.Shake, PlayerView squash, HUD toasts, results
LevelScreen.Update  ──► views draw positions interpolated between the last two ticks
```

The simulation never reads Unity, and the views never write to the simulation. So:
- the **EditMode tests** run levels without a scene, and also run under plain .NET (CI);
- **replays** (`replays/level_NN.replay`) re-run exactly, tick for tick;
- the **Autopilot** bot and the **Reach** linter use the same movement code as the game.

## Game.Core (`Assets/Game/Core/`, `noEngineReferences: true`)

| File | Job |
|---|---|
| `World.cs` | The level simulation: grid, player, actors, bosses, coins, gems, deaths, checkpoint, goal. `Step(input)` returns the tick's events; `Snapshot()` for determinism tests. |
| `Player.cs` | All movement and health: run, jump, coyote, buffer, apex hang, dash, double jump, wall jump, hurt, death. Every number from `GameConfig`. |
| `Collide.cs` | Moves a `Body` (a box positioned at its feet) through the grid, x then y, in small steps; one-way tiles, moving surfaces, corner and ledge correction, dash walls. |
| `TileGrid.cs` | Flags per cell: Solid, OneWay, Spikes, Crumble, DashWall. `TileGrid.FromRows(...)` for tests. |
| `Actors/*.cs` | `Actor` and its kinds. Each has `Setup`, `Step`, `Touched(world, stomp)`, `Anim`, `Facing`, `Surface`. |
| `Bosses/*.cs` | `Boss` (health, phases, wake, reset on respawn), `Beetle`. |
| `LevelData.cs` | Parses level files with `legend.json`; errors name the line and column. `ActorTypes` maps legend names to classes. `LevelData.FromRows(...)` for tests. |
| `GameConfig.cs`, `EnemyConfig.cs` | The numbers in `config.json` and `enemies.json`, with defaults and docs. `Derived()` solves gravity and launch speeds. |
| `GameContent.cs` | Loads config, enemies, worlds, legend and every level through a reader function (Resources in the game, files in tests and tools). |
| `Reach.cs` | The level linter: records jump/fall/dash/double-jump paths with the real `Player` in empty space, replays them through `Collide` on the level, plays wall-jump paths live against the level's walls (when on), reports unreachable targets. |
| `Replay.cs` | The run-length text format for inputs, and `Replay.Play` (which also logs the enemies a run meets and defeats). |
| `Autopilot.cs` | The bot: routes with Reach; fights bosses with a lookahead search (re-simulating short input plans). |
| `Progress.cs` | The save format (JSON with a version) and `Migrate` for old versions. |
| `Json.cs`, `V2d.cs` | A small JSON reader/writer; `V2d`, `Cell`, `Box`, `M` (math), `Trig` (a sine table built with + − × ÷ only), `Rng` (seeded). |
| `InputFrame.cs`, `SimEvent.cs` | Input bits and their replay letters; event types. |
| `ProjectPaths.cs` | Where the project's files are (resources, scene, replays, docs, shots). Code that builds a path to a project file uses it; `checks/layout.mjs` holds the same paths for the Node tools. |

### Units and time

- Positions are in **tiles**: x to the right, **y downward**, (0, 0) at the top-left of the map. A body's `Pos` is its **feet** (bottom-center).
- 60 ticks per second. Timers count ticks; config seconds become ticks with `GameConfig.Ticks()`.
- Jump speeds are solved numerically in `GameConfig.Derived()`, so `jumpHeight`, `airJumpHeight` and `springHeight` are reached exactly.
- Views convert with `LevelScreen.Feet(pos)`: Unity world units = tiles, **y up** (`y = -simY`), 16 pixels per unit.

## Game.Runtime (`Assets/Game/Runtime/`)

| File | Job |
|---|---|
| `Boot.cs` | The only scene component. Builds the camera (URP 2D, Pixel Perfect Camera at 480×270), a global 2D light, audio, input, UI; then the title (or a screenshot). |
| `App.cs` | Services: `Content`, `Config`, `Progress` (+ `Save()`), `Art`, `Audio`, `Input`, `Screens`, the current `Level`. |
| `Art.cs` | `ArtSlots`: every sprite strip with its frame size and fps. `Art` cuts textures into sprites (feet pivot for characters, center for tiles). |
| `AudioDirector.cs` | `Play("coin")`, `PlayMusic("music_world1")` with crossfade; volumes from settings. |
| `InputSource.cs` | Input System actions (`Content/controls.inputactions`) + touch bits → `InputFrame` bits. |
| `SaveStore.cs` | Reads/writes `progress.json` in `Application.persistentDataPath`. |
| `ShotDirector.cs` | Drives the game to a state for `npm run shots` and saves a PNG. |
| `Level/LevelScreen.cs` | Plays one level: ticks the world, routes events, interpolates views, pause and results. `InputOverride` and `StepTicks()` let tests and tools drive it. |
| `Level/WorldView.cs` | A Tilemap built at runtime; terrain autotiles (frame = N·1 + E·2 + S·4 + W·8); crumbles and dash walls follow the sim. |
| `Level/PlayerView.cs`, `ActorView.cs` | Sprites picked from state; `ActorView.ART` maps each actor kind and anim to its strip. |
| `Level/CameraRig.cs`, `Backdrop.cs`, `Fx.cs`, `SpriteAnim.cs` | Camera follow and shake, parallax layers, little effects, strip playback. |
| `UI/ScreenRouter.cs` | The UI Toolkit panel (scaled by whole numbers), menu screens, the HUD layer, overlays (pause, results, settings). |
| `UI/*.cs` | `TitleScreen`, `WorldMapScreen`, `SettingsScreen`, `CreditsScreen`, `HudView`, `PauseMenu`, `ResultsPanel`, `TouchControls`, `MenuBackdrop`, `Ui` (helpers, the pixel font). |

Screens are UXML in `Assets/Resources/UI/` with all styles in `Game.uss` (colors are variables at the top). Code finds elements by `name`.

## Game.Editor (`Assets/Game/Editor/`)

`Shots` (screenshots: enters play mode in a hidden editor), `Tools` (reach, replay, autoplay, feel), `Builds` (web, Windows, macOS), `ImportSettings` (pixel-art import for everything in `Art/`, audio settings), `ProjectSetup` (how the URP assets and the Boot scene were made).

## Checks

`npm run check` → `checks/run.mjs`: finds Unity, runs `-runTests` for EditMode then PlayMode (NUnit XML results are parsed and failures printed), then `checks/rules.mjs`. Logs and results: `Logs/codeplate/`. `checks/layout.mjs` says where things are (the same paths as `ProjectPaths.cs`, and a rule checks they agree); the tools find the Unity project folder by looking for `ProjectSettings/`, so they work from any folder.

| Tests | Covers |
|---|---|
| `EditMode/MotorTests.cs` | Movement against config |
| `EditMode/WorldTests.cs` | Collision, blocks, pickups, health, goal |
| `EditMode/ActorTests.cs` | Enemies, shots |
| `EditMode/BossTests.cs` | The boss and the boss-gated goal |
| `EditMode/LevelTests.cs` | Every level: format, reach, replay; bonus worlds; enemies a replay meets; determinism; the `docs/levels.md` example |
| `EditMode/ReachTests.cs` | Reach and the Autopilot with double and wall jumps on and off |
| `EditMode/ContentTests.cs` | Config loading, derived numbers, legend, saves and migration |
| `PlayMode/GameTests.cs` | The real game: menus, keyboard, touch, pause, results + saving, settings, a boss fight, art |

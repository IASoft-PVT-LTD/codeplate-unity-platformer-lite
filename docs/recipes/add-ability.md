# Add an ability

Use this when the user wants double jump or wall jump, a new move (wall climb, ground pound, glide), or abilities that unlock as the game goes on.

## Turn on a built-in ability

1. In `Assets/Resources/Content/config.json`: `"doubleJumpEnabled": true` (and `airJumps`, `airJumpHeight`), or `"wallJumpEnabled": true` (and the `wall*` numbers). To remove the dash: `"dashEnabled": false` — then check that no level needs it (dash walls `d`, long gaps): `npm run reach`.
2. Reach and the bot already use double jumps and wall jumps when they're on (wall jumps are tried live against each level's walls: up one wall, or between two walls of a chimney), so a ledge only a wall jump reaches counts as reachable. Run `npm run check`; re-record replays that broke with `npm run autoplay -- level=N`. Each replay's `# moves:` line says which moves were on when it was recorded: a replay that breaks says when a move was switched on since, and `ReplayTests` fails until each replay's line matches `config.json` again (re-record it, or, if it still finishes, update the line by hand).
3. Look at a contact sheet of the move (Recipe `tune-the-feel.md`, step 5).

## Add a new move (example: ground pound)

1. **Input:** if it needs a new button, add an action to `Assets/Resources/Content/controls.inputactions` (JSON: copy the `Dash` action and its bindings with new ids), a bit in `InputFrame` (next free bit `64`, a new letter in `Letters`), and read it in `InputSource.Read()`. Many moves reuse existing input instead (ground pound = down + jump in the air).
2. **Logic** in `Assets/Game/Core/Player.cs`: a timer or state (`_pounding`), a new `State` value if the view should show it, and an event (`SimEventType.GroundPound` in `SimEvent.cs`) for sound and effects. Numbers go in `config.json` + `GameConfig.cs` with a `...Enabled` switch.
3. **What it does to the world** (breaking blocks below, stunning enemies) goes in `World` (look at how dash walls break: `Collide.WallBreak.Break`).
4. **View:** a strip in `Art/player/` (in `assets.json` and `ArtSlots`), picked in `PlayerView.Show`; sound and effect in `LevelScreen.OnEvent`.
5. **Tests** in `MotorTests.cs`: the move happens with the input, not without; disabled when its switch is off. Set the switches a test relies on in the test itself (`Kit.Moves(...)`, or `cfg.groundPoundEnabled = true` on a `Kit.Moves()` copy), never rely on what config.json ships with.
6. Give it a name in `Replay.Moves` (`Core/Replay.cs`) next to `dash`, `double_jump` and `wall_jump`, so replays record whether it was on. A move that acts while a button is **held** (a glide, a slow fall, a charged jump) is safe for old replays: the bot lets go of jump once a jump starts to fall (`Autopilot.JumpRelease`), so recorded runs don't hold jump through falls.
7. If levels may require it, teach Reach: add a style in `Reach.RecordPaths` and its inputs in `Reach.PathInput` (look at `"dash"` and `"jump_dash"`), so Reach and the bot can use it. A move that depends on the level's tiles (like the wall jump) is a `Live` path, played against the level from each standing spot (look at `"wall"`). Add a test like `ReachTests`: a small room the move is needed for, unreachable without it, and the Autopilot finishing it.

## Unlock it later

Abilities can depend on progress: e.g. double jump after beating world 1's boss. Keep the switch in config as the default, and decide at level start in `LevelScreen.Begin`: copy the config (`var cfg = App.Config.Clone()`), set `cfg.doubleJumpEnabled = App.Progress.Level(5).Completed`, and pass `cfg` to `new World(...)`. Tests and Reach use config.json as is, so make levels before the unlock beatable without it. Tell the player when it unlocks (a toast on the results screen).

## Verify

- `npm run check` passes, including the new movement tests and every replay.
- A contact sheet shows the move: `npm run shots -- screen=level level=1 x=10 hold=<letters> frames=6 every=5`.

# Add an enemy

Use this when the user wants a new kind of enemy: a hopper, a charger, a turret that aims, a swooping bird.

1. **Pick the closest existing enemy** to copy: `Walker` (walks, turns at walls and ledges), `Spiker` (a walker you can't stomp), `Flyer` (a path that ignores walls), `Shooter` (spawns projectiles on a timer), or a boss's state machine (`Bosses/Beetle.cs`) for anything with phases.
2. **Write the logic** in `Assets/Game/Core/Actors/<Name>.cs`, a subclass of `Actor`:
   - `Setup(World)`: size, speed and parameters. Read numbers from `world.Enemies.<name>` (add a class and field to `EnemyConfig.cs` and the values to `Content/enemies.json`) and per-placement overrides with `Num("speed", default)` from the level's `at` line.
   - `Step(World)`: one tick of movement. Count ticks, not seconds (`GameConfig.Ticks(s)`); move with `Collide.MoveX/MoveY` if it should respect walls.
   - `Touched(World, stomp)`: call `world.Stomped(this)` to be stomped (then deactivate after a short squash), `world.HurtPlayer(Pos.X)` to hurt.
   - `Anim` (a state name for the view), `Facing`, and `IsDangerous` (the bot avoids dangerous actors; an actor that is ever dangerous counts as an enemy in `npm run replay`'s list). If it stays on screen for a moment once beaten (a squash), override `IsDefeated` too.
   - No UnityEngine, no `System.Random` (use `world.Rng`), no `Math.Sin` (use `Trig.SinTicks`).
3. **Register it:** add `["name"] = () => new Name()` to `LevelData.ActorTypes`, and a letter to `Assets/Resources/Levels/legend.json`: `"q": {"name": "hopper", "actor": "hopper"}`.
4. **Art:** add a strip under `Assets/Resources/Art/enemies/` (frames side by side, feet on the bottom row, facing right), list it in `assets.json` (size, frames, use, license, credit) and in the `ArtSlots` table in `Assets/Game/Runtime/Art.cs` (frame size, fps). Map its anims in `ActorView.ART`: `["hopper"] = new Dictionary<string, string> { ["*"] = "enemies/hopper_hop", ["squashed"] = "enemies/hopper_squashed" }`. No art yet? Generate a clearly-placeholder strip and tell the user.
5. **Place it** in a level (`q` in the map, `at x,y: hopper ...` for parameters).
6. **Test it** in `Assets/Tests/EditMode/ActorTests.cs` on a small room, e.g. `Kit.Room("P.......", "...q....", "########")`: it moves as intended, a stomp defeats it (or hurts, if it shouldn't be stomped), a side touch hurts.
7. **Re-record the replays** of levels you placed it in: `npm run autoplay -- level=N` (the old run doesn't know about it).
8. **Confirm a replay meets it:** `npm run replay -- level=N` lists every enemy in the level as met (came within 4 tiles, or touched), defeated, or `NOT met`. A replay that never meets the new enemy proves nothing about it: re-record (`npm run autoplay -- level=N`), or move the enemy onto the route the run takes (`npm run reach -- level=N` shows it), then check again. Finishing times that didn't change after adding an enemy are a hint the run never met it.

## Verify

- `npm run check` passes, including your new test and the level's replay.
- `npm run replay -- level=N` shows the new enemy as met (or defeated) in at least one level's replay.
- A screenshot shows it in the level: `npm run shots -- screen=level level=N x=<its column - 6>`, and a contact sheet shows it moving (`frames=6 every=10`).

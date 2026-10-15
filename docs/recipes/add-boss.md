# Add a boss

Use this when the user wants a new boss, or to change how the beetle fight goes.

Bosses are actors with health, phases and a small state machine (`Assets/Game/Core/Bosses/`). The level's goal stays shut until every boss is defeated; the boss sleeps until the player comes within its wake distance and resets to full health when the player respawns. The HUD shows its name and health; its world's `bossMusic` plays during the fight.

1. **Design the fight as states** with a clear tell and a clear opening: e.g. *idle → telegraph (warning) → attack → recover (open to a stomp)*. Each hit makes the next phase harder (per-phase arrays like `chargeSpeed: [9, 11, 13]`). Keep it beatable by jumping and stomping: the Autopilot bot fights by trying short input plans (waits, runs, jumps, dashes) and needs a window of at least ~0.5 s to land a stomp.
2. **Copy `Beetle.cs`** (moves on the ground, uses `Move(world, vx)`) to `Bosses/<Name>.cs`, subclass of `Boss`. For a boss that spawns things, see how `Actors/Shooter.cs` spawns its projectiles:
   - `Setup`: `MaxHealth = Health = ...`, `Size`, `Name`, `Body = new Body(Origin, Size)`.
   - `Step`: the state machine. `Wake(world)` when the player is near; `world.Emit(SimEventType.BossTelegraph / BossSlam, Pos)` for the view's sounds and shake; `world.Spawn(new Actor...)` for projectiles or blocks.
   - `Touched`: in the open state, `TakeHit(world)` on a stomp; elsewhere `Bump(world)` on a stomp and `world.HurtPlayer(Pos.X)` on a side touch.
   - `OnPlayerRespawn`: reset health, position and state (unless `Defeated`).
   - `Anim` names per state; `IsDangerous` true while touching it hurts.
3. **Numbers** go in `Content/enemies.json` under a new key, with a matching class in `EnemyConfig.cs`.
4. **Register** it: `["name"] = () => new Name()` in `LevelData.ActorTypes`. Levels place it with `B` and `at x,y: boss type=name` (optional `name=Big_Bertha health=4`).
5. **Art:** strips under `Art/bosses/`, listed in `assets.json` and `ArtSlots` (`Art.cs`), mapped per anim in `ActorView.ART` (no art yet? stand-in strips of its own: Recipe `reskin-and-rebrand.md`, **Stand-in art**). Its on-screen name is `name=` on its `at` line, with a default in its class. ActorView already shakes the sprite during `telegraph`/`windup`/`warn` anims and fades `defeated`.
6. **Tests** in `Assets/Tests/EditMode/BossTests.cs`: it wakes, its attack hurts a player in the way, it can only be hurt in its open state (set `boss.Current = State.X` and use `DropOn`), N hits defeat it and open the goal.
7. **Build its arena** (Recipe `add-level.md`, step 3) and record the replay: `npm run autoplay -- level=N`. If the bot can't win, widen the opening (longer stun, slower attack) rather than weakening the bot's check.

## Verify

- `npm run check` passes: the boss tests, and the level's replay finishes with the boss defeated.
- A contact sheet mid-fight looks right: `npm run shots -- screen=level level=N replay=<a tick during the fight> frames=6 every=10`.

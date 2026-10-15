# Change health, lives and death

Use this when the user wants one-hit mode, more hearts, different rules for falls and running out of hearts, lives and game over, or different checkpoint rules.

The shipped rules ("hearts matter", `Game.Core/World.cs`): an enemy, a shot or spikes cost a heart (knockback, a short blink); a fall into a pit costs a heart too and the player respawns at the last checkpoint keeping the hearts left (no refill); time up on the level clock costs one (`level-clock.md`). Losing the last heart is **out of hearts**: a short "Out of hearts / Back to the start of the level" card, then the level starts again from the very beginning (checkpoints, clock, coins and this attempt's gems reset). Saved progress (best times, gems from finished runs) is untouched: it only changes when a level is finished.

## Settings (`Assets/Resources/Content/config.json`, `"health"`)

| Key | Default | What |
|---|---|---|
| `maxHealth` | `3` | hearts; 1 = one-hit mode. The HUD draws one per point. Hearts `h` in levels heal one, up to this |
| `invulnTime`, `knockback` | 1.0, [6, -9] | the blink after a hit, the push away and up |
| `respawnDelay` | `0.9` | seconds before the respawn |
| `fallsCostHeart` | `true` | `false`: a fall only sends the player back to the checkpoint |
| `outOfHeartsRestarts` | `true` | `false`: the classic rule, the last heart lost means a respawn at the checkpoint with full hearts (the level never fails) |
| `outOfHeartsTime` | `2.2` | seconds the "Out of hearts" card shows before the restart |

Bosses reset when the player respawns; found gems and coins stay.

## A continue instead of the restart

`World.ContinueWithExtraHeart()` gives one heart back at the last checkpoint (the clock as it was) while the level is out of hearts, and counts `ContinuesUsed` for the attempt. `LevelScreen` has a hook for offering one: implement `partial void OfferContinue(ref bool offered)` in another file of the partial class (open your own screen, set `offered = true`, and later call `World.ContinueWithExtraHeart()` or `RestartLevel()`). For example: spend 50 coins for a heart.

## Lives and game over across levels

1. **Count lives** in `Progress` (they last across levels), set from a new `startLives` key in `config.json` + `GameConfig.cs`.
2. **On out of hearts** (`SimEventType.OutOfHearts` in `LevelScreen.OnEvent`): take a life; with none left, show a "Game over" panel and send the player to the world map (or reset progress: ask the user which).
3. **Extra lives** (e.g. every 50 coins): in `World.Collect`'s coin case, emit an event; `LevelScreen` adds the life.
4. **Show it:** a lives counter in `UI/Hud.uxml` (copy the coins label) filled in `HudView.Update`.
5. **Tests:** `HeartsTests` and `WorldTests` pin the shipped rules; update them if the rules change and say so; add tests for losing the last life.
6. **Replays** must finish without running out of hearts: if the rules get harsher, re-run `npm run check`; re-record broken replays with `npm run autoplay -- level=N` (it retries with other timings rather than dying).

## Checkpoint rules

Checkpoints are `Checkpoint` actors (`Assets/Game/Core/Actors/Props.cs`); `World.ReachCheckpoint` stores the spot. To make checkpoints heal, add `Player.Health = Config.maxHealth` there; to make them one-use per life, keep a list in `World`.

## Verify

- `npm run check` passes, with any changed test explained to the user.
- Screenshots: the hearts `npm run shots -- screen=level level=1 x=5 hearts=1`, the card `npm run shots -- screen=level level=1 x=5 outofhearts=1`.

# Change health, lives and death

Use this when the user wants one-hit mode, more hearts, lives and game over, or different checkpoint rules.

- **Hearts:** `maxHealth` in `Content/config.json` (1 = one-hit mode). The HUD draws one heart per point. Hearts `h` in levels heal one.
- **After a hit:** `invulnTime` (the blink), `knockback` ([away, up]).
- **Respawn wait:** `respawnDelay`. Respawn is at the last checkpoint touched, with full health; found gems and coins stay; bosses reset.

## Lives and game over

1. **Count lives** in the simulation so replays and tests see them: add `public int Lives` to `World` (set from a new config key `startLives` in `config.json` + `GameConfig.cs`), and in `World.Die()` decrement it.
2. **Game over** when lives run out: in `World.Respawn()`, if `Lives <= 0`, either restart the level (reset `Checkpoint = Level.Start`, `Lives = startLives`, and emit a new `SimEventType.GameOver`), or end it (`Finished = true` plus a `Failed` flag) — ask the user which.
3. **Extra lives** (e.g. every 50 coins): in `World.Collect`'s coin case, `if (Coins % cfg.coinsPerLife == 0) Lives++` and emit an event for a sound.
4. **Show it:** a lives counter in `UI/Hud.uxml` (copy the coins label) filled in `HudView.Update`; a "Game over" toast or panel in `LevelScreen.OnEvent`.
5. **Tests:** update `WorldTests.SpikesHurtAndPitsKill` and `PickupsCountOnceAndGemsSurviveDeath` if their expectations change, and say so; add tests for losing the last life and for extra lives.
6. **Replays** record deaths too: if the rules change what a death does, re-run `npm run check`; re-record broken replays with `npm run autoplay -- level=N`.

## Checkpoint rules

Checkpoints are `Checkpoint` actors (`Assets/Game/Core/Actors/Props.cs`); `World.ReachCheckpoint` stores the spot. To make checkpoints heal, add `Player.Health = Config.maxHealth` there; to make them one-use per life, keep a list in `World`.

## Verify

- `npm run check` passes, with any changed test explained to the user.
- A screenshot of the HUD shows the new hearts or lives: `npm run shots -- screen=level level=1 x=5`.

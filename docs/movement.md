# Movement settings

All of these are in `Assets/Resources/Content/config.json`, in **tiles** and **seconds** (the groups like `"run"` and `"jump"` are only for reading; every key is a field of `GameConfig.cs`, which documents it too). Run `npm run feel` before and after a change to see what the numbers add up to.

| Key | Default | What it feels like |
|---|---|---|
| `runSpeed` | 8.5 | Top speed, tiles per second. 7 = relaxed, 10+ = fast and twitchy. |
| `groundAccelTime` / `groundDecelTime` | 0.10 / 0.06 | Seconds to reach top speed, and to stop. Higher = slippery, as on ice. |
| `turnAccelMult` | 1.8 | How quickly you turn around. |
| `airAccelMult` / `airDecelMult` | 0.65 / 0.35 | Air control. Lower = committed jumps; 1.0 = full control. |
| `jumpHeight` | 3.25 | Tiles risen while jump is held. **Changing it changes which ledges are reachable**: run the checks. |
| `jumpMinHeight` | 1.2 | Tiles risen by a quick tap (variable jump height). |
| `jumpTimeToApex` | 0.36 | Seconds to the top. Longer = floatier, shorter = snappier. Gravity is derived from this and `jumpHeight`. |
| `fallGravityMult` | 1.7 | Gravity multiplier while falling. Higher = less floaty landings. |
| `apexHangThreshold` / `apexGravityMult` | 2.5 / 0.5 | A little hang time at the top of a held jump. |
| `maxFallSpeed` / `fastFallSpeed` | 18 / 24 | Terminal fall speed; holding down falls faster. |
| `coyoteTime` | 0.10 | Seconds after running off a ledge when jump still works. |
| `jumpBufferTime` | 0.12 | Seconds a jump press is remembered before landing. |
| `cornerCorrection` | 0.30 | Bumping a ceiling corner by this little slides you past it. |
| `ledgeCorrection` | 0.15 | Clipping a ledge top by this little pops you up onto it. |
| `dashEnabled` | true | The dash (X, Shift, J, or gamepad X / right shoulder). |
| `dashDistance` / `dashTime` | 3.5 / 0.14 | How far and how fast. The dash has no gravity, so it also crosses gaps. |
| `airDashes` | 1 | Dashes in the air before landing (ground dashes are unlimited, after `dashCooldown`). |
| `dashCooldown` | 0.25 | Seconds between dashes. |
| `doubleJumpEnabled`, `airJumps`, `airJumpHeight` | off, 1, 2.4 | Double (or triple…) jump. |
| `wallJumpEnabled`, `wallSlideSpeed`, `wallJumpVelocity`, `wallJumpLockTime`, `wallCoyoteTime` | off, 4, [9, −15.5], 0.15, 0.08 | Wall slide and wall jump. Velocity is [away from the wall, up (negative)]. |
| `maxHealth`, `invulnTime`, `knockback`, `respawnDelay` | 3, 1.0, [6, −9], 0.9 | Hearts, the blink after a hit, the push away, the wait before respawning. `maxHealth: 1` is one-hit mode. |
| `stompBounce` / `stompBounceHeld` | 11 / 17 | Bounce off a stomped enemy (tiles/s); higher while jump is held. |
| `springHeight` | 7 | Tiles a spring launches you. |
| `crumbleDelay` / `crumbleRespawn` | 0.45 / 3.0 | How long a crumbling block holds, and when it comes back. |
| `playerSize` | [0.625, 0.875] | The hitbox in tiles. The sprite is drawn bigger; the hitbox decides what fits through gaps. |

View-only keys (they don't affect the simulation, replays or Reach): `camLookahead`, `camDeadzone`, `camSmoothing`, `landSquash`, `shakeHurt`, `shakeStomp`, `shakeDie`, `shakeBoss`, `hitstopStomp`, `dustParticles`, `tilePx`, `viewWidth`, `viewHeight`, `touchControls`.

"Floatier" usually means a longer `jumpTimeToApex` and a smaller `fallGravityMult`. "Snappier" means the reverse, plus shorter accel and decel times.

**After any movement change, run the checks.**
- Reach re-tests every level with the new numbers.
- The replays must still finish. If one doesn't, see `docs/recipes/tune-the-feel.md`.

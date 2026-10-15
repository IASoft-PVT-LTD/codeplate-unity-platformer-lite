# Level clock (time limits)

Use this when the user wants a time limit on some or all levels, a different clock pressure (more or less time, other warning colours, more time pickups), or no clock at all.

How it works (`Game.Core/World.cs`, `LevelClock.cs`, `HudView` in `Runtime/UI/LevelUi.cs`): a level with `time_limit: S` in its header counts down from S seconds while the player is alive. The HUD clock sits big in the top middle: calm, then amber from `timeWarning` seconds left, then red with a pulse every second from `timeDanger`. Clock pickups (`o` in the map, `pickups/clock` art, `clock` sound) add `timeBonus` seconds. Time up costs a heart (the toast says "Time's up!") and sends the player back to the last checkpoint with `timeUpRefill` of the limit (the whole limit when no checkpoint was reached yet); on the last heart it's out of hearts (`change-health-and-lives.md`). A level without a limit shows the time taken. The sample game gives every level a limit (its robot winds down); `docs/levels.md` has the format.

## Settings (`Assets/Resources/Content/config.json`, `"clock"`)

| Key | Default | What |
|---|---|---|
| `clockEnabled` | `true` | `false`: no countdown anywhere, whatever the levels say |
| `timeLimit` | `0` | seconds for levels WITHOUT their own `time_limit:` (0: those levels have no countdown) |
| `timeBonus` | `10` | seconds a clock pickup adds |
| `timeWarning` | `30` | the clock turns amber at this many seconds left |
| `timeDanger` | `10` | red and pulsing from here |
| `timeUpRefill` | `0.6667` | share of the limit back on the clock after a time up at a checkpoint (above 0, at most 1) |

Colours: `--amber` and `--alarm` in `Assets/Resources/UI/Game.uss` (`.clock.warn`, `.clock.danger`).

## Steps

1. **Ask** which levels get a clock, and how tight: "relaxed" (about 2.5x the bot's run), "normal" (2x), "tight" (1.7x, the least the check allows).
2. **Measure** each level: `npm run replay -- level=N` prints the recorded run's time. Limit = that x the factor, rounded up to 5 s, and at least `par_time` + 10.
3. **Write** `time_limit: S` under `par_time:` in each `Assets/Resources/Levels/level_NN.txt`. To take a level's clock away, delete the line.
4. **Clock pickups**: put `o` on reachable cells, a few per level, by checkpoints and before the hard parts (players who explore get time back). Reach checks every one is reachable.
5. **No clock at all**: `"clockEnabled": false` (keeps the `time_limit:` lines for later), or delete the lines and the `o` pickups.
6. **Feel**: if time up should be harsher, lower `timeUpRefill`; if the clock should only warn, set `timeDanger` to 0.

## Verify

- `npm run check` passes. `LevelTests.EveryTimeLimitIsBeatenByTheReplayWithMargin` fails with a message saying which level and by how much when a limit is too tight or too loose; fix the limit (or re-record a faster run: `npm run autoplay -- level=N`). `ClockTests` covers the rules.
- Screenshots of the clock's stages: `npm run shots -- screen=level level=1 x=8 timeleft=25` (amber) and `timeleft=8` (red).

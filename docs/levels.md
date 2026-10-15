# Level format

Every level is a text file, `Assets/Resources/Levels/level_NN.txt` (two digits, no gaps: `level_01`, `level_02`, ...). The game builds the tiles, enemies and everything else from it when the level starts; there are no level scenes. `Game.Core.LevelData` reads it, and its errors name the file, line and column.

A file has a short header, a `---` line, then the map:

```text
title: Example Steps
world: 1
par_time: 30
time_limit: 45
at 14,12: flyer range=2
---
..............................
..............................
..............................
..............................
..............................
..............................
..............................
..............................
............*.................
..........=====...............
..............................
..............................
..............f......*........
..P.....c.c..........dd....*..
....w..............C.dd.o....G
##############################
##############################
```

## Header

| Line | Meaning |
|---|---|
| `title: ...` | Shown on the world map, the level intro and the results. |
| `world: N` | Which world: its art (`Art/tiles/terrain_wN.png`, `Art/bg/wN_*.png`), music and world-map page. The world must be in `Content/worlds.json`. |
| `par_time: S` | Target time in seconds, shown on the results screen. |
| `time_limit: S` | Optional: the level clock counts down from S seconds (see below). Leave it out for no countdown. |
| `at x,y: kind key=value ...` | Parameters for the actor at column x, row y of the map (both count from 0, top-left). The kind must match the map character there. |
| `# ...` | A comment. |

## Map

- Rows: 17 to 64. Columns: 30 to 400. Row 0 is the top. Short rows are padded with `.`.
- One tile = 16 px of art = 1.0 in the simulation. The screen shows about 30 × 17 tiles.
- Outside the map: solid walls left and right, open sky above. Falling below the bottom row kills (a pit).
- Exactly one `P`, one `G`, and exactly 3 gems `*` per level.
- A level whose world ends with it should have a boss (`B`); the goal stays shut until the boss is beaten. A world marked `"bonus": true` in `Content/worlds.json` may end without one (extra levels, a secret world).

`Assets/Resources/Levels/legend.json` says what each character means:

| Char | Meaning | Char | Meaning |
|---|---|---|---|
| `.` | empty | `P` | player start |
| `#` | ground (the world's terrain, autotiled) | `G` | goal |
| `=` | one-way ledge (jump up through it; down + jump drops) | `C` | checkpoint |
| `^` | spikes | `c` | coin |
| `x` | crumbling block (breaks under you, comes back) | `*` | gem (3 per level, saved) |
| `d` | dash wall (a dash breaks it for good) | `h` | heart (+1 health) |
| `s` | spring (launches `springHeight` tiles) | `-` | moving platform |
| `w` | walker (stomp it) | `f` | flyer (stomp it) |
| `k` | spiker (don't stomp it) | `t` | shooter (stomp it; dodge its shots) |
| `B` | boss: needs `at x,y: boss type=beetle` | `o` | clock pickup (+`timeBonus` seconds) |

## Actor parameters (`at` lines)

| Kind | Parameters (defaults in `Content/enemies.json`) |
|---|---|
| `platform` | `dx`, `dy`: how far it travels (tiles, default dx=4); `speed` (2); `w` width (3); `wait` seconds at each end (0.5). It starts at its map cell. |
| `walker`, `spiker` | `dir=-1` or `1` (start direction), `speed` |
| `flyer` | `range` (tiles each way), `speed`, `amp` (bob height) |
| `shooter` | `dir=-1` or `1` (fixed aim; default: aims at the player), `interval` (seconds between shots), `delay` (before the first shot) |
| `boss` | `type=beetle` (required), `health`, `name` (use `_` for spaces: `name=Big_Bertha`) |

## The level clock (`time_limit:`)

A level with `time_limit: S` in its header has a countdown: the HUD shows it big in the top middle, amber from `timeWarning` seconds left, red and pulsing from `timeDanger`. Clock pickups (`o`) add `timeBonus` seconds. When it runs out (time up), the player loses a heart and goes back to the last checkpoint with `timeUpRefill` of the limit on the clock (all of it if no checkpoint was reached: that's back at the start); on the last heart, time up is out of hearts. The clock only runs while the player is alive. A level without `time_limit:` shows the time taken instead (`timeLimit` in `config.json` gives such levels a countdown too; 0, the default, doesn't), and `clockEnabled: false` turns every countdown off. Every key is in `config.json` under `"clock"`; the recipe is `docs/recipes/level-clock.md`.

Picking a limit: about twice the level's recorded run (`npm run replay -- level=N` prints its time), rounded up to 5 s, and at least `par_time` + 10. `npm run check` holds every limit to that: the replay must finish inside 60% of it without the clock pickups, from every checkpoint it reaches it must finish inside 60% of what a time up there leaves, and the limit can't be more than 3x the run (or par time + 15 s, if that's more). Clock pickups are Reach targets, like gems: each must be reachable.

## Checking a level

```
npm run reach -- level=3        # the map with everywhere the player can get to, and anything unreachable
npm run autoplay -- level=3     # the bot plays it and writes replays/level_03.replay
npm run replay -- level=3 trace=30   # also lists the level's enemies: met, defeated, or NOT met
npm run shots -- level=3 x=40   # a screenshot at column 40
```

`npm run check` re-runs Reach on every level (every gem, checkpoint, clock pickup and the goal must be reachable with the current movement settings, including double and wall jumps when they're on) and plays every replay (it must finish and beat the boss without running out of hearts, inside the level's time limit).

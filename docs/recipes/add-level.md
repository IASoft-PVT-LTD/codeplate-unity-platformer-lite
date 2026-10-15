# Add a level

Use this when the user wants a new level, more levels in a world, or a level redesigned.

1. **Pick its number.** Levels play in file order: `Assets/Resources/Levels/level_01.txt`, `level_02.txt`, ... with no gaps. To add a level in the middle of a world, renumber the files after it (rename each `.txt` **and its `.txt.meta`**, and each `replays/level_NN.replay`), from the last one backwards so nothing is overwritten.
2. **Copy the closest level** to `level_NN.txt` (Unity makes the `.meta` on the next check). Set the header: `title:`, `world:` (must exist in `Content/worlds.json`), `par_time:` (seconds; about 1.5× the bot's time is fair).
3. **Draw the map** (format: `docs/levels.md`). Keep these in mind:
   - 17 to 64 rows; the screen shows about 30 × 17 tiles. Row 0 is the top.
   - Exactly one `P`, one `G`, three gems `*`. Hide gems: up high, behind a dash wall `d`, past a spring, or over a pit.
   - A held jump rises 3.25 tiles: a ledge 3 rows above the floor is the most a plain jump reaches. A running jump crosses about 5–6 tiles; a dash adds 3.5 (`npm run feel` prints the current numbers).
   - Put a checkpoint `C` before every hard part.
   - Moving platforms, flyers, shooters and bosses need an `at x,y:` line for their parameters (x = column, y = row, from 0 at the top-left).
   - A boss level: a closed arena with a wall on each side (the beetle charges until it hits one), the boss `B` with `at x,y: boss type=beetle`, and the goal `G` inside the arena (it opens when the boss is beaten).
4. **Check reachability:** `npm run reach -- level=N`. It prints the map with `+` where the player can stand and `!` on anything unreachable. Fix the map until it says all targets are reachable.
5. **Record a replay:** `npm run autoplay -- level=N`. The bot plays the level and writes `replays/level_NN.replay`. If it fails, it prints its last moves: usually a jump that's too tight. Loosen that spot (or ask the user to accept a hand-recorded replay).
6. **Look at it:** `npm run shots -- screen=level level=N x=10`, then a few more `x=` columns along the level, and `screen=map world=W progress=demo` for the map page. Open each PNG.

## Verify

- `npm run check` passes: the new level parses, Reach finds every gem, checkpoint and the goal, and the replay finishes (and beats the boss in a boss level).
- The screenshots show the level as intended, and the world map shows the new node.
- Tell the user the level's number, title, and the bot's time.

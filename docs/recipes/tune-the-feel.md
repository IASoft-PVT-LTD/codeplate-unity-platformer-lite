# Tune the feel

Use this when the user says jumping is too floaty or too stiff, the hero is too slow, the dash is too short, or the camera or shake bothers them.

1. **Measure first:** `npm run feel` prints jump height, distance and air time, dash distance and speed. Note the numbers.
2. **Change `Assets/Resources/Content/config.json`** (never the defaults in `GameConfig.cs`). `docs/movement.md` explains every key. Typical requests:
   - **Floatier:** raise `jumpTimeToApex` (0.36 → 0.42) and lower `fallGravityMult` (1.7 → 1.4).
   - **Snappier:** lower `jumpTimeToApex` (→ 0.30), raise `fallGravityMult` (→ 2.0), shorten `groundAccelTime`/`groundDecelTime`.
   - **Jump higher:** raise `jumpHeight`. This changes which ledges are reachable.
   - **Faster:** raise `runSpeed`. Long jumps get longer too.
   - **Longer dash:** raise `dashDistance`; a quicker one: lower `dashTime`.
   - **More forgiving:** raise `coyoteTime` and `jumpBufferTime` (0.15 each is generous).
   - **Camera:** `camLookahead`, `camDeadzone`, `camSmoothing`. **Shake:** `shakeHurt`, `shakeStomp`, `shakeDie`, `shakeBoss` (0 turns one off; players can also turn shake off in Settings).
3. **Measure again** with `npm run feel` and show the user before/after.
4. **Run `npm run check`.** Movement tests read the config, so they follow your change. Two kinds of failures can appear:
   - **Reach** says a gem, checkpoint or goal is unreachable (e.g. the jump got lower): lower that ledge in the level file or add a spring, then `npm run reach -- level=N`.
   - **A replay** no longer finishes: recorded inputs depend on the exact feel. See where it fails with `npm run replay -- level=N trace=30`, then record a new run with `npm run autoplay -- level=N` (or `npm run autoplay` for all levels). Never delete a replay to pass the checks.
5. **Look at it.** Make a contact sheet of the move you changed, open the PNG and look at it: numbers can't tell you whether it feels right, but the arc can.
   - A jump: `npm run shots -- screen=level level=1 x=8 hold=RJ frames=6 every=6`. Higher jumps: check the hero doesn't leave the top of the screen or hit ceilings that used to be clear.
   - Running: `hold=R frames=6 every=4`. A dash: `hold=RX frames=6 every=2`. A wall jump or double jump: find a wall (`npm run reach -- level=N`) and use `ticks=` + `hold=` to get there, or `replay=<tick>` on a replay that does it.
   - Compare with a sheet taken before the change if the user wants to see the difference, and show them both.

## Verify

- `npm run check` passes, with every replay finishing (re-recorded where needed, and you told the user which).
- The feel numbers before and after are in your message, and the change is noted in `docs/project.md`.
- You looked at a contact sheet of the changed move after the change.

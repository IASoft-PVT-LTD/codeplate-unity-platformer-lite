# Add a hazard or block

Use this when the user wants lava, water, ice, conveyor belts, saws, falling platforms, or any new kind of tile or obstacle.

First decide which kind it is:
- **A tile** (part of the map grid, doesn't move): lava, ice, conveyors, breakable blocks. → steps 1–4.
- **An actor** (moves, has state, or is bigger than a tile): a saw on a path, a falling platform, a flame jet on a timer. → follow `add-enemy.md` instead; `MovingPlatform.cs` (a path) and `Shooter.cs` (a timer) are good models.

1. **Add a flag** in `Assets/Game/Core/TileGrid.cs` (the next free bit: `public const byte Lava = 32;`, then 64, 128) and a name for it in `LevelData.TileFlags` (`["lava"] = TileGrid.Lava`, or `TileGrid.Solid | TileGrid.Ice` for something you stand on).
2. **Give it a letter** in `Assets/Resources/Levels/legend.json`: `"~": {"name": "lava", "tile": "lava"}`.
3. **Make it do something** in the simulation:
   - Hurts or kills on touch → in `World.Step` next to the spikes check, e.g. a helper like `Collide.TouchingSpikes` that tests the new flag, then `HurtPlayer` or a kill.
   - Changes movement (ice: less grip; conveyor: push) → in `Player.Step`, look up the flags under the feet (`grid.Flags(col, M.Round(Body.Bottom))`) and change `rate` or `Body.Vel.X`. Put the numbers in `config.json` + `GameConfig.cs` (e.g. `iceGripMult`, `conveyorSpeed`).
   - Breaks or changes over time → copy the crumble logic in `World` (`_crumbling`, `RestoreCrumbled`, `CrumbleState`).
   - If it matters for reachability (a wall that can be passed, a floor you can't stand on), teach `Reach.Standable` and the replay loop in `Reach.Analyze` about it.
4. **Draw it:** add the art (e.g. `Assets/Resources/Art/blocks/lava.png`, 16×16, frames side by side), list it in `assets.json` and `ArtSlots` (`Art.cs`), and add a branch in `WorldView.Setup` that places its tile (look at how spikes and one-way ledges are placed). Animated tiles: swap the tile's sprite in `WorldView.Sync`.
5. **Test it** in `Assets/Tests/EditMode/WorldTests.cs` with a little room: `Kit.Room("..P..~~..", "#########")` and assert the effect (hurt, speed, push).
6. **Use it** in a level, then `npm run reach -- level=N` and `npm run autoplay -- level=N`.

## Verify

- `npm run check` passes with your new test and every replay.
- A screenshot shows the hazard drawn on the grid: `npm run shots -- screen=level level=N x=<column>`.

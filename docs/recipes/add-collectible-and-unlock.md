# Add a collectible or an unlock

Use this when the user wants a new pickup (a key, a timed power-up, a 1-up), or wants gems or coins to unlock something (a bonus level, a skin, a world).

## A new pickup

1. **Kind:** pickups are `Pickup` actors told apart by `Kind`. Add the kind to `LevelData.ActorTypes` (`["key"] = () => new Pickup()`) and a letter to `legend.json` (`"k"` is taken: pick a free one).
2. **Effect** in `World.Collect` (`Assets/Game/Core/World.cs`): a new `case "key":` that changes world state (e.g. `Keys++`) and emits an event (add `SimEventType.Key`). A timed power-up: a tick counter on `Player` (e.g. `SpeedBoostTicks`) that `Player.Step` reads and counts down; its numbers in `config.json` + `GameConfig.cs`.
3. **What it opens:** e.g. a locked door is a tile or actor that checks `world.Keys` in `Touched`.
4. **Art and feedback:** a strip under `Art/pickups/` (in `assets.json` and `ArtSlots`; no art yet? a stand-in strip of its own, Recipe `reskin-and-rebrand.md` **Stand-in art**), a line in `ActorView.ART`, a sound and toast in `LevelScreen.OnEvent`, and maybe a HUD counter (`UI/Hud.uxml` + `HudView.Update`).
5. **Saved?** If it should persist between plays, add it to `Progress` (Recipe `save-data.md`).
6. **Test** it in `WorldTests.cs`: collected once, the effect happens.

## An unlock

Unlock rules live in `Game.Core.Progress` (saved data) and the screens that show them:
- **Which levels are open:** `Progress.IsUnlocked(n)` (default: the previous level is completed). E.g. a bonus level that needs 12 gems: `if (n == 11) return TotalGems() >= 12;` — add a `TotalGems()` helper that sums `GemCount`.
- **Worlds:** the world map opens a page when its first level is unlocked.
- **Cosmetics or abilities:** check progress where they're used (e.g. pick `player/run_gold` in `PlayerView` when `App.Progress.TotalGems() >= 30`).
- Tell the player: a toast or a line on the results screen (`ResultsPanel`), a lock icon and hint on the map (`WorldMapScreen.ShowInfo`).
- Test the rule in `ContentTests.cs` with a `Progress` you fill by hand.

## Verify

- `npm run check` passes with the new tests.
- Screenshots: the pickup in a level, and the map or results showing the unlock (`npm run shots -- screen=map progress=demo`).

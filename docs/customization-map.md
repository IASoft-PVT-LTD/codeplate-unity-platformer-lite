# Customization map

"I want to change X." Here's where to go. If a recipe exists, follow it.

| I want to… | Change | Recipe |
|---|---|---|
| Use my own hero, enemy or boss sprites | `Assets/Resources/Art/player/` ..., `assets.json`, `ArtSlots` in `Assets/Game/Runtime/Art.cs` | `recipes/reskin-and-rebrand.md` |
| Use my own tiles, blocks or backgrounds | `Art/tiles/`, `Art/blocks/`, `Art/bg/`, `assets.json`; `npm run art -- grid` for a tile sheet | `recipes/reskin-and-rebrand.md` |
| Use 32 px or high-resolution art | `tilePx`, `viewWidth`, `viewHeight` in `Content/config.json`; `ImportSettings.cs` | `recipes/reskin-and-rebrand.md` |
| Rename the game, logo, icon, UI colors, font | `ProjectSettings.asset` (`productName`), `Art/ui/`, `UI/Game.uss` variables, `Art/ui/font.ttf` | `recipes/reskin-and-rebrand.md` |
| Rename what players see: worlds, levels, bosses, moves | `Content/worlds.json` (`name`, `subtitle`), each level's `title:`, the boss's `name=` in its level's `at` line, `UI/MenuScreens.cs` (the controls help) | `recipes/reskin-and-rebrand.md` |
| Tint a world's backgrounds (a world borrowing another's art) | `tint` in `Content/worlds.json` | `recipes/add-world.md` |
| Make jumping floatier, snappier, higher; a longer dash | `Assets/Resources/Content/config.json` | `recipes/tune-the-feel.md` |
| Change the camera, screen shake, squash, hit-stop | `cam*`, `shake*`, `landSquash`, `hitstopStomp` in `config.json`; `Level/CameraRig.cs` | `recipes/tune-the-feel.md` |
| Add or change levels | `Assets/Resources/Levels/level_NN.txt`, then `npm run reach` / `npm run autoplay` | `recipes/add-level.md` |
| Add a map character | `Levels/legend.json` (+ code for new kinds) | `recipes/add-hazard.md`, `recipes/add-enemy.md` |
| Add a world | `Content/worlds.json`, `Art/tiles/terrain_wN.png`, `Art/bg/wN_*`, music, levels | `recipes/add-world.md` |
| Add an enemy | `Assets/Game/Core/Actors/`, `LevelData.ActorTypes`, legend, `ActorView.ART`, `enemies.json` | `recipes/add-enemy.md` |
| Change an enemy's or boss's numbers | `Assets/Resources/Content/enemies.json` | — |
| Add lava, ice, conveyors, saws | `Core/TileGrid.cs` + `World`/`Player`, or a new actor | `recipes/add-hazard.md` |
| Add or change a boss | `Assets/Game/Core/Bosses/`, `enemies.json` | `recipes/add-boss.md` |
| Double jump, wall jump, a new move, unlockable abilities | `config.json` switches; `Core/Player.cs` | `recipes/add-ability.md` |
| A new collectible, or unlocks for gems/coins | `World.Collect`, `Core/Progress.cs` (`IsUnlocked`) | `recipes/add-collectible-and-unlock.md` |
| Hearts, falls, out of hearts, one-hit mode, lives, game over, checkpoints | `"health"` in `config.json` (`fallsCostHeart`, `outOfHeartsRestarts`); `Core/World.cs` | `recipes/change-health-and-lives.md` |
| Time limits, clock pickups, the HUD clock's colours | `time_limit:` in levels, `"clock"` in `config.json`, `o` in maps, `.clock` in `UI/Game.uss` | `recipes/level-clock.md` |
| Touch controls: d-pad, layout, size, when they show | `dpadAxes` in `config.json`, `Core/TouchLayout.cs`, `UI/Touch.uxml`, `UI/Game.uss`, Settings | `recipes/touch-controls-and-mobile-web.md` |
| A privacy policy link in the game | `privacyPolicyUrl` in `config.json` (empty = no button) | — |
| Translate the game, or add languages | `Content/strings.json`, `Content/credits_<code>.txt`, the `WORDS` table in `WebGLTemplates/Game/index.html`; texts in code use `Loc.T` | `recipes/add-language.md` |
| Sounds and music | `Assets/Resources/Audio/`, `LevelScreen.OnEvent`, `Content/worlds.json` | `recipes/music-and-sound.md` |
| Keys and gamepad buttons | `Assets/Resources/Content/controls.inputactions` (JSON), `InputSource.cs` | — |
| What's saved, without breaking old saves | `Core/Progress.cs` (`CurrentVersion`, `Migrate`) | `recipes/save-data.md` |
| Build for web, Windows, macOS | `npm run build:web` / `build:windows` / `build:macos` | `recipes/builds.md` |
| The title screen | `UI/Title.uxml`, `UI/MenuScreens.cs` (`TitleScreen`) | — |
| The world map | `UI/WorldMap.uxml`, `UI/WorldMapScreen.cs` | — |
| The HUD | `UI/Hud.uxml`, `HudView` in `UI/LevelUi.cs` | — |
| Pause and results screens | `UI/Pause.uxml`, `UI/Results.uxml`, `PauseMenu`/`ResultsPanel` in `UI/LevelUi.cs` | — |
| Settings screen | `UI/Settings.uxml`, `SettingsScreen` in `UI/MenuScreens.cs`, `Progress.SettingsData` | `recipes/save-data.md` |
| Credits | `Assets/Resources/Content/credits.txt` (`credits_<code>.txt` per language), `CREDITS.md` | — |
| Which level comes next, what unlocks | `Progress.IsUnlocked`, `GameContent.NextLevel` | `recipes/add-collectible-and-unlock.md` |
| Add a new screen | a UXML in `Assets/Resources/UI/`, a class implementing `IScreen`, `App.Screens.Open(...)` | — |

(UI and runtime paths are under `Assets/Resources/UI/` and `Assets/Game/Runtime/`; simulation paths under `Assets/Game/Core/`.)

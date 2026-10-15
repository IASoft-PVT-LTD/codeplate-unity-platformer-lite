# Reskin and rebrand

Use this when the user brings their own art (hero, enemies, tiles, backgrounds, UI), or renames the game and changes its look.

All art lives in `Assets/Resources/Art/` and is listed in `assets.json` (frame size, frame count, what it's for). Sprite strips are frames side by side, left to right. Characters are **feet-anchored** (feet on the bottom row of each frame, centered) and **face right** (the game flips them). The hitbox is separate from the art (`playerSize` in `config.json`), so bigger art doesn't change the gameplay.

## The hero (or any character)

1. Put the new strips in place of the old (`Art/player/run.png` ...). Each animation: `idle`, `run`, `jump`, `fall`, `dash`, `hurt`, `land`, `wall_slide`.
2. Different frame size or count? Update **both**:
   - `assets.json`: `width`/`height` = ONE frame; `frames` = how many.
   - The `ArtSlots` table in `Assets/Game/Runtime/Art.cs`: `new Slot("player/run", 32, 32, 14)` (frame width, height, frames per second).
3. A missing animation (the user has no `wall_slide`)? Tell the user; meanwhile keep the placeholder, or point the slot at another strip in `PlayerView.Show`. Don't invent art silently. For art the template never had (a new enemy, pickup or world), see **Stand-in art** below.
4. Enemies, bosses, pickups: same steps; their anim → strip table is `ActorView.ART`.
5. Look: `npm run shots -- screen=level level=1 x=10`, and a contact sheet for motion: `... hold=R frames=6 every=4`.

## Tiles and backgrounds

- **Terrain:** `Art/tiles/terrain_w1.png`: 16 frames, index = N·1 + E·2 + S·4 + W·8 (bit set = that neighbour is also solid; frame 0 a lone block, 15 fully surrounded). Users often have a 3×3 or 4×4 tileset sheet: rearrange it into this order with a small Pillow script, and tell them.
- **Blocks:** `Art/blocks/` (one-way ledge left/middle/right, spikes, crumble stages, dash wall, spring, moving platform left/middle/right).
- **Backgrounds:** `Art/bg/w<N>_sky.png`, `_far.png`, `_near.png`, 480×270. Far and near repeat sideways (their left and right edges must match) and scroll slower than the level (`Backdrop.Parallax`).

## A different tile size, or high-resolution art

The game is 480×270 game pixels with 16 px tiles. For 32 px pixel art, set in `Content/config.json`: `"tilePx": 32, "viewWidth": 960, "viewHeight": 540` (the same 30×17 tiles on screen; sprites and the Pixel Perfect Camera read `tilePx`). UI sizes in `Game.uss` are in screen pixels of the reference resolution: double them too (or keep 480×270 for the UI by scaling the panel: `ScreenRouter.Zoom`).
For non-pixel (HD, smooth) art: set `Filter = FilterMode.Bilinear` in `ImportSettings.cs`, and on `Boot`'s Pixel Perfect Camera set `gridSnapping` to `None`. Then reimport: delete `Library/` or run the checks (Unity reimports changed import code).

## Name, logo, colors, font

- **Name:** `productName` (and `companyName`) in `ProjectSettings/ProjectSettings.asset` (edit just those lines, or Edit > Project Settings > Player in the editor). Also the title tagline (`Assets/Resources/UI/Title.uxml`), `Content/credits.txt`, `README.md`.
- **Logo:** `Art/ui/logo.png` (288×72) on the title screen; **icon:** `Art/ui/icon.png` (set it as the Default Icon in Player settings for builds).
- **Colors:** the variables at the top of `Assets/Resources/UI/Game.uss` (`--brass`, `--cream`, `--panel` ...); world accents in `Content/worlds.json`. The web page around the game (`Assets/WebGLTemplates/Game/index.html`: loading bar, "turn your phone sideways" card) repeats `--ink`, `--panel`, `--brass`, `--brass-dark` and `--cream`: change them there too (a project rule fails when the two differ).
- **UI frames:** `Art/ui/panel.png` (9-slice, 16 px borders), `button*.png` (9-slice, 8 px), `node_*.png` (map markers), hearts, gems.
- **Font:** `Art/ui/font.ttf`, used everywhere (`Ui.ApplyFont`). Any TTF/OTF works; a pixel font looks best at sizes that are multiples of its pixel grid (`--font: 8px` in `Game.uss` for an 8 px font).

## Names players see

A new name or theme means renaming more than the game. Go through each and rename what still belongs to the template's story (Sprocket, gears, clockwork), or list what you left for the user:
- **Worlds:** `name` and `subtitle` in `Content/worlds.json` (map page, level banner).
- **Levels:** the `title:` line of each `Assets/Resources/Levels/level_NN.txt` (map, level banner, results).
- **Bosses:** the name above the boss's health bar is `name=` on the boss's `at` line in its level (`at 60,13: boss type=beetle name=Big_Bertha`, `_` for spaces). Without it, the default in its class under `Assets/Game/Core/Bosses/` shows.
- **Moves and controls:** the help text on the Settings screen (`SettingsScreen` in `UI/MenuScreens.cs`), if the user renames a move ("dash" to "pounce").
- Ordinary enemies have no on-screen names. Their ids in code and levels (`walker`, `beetle` ...) can stay; players never see them.

## Stand-in art

The user's pack has no art for something new (an enemy, a pickup, a second world)? Say so, and give the thing its own slot anyway, so the user only has to replace one file later:
1. Copy a strip with the same frame size and count to the new name (`Art/enemies/walker_walk.png` to `Art/enemies/hopper_walk.png`), or draw a simple one with a script.
2. List it in `assets.json` with a `use` that starts with `STAND-IN:` and says what to draw (frame size, frames, poses), and in `ArtSlots`. Unity makes its `.meta` on the next check.
3. Point the new thing at it (`ActorView.ART`), never at another actor's strip, and tell the user which files are stand-ins. A tint in code (`ActorView`) can make it easier to tell apart in the meantime.

A world without its own backgrounds or terrain uses world 1's by itself; a `tint` in `worlds.json` sets it apart (Recipe `add-world.md`).

## Credits and licenses

For every file the user brings: its `assets.json` entry gets their `license` and `credit` (or `CC0-1.0` and a `source` URL), and a line in `Assets/Resources/Content/credits.txt` (shown in-game) and `CREDITS.md`.

## Verify

- `npm run check` passes: `rules` compares every PNG with `assets.json`, and the PlayMode art test compares `Art.cs` with `assets.json`.
- Screenshots of the title, the map and a level look right: `npm run shots -- screen=title`, `screen=map progress=demo`, `screen=level level=1 x=10`. Open them and look. With a new font, also `screen=settings`, `screen=pause level=1` and `screen=results level=1`: no text cut off or spilling out of its panel.
- No names from the template's story are left where players see them (**Names players see**), or you told the user which ones you left.
- The user wants web or phones (`docs/project.md`)? Look at the touch buttons too (`screen=level level=1 x=8 touch=1`), and when the reskin is done, make a web build (Recipe `builds.md`) and open it in a browser, also at a phone's landscape size.

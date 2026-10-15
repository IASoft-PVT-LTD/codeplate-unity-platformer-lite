# Touch controls and mobile web

Use this when the user wants the game playable on phones or tablets (in the browser or as an app), or wants the touch controls moved, resized, restyled or turned off.

The on-screen controls are a **d-pad** (bottom left), **dash** and **jump** (bottom right) and **pause** (top right). They're UI Toolkit elements in `Assets/Resources/UI/Touch.uxml`, drawn by the `.touch-*`/`.dpad-*` rules in `Assets/Resources/UI/Game.uss`, and wired by `TouchControls` (`Assets/Game/Runtime/UI/LevelUi.cs`). Their bits are OR-ed into the same `InputFrame` as the keyboard, so everything (replays, tests) treats them alike.

- **The d-pad is one touch surface.** The finger's position relative to the pad's centre picks the direction (`Game.Core/Dpad.cs`), with a dead zone in the middle (`Dpad.DeadZone`, 18% of the half-width). Sliding the thumb changes direction without lifting; the pad follows that one finger by its pointer id, so another finger can hold jump at the same time, and a finger that slides off the pad keeps steering. Its touch area is a little larger than its art (left, top and right). The arm being held lights up.
- **Axes** (`dpadAxes` in `config.json`): `"x_down"` (default: left, right and down; down + jump drops through a one-way ledge, down in the air falls faster; down needs the thumb clearly low so a resting thumb doesn't turn a jump into a drop), `"x"` (left and right), `"xy"` (all four and diagonals), `"y"`. Arms the game doesn't use are dimmed and never pressed.
- **Placement** is pure maths in `Game.Core/TouchLayout.cs`, so the tests check it on any screen: every control's bottom edge sits at least **15% of the screen height** above the bottom (a thumb resting on the phone's edge doesn't cover it, and the floor stays visible), everything stays inside the **safe area** (camera cutouts, rounded corners, the gesture bar: `Ui.FitSafeArea` insets the HUD and touch layers by `Screen.safeArea`), and nothing reaches up into the HUD's top 44 units. The d-pad is a quarter of the screen width, at most 48% of its height (20:9 phones are wide but short), in three whole-unit cells; jump is 62 units, dash 56. The UI zooms by whole numbers (`TouchLayout.Zoom`), so sizes are in units of the 480×270 game screen (a 20:9 phone is 600×270 of them, a 4:3 tablet 512×384).

## When they show

- Settings > Touch buttons: **Auto** (default: shown on phones and tablets, and on any device after the first touch on the screen), **On**, **Off**. Saved per player.
- `touchControls` in `Content/config.json` overrides "auto" for the whole game: `"always"` (handy while testing on desktop) or `"off"`.
- Settings also has Button size (0.75–1.5×: controls grow up and inward from their corner, never down) and Button opacity.

## Change the layout or look

1. Sizes and margins: the constants at the top of `TouchLayout` (`DpadWidthShare`, `DpadHeightShare`, `DpadTouchMargin`, `JumpSize`, `DashSize`, `SideMargin`, `MinBottom`...). Keep `MinBottom` at 0.15 or more; `TouchTests` checks the rules on 16:9, 20:9, 19.5:9, 4:3 and 16:10 screens with and without cutouts.
2. Art: `Assets/Resources/Art/ui/touch_*.png` (48×48 buttons) and `ui/dpad_*.png` (24×24 cells of the 3×3 pad: `up`, `left`, `right`, `down`, each with a `_lit` version, `hub`, `corner` and `corner_lit` for the diagonals). They're drawn by `tools/art/unity_platformer_placeholders.py`; replace them keeping the sizes, or change the size in the PNG and `assets.json`.
3. A new button (e.g. "attack"): add a `<ui:VisualElement name="touch-attack" class="touch-btn" />` to `Touch.uxml`, a `#touch-attack` rule to `Game.uss`, `Add("touch-attack", InputFrame.Attack)` in the `TouchControls` constructor, a box for it in `TouchLayout.For` (and a `Place(...)` call in `TouchControls.Apply`), and a line in `TouchTests.EveryControlSitsAtLeastFifteenPercentUpOnEveryScreen`.
4. Look at it: `npm run shots -- screen=level level=1 x=8 touch=1`, and on other screens: `aspect=20:9`, `aspect=4:3`, `safe=40,0,0,12` (a cutout on the left and a gesture bar, in UI units).

## Phones in general

- Back (Android) works like Esc: pauses a level, resumes from pause, closes menus; on the title screen it quits the app. Leaving the app mid-level (home button, a call) opens the pause menu.

## Mobile web

- Build with `npm run build:web` (Recipe `builds.md`) and host it; phones play in landscape. The game scales to the screen in whole-pixel steps; on very tall phones the extra space shows more of the level.
- Held upright, a phone gets a small "Turn your phone sideways" card over the game (a CSS orientation media query in `Assets/WebGLTemplates/Game/index.html`). Restyle or reword it there.
- Saves and settings survive a reload because the page passes `autoSyncPersistentDataPath: true` to Unity (a project rule checks it is there).
- Keep the web build small: compressed audio, no unnecessary packages.

## Verify

- `npm run check` passes: `TouchTests` (the d-pad's directions and the layout on every screen shape) and the PlayMode tests `TouchButtonsDriveThePlayer`, `TheDpadIsOneSurfaceThatFollowsItsFinger` and `TouchControlsSitAtLeastFifteenPercentUp`.
- The touch screenshots show the controls clear of the HUD and not covering the player, on 16:9, 20:9 and 4:3.

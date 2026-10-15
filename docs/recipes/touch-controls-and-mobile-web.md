# Touch controls and mobile web

Use this when the user wants the game playable on phones or tablets (in the browser or as an app), or wants the touch buttons moved, resized, restyled or turned off.

The game has five on-screen buttons: left, right, dash, jump (bottom corners) and pause (top right). They're UI Toolkit elements in `Assets/Resources/UI/Touch.uxml`, placed and drawn by the `#touch-*` rules in `Assets/Resources/UI/Game.uss`, and wired by `TouchControls` (`Assets/Game/Runtime/UI/LevelUi.cs`). Each button tracks its own fingers, so running and jumping at once works. Their bits are OR-ed into the same `InputFrame` as the keyboard, so everything (replays, tests) treats them alike.

## When they show

- Settings > Touch buttons: **Auto** (default: shown on phones and tablets, and on any device after the first touch on the screen), **On**, **Off**. Saved per player.
- `touchControls` in `Content/config.json` overrides "auto" for the whole game: `"always"` (handy while testing on desktop) or `"off"`.
- Settings also has Button size (0.75–1.5×) and Button opacity.

## Change the layout or look

1. Move or resize a button in `Game.uss`: e.g. `#touch-jump { right: 8px; bottom: 26px; width: 48px; height: 48px; }`. Units are game pixels of the 480×270 screen, so the layout scales with the screen.
2. Art: `Assets/Resources/Art/ui/touch_*.png` (48×48). Replace them keeping the size, or change the size in both the PNG, `assets.json` and the USS.
3. A new button (e.g. "attack"): add a `<ui:VisualElement name="touch-attack" class="touch-btn" />` to `Touch.uxml`, a `#touch-attack` rule to `Game.uss`, and `Add("touch-attack", InputFrame.Attack)` in the `TouchControls` constructor.
4. Look at it: `npm run shots -- screen=level level=1 x=8 touch=1`.

## Mobile web

- Build with `npm run build:web` (Recipe `builds.md`) and host it; phones play in landscape. The game scales to the screen in whole-pixel steps; on very tall phones the extra space shows more of the level.
- Held upright, a phone gets a small "Turn your phone sideways" card over the game (a CSS orientation media query in `Assets/WebGLTemplates/Game/index.html`). Restyle or reword it there.
- Keep the web build small: compressed audio, no unnecessary packages.

## Verify

- `npm run check` passes (the PlayMode test `TouchButtonsDriveThePlayer` checks every button is on screen, that holding right runs, a second finger jumps, and pause pauses).
- The touch screenshot shows the buttons clear of the HUD and not covering the player.

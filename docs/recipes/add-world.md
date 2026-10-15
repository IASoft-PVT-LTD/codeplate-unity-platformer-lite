# Add a world

Use this when the user wants a second world (or to replace a world's theme): its own terrain, backgrounds, music, levels and map page.

1. **Register it** in `Assets/Resources/Content/worlds.json`: `{"id": 2, "name": "The Clock Spire", "subtitle": "...", "music": "music_world2", "bossMusic": "music_boss", "accent": "#e8d36a", "sky": "#1c1630"}`. `accent` colors the map title.
2. **Art** (names follow the world id; missing ones fall back to world 1's):
   - `Assets/Resources/Art/tiles/terrain_w2.png`: 16 frames of 16×16, side by side. Frame index = N·1 + E·2 + S·4 + W·8, where a bit is set when that neighbour is solid (frame 0 = a lone block, 15 = fully surrounded). Lit edges belong on the top of frames without N.
   - `Art/bg/w2_sky.png` (480×270, opaque), `w2_far.png` and `w2_near.png` (480×270, transparent, the left and right edges must match: they repeat sideways).
   - List each in `assets.json`; add the terrain strip to `ArtSlots` in `Assets/Game/Runtime/Art.cs` (`new Slot("tiles/terrain_w2", 16, 16)`).
3. **Music:** `Assets/Resources/Audio/music_world2.wav` (a seamless loop), listed in `assets.json` (Recipe `music-and-sound.md`).
4. **Levels:** add them after the last level with `world: 2` (Recipe `add-level.md`); end the world with a boss level. Each world's map page shows its levels in order. A **bonus world** (extra levels, a secret world) doesn't need a boss: add `"bonus": true` to its entry in `worlds.json`.
5. **Check:** `npm run check`, then `npm run shots -- screen=map world=2 progress=demo` and a level shot `npm run shots -- screen=level level=<first of world 2> x=10`.

## Verify

- `npm run check` passes: the levels' world exists, every level is reachable and its replay finishes, every world ends with a boss (except bonus worlds).
- Screenshots of the map page and a level show the new look (no world-1 tiles or sky left over).

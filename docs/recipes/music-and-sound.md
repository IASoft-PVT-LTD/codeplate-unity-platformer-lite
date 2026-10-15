# Music and sound

Use this when the user brings their own sounds or music, wants different volumes, or wants a sound for something new.

Every sound is a file in `Assets/Resources/Audio/`, played by name: `App.Audio.Play("coin")` plays `Audio/coin.wav`; `App.Audio.PlayMusic("music_world1")` loops a track with a short crossfade. WAV, OGG and MP3 all work (Unity converts them on import; `Assets/Game/Editor/ImportSettings.cs` sets compression, and treats files starting with `music_` as music).

## Replace a sound or track

1. Save the new file **under the same name** (e.g. `Audio/jump.wav`). If the extension changes (`jump.ogg`), delete the old file **and its `.meta`**, and update its `path` in `assets.json`.
2. Update its `assets.json` entry: `license` and `credit` (the user's own, or CC0 with a `source` link), and credit it in `Assets/Resources/Content/credits.txt` and `CREDITS.md`.
3. Music should loop cleanly (no gap or click at the end). Keep files reasonable: under ~1 MB per track for web builds.

## Which sound plays when

`LevelScreen.OnEvent` (`Assets/Game/Runtime/Level/LevelScreen.cs`) maps simulation events to sounds: jump, air_jump, wall_jump, dash, land, coin (pitch rises with each coin), gem, heart, stomp, bump, hurt, die, checkpoint, spring, crumble, wall_break, shoot, boss_wake, boss_slam, boss_hit, boss_defeated, goal_open, goal. Menus play `click` (`Ui.Button`) and `pause`. Music: `music_title` on the title and map; each world's `music` and `bossMusic` from `Content/worlds.json`.

To add a sound: put the file in `Audio/`, list it in `assets.json`, and call `App.Audio.Play("name")` where it happens (usually a new `case` in `OnEvent`). Lower a loud one with the third argument: `Play("land", 1f, -6f)` is 6 dB quieter.

## Volumes

Players set music and effects volume in Settings (saved). Defaults are `MusicVolume` and `SfxVolume` in `Progress.SettingsData` (`Assets/Game/Core/Progress.cs`).

## Verify

- `npm run check` passes (the art and sound test loads the core sounds; the rules check every file is in `assets.json`).
- Tell the user which sounds changed; they can hear them by playing in the editor or a web build.

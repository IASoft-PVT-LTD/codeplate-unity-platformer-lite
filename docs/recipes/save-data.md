# Change the save data

Use this when something new must be remembered between plays (keys, unlocked skins, achievements, a new setting), or a saved field changes meaning.

Saves are `progress.json` in `Application.persistentDataPath` (on the web: the browser's storage). `Game.Core.Progress` turns it to and from JSON and owns the format; `Game.Runtime.SaveStore` only reads and writes the file. A broken or newer file never crashes the game: it starts fresh and logs a warning.

## Add a field (no version bump needed)

New fields with a sensible default don't need a migration: old saves simply don't have them.
1. Add the field to `Progress` (or `Progress.SettingsData` / `LevelRecord`) with its default.
2. Write it in `ToJson()` and read it in `FromJson()` with a fallback (`s.Bool("newThing", d.NewThing)`), clamping numbers to a valid range.
3. Use it, and save after it changes: `App.Save()`.
4. Extend `ContentTests.ProgressRoundTrips` to cover it.

## Change or rename a field (bump the version)

Renaming, moving or changing the meaning of a saved field needs a migration, or players lose progress.
1. Bump `Progress.CurrentVersion` (e.g. 1 → 2).
2. In `Progress.Migrate`, add a step after the existing one: `if (version < 2) { ... move root["old"] to root["new"] ...; version = 2; }`. Each step upgrades exactly one version; never edit an old step.
3. Update `ToJson()`/`FromJson()` for the new shape.
4. Add a test like `ContentTests.VersionZeroSavesAreMigrated` that loads a **version 1** file (paste a real example as a string) and checks the result.

## Where saves live while testing

Tests and screenshots use scratch files (`SaveStore.PathOverride`), never the player's save. To reset your own: Settings > Reset progress in the game, or delete `progress.json` from `Application.persistentDataPath` (on Windows: `%USERPROFILE%\AppData\LocalLow\<company>\<product>\`).

## Verify

- `npm run check` passes, including the round-trip test and (for a version bump) the migration test from the previous version.
- Tell the user whether old saves carry over.

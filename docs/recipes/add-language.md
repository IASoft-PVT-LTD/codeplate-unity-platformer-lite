# Add a language

Use this when the user wants the game in another language, or in several.

Every text players see goes through `Game.Core.Loc`. The English text is the key: a UXML file says `text="Settings"`, the code says `Loc.T("Checkpoint")` or `Loc.T("Gem {0}/{1}", found, 3)`, and each other language has a table in `Assets/Resources/Content/strings.json` from those English texts to its own. A text a table lacks is shown in English. The template ships English only, so Settings has no Language row yet.

## Add the language

1. In `strings.json`, add the language to `"languages"` with its code and its own name (`"fr": "Français"`), after English. The order is the order the Settings button cycles through.
2. Add its table under `"texts"`: `"fr": { "Settings": "Réglages", "Gem {0}/{1}": "Gemme {0}/{1}" }`. To get the list of texts to translate, run `npm run check -- --filter=EveryTextHasAnEntry`: the failure lists every text the new language has no entry for, with the file it comes from (UXML files, `Loc.T` calls, `worlds.json`, level titles, boss names). Keep each `{0}`, `{1}` and `\n` of the English in the translation; a test compares the values.
3. Credits: add `Assets/Resources/Content/credits_fr.txt` (the credits page reads it in French, `credits.txt` otherwise). Keep the names and licence lines as they are.
4. The web page's own words (the loading bar, "Turn your phone sideways", the start-up error) are in the `WORDS` table at the top of the script in `Assets/WebGLTemplates/Game/index.html`. Add a block with the same code.
5. Run `npm run check`. Settings now shows a Language button; the choice is saved in `progress.json` (`settings.language`).

## Fonts

The pixel font (`Art/ui/font.ttf`) has ASCII and a few symbols only. A project rule reads its character map and fails on every character the game shows that it can't draw, naming the files that use it ("é" in `strings.json`, a level title...). Then either:
- use a font that has the characters (Recipe `reskin-and-rebrand.md`, the font section; record it in `assets.json`; it must be OFL or CC0, with its licence file shipped), or
- for a few characters, draw them into the pixel font (tell the user the font was changed).

Languages with their own scripts (Japanese, Chinese, Korean, Arabic, Hindi...) need a font made for them and more room per line; check every screen with screenshots in that language. Right-to-left languages also need the layout mirrored, which the template doesn't do: tell the user.

## Add a text

- In UXML: write the English in `text="..."`. `Ui.Load` translates it when the screen is built.
- In code: `Loc.T("The door is open!")`, with values as `{0}`: `Loc.T("Best {0}   Par {1}", best, par)`. Don't build sentences out of pieces (`"Gem " + n`): word order differs between languages.
- Content names (a new world, level title or boss name) are passed through `Loc.T` where they're shown; add their translations to each table.

Then add the translation to every shipped language's table; the check lists any you missed.

## Start in the player's language

The game starts in English until the player picks another language in Settings. To start in the device's language instead: give `Language` in `Progress.SettingsData` (`Game.Core/Progress.cs`) the default `""` (no choice yet), and in `App.Init` (`Game.Runtime/App.cs`), when it's empty, map `Application.systemLanguage` to a code you ship (for example `SystemLanguage.French` to `"fr"`) and pass that to `Loc.Use`. `Loc.Use` falls back to English for a code that isn't shipped. Update `TheLanguageIsSavedWithTheSettings` in `LocTests.cs`, which expects English for a fresh save.

## Verify

- `npm run check` passes: `LocTests` (every text has an entry in every shipped language, the values match, the choice is saved) and the font rule.
- Screenshots in the new language: `npm run shots -- screen=title lang=fr`, and the same with `screen=settings`, `screen=map`, `screen=level level=1 x=8`, `screen=pause level=1`, `screen=results level=1`. Look for text that runs out of its box: translations are often longer than the English.

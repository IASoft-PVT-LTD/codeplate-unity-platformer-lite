# Build for the web, Windows and macOS

Use this when the user wants a playable build to share or ship.

Builds need the target's **module** installed for Unity 6000.6.5f1 (Unity Hub > Installs > the editor's gear > Add modules, or `unity install-modules`): **Web Build Support** for the web, **Windows Build Support (Mono)** on Windows (included by default), **Mac Build Support (Mono)** on a Mac. Close the editor on this project first.

```
npm run build:web        # Builds/Web/index.html (+ Build/, TemplateData/)
npm run build:windows    # Builds/Windows/<Product>.exe
npm run build:macos      # Builds/macOS/<Product>.app (build it on a Mac)
```

The first web build takes a long time (about 10 minutes: it compiles IL2CPP and the shaders); later ones are faster. The build fails with a clear message if the module is missing.

## Before you build

- **Name, company, icon and version:** `ProjectSettings/ProjectSettings.asset` has `productName`, `companyName`, `bundleVersion`. Change them in the editor (Edit > Project Settings > Player) or carefully in the file (Recipe `reskin-and-rebrand.md`). The saves folder is named after company and product, so decide before players start.
- **Run `npm run check`** and look at a few screenshots.

## Web

- Serve `Builds/Web/` from any static web server (itch.io, Cloudflare Pages, GitHub Pages: upload the folder's contents). Opening `index.html` from disk doesn't work (browsers block it); test locally with `npx serve Builds/Web` or `python -m http.server -d Builds/Web`.
- The build is compressed with a decompression fallback, so it works on hosts that don't set `Content-Encoding` headers.
- Touch buttons appear on phones and tablets automatically (Recipe `touch-controls-and-mobile-web.md`). The game runs in landscape; on a phone held upright the page shows a small "Turn your phone sideways" card.
- The page around the game is `Assets/WebGLTemplates/Game/index.html` (full-window canvas, loading bar, the turn-sideways card). Its colors repeat the palette from `Game.uss`: change them together. The build picks it up by itself.
- Progress saves in the browser's storage for that site (IndexedDB): the page passes `autoSyncPersistentDataPath: true` to Unity, which copies the save to it as it's written. Keep that line if you change the page; a project rule fails without it.

## Desktop

- Windows: zip the whole `Builds/Windows/` folder (the `.exe` needs its `_Data` folder and `UnityPlayer.dll`).
- macOS: the `.app` isn't signed; for wider distribution the user needs an Apple Developer ID to sign and notarize it.
- Settings has a Fullscreen switch on desktop.

## Verify

- The build command ends with `build Succeeded` and the path.
- Open it (serve the web build locally) and play the first level for a few seconds; tell the user where the files are.
- Web: change a setting (say the music volume), reload the page, and check it stuck.

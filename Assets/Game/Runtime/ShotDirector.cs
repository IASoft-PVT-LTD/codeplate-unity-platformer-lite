using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Runtime
{
    /// <summary>
    /// Screenshots for agents: `npm run shots -- screen=level level=3 x=40` starts the game with
    /// `-codeplateShot screen=level level=3 x=40`, and this drives it to that state, renders the
    /// game and the UI into a PNG under shots/, and finishes. Arguments:
    ///   screen=title|map|settings|credits|level|pause|results   (default level)
    ///   level=N        level number (level, pause, results)
    ///   world=N        world map page (map)
    ///   x=COL y=ROW    put the player on the ground at map column COL (first ground at or below ROW)
    ///   ticks=N hold=R run N ticks holding letters (L R U D J X) first
    ///   replay=N       play the level's replay for N ticks first (bosses mid-fight, etc.)
    ///   frames=6 every=10   a contact sheet: 6 shots 10 ticks apart in one PNG (motion, animation)
    ///   touch=1        show the touch buttons;  progress=demo  a save with some levels done
    ///   out=NAME       file name (default from the arguments); scale=2 (pixels per game pixel)
    /// Your save is never touched: shots use a scratch save file.
    /// </summary>
    public static class ShotDirector
    {
        public static bool Finished { get; private set; }
        public static int ExitCode { get; private set; }
        public static Dictionary<string, string> Args { get; private set; }

        public static bool Active => Args != null;

        /// <summary>Reads `-codeplateShot key=value ...` from the command line. Returns true if a shot was requested.</summary>
        public static bool TryStart(MonoBehaviour host)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-codeplateShot");
            if (i < 0) return false;
            Args = new Dictionary<string, string>();
            for (int k = i + 1; k < args.Length && !args[k].StartsWith("-"); k++)
            {
                foreach (var part in args[k].Split(' '))
                {
                    int eq = part.IndexOf('=');
                    if (eq > 0) Args[part.Substring(0, eq)] = part.Substring(eq + 1);
                }
            }
            host.StartCoroutine(Run());
            return true;
        }

        static string Arg(string key, string fallback = "") => Args.TryGetValue(key, out var v) ? v : fallback;
        static int IntArg(string key, int fallback) => int.TryParse(Arg(key), out int v) ? v : fallback;

        static IEnumerator Run()
        {
            Log($"shot: {string.Join(" ", Args)}");
            Exception error = null;
            IEnumerator steps = null;
            try
            {
                steps = Steps();
            }
            catch (Exception e)
            {
                error = e;
            }
            while (error == null)
            {
                object current;
                try
                {
                    if (!steps.MoveNext()) break;
                    current = steps.Current;
                }
                catch (Exception e)
                {
                    error = e;
                    break;
                }
                yield return current;
            }
            if (error != null)
            {
                Debug.LogError($"[CodePlate] shot failed: {error}");
                ExitCode = 1;
            }
            Finished = true;
            if (!Application.isEditor) Application.Quit(ExitCode);
        }

        static IEnumerator Steps()
        {
            var scratch = Path.Combine(Application.temporaryCachePath, "codeplate-shot-progress.json");
            SaveStore.PathOverride = scratch;
            if (File.Exists(scratch)) File.Delete(scratch);
            var progress = App.Progress;
            progress.ResetProgress();
            progress.Settings = new Progress.SettingsData();
            if (Arg("progress") == "demo")
            {
                // The first levels done (up to 6), with some gems.
                int count = App.Content.LevelCount;
                for (int n = 1; n <= Math.Min(6, count); n++)
                    progress.Record(n, 20 + n * 7.3, new[] { true, n % 2 == 0, n % 3 == 0 }, 10 + n, n % 3);
            }
            if (Arg("touch") == "1") progress.Settings.TouchControls = "on";
            int scale = IntArg("scale", 2);
            ScreenRouter.ZoomOverride = scale;

            string screen = Arg("screen", "level");
            int level = IntArg("level", 1);
            switch (screen)
            {
                case "title":
                    App.Screens.ShowTitle();
                    break;
                case "map":
                    App.Screens.ShowMap(IntArg("world", 0));
                    break;
                case "settings":
                    App.Screens.ShowTitle();
                    App.Screens.OpenSettings();
                    break;
                case "credits":
                    App.Screens.ShowTitle();
                    App.Screens.OpenCredits();
                    break;
                default:
                    App.PlayLevel(level);
                    break;
            }
            yield return null;
            var ls = App.Level;
            if (ls != null)
            {
                ls.Paused = true; // the shot controls time from here
                if (Args.ContainsKey("replay"))
                {
                    var file = Path.Combine(Directory.GetCurrentDirectory(), ProjectPaths.Replay(level));
                    var inputs = Replay.Parse(File.ReadAllText(file), new List<string>());
                    int upTo = Math.Min(IntArg("replay", inputs.Count), inputs.Count);
                    if (screen == "results") upTo = inputs.Count;
                    ls.InputOverride = t => t < inputs.Count ? inputs[t] : 0;
                    ls.StepTicks(upTo);
                    ls.InputOverride = null;
                }
                else if (screen == "results")
                {
                    var file = Path.Combine(Directory.GetCurrentDirectory(), ProjectPaths.Replay(level));
                    var inputs = Replay.Parse(File.ReadAllText(file), new List<string>());
                    ls.InputOverride = t => t < inputs.Count ? inputs[t] : 0;
                    ls.StepTicks(inputs.Count + 120);
                    ls.InputOverride = null;
                }
                if (Args.ContainsKey("x")) PlacePlayer(ls, IntArg("x", 0), IntArg("y", 0));
                int hold = InputFrame.FromText(Arg("hold", "."));
                ls.StepTicks(IntArg("ticks", 0), Math.Max(0, hold));
                // The HUD's banner and toasts time out in real seconds; after a fast-forward, clear them
                // as they would have cleared in play (the intro banner shows for about 2 s).
                if (ls.World.Tick > 140) ls.Hud.ClearMessages();
                if (screen == "pause") ls.OpenPause();
                if (screen == "results") ls.ShowResults();
            }

            var rt = new RenderTexture(App.Config.viewWidth * scale, App.Config.viewHeight * scale, 24);
            var uiRt = new RenderTexture(rt.width, rt.height, 0);
            App.Screens.Panel.targetTexture = uiRt;
            App.Screens.Panel.clearColor = true;
            App.Screens.Panel.colorClearValue = new Color(0, 0, 0, 0);

            int frames = Math.Max(1, IntArg("frames", 1));
            int every = Math.Max(1, IntArg("every", 10));
            var shots = new List<Texture2D>();
            for (int f = 0; f < frames; f++)
            {
                if (f > 0 && ls != null)
                {
                    int held = InputFrame.FromText(Arg("hold", "."));
                    ls.StepTicks(every, Math.Max(0, held));
                }
                // Let the UI lay out and the views settle (animations run on unscaled frames).
                for (int k = 0; k < 6; k++)
                {
                    ls?.SnapCamera();
                    yield return null;
                }
                shots.Add(Capture(rt, uiRt));
            }
            var output = shots.Count == 1 ? shots[0] : Sheet(shots);
            string name = Arg("out", $"{screen}{(ls != null ? "_" + level : "")}{(Args.ContainsKey("x") ? "_x" + Arg("x") : "")}{(Args.ContainsKey("replay") ? "_r" + Arg("replay") : "")}{(frames > 1 ? "_sheet" : "")}");
            Directory.CreateDirectory(ProjectPaths.Shots);
            var path = Path.GetFullPath(Path.Combine(ProjectPaths.Shots, name + ".png"));
            File.WriteAllBytes(path, output.EncodeToPNG());
            Log($"shot saved: {path}");
            App.Screens.Panel.targetTexture = null;
        }

        /// <summary>Puts the player on the ground at column x (the first ground at or below row y).</summary>
        static void PlacePlayer(LevelScreen ls, int x, int y)
        {
            var g = ls.World.Grid;
            x = Mathf.Clamp(x, 0, g.Width - 1);
            for (int row = Mathf.Max(0, y); row < g.Height - 1; row++)
            {
                if (!g.IsSolid(x, row) && g.IsFloor(x, row + 1))
                {
                    ls.World.Player.Body.Pos = new V2d(x + 0.5, row + 1);
                    ls.World.Player.Body.Vel = V2d.Zero;
                    break;
                }
            }
            ls.CameraRig.Snap(LevelScreen.Feet(ls.World.Player.Body.Pos), 1);
            ls.StepTicks(1);
        }

        static Texture2D Capture(RenderTexture rt, RenderTexture uiRt)
        {
            var cam = Boot.Instance.Camera;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var game = Read(rt, TextureFormat.RGB24);
            var ui = Read(uiRt, TextureFormat.RGBA32);
            var a = game.GetPixels32();
            var b = ui.GetPixels32();
            for (int i = 0; i < a.Length; i++)
            {
                // The panel renders premultiplied: out = ui + game * (1 - alpha).
                int al = b[i].a;
                a[i] = new Color32(
                    (byte)Mathf.Min(255, b[i].r + a[i].r * (255 - al) / 255),
                    (byte)Mathf.Min(255, b[i].g + a[i].g * (255 - al) / 255),
                    (byte)Mathf.Min(255, b[i].b + a[i].b * (255 - al) / 255), 255);
            }
            game.SetPixels32(a);
            game.Apply();
            return game;
        }

        static Texture2D Read(RenderTexture rt, TextureFormat format)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, format, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return t;
        }

        /// <summary>Several shots in a grid, 3 per row, left to right then top to bottom.</summary>
        static Texture2D Sheet(List<Texture2D> shots)
        {
            int w = shots[0].width, h = shots[0].height, cols = Math.Min(3, shots.Count), rows = (shots.Count + cols - 1) / cols;
            var sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count; i++)
            {
                int cx = i % cols, cy = rows - 1 - i / cols;
                sheet.SetPixels(cx * w, cy * h, w, h, shots[i].GetPixels());
            }
            sheet.Apply();
            return sheet;
        }

        static void Log(string s) => Debug.Log($"[CodePlate] {s}");
    }
}

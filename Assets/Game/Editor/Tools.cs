using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace CodePlate
{
    /// <summary>
    /// Command-line tools on the pure simulation, run by `npm run reach|replay|autoplay|feel`
    /// (tools/unity.mjs starts the editor in batch mode with -executeMethod CodePlate.Tools.X
    /// -codeplateArgs "level=3 ..."). Output lines start with [CodePlate] so the script can show them.
    /// </summary>
    public static class Tools
    {
        static Dictionary<string, string> Args()
        {
            var d = new Dictionary<string, string>();
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-codeplateArgs");
            for (int k = i + 1; i >= 0 && k < args.Length && !args[k].StartsWith("-"); k++)
                foreach (var part in args[k].Split(' '))
                {
                    int eq = part.IndexOf('=');
                    if (eq > 0) d[part.Substring(0, eq)] = part.Substring(eq + 1);
                }
            return d;
        }

        static GameContent Content()
        {
            var c = GameContent.Load(path =>
            {
                foreach (var ext in new[] { ".txt", ".json" })
                {
                    var f = Path.Combine(ProjectPaths.Resources, path + ext);
                    if (File.Exists(f)) return File.ReadAllText(f);
                }
                return null;
            });
            foreach (var e in c.Errors) Out("ERROR " + e);
            return c;
        }

        static void Out(string text)
        {
            foreach (var line in text.TrimEnd('\n').Split('\n')) Debug.Log("[CodePlate] " + line);
        }

        static List<int> Levels(GameContent c, Dictionary<string, string> args)
        {
            var list = new List<int>();
            if (args.TryGetValue("level", out var s) && int.TryParse(s, out int n)) list.Add(n);
            else for (int i = 1; i <= c.LevelCount; i++) list.Add(i);
            return list;
        }

        static void Exit(bool ok) => EditorApplication.Exit(ok ? 0 : 1);

        /// <summary>Reach map and report: `npm run reach -- level=3` (all levels without level=).</summary>
        public static void Reach()
        {
            var args = Args();
            var c = Content();
            bool ok = c.Errors.Count == 0;
            foreach (int n in Levels(c, args))
            {
                var l = c.Level(n);
                if (l == null) { ok = false; continue; }
                var r = Game.Core.Reach.Analyze(l, c.Config, c.Enemies);
                Out($"level {n} \"{l.Title}\":");
                Out(Game.Core.Reach.Report(l, r));
                ok &= r.AllReached;
            }
            Out("Legend: + standing spots the player can reach, : cells a jump or fall passes through, ! a target nobody can reach.");
            Exit(ok);
        }

        /// <summary>
        /// Plays a replay headless: `npm run replay -- level=3 trace=30` prints the state every 30
        /// ticks. Each replay also lists the level's enemies: met (within near=4 tiles, or touched)
        /// and defeated, so you can see whether the run actually meets a new enemy.
        /// </summary>
        public static void Replay()
        {
            var args = Args();
            var c = Content();
            bool ok = true;
            int trace = args.TryGetValue("trace", out var t) && int.TryParse(t, out int tv) ? tv : 0;
            double near = args.TryGetValue("near", out var nr) && double.TryParse(nr, NumberStyles.Float, CultureInfo.InvariantCulture, out double nv) ? nv : Game.Core.Replay.DefaultNear;
            foreach (int n in Levels(c, args))
            {
                var path = ProjectPaths.Replay(n);
                if (!File.Exists(path))
                {
                    Out($"level {n}: no replay ({path}); make one with npm run autoplay -- level={n}");
                    ok = false;
                    continue;
                }
                var errors = new List<string>();
                var inputs = Game.Core.Replay.Parse(File.ReadAllText(path), errors, path);
                foreach (var e in errors) Out("ERROR " + e);
                var w = new World(c.Level(n), c.Config, c.Enemies);
                for (int i = 0; i < inputs.Count + 60 && !w.Finished; i++)
                {
                    var ev = w.Step(i < inputs.Count ? inputs[i] : 0);
                    foreach (var e in ev)
                        if (e.Type == Game.Core.SimEventType.Die || e.Type == Game.Core.SimEventType.Hurt || e.Type == Game.Core.SimEventType.BossHit || e.Type == Game.Core.SimEventType.Goal)
                            Out($"  tick {w.Tick}: {e.Type} at {e.Pos}");
                    if (trace > 0 && w.Tick % trace == 0)
                    {
                        var p = w.Player;
                        Out(string.Format(CultureInfo.InvariantCulture, "  tick {0,5}  input {1,-4} pos {2,7:0.00},{3,6:0.00}  vel {4,6:0.0},{5,6:0.0}  {6}  hp {7}",
                            w.Tick, InputFrame.ToText(i < inputs.Count ? inputs[i] : 0), p.Body.Pos.X, p.Body.Pos.Y, p.Body.Vel.X, p.Body.Vel.Y, p.Current, p.Health));
                    }
                }
                var o = Game.Core.Replay.Play(c.Level(n), c.Config, c.Enemies, inputs, near: near);
                Out($"level {n}: {o}");
                if (!o.Finished || !o.BossesDefeated)
                {
                    string note = Game.Core.Replay.MovesNote(File.ReadAllText(path), c.Config, path);
                    if (note.Length > 0) Out("  " + note);
                }
                Out("  " + o.EnemyReport(near).TrimEnd('\n').Replace("\n", "\n  "));
                ok &= o.Finished && o.BossesDefeated;
            }
            Exit(ok);
        }

        /// <summary>The bot plays and writes replays/level_NN.replay on a win: `npm run autoplay -- level=3`.</summary>
        public static void Autoplay()
        {
            var args = Args();
            var c = Content();
            bool ok = true;
            bool verbose = args.ContainsKey("verbose");
            foreach (int n in Levels(c, args))
            {
                var l = c.Level(n);
                Autopilot win = null, last = null;
                // Hearts are limited (out of hearts fails the run), so retries change the timing
                // rather than dying until it happens to work: a start delay, then a timing seed.
                foreach (int seed in new[] { 0, 1, 2, 3 })
                foreach (int delay in new[] { 0, 17, 41, 73, 101, 139 })
                {
                    if (win != null) break;
                    var a = new Autopilot(l, c.Config, c.Enemies) { StartDelay = delay, TimingSeed = seed, Verbose = verbose };
                    bool won = a.Run();
                    last = a;
                    if (verbose) foreach (var s in a.Log) Out("  " + s);
                    if (won)
                    {
                        win = a;
                        break;
                    }
                    Out($"level {n}: attempt with start delay {delay}, timing seed {seed} failed at tick {a.World.Tick} ({a.World.Deaths} deaths{(a.World.OutOfHearts ? ", out of hearts" : "")}, player at {a.World.Player.Body.Pos})");
                }
                if (win == null)
                {
                    ok = false;
                    Out($"level {n}: the bot couldn't finish it. Last moves:");
                    foreach (var s in last.Log.GetRange(Math.Max(0, last.Log.Count - 12), Math.Min(12, last.Log.Count))) Out("  " + s);
                    Out("Check the level with npm run reach, make the hard part easier, or record a run by hand (docs/recipes/add-level.md).");
                    continue;
                }
                var o = Game.Core.Replay.Play(l, c.Config, c.Enemies, win.Inputs);
                Directory.CreateDirectory(ProjectPaths.Replays);
                var path = ProjectPaths.Replay(n);
                File.WriteAllText(path, Game.Core.Replay.ToText(win.Inputs, $"level {n} {l.Title}: autopilot run, {o.Ticks / 60.0:0.0} s, {o.Deaths} deaths", Game.Core.Replay.Moves(c.Config)));
                Out($"level {n}: {o}; wrote {path}");
            }
            Exit(ok);
        }

        /// <summary>The derived movement numbers for the current config.json: `npm run feel`.</summary>
        public static void Feel()
        {
            var c = Content();
            Out(FeelReport(c.Config));
            Exit(c.Errors.Count == 0);
        }

        public static string FeelReport(GameConfig cfg)
        {
            var d = cfg.Derived();
            var sb = new StringBuilder();
            sb.AppendLine("Derived from config.json (tiles, seconds):");
            sb.AppendLine($"  gravity            {d.Gravity:0.00} t/s^2 (x{cfg.fallGravityMult} when falling)");
            sb.AppendLine($"  jump speed         {d.JumpVelocity:0.00} t/s  -> rises {cfg.Peak(d.JumpVelocity, d.Gravity, true):0.00} tiles held");
            sb.AppendLine($"  short hop          {d.MinJumpVelocity:0.00} t/s  -> about {cfg.jumpMinHeight:0.00} tiles");
            // Air time and jump distance at full run, measured with the real Player.
            var grid = new TileGrid(200, 60);
            for (int x = 0; x < 200; x++) grid.Set(x, 50, TileGrid.Solid);
            var p = new Player(cfg, new V2d(10.5, 50));
            p.Body.Vel.X = cfg.runSpeed;
            p.ForceGrounded(true);
            var ev = new List<SimEvent>();
            int ticks = 0;
            double x0 = p.Body.Pos.X;
            for (; ticks < 400; ticks++)
            {
                ev.Clear();
                p.Step(InputFrame.Right | (ticks < 40 ? InputFrame.Jump : 0), grid, null, ev);
                if (ticks > 2 && p.IsGrounded) break;
            }
            sb.AppendLine($"  full running jump  {p.Body.Pos.X - x0:0.00} tiles across, {ticks / 60.0:0.00} s in the air");
            sb.AppendLine($"  dash               {cfg.dashDistance:0.00} tiles in {GameConfig.Ticks(cfg.dashTime)} ticks ({d.DashSpeed:0.0} t/s), {cfg.airDashes} in the air");
            sb.AppendLine($"  spring             {d.SpringVelocity:0.00} t/s -> {cfg.springHeight:0.00} tiles");
            sb.AppendLine($"  run                {cfg.runSpeed:0.00} t/s, top speed in {GameConfig.Ticks(cfg.groundAccelTime)} ticks");
            if (cfg.doubleJumpEnabled) sb.AppendLine($"  air jump           {d.AirJumpVelocity:0.00} t/s -> {cfg.airJumpHeight:0.00} tiles, x{cfg.airJumps}");
            return sb.ToString();
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Shared helpers for the EditMode tests. These tests use only Game.Core and NUnit (no
    /// UnityEngine), so they also run under plain .NET (CI's "unity" job). Content is read from
    /// the files on disk, exactly as the game reads it through Resources.
    /// </summary>
    public static class Kit
    {
        static GameContent _content;

        /// <summary>The Unity project folder (the one holding Assets/).</summary>
        public static string Root
        {
            get
            {
                foreach (var a in typeof(Kit).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                    if (a.Key == "CodePlateProject" && !string.IsNullOrEmpty(a.Value)) return a.Value;
                return Directory.GetCurrentDirectory(); // Unity runs tests from the project folder
            }
        }

        /// <summary>A resource's text by its Resources path (no extension), or null.</summary>
        public static string Read(string resourcePath)
        {
            foreach (var ext in new[] { ".txt", ".json" })
            {
                var f = Path.Combine(Root, ProjectPaths.Resources, resourcePath + ext);
                if (File.Exists(f)) return File.ReadAllText(f);
            }
            return null;
        }

        /// <summary>All game content, loaded once. Tests that change config use Config() for a fresh copy.</summary>
        public static GameContent Content => _content ??= GameContent.Load(Read);

        public static GameConfig Config() => Content.Config.Clone();
        public static EnemyConfig Enemies => Content.Enemies;

        /// <summary>
        /// The shipped config.json numbers with every movement switch set explicitly, so a test
        /// doesn't silently depend on what the game ships with: dash on, double jump and wall jump
        /// off unless asked for, one air jump and one air dash. Use it for small rooms; use Config()
        /// where the shipped game itself is under test (levels, replays).
        /// </summary>
        public static GameConfig Moves(bool dash = true, bool doubleJump = false, bool wallJump = false)
        {
            var cfg = Config();
            cfg.dashEnabled = dash;
            cfg.airDashes = 1;
            cfg.doubleJumpEnabled = doubleJump;
            cfg.airJumps = 1;
            cfg.wallJumpEnabled = wallJump;
            return cfg;
        }

        /// <summary>A small room from map rows (the real legend; 'P' is the start), with Moves().</summary>
        public static World Room(params string[] rows) => Room(Moves(), rows);

        public static World Room(GameConfig cfg, params string[] rows) => new World(LevelData.FromRows(Content.Legend, rows), cfg, Enemies);

        /// <summary>Steps the world `ticks` times with one input; returns every event.</summary>
        public static List<SimEvent> Run(this World w, int input, int ticks)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < ticks; i++) all.AddRange(w.Step(input));
            return all;
        }

        /// <summary>Steps until the player stands (at most 120 ticks).</summary>
        public static void Settle(this World w)
        {
            for (int i = 0; i < 120 && !w.Player.IsGrounded; i++) w.Step(0);
            Assert.IsTrue(w.Player.IsGrounded, "the player should be standing");
        }

        public static bool Has(this List<SimEvent> events, SimEventType t) => events.Exists(e => e.Type == t);
        public static int Count(this List<SimEvent> events, SimEventType t) => events.FindAll(e => e.Type == t).Count;

        public static T Actor<T>(this World w) where T : Actor => (T)w.Actors.Find(a => a is T);

        public static string ReplayPath(int n) => Path.Combine(Root, ProjectPaths.Replay(n));
    }
}

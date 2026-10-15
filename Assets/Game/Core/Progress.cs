using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Saved progress and settings, as JSON with a version number. Game.Runtime's SaveStore reads
    /// and writes the file (progress.json in Application.persistentDataPath); this class only
    /// turns it to and from text, so tests can check every format change.
    ///
    /// To change the save format: bump <see cref="CurrentVersion"/>, add a step to
    /// <see cref="Migrate"/> that upgrades the previous version, and add a test that loads an old
    /// file (docs/recipes/save-data.md). Never just rename a field: old saves would lose it.
    /// </summary>
    public sealed class Progress
    {
        public const int CurrentVersion = 1;

        public sealed class LevelRecord
        {
            public bool Completed;
            /// <summary>Best time in seconds, 0 = none yet.</summary>
            public double BestTime;
            public bool[] Gems = new bool[LevelData.GemsPerLevel];
            public int BestCoins;
        }

        public sealed class SettingsData
        {
            public double MusicVolume = 0.7;
            public double SfxVolume = 0.9;
            public bool ScreenShake = true;
            /// <summary>"auto" (only on touch screens), "on" or "off".</summary>
            public string TouchControls = "auto";
            /// <summary>Touch button size, 0.75 .. 1.5.</summary>
            public double TouchScale = 1.0;
            /// <summary>Touch button opacity, 0.2 .. 1.</summary>
            public double TouchOpacity = 0.6;
            public bool Fullscreen;
            /// <summary>The language code (Loc.Languages); one the game doesn't ship falls back to English.</summary>
            public string Language = Loc.English;
        }

        public readonly Dictionary<int, LevelRecord> Levels = new Dictionary<int, LevelRecord>();
        public SettingsData Settings = new SettingsData();
        public int TotalDeaths;
        public int TotalCoins;

        public LevelRecord Level(int n)
        {
            if (!Levels.TryGetValue(n, out var r)) Levels[n] = r = new LevelRecord();
            return r;
        }

        /// <summary>Level 1 is always open; every other level opens when the one before it is completed.</summary>
        public bool IsUnlocked(int n) => n <= 1 || (Levels.TryGetValue(n - 1, out var r) && r.Completed);

        public int GemCount(int n) => Levels.TryGetValue(n, out var r) ? Array.FindAll(r.Gems, g => g).Length : 0;

        /// <summary>Records a finished level. Returns true if it's a new best time.</summary>
        public bool Record(int n, double seconds, bool[] gems, int coins, int deaths)
        {
            var r = Level(n);
            bool best = r.BestTime <= 0 || seconds < r.BestTime;
            if (best) r.BestTime = Math.Round(seconds, 2);
            r.Completed = true;
            for (int i = 0; i < r.Gems.Length && i < gems.Length; i++) r.Gems[i] |= gems[i];
            r.BestCoins = Math.Max(r.BestCoins, coins);
            TotalDeaths += deaths;
            TotalCoins += coins;
            return best;
        }

        /// <summary>Clears levels and stats, keeps settings.</summary>
        public void ResetProgress()
        {
            Levels.Clear();
            TotalDeaths = 0;
            TotalCoins = 0;
        }

        // ------------------------------------------------------------------ text

        public string ToJson()
        {
            var levels = new JsonObject();
            var keys = new List<int>(Levels.Keys);
            keys.Sort();
            foreach (int n in keys)
            {
                var r = Levels[n];
                var o = new JsonObject
                {
                    ["completed"] = r.Completed,
                    ["bestTime"] = r.BestTime,
                    ["gems"] = new List<object> { r.Gems[0], r.Gems[1], r.Gems[2] },
                    ["bestCoins"] = (double)r.BestCoins,
                };
                levels[n.ToString()] = o;
            }
            var s = Settings;
            var root = new JsonObject
            {
                ["version"] = (double)CurrentVersion,
                ["levels"] = levels,
                ["stats"] = new JsonObject { ["deaths"] = (double)TotalDeaths, ["coins"] = (double)TotalCoins },
                ["settings"] = new JsonObject
                {
                    ["musicVolume"] = s.MusicVolume,
                    ["sfxVolume"] = s.SfxVolume,
                    ["screenShake"] = s.ScreenShake,
                    ["touchControls"] = s.TouchControls,
                    ["touchScale"] = s.TouchScale,
                    ["touchOpacity"] = s.TouchOpacity,
                    ["fullscreen"] = s.Fullscreen,
                    ["language"] = s.Language,
                },
            };
            return Json.Write(root);
        }

        /// <summary>
        /// Reads a save. A missing, empty or broken file gives fresh progress (with a note in
        /// <paramref name="problem"/>), never an exception: a bad save must not stop the game.
        /// </summary>
        public static Progress FromJson(string text, out string problem)
        {
            problem = null;
            var p = new Progress();
            if (string.IsNullOrWhiteSpace(text)) return p;
            var root = Json.TryParse(text, "progress.json", out string err) as JsonObject;
            if (err != null || root == null)
            {
                problem = err ?? "progress.json: not an object";
                return p;
            }
            int version = root.Int("version", 0);
            if (version > CurrentVersion)
            {
                problem = $"progress.json is version {version}, newer than this game ({CurrentVersion}); starting fresh";
                return p;
            }
            root = Migrate(root, version);

            var levels = root.Obj("levels");
            if (levels != null)
            {
                foreach (var key in levels.Keys)
                {
                    if (!int.TryParse(key, out int n) || !(levels[key] is JsonObject o)) continue;
                    var r = p.Level(n);
                    r.Completed = o.Bool("completed");
                    r.BestTime = o.Num("bestTime");
                    r.BestCoins = o.Int("bestCoins");
                    var gems = o.Arr("gems");
                    for (int i = 0; gems != null && i < gems.Count && i < r.Gems.Length; i++) r.Gems[i] = gems[i] is bool b && b;
                }
            }
            var stats = root.Obj("stats");
            if (stats != null)
            {
                p.TotalDeaths = stats.Int("deaths");
                p.TotalCoins = stats.Int("coins");
            }
            var s = root.Obj("settings");
            if (s != null)
            {
                var d = p.Settings;
                d.MusicVolume = M.Clamp(s.Num("musicVolume", d.MusicVolume), 0, 1);
                d.SfxVolume = M.Clamp(s.Num("sfxVolume", d.SfxVolume), 0, 1);
                d.ScreenShake = s.Bool("screenShake", d.ScreenShake);
                string tc = s.Str("touchControls", d.TouchControls);
                d.TouchControls = tc == "on" || tc == "off" ? tc : "auto";
                d.TouchScale = M.Clamp(s.Num("touchScale", d.TouchScale), 0.75, 1.5);
                d.TouchOpacity = M.Clamp(s.Num("touchOpacity", d.TouchOpacity), 0.2, 1);
                d.Fullscreen = s.Bool("fullscreen", d.Fullscreen);
                d.Language = s.Str("language", d.Language);
            }
            return p;
        }

        /// <summary>
        /// Upgrades an older save, one version at a time, to the current shape.
        /// Version 0 (the first release) stored: {"unlocked": n, "best_times": {"1": 12.3},
        /// "gems": {"1": [true, false, false]}, "music": 0.8, "sfx": 1.0}.
        /// </summary>
        public static JsonObject Migrate(JsonObject root, int version)
        {
            if (version < 1)
            {
                var levels = new JsonObject();
                int unlocked = root.Int("unlocked", 1);
                var best = root.Obj("best_times");
                var gems = root.Obj("gems");
                for (int n = 1; n < Math.Max(unlocked, 1); n++)
                    levels[n.ToString()] = new JsonObject { ["completed"] = true };
                foreach (var src in new[] { best, gems })
                {
                    if (src == null) continue;
                    foreach (var key in src.Keys)
                        if (!levels.ContainsKey(key)) levels[key] = new JsonObject { ["completed"] = true };
                }
                if (best != null)
                    foreach (var key in best.Keys) ((JsonObject)levels[key])["bestTime"] = best.Num(key);
                if (gems != null)
                    foreach (var key in gems.Keys) ((JsonObject)levels[key])["gems"] = gems.Arr(key);
                root = new JsonObject
                {
                    ["version"] = 1.0,
                    ["levels"] = levels,
                    ["settings"] = new JsonObject
                    {
                        ["musicVolume"] = root.Num("music", 0.7),
                        ["sfxVolume"] = root.Num("sfx", 0.9),
                    },
                };
                version = 1;
            }
            // if (version < 2) { ... upgrade 1 -> 2 here ...; version = 2; }
            return root;
        }
    }
}

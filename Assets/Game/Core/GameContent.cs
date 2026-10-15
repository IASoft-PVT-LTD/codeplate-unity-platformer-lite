using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>One world: a page of the world map, an art set and a music track (Content/worlds.json).</summary>
    public sealed class WorldInfo
    {
        public int Id;
        public string Name = "";
        public string Subtitle = "";
        /// <summary>Music track name (an Audio/ file without extension), and the one for its boss.</summary>
        public string Music = "";
        public string BossMusic = "";
        /// <summary>The color of this world's name on its map page, as #rrggbb.</summary>
        public string Accent = "#ffffff";
        /// <summary>The world's sky color, as #rrggbb. Nothing in the template draws it (the sky is Art/bg/w&lt;N&gt;_sky.png); it's there for your own uses.</summary>
        public string Sky = "#000000";
        /// <summary>
        /// A color multiplied over this world's backgrounds, on its map page and in its levels, as #rrggbb.
        /// White (the default) leaves them as drawn; a world that borrows another world's art can use it to look different.
        /// </summary>
        public string Tint = "#ffffff";
        /// <summary>A bonus world ("bonus": true): its last level doesn't need a boss.</summary>
        public bool Bonus;
    }

    /// <summary>
    /// Everything the game reads from Assets/Resources: config.json, enemies.json, worlds.json,
    /// the legend and every level. Loaded through a reader function, so the game (Resources),
    /// the tests and the tools all load exactly the same data. Problems go to <see cref="Errors"/>.
    /// </summary>
    public sealed class GameContent
    {
        public GameConfig Config;
        public EnemyConfig Enemies;
        public Legend Legend;
        public readonly List<WorldInfo> Worlds = new List<WorldInfo>();
        /// <summary>Levels that loaded, in play order (a level with errors is left out; see Errors).</summary>
        public readonly List<LevelData> Levels = new List<LevelData>();
        /// <summary>How many level files exist (level_01 .. level_NN), loaded or not.</summary>
        public int LevelCount;
        public readonly List<string> Errors = new List<string>();

        /// <summary>Resource paths, without extensions (Unity's Resources.Load wants none).</summary>
        public const string ConfigPath = "Content/config";
        public const string EnemiesPath = "Content/enemies";
        public const string WorldsPath = "Content/worlds";
        public const string LegendPath = "Levels/legend";

        public static string LevelPath(int n) => $"Levels/level_{n:00}";

        /// <summary>
        /// Loads everything. <paramref name="read"/> returns a resource's text, or null if it doesn't
        /// exist. Levels are read as level_01, level_02 ... until one is missing.
        /// </summary>
        public static GameContent Load(Func<string, string> read)
        {
            var c = new GameContent();
            string Need(string path)
            {
                var t = read(path);
                if (t == null) c.Errors.Add($"Assets/Resources/{path} is missing");
                return t;
            }
            c.Config = GameConfig.FromJson(Need(ConfigPath) ?? "{}", c.Errors);
            c.Enemies = EnemyConfig.FromJson(Need(EnemiesPath) ?? "{}", c.Errors);
            c.Legend = Legend.FromJson(Need(LegendPath) ?? "{}", c.Errors);
            ReadWorlds(c, Need(WorldsPath) ?? "{\"worlds\": []}");
            for (int n = 1; n < 100; n++)
            {
                var text = read(LevelPath(n));
                if (text == null) break;
                c.LevelCount = n;
                var level = LevelData.Parse(text, c.Legend, c.Errors, $"level_{n:00}.txt");
                if (level == null) continue;
                level.Number = n;
                c.Levels.Add(level);
            }
            if (c.Levels.Count == 0 && c.Errors.Count == 0) c.Errors.Add("no levels: expected Assets/Resources/Levels/level_01.txt");
            foreach (var l in c.Levels)
                if (c.World(l.World) == null) c.Errors.Add($"level_{l.Number:00}.txt: world {l.World} isn't in worlds.json");
            return c;
        }

        static void ReadWorlds(GameContent c, string text)
        {
            var root = Json.TryParse(text, "worlds.json", out string err) as JsonObject;
            if (err != null || root == null)
            {
                c.Errors.Add(err ?? "worlds.json: expected an object");
                return;
            }
            var list = root.Arr("worlds");
            if (list == null)
            {
                c.Errors.Add("worlds.json: expected {\"worlds\": [ ... ]}");
                return;
            }
            foreach (var item in list)
            {
                if (!(item is JsonObject o)) continue;
                c.Worlds.Add(new WorldInfo
                {
                    Id = o.Int("id"),
                    Name = o.Str("name"),
                    Subtitle = o.Str("subtitle"),
                    Music = o.Str("music"),
                    BossMusic = o.Str("bossMusic"),
                    Accent = o.Str("accent", "#ffffff"),
                    Sky = o.Str("sky", "#000000"),
                    Tint = o.Str("tint", "#ffffff"),
                    Bonus = o.Bool("bonus"),
                });
            }
        }

        public WorldInfo World(int id) => Worlds.Find(w => w.Id == id);

        public LevelData Level(int number) => Levels.Find(l => l.Number == number);

        /// <summary>The levels of one world, in order.</summary>
        public List<LevelData> LevelsOf(int world) => Levels.FindAll(l => l.World == world);

        /// <summary>The next level's number, or 0 after the last one.</summary>
        public int NextLevel(int number) => number < LevelCount ? number + 1 : 0;

        /// <summary>A fresh simulation of a level.</summary>
        public World NewWorld(int number) => new World(Level(number), Config, Enemies);
    }
}

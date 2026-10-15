using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Game.Core
{
    /// <summary>
    /// One level, from Assets/Resources/Levels/level_NN.txt: a short header, a `---` line, then an
    /// ASCII map. Map characters are defined in Levels/legend.json. Errors name the file, line and
    /// column. Format: docs/levels.md.
    /// </summary>
    public sealed class LevelData
    {
        public const int MinRows = 17, MaxRows = 64, MinCols = 30, MaxCols = 400;
        public const int GemsPerLevel = 3;

        /// <summary>
        /// Legend actor names to classes. Add new actor kinds here AND in legend.json.
        /// "boss" is special: its `at` line's type= picks the class (beetle).
        /// </summary>
        public static readonly Dictionary<string, Func<Actor>> ActorTypes = new Dictionary<string, Func<Actor>>
        {
            ["walker"] = () => new Walker(),
            ["spiker"] = () => new Spiker(),
            ["flyer"] = () => new Flyer(),
            ["shooter"] = () => new Shooter(),
            ["platform"] = () => new MovingPlatform(),
            ["spring"] = () => new Spring(),
            ["coin"] = () => new Pickup(),
            ["gem"] = () => new Pickup(),
            ["heart"] = () => new Pickup(),
            ["checkpoint"] = () => new Checkpoint(),
            ["goal"] = () => new Checkpoint(),
            ["beetle"] = () => new Beetle(),
        };

        /// <summary>Legend tile names to TileGrid flags.</summary>
        public static readonly Dictionary<string, int> TileFlags = new Dictionary<string, int>
        {
            ["solid"] = TileGrid.Solid,
            ["one_way"] = TileGrid.OneWay,
            ["spikes"] = TileGrid.Spikes,
            ["crumble"] = TileGrid.Solid | TileGrid.Crumble,
            ["dash_wall"] = TileGrid.Solid | TileGrid.DashWall,
        };

        /// <summary>File number: level_03.txt has number 3.</summary>
        public int Number;
        public string Title = "";
        /// <summary>Which world (art set, music and map page) the level belongs to: 1, 2, ...</summary>
        public int World = 1;
        /// <summary>Target time in seconds, shown on the results screen.</summary>
        public int ParTime = 60;
        public List<string> Rows = new List<string>();
        public int Width, Height;
        /// <summary>Player start: feet position in tiles.</summary>
        public V2d Start;
        /// <summary>"x,y" → params, from `at x,y: kind key=value ...` header lines.</summary>
        public Dictionary<string, Dictionary<string, string>> Params = new Dictionary<string, Dictionary<string, string>>();
        public Legend Legend;

        /// <summary>True if the level has a boss ('B').</summary>
        public bool HasBoss;

        static readonly Regex AtLine = new Regex(@"^at\s+(\d+)\s*,\s*(\d+)\s*:\s*(\w+)(.*)$");

        /// <summary>Parses level text. <paramref name="where"/> prefixes error messages (the file name). Returns null on errors.</summary>
        public static LevelData Parse(string text, Legend legend, List<string> errors, string where = "level")
        {
            int startErrors = errors.Count;
            var l = new LevelData { Legend = legend };
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            int sep = Array.FindIndex(lines, s => s.Trim() == "---");
            if (sep < 0)
            {
                errors.Add($"{where}: no '---' line between the header and the map");
                return null;
            }

            var atLines = new List<(int line, string text)>();
            for (int i = 0; i < sep; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("at "))
                {
                    atLines.Add((i + 1, line));
                    continue;
                }
                int colon = line.IndexOf(':');
                if (colon < 0)
                {
                    errors.Add($"{where}:{i + 1}: expected 'key: value' or 'at x,y: kind key=value ...'");
                    continue;
                }
                string key = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                switch (key)
                {
                    case "title": l.Title = value; break;
                    case "world":
                        if (!int.TryParse(value, out l.World) || l.World < 1) errors.Add($"{where}:{i + 1}: world must be 1 or more");
                        break;
                    case "par_time":
                        if (!int.TryParse(value, out l.ParTime) || l.ParTime < 1) errors.Add($"{where}:{i + 1}: par_time must be a whole number of seconds");
                        break;
                    default:
                        errors.Add($"{where}:{i + 1}: unknown header key '{key}' (title, world, par_time, at)");
                        break;
                }
            }

            for (int i = sep + 1; i < lines.Length; i++) l.Rows.Add(lines[i].TrimEnd(' ', '\t'));
            while (l.Rows.Count > 0 && l.Rows[l.Rows.Count - 1].Trim().Length == 0) l.Rows.RemoveAt(l.Rows.Count - 1);
            l.Height = l.Rows.Count;
            l.Width = 0;
            foreach (var r in l.Rows) l.Width = Math.Max(l.Width, r.Length);
            if (l.Height < MinRows || l.Height > MaxRows) errors.Add($"{where}: the map has {l.Height} rows; it needs {MinRows}..{MaxRows}");
            if (l.Width < MinCols || l.Width > MaxCols) errors.Add($"{where}: the map is {l.Width} columns wide; it needs {MinCols}..{MaxCols}");
            // Pad short rows with empty space so every row has the same width.
            for (int y = 0; y < l.Rows.Count; y++) l.Rows[y] = l.Rows[y].PadRight(l.Width, '.');

            int starts = 0, goals = 0, gems = 0;
            var bosses = new List<Cell>();
            for (int y = 0; y < l.Height; y++)
            for (int x = 0; x < l.Width; x++)
            {
                char ch = l.Rows[y][x];
                if (!legend.Entries.TryGetValue(ch, out var e))
                {
                    errors.Add($"{where}:{sep + 2 + y}:{x + 1}: unknown map character '{ch}' (see Levels/legend.json)");
                    continue;
                }
                if (e.Special == "start")
                {
                    starts++;
                    l.Start = new V2d(x + 0.5, y + 1);
                }
                if (e.Actor == "goal") goals++;
                if (e.Actor == "gem") gems++;
                if (e.Actor == "boss") bosses.Add(new Cell(x, y));
            }
            if (starts != 1) errors.Add($"{where}: the map needs exactly one player start 'P' (found {starts})");
            if (goals != 1) errors.Add($"{where}: the map needs exactly one goal 'G' (found {goals})");
            if (gems != GemsPerLevel) errors.Add($"{where}: the map needs exactly {GemsPerLevel} gems '*' (found {gems})");

            foreach (var (lineNo, lineText) in atLines)
            {
                var m = AtLine.Match(lineText);
                if (!m.Success)
                {
                    errors.Add($"{where}:{lineNo}: expected 'at x,y: kind key=value ...'");
                    continue;
                }
                int x = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int y = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                string kind = m.Groups[3].Value;
                char ch = y < l.Height && x < l.Width ? l.Rows[y][x] : '\0';
                legend.Entries.TryGetValue(ch, out var e);
                if (e == null || e.Actor != kind)
                {
                    errors.Add($"{where}:{lineNo}: no '{kind}' at column {x}, row {y} of the map (x,y count from 0, top-left)");
                    continue;
                }
                var p = new Dictionary<string, string>();
                foreach (var pair in m.Groups[4].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = pair.IndexOf('=');
                    if (eq <= 0) errors.Add($"{where}:{lineNo}: parameter '{pair}' should be key=value");
                    else p[pair.Substring(0, eq)] = pair.Substring(eq + 1);
                }
                l.Params[$"{x},{y}"] = p;
            }
            foreach (var b in bosses)
            {
                l.HasBoss = true;
                if (!l.Params.TryGetValue($"{b.X},{b.Y}", out var p) || !p.TryGetValue("type", out var type) || !ActorTypes.ContainsKey(type) || !(ActorTypes[type]() is Boss))
                    errors.Add($"{where}: the boss 'B' at {b.X},{b.Y} needs a line like 'at {b.X},{b.Y}: boss type=beetle' (types: beetle)");
            }
            return errors.Count == startErrors ? l : null;
        }

        /// <summary>A small level from map rows with no size or gem rules, for tests. 'P' sets the start (default: top-left).</summary>
        public static LevelData FromRows(Legend legend, params string[] rows)
        {
            var l = new LevelData { Legend = legend, Width = rows[0].Length, Height = rows.Length, Start = new V2d(0.5, 1) };
            l.Rows.AddRange(rows);
            for (int y = 0; y < rows.Length; y++)
            {
                int x = rows[y].IndexOf('P');
                if (x >= 0) l.Start = new V2d(x + 0.5, y + 1);
                if (rows[y].IndexOf('B') >= 0) l.HasBoss = true;
            }
            return l;
        }

        /// <summary>Adds `at` parameters (for tests): l.At(4, 2, "type=beetle health=1").</summary>
        public LevelData At(int x, int y, string parameters)
        {
            var p = new Dictionary<string, string>();
            foreach (var pair in parameters.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0) p[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }
            Params[$"{x},{y}"] = p;
            return this;
        }

        public char CharAt(int x, int y) => y >= 0 && y < Height && x >= 0 && x < Width ? Rows[y][x] : '.';

        /// <summary>The tile grid of this level (a fresh copy every call).</summary>
        public TileGrid BuildGrid()
        {
            var g = new TileGrid(Width, Height);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (Legend.Entries.TryGetValue(Rows[y][x], out var e) && e.Tile != null && TileFlags.TryGetValue(e.Tile, out int f))
                    g.Set(x, y, f);
            return g;
        }

        /// <summary>Fresh actors for this level, in map order (top to bottom, left to right). Gems are numbered 0..2 left to right.</summary>
        public List<Actor> BuildActors()
        {
            var list = new List<Actor>();
            var gems = new List<Pickup>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (!Legend.Entries.TryGetValue(Rows[y][x], out var e) || e.Actor == null) continue;
                Params.TryGetValue($"{x},{y}", out var p);
                string kind = e.Actor;
                if (kind == "boss" && p != null && p.TryGetValue("type", out var type)) kind = type;
                if (!ActorTypes.TryGetValue(kind, out var make)) continue;
                var a = make();
                a.Kind = kind;
                a.Origin = new V2d(x + 0.5, y + 1);
                a.Pos = a.Origin;
                if (p != null) a.Params = new Dictionary<string, string>(p);
                list.Add(a);
                if (kind == "gem") gems.Add((Pickup)a);
            }
            gems.Sort((a, b) => a.Origin.X != b.Origin.X ? a.Origin.X.CompareTo(b.Origin.X) : a.Origin.Y.CompareTo(b.Origin.Y));
            for (int i = 0; i < gems.Count; i++) gems[i].GemIndex = i;
            return list;
        }
    }

    /// <summary>What each map character means, from Levels/legend.json.</summary>
    public sealed class Legend
    {
        public sealed class Entry
        {
            public string Name;
            /// <summary>A TileGrid flag name (LevelData.TileFlags), or null.</summary>
            public string Tile;
            /// <summary>An actor kind (LevelData.ActorTypes, or "boss"), or null.</summary>
            public string Actor;
            /// <summary>"start" for the player start.</summary>
            public string Special;
        }

        public readonly Dictionary<char, Entry> Entries = new Dictionary<char, Entry>();

        public static Legend FromJson(string text, List<string> errors, string where = "legend.json")
        {
            var legend = new Legend();
            var root = Json.TryParse(text, where, out string err) as JsonObject;
            if (err != null || root == null)
            {
                errors.Add(err ?? $"{where}: expected an object");
                return legend;
            }
            foreach (var key in root.Keys)
            {
                if (key.StartsWith("_")) continue;
                if (key.Length != 1)
                {
                    errors.Add($"{where}: key \"{key}\" must be a single map character");
                    continue;
                }
                var o = root.Obj(key);
                if (o == null)
                {
                    errors.Add($"{where}: \"{key}\" must be an object like {{\"name\": \"...\", \"tile\": \"solid\"}}");
                    continue;
                }
                var e = new Entry { Name = o.Str("name", key), Tile = o.Get("tile") as string, Actor = o.Get("actor") as string, Special = o.Get("special") as string };
                if (e.Tile != null && !LevelData.TileFlags.ContainsKey(e.Tile))
                    errors.Add($"{where}: \"{key}\" has unknown tile \"{e.Tile}\" (known: {string.Join(", ", LevelData.TileFlags.Keys)})");
                if (e.Actor != null && e.Actor != "boss" && !LevelData.ActorTypes.ContainsKey(e.Actor))
                    errors.Add($"{where}: \"{key}\" has unknown actor \"{e.Actor}\" (add it to LevelData.ActorTypes)");
                legend.Entries[key[0]] = e;
            }
            return legend;
        }
    }
}

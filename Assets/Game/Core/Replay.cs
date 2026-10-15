using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// Recorded inputs as text: one line per run of identical input, "&lt;ticks&gt; &lt;letters&gt;",
    /// letters from InputFrame.Letters (L R U D J X) or "." for nothing. Lines starting with #
    /// are comments. Example:
    /// <code>
    /// # level 1, 34.2 s
    /// 30 .
    /// 12 R
    /// 9 RJ
    /// </code>
    /// Replays live in replays/level_NN.replay (next to Assets/). You can edit them by hand
    /// ("hold jump 3 ticks longer at line 14"). The "# moves:" line names the moves that were on
    /// when the run was recorded (<see cref="Moves"/>); when a replay stops finishing after a move
    /// was switched on, <see cref="MovesNote"/> says so.
    /// </summary>
    public static class Replay
    {
        public static List<int> Parse(string text, List<string> errors, string where = "replay")
        {
            var output = new List<int>();
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string l = lines[n].Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                var parts = l.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                int bits = parts.Length == 2 ? InputFrame.FromText(parts[1]) : -1;
                if (parts.Length != 2 || !int.TryParse(parts[0], out int count) || count < 0 || bits < 0)
                {
                    errors.Add($"{where}:{n + 1}: expected '<ticks> <letters>' (letters {InputFrame.Letters} or .), got '{l}'");
                    continue;
                }
                for (int i = 0; i < count; i++) output.Add(bits);
            }
            return output;
        }

        public static string ToText(IReadOnlyList<int> inputs, string comment = "", string moves = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(comment)) sb.Append("# ").Append(comment).Append('\n');
            if (moves != null) sb.Append(MovesHeader).Append(' ').Append(moves).Append('\n');
            int i = 0;
            while (i < inputs.Count)
            {
                int j = i;
                while (j < inputs.Count && inputs[j] == inputs[i]) j++;
                sb.Append(j - i).Append(' ').Append(InputFrame.ToText(inputs[i])).Append('\n');
                i = j;
            }
            return sb.ToString();
        }

        public const string MovesHeader = "# moves:";

        /// <summary>
        /// The optional moves switched on in <paramref name="c"/>, as the "# moves:" line writes
        /// them: "dash double_jump wall_jump", or "none". A new move with an ...Enabled switch
        /// belongs here too (docs/recipes/add-ability.md).
        /// </summary>
        public static string Moves(GameConfig c)
        {
            var on = new List<string>();
            if (c.dashEnabled) on.Add("dash");
            if (c.doubleJumpEnabled) on.Add("double_jump");
            if (c.wallJumpEnabled) on.Add("wall_jump");
            return on.Count == 0 ? "none" : string.Join(" ", on);
        }

        /// <summary>The moves a replay's "# moves:" line names (empty for "none"), or null when it has no such line.</summary>
        public static List<string> RecordedMoves(string text)
        {
            foreach (var raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string l = raw.Trim();
                if (!l.StartsWith(MovesHeader)) continue;
                var moves = new List<string>(l.Substring(MovesHeader.Length).Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries));
                moves.Remove("none");
                return moves;
            }
            return null;
        }

        /// <summary>
        /// Why a replay that no longer finishes may have stopped working: the moves on now differ
        /// from the ones it was recorded with (a recorded run can hold jump or press dash where a
        /// new move reacts to it). Empty when they match.
        /// </summary>
        public static string MovesNote(string text, GameConfig c, string name = "this replay")
        {
            var recorded = RecordedMoves(text);
            var now = new List<string>(Moves(c).Split(' '));
            now.Remove("none");
            if (recorded == null)
                return $"{name} has no \"{MovesHeader}\" line, so it may have been recorded with other moves switched on (now: {Moves(c)}).";
            var added = now.FindAll(m => !recorded.Contains(m));
            var removed = recorded.FindAll(m => !now.Contains(m));
            if (added.Count == 0 && removed.Count == 0) return "";
            var parts = new List<string>();
            if (added.Count > 0) parts.Add($"was recorded before {string.Join(", ", added)} was switched on");
            if (removed.Count > 0) parts.Add($"was recorded with {string.Join(", ", removed)} on, which is now off");
            return $"{name} {string.Join(" and ", parts)} (its header says \"{MovesHeader} {(recorded.Count == 0 ? "none" : string.Join(" ", recorded))}\"): record it again.";
        }

        /// <summary>The result of playing a replay: did it finish, and how.</summary>
        public sealed class Outcome
        {
            public bool Finished;
            public int Ticks;
            public int Deaths;
            public int Coins;
            public int Gems;
            public bool BossesDefeated;
            /// <summary>The run lost its last heart (a failed level: a replay must never do that).</summary>
            public bool OutOfHearts;
            /// <summary>Times the level clock ran out during the run.</summary>
            public int TimeUps;
            /// <summary>Hearts left at the end.</summary>
            public int Hearts;
            /// <summary>The tick each checkpoint was reached on, in order.</summary>
            public List<int> CheckpointTicks = new List<int>();
            public World World;
            /// <summary>Every enemy in the level (left to right) and whether the run met and defeated it.</summary>
            public List<Encounter> Enemies = new List<Encounter>();

            public int Met => Enemies.FindAll(e => e.Met).Count;
            public int Defeated => Enemies.FindAll(e => e.Defeated).Count;

            /// <summary>"enemies: 2 of 5 met, 1 defeated", then one line per enemy.</summary>
            public string EnemyReport(double near = DefaultNear)
            {
                var sb = new StringBuilder();
                sb.Append($"enemies: {Met} of {Enemies.Count} met (within {near:0.#} tiles or touched), {Defeated} defeated\n");
                foreach (var e in Enemies) sb.Append("  ").Append(e).Append('\n');
                return sb.ToString();
            }

            public override string ToString() =>
                $"{(Finished ? "finished" : "did NOT finish")} at tick {Ticks} ({Ticks / 60.0:0.0} s), deaths {Deaths}, hearts left {Hearts}, coins {Coins}, gems {Gems}/3" +
                (OutOfHearts ? ", OUT OF HEARTS" : "") + (TimeUps > 0 ? $", time ran out {TimeUps}x" : "") +
                (World != null && World.Bosses.Count > 0 ? $", boss {(BossesDefeated ? "defeated" : "NOT defeated")}" : "") +
                (World != null && !Finished ? $"; player at {World.Player.Body.Pos}, {World.Player.Current}" : "");
        }

        /// <summary>How close (tiles, in x and in y) an enemy must come for a run to have met it.</summary>
        public const double DefaultNear = 4;

        /// <summary>
        /// Plays inputs through a fresh World, plus up to <paramref name="extraTicks"/> idle ticks
        /// after them. Also notes which enemies the run met (came within <paramref name="near"/>
        /// tiles of, or touched) and defeated.
        /// </summary>
        public static Outcome Play(LevelData level, GameConfig config, EnemyConfig enemies, IReadOnlyList<int> inputs, int extraTicks = 60, double near = DefaultNear)
        {
            var w = new World(level, config, enemies);
            var log = new EnemyLog(w, near);
            int timeUps = 0;
            var checkpoints = new List<int>();
            for (int i = 0; i < inputs.Count + extraTicks && !w.Finished && !w.OutOfHearts; i++)
            {
                foreach (var e in w.Step(i < inputs.Count ? inputs[i] : 0))
                {
                    if (e.Type == SimEventType.TimeUp) timeUps++;
                    if (e.Type == SimEventType.Checkpoint) checkpoints.Add(w.Tick);
                }
                log.Observe();
            }
            int gems = 0;
            foreach (bool g in w.Gems) if (g) gems++;
            bool bosses = true;
            foreach (var b in w.Bosses) bosses &= b.Defeated;
            return new Outcome { Finished = w.Finished, Ticks = w.Tick, Deaths = w.Deaths, Coins = w.Coins, Gems = gems, BossesDefeated = bosses, World = w, Enemies = log.Encounters,
                OutOfHearts = w.OutOfHearts, TimeUps = timeUps, Hearts = w.Player.Health, CheckpointTicks = checkpoints };
        }

        /// <summary>One enemy of the level, and what a run did with it.</summary>
        public sealed class Encounter
        {
            public Actor Actor;
            public string Kind;
            /// <summary>Its map cell (column, row).</summary>
            public Cell Cell;
            /// <summary>The first tick it was within reach or touched; -1 = never.</summary>
            public int MetAt = -1;
            public bool Touched;
            /// <summary>The tick it was defeated; -1 = never.</summary>
            public int DefeatedAt = -1;

            public bool Met => MetAt >= 0;
            public bool Defeated => DefeatedAt >= 0;

            public override string ToString() =>
                $"{Kind,-8} at {Cell.X},{Cell.Y}: " +
                (Defeated ? $"defeated at tick {DefeatedAt}" : Touched ? $"touched at tick {MetAt}, not defeated" : Met ? $"met at tick {MetAt}, not defeated" : "NOT met");
        }

        /// <summary>
        /// Watches a World tick by tick and notes, for every enemy placed in the level, whether the
        /// player came near it, touched it and defeated it. An enemy is any placed (not spawned)
        /// actor that is dangerous to touch at some point, so new enemy kinds count too.
        /// </summary>
        public sealed class EnemyLog
        {
            readonly World _w;
            readonly double _near;
            readonly List<Actor> _placed;
            readonly Dictionary<Actor, Encounter> _seen = new Dictionary<Actor, Encounter>();

            public EnemyLog(World w, double near = DefaultNear)
            {
                _w = w;
                _near = near;
                _placed = w.Actors.FindAll(a => !a.Transient);
                Observe();
            }

            /// <summary>Call after every World.Step.</summary>
            public void Observe()
            {
                var p = _w.Player;
                bool alive = p.Current != Player.State.Dead;
                foreach (var a in _placed)
                {
                    if (!_seen.TryGetValue(a, out var e))
                    {
                        if (!a.IsDangerous || !a.Active) continue;
                        e = new Encounter { Actor = a, Kind = a.Kind, Cell = new Cell(M.Floor(a.Origin.X), M.Floor(a.Origin.Y) - 1) };
                        _seen[a] = e;
                    }
                    if (e.Defeated) continue;
                    if (alive && a.Active)
                    {
                        bool touching = p.Body.Rect.Intersects(a.Rect);
                        e.Touched |= touching;
                        if (e.MetAt < 0 && (touching || (System.Math.Abs(a.Pos.X - p.Body.Pos.X) <= _near && System.Math.Abs(a.Pos.Y - p.Body.Pos.Y) <= _near)))
                            e.MetAt = _w.Tick;
                    }
                    if (a.IsDefeated)
                    {
                        e.DefeatedAt = _w.Tick;
                        if (e.MetAt < 0) e.MetAt = _w.Tick;
                    }
                }
            }

            /// <summary>The enemies so far, left to right through the level (then top to bottom).</summary>
            public List<Encounter> Encounters
            {
                get
                {
                    var list = new List<Encounter>();
                    foreach (var a in _placed)
                        if (_seen.TryGetValue(a, out var e)) list.Add(e);
                    // A stable sort: equal cells keep the actor order.
                    var sorted = new List<Encounter>();
                    foreach (var e in list)
                    {
                        int i = sorted.Count;
                        while (i > 0 && (sorted[i - 1].Cell.X > e.Cell.X || (sorted[i - 1].Cell.X == e.Cell.X && sorted[i - 1].Cell.Y > e.Cell.Y))) i--;
                        sorted.Insert(i, e);
                    }
                    return sorted;
                }
            }
        }
    }
}

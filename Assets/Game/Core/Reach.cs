using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// The level linter: can the player get from the start to every gem, checkpoint and the goal
    /// with the CURRENT movement settings? Pure logic.
    ///
    /// How: jump, fall and dash paths are recorded by running the real Player in empty space
    /// (running and standing starts, held and tapped jumps, walking off a ledge, dashes, double
    /// jumps when enabled). Then, from every reachable standing spot, those paths are replayed
    /// through the real Collide code on the level's tiles. Wherever a path lands is reachable;
    /// every cell a path passes through counts as touched. Moving platforms count as ledges along
    /// their whole path; springs launch at springHeight; dashes pass dash walls.
    ///
    /// Wall jumps (when wallJumpEnabled) need the level's walls, so they can't be recorded in
    /// empty space: from every standing spot the real Player is stepped on the level's tiles,
    /// jumping at a wall and wall-jumping off it again and again, either back up the same wall
    /// ("wall") or across to the opposite one ("wall_switch", a chimney). Paths that never touch a
    /// wall are dropped (the plain jumps cover them). Enemies and bosses are ignored, so the
    /// answer is "reachable without them".
    /// </summary>
    public static class Reach
    {
        /// <summary>How a recorded path is played: the inputs that make it.</summary>
        public sealed class PathInfo
        {
            public int Dir;
            /// <summary>Starting horizontal speed: 0 (standing) or a running start.</summary>
            public double Vx;
            /// <summary>"full", "tap", "walk", "steer_late", "double", "dash", "jump_dash", "spring", "wall", "wall_switch".</summary>
            public string Style;
            public bool FromSpring;
            /// <summary>
            /// Played live with the real Player on the level's tiles from each standing spot (wall
            /// jumps), instead of replayed from a recording made in empty space. Its entry in
            /// Result.Paths is empty.
            /// </summary>
            public bool Live;
            public override string ToString() => $"{Style} dir {Dir}{(Vx != 0 ? " running" : "")}{(FromSpring ? " from a spring" : "")}";
        }

        public sealed class Result
        {
            /// <summary>Standing cells reached.</summary>
            public readonly HashSet<Cell> Standable = new HashSet<Cell>();
            /// <summary>Every cell the player's body passed through.</summary>
            public readonly HashSet<Cell> Touched = new HashSet<Cell>();
            /// <summary>Targets that couldn't be reached: (kind, cell).</summary>
            public readonly List<(string Kind, Cell Cell)> Missing = new List<(string, Cell)>();
            public readonly List<(string Kind, Cell Cell)> Targets = new List<(string, Cell)>();
            /// <summary>How each standing cell was first reached: (from cell, path id; -1 = walked).</summary>
            public readonly Dictionary<Cell, (Cell From, int Path)> CameFrom = new Dictionary<Cell, (Cell, int)>();
            public readonly List<PathInfo> PathInfo = new List<PathInfo>();
            /// <summary>Per path: feet offsets per tick, and whether each tick was dashing.</summary>
            public readonly List<V2d[]> Paths = new List<V2d[]>();
            public readonly List<bool[]> Dashing = new List<bool[]>();
            /// <summary>Targets: cell → the standing cell whose move touched it.</summary>
            public readonly Dictionary<Cell, Cell> TouchedFrom = new Dictionary<Cell, Cell>();

            public bool AllReached => Missing.Count == 0;
        }

        /// <summary>The most wall jumps one live path chains, and its longest flight in ticks.</summary>
        public const int MaxWallJumps = 8;
        public const int LiveTicks = 320;

        static readonly Dictionary<string, (List<V2d[]>, List<bool[]>, List<PathInfo>)> PathCache = new Dictionary<string, (List<V2d[]>, List<bool[]>, List<PathInfo>)>();

        /// <summary>
        /// Analyses a level. <paramref name="from"/> starts the search somewhere other than 'P';
        /// <paramref name="grid"/> uses a live grid (broken walls stay open) instead of a fresh one.
        /// </summary>
        public static Result Analyze(LevelData level, GameConfig config, EnemyConfig enemies = null, Cell? from = null, TileGrid grid = null)
        {
            var r = new Result();
            grid = grid != null ? grid.Clone() : level.BuildGrid();
            var springs = new HashSet<Cell>();
            var targetCells = new HashSet<Cell>();
            foreach (var a in level.BuildActors())
            {
                var cell = new Cell(M.Floor(a.Origin.X), M.Floor(a.Origin.Y) - 1);
                switch (a.Kind)
                {
                    case "gem":
                    case "checkpoint":
                    case "goal":
                        r.Targets.Add((a.Kind, cell));
                        targetCells.Add(cell);
                        break;
                    case "spring":
                        springs.Add(cell);
                        break;
                    case "platform":
                    {
                        // Treat the platform's whole path as one-way ledges.
                        var mp = (MovingPlatform)a;
                        mp.Setup(null);
                        var a0 = mp.PathStart;
                        var a1 = mp.PathEnd;
                        int steps = M.Ceil(a0.DistanceTo(a1) * 2) + 1;
                        for (int i = 0; i <= steps; i++)
                        {
                            var p = a0.Lerp(a1, (double)i / Math.Max(steps, 1));
                            int row = M.Floor(p.Y - a.Size.Y + 0.01);
                            for (int x = M.Floor(p.X - a.Size.X * 0.5 + 0.01); x <= M.Floor(p.X + a.Size.X * 0.5 - 0.01); x++)
                                if (grid.InBounds(x, row) && !grid.IsSolid(x, row)) grid.Set(x, row, TileGrid.OneWay);
                        }
                        break;
                    }
                }
            }
            // Crumbling blocks count as solid (they come back).
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (grid.Has(x, y, TileGrid.Crumble)) grid.Set(x, y, TileGrid.Solid | TileGrid.Crumble);

            var (paths, dashing, infos) = RecordPaths(config);
            r.Paths.AddRange(paths);
            r.Dashing.AddRange(dashing);
            r.PathInfo.AddRange(infos);

            var size = config.playerSize;
            var start = from ?? new Cell(M.Floor(level.Start.X), M.Floor(level.Start.Y) - 1);
            // Cheapest-first search, so routes prefer walking and short hops over long running
            // jumps (the Autopilot follows these routes).
            var cost = new Dictionary<Cell, double> { [start] = 0 };
            var open = new MinHeap();
            open.Push(0, start);
            var done = new HashSet<Cell>();
            r.Standable.Add(start);
            var hit = new Collide.Hit();
            var events = new List<SimEvent>();
            var trail = new List<V2d>();
            while (open.Count > 0)
            {
                var s = open.Pop();
                if (!done.Add(s)) continue;
                double baseCost = cost[s];
                var feet = new V2d(s.X + 0.5, s.Y + 1);
                Mark(r, feet, size, targetCells, s);
                bool onSpring = springs.Contains(s);
                if (onSpring) feet.Y -= 0.5; // springs launch from their top, half a tile up
                foreach (int dx in new[] { -1, 1 })
                {
                    var n = new Cell(s.X + dx, s.Y);
                    if (Standable(grid, n, size)) Relax(r, cost, open, n, s, -1, baseCost + 1);
                }
                for (int k = 0; k < r.Paths.Count; k++)
                {
                    var info = r.PathInfo[k];
                    if (info.FromSpring != onSpring) continue;
                    if (info.Live)
                    {
                        PlayLive(r, grid, config, info, k, s, feet, targetCells, cost, open, baseCost, events, trail);
                        continue;
                    }
                    var path = r.Paths[k];
                    var dash = r.Dashing[k];
                    var b = new Body(feet, size);
                    var prev = V2d.Zero;
                    for (int t = 0; t < path.Length; t++)
                    {
                        var d = path[t] - prev;
                        prev = path[t];
                        hit.Clear();
                        Collide.MoveX(b, grid, d.X, config.ledgeCorrection, true, hit, dash[t] ? Collide.WallBreak.PassThrough : Collide.WallBreak.None);
                        Collide.MoveY(b, grid, d.Y, config.cornerCorrection, false, null, hit);
                        Mark(r, b.Pos, size, targetCells, s);
                        if (Collide.TouchingSpikes(b, grid) || b.Top > grid.Height + 1) break;
                        if (hit.Floor && t > 0)
                        {
                            var land = new Cell(M.Floor(b.Pos.X), M.Round(b.Pos.Y) - 1);
                            double jumpCost = (info.Vx != 0 ? 6.0 : 4.0) + (info.Style.Contains("dash") ? 2.0 : 0) + 0.5 * Math.Abs(land.X - s.X);
                            foreach (var cell in new[] { land, new Cell(M.Round(b.Pos.X - 0.5), land.Y) })
                                if (Standable(grid, cell, size)) Relax(r, cost, open, cell, s, k, baseCost + jumpCost);
                            break;
                        }
                    }
                }
            }
            foreach (var t in r.Targets)
                if (!r.Touched.Contains(t.Cell)) r.Missing.Add(t);
            return r;
        }

        /// <summary>Steps the real Player from a standing spot on the level's tiles (the wall-jump paths).</summary>
        static void PlayLive(Result r, TileGrid grid, GameConfig config, PathInfo info, int k, Cell s, V2d feet, HashSet<Cell> targetCells,
            Dictionary<Cell, double> cost, MinHeap open, double baseCost, List<SimEvent> events, List<V2d> trail)
        {
            var size = config.playerSize;
            var p = new Player(config, feet);
            p.Body.Vel.X = info.Vx;
            p.ForceGrounded(true);
            var state = new PathState();
            trail.Clear();
            for (int t = 0; t < LiveTicks; t++)
            {
                events.Clear();
                p.Step(PathInput(info, t, p, state, grid), grid, null, events);
                trail.Add(p.Body.Pos);
                if (Collide.TouchingSpikes(p.Body, grid) || p.Body.Top > grid.Height + 1) break;
                if (t > 0 && p.IsGrounded)
                {
                    if (state.Jumps == 0) return; // never touched a wall: a plain jump covers it
                    var land = new Cell(M.Floor(p.Body.Pos.X), M.Round(p.Body.Pos.Y) - 1);
                    double jumpCost = (info.Vx != 0 ? 8.0 : 6.0) + 2.0 * state.Jumps + 0.5 * Math.Abs(land.X - s.X);
                    foreach (var cell in new[] { land, new Cell(M.Round(p.Body.Pos.X - 0.5), land.Y) })
                        if (Standable(grid, cell, size)) Relax(r, cost, open, cell, s, k, baseCost + jumpCost);
                    break;
                }
            }
            if (state.Jumps == 0) return;
            foreach (var pos in trail) Mark(r, pos, size, targetCells, s);
        }

        static void Relax(Result r, Dictionary<Cell, double> cost, MinHeap open, Cell cell, Cell from, int pathId, double c)
        {
            if (cost.TryGetValue(cell, out double old) && old <= c) return;
            cost[cell] = c;
            r.Standable.Add(cell);
            r.CameFrom[cell] = (from, pathId);
            open.Push(c, cell);
        }

        /// <summary>True if the player can stand in the cell (feet on its bottom edge).</summary>
        public static bool Standable(TileGrid grid, Cell c, V2d size)
        {
            if (!grid.InBounds(c.X, c.Y) || !grid.InBounds(c.X, c.Y + 1)) return false;
            if (!grid.IsFloor(c.X, c.Y + 1)) return false;
            if (grid.Has(c.X, c.Y, TileGrid.Spikes)) return false;
            return Collide.BoxFree(grid, c.X + 0.5, c.Y + 1.0, size);
        }

        static void Mark(Result r, V2d feet, V2d size, HashSet<Cell> targets, Cell from)
        {
            for (int row = M.Floor(feet.Y - size.Y + 0.01); row <= M.Floor(feet.Y - 0.01); row++)
            for (int col = M.Floor(feet.X - size.X * 0.5 + 0.01); col <= M.Floor(feet.X + size.X * 0.5 - 0.01); col++)
            {
                var c = new Cell(col, row);
                r.Touched.Add(c);
                if (targets.Contains(c) && !r.TouchedFrom.ContainsKey(c)) r.TouchedFrom[c] = from;
            }
        }

        /// <summary>Every way of leaving a ledge, recorded with the real Player in empty space. Cached per config.</summary>
        static (List<V2d[]>, List<bool[]>, List<PathInfo>) RecordPaths(GameConfig config)
        {
            string key = string.Join("|", config.Derived().JumpVelocity, config.runSpeed, config.groundAccelTime, config.airAccelMult, config.airDecelMult,
                config.fallGravityMult, config.maxFallSpeed, config.dashEnabled, config.dashDistance, config.dashTime, config.doubleJumpEnabled,
                config.airJumps, config.airJumpHeight, config.springHeight, config.jumpMinHeight, config.playerSize.X, config.playerSize.Y,
                config.apexGravityMult, config.apexHangThreshold, config.turnAccelMult, config.groundDecelTime, config.airDashes, config.wallJumpEnabled);
            if (PathCache.TryGetValue(key, out var cached)) return cached;
            var paths = new List<V2d[]>();
            var dashing = new List<bool[]>();
            var infos = new List<PathInfo>();
            var grid = new TileGrid(400, 200);
            var origin = new V2d(200, 60);
            var events = new List<SimEvent>();
            foreach (bool spring in new[] { false, true })
            foreach (int dir in new[] { -1, 0, 1 })
            {
                var starts = dir == 0 ? new[] { 0.0 } : new[] { 0.0, dir * config.runSpeed };
                foreach (double vx in starts)
                {
                    var styles = new List<string>(spring ? new[] { "spring", "steer_late" } : new[] { "full", "tap", "walk", "steer_late" });
                    if (config.doubleJumpEnabled) styles.Add("double");
                    if (config.dashEnabled && dir != 0) styles.AddRange(spring ? new[] { "jump_dash" } : new[] { "dash", "jump_dash" });
                    if (config.wallJumpEnabled && dir != 0 && !spring) styles.AddRange(new[] { "wall", "wall_switch" });
                    foreach (var style in styles)
                    {
                        if (style.StartsWith("wall"))
                        {
                            // Needs the level's walls: Analyze plays it live from each standing spot.
                            paths.Add(new V2d[0]);
                            dashing.Add(new bool[0]);
                            infos.Add(new PathInfo { Dir = dir, Vx = vx, Style = style, Live = true });
                            continue;
                        }
                        if (style == "walk" && (dir == 0 || vx == 0)) continue;
                        if (style == "steer_late" && (dir == 0 || vx != 0)) continue;
                        if (style == "dash" && vx != 0) continue;
                        var info = new PathInfo { Dir = dir, Vx = vx, Style = style, FromSpring = spring };
                        var p = new Player(config, origin);
                        p.Body.Vel.X = vx;
                        p.ForceGrounded(!spring);
                        if (spring) p.Launch(config.Derived().SpringVelocity);
                        var state = new PathState();
                        var pts = new List<V2d>();
                        var ds = new List<bool>();
                        for (int t = 0; t < 150; t++)
                        {
                            events.Clear();
                            p.Step(PathInput(info, t, p, state), grid, null, events);
                            pts.Add(p.Body.Pos - origin);
                            ds.Add(p.IsDashing || events.Exists(e => e.Type == SimEventType.Dash));
                            if (p.Body.Pos.Y - origin.Y > 60) break;
                        }
                        paths.Add(pts.ToArray());
                        dashing.Add(ds.ToArray());
                        infos.Add(info);
                    }
                }
            }
            var result = (paths, dashing, infos);
            PathCache[key] = result;
            return result;
        }

        /// <summary>Scratch state kept between ticks while playing a path.</summary>
        public sealed class PathState
        {
            public int Phase;
            public int Until;
            /// <summary>Wall paths: the side being steered toward (1 right, -1 left), and the wall jumps made so far.</summary>
            public int Wall;
            public int Jumps;
        }

        /// <summary>
        /// The input for tick t of a path. The Autopilot plays paths with this. Wall paths look at
        /// the walls, so they need the <paramref name="grid"/> (without one they never wall-jump).
        /// </summary>
        public static int PathInput(PathInfo info, int t, Player p, PathState state, TileGrid grid = null)
        {
            int input = 0;
            bool steering = info.Style != "steer_late" || t >= 14; // straight up first, then steer
            if (info.Dir > 0 && steering) input |= InputFrame.Right;
            else if (info.Dir < 0 && steering) input |= InputFrame.Left;
            switch (info.Style)
            {
                case "full":
                case "steer_late":
                    if (!info.FromSpring && t < 30) input |= InputFrame.Jump;
                    break;
                case "tap":
                    if (t == 0) input |= InputFrame.Jump;
                    break;
                case "dash":
                    if (t == 0) input |= InputFrame.Dash;
                    break;
                case "jump_dash":
                    // Jump (held), then dash once the rise slows near the top.
                    if (!info.FromSpring && t < 30 && state.Phase == 0) input |= InputFrame.Jump;
                    if (state.Phase == 0 && t > 2 && p.Body.Vel.Y >= -2.0)
                    {
                        state.Phase = 1;
                        input |= InputFrame.Dash;
                        input &= ~InputFrame.Jump;
                    }
                    break;
                case "double":
                    if (t <= 2) input |= InputFrame.Jump;
                    else if (state.Phase == 0)
                    {
                        // Release at the top of the first jump, then press again for the air jump.
                        if (p.Body.Vel.Y >= -0.5) state.Phase = 1;
                        else input |= InputFrame.Jump;
                    }
                    else if (state.Phase == 1)
                    {
                        state.Phase = 2;
                        state.Until = t + 24;
                        input |= InputFrame.Jump;
                    }
                    else if (t < state.Until) input |= InputFrame.Jump;
                    break;
                case "wall":
                case "wall_switch":
                {
                    // Jump toward a wall (held for height), let go near the top, and jump off the wall
                    // once touching it. Then steer back to the same wall ("wall") or on to the
                    // opposite one ("wall_switch"), and repeat. Landing anywhere ends the path.
                    if (t == 0) state.Wall = info.Dir;
                    input = state.Wall > 0 ? InputFrame.Right : InputFrame.Left;
                    int since = t - state.Until;
                    if (state.Phase == 0)
                    {
                        if (since < 2 || (since < 30 && p.Body.Vel.Y < -1.0)) input |= InputFrame.Jump;
                        else state.Phase = 1;
                    }
                    if (state.Phase == 1 && grid != null && state.Jumps < MaxWallJumps && !p.IsGrounded && p.Body.Vel.Y >= -1.0
                        && (p.PrevInput & InputFrame.Jump) == 0 && Collide.TouchingWall(p.Body, grid, state.Wall))
                    {
                        input |= InputFrame.Jump;
                        state.Phase = 0;
                        state.Until = t;
                        state.Jumps++;
                        if (info.Style == "wall_switch") state.Wall = -state.Wall;
                    }
                    break;
                }
            }
            return input;
        }

        /// <summary>
        /// The level map with what Reach found: '+' standing spots reached, ':' cells the player can
        /// pass through, and '!' over each unreachable target. Then one line per unreachable target.
        /// </summary>
        public static string Report(LevelData level, Result r)
        {
            var sb = new StringBuilder();
            var missing = new HashSet<Cell>();
            foreach (var m in r.Missing) missing.Add(m.Cell);
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    var c = new Cell(x, y);
                    char ch = level.Rows[y][x];
                    if (missing.Contains(c)) sb.Append('!');
                    else if (ch == '.' && r.Standable.Contains(c)) sb.Append('+');
                    else if (ch == '.' && r.Touched.Contains(c)) sb.Append(':');
                    else sb.Append(ch);
                }
                sb.Append('\n');
            }
            sb.Append(r.Missing.Count == 0
                ? $"all {r.Targets.Count} targets (gems, checkpoints, goal) are reachable\n"
                : $"{r.Missing.Count} of {r.Targets.Count} targets are NOT reachable:\n");
            foreach (var m in r.Missing) sb.Append($"  {m.Kind} at {m.Cell.X},{m.Cell.Y} (column {m.Cell.X}, row {m.Cell.Y})\n");
            return sb.ToString();
        }

        /// <summary>A binary min-heap of (cost, cell); equal costs pop in insertion order, so searches are repeatable.</summary>
        sealed class MinHeap
        {
            readonly List<(double C, long N, Cell Cell)> _h = new List<(double, long, Cell)>();
            long _n;
            public int Count => _h.Count;

            public void Push(double c, Cell cell)
            {
                _h.Add((c, _n++, cell));
                int i = _h.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (!Less(_h[i], _h[p])) break;
                    (_h[i], _h[p]) = (_h[p], _h[i]);
                    i = p;
                }
            }

            public Cell Pop()
            {
                var top = _h[0].Cell;
                _h[0] = _h[_h.Count - 1];
                _h.RemoveAt(_h.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, rr = l + 1, m = i;
                    if (l < _h.Count && Less(_h[l], _h[m])) m = l;
                    if (rr < _h.Count && Less(_h[rr], _h[m])) m = rr;
                    if (m == i) break;
                    (_h[i], _h[m]) = (_h[m], _h[i]);
                    i = m;
                }
                return top;
            }

            static bool Less((double C, long N, Cell) a, (double C, long N, Cell) b) => a.C < b.C || (a.C == b.C && a.N < b.N);
        }
    }
}

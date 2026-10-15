using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// A bot that finishes levels in the real simulation. It plans a route with Reach (from
    /// wherever it stands; with every move the config allows, double and wall jumps included),
    /// walks to the take-off spot, plays the planned jump's inputs and
    /// re-plans after every landing or death. Boss fights use a lookahead search instead: it tries
    /// a few dozen short input plans by re-simulating the run so far plus the plan, and keeps the
    /// one that lands a stomp without getting hurt. Every input goes through World.Step(), so a
    /// win is a real, replayable run. `npm run autoplay -- level=N` writes replays/level_NN.replay.
    /// </summary>
    public sealed class Autopilot
    {
        public readonly LevelData Level;
        public readonly GameConfig Config;
        public readonly EnemyConfig Enemies;
        public World World;
        /// <summary>Every input played, one per tick (a replay).</summary>
        public readonly List<int> Inputs = new List<int>();
        public readonly List<string> Log = new List<string>();
        public int MaxTicks = 20000;
        public int MaxDeaths = 30;
        /// <summary>Ticks to wait before starting. The tool retries with different delays when a run fails.</summary>
        public int StartDelay;
        public bool Verbose;
        int _flight = 30;
        readonly List<SimEvent> _lastEvents = new List<SimEvent>();

        public Autopilot(LevelData level, GameConfig config, EnemyConfig enemies)
        {
            Level = level;
            Config = config;
            Enemies = enemies;
            World = new World(level, config, enemies);
        }

        /// <summary>Plays until the goal or until it gives up. Returns true on a win.</summary>
        public bool Run()
        {
            var goal = new Cell(-1, -1);
            foreach (var a in World.Actors)
                if (a.Kind == "goal") goal = new Cell(M.Floor(a.Origin.X), M.Floor(a.Origin.Y) - 1);
            Settle();
            Wait(StartDelay);
            int stuck = 0;
            while (!World.Finished && World.Tick < MaxTicks && World.Deaths <= MaxDeaths)
            {
                if (World.Player.Current == Player.State.Dead)
                {
                    StepInput(0);
                    continue;
                }
                var boss = World.ActiveBoss;
                if (boss != null)
                {
                    FightBoss(boss);
                    continue;
                }
                var cur = CurrentCell();
                var r = Reach.Analyze(Level, Config, Enemies, cur, World.Grid);
                if (!r.TouchedFrom.TryGetValue(goal, out var goalFrom))
                {
                    Note($"no route to the goal from {cur}; waiting");
                    Wait(30);
                    if (++stuck > 20) return false;
                    continue;
                }
                var chain = Chain(r, cur, goalFrom);
                // Walking onto a spot that only a moving platform covers: board it first.
                bool boarded = false;
                foreach (var e in chain)
                {
                    if (e.Path >= 0) break;
                    if (!Supported(e.To) && Supported(e.From))
                    {
                        Board(e.From, e.To);
                        boarded = true;
                        break;
                    }
                }
                if (boarded) continue;
                int jump = -1;
                Cell takeOff = cur, land = cur;
                Reach.PathInfo afterSpring = null;
                for (int i = 0; i < chain.Count; i++)
                {
                    if (chain[i].Path < 0) continue;
                    jump = chain[i].Path;
                    takeOff = chain[i].From;
                    land = chain[i].To;
                    // Landing on a spring launches at once: play the planned spring path then.
                    if (i + 1 < chain.Count && chain[i + 1].Path >= 0 && r.PathInfo[chain[i + 1].Path].FromSpring)
                        afterSpring = r.PathInfo[chain[i + 1].Path];
                    break;
                }
                if (jump < 0)
                {
                    // Only walking left: go to the standing spot, then into the goal.
                    var stand = chain.Count > 0 ? chain[chain.Count - 1].To : cur;
                    WalkTo(stand.X + 0.5);
                    int toward = goal.X + 0.5 > World.Player.Body.Pos.X ? InputFrame.Right : InputFrame.Left;
                    if (goal.Y == stand.Y && stuck < 2) Hold(toward, 90);
                    else
                    {
                        // The goal is up a step (or walking didn't reach it): jump at it.
                        Hold(toward | InputFrame.Jump, 24);
                        Hold(toward, 50);
                    }
                    if (!World.Finished && ++stuck > 20) return false;
                    continue;
                }
                int deaths = World.Deaths;
                Wait((Math.Min(stuck, 10) + World.Deaths % 4) * 13); // vary timing on retries (moving platforms, enemies)
                Note($"at {cur}, jump from {takeOff} to {land} with {r.PathInfo[jump]}");
                _flight = FlightTicks(r.Paths[jump], land - takeOff);
                WaitForClear(new[] { takeOff, land });
                DoJump(r.PathInfo[jump], takeOff, land, afterSpring);
                if (World.Deaths > deaths) Note($"died near {World.Player.Body.Pos}");
                if (World.Deaths > deaths || CurrentCell() == cur) stuck++;
                else stuck = 0;
                if (stuck > 40) return false;
            }
            return World.Finished;
        }

        // ------------------------------------------------------------------ boss fights

        /// <summary>One short input plan: wait, then move toward/away from the boss, jumping at some point.</summary>
        struct Plan
        {
            public int Wait, Move, JumpAt, Hold, Dash;
            public override string ToString() => $"wait {Wait}, move {Move}, jump at {JumpAt} hold {Hold}{(Dash >= 0 ? $", dash at {Dash}" : "")}";
        }

        static readonly List<Plan> Plans = BuildPlans();

        static List<Plan> BuildPlans()
        {
            var list = new List<Plan>();
            foreach (int wait in new[] { 0, 8, 20, 40 })
            foreach (int move in new[] { 1, 0, -1 })
            foreach (var (j, h) in new[] { (-1, 0), (0, 30), (0, 10), (6, 30), (14, 30), (24, 30) })
            {
                list.Add(new Plan { Wait = wait, Move = move, JumpAt = j, Hold = h, Dash = -1 });
                if (move != 0 && j >= 0 && h == 30) list.Add(new Plan { Wait = wait, Move = move, JumpAt = j, Hold = h, Dash = j + 12 });
            }
            return list;
        }

        int PlanInput(Plan p, int t, double bossX, double playerX)
        {
            if (t < p.Wait) return 0;
            int k = t - p.Wait;
            if (k > 90) return 0;
            int toward = bossX >= playerX ? 1 : -1;
            int input = 0;
            if (p.Move != 0) input |= p.Move * toward > 0 ? InputFrame.Right : InputFrame.Left;
            if (p.JumpAt >= 0 && k >= p.JumpAt && k < p.JumpAt + p.Hold) input |= InputFrame.Jump;
            if (p.Dash >= 0 && k == p.Dash) input |= InputFrame.Dash;
            return input;
        }

        /// <summary>
        /// Scores a plan by re-simulating the whole run so far plus the plan (the simulation is
        /// deterministic, so this is exactly what would happen). Returns the score and the tick
        /// (within the plan) of the first boss hit, or -1.
        /// </summary>
        (double score, int hitAt) Score(Plan plan, int horizon)
        {
            var w = new World(Level, Config, Enemies);
            foreach (int input in Inputs) w.Step(input);
            int hurtAt = -1, hitAt = -1;
            var boss = w.ActiveBoss;
            for (int t = 0; t < horizon && !w.Finished; t++)
            {
                var b = w.ActiveBoss ?? boss;
                var ev = w.Step(PlanInput(plan, t, b?.Pos.X ?? 0, w.Player.Body.Pos.X));
                foreach (var e in ev)
                {
                    if ((e.Type == SimEventType.Hurt || e.Type == SimEventType.Die) && hurtAt < 0) hurtAt = t;
                    if (e.Type == SimEventType.BossHit && hitAt < 0) hitAt = t;
                }
                if (hitAt >= 0 && hurtAt < 0 && t > hitAt + 20) break;
                if (hurtAt >= 0 && hitAt < 0) break;
            }
            if (hitAt >= 0 && (hurtAt < 0 || hitAt < hurtAt)) return (10000 - hitAt - (hurtAt >= 0 ? 3000 : 0), hitAt);
            if (hurtAt >= 0) return (-5000 + hurtAt, -1);
            double dist = boss != null ? Math.Abs(w.Player.Body.Pos.X - boss.Pos.X) : 0;
            return (1000 - dist * 4 - plan.Wait * 0.5, -1);
        }

        void FightBoss(Boss boss)
        {
            const int horizon = 170;
            Plan best = Plans[0];
            double bestScore = double.MinValue;
            int bestHit = -1;
            foreach (var p in Plans)
            {
                var (s, hitAt) = Score(p, horizon);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = p;
                    bestHit = hitAt;
                }
            }
            Note($"boss {boss.Name} hp {boss.Health} ({boss.Anim}): {best} score {bestScore:0}");
            int commit = bestHit >= 0 ? bestHit + 4 : Math.Min(best.Wait + 10, 14);
            for (int t = 0; t < commit && !World.Finished; t++)
            {
                var b = World.ActiveBoss ?? boss;
                StepInput(PlanInput(best, t, b.Pos.X, World.Player.Body.Pos.X));
                if (World.Player.Current == Player.State.Dead) return;
            }
            if (bestHit >= 0) Settle();
        }

        // ------------------------------------------------------------------ routes

        void DoJump(Reach.PathInfo meta, Cell from, Cell land, Reach.PathInfo afterSpring)
        {
            int dir = meta.Dir;
            double x = from.X + 0.5;
            if (!Supported(from))
            {
                // The take-off spot is only a place a moving platform passes: ride it there.
                RideTo(x);
                WaitForPlatform(land, _flight);
                PlayPath(meta, afterSpring);
                return;
            }
            if (meta.FromSpring)
            {
                // Walk onto the spring; the launch happens on its own. Then steer.
                for (int guard = 0; guard < 240 && !World.Finished; guard++)
                {
                    double px = World.Player.Body.Pos.X;
                    var ev = StepInput(x > px + 0.05 ? InputFrame.Right : x < px - 0.05 ? InputFrame.Left : 0);
                    if (ev.Exists(e => e.Type == SimEventType.Spring)) break;
                }
                PlayPath(meta, null);
                return;
            }
            if (meta.Vx != 0)
            {
                // Running start: back up (only over safe ground), then run through the take-off point.
                WalkTo(x - dir * SafeRunup(from, dir));
                WaitForPlatform(land, _flight + 14);
                for (int guard = 0; guard < 120 && !World.Finished && (World.Player.Body.Pos.X - x) * dir < 0; guard++)
                    StepInput(dir > 0 ? InputFrame.Right : InputFrame.Left);
            }
            else
            {
                WalkTo(x);
                WaitForPlatform(land, _flight);
            }
            PlayPath(meta, afterSpring);
        }

        /// <summary>If the landing spot is only a ledge because a moving platform passes there, waits until it will be there.</summary>
        void WaitForPlatform(Cell land, int lead)
        {
            if (Supported(land)) return;
            for (int i = 0; i < 400; i++)
            {
                foreach (var a in World.Actors)
                {
                    if (!(a is MovingPlatform mp)) continue;
                    var p = mp.Predict(lead);
                    if (Math.Abs(p.X - (land.X + 0.5)) < a.Size.X * 0.5 - 0.3 && Math.Abs(p.Y - a.Size.Y - (land.Y + 1)) < 0.75) return;
                }
                StepInput(0);
            }
        }

        static int FlightTicks(V2d[] path, Cell d)
        {
            for (int i = 0; i < path.Length; i++)
                if (i > 3 && Math.Abs(path[i].X - d.X) < 0.6 && path[i].Y >= d.Y - 0.1) return i;
            return 30;
        }

        void PlayPath(Reach.PathInfo meta, Reach.PathInfo afterSpring)
        {
            var state = new Reach.PathState();
            int ticks = meta.Live ? Reach.LiveTicks : 180;
            for (int t = 0; t < ticks; t++)
            {
                if (World.Finished || World.Player.Current == Player.State.Dead) return;
                var ev = StepInput(Reach.PathInput(meta, t, World.Player, state, World.Grid));
                if (afterSpring != null && ev.Exists(e => e.Type == SimEventType.Spring))
                {
                    PlayPath(afterSpring, null);
                    return;
                }
                if (t > 3 && World.Player.IsGrounded)
                {
                    Settle();
                    return;
                }
            }
        }

        /// <summary>True if a dangerous enemy is just ahead on our level, in direction dir.</summary>
        bool EnemyAhead(int dir)
        {
            var p = World.Player.Body.Pos;
            foreach (var a in World.Actors)
            {
                if (!a.Active || !a.IsDangerous || a is Boss) continue;
                double dx = (a.Pos.X - p.X) * dir;
                if (dx > 0.6 && dx < 2.4 && Math.Abs(a.Pos.Y - p.Y) < 1.2) return true;
            }
            return false;
        }

        /// <summary>A full jump forward: stomps a walker in the way, clears a spiker.</summary>
        void HopOver(int dir)
        {
            int run = dir > 0 ? InputFrame.Right : InputFrame.Left;
            for (int t = 0; t < 60; t++)
            {
                if (World.Finished || World.Player.Current == Player.State.Dead) return;
                StepInput(t < 18 ? run | InputFrame.Jump : run);
                if (t > 3 && World.Player.IsGrounded) return;
            }
        }

        /// <summary>Waits (up to 3 s) until no dangerous enemy is near any of the cells.</summary>
        void WaitForClear(Cell[] cells)
        {
            for (int i = 0; i < 180; i++)
            {
                bool clear = true;
                foreach (var a in World.Actors)
                {
                    if (!a.Active || !a.IsDangerous || a is Boss) continue;
                    foreach (var c in cells)
                        if (Math.Abs(a.Pos.X - (c.X + 0.5)) < 2.5 && Math.Abs(a.Pos.Y - (c.Y + 1)) < 2.5) clear = false;
                }
                if (clear || World.Finished) return;
                StepInput(0);
            }
        }

        bool Supported(Cell c) => World.Grid.IsFloor(c.X, c.Y + 1);

        /// <summary>Walks to the ledge cell `from`, waits for a platform to cover `to`, steps on.</summary>
        void Board(Cell from, Cell to)
        {
            WalkTo(from.X + 0.5);
            int dir = to.X > from.X ? 1 : -1;
            for (int i = 0; i < 600; i++)
            {
                if (World.Finished || World.Player.Current == Player.State.Dead) return;
                bool ready = false;
                foreach (var s in World.Surfaces)
                    if (Math.Abs(s.Y - (to.Y + 1)) < 0.3 && to.X + 0.5 > s.X + 0.4 && to.X + 0.5 < s.Right - 0.4) ready = true;
                if (ready) break;
                StepInput(0);
            }
            for (int i = 0; i < 40; i++)
            {
                if (World.Player.Surface >= 0 || World.Player.Current == Player.State.Dead) break;
                StepInput(dir > 0 ? InputFrame.Right : InputFrame.Left);
            }
            Note($"boarded a platform at {World.Player.Body.Pos.X:0.00}");
        }

        /// <summary>Rides a moving platform toward x, walking along it (without stepping off its edge) when the ride alone won't get there.</summary>
        void RideTo(double x)
        {
            for (int i = 0; i < 600; i++)
            {
                if (World.Finished || World.Player.Current == Player.State.Dead) return;
                var b = World.Player.Body;
                double dx = x - b.Pos.X;
                if (Math.Abs(dx) < 0.3 && World.Player.IsGrounded) return;
                int input = 0;
                int pf = World.Player.Surface;
                if (pf >= 0 && pf < World.Surfaces.Count && Math.Abs(dx) >= 0.3)
                {
                    var s = World.Surfaces[pf];
                    int dir = dx > 0 ? 1 : -1;
                    double ahead = b.Pos.X + dir * 0.45;
                    if (ahead > s.X + 0.1 && ahead < s.Right - 0.1) input = dir > 0 ? InputFrame.Right : InputFrame.Left;
                }
                StepInput(input);
            }
        }

        /// <summary>Walks to x (feet) and stops there.</summary>
        void WalkTo(double x)
        {
            int guard = 0;
            while (guard < 300 && !World.Finished && World.Player.Current != Player.State.Dead)
            {
                double dx = x - World.Player.Body.Pos.X;
                double vx = World.Player.Body.Vel.X;
                if (Math.Abs(dx) < 0.12 && Math.Abs(vx) < 0.5) return;
                // Brake early enough to stop on the spot.
                double stopping = vx * vx / (2.0 * Config.Derived().GroundDecel);
                int input = 0;
                if (Math.Abs(dx) > stopping + 0.05)
                {
                    int dir = dx > 0 ? 1 : -1;
                    input = dir > 0 ? InputFrame.Right : InputFrame.Left;
                    if (World.Player.IsGrounded && EnemyAhead(dir))
                    {
                        HopOver(dir);
                        guard += 30;
                        continue;
                    }
                }
                StepInput(input);
                guard++;
            }
        }

        void Settle()
        {
            for (int guard = 0; guard < 120 && !World.Finished && !World.Player.IsGrounded && World.Player.Current != Player.State.Dead; guard++)
                StepInput(0);
        }

        void Hold(int input, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                if (World.Finished || World.Player.Current == Player.State.Dead) return;
                if (World.ActiveBoss != null) return;
                StepInput(input);
            }
        }

        void Wait(int ticks)
        {
            for (int i = 0; i < ticks && !World.Finished; i++) StepInput(0);
        }

        List<SimEvent> StepInput(int input)
        {
            Inputs.Add(input);
            var ev = World.Step(input);
            _lastEvents.Clear();
            _lastEvents.AddRange(ev);
            if (Verbose)
                foreach (var e in ev)
                    if (e.Type == SimEventType.Hurt || e.Type == SimEventType.Die || e.Type == SimEventType.BossHit)
                        Note($"{e.Type} at {e.Pos}");
            return _lastEvents;
        }

        /// <summary>How far back (tiles) to start a run-up from `from`: up to 1.6, over cells that are safe to stand on.</summary>
        double SafeRunup(Cell from, int dir)
        {
            double best = 0;
            for (int d = 1; d <= 2; d++)
            {
                var c = new Cell(from.X - dir * d, from.Y);
                var g = World.Grid;
                if (!g.IsFloor(c.X, c.Y + 1) || g.Has(c.X, c.Y, TileGrid.Spikes) || g.IsSolid(c.X, c.Y)) break;
                best = d == 1 ? 0.8 : 1.6;
            }
            return best;
        }

        /// <summary>The standing cell under the player: the column under the body center, or the neighbour it actually stands on at a ledge edge.</summary>
        Cell CurrentCell()
        {
            var b = World.Player.Body;
            int row = M.Round(b.Pos.Y) - 1;
            int cx = M.Floor(b.Pos.X);
            foreach (int col in new[] { cx, M.Floor(b.Left + 0.01), M.Floor(b.Right - 0.01) })
                if (World.Grid.IsFloor(col, row + 1)) return new Cell(col, row);
            return new Cell(cx, row);
        }

        struct Edge
        {
            public Cell From, To;
            public int Path;
        }

        /// <summary>The edges from start to target (Path -1 = walk).</summary>
        static List<Edge> Chain(Reach.Result r, Cell start, Cell target)
        {
            var list = new List<Edge>();
            var c = target;
            for (int guard = 0; c != start && r.CameFrom.TryGetValue(c, out var e) && guard < 10000; guard++)
            {
                list.Insert(0, new Edge { From = e.From, To = c, Path = e.Path });
                c = e.From;
            }
            return list;
        }

        void Note(string s)
        {
            Log.Add($"tick {World.Tick}: {s}");
        }
    }
}

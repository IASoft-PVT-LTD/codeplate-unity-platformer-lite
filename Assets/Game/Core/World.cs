using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// The whole level simulation: tiles, player, actors and the rules that tie them together.
    /// Pure and deterministic: the same level, config and inputs always give the same run.
    /// Advance it with Step(input) once per tick (60 per second); it returns that tick's events.
    /// </summary>
    public sealed class World
    {
        public readonly GameConfig Config;
        public readonly EnemyConfig Enemies;
        public readonly LevelData Level;
        public readonly TileGrid Grid;
        public readonly Player Player;
        public readonly List<Actor> Actors;
        public readonly List<Boss> Bosses = new List<Boss>();
        /// <summary>Seeded from the level number, for anything random in the level.</summary>
        public readonly Rng Rng;
        public int Tick;

        public int Coins;
        /// <summary>Which of the level's 3 gems were found, by gem index.</summary>
        public readonly bool[] Gems = new bool[LevelData.GemsPerLevel];
        public int Deaths;
        /// <summary>The level clock's time limit in ticks: the level's `time_limit:`, else config.json's timeLimit (0 = no countdown).</summary>
        public readonly int AllowanceTicks;
        /// <summary>Ticks left on the level clock (clock pickups add to it). Only counts down while the player is alive.</summary>
        public int TicksLeft;
        /// <summary>This level has a countdown (clockEnabled, and a time limit from the level or config.json).</summary>
        public bool TimerOn => Config.clockEnabled && AllowanceTicks > 0;
        public double TimeLeft => TicksLeft * GameConfig.Dt;
        bool _timeUp, _checkpointReached;
        public bool Finished;
        /// <summary>
        /// The last heart is gone and outOfHeartsRestarts is on: the level is failed. The world keeps
        /// stepping its actors but the player never comes back; the game starts the level again from
        /// the beginning (a new World), or a continue gives a heart back (<see cref="ContinueWithExtraHeart"/>).
        /// </summary>
        public bool OutOfHearts;
        /// <summary>Continues (<see cref="ContinueWithExtraHeart"/>) used in this attempt.</summary>
        public int ContinuesUsed;
        /// <summary>The goal opens once every boss is defeated (at once in levels without a boss).</summary>
        public bool GoalOpen;
        public V2d Checkpoint;

        /// <summary>Surfaces the player can stand on this tick (index-aligned with their owners).</summary>
        public readonly List<Box> Surfaces = new List<Box>();
        readonly List<Actor> _surfaceOwners = new List<Actor>();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly List<Actor> _spawned = new List<Actor>();
        readonly Dictionary<Cell, int> _crumbling = new Dictionary<Cell, int>();
        readonly Dictionary<Cell, int> _broken = new Dictionary<Cell, int>();
        int _respawnIn;
        double _prevBottom;
        readonly Collide.Hit _carryHit = new Collide.Hit();

        public World(LevelData level, GameConfig config, EnemyConfig enemies)
        {
            Level = level;
            Config = config;
            Enemies = enemies ?? new EnemyConfig();
            Grid = level.BuildGrid();
            Checkpoint = level.Start;
            AllowanceTicks = GameConfig.Ticks(level.TimeLimit > 0 ? level.TimeLimit : config.timeLimit);
            TicksLeft = AllowanceTicks;
            Rng = new Rng((uint)(level.Number * 2654435761u + 1));
            Player = new Player(config, level.Start);
            Actors = level.BuildActors();
            foreach (var a in Actors)
            {
                a.Setup(this);
                if (a is Boss b) Bosses.Add(b);
            }
            GoalOpen = Bosses.Count == 0;
            _prevBottom = Player.Body.Bottom;
            CollectSurfaces();
        }

        /// <summary>Advances the world by one tick with this input (InputFrame bits). The list is reused: read it before the next Step.</summary>
        public List<SimEvent> Step(int input)
        {
            _events.Clear();
            if (Finished) return _events;
            Tick++;

            // 1. Moving things first; carry the player if they stand on one.
            foreach (var a in Actors)
                if (a.Active) a.Step(this);
            if (_spawned.Count > 0)
            {
                foreach (var a in _spawned)
                {
                    a.Setup(this);
                    Actors.Add(a);
                }
                _spawned.Clear();
            }
            if (Player.Surface >= 0 && Player.Surface < _surfaceOwners.Count && Player.Current != Player.State.Dead)
            {
                var owner = _surfaceOwners[Player.Surface];
                var d = owner.SurfaceDelta;
                if (owner.Active && (d.X != 0 || d.Y != 0))
                {
                    _carryHit.Clear();
                    Collide.MoveX(Player.Body, Grid, d.X, 0, false, _carryHit);
                    var s = owner.Surface;
                    if (!s.IsEmpty) Player.Body.Pos.Y = s.Y;
                }
            }
            CollectSurfaces();

            // 2. The player.
            _prevBottom = Player.Body.Bottom;
            if (Player.Current == Player.State.Dead)
            {
                if (!OutOfHearts && --_respawnIn <= 0) Respawn();
            }
            else
            {
                Player.Step(input, Grid, Surfaces, _events);
                Crumble();
                TouchActors();
                if (TimerOn && !Finished && Player.Current != Player.State.Dead && --TicksLeft <= 0)
                {
                    TicksLeft = 0;
                    _timeUp = true;
                    Emit(SimEventType.TimeUp, Player.Body.Pos);
                    Die(loseHeart: true);
                }
                if (Player.Current != Player.State.Dead && Collide.TouchingSpikes(Player.Body, Grid)) HurtPlayer(Player.Body.Pos.X);
                if (Player.Current != Player.State.Dead && Player.Body.Top > Grid.Height + 1) Die(loseHeart: Config.fallsCostHeart);
            }
            RestoreCrumbled();
            Actors.RemoveAll(a => a.Transient && !a.Active);
            return _events;
        }

        /// <summary>The events of the last Step (or of ContinueWithExtraHeart).</summary>
        public IReadOnlyList<SimEvent> LastEvents => _events;

        /// <summary>Adds an actor during play (projectiles). It's set up and starts stepping next tick.</summary>
        public void Spawn(Actor a)
        {
            a.Transient = true;
            _spawned.Add(a);
        }

        /// <summary>Adds an event to this tick's list (actors use it for sounds and effects).</summary>
        public void Emit(SimEventType type, V2d pos, double value = 0) => _events.Add(new SimEvent(type, pos, value));

        /// <summary>Called by enemies when the player touches them unsafely.</summary>
        public void HurtPlayer(double fromX)
        {
            if (Player.Hurt(fromX, _events) && Player.Health <= 0) Die(loseHeart: false); // the hit took the last heart
        }

        /// <summary>Called by enemies when stomped: the player bounces (higher while holding jump).</summary>
        public void Stomped(Actor a, bool bump = false)
        {
            bool held = (Player.PrevInput & InputFrame.Jump) != 0;
            Player.Launch(held ? Config.stompBounceHeld : Config.stompBounce);
            Emit(bump ? SimEventType.Bump : SimEventType.Stomp, a.Pos);
        }

        /// <summary>Called by pickups.</summary>
        public void Collect(Pickup p)
        {
            p.Active = false;
            switch (p.Kind)
            {
                case "coin":
                    Coins++;
                    Emit(SimEventType.Coin, p.Pos, Coins);
                    break;
                case "gem":
                    if (p.GemIndex >= 0 && p.GemIndex < Gems.Length) Gems[p.GemIndex] = true;
                    Emit(SimEventType.Gem, p.Pos, p.GemIndex);
                    break;
                case "clock":
                    TicksLeft += GameConfig.Ticks(Config.timeBonus);
                    Emit(SimEventType.Clock, p.Pos, TicksLeft);
                    break;
                case "heart":
                    Player.Health = Math.Min(Player.Health + 1, Config.maxHealth);
                    Emit(SimEventType.Heart, p.Pos, Player.Health);
                    break;
            }
        }

        public void ReachCheckpoint(Checkpoint c)
        {
            Checkpoint = c.Origin;
            _checkpointReached = true;
            Emit(SimEventType.Checkpoint, c.Pos);
        }

        public void ReachGoal(Checkpoint g)
        {
            Finished = true;
            g.Reached = true;
            Emit(SimEventType.Goal, g.Pos);
        }

        /// <summary>Called by springs the player touches.</summary>
        public void SpringLaunch(Spring s)
        {
            Player.Body.Pos.Y = Math.Min(Player.Body.Pos.Y, s.Rect.Y);
            Player.Launch(Config.Derived().SpringVelocity);
            Emit(SimEventType.Spring, s.Pos);
        }

        /// <summary>Opens the goal once every boss is defeated.</summary>
        public void CheckGoalOpen()
        {
            if (GoalOpen) return;
            foreach (var b in Bosses)
                if (!b.Defeated) return;
            GoalOpen = true;
            foreach (var a in Actors)
                if (a.Kind == "goal") Emit(SimEventType.GoalOpen, a.Pos);
        }

        /// <summary>Elapsed play time in seconds.</summary>
        public double Seconds => Tick * GameConfig.Dt;

        /// <summary>The bosses that are awake and not yet defeated (the HUD shows their health).</summary>
        public Boss ActiveBoss
        {
            get
            {
                foreach (var b in Bosses)
                    if (b.Awake && !b.Defeated) return b;
                return null;
            }
        }

        /// <summary>A compact, comparable picture of the world (tests compare these for determinism).</summary>
        public string Snapshot()
        {
            var sb = new StringBuilder();
            var b = Player.Body;
            sb.AppendFormat(CultureInfo.InvariantCulture, "t{0} p{1:F4},{2:F4} {3} h{4} c{5} g{6}{7}{8} d{9} f{10} s{11}",
                Tick, b.Pos.X, b.Pos.Y, Player.Current, Player.Health, Coins, Gems[0] ? 1 : 0, Gems[1] ? 1 : 0, Gems[2] ? 1 : 0, Deaths, Finished ? 1 : OutOfHearts ? 2 : 0, TicksLeft);
            foreach (var a in Actors) sb.Append(' ').Append(a.Snapshot());
            return sb.ToString();
        }

        /// <summary>Crumble state for views: 0 intact, 1 cracking, 2 broken.</summary>
        public int CrumbleState(Cell c) => _broken.ContainsKey(c) ? 2 : _crumbling.ContainsKey(c) ? 1 : 0;

        /// <summary>
        /// A death. Falls (fallsCostHeart) and time up cost a heart here (<paramref name="loseHeart"/>);
        /// an enemy's or spike's hit already took its heart. With hearts left the player respawns at
        /// the last checkpoint keeping them. With none: OutOfHearts (outOfHeartsRestarts), or the
        /// classic rule, a respawn with full hearts.
        /// </summary>
        void Die(bool loseHeart)
        {
            if (Player.Current == Player.State.Dead) return;
            if (loseHeart && Player.Health > 0) Player.Health--;
            Player.Kill();
            Deaths++;
            _respawnIn = GameConfig.Ticks(Config.respawnDelay);
            Emit(SimEventType.Die, Player.Body.Pos, Player.Health);
            if (Player.Health <= 0 && Config.outOfHeartsRestarts)
            {
                OutOfHearts = true;
                Emit(SimEventType.OutOfHearts, Player.Body.Pos);
            }
        }

        /// <summary>
        /// A continue after running out of hearts (an ad or a shop can grant one): one
        /// heart, back at the last checkpoint (the start if none), keeping coins, gems and the clock
        /// as they were when the last heart went. If that heart went at time up, the clock gets the
        /// usual time-up refill, as a time up with hearts left would. Returns false (and changes
        /// nothing) unless the level is out of hearts. Its events (Respawn, ExtraHeart) are in
        /// <see cref="LastEvents"/> until the next Step.
        /// </summary>
        public bool ContinueWithExtraHeart()
        {
            if (!OutOfHearts || Finished) return false;
            _events.Clear();
            OutOfHearts = false;
            ContinuesUsed++;
            Player.Health = 1;
            Respawn();
            Emit(SimEventType.ExtraHeart, Checkpoint, TicksLeft);
            return true;
        }

        /// <summary>The clock after a time up: timeUpRefill of the limit at a checkpoint, all of it back at the start.</summary>
        public int TimeUpRefillTicks => _checkpointReached ? GameConfig.Ticks(AllowanceTicks * GameConfig.Dt * Config.timeUpRefill) : AllowanceTicks;

        void Respawn()
        {
            if (_timeUp)
            {
                _timeUp = false;
                TicksLeft = TimeUpRefillTicks;
            }
            if (Player.Health <= 0) Player.Health = Config.maxHealth; // the classic rule (outOfHeartsRestarts false)
            Player.ResetAt(Checkpoint);
            foreach (var a in Actors) a.OnPlayerRespawn(this);
            Actors.RemoveAll(a => a.Transient && !a.Active);
            CollectSurfaces();
            Emit(SimEventType.Respawn, Checkpoint);
        }

        void CollectSurfaces()
        {
            Surfaces.Clear();
            _surfaceOwners.Clear();
            foreach (var a in Actors)
            {
                if (!a.Active) continue;
                var r = a.Surface;
                if (r.IsEmpty) continue;
                Surfaces.Add(r);
                _surfaceOwners.Add(a);
            }
        }

        void Crumble()
        {
            if (!Player.IsGrounded) return;
            int row = M.Round(Player.Body.Bottom);
            var b = Player.Body;
            for (int col = M.Floor(b.Left + Collide.Eps); col <= M.Floor(b.Right - Collide.Eps); col++)
            {
                var c = new Cell(col, row);
                if (Grid.Has(col, row, TileGrid.Crumble) && Grid.IsSolid(col, row) && !_crumbling.ContainsKey(c))
                    _crumbling[c] = GameConfig.Ticks(Config.crumbleDelay);
            }
        }

        static readonly List<Cell> Scratch = new List<Cell>();

        void RestoreCrumbled()
        {
            if (_crumbling.Count > 0)
            {
                Scratch.Clear();
                Scratch.AddRange(_crumbling.Keys);
                Scratch.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
                foreach (var c in Scratch)
                {
                    if (--_crumbling[c] > 0) continue;
                    _crumbling.Remove(c);
                    Grid.Set(c.X, c.Y, TileGrid.Crumble); // no longer solid
                    _broken[c] = GameConfig.Ticks(Config.crumbleRespawn);
                    Emit(SimEventType.Crumble, new V2d(c.X + 0.5, c.Y + 1));
                }
            }
            if (_broken.Count > 0)
            {
                Scratch.Clear();
                Scratch.AddRange(_broken.Keys);
                Scratch.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
                foreach (var c in Scratch)
                {
                    if (--_broken[c] > 0) continue;
                    if (Player.Body.Rect.Intersects(new Box(c.X, c.Y, 1, 1)))
                    {
                        _broken[c] = 1; // wait until the player is out of the way
                        continue;
                    }
                    _broken.Remove(c);
                    Grid.Set(c.X, c.Y, TileGrid.Solid | TileGrid.Crumble);
                }
            }
        }

        void TouchActors()
        {
            var b = Player.Body;
            // Index loop: touching can't add actors, but keep it robust if one ever does.
            for (int i = 0; i < Actors.Count; i++)
            {
                var a = Actors[i];
                if (!a.Active || a is MovingPlatform) continue;
                if (!b.Rect.Intersects(a.Rect)) continue;
                // A stomp: falling, and our feet were above the actor's top half last tick.
                bool stomp = b.Vel.Y > 0 && _prevBottom <= a.Rect.Y + a.Size.Y * 0.5;
                a.Touched(this, stomp);
                if (Player.Current == Player.State.Dead || Finished) return;
            }
        }
    }
}

using System;

namespace Game.Core
{
    /// <summary>
    /// A turret that fires a slow projectile on a fixed timer while the player is near: the
    /// example for anything that spawns actors. It glows for `windup` seconds before each shot.
    /// Stomp it to defeat it. `at` params: dir=-1|1 (fixed aim; default: toward the player),
    /// interval (seconds between shots), delay (seconds before the first shot).
    /// </summary>
    public sealed class Shooter : Actor
    {
        int _interval, _windup, _timer, _squashed, _aim = -1;
        bool _fixedAim;

        public override void Setup(World world)
        {
            var info = world.Enemies.shooter;
            Size = info.size;
            Pos = Origin;
            _interval = Math.Max(10, GameConfig.Ticks(Num("interval", info.interval)));
            _windup = Math.Min(_interval - 1, GameConfig.Ticks(info.windup));
            _timer = GameConfig.Ticks(Num("delay", info.interval));
            double dir = Num("dir", 0);
            _fixedAim = dir != 0;
            if (_fixedAim) _aim = dir > 0 ? 1 : -1;
        }

        public override void Step(World world)
        {
            if (_squashed > 0)
            {
                if (--_squashed == 0) Active = false;
                return;
            }
            var info = world.Enemies.shooter;
            var p = world.Player.Body.Pos;
            if (Math.Abs(p.X - Pos.X) > info.wakeDistance || Math.Abs(p.Y - Pos.Y) > 8)
            {
                _timer = Math.Max(_timer, _windup + 1);
                return;
            }
            if (!_fixedAim && _timer > _windup) _aim = p.X >= Pos.X ? 1 : -1;
            if (--_timer > 0) return;
            _timer = _interval;
            var shot = new Projectile
            {
                Kind = "projectile",
                Origin = new V2d(Pos.X + _aim * (Size.X * 0.5 + 0.1), Pos.Y - Size.Y * 0.35),
                Dir = _aim,
            };
            world.Spawn(shot);
            world.Emit(SimEventType.Shoot, Pos, _aim);
        }

        public override void Touched(World world, bool stomp)
        {
            if (_squashed > 0) return;
            if (stomp)
            {
                _squashed = 20;
                world.Stomped(this);
            }
            else world.HurtPlayer(Pos.X);
        }

        /// <summary>"charge" during the windup before a shot.</summary>
        public override string Anim => _squashed > 0 ? "squashed" : _timer <= _windup ? "charge" : "idle";
        public override int Facing => _aim;
        public override bool IsDangerous => _squashed == 0;
        public override bool IsDefeated => _squashed > 0 || !Active;
    }

    /// <summary>A Shooter's shot: flies straight, hurts on touch, vanishes at a wall or after its range.</summary>
    public sealed class Projectile : Actor
    {
        public int Dir = 1;
        double _speed, _left;

        public override void Setup(World world)
        {
            var info = world.Enemies.shooter;
            Transient = true;
            Size = info.projectileSize;
            Pos = new V2d(Origin.X, Origin.Y + Size.Y * 0.5);
            _speed = info.projectileSpeed;
            _left = info.projectileRange;
        }

        public override void Step(World world)
        {
            double dx = Dir * _speed * GameConfig.Dt;
            Pos.X += dx;
            _left -= Math.Abs(dx);
            int col = M.Floor(Pos.X + Dir * Size.X * 0.5);
            int row = M.Floor(Pos.Y - Size.Y * 0.5);
            if (_left <= 0 || world.Grid.IsSolid(col, row)) Active = false;
        }

        public override void Touched(World world, bool stomp)
        {
            world.HurtPlayer(Pos.X - Dir);
            Active = false;
        }

        public override void OnPlayerRespawn(World world) => Active = false;
        public override string Anim => "fly";
        public override int Facing => Dir;
        public override bool IsDangerous => true;
    }
}

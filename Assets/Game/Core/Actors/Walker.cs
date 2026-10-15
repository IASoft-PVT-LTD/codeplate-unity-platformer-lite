using System;

namespace Game.Core
{
    /// <summary>
    /// Walks back and forth, turning at walls and ledges. Stomp it to defeat it.
    /// A Spiker is a Walker with Stompable = false: stomping it hurts.
    /// `at` params: dir=-1|1 (start direction, default -1), speed (tiles/s).
    /// </summary>
    public class Walker : Actor
    {
        public bool Stompable = true;
        public Body Body;
        int _dir = -1;
        double _speed;
        int _squashed;
        readonly Collide.Hit _hit = new Collide.Hit();

        public override void Setup(World world)
        {
            var info = Stompable ? world.Enemies.walker : world.Enemies.spiker;
            Size = info.size;
            Body = new Body(Origin, Size);
            _dir = Num("dir", -1) >= 0 ? 1 : -1;
            _speed = Num("speed", info.speed);
            Pos = Body.Pos;
        }

        public override void Step(World world)
        {
            if (_squashed > 0)
            {
                if (--_squashed == 0) Active = false;
                return;
            }
            double dt = GameConfig.Dt;
            var d = world.Config.Derived();
            Body.Vel.X = _dir * _speed;
            Body.Vel.Y = Math.Min(Body.Vel.Y + d.Gravity * world.Config.fallGravityMult * dt, world.Config.maxFallSpeed);
            _hit.Clear();
            Collide.MoveX(Body, world.Grid, Body.Vel.X * dt, 0, false, _hit);
            Collide.MoveY(Body, world.Grid, Body.Vel.Y * dt, 0, false, null, _hit);
            if (_hit.Left || _hit.Right) _dir = -_dir;
            else if (_hit.Floor || Collide.OnGround(Body, world.Grid, null, false, out _))
            {
                // Turn at ledges: no ground under the leading edge.
                double aheadX = _dir > 0 ? Body.Right + 0.05 : Body.Left - 0.05;
                if (!world.Grid.IsFloor(M.Floor(aheadX), M.Round(Body.Bottom))) _dir = -_dir;
            }
            Pos = Body.Pos;
            if (Pos.Y > world.Grid.Height + 4) Active = false;
        }

        public override void Touched(World world, bool stomp)
        {
            if (_squashed > 0) return;
            if (stomp && Stompable)
            {
                _squashed = 20;
                world.Stomped(this);
            }
            else world.HurtPlayer(Pos.X);
        }

        public override string Anim => _squashed > 0 ? "squashed" : "walk";
        public override int Facing => _dir;
        public override bool IsDangerous => _squashed == 0;
        public override bool IsDefeated => _squashed > 0 || !Active;
    }

    /// <summary>A Walker with spikes on its back: stomping it hurts. Jump over it, or dash past it while it's turning.</summary>
    public sealed class Spiker : Walker
    {
        public Spiker() => Stompable = false;
    }
}

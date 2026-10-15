namespace Game.Core
{
    /// <summary>
    /// Flies back and forth along a fixed path while bobbing up and down. Ignores walls. Stomp it
    /// to defeat it. `at` params: range (tiles each way), speed (tiles/s), amp (bob height, tiles).
    /// The bob uses Trig's table, never Math.Sin, so runs are identical on every machine.
    /// </summary>
    public sealed class Flyer : Actor
    {
        double _range, _speed, _amp, _x;
        int _period, _t, _dir = 1, _squashed;

        public override void Setup(World world)
        {
            var info = world.Enemies.flyer;
            Size = info.size;
            _range = Num("range", info.range);
            _speed = Num("speed", info.speed);
            _amp = Num("amp", info.amplitude);
            _period = System.Math.Max(1, GameConfig.Ticks(info.period));
            _x = Origin.X;
            Pos = Origin;
        }

        public override void Step(World world)
        {
            if (_squashed > 0)
            {
                if (--_squashed == 0) Active = false;
                Pos.Y += 0.15;
                return;
            }
            _t++;
            _x += _dir * _speed * GameConfig.Dt;
            if (_x > Origin.X + _range) { _x = Origin.X + _range; _dir = -1; }
            else if (_x < Origin.X - _range) { _x = Origin.X - _range; _dir = 1; }
            Pos = new V2d(_x, Origin.Y + Trig.SinTicks(_t, _period) * _amp * 0.5);
        }

        public override void Touched(World world, bool stomp)
        {
            if (_squashed > 0) return;
            if (stomp)
            {
                _squashed = 30;
                world.Stomped(this);
            }
            else world.HurtPlayer(Pos.X);
        }

        public override string Anim => _squashed > 0 ? "squashed" : "fly";
        public override int Facing => _dir;
        public override bool IsDangerous => _squashed == 0;
        public override bool IsDefeated => _squashed > 0 || !Active;
    }
}

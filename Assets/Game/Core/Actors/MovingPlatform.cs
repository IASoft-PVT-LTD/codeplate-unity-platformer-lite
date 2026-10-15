using System;

namespace Game.Core
{
    /// <summary>
    /// A platform that slides back and forth and carries the player. You can jump up through it
    /// and land on top (like a one-way tile). `at` params: dx, dy (how far it travels from its
    /// start, tiles; default dx=4), speed (tiles/s, default 2), w (width in tiles, default 3),
    /// wait (seconds it pauses at each end, default 0.5).
    /// </summary>
    public sealed class MovingPlatform : Actor
    {
        V2d _from, _to;
        double _speed, _t;
        int _dir = 1, _waitTicks, _paused;

        public override void Setup(World world)
        {
            Size = new V2d(Num("w", 3), 0.375);
            _from = new V2d(Origin.X - 0.5 + Size.X * 0.5, Origin.Y - 1.0 + Size.Y);
            _to = _from + new V2d(Num("dx", 4), Num("dy", 0));
            _speed = Num("speed", 2);
            _waitTicks = GameConfig.Ticks(Num("wait", 0.5));
            _paused = _waitTicks;
            Pos = _from;
        }

        public override void Step(World world)
        {
            (_t, _dir, _paused) = Advance(_t, _dir, _paused);
            var next = _from.Lerp(_to, _t);
            SurfaceDelta = next - Pos;
            Pos = next;
        }

        (double, int, int) Advance(double t, int dir, int paused)
        {
            double length = _from.DistanceTo(_to);
            if (length <= 0) return (t, dir, paused);
            if (paused > 0) return (t, dir, paused - 1);
            t += dir * _speed * GameConfig.Dt / length;
            if (t >= 1) return (1, -1, _waitTicks);
            if (t <= 0) return (0, 1, _waitTicks);
            return (t, dir, 0);
        }

        /// <summary>Where the platform will be <paramref name="ticks"/> from now (the Autopilot times jumps with it).</summary>
        public V2d Predict(int ticks)
        {
            var s = (_t, _dir, _paused);
            for (int i = 0; i < ticks; i++) s = Advance(s.Item1, s.Item2, s.Item3);
            return _from.Lerp(_to, s.Item1);
        }

        public V2d PathStart => _from;
        public V2d PathEnd => _to;
        public override Box Surface => Rect;
    }
}

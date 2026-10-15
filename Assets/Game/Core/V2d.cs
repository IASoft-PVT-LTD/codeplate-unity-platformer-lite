using System;
using System.Globalization;

namespace Game.Core
{
    /// <summary>
    /// A 2D vector of doubles, in tile units: x to the right, y DOWN (row 0 is the top of the map).
    /// The simulation uses this instead of UnityEngine.Vector2 so it stays engine-free and
    /// deterministic. Views convert with <c>ToVector3()</c> in Game.Runtime (flipping y).
    /// </summary>
    [Serializable]
    public struct V2d : IEquatable<V2d>
    {
        public double X;
        public double Y;

        public V2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static readonly V2d Zero = new V2d(0, 0);
        public static readonly V2d One = new V2d(1, 1);

        public static V2d operator +(V2d a, V2d b) => new V2d(a.X + b.X, a.Y + b.Y);
        public static V2d operator -(V2d a, V2d b) => new V2d(a.X - b.X, a.Y - b.Y);
        public static V2d operator -(V2d a) => new V2d(-a.X, -a.Y);
        public static V2d operator *(V2d a, double s) => new V2d(a.X * s, a.Y * s);
        public static V2d operator *(double s, V2d a) => new V2d(a.X * s, a.Y * s);
        public static V2d operator /(V2d a, double s) => new V2d(a.X / s, a.Y / s);
        public static bool operator ==(V2d a, V2d b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(V2d a, V2d b) => !(a == b);

        public double Length => Math.Sqrt(X * X + Y * Y);
        public double DistanceTo(V2d o) => (o - this).Length;
        public V2d Lerp(V2d to, double t) => new V2d(X + (to.X - X) * t, Y + (to.Y - Y) * t);

        public bool Equals(V2d o) => this == o;
        public override bool Equals(object obj) => obj is V2d o && this == o;
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", X, Y);
    }

    /// <summary>A whole-tile cell coordinate (column, row).</summary>
    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int X;
        public int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public static Cell operator -(Cell a, Cell b) => new Cell(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Cell a, Cell b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Cell a, Cell b) => !(a == b);
        public bool Equals(Cell o) => this == o;
        public override bool Equals(object obj) => obj is Cell o && this == o;
        public override int GetHashCode() => X * 7919 + Y;
        public override string ToString() => $"{X},{Y}";
    }

    /// <summary>An axis-aligned box in tile units: (X, Y) is its top-left corner.</summary>
    [Serializable]
    public struct Box
    {
        public double X, Y, W, H;

        public Box(double x, double y, double w, double h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public double Right => X + W;
        public double Bottom => Y + H;
        public bool IsEmpty => W <= 0 || H <= 0;

        public bool Intersects(Box o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
        public bool Contains(V2d p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "[{0:0.##},{1:0.##} {2:0.##}x{3:0.##}]", X, Y, W, H);
    }

    /// <summary>Small math helpers. Only + - * / and Math.Sqrt, so results match on every machine.</summary>
    public static class M
    {
        public static double MoveToward(double from, double to, double delta)
        {
            if (Math.Abs(to - from) <= delta) return to;
            return from + Math.Sign(to - from) * delta;
        }

        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        public static int Floor(double v) => (int)Math.Floor(v);
        public static int Ceil(double v) => (int)Math.Ceiling(v);
        public static int Round(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
        public static double Snap(double v, double step) => Math.Round(v / step, MidpointRounding.AwayFromZero) * step;
    }

    /// <summary>
    /// A sine table built at load from a Taylor series (only + - * /), so every machine gets the
    /// same values. The simulation never calls Math.Sin. <c>Trig.Sin01(phase)</c> takes a phase in
    /// [0, 1) of a full turn.
    /// </summary>
    public static class Trig
    {
        public const int Size = 256;
        static readonly double[] Table = Build();

        static double[] Build()
        {
            var t = new double[Size];
            const double pi = 3.14159265358979323846;
            for (int i = 0; i < Size; i++)
            {
                // Reduce to [-pi, pi] first so the series converges fast.
                double x = 2 * pi * i / Size;
                if (x > pi) x -= 2 * pi;
                double term = x, sum = x;
                for (int k = 1; k < 12; k++)
                {
                    term *= -x * x / ((2 * k) * (2 * k + 1));
                    sum += term;
                }
                t[i] = sum;
            }
            return t;
        }

        /// <summary>Sine of a whole-number step: index 0..Size-1 covers one full turn.</summary>
        public static double SinIndex(int i) => Table[((i % Size) + Size) % Size];

        /// <summary>Sine of tick <paramref name="t"/> in a cycle of <paramref name="period"/> ticks.</summary>
        public static double SinTicks(int t, int period) => SinIndex((int)((long)(t % period) * Size / period));
    }

    /// <summary>
    /// A small seeded random number generator (xorshift32). The simulation never uses
    /// System.Random or UnityEngine.Random; anything random in a level uses World.Rng, seeded
    /// from the level, so replays stay exact.
    /// </summary>
    public sealed class Rng
    {
        uint _state;

        public Rng(uint seed) => _state = seed == 0 ? 0x9E3779B9u : seed;

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>An int in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));

        /// <summary>A double in [0, 1).</summary>
        public double Next01() => (NextUInt() >> 8) / 16777216.0;
    }
}

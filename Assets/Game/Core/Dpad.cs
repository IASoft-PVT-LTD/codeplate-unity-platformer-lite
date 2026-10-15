using System;

namespace Game.Core
{
    /// <summary>Which directions the on-screen d-pad gives (config.json "dpadAxes").</summary>
    public enum DpadAxes
    {
        /// <summary>All four, with diagonals (eight 45° sectors). "xy".</summary>
        XY,
        /// <summary>Left and right only: the side of the centre the thumb is on. "x".</summary>
        X,
        /// <summary>Up and down only. "y".</summary>
        Y,
        /// <summary>
        /// Left and right, plus down ("x_down", the default: down + jump drops through a one-way
        /// ledge, down in the air falls faster; up does nothing in this game). Above the centre it
        /// reads like X, so a thumb that strays up never loses its direction. Below the centre, down
        /// needs the thumb clearly low: more than 35° below level for a diagonal, more than 70° for
        /// straight down, so a thumb resting a little low doesn't turn a jump into a drop.
        /// </summary>
        XDown,
    }

    /// <summary>
    /// The on-screen d-pad's maths: one touch surface, and a finger picks its directions from where
    /// it is relative to the centre, with a dead zone in the middle. Pure and engine-free so the
    /// EditMode tests can check it; TouchControls feeds it pointer positions.
    /// </summary>
    public static class Dpad
    {
        /// <summary>A thumb this close to the centre (a fraction of the pad's half-width) holds no direction.</summary>
        public const double DeadZone = 0.18;

        // tan(22.5°) and tan(67.5°): the borders of the eight 45° sectors.
        const double Tan22 = 0.41421356237309503;
        const double Tan67 = 2.414213562373095;
        // tan(35°) and tan(70°): XDown's stricter borders below the centre.
        const double Tan35 = 0.7002075382097097;
        const double Tan70 = 2.7474774194546216;

        /// <summary>config.json's "dpadAxes" ("x_down", "x", "xy", "y"); anything else is an error and reads as XDown.</summary>
        public static DpadAxes Parse(string axes, out bool ok)
        {
            ok = true;
            switch ((axes ?? "").Trim().ToLowerInvariant())
            {
                case "x_down": return DpadAxes.XDown;
                case "x": return DpadAxes.X;
                case "xy": return DpadAxes.XY;
                case "y": return DpadAxes.Y;
                default:
                    ok = false;
                    return DpadAxes.XDown;
            }
        }

        /// <summary>
        /// The InputFrame direction bits held by a finger at (dx, dy) from the centre, in fractions
        /// of the pad's half-width (screen y points down, so dy &gt; 0 is below the centre).
        /// </summary>
        public static int Directions(double dx, double dy, DpadAxes axes = DpadAxes.XY)
        {
            switch (axes)
            {
                case DpadAxes.X:
                    return Math.Abs(dx) < DeadZone ? 0 : dx < 0 ? InputFrame.Left : InputFrame.Right;
                case DpadAxes.Y:
                    return Math.Abs(dy) < DeadZone ? 0 : dy < 0 ? InputFrame.Up : InputFrame.Down;
                case DpadAxes.XDown:
                    if (dy <= 0) return Directions(dx, dy, DpadAxes.X);
                    if (dx * dx + dy * dy < DeadZone * DeadZone) return 0;
                    return Sector(dx, dy, Tan35, Tan70);
                default:
                    if (dx * dx + dy * dy < DeadZone * DeadZone) return 0;
                    return Sector(dx, dy, Tan22, Tan67);
            }
        }

        /// <summary>Straight along an axis inside lo, diagonal between lo and hi, straight across beyond hi (as slopes |dy|/|dx|).</summary>
        static int Sector(double dx, double dy, double lo, double hi)
        {
            int h = dx < 0 ? InputFrame.Left : InputFrame.Right;
            int v = dy < 0 ? InputFrame.Up : InputFrame.Down;
            double ax = Math.Abs(dx), ay = Math.Abs(dy);
            if (ay <= ax * lo) return h;
            if (ay >= ax * hi) return v;
            return h | v;
        }

        /// <summary>The direction bits a pad on these axes can give (the other arms are dimmed).</summary>
        public static int Gives(DpadAxes axes)
        {
            switch (axes)
            {
                case DpadAxes.X: return InputFrame.Left | InputFrame.Right;
                case DpadAxes.Y: return InputFrame.Up | InputFrame.Down;
                case DpadAxes.XDown: return InputFrame.Left | InputFrame.Right | InputFrame.Down;
                default: return InputFrame.Left | InputFrame.Right | InputFrame.Up | InputFrame.Down;
            }
        }
    }
}

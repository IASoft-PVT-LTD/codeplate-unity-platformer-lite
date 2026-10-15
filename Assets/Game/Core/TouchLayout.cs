using System;

namespace Game.Core
{
    /// <summary>A box on screen in UI units (game pixels before the UI zoom), measured from the screen's top-left.</summary>
    public struct UiBox
    {
        public double Left, Top, Width, Height;
        public double Right => Left + Width;
        public double Bottom => Top + Height;

        public UiBox(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public bool Overlaps(UiBox o) => Left < o.Right && o.Left < Right && Top < o.Bottom && o.Top < Bottom;

        public override string ToString() => $"({Left:0.#},{Top:0.#} {Width:0.#}x{Height:0.#})";
    }

    /// <summary>
    /// Where the on-screen controls go, for any screen: the d-pad bottom left, dash and jump
    /// bottom right. Every control's bottom edge sits at least <see cref="MinBottom"/> of the
    /// screen's height above the screen's bottom edge (a thumb resting on the phone's edge
    /// shouldn't cover them, and the game's floor stays visible), and inside the safe area
    /// (camera cutouts, rounded corners, the gesture bar). Sizes are in UI units: the UI zooms by
    /// whole numbers, so the screen is always at least 480 x 270 of them (ScreenRouter.Zoom).
    /// The d-pad is sized from the screen: <see cref="DpadWidthShare"/> of its width, at most
    /// <see cref="DpadHeightShare"/> of its height (20:9 phones are wide but short), and its touch
    /// area (<see cref="DpadTouch"/>) reaches <see cref="DpadTouchMargin"/> past the art on the
    /// left, top and right (never lower: the 15% rule holds for the touch area too).
    /// Pure maths so the EditMode tests can check it on any screen shape; TouchControls applies it.
    /// docs/recipes/touch-controls-and-mobile-web.md.
    /// </summary>
    public sealed class TouchLayout
    {
        /// <summary>The lowest a control's bottom edge may sit, as a share of the screen's height.</summary>
        public const double MinBottom = 0.15;
        /// <summary>The d-pad's size at touch size 1: this share of the screen width, but at most DpadHeightShare of its height.</summary>
        public const double DpadWidthShare = 0.25, DpadHeightShare = 0.48;
        /// <summary>How far the d-pad's touch area reaches past its art (left, top, right), as a share of its size.</summary>
        public const double DpadTouchMargin = 0.12;
        /// <summary>Space kept free at the top for the HUD (hearts, clock, gems): no control reaches higher.</summary>
        public const double HudReserve = 44;
        public const double JumpSize = 62, DashSize = 56;
        /// <summary>From the safe area's side edges; between dash and jump; how much higher jump sits.</summary>
        public const double SideMargin = 10, Gap = 6, JumpRaise = 6;
        /// <summary>The space kept clear between the d-pad's touch area and dash.</summary>
        public const double ClusterGap = 40;

        /// <summary>The d-pad as drawn, and the larger area that takes its touches.</summary>
        public UiBox Dpad, DpadTouch, Jump, Dash;
        /// <summary>The bottom edges' distance above the screen's bottom (jump sits JumpRaise higher).</summary>
        public double Baseline;

        /// <summary>The d-pad's side for this screen and touch size, before the room checks: a whole number of UI units divisible by 3 (three whole-unit cells).</summary>
        public static double DpadSideFor(double screenW, double screenH, double scale = 1)
        {
            double raw = Math.Min(screenW * DpadWidthShare, screenH * DpadHeightShare) * scale;
            return 3 * Math.Max(1, Math.Round(raw / 3));
        }

        /// <param name="screenW">Screen width in UI units.</param>
        /// <param name="screenH">Screen height in UI units.</param>
        /// <param name="safeLeft">Safe-area insets from each screen edge, in UI units.</param>
        /// <param name="scale">The touch size setting (0.75 to 1.5): controls grow up and inward from their corner.</param>
        public static TouchLayout For(double screenW, double screenH, double safeLeft = 0, double safeRight = 0, double safeTop = 0, double safeBottom = 0, double scale = 1)
        {
            var t = new TouchLayout();
            t.Baseline = Math.Max(Math.Ceiling(screenH * MinBottom), Math.Ceiling(safeBottom));
            double j = Math.Round(JumpSize * scale), x = Math.Round(DashSize * scale);
            double floor = screenH - t.Baseline; // y of the bottom edges
            double jumpRight = screenW - safeRight - SideMargin;
            t.Jump = new UiBox(jumpRight - j, floor - JumpRaise - j, j, j);
            t.Dash = new UiBox(t.Jump.Left - Gap - x, floor - x, x, x);

            // The d-pad: as big as the screen share asks, but never up into the HUD, and its touch
            // area never closer than ClusterGap to dash.
            double d = DpadSideFor(screenW, screenH, scale);
            double left = safeLeft + SideMargin;
            double top = Math.Max(HudReserve, safeTop) + 1;
            double maxByHeight = (floor - top) / (1 + DpadTouchMargin);
            double maxByWidth = (t.Dash.Left - ClusterGap - 1 - left) / (1 + DpadTouchMargin);
            double room = 3 * Math.Floor(Math.Min(maxByHeight, maxByWidth) / 3);
            if (d > room) d = Math.Max(3, room);
            t.Dpad = new UiBox(left, floor - d, d, d);
            double m = Math.Round(d * DpadTouchMargin);
            double touchLeft = Math.Max(safeLeft, left - m);
            t.DpadTouch = new UiBox(touchLeft, t.Dpad.Top - m, t.Dpad.Right + m - touchLeft, d + m);
            return t;
        }

        /// <summary>The UI's whole-number zoom for a screen of w x h device pixels (ScreenRouter uses it): the largest that fits viewW x viewH.</summary>
        public static int Zoom(int w, int h, int viewW = 480, int viewH = 270) => Math.Max(1, Math.Min(w / viewW, h / viewH));

        /// <summary>The layout for a screen in device pixels, with its safe area's insets in device pixels (left, right, top, bottom).</summary>
        public static TouchLayout ForDevice(int w, int h, double insetL = 0, double insetR = 0, double insetT = 0, double insetB = 0, double scale = 1)
        {
            double z = Zoom(w, h);
            return For(w / z, h / z, insetL / z, insetR / z, insetT / z, insetB / z, scale);
        }

        /// <summary>The lowest bottom edge of any control, as a share of the screen height above the screen's bottom.</summary>
        public double LowestEdge(double screenH) => (screenH - Math.Max(Math.Max(Dpad.Bottom, DpadTouch.Bottom), Math.Max(Jump.Bottom, Dash.Bottom))) / screenH;
    }
}

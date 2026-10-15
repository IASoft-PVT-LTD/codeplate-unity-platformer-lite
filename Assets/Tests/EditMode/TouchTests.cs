using Game.Core;
using NUnit.Framework;
using static Game.Core.InputFrame;

namespace Game.Tests
{
    /// <summary>
    /// The on-screen controls without a screen: the d-pad's direction maths and where the
    /// controls go on phones and tablets of every shape (16:9, 20:9, 19.5:9, 4:3, 16:10).
    /// </summary>
    public class TouchTests
    {
        // ------------------------------------------------------------------ d-pad

        [Test]
        public void TheDeadZoneHoldsNothing()
        {
            Assert.AreEqual(0.18, Dpad.DeadZone);
            foreach (var axes in new[] { DpadAxes.XY, DpadAxes.X, DpadAxes.Y, DpadAxes.XDown })
            {
                Assert.AreEqual(0, Dpad.Directions(0, 0, axes), $"{axes}: centre");
                Assert.AreEqual(0, Dpad.Directions(0.17, 0, axes), $"{axes}: just inside, right");
                Assert.AreEqual(0, Dpad.Directions(-0.1, 0.1, axes), $"{axes}: inside, low left");
            }
            Assert.AreEqual(Right, Dpad.Directions(0.19, 0), "just outside");
            Assert.AreEqual(Up, Dpad.Directions(0, -0.19));
        }

        [Test]
        public void EightSectorsClockwiseFromRight()
        {
            // Screen y points down: dy > 0 is below the centre.
            Assert.AreEqual(Right, Dpad.Directions(1, 0));
            Assert.AreEqual(Right | Down, Dpad.Directions(0.7, 0.7));
            Assert.AreEqual(Down, Dpad.Directions(0, 1));
            Assert.AreEqual(Down | Left, Dpad.Directions(-0.7, 0.7));
            Assert.AreEqual(Left, Dpad.Directions(-1, 0));
            Assert.AreEqual(Left | Up, Dpad.Directions(-0.7, -0.7));
            Assert.AreEqual(Up, Dpad.Directions(0, -1));
            Assert.AreEqual(Up | Right, Dpad.Directions(0.7, -0.7));
            // The sector borders sit at 22.5° either side of each axis.
            Assert.AreEqual(Right, Dpad.Directions(1, 0.40), "21.8° below level is still right");
            Assert.AreEqual(Right | Down, Dpad.Directions(1, 0.43), "23.3° is the diagonal");
            Assert.AreEqual(Down, Dpad.Directions(0.40, 1), "68.2° is down");
            // Far outside the pad still counts (the finger is captured while it slides).
            Assert.AreEqual(Left, Dpad.Directions(-3, 0.2));
        }

        [Test]
        public void XOnlyIsTheSideOfTheCentre()
        {
            Assert.AreEqual(Right, Dpad.Directions(0.5, 0.9, DpadAxes.X), "low right is still only right");
            Assert.AreEqual(Left, Dpad.Directions(-0.3, -1, DpadAxes.X), "high left is still only left");
            Assert.AreEqual(0, Dpad.Directions(0.1, 1, DpadAxes.X), "straight down presses nothing");
            Assert.AreEqual(0, Dpad.Directions(-0.17, -1, DpadAxes.X));
            Assert.AreEqual(Up, Dpad.Directions(0.9, -0.5, DpadAxes.Y));
            Assert.AreEqual(Left | Right | Down, Dpad.Gives(DpadAxes.XDown));
            Assert.AreEqual(Left | Right, Dpad.Gives(DpadAxes.X));
        }

        [Test]
        public void ConfigNamesTheAxes()
        {
            Assert.AreEqual(DpadAxes.XDown, Dpad.Parse("x_down", out bool ok));
            Assert.IsTrue(ok);
            Assert.AreEqual(DpadAxes.XY, Dpad.Parse("XY", out ok));
            Assert.AreEqual(DpadAxes.X, Dpad.Parse("x", out ok));
            Assert.AreEqual(DpadAxes.Y, Dpad.Parse("y", out ok));
            Dpad.Parse("diagonal", out ok);
            Assert.IsFalse(ok);
            Assert.AreEqual(DpadAxes.XDown, Dpad.Parse(Kit.Config().dpadAxes, out ok), "the shipped game: left, right and down");
            Assert.IsTrue(ok);
        }

        [Test]
        public void XDownNeverPressesUpAndNeedsAClearlyLowThumbForDown()
        {
            // The default pad ("x_down"): up does nothing in this game, down drops through ledges.
            for (int deg = 0; deg < 360; deg += 5)
            {
                double a = deg * System.Math.PI / 180;
                int bits = Dpad.Directions(System.Math.Cos(a), System.Math.Sin(a), DpadAxes.XDown);
                Assert.AreEqual(0, bits & Up, $"{deg}°: never up");
            }
            Assert.AreEqual(Right, Dpad.Directions(0.7, -0.7, DpadAxes.XDown), "up-right is right");
            Assert.AreEqual(Left, Dpad.Directions(-0.1 - 0.1, -1, DpadAxes.XDown), "a thumb rolled up past the centre keeps left");
            Assert.AreEqual(0, Dpad.Directions(0.1, -1, DpadAxes.XDown), "straight up presses nothing");
            Assert.AreEqual(Right, Dpad.Directions(1, 0.5, DpadAxes.XDown), "26.6° low: still only right, so jump isn't a drop");
            Assert.AreEqual(Right | Down, Dpad.Directions(1, 1, DpadAxes.XDown), "45°: the diagonal");
            Assert.AreEqual(Down, Dpad.Directions(0.3, 1, DpadAxes.XDown), "73°: down");
            Assert.AreEqual(Down | Left, Dpad.Directions(-0.6, 1, DpadAxes.XDown));
        }

        [Test]
        public void SlidingTheThumbRoundChangesDirectionWithoutLifting()
        {
            // The same finger's positions as it rolls from left, under the centre, to right.
            int[] seen =
            {
                Dpad.Directions(-1, 0.1, DpadAxes.XDown),
                Dpad.Directions(-0.7, 0.8, DpadAxes.XDown),
                Dpad.Directions(0, 1, DpadAxes.XDown),
                Dpad.Directions(0.7, 0.8, DpadAxes.XDown),
                Dpad.Directions(1, 0.1, DpadAxes.XDown),
            };
            CollectionAssert.AreEqual(new[] { Left, Left | Down, Down, Right | Down, Right }, seen);
        }

        // ------------------------------------------------------------------ layout

        // Real screens in device pixels (landscape), with their safe-area insets (left, right, top, bottom).
        static readonly (string name, int w, int h, int l, int r, int t, int b)[] Screens =
        {
            ("16:9 phone 1920x1080", 1920, 1080, 0, 0, 0, 0),
            ("16:9 phone 1280x720", 1280, 720, 0, 0, 0, 0),
            ("20:9 phone 2400x1080", 2400, 1080, 0, 0, 0, 0),
            ("20:9 phone 2400x1080, camera cutout and gesture bar", 2400, 1080, 132, 0, 0, 48),
            ("20:9 phone 3200x1440, cutout", 3200, 1440, 0, 160, 0, 0),
            ("20:9 phone 1600x720", 1600, 720, 0, 0, 0, 0),
            ("19.5:9 phone 2340x1080, cutout both sides", 2340, 1080, 120, 120, 0, 0),
            ("4:3 tablet 2048x1536", 2048, 1536, 0, 0, 0, 0),
            ("4:3 tablet 1024x768", 1024, 768, 0, 0, 0, 0),
            ("4:3 tablet 2732x2048, home bar", 2732, 2048, 0, 0, 0, 40),
            ("16:10 tablet 2560x1600", 2560, 1600, 0, 0, 0, 0),
        };

        [Test]
        public void EveryControlSitsAtLeastFifteenPercentUpOnEveryScreen()
        {
            foreach (var s in Screens)
            foreach (double scale in new[] { 0.75, 1.0, 1.5 })
            {
                double z = TouchLayout.Zoom(s.w, s.h);
                double w = s.w / z, h = s.h / z;
                var t = TouchLayout.ForDevice(s.w, s.h, s.l, s.r, s.t, s.b, scale);
                string where = $"{s.name} at touch size {scale}";
                foreach (var (name, box) in new[] { ("d-pad", t.Dpad), ("d-pad's touch area", t.DpadTouch), ("jump", t.Jump), ("dash", t.Dash) })
                {
                    double above = h - box.Bottom;
                    Assert.GreaterOrEqual(above, 0.15 * h - 1e-9, $"{where}: {name}'s bottom edge is {above / h:P1} up, under 15%");
                    Assert.GreaterOrEqual(box.Left, s.l / z - 1e-9, $"{where}: {name} is left of the safe area");
                    Assert.LessOrEqual(box.Right, w - s.r / z + 1e-9, $"{where}: {name} is right of the safe area");
                    Assert.LessOrEqual(box.Bottom, h - s.b / z + 1e-9, $"{where}: {name} is under the safe area");
                    Assert.Greater(box.Top, 40, $"{where}: {name} reaches up into the HUD");
                }
                Assert.GreaterOrEqual(t.LowestEdge(h), 0.15 - 1e-9, where);
                Assert.IsFalse(t.Jump.Overlaps(t.Dash), $"{where}: jump and dash overlap");
                Assert.Less(t.Dpad.Right + 40, t.Dash.Left, $"{where}: the clusters are too close");
                Assert.Less(t.DpadTouch.Right + 40, t.Dash.Left, $"{where}: the d-pad's touch area comes too close to dash");
                Assert.IsFalse(t.DpadTouch.Overlaps(t.Jump) || t.DpadTouch.Overlaps(t.Dash), $"{where}: the d-pad's touch area overlaps a button");
            }
        }

        [Test]
        public void TheDpadIsAboutAQuarterOfTheScreenWidth()
        {
            // 25% of the width, at most 48% of the height, cells of whole units.
            var phone16 = TouchLayout.ForDevice(1920, 1080);   // 480x270 UI units
            var phone20 = TouchLayout.ForDevice(2400, 1080);   // 600x270
            var tablet = TouchLayout.ForDevice(2048, 1536);    // 512x384
            Assert.AreEqual(120, phone16.Dpad.Width, "16:9: 25% of 480 (cells of 40)");
            Assert.AreEqual(129, phone20.Dpad.Width, "20:9: 48% of 270 caps it (25% of 600 would be 150)");
            Assert.AreEqual(129, tablet.Dpad.Width, "4:3: 25% of 512 is 128, to whole cells of 43");
            foreach (var t in new[] { phone16, phone20, tablet })
            {
                Assert.AreEqual(t.Dpad.Width, t.Dpad.Height, "square");
                Assert.AreEqual(0, t.Dpad.Width % 3, "three whole-unit cells");
                Assert.Greater(t.Dpad.Width, 1.5 * 72, "well over a thumb's width");
            }
            Assert.GreaterOrEqual(phone16.Dpad.Width / 480, 0.24);
            Assert.GreaterOrEqual(phone20.Dpad.Width / 600, 0.21);
            Assert.GreaterOrEqual(tablet.Dpad.Width / 512, 0.24);
        }

        [Test]
        public void TheDpadTakesTouchesALittleBeyondItsArt()
        {
            foreach (var s in Screens)
            foreach (double scale in new[] { 0.75, 1.0, 1.5 })
            {
                var t = TouchLayout.ForDevice(s.w, s.h, s.l, s.r, s.t, s.b, scale);
                string where = $"{s.name} at touch size {scale}";
                Assert.LessOrEqual(t.DpadTouch.Left, t.Dpad.Left, where);
                Assert.LessOrEqual(t.DpadTouch.Top, t.Dpad.Top - 0.1 * t.Dpad.Height, $"{where}: reaches above the art");
                Assert.GreaterOrEqual(t.DpadTouch.Right, t.Dpad.Right + 0.1 * t.Dpad.Width, $"{where}: reaches right of the art");
                Assert.AreEqual(t.Dpad.Bottom, t.DpadTouch.Bottom, 1e-9, $"{where}: never lower than the art (the 15% rule)");
                Assert.GreaterOrEqual(t.DpadTouch.Width * t.DpadTouch.Height, 1.2 * t.Dpad.Width * t.Dpad.Height, where);
            }
        }

        [Test]
        public void TheTouchSizeSettingStillScalesTheDpad()
        {
            var small = TouchLayout.ForDevice(1920, 1080, scale: 0.75);
            var normal = TouchLayout.ForDevice(1920, 1080);
            var big = TouchLayout.ForDevice(1920, 1080, scale: 1.5);
            Assert.Less(small.Dpad.Width, normal.Dpad.Width);
            Assert.Less(normal.Dpad.Width, big.Dpad.Width);
            Assert.AreEqual(0.75 * normal.Dpad.Width, small.Dpad.Width, 3);
            Assert.Greater(big.Dpad.Width, 1.3 * normal.Dpad.Width, "1.5 grows it (capped only by the HUD's room above)");
            Assert.AreEqual(normal.Dpad.Bottom, big.Dpad.Bottom, 1e-9, "it grows up and inward, never down");
            Assert.AreEqual(normal.Dpad.Left, big.Dpad.Left, 1e-9);
        }

        [Test]
        public void TheLayoutScalesWithTheScreenNotFixedPixels()
        {
            // The same share of the screen on a small and a big screen of one shape.
            var small = TouchLayout.ForDevice(1920, 1080);
            var big = TouchLayout.ForDevice(3840, 2160);
            Assert.AreEqual(small.LowestEdge(270), big.LowestEdge(270), 1e-9, "1920x1080 and 3840x2160 are both 480x270 UI units");
            var phone20 = TouchLayout.ForDevice(3200, 1440);
            Assert.AreEqual(System.Math.Ceiling(0.15 * 288), phone20.Baseline, "3200x1440 is 640x288 UI units: 15% of 288, rounded up");
            // A 4:3 tablet's controls sit higher in UI units because its screen is taller.
            var tablet = TouchLayout.ForDevice(2048, 1536);
            var phone = TouchLayout.ForDevice(1920, 1080);
            Assert.Greater(tablet.Baseline, phone.Baseline);
            Assert.AreEqual(System.Math.Ceiling(0.15 * 384), tablet.Baseline, "2048x1536 is 512x384 UI units");
            Assert.AreEqual(System.Math.Ceiling(0.15 * 270), phone.Baseline);
        }

        [Test]
        public void TheUiZoomIsTheBiggestWholeNumberThatFits()
        {
            Assert.AreEqual(4, TouchLayout.Zoom(1920, 1080));
            Assert.AreEqual(4, TouchLayout.Zoom(2400, 1080), "20:9: 600x270 UI units");
            Assert.AreEqual(4, TouchLayout.Zoom(2048, 1536), "4:3: 512x384");
            Assert.AreEqual(1, TouchLayout.Zoom(400, 200), "never below 1");
        }
    }
}

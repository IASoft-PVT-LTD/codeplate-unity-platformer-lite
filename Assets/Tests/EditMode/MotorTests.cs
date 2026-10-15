using System;
using Game.Core;
using NUnit.Framework;
using static Game.Core.InputFrame;

namespace Game.Tests
{
    /// <summary>Player movement against config.json: every number in docs/movement.md does what it says.</summary>
    public class MotorTests
    {
        static readonly string[] Field =
        {
            "................................................",
            "................................................",
            "................................................",
            "................................................",
            "................................................",
            "................................................",
            "................................................",
            "................................................",
            "................................................",
            "....P...........................................",
            "################################################",
        };

        static World Flat(GameConfig cfg = null)
        {
            var w = Kit.Room(cfg ?? Kit.Moves(), Field);
            w.Settle();
            return w;
        }

        /// <summary>Highest rise (tiles) of the feet while stepping `ticks` with the given inputs.</summary>
        static double Rise(World w, Func<int, int> input, int ticks)
        {
            double start = w.Player.Body.Pos.Y, top = start;
            for (int t = 0; t < ticks; t++)
            {
                w.Step(input(t));
                top = Math.Min(top, w.Player.Body.Pos.Y);
            }
            return start - top;
        }

        [Test]
        public void ReachesRunSpeedWithinAccelTime()
        {
            var w = Flat();
            var cfg = w.Config;
            w.Run(Right, GameConfig.Ticks(cfg.groundAccelTime) + 1);
            Assert.AreEqual(cfg.runSpeed, w.Player.Body.Vel.X, 1e-9);
        }

        [Test]
        public void StopsWithinDecelTime()
        {
            var w = Flat();
            w.Run(Right, 30);
            w.Run(0, GameConfig.Ticks(w.Config.groundDecelTime) + 1);
            Assert.AreEqual(0, w.Player.Body.Vel.X, 1e-9);
        }

        [Test]
        public void HeldJumpPeaksAtJumpHeight()
        {
            var w = Flat();
            double rise = Rise(w, t => Jump, 90);
            Assert.AreEqual(w.Config.jumpHeight, rise, 0.05, "a held jump should rise jumpHeight tiles");
        }

        [Test]
        public void TappedJumpIsShort()
        {
            var w = Flat();
            double rise = Rise(w, t => t == 0 ? Jump : 0, 90);
            Assert.That(rise, Is.InRange(w.Config.jumpMinHeight, w.Config.jumpMinHeight + 0.5), "a tap should rise about jumpMinHeight");
        }

        [Test]
        public void ApexTimeMatchesConfig()
        {
            var w = Flat();
            double best = double.MaxValue;
            int apex = 0;
            for (int t = 0; t < 90; t++)
            {
                w.Step(Jump);
                if (w.Player.Body.Pos.Y < best)
                {
                    best = w.Player.Body.Pos.Y;
                    apex = t + 1;
                }
            }
            // Apex hang stretches the top a little, so allow a few ticks past the configured time.
            Assert.That(apex, Is.InRange(GameConfig.Ticks(w.Config.jumpTimeToApex) - 1, GameConfig.Ticks(w.Config.jumpTimeToApex) + 6));
        }

        [Test]
        public void FallSpeedIsCapped()
        {
            var cfg = Kit.Moves();
            var rows = new string[40];
            for (int i = 0; i < 39; i++) rows[i] = "....";
            rows[0] = ".P..";
            rows[39] = "####";
            var w = Kit.Room(cfg, rows);
            w.Run(0, 50);
            Assert.AreEqual(cfg.maxFallSpeed, w.Player.Body.Vel.Y, 1e-9);
            w.Run(Down, 5);
            Assert.AreEqual(cfg.fastFallSpeed, w.Player.Body.Vel.Y, 1e-9, "holding down falls faster");
        }

        // A ledge at x=10 with a long drop, to time coyote jumps.
        static readonly string[] Ledge =
        {
            "..............................",
            "..............................",
            "..............................",
            "..............................",
            "..............................",
            "....P.........................",
            "##########....................",
            "..............................",
            "..............................",
            "..............................",
            "..............................",
            "..............................",
            "..............................",
            "..............................",
            "##############################",
        };

        /// <summary>Walks right off the ledge, presses jump `late` ticks after leaving the ground. Did it jump?</summary>
        static bool CoyoteJump(int late)
        {
            var w = Kit.Room(Ledge);
            w.Settle();
            int left = -1;
            for (int t = 0; t < 120; t++)
            {
                bool jump = left >= 0 && t - left == late;
                var ev = w.Step(Right | (jump ? Jump : 0));
                if (ev.Has(SimEventType.Jump)) return true;
                if (left < 0 && !w.Player.IsGrounded) left = t;
                if (left >= 0 && t - left > late) return false;
            }
            return false;
        }

        [Test]
        public void CoyoteTimeAllowsALateJump()
        {
            int coyote = GameConfig.Ticks(Kit.Config().coyoteTime);
            Assert.IsTrue(CoyoteJump(coyote - 1), "a jump just after leaving the ledge should work");
            Assert.IsFalse(CoyoteJump(coyote + 2), "a jump well after coyote time shouldn't");
        }

        [Test]
        public void BufferedJumpFiresOnLanding()
        {
            var w = Kit.Room(Field);
            w.Settle();
            w.Run(Jump, 12);
            // Falling now: press jump a few ticks before landing (the floor is at y = 10).
            int buffer = GameConfig.Ticks(w.Config.jumpBufferTime);
            int pressed = -1, jumped = -1, landed = -1;
            for (int t = 0; t < 120 && jumped < 0; t++)
            {
                if (pressed < 0 && w.Player.Body.Vel.Y > 0 && w.Player.Body.Pos.Y > 10 - 0.9) pressed = t;
                var ev = w.Step(pressed >= 0 ? Jump : 0);
                if (ev.Has(SimEventType.Land)) landed = t;
                if (ev.Has(SimEventType.Jump)) jumped = t;
            }
            Assert.GreaterOrEqual(pressed, 0, "the test should press jump while falling");
            Assert.GreaterOrEqual(landed, pressed, "pressed before landing");
            Assert.GreaterOrEqual(jumped, 0, "the buffered jump should fire");
            Assert.LessOrEqual(jumped - pressed, buffer, "within the buffer window");
            Assert.LessOrEqual(jumped - landed, 1, "right on landing");
        }

        static readonly string[] Ceiling =
        {
            "..........",
            "..........",
            ".....#....",
            "..........",
            "..........",
            "..........",
            "##########",
        };

        [Test]
        public void CornerCorrectionSlidesPastAClippedCorner()
        {
            // The player's head clips the left edge of a ceiling block by 0.15 tiles.
            var w = Kit.Room(Ceiling);
            w.Settle();
            double half = w.Config.playerSize.X * 0.5;
            w.Player.Body.Pos.X = 5 - half + 0.15;
            Assert.Greater(Rise(w, t => Jump, 30), 3.0, "the player should get past the block");
            Assert.Less(w.Player.Body.Pos.X, 5 - half + 0.01, "nudged to the left of the block");
        }

        [Test]
        public void NoCornerCorrectionForADeepOverlap()
        {
            var w = Kit.Room(Ceiling);
            w.Settle();
            double half = w.Config.playerSize.X * 0.5;
            w.Player.Body.Pos.X = 5 - half + 0.4;
            Assert.LessOrEqual(Rise(w, t => Jump, 30), 6 - 3.875 + 1e-6, "the head bumps the block");
        }

        [Test]
        public void NoAirJumpWhenDoubleJumpIsOff()
        {
            var w = Flat(Kit.Moves(doubleJump: false));
            w.Run(Jump, 10);
            w.Run(0, 2);
            var ev = w.Run(Jump, 5);
            Assert.IsFalse(ev.Has(SimEventType.AirJump));
        }

        [Test]
        public void DoubleJumpGivesAirJumpsAndResetsOnLanding()
        {
            var w = Flat(Kit.Moves(doubleJump: true));
            w.Run(Jump, 10);
            w.Run(0, 2);
            Assert.AreEqual(1, w.Run(Jump, 4).Count(SimEventType.AirJump));
            w.Run(0, 2);
            Assert.AreEqual(0, w.Run(Jump, 4).Count(SimEventType.AirJump), "only airJumps per jump");
            w.Settle();
            w.Run(Jump, 10);
            w.Run(0, 2);
            Assert.AreEqual(1, w.Run(Jump, 4).Count(SimEventType.AirJump), "reset after landing");
        }

        static readonly string[] Shaft =
        {
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#........P.#",
            "############",
        };

        [Test]
        public void WallSlideCapsTheFall()
        {
            var cfg = Kit.Moves(wallJump: true);
            var w = Kit.Room(cfg, Shaft);
            w.Settle();
            w.Run(Jump | Right, 30);
            w.Run(Right, 20);
            Assert.AreEqual(Player.State.WallSlide, w.Player.Current);
            Assert.LessOrEqual(w.Player.Body.Vel.Y, cfg.wallSlideSpeed + 1e-9);
        }

        [Test]
        public void WallJumpPushesAwayAndLocksSteering()
        {
            var cfg = Kit.Moves(wallJump: true);
            var w = Kit.Room(cfg, Shaft);
            w.Settle();
            w.Run(Jump | Right, 30);
            w.Run(Right, 4);
            var ev = w.Run(Right | Jump, 1);
            Assert.IsTrue(ev.Has(SimEventType.WallJump));
            Assert.Less(w.Player.Body.Vel.X, 0, "pushed away from the right wall");
            w.Run(Right | Jump, GameConfig.Ticks(cfg.wallJumpLockTime) - 2);
            Assert.Less(w.Player.Body.Vel.X, 0, "steering is locked for wallJumpLockTime");
        }

        [Test]
        public void NoWallSlideWhenWallJumpIsOff()
        {
            var w = Kit.Room(Kit.Moves(wallJump: false), Shaft);
            w.Settle();
            w.Run(Jump | Right, 30);
            w.Run(Right, 20);
            Assert.AreNotEqual(Player.State.WallSlide, w.Player.Current);
        }

        [Test]
        public void DashCoversDashDistance()
        {
            var w = Flat(Kit.Moves(dash: true));
            double x0 = w.Player.Body.Pos.X;
            var ev = w.Run(Dash, 1);
            Assert.IsTrue(ev.Has(SimEventType.Dash));
            w.Run(0, GameConfig.Ticks(w.Config.dashTime) - 1);
            Assert.AreEqual(w.Config.dashDistance, w.Player.Body.Pos.X - x0, 0.02);
        }

        [Test]
        public void DashHasNoGravityAndOneAirDash()
        {
            var w = Flat(Kit.Moves(dash: true)); // one air dash
            w.Run(Jump, 20);
            w.Run(0, 1);
            double y = w.Player.Body.Pos.Y;
            Assert.IsTrue(w.Run(Dash | Right, 1).Has(SimEventType.Dash));
            w.Run(Right, GameConfig.Ticks(w.Config.dashTime) - 1);
            Assert.AreEqual(y, w.Player.Body.Pos.Y, 1e-9, "a dash keeps its height");
            w.Run(0, GameConfig.Ticks(w.Config.dashCooldown) + 1);
            Assert.IsFalse(w.Run(Dash, 1).Has(SimEventType.Dash), "only airDashes in the air");
            w.Settle();
            w.Run(0, GameConfig.Ticks(w.Config.dashCooldown) + 1);
            Assert.IsTrue(w.Run(Dash, 1).Has(SimEventType.Dash), "dashes reset on landing");
        }

        [Test]
        public void DashDisabledDoesNothing()
        {
            var w = Flat(Kit.Moves(dash: false));
            Assert.IsFalse(w.Run(Dash, 5).Has(SimEventType.Dash));
        }

        [Test]
        public void DashBreaksDashWallsButNotGround()
        {
            var w = Kit.Room(
                "............",
                "............",
                "..P..dd..#..",
                "############");
            w.Settle();
            var ev = w.Run(Right | Dash, 1);
            ev.AddRange(w.Run(Right, 12));
            Assert.AreEqual(2, ev.Count(SimEventType.WallBreak), "both dash-wall cells break");
            Assert.IsFalse(w.Grid.IsSolid(5, 2));
            Assert.IsFalse(w.Grid.IsSolid(6, 2));
            w.Run(Right | Dash, 30);
            Assert.IsTrue(w.Grid.IsSolid(9, 2), "a plain solid tile stops a dash");
            Assert.Less(w.Player.Body.Right, 9.0001);
        }

        [Test]
        public void WalkingIntoADashWallIsBlocked()
        {
            var w = Kit.Room(
                "............",
                "..P..d......",
                "############");
            w.Settle();
            w.Run(Right, 60);
            Assert.IsTrue(w.Grid.IsSolid(5, 1));
            Assert.Less(w.Player.Body.Right, 5.0001);
        }
    }
}

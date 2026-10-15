using Game.Core;
using NUnit.Framework;
using static Game.Core.InputFrame;

namespace Game.Tests
{
    /// <summary>Collision edge cases, blocks and level rules, on small rooms.</summary>
    public class WorldTests
    {
        [Test]
        public void CantTunnelThroughAThinFloorAtFastFall()
        {
            var rows = new string[30];
            for (int i = 0; i < 30; i++) rows[i] = "....";
            rows[0] = ".P..";
            rows[25] = "####";
            var w = Kit.Room(rows);
            w.Run(Down, 120);
            Assert.AreEqual(25.0, w.Player.Body.Pos.Y, 1e-9);
        }

        [Test]
        public void OneWayLedgeLetsYouUpAndHoldsYou()
        {
            var w = Kit.Room(
                "..........",
                "..........",
                "..........",
                "...====...",
                "..........",
                "...P......",
                "##########");
            w.Settle();
            w.Run(Jump, 40);
            w.Run(0, 40);
            Assert.AreEqual(3.0, w.Player.Body.Pos.Y, 1e-9, "jumped up through, landed on top");
            w.Run(Down | Jump, 2);
            w.Run(0, 40);
            Assert.AreEqual(6.0, w.Player.Body.Pos.Y, 1e-9, "down + jump drops through");
        }

        [Test]
        public void MovingPlatformCarriesThePlayer()
        {
            var lvl = LevelData.FromRows(Kit.Content.Legend,
                "....................",
                "....................",
                "..P.................",
                "..-.................",
                "....................",
                "....................",
                "####################").At(2, 3, "dx=8 speed=2 wait=0");
            var w = new World(lvl, Kit.Moves(), Kit.Enemies);
            w.Run(0, 30);
            Assert.IsTrue(w.Player.IsGrounded);
            double x0 = w.Player.Body.Pos.X;
            w.Run(0, 60);
            Assert.AreEqual(x0 + 2.0, w.Player.Body.Pos.X, 0.05, "carried 2 tiles in 1 s");
        }

        [Test]
        public void CrumbleBreaksAndComesBack()
        {
            var w = Kit.Room(
                "..........",
                "...P......",
                "...x......",
                "..........",
                "..........",
                "##########");
            w.Settle();
            var cfg = w.Config;
            var ev = w.Run(0, GameConfig.Ticks(cfg.crumbleDelay) + 2);
            Assert.IsTrue(ev.Has(SimEventType.Crumble));
            Assert.IsFalse(w.Grid.IsSolid(3, 2));
            w.Run(0, 30);
            Assert.AreEqual(5.0, w.Player.Body.Pos.Y, 1e-9, "fell through");
            w.Run(Right, 20);
            w.Run(0, GameConfig.Ticks(cfg.crumbleRespawn));
            Assert.IsTrue(w.Grid.IsSolid(3, 2), "back after crumbleRespawn");
        }

        [Test]
        public void SpringLaunchesToSpringHeight()
        {
            var rows = new string[16];
            for (int i = 0; i < 16; i++) rows[i] = "........";
            rows[13] = "..Ps....";
            rows[14] = "########";
            rows[15] = "########";
            var w = Kit.Room(rows);
            w.Settle();
            double start = w.Player.Body.Pos.Y, top = start;
            bool sprung = false;
            for (int t = 0; t < 120; t++)
            {
                sprung |= w.Step(t < 8 ? Right : 0).Has(SimEventType.Spring);
                top = System.Math.Min(top, w.Player.Body.Pos.Y);
            }
            Assert.IsTrue(sprung);
            Assert.AreEqual(w.Config.springHeight, 13.5 - top, 0.1, "rises springHeight from the spring's top");
        }

        [Test]
        public void SpikesHurtAndPitsKill()
        {
            var w = Kit.Room(
                "..........",
                "..P..^....",
                "#######..#",
                "#######..#");
            w.Settle();
            var ev = w.Run(Right, 25);
            Assert.IsTrue(ev.Has(SimEventType.Hurt), "spikes hurt");
            Assert.AreEqual(w.Config.maxHealth - 1, w.Player.Health);
            for (int t = 0; t < 400 && !ev.Has(SimEventType.Die); t++) ev = w.Run(Right, 1);
            Assert.IsTrue(ev.Has(SimEventType.Die), "the pit kills");
            ev = w.Run(0, GameConfig.Ticks(w.Config.respawnDelay) + 1);
            Assert.IsTrue(ev.Has(SimEventType.Respawn));
            Assert.AreEqual(1, w.Deaths);
            Assert.AreEqual(w.Config.maxHealth, w.Player.Health, "respawn with full health");
        }

        [Test]
        public void PickupsCountOnceAndGemsSurviveDeath()
        {
            var w = Kit.Room(
                "....................",
                "..P.cc*h.C...^^^^^^^",
                "####################");
            w.Settle();
            w.Player.Health = 1;
            var ev = w.Run(Right, 55);
            Assert.AreEqual(2, w.Coins);
            Assert.IsTrue(w.Gems[0]);
            Assert.AreEqual(1, ev.Count(SimEventType.Heart));
            Assert.IsTrue(ev.Has(SimEventType.Checkpoint));
            ev = w.Run(Right, 200);
            Assert.IsTrue(ev.Has(SimEventType.Die));
            Assert.AreEqual(9.5, w.Checkpoint.X, 1e-9);
            Assert.IsTrue(w.Gems[0], "a found gem stays found after dying");
            Assert.AreEqual(2, w.Coins);
        }

        [Test]
        public void OneHitModeWithMaxHealthOne()
        {
            var cfg = Kit.Moves();
            cfg.maxHealth = 1;
            var w = Kit.Room(cfg,
                "..........",
                "..P..^....",
                "##########");
            w.Settle();
            var ev = w.Run(Right, 30);
            Assert.IsTrue(ev.Has(SimEventType.Die));
        }

        [Test]
        public void GoalFinishesTheLevel()
        {
            var w = Kit.Room(
                "..........",
                "..........",
                "..P....G..",
                "##########");
            w.Settle();
            Assert.IsTrue(w.GoalOpen);
            var ev = w.Run(Right, 60);
            Assert.IsTrue(ev.Has(SimEventType.Goal));
            Assert.IsTrue(w.Finished);
            Assert.AreEqual(0, w.Step(Right).Count, "a finished world doesn't step");
        }
    }
}

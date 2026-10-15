using Game.Core;
using NUnit.Framework;
using static Game.Core.InputFrame;

namespace Game.Tests
{
    /// <summary>The boss's state machine, and the boss-gated goal.</summary>
    public class BossTests
    {
        static World Arena(string type, params string[] rows)
        {
            var lvl = LevelData.FromRows(Kit.Content.Legend, rows);
            for (int y = 0; y < rows.Length; y++)
            {
                int x = rows[y].IndexOf('B');
                if (x >= 0) lvl.At(x, y, "type=" + type);
            }
            return new World(lvl, Kit.Moves(), Kit.Enemies);
        }

        /// <summary>Drops the player onto the boss from just above it; returns the events until it lands or bounces.</summary>
        static System.Collections.Generic.List<SimEvent> DropOn(World w, Boss b)
        {
            w.Player.Body.Pos = new V2d(b.Pos.X, b.Pos.Y - b.Size.Y - 1.2);
            w.Player.Body.Vel = V2d.Zero;
            var all = new System.Collections.Generic.List<SimEvent>();
            for (int t = 0; t < 30; t++)
            {
                var ev = w.Step(0);
                all.AddRange(ev);
                if (ev.Has(SimEventType.BossHit) || ev.Has(SimEventType.Bump) || ev.Has(SimEventType.Hurt)) break;
            }
            return all;
        }

        static readonly string[] BeetleLedgeRoom =
        {
            "#.........................#",
            "#.........................#",
            "#..P......................#",
            "#.====....................#",
            "#.........................#",
            "#.........................#",
            "#............B.........G..#",
            "###########################",
        };

        [Test]
        public void BeetleWakesChargesAndIsStunnedByTheWall()
        {
            var w = Arena("beetle", BeetleLedgeRoom);
            var beetle = w.Actor<Beetle>();
            var ev = w.Run(0, 300);
            Assert.IsTrue(ev.Has(SimEventType.BossWake));
            Assert.IsTrue(ev.Has(SimEventType.BossTelegraph), "it warns before charging");
            Assert.IsTrue(ev.Has(SimEventType.BossSlam), "it slams into the wall");
            Assert.IsFalse(ev.Has(SimEventType.Hurt), "the player on the ledge is safe");
            Assert.AreSame(beetle, w.ActiveBoss);
        }

        [Test]
        public void BeetleShrugsOffStompsUnlessStunned()
        {
            var w = Arena("beetle", BeetleLedgeRoom);
            var beetle = w.Actor<Beetle>();
            w.Run(0, 5);
            var ev = DropOn(w, beetle);
            Assert.IsTrue(ev.Has(SimEventType.Bump), "a stomp on its shell bounces off");
            Assert.AreEqual(beetle.MaxHealth, beetle.Health);
        }

        [Test]
        public void ThreeStompsWhileStunnedDefeatItAndOpenTheGoal()
        {
            var w = Arena("beetle", BeetleLedgeRoom);
            var beetle = w.Actor<Beetle>();
            Assert.IsFalse(w.GoalOpen, "the goal is shut while a boss lives");
            bool opened = false;
            for (int hit = 0; hit < beetle.MaxHealth; hit++)
            {
                beetle.Current = Beetle.State.Stunned;
                var ev = DropOn(w, beetle);
                Assert.IsTrue(ev.Has(SimEventType.BossHit), $"hit {hit + 1} lands");
                opened |= ev.Has(SimEventType.GoalOpen);
                w.Run(0, 5);
            }
            Assert.IsTrue(beetle.Defeated);
            Assert.IsTrue(opened && w.GoalOpen);
            Assert.IsNull(w.ActiveBoss);
        }

        [Test]
        public void ChargeHurtsAPlayerInTheWay()
        {
            var w = Arena("beetle",
                "#.........................#",
                "#.........................#",
                "#....P........B...........#",
                "###########################");
            var ev = w.Run(0, 200);
            Assert.IsTrue(ev.Has(SimEventType.Hurt));
        }

        [Test]
        public void BossResetsWhenThePlayerRespawns()
        {
            var w = Arena("beetle", BeetleLedgeRoom);
            var beetle = w.Actor<Beetle>();
            w.Run(0, 5);
            beetle.Current = Beetle.State.Stunned;
            Assert.IsTrue(DropOn(w, beetle).Has(SimEventType.BossHit));
            Assert.AreEqual(beetle.MaxHealth - 1, beetle.Health);
            w.Player.Body.Pos = new V2d(20.5, 30); // into the pit below the map
            var ev = w.Run(0, 120);
            Assert.IsTrue(ev.Has(SimEventType.Respawn));
            Assert.AreEqual(beetle.MaxHealth, beetle.Health, "full health again");
            Assert.AreEqual(beetle.Origin.X, beetle.Pos.X, 1e-9, "back at its start");
        }

        [Test]
        public void GoalStaysShutUntilTheBossIsDefeated()
        {
            var w = Arena("beetle",
                "##############################",
                "#............................#",
                "#..P...G.....................#",
                "##########################B..#",
                "##############################");
            // The beetle sits out of reach inside the floor, so it can never be beaten.
            var ev = w.Run(Right, 120);
            Assert.IsFalse(ev.Has(SimEventType.Goal));
            Assert.IsFalse(w.Finished);
        }
    }
}

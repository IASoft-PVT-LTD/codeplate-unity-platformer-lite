using System;
using Game.Core;
using NUnit.Framework;
using static Game.Core.InputFrame;

namespace Game.Tests
{
    /// <summary>Enemies on small rooms: walking, turning, stomps, hurts, shots.</summary>
    public class ActorTests
    {
        [Test]
        public void WalkerTurnsAtAWall()
        {
            var w = Kit.Room(
                "P.........",
                "..#..w....",
                "##########");
            var walker = w.Actor<Walker>();
            w.Run(0, 120);
            Assert.AreEqual(1, walker.Facing, "turned around at the block");
        }

        [Test]
        public void WalkerTurnsAtALedge()
        {
            var w = Kit.Room(
                "P.............",
                "........w.....",
                "#....######...",
                "#.............");
            var walker = w.Actor<Walker>();
            double minX = double.MaxValue;
            for (int i = 0; i < 400; i++)
            {
                w.Step(0);
                minX = Math.Min(minX, walker.Pos.X);
            }
            Assert.IsTrue(walker.Active, "it never walked off");
            Assert.GreaterOrEqual(minX, 5.0, "stays on its platform");
            Assert.AreEqual(2.0, walker.Pos.Y, 1e-9);
        }

        [Test]
        public void StompDefeatsAWalkerAndBounces()
        {
            var w = Kit.Room(
                "..........",
                "..........",
                "..........",
                "....P.....",
                "..........",
                "..........",
                "....w.....",
                "##########");
            var walker = w.Actor<Walker>();
            // Hover the walker in place so the stomp lands.
            walker.Params["speed"] = "0";
            walker.Setup(w);
            var ev = w.Run(0, 40);
            Assert.IsTrue(ev.Has(SimEventType.Stomp));
            Assert.IsFalse(ev.Has(SimEventType.Hurt));
            w.Run(0, 30);
            Assert.IsFalse(walker.Active, "squashed walkers disappear");
        }

        [Test]
        public void SideTouchHurtsAndGivesInvulnerability()
        {
            var w = Kit.Room(
                "............",
                "..P....w....",
                "############");
            w.Settle();
            var ev = w.Run(Right, 60);
            Assert.AreEqual(1, ev.Count(SimEventType.Hurt), "one hit, then invulnerable");
            Assert.IsTrue(w.Player.IsInvulnerable);
            Assert.AreEqual(w.Config.maxHealth - 1, w.Player.Health);
        }

        [Test]
        public void StompingASpikerHurts()
        {
            var w = Kit.Room(
                "..........",
                "....P.....",
                "..........",
                "..........",
                "....k.....",
                "##########");
            var spiker = w.Actor<Spiker>();
            spiker.Params["speed"] = "0";
            spiker.Setup(w);
            var ev = w.Run(0, 40);
            Assert.IsTrue(ev.Has(SimEventType.Hurt));
            Assert.IsFalse(ev.Has(SimEventType.Stomp));
            Assert.IsTrue(spiker.Active);
        }

        [Test]
        public void FlyerPathIsPeriodicAndDeterministic()
        {
            string[] room =
            {
                "P...................",
                "....................",
                "..........f.........",
                "....................",
                "....................",
                "####################",
            };
            var a = Kit.Room(room);
            var b = Kit.Room(room);
            var fa = a.Actor<Flyer>();
            var fb = b.Actor<Flyer>();
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int t = 0; t < 600; t++)
            {
                a.Step(0);
                b.Step(0);
                Assert.AreEqual(fa.Pos, fb.Pos, "same inputs, same flight");
                minY = Math.Min(minY, fa.Pos.Y);
                maxY = Math.Max(maxY, fa.Pos.Y);
            }
            Assert.AreEqual(Kit.Enemies.flyer.amplitude, maxY - minY, 0.05, "bobs by its amplitude");
        }

        [Test]
        public void ShooterFiresOnItsTimerAndShotsHurt()
        {
            var lvl = LevelData.FromRows(Kit.Content.Legend,
                "..................",
                "..P..........t....",
                "##################").At(13, 1, "dir=-1 interval=1 delay=0.5");
            var w = new World(lvl, Kit.Moves(), Kit.Enemies);
            var ev = w.Run(0, 30);
            Assert.AreEqual(1, ev.Count(SimEventType.Shoot), "first shot after `delay`");
            ev = w.Run(0, 60);
            Assert.AreEqual(1, ev.Count(SimEventType.Shoot), "then one every `interval`");
            ev = w.Run(0, 240);
            Assert.IsTrue(ev.Has(SimEventType.Hurt), "a shot reaches the player");
        }

        [Test]
        public void ShotsStopAtWalls()
        {
            var lvl = LevelData.FromRows(Kit.Content.Legend,
                "P.................",
                "......#......t....",
                "##################").At(13, 1, "dir=-1 interval=5 delay=0.1");
            var w = new World(lvl, Kit.Moves(), Kit.Enemies);
            w.Run(0, 10);
            Assert.IsTrue(w.Actors.Exists(a => a is Projectile), "fired");
            w.Run(0, 150);
            Assert.IsFalse(w.Actors.Exists(a => a is Projectile), "the shot vanished at the wall");
        }

        [Test]
        public void ShooterCanBeStomped()
        {
            var w = Kit.Room(
                "..........",
                "....P.....",
                "..........",
                "..........",
                "....t.....",
                "##########");
            var ev = w.Run(0, 40);
            Assert.IsTrue(ev.Has(SimEventType.Stomp));
            w.Run(0, 30);
            Assert.IsFalse(w.Actor<Shooter>().Active);
        }
    }
}

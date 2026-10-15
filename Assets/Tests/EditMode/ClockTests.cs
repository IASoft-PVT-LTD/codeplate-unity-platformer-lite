using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using static Game.Core.InputFrame;

namespace Game.Tests
{
    /// <summary>
    /// The level clock (docs/recipes/level-clock.md): a level's `time_limit:` counts down, clock
    /// pickups add to it, and time up costs a heart and sends the player back to the last
    /// checkpoint with timeUpRefill of the limit (all of it back at the start). A level without a
    /// limit has no countdown. The HUD clock's stages.
    /// </summary>
    public class ClockTests
    {
        static GameConfig Cfg(double limit)
        {
            var cfg = Kit.Moves();
            cfg.timeLimit = limit;
            cfg.timeBonus = 10;
            cfg.timeUpRefill = 2.0 / 3.0;
            return cfg;
        }

        /// <summary>Steps idle until the player respawns (or gives up after 600 ticks).</summary>
        static bool WaitForTimeUpAndRespawn(World w, out bool timeUp)
        {
            timeUp = false;
            for (int i = 0; i < 600; i++)
            {
                var ev = w.Step(0);
                timeUp |= ev.Has(SimEventType.TimeUp);
                if (ev.Has(SimEventType.Respawn)) return true;
                if (w.OutOfHearts) return false;
            }
            return false;
        }

        static LevelData Level(string header, out List<string> errors)
        {
            errors = new List<string>();
            var rows = new List<string> { "title: T", "par_time: 20" };
            if (header != null) rows.Add(header);
            rows.Add("---");
            for (int i = 0; i < 15; i++) rows.Add(new string('.', 30));
            rows.Add("P..*.*.*.....................G");
            rows.Add(new string('#', 30));
            return LevelData.Parse(string.Join("\n", rows), Kit.Content.Legend, errors, "t.txt");
        }

        [Test]
        public void TheClockStartsAtTheLimitAndCountsDown()
        {
            var w = Kit.Room(Cfg(120), "P....", "#####");
            Assert.IsTrue(w.TimerOn);
            Assert.AreEqual(120 * 60, w.TicksLeft);
            Assert.AreEqual(120 * 60, w.AllowanceTicks);
            w.Run(0, 60);
            Assert.AreEqual(119 * 60, w.TicksLeft, "one second later");
        }

        [Test]
        public void ALevelsTimeLimitHeaderSetsItsLimit()
        {
            var level = Level("time_limit: 42", out var errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(42, level.TimeLimit);
            var w = new World(level, Cfg(0), Kit.Enemies);
            Assert.IsTrue(w.TimerOn, "the header alone turns the clock on");
            Assert.AreEqual(42 * 60, w.TicksLeft, "the level's own limit, not config.json's");

            Level("time_limit: soon", out errors);
            Assert.That(errors, Has.Some.Contains("t.txt:3: time_limit must be a whole number of seconds"));
        }

        [Test]
        public void ALevelWithoutALimitHasNoCountdown()
        {
            var level = Level(null, out var errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(0, level.TimeLimit);
            var w = new World(level, Cfg(0), Kit.Enemies);
            Assert.IsFalse(w.TimerOn);
            var ev = w.Run(0, 600);
            Assert.IsFalse(ev.Has(SimEventType.TimeUp));
        }

        [Test]
        public void ClockEnabledOffSwitchesEveryCountdownOff()
        {
            var cfg = Cfg(0);
            cfg.clockEnabled = false;
            var w = new World(Level("time_limit: 2", out _), cfg, Kit.Enemies);
            Assert.IsFalse(w.TimerOn);
            Assert.IsFalse(w.Run(0, 600).Has(SimEventType.TimeUp));
        }

        [Test]
        public void AClockPickupAddsTimeBonus()
        {
            var w = Kit.Room(Cfg(120), "Po....", "######");
            var ev = w.Run(Right, 30);
            Assert.IsTrue(ev.Has(SimEventType.Clock), "picked up");
            Assert.Greater(w.TicksLeft, 120 * 60 + 9 * 60 - 40, "about 10 s more");
            Assert.LessOrEqual(w.TicksLeft, 130 * 60);
        }

        [Test]
        public void TimeUpCostsAHeartAndSendsThePlayerBackToTheCheckpointWithTheRefill()
        {
            var w = Kit.Room(Cfg(6), "PC..........", "#############");
            // Walk past the checkpoint, away from it, then wait out the clock.
            w.Run(Right, 20);
            Assert.Greater(w.Checkpoint.X, w.Level.Start.X, "the checkpoint was reached");
            w.Run(Right, 60);
            double farX = w.Player.Body.Pos.X;
            int hearts = w.Player.Health;
            Assert.IsTrue(WaitForTimeUpAndRespawn(w, out bool timeUp), "the player came back");
            Assert.IsTrue(timeUp, "time ran out");
            Assert.AreEqual(hearts - 1, w.Player.Health, "time up cost a heart");
            Assert.Less(w.Player.Body.Pos.X, farX, "back at the checkpoint");
            Assert.AreEqual(w.Checkpoint.X, w.Player.Body.Pos.X, 0.6);
            Assert.AreEqual(4 * 60, w.TicksLeft, "two thirds of the 6 s limit");
        }

        [Test]
        public void TimeUpBeforeAnyCheckpointRefillsTheWholeLimit()
        {
            var w = Kit.Room(Cfg(3), "P...........", "#############");
            int hearts = w.Player.Health;
            Assert.IsTrue(WaitForTimeUpAndRespawn(w, out bool timeUp));
            Assert.IsTrue(timeUp);
            Assert.AreEqual(hearts - 1, w.Player.Health);
            Assert.AreEqual(w.Level.Start.X, w.Player.Body.Pos.X, 1e-9, "back at the start");
            Assert.AreEqual(3 * 60, w.TicksLeft, "the whole limit again: it's a new run from the start");
        }

        [Test]
        public void TimeUpOnTheLastHeartIsOutOfHearts()
        {
            var w = Kit.Room(Cfg(2), "PC..........", "#############");
            w.Run(Right, 20);
            w.Player.Health = 1;
            Assert.IsFalse(WaitForTimeUpAndRespawn(w, out bool timeUp), "no respawn");
            Assert.IsTrue(timeUp);
            Assert.IsTrue(w.OutOfHearts);
            Assert.AreEqual(0, w.Player.Health);
        }

        [Test]
        public void AContinueAfterTimeUpGetsTheRefillNotMore()
        {
            var w = Kit.Room(Cfg(6), "PC..........", "#############");
            w.Run(Right, 20);
            Assert.Greater(w.Checkpoint.X, w.Level.Start.X);
            w.Player.Health = 1;
            for (int i = 0; i < 600 && !w.OutOfHearts; i++) w.Step(0);
            Assert.IsTrue(w.OutOfHearts);
            Assert.AreEqual(0, w.TicksLeft, "time is up");
            Assert.IsTrue(w.ContinueWithExtraHeart());
            Assert.AreEqual(4 * 60, w.TicksLeft, "two thirds of the limit, like any time up at a checkpoint");
            Assert.LessOrEqual(w.TicksLeft, w.AllowanceTicks);
        }

        [Test]
        public void TheClockStopsWhileThePlayerIsDownAndAFallDoesNotRefillIt()
        {
            var w = Kit.Room(Cfg(60),
                "..........",
                "..P.......",
                "####...###");
            w.Settle();
            for (int t = 0; t < 200 && w.Player.Current != Player.State.Dead; t++) w.Step(Right);
            Assert.AreEqual(Player.State.Dead, w.Player.Current, "fell");
            int left = w.TicksLeft;
            w.Run(0, GameConfig.Ticks(w.Config.respawnDelay) + 1);
            Assert.AreNotEqual(Player.State.Dead, w.Player.Current, "back");
            Assert.AreEqual(left - 1, w.TicksLeft, 1, "the clock waited while the player was down and wasn't refilled");
        }

        [Test]
        public void FinishingBeforeTimeUpStopsTheClock()
        {
            var w = Kit.Room(Cfg(5), "PG...", "#####");
            w.Run(Right, 120);
            Assert.IsTrue(w.Finished);
            int left = w.TicksLeft;
            w.Run(0, 60);
            Assert.AreEqual(left, w.TicksLeft);
        }

        [Test]
        public void TheHudClockGoesAmberThenRed()
        {
            var cfg = Kit.Config();
            cfg.timeWarning = 30;
            cfg.timeDanger = 10;
            Assert.AreEqual(LevelClock.Stage.Calm, LevelClock.StageFor(45, cfg));
            Assert.AreEqual(LevelClock.Stage.Calm, LevelClock.StageFor(30.01, cfg), "shows 0:31");
            Assert.AreEqual(LevelClock.Stage.Warning, LevelClock.StageFor(30, cfg), "0:30 is amber");
            Assert.AreEqual(LevelClock.Stage.Warning, LevelClock.StageFor(10.5, cfg), "shows 0:11");
            Assert.AreEqual(LevelClock.Stage.Danger, LevelClock.StageFor(10, cfg), "0:10 or less is red");
            Assert.AreEqual(LevelClock.Stage.Danger, LevelClock.StageFor(0.2, cfg));
            Assert.AreEqual("1:05", LevelClock.Text(64.2));
            Assert.AreEqual("0:10", LevelClock.Text(10));
            Assert.AreEqual("0:01", LevelClock.Text(0.01));
            Assert.AreEqual("0:00", LevelClock.Text(0));
        }

        [Test]
        public void ConfigRejectsABadRefillAndUnknownDpadAxes()
        {
            var errors = new List<string>();
            GameConfig.FromJson("{\"clock\": {\"timeUpRefill\": 1.5}, \"display\": {\"dpadAxes\": \"diagonal\"}}", errors);
            Assert.That(errors, Has.Some.Contains("timeUpRefill"));
            Assert.That(errors, Has.Some.Contains("dpadAxes"));
        }
    }
}

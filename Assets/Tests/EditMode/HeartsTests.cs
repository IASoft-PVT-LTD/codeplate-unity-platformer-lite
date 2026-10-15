using Game.Core;
using NUnit.Framework;
using static Game.Core.InputFrame;

namespace Game.Tests
{
    /// <summary>
    /// Hearts matter: a fall costs a heart like an enemy hit (fallsCostHeart), respawning at the
    /// last checkpoint keeps the hearts left, and losing the last heart fails the level
    /// (outOfHeartsRestarts: OutOfHearts, and the game starts the level again from the beginning;
    /// see GameTests). With outOfHeartsRestarts off it's the classic rule: respawn with full hearts.
    /// </summary>
    public class HeartsTests
    {
        static World PitRoom(GameConfig cfg = null) => Kit.Room(cfg ?? Kit.Moves(),
            "..............",
            "..P..C........",
            "##########...#");

        /// <summary>Runs right until the player dies (at most 300 ticks).</summary>
        static void RunIntoThePit(World w)
        {
            for (int t = 0; t < 300 && w.Player.Current != Player.State.Dead; t++) w.Step(Right);
            Assert.AreEqual(Player.State.Dead, w.Player.Current, "fell into the pit");
        }

        static System.Collections.Generic.List<SimEvent> WaitOutTheRespawn(World w) => w.Run(0, GameConfig.Ticks(w.Config.respawnDelay) + 1);

        [Test]
        public void TheShippedGameUsesTheHeartRules()
        {
            var cfg = Kit.Config();
            Assert.IsTrue(cfg.fallsCostHeart, "config.json: falls cost a heart");
            Assert.IsTrue(cfg.outOfHeartsRestarts, "config.json: out of hearts restarts the level");
            Assert.Greater(cfg.outOfHeartsTime, 0.5);
        }

        [Test]
        public void AFallCostsAHeartAndRespawnKeepsTheRest()
        {
            var w = PitRoom();
            w.Settle();
            Assert.AreEqual(3, w.Player.Health);
            RunIntoThePit(w);
            Assert.AreEqual(2, w.Player.Health, "the fall cost one heart");
            var ev = WaitOutTheRespawn(w);
            Assert.IsTrue(ev.Has(SimEventType.Respawn));
            Assert.AreEqual(2, w.Player.Health, "no refill at the checkpoint");
            Assert.AreEqual(w.Checkpoint.X, w.Player.Body.Pos.X, 1e-9, "at the last checkpoint");
            Assert.Greater(w.Checkpoint.X, w.Level.Start.X);
        }

        [Test]
        public void AFallCostsTheSameAsAnEnemyHit()
        {
            var hit = Kit.Room(
                "..........",
                "..P..^....",
                "##########");
            hit.Settle();
            hit.Run(Right, 25);
            var fall = PitRoom();
            fall.Settle();
            RunIntoThePit(fall);
            Assert.AreEqual(hit.Config.maxHealth - 1, hit.Player.Health);
            Assert.AreEqual(hit.Player.Health, fall.Player.Health, "one heart each");
        }

        [Test]
        public void HeartsLostToHitsStayLostAfterAFall()
        {
            var w = PitRoom();
            w.Settle();
            w.Player.Health = 2; // an earlier hit
            RunIntoThePit(w);
            WaitOutTheRespawn(w);
            Assert.AreEqual(1, w.Player.Health);
            Assert.IsFalse(w.OutOfHearts);
        }

        [Test]
        public void FallingOnTheLastHeartFailsTheLevel()
        {
            var w = PitRoom();
            w.Settle();
            w.Player.Health = 1;
            var events = new System.Collections.Generic.List<SimEvent>();
            for (int t = 0; t < 300 && w.Player.Current != Player.State.Dead; t++) events.AddRange(w.Step(Right));
            Assert.IsTrue(events.Has(SimEventType.Die));
            Assert.IsTrue(events.Has(SimEventType.OutOfHearts), "out of hearts");
            Assert.IsTrue(w.OutOfHearts);
            Assert.AreEqual(0, w.Player.Health);
            var later = w.Run(0, 600);
            Assert.IsFalse(later.Has(SimEventType.Respawn), "a checkpoint doesn't help without hearts");
            Assert.AreEqual(Player.State.Dead, w.Player.Current);
            Assert.IsFalse(w.Finished);
        }

        [Test]
        public void AnEnemyTakingTheLastHeartFailsTheLevel()
        {
            var w = Kit.Room(
                "..........",
                "..P..^....",
                "##########");
            w.Settle();
            w.Player.Health = 1;
            var ev = w.Run(Right, 25);
            Assert.IsTrue(ev.Has(SimEventType.Hurt));
            Assert.IsTrue(ev.Has(SimEventType.OutOfHearts));
            Assert.AreEqual(1, w.Deaths);
            Assert.IsFalse(w.Run(0, 600).Has(SimEventType.Respawn));
        }

        [Test]
        public void WithFallsCostHeartOffAFallOnlySendsYouBack()
        {
            var cfg = Kit.Moves();
            cfg.fallsCostHeart = false;
            var w = PitRoom(cfg);
            w.Settle();
            w.Player.Health = 1;
            RunIntoThePit(w);
            Assert.IsFalse(w.OutOfHearts);
            Assert.AreEqual(1, w.Player.Health, "the fall cost nothing");
            Assert.IsTrue(WaitOutTheRespawn(w).Has(SimEventType.Respawn));
            Assert.AreEqual(w.Checkpoint.X, w.Player.Body.Pos.X, 1e-9);
        }

        [Test]
        public void WithOutOfHeartsRestartsOffTheLastHeartRespawnsWithFullHearts()
        {
            var cfg = Kit.Moves();
            cfg.outOfHeartsRestarts = false;
            var w = PitRoom(cfg);
            w.Settle();
            w.Player.Health = 1;
            var events = new System.Collections.Generic.List<SimEvent>();
            for (int t = 0; t < 300 && w.Player.Current != Player.State.Dead; t++) events.AddRange(w.Step(Right));
            Assert.IsFalse(events.Has(SimEventType.OutOfHearts), "the classic rule never fails the level");
            Assert.IsFalse(w.OutOfHearts);
            Assert.IsTrue(WaitOutTheRespawn(w).Has(SimEventType.Respawn));
            Assert.AreEqual(cfg.maxHealth, w.Player.Health, "back at the checkpoint with full hearts");
            Assert.AreEqual(w.Checkpoint.X, w.Player.Body.Pos.X, 1e-9);
        }

        [Test]
        public void AHeartPickupHealsOneUpToTheMost()
        {
            var w = Kit.Room(
                "............",
                "..P.h..h....",
                "############");
            w.Settle();
            w.Player.Health = 2;
            var ev = w.Run(Right, 60);
            Assert.AreEqual(2, ev.Count(SimEventType.Heart), "both picked up");
            Assert.AreEqual(w.Config.maxHealth, w.Player.Health, "never above maxHealth");
        }

        [Test]
        public void ANewWorldIsAFreshAttempt()
        {
            // What the game does after OutOfHearts: a new World for the level. Nothing carries over.
            var level = LevelData.FromRows(Kit.Content.Legend,
                "..............",
                "..P..C.o......",
                "##########...#");
            var cfg = Kit.Moves();
            cfg.timeLimit = 60;
            var w = new World(level, cfg, Kit.Enemies);
            w.Settle();
            w.Player.Health = 1;
            RunIntoThePit(w);
            Assert.IsTrue(w.OutOfHearts);
            var again = new World(level, cfg, Kit.Enemies);
            Assert.AreEqual(again.Config.maxHealth, again.Player.Health);
            Assert.AreEqual(level.Start.X, again.Checkpoint.X, 1e-9, "no checkpoint reached");
            Assert.AreEqual(again.AllowanceTicks, again.TicksLeft, "the whole time limit");
            Assert.AreEqual(0, again.Coins);
            Assert.IsTrue(again.Actors.Exists(a => a.Kind == "clock" && a.Active), "the clock pickup is back");
        }

        // ------------------------------------------------------------------ continues

        [Test]
        public void AContinueGivesOneHeartAtTheCheckpoint()
        {
            var w = PitRoom();
            w.Settle();
            Assert.IsFalse(w.ContinueWithExtraHeart(), "hearts left: nothing to continue");
            Assert.AreEqual(0, w.ContinuesUsed);
            w.Player.Health = 1;
            RunIntoThePit(w);
            Assert.IsTrue(w.OutOfHearts);
            int coins = w.Coins, deaths = w.Deaths;
            Assert.IsTrue(w.ContinueWithExtraHeart());
            var ev = new System.Collections.Generic.List<SimEvent>(w.LastEvents);
            Assert.IsTrue(ev.Has(SimEventType.Respawn));
            Assert.IsTrue(ev.Has(SimEventType.ExtraHeart));
            Assert.IsFalse(w.OutOfHearts);
            Assert.AreEqual(1, w.Player.Health, "one heart");
            Assert.AreNotEqual(Player.State.Dead, w.Player.Current);
            Assert.AreEqual(w.Checkpoint.X, w.Player.Body.Pos.X, 1e-9, "at the last checkpoint");
            Assert.AreEqual(coins, w.Coins);
            Assert.AreEqual(deaths, w.Deaths);
            Assert.AreEqual(1, w.ContinuesUsed);
        }
    }
}

using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>config.json, enemies.json, worlds.json, the legend, and saved progress.</summary>
    public class ContentTests
    {
        [Test]
        public void ConfigReportsUnknownKeysAndWrongTypes()
        {
            var errors = new List<string>();
            var cfg = GameConfig.FromJson("{\"jump\": {\"jumpHieght\": 4, \"jumpHeight\": \"high\"}, \"run\": {\"runSpeed\": 10}}", errors);
            Assert.That(errors, Has.Some.Contains("unknown key \"jumpHieght\""));
            Assert.That(errors, Has.Some.Contains("\"jumpHeight\" should be a number"));
            Assert.AreEqual(10, cfg.runSpeed);
        }

        [Test]
        public void ShippedConfigHasNoUnknownKeys()
        {
            var errors = new List<string>();
            GameConfig.FromJson(Kit.Read(GameContent.ConfigPath), errors);
            EnemyConfig.FromJson(Kit.Read(GameContent.EnemiesPath), errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void DerivedSpeedsHitTheConfiguredHeights()
        {
            var cfg = Kit.Config();
            var d = cfg.Derived();
            Assert.AreEqual(cfg.jumpHeight, cfg.Peak(d.JumpVelocity, d.Gravity, true), 1e-6);
            Assert.AreEqual(cfg.springHeight, cfg.Peak(d.SpringVelocity, d.Gravity, false), 1e-6);
            cfg.jumpHeight = 4;
            Assert.AreEqual(4, cfg.Peak(cfg.Derived().JumpVelocity, cfg.Derived().Gravity, true), 1e-6, "recomputed after a change");
        }

        [Test]
        public void EveryLegendActorHasAClass()
        {
            foreach (var kv in Kit.Content.Legend.Entries)
            {
                var e = kv.Value;
                if (e.Actor != null && e.Actor != "boss") Assert.IsTrue(LevelData.ActorTypes.ContainsKey(e.Actor), $"'{kv.Key}': {e.Actor}");
                if (e.Tile != null) Assert.IsTrue(LevelData.TileFlags.ContainsKey(e.Tile), $"'{kv.Key}': {e.Tile}");
            }
        }

        [Test]
        public void WorldsHaveNamesAndMusic()
        {
            Assert.GreaterOrEqual(Kit.Content.Worlds.Count, 1);
            foreach (var w in Kit.Content.Worlds)
            {
                Assert.IsNotEmpty(w.Name);
                Assert.IsNotEmpty(w.Music);
            }
        }

        [Test]
        public void JsonRejectsTrailingCommasWithAPosition()
        {
            Json.TryParse("{\n  \"a\": 1,\n}", "x.json", out string err);
            Assert.That(err, Does.StartWith("x.json:3:"));
        }

        // ------------------------------------------------------------------ saves

        [Test]
        public void ProgressRoundTrips()
        {
            var p = new Progress();
            Assert.IsTrue(p.Record(1, 31.256, new[] { true, false, true }, 12, 2));
            Assert.IsFalse(p.Record(1, 40, new[] { false, true, false }, 3, 1), "slower isn't a new best");
            p.Settings.MusicVolume = 0.25;
            p.Settings.TouchControls = "on";
            var back = Progress.FromJson(p.ToJson(), out string problem);
            Assert.IsNull(problem);
            Assert.AreEqual(31.26, back.Level(1).BestTime, 1e-9);
            Assert.AreEqual(new[] { true, true, true }, back.Level(1).Gems, "gems found in any run stay found");
            Assert.AreEqual(12, back.Level(1).BestCoins);
            Assert.AreEqual(3, back.TotalDeaths);
            Assert.AreEqual(0.25, back.Settings.MusicVolume, 1e-9);
            Assert.AreEqual("on", back.Settings.TouchControls);
            Assert.IsTrue(back.IsUnlocked(2));
            Assert.IsFalse(back.IsUnlocked(3));
        }

        [Test]
        public void BrokenSavesStartFresh()
        {
            var p = Progress.FromJson("{ not json", out string problem);
            Assert.IsNotNull(problem);
            Assert.AreEqual(0, p.Levels.Count);
            Assert.IsTrue(p.IsUnlocked(1));
            p = Progress.FromJson("{\"version\": 99}", out problem);
            Assert.That(problem, Does.Contain("newer"));
        }

        [Test]
        public void VersionZeroSavesAreMigrated()
        {
            const string v0 = "{\"unlocked\": 3, \"best_times\": {\"1\": 20.5, \"2\": 33}, \"gems\": {\"2\": [false, true, false]}, \"music\": 0.4, \"sfx\": 0.5}";
            var p = Progress.FromJson(v0, out string problem);
            Assert.IsNull(problem);
            Assert.IsTrue(p.Level(1).Completed && p.Level(2).Completed);
            Assert.IsTrue(p.IsUnlocked(3));
            Assert.IsFalse(p.IsUnlocked(4));
            Assert.AreEqual(20.5, p.Level(1).BestTime, 1e-9);
            Assert.IsTrue(p.Level(2).Gems[1]);
            Assert.AreEqual(0.4, p.Settings.MusicVolume, 1e-9);
            StringAssert.Contains($"\"version\": {Progress.CurrentVersion}", p.ToJson(), "saved back in the current format");
        }
    }
}

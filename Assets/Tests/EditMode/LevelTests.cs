using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Every level: its format, that every gem/checkpoint/goal is reachable with the CURRENT
    /// movement settings (Reach), that every recorded replay still finishes (and beats the boss),
    /// and that runs are deterministic.
    /// </summary>
    public class LevelTests
    {
        static IEnumerable<int> LevelNumbers()
        {
            for (int n = 1; n <= Kit.Content.LevelCount; n++) yield return n;
        }

        [Test]
        public void ContentLoadsWithoutErrors()
        {
            Assert.IsEmpty(Kit.Content.Errors, string.Join("\n", Kit.Content.Errors));
            Assert.GreaterOrEqual(Kit.Content.Levels.Count, 1);
            Assert.AreEqual(Kit.Content.LevelCount, Kit.Content.Levels.Count, "every level file loads");
        }

        [Test]
        public void LevelFilesAreNumberedWithoutGaps()
        {
            var dir = Path.Combine(Kit.Root, ProjectPaths.Levels);
            var numbers = new List<int>();
            foreach (var f in Directory.GetFiles(dir, "level_*.txt"))
            {
                var m = Regex.Match(Path.GetFileName(f), @"^level_(\d\d)\.txt$");
                Assert.IsTrue(m.Success, $"{Path.GetFileName(f)}: name levels level_NN.txt (two digits)");
                numbers.Add(int.Parse(m.Groups[1].Value));
            }
            numbers.Sort();
            for (int i = 0; i < numbers.Count; i++) Assert.AreEqual(i + 1, numbers[i], "level numbers must run 01, 02, 03 ... with no gaps");
        }

        [Test]
        public void EveryWorldHasLevelsAndEndsWithABoss()
        {
            foreach (var world in Kit.Content.Worlds)
            {
                var levels = Kit.Content.LevelsOf(world.Id);
                Assert.IsNotEmpty(levels, $"world {world.Id} has no levels");
                if (world.Bonus) continue; // a bonus world may end without a boss
                Assert.IsTrue(levels[levels.Count - 1].HasBoss,
                    $"world {world.Id}'s last level should have a boss ('B'), or mark the world \"bonus\": true in worlds.json (docs/levels.md)");
            }
        }

        [Test]
        public void ABonusWorldNeedsNoBoss()
        {
            var worlds = "{\"worlds\": [{\"id\": 1, \"name\": \"One\", \"music\": \"m\"}, {\"id\": 2, \"name\": \"Extra\", \"music\": \"m\", \"bonus\": true}]}";
            var c = GameContent.Load(path => path == GameContent.WorldsPath ? worlds : Kit.Read(path));
            Assert.IsFalse(c.World(1).Bonus, "worlds are not bonus worlds unless they say so");
            Assert.IsTrue(c.World(2).Bonus);
        }

        [Test]
        public void ParseErrorsNameTheLineAndColumn()
        {
            var errors = new List<string>();
            var rows = new List<string> { "title: Bad", "world: 1", "colour: red", "---" };
            for (int i = 0; i < 17; i++) rows.Add(new string('.', 30));
            rows[4 + 3] = "..Q" + new string('.', 27);
            Assert.IsNull(LevelData.Parse(string.Join("\n", rows), Kit.Content.Legend, errors, "bad.txt"));
            Assert.That(errors, Has.Some.Contains("bad.txt:3: unknown header key 'colour'"));
            Assert.That(errors, Has.Some.Contains("bad.txt:8:3: unknown map character 'Q'"));
            Assert.That(errors, Has.Some.Contains("exactly one player start"));
            Assert.That(errors, Has.Some.Contains("exactly 3 gems"));
        }

        [Test]
        public void AtLinesMustPointAtTheirActor()
        {
            var errors = new List<string>();
            var text = "title: T\nat 3,1: flyer range=2\n---\n" + string.Join("\n", Rows("P..*.*.*..G"));
            LevelData.Parse(text, Kit.Content.Legend, errors, "t.txt");
            Assert.That(errors, Has.Some.Contains("t.txt:2: no 'flyer' at column 3, row 1"));
        }

        [Test]
        public void ABossNeedsItsType()
        {
            var errors = new List<string>();
            var text = "title: T\n---\n" + string.Join("\n", Rows("P..*.*.*.B.G"));
            LevelData.Parse(text, Kit.Content.Legend, errors, "t.txt");
            Assert.That(errors, Has.Some.Contains("boss type=beetle"));
        }

        /// <summary>17 rows of 30, with `line` on the floor row.</summary>
        static List<string> Rows(string line)
        {
            var rows = new List<string>();
            for (int i = 0; i < 15; i++) rows.Add(new string('.', 30));
            rows.Add(line.PadRight(30, '.'));
            rows.Add(new string('#', 30));
            return rows;
        }

        [TestCaseSource(nameof(LevelNumbers))]
        public void EveryTargetIsReachable(int n)
        {
            var level = Kit.Content.Level(n);
            Assert.IsNotNull(level, $"level {n} didn't load: {string.Join("; ", Kit.Content.Errors)}");
            var r = Reach.Analyze(level, Kit.Config(), Kit.Enemies);
            Assert.IsTrue(r.AllReached,
                $"level {n}: some targets can't be reached with the current movement settings. Run `npm run reach -- level={n}` for the map.\n" +
                Reach.Report(level, r));
        }

        [TestCaseSource(nameof(LevelNumbers))]
        public void EveryReplayStillFinishes(int n)
        {
            var path = Kit.ReplayPath(n);
            if (!File.Exists(path))
            {
                Assert.Ignore($"level {n} has no replay yet: record one with `npm run autoplay -- level={n}` (a shipped level should have one)");
                return;
            }
            var errors = new List<string>();
            var inputs = Replay.Parse(File.ReadAllText(path), errors, Path.GetFileName(path));
            Assert.IsEmpty(errors, string.Join("\n", errors));
            var o = Replay.Play(Kit.Content.Level(n), Kit.Config(), Kit.Enemies, inputs);
            Assert.IsTrue(o.Finished && o.BossesDefeated,
                $"replays/level_{n:00}.replay no longer finishes: {o}.\n" +
                $"See where it goes wrong: npm run replay -- level={n} trace=30. Then fix the level or make a new run: npm run autoplay -- level={n}. Never just delete it.");
        }

        [Test]
        public void ReplaysListTheEnemiesTheyMeetAndDefeat()
        {
            // The player drops onto a walker (met, touched, defeated); the spiker far away is never met.
            var level = LevelData.FromRows(Kit.Content.Legend,
                "..............................",
                "....P.........................",
                "..............................",
                "..............................",
                "....w....................k....",
                "##############################").At(4, 4, "speed=0").At(25, 4, "speed=0");
            var o = Replay.Play(level, Kit.Moves(), Kit.Enemies, new List<int>(new int[90]));
            Assert.AreEqual(2, o.Enemies.Count, o.EnemyReport());
            var walker = o.Enemies.Find(e => e.Kind == "walker");
            var spiker = o.Enemies.Find(e => e.Kind == "spiker");
            Assert.IsTrue(walker.Met && walker.Touched && walker.Defeated, walker.ToString());
            Assert.IsFalse(spiker.Met || spiker.Defeated, spiker.ToString());
            Assert.AreEqual(new Cell(25, 4), spiker.Cell);
            StringAssert.Contains("1 of 2 met", o.EnemyReport());
            StringAssert.Contains("spiker   at 25,4: NOT met", o.EnemyReport());
        }

        [Test]
        public void ShippedLevelsAllHaveReplays()
        {
            for (int n = 1; n <= Kit.Content.LevelCount; n++)
                Assert.IsTrue(File.Exists(Kit.ReplayPath(n)), $"replays/level_{n:00}.replay is missing: npm run autoplay -- level={n}");
        }

        [Test]
        public void RunsAreDeterministic()
        {
            // Two plays of a boss level's replay match tick for tick.
            int n = Kit.Content.Levels.Find(l => l.HasBoss)?.Number ?? 1;
            var inputs = Replay.Parse(File.ReadAllText(Kit.ReplayPath(n)), new List<string>());
            var a = Kit.Content.NewWorld(n);
            var b = Kit.Content.NewWorld(n);
            for (int t = 0; t < inputs.Count; t++)
            {
                a.Step(inputs[t]);
                b.Step(inputs[t]);
                if (t % 60 == 0) Assert.AreEqual(a.Snapshot(), b.Snapshot(), $"tick {t}");
            }
            Assert.AreEqual(a.Snapshot(), b.Snapshot());
        }

        [Test]
        public void ReplayTextRoundTrips()
        {
            var inputs = new List<int> { 0, 0, InputFrame.Right, InputFrame.Right | InputFrame.Jump, InputFrame.Dash, 0 };
            var text = Replay.ToText(inputs, "test");
            Assert.AreEqual("# test\n2 .\n1 R\n1 RJ\n1 X\n1 .\n", text);
            var errors = new List<string>();
            Assert.AreEqual(inputs, Replay.Parse(text, errors));
            Replay.Parse("3 RQ\n", errors, "r");
            Assert.That(errors, Has.Some.Contains("r:1:"));
        }

        [Test]
        public void TheLevelFormatDocExampleParses()
        {
            var doc = File.ReadAllText(Path.Combine(Kit.Root, ProjectPaths.Docs, "levels.md"));
            var m = Regex.Match(doc, "```text\\n(title:[\\s\\S]*?)```");
            Assert.IsTrue(m.Success, "docs/levels.md should contain a ```text example level");
            var errors = new List<string>();
            Assert.IsNotNull(LevelData.Parse(m.Groups[1].Value, Kit.Content.Legend, errors, "docs/levels.md"), string.Join("\n", errors));
        }
    }
}

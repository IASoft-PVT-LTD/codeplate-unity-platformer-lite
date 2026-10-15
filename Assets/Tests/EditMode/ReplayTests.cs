using System.Collections.Generic;
using System.IO;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Recorded runs and the moves they were made with: the bot never holds jump through a fall
    /// (so a hold-to-act move added later can't change an old replay), and every replay names the
    /// moves that were on when it was recorded ("# moves:"), so a replay that breaks after a move
    /// is switched on says why.
    /// </summary>
    public class ReplayTests
    {
        static IEnumerable<int> LevelNumbers()
        {
            for (int n = 1; n <= Kit.Content.LevelCount; n++) yield return n;
        }

        /// <summary>Ticks of <paramref name="inputs"/> where jump stays held while the player falls past the apex with nothing to stomp below.</summary>
        static List<int> HeldThroughFalls(LevelData level, GameConfig cfg, List<int> inputs)
        {
            var w = new World(level, cfg, Kit.Enemies);
            var held = new List<int>();
            int prev = 0;
            foreach (int input in inputs)
            {
                if ((input & InputFrame.Jump) != 0 && (prev & InputFrame.Jump) != 0 &&
                    Autopilot.JumpRelease.Falling(w) && !Autopilot.JumpRelease.StompBelow(w))
                    held.Add(w.Tick);
                w.Step(input);
                prev = input;
                if (w.Finished) break;
            }
            return held;
        }

        [TestCase(1)]
        [TestCase(3)]
        public void TheBotLetsGoOfJumpOnceItFalls(int n)
        {
            var level = Kit.Content.Level(n);
            var bot = new Autopilot(level, Kit.Config(), Kit.Enemies);
            Assert.IsTrue(bot.Run(), $"the bot should finish level {n}:\n{string.Join("\n", bot.Log)}");
            var held = HeldThroughFalls(level, Kit.Config(), bot.Inputs);
            Assert.IsEmpty(held, $"level {n}: the bot held jump while falling at ticks {string.Join(", ", held)}");
        }

        [Test]
        public void JumpReleaseKeepsFreshPressesAndNeverMakesOne()
        {
            // A long hold from the ground: kept while rising, dropped once falling, and not pressed
            // again while the plan still holds it (that would be a new jump on landing).
            var cfg = Kit.Moves();
            var w = new World(Kit.Content.Level(1), cfg, Kit.Enemies);
            var jr = new Autopilot.JumpRelease();
            for (int i = 0; i < 30; i++) w.Step(jr.Apply(w, 0));
            Assert.IsTrue(w.Player.IsGrounded, "the player starts on the ground");
            var sent = new List<int>();
            for (int i = 0; i < 150; i++)
            {
                int s = jr.Apply(w, InputFrame.Jump);
                sent.Add(s);
                w.Step(s);
            }
            Assert.AreEqual(InputFrame.Jump, sent[0], "the press goes through");
            int firstDrop = sent.IndexOf(0);
            Assert.Greater(firstDrop, 5, "jump is held while rising");
            for (int i = firstDrop; i < sent.Count; i++) Assert.AreEqual(0, sent[i], $"tick {i}: once let go, jump stays released while the plan holds it");
            // A fresh press after letting go goes through.
            w.Step(jr.Apply(w, 0));
            Assert.AreEqual(InputFrame.Jump, jr.Apply(w, InputFrame.Jump));
        }

        [Test]
        public void MovesNoteSaysWhenAMoveWasSwitchedOnAfterRecording()
        {
            var cfg = Kit.Moves(dash: true, wallJump: true);
            Assert.AreEqual("dash wall_jump", Replay.Moves(cfg));
            Assert.AreEqual("", Replay.MovesNote("# moves: dash wall_jump\n10 R\n", cfg));
            string older = Replay.MovesNote("# level 1\n# moves: dash\n10 R\n", cfg, "level_01.replay");
            StringAssert.Contains("recorded before wall_jump was switched on", older);
            StringAssert.Contains("# moves: dash", older);
            StringAssert.Contains("which is now off", Replay.MovesNote("# moves: dash double_jump wall_jump\n", cfg));
            StringAssert.Contains("no \"# moves:\" line", Replay.MovesNote("10 R\n", cfg));
            Assert.AreEqual("", Replay.MovesNote("# moves: none\n", Kit.Moves(dash: false)));
            // The header round-trips through ToText and doesn't change the inputs.
            var inputs = new List<int> { 0, InputFrame.Right, InputFrame.Right };
            string text = Replay.ToText(inputs, "level 1", Replay.Moves(cfg));
            CollectionAssert.AreEqual(new[] { "dash", "wall_jump" }, Replay.RecordedMoves(text));
            CollectionAssert.AreEqual(inputs, Replay.Parse(text, new List<string>()));
        }

        [TestCaseSource(nameof(LevelNumbers))]
        public void EveryReplayNamesTheMovesItWasRecordedWith(int n)
        {
            var path = Kit.ReplayPath(n);
            if (!File.Exists(path))
            {
                Assert.Ignore($"level {n} has no replay yet");
                return;
            }
            string note = Replay.MovesNote(File.ReadAllText(path), Kit.Config(), $"replays/level_{n:00}.replay");
            Assert.AreEqual("", note, $"{note} If it still finishes, the moves line can be updated by hand; otherwise npm run autoplay -- level={n}.");
        }
    }
}

using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Reach and the Autopilot know every move the config switches on: a ledge that only a wall
    /// jump (or a double jump) gets to is reachable with the switch on, out of reach with it off,
    /// and the bot finishes the room.
    /// </summary>
    public class ReachTests
    {
        // A chimney: the goal ledge is 8 tiles up, far above a plain jump. Wall jumps between the
        // two walls climb it.
        static readonly string[] Chimney =
        {
            "..............",
            "..............",
            "...........G..",
            "#....#########",
            "#....#........",
            "#....#........",
            "#....#........",
            "#....#........",
            "#....#........",
            "#....#........",
            "#.P..#........",
            "##############",
        };

        // One tall wall with a ledge on top, 5 tiles up: too high for a jump, not for a wall jump.
        static readonly string[] Wall =
        {
            "..............",
            "..........G...",
            "........######",
            "........#.....",
            "........#.....",
            "........#.....",
            "........#.....",
            "..P.....#.....",
            "##############",
        };

        // A ledge 5 tiles up past an edge: a jump plus an air jump.
        static readonly string[] HighLedge =
        {
            "..............",
            "..........G...",
            ".......#######",
            "..............",
            "..............",
            "..............",
            "..P...........",
            "##############",
        };

        // These rooms are drawn for the template's jump heights (a ledge one jump can't reach), so they
        // pin them: a higher jump in config.json mustn't make "out of reach without X" reachable.
        static GameConfig Moves(bool wallJump, bool doubleJump)
        {
            var cfg = Kit.Moves(dash: true, doubleJump: doubleJump, wallJump: wallJump);
            cfg.jumpHeight = 3.25;
            cfg.airJumpHeight = 2.4;
            return cfg;
        }

        static LevelData Level(string[] rows) => LevelData.FromRows(Kit.Content.Legend, rows);

        static Reach.Result Analyze(string[] rows, GameConfig cfg) => Reach.Analyze(Level(rows), cfg, Kit.Enemies);

        [TestCase("chimney")]
        [TestCase("wall")]
        public void AWallJumpLedgeIsReachableOnlyWithWallJump(string room)
        {
            var rows = room == "chimney" ? Chimney : Wall;
            var off = Analyze(rows, Moves(false, false));
            Assert.IsFalse(off.AllReached, "without wall jump the ledge is out of reach:\n" + Reach.Report(Level(rows), off));
            var on = Analyze(rows, Moves(true, false));
            Assert.IsTrue(on.AllReached, "with wall jump Reach climbs to the ledge:\n" + Reach.Report(Level(rows), on));
        }

        [TestCase("chimney")]
        [TestCase("wall")]
        public void TheAutopilotWallJumpsToTheGoal(string room)
        {
            var bot = new Autopilot(Level(room == "chimney" ? Chimney : Wall), Moves(true, false), Kit.Enemies) { MaxTicks = 3000 };
            Assert.IsTrue(bot.Run(), "the bot should finish the room:\n" + string.Join("\n", bot.Log));
            Assert.AreEqual(0, bot.World.Deaths);
            // And its inputs are a real replay.
            var o = Replay.Play(Level(room == "chimney" ? Chimney : Wall), Moves(true, false), Kit.Enemies, bot.Inputs);
            Assert.IsTrue(o.Finished, o.ToString());
        }

        [Test]
        public void AHighLedgeIsReachableOnlyWithDoubleJump()
        {
            var off = Analyze(HighLedge, Moves(false, false));
            Assert.IsFalse(off.AllReached, "without double jump the ledge is out of reach:\n" + Reach.Report(Level(HighLedge), off));
            var on = Analyze(HighLedge, Moves(false, true));
            Assert.IsTrue(on.AllReached, "with double jump Reach gets to the ledge:\n" + Reach.Report(Level(HighLedge), on));
            var bot = new Autopilot(Level(HighLedge), Moves(false, true), Kit.Enemies) { MaxTicks = 3000 };
            Assert.IsTrue(bot.Run(), "the bot should finish the room:\n" + string.Join("\n", bot.Log));
        }
    }
}

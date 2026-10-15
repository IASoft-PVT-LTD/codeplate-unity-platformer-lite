using System;

namespace Game.Core
{
    /// <summary>Step or land on it to be launched springHeight tiles up (config.json).</summary>
    public sealed class Spring : Actor
    {
        int _launched;

        public override void Setup(World world)
        {
            Size = new V2d(0.9, 0.5);
            Pos = Origin;
        }

        public override void Step(World world) => _launched = Math.Max(0, _launched - 1);

        public override void Touched(World world, bool stomp)
        {
            if (world.Player.Body.Vel.Y >= 0 && _launched == 0)
            {
                _launched = 12;
                world.SpringLaunch(this);
            }
        }

        public override string Anim => _launched > 0 ? "launched" : "idle";
    }

    /// <summary>Something to collect: kind "coin", "gem" or "heart". Gems are numbered 0..2 left to right.</summary>
    public sealed class Pickup : Actor
    {
        public int GemIndex = -1;

        public override void Setup(World world)
        {
            Size = new V2d(0.7, 0.7);
            Pos = Origin + new V2d(0, -0.15);
        }

        public override void Touched(World world, bool stomp) => world.Collect(this);
    }

    /// <summary>
    /// Touch it to respawn here after dying (kind "checkpoint"), or to finish the level (kind
    /// "goal"). The goal stays shut until every boss in the level is defeated.
    /// </summary>
    public sealed class Checkpoint : Actor
    {
        public bool Reached;

        public override void Setup(World world)
        {
            Size = Kind == "checkpoint" ? new V2d(1.0, 2.0) : new V2d(1.5, 3.0);
            Pos = Origin;
        }

        public override void Touched(World world, bool stomp)
        {
            if (Kind == "goal")
            {
                if (world.GoalOpen) world.ReachGoal(this);
            }
            else if (!Reached)
            {
                Reached = true;
                world.ReachCheckpoint(this);
            }
        }

        public override string Anim => Kind == "goal" ? (Reached ? "done" : "idle") : Reached ? "up" : "down";
    }
}

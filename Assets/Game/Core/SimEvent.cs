namespace Game.Core
{
    /// <summary>What kind of thing happened during a tick.</summary>
    public enum SimEventType
    {
        Jump, AirJump, WallJump, Dash, Land,
        Coin, Gem, Heart,
        Stomp, Bump, Hurt, Die, Respawn,
        Checkpoint, Spring, Crumble, WallBreak,
        Shoot,
        BossWake, BossTelegraph, BossSlam, BossHit, BossDefeated,
        GoalOpen, Goal,
    }

    /// <summary>
    /// Something that happened during a simulation tick. The views turn events into sounds,
    /// particles, shake and HUD updates; tests assert on them.
    /// </summary>
    public struct SimEvent
    {
        public SimEventType Type;
        /// <summary>Where it happened, in tiles (feet position for the player).</summary>
        public V2d Pos;
        /// <summary>Extra data: landing speed for Land, gem index for Gem, health left for BossHit...</summary>
        public double Value;

        public SimEvent(SimEventType type, V2d pos, double value = 0)
        {
            Type = type;
            Pos = pos;
            Value = value;
        }

        public override string ToString() => $"{Type}@{Pos}";
    }
}

using System.Globalization;

namespace Game.Core
{
    /// <summary>
    /// Base class for bosses: actors with health, phases and a small state machine. The level's
    /// goal stays shut until every boss is defeated. A boss sleeps until the player comes within
    /// its wake distance, and resets to full health when the player respawns.
    /// See docs/recipes/add-boss.md.
    /// </summary>
    public abstract class Boss : Actor
    {
        public int MaxHealth = 3;
        public int Health = 3;
        /// <summary>Display name for the HUD's health bar (the `at` line's name=..., or the kind).</summary>
        public string Name;
        public bool Awake;
        public bool Defeated;
        /// <summary>Ticks spent in the current state (state machines read and reset it).</summary>
        protected int StateTicks;
        /// <summary>0 on full health, then 1, 2... after each hit.</summary>
        public int Phase => MaxHealth - Health;

        protected Body Body;
        protected readonly Collide.Hit Hit = new Collide.Hit();

        public override bool IsDefeated => Defeated;

        /// <summary>Takes one hit: the player stomped it while it was open to attack.</summary>
        protected void TakeHit(World world)
        {
            Health--;
            world.Stomped(this);
            world.Emit(SimEventType.BossHit, Pos, Health);
            if (Health <= 0)
            {
                Defeated = true;
                world.Emit(SimEventType.BossDefeated, Pos);
                world.CheckGoalOpen();
            }
        }

        /// <summary>A stomp it shrugs off: the player bounces, no damage either way.</summary>
        protected void Bump(World world)
        {
            world.Stomped(this, bump: true);
        }

        protected void Wake(World world)
        {
            if (Awake) return;
            Awake = true;
            world.Emit(SimEventType.BossWake, Pos);
        }

        /// <summary>Moves the body with gravity through the grid; returns the hit info.</summary>
        protected Collide.Hit Move(World world, double vx)
        {
            double dt = GameConfig.Dt;
            Body.Vel.X = vx;
            Body.Vel.Y = System.Math.Min(Body.Vel.Y + world.Config.Derived().Gravity * dt, world.Config.maxFallSpeed);
            Hit.Clear();
            Collide.MoveX(Body, world.Grid, Body.Vel.X * dt, 0, false, Hit);
            Collide.MoveY(Body, world.Grid, Body.Vel.Y * dt, 0, false, null, Hit);
            Pos = Body.Pos;
            return Hit;
        }

        public override string Snapshot() =>
            base.Snapshot() + string.Format(CultureInfo.InvariantCulture, ":hp{0}:{1}", Health, Anim);
    }
}

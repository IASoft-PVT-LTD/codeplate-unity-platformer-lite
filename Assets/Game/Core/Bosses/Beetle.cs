using System;

namespace Game.Core
{
    /// <summary>
    /// World 1's boss: a brass beetle that charges across its arena. Each round it faces the
    /// player, paws the ground (the warning), then charges until it slams into a wall, where it's
    /// stunned: stomp it then. Jump over it while it charges. Each hit makes it faster and its
    /// stun shorter (enemies.json "beetle"). Its arena needs a wall on each side.
    /// </summary>
    public sealed class Beetle : Boss
    {
        public enum State { Sleep, Idle, Telegraph, Charge, Stunned, Hurt, Defeated }

        public State Current = State.Sleep;
        int _dir = -1;

        public override void Setup(World world)
        {
            var info = world.Enemies.beetle;
            MaxHealth = Health = Math.Max(1, (int)Num("health", info.health));
            Size = info.size;
            Name = Params.TryGetValue("name", out var n) ? n.Replace('_', ' ') : "Brass Beetle";
            Body = new Body(Origin, Size);
            Pos = Origin;
        }

        public override void OnPlayerRespawn(World world)
        {
            if (Defeated) return;
            Health = MaxHealth;
            Awake = false;
            Body.Pos = Origin;
            Body.Vel = V2d.Zero;
            Pos = Origin;
            Go(State.Sleep);
        }

        void Go(State s)
        {
            Current = s;
            StateTicks = 0;
        }

        double Pick(double[] perPhase) => perPhase[Math.Min(Phase, perPhase.Length - 1)];

        public override void Step(World world)
        {
            var info = world.Enemies.beetle;
            StateTicks++;
            var player = world.Player.Body.Pos;
            switch (Current)
            {
                case State.Sleep:
                    Move(world, 0);
                    if (world.Player.Current != Player.State.Dead && Math.Abs(player.X - Pos.X) < info.wakeDistance && Math.Abs(player.Y - Pos.Y) < 6)
                    {
                        Wake(world);
                        Go(State.Idle);
                    }
                    break;
                case State.Idle:
                    Move(world, 0);
                    _dir = player.X >= Pos.X ? 1 : -1;
                    if (StateTicks >= GameConfig.Ticks(info.idleTime))
                    {
                        Go(State.Telegraph);
                        world.Emit(SimEventType.BossTelegraph, Pos, _dir);
                    }
                    break;
                case State.Telegraph:
                    Move(world, 0);
                    if (StateTicks >= GameConfig.Ticks(info.telegraphTime)) Go(State.Charge);
                    break;
                case State.Charge:
                {
                    var hit = Move(world, _dir * Pick(info.chargeSpeed));
                    if (hit.Left || hit.Right)
                    {
                        Go(State.Stunned);
                        world.Emit(SimEventType.BossSlam, Pos, _dir);
                    }
                    break;
                }
                case State.Stunned:
                    Move(world, 0);
                    if (StateTicks >= GameConfig.Ticks(Pick(info.stunTime))) Go(State.Idle);
                    break;
                case State.Hurt:
                    Move(world, 0);
                    if (StateTicks >= 40) Go(State.Idle);
                    break;
                case State.Defeated:
                    Move(world, 0);
                    if (StateTicks >= 90) Active = false;
                    break;
            }
        }

        public override void Touched(World world, bool stomp)
        {
            switch (Current)
            {
                case State.Stunned:
                    if (!stomp) return;
                    TakeHit(world);
                    Go(Defeated ? State.Defeated : State.Hurt);
                    break;
                case State.Hurt:
                case State.Defeated:
                    return;
                default:
                    if (stomp) Bump(world);
                    else world.HurtPlayer(Pos.X);
                    break;
            }
        }

        public override string Anim => Current switch
        {
            State.Sleep => "sleep",
            State.Telegraph => "telegraph",
            State.Charge => "charge",
            State.Stunned => "stunned",
            State.Hurt => "hurt",
            State.Defeated => "defeated",
            _ => "idle",
        };

        public override int Facing => _dir;
        public override bool IsDangerous => Current == State.Idle || Current == State.Telegraph || Current == State.Charge || Current == State.Sleep;
    }
}

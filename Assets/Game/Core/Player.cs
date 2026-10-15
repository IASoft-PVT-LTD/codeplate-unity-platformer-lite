using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// The player's movement: running, jumping, dashing, falling, walls, getting hurt. Pure logic,
    /// stepped once per tick by World. Every number comes from GameConfig (config.json); see
    /// docs/movement.md for what each one feels like.
    /// </summary>
    public sealed class Player
    {
        public enum State { Ground, Air, Dash, WallSlide, Hurt, Dead }

        public readonly Body Body;
        public State Current = State.Air;
        /// <summary>1 = facing right, -1 = facing left.</summary>
        public int Facing = 1;
        public int Health;
        /// <summary>Index into World.Surfaces of the surface we stand on, or -1.</summary>
        public int Surface = -1;
        /// <summary>The input of the previous tick (for pressed/released edges).</summary>
        public int PrevInput;
        /// <summary>Air dashes left before landing.</summary>
        public int AirDashesLeft;

        readonly GameConfig _cfg;
        readonly GameConfig.DerivedValues _d;
        int _coyote, _buffer, _airJumpsLeft, _wallLock, _wallCoyote, _wallDir, _invuln, _hurt, _drop;
        int _dashTicks, _dashCooldown, _dashDir;
        bool _jumping, _grounded;
        readonly Collide.Hit _hit = new Collide.Hit();

        public Player(GameConfig cfg, V2d start)
        {
            _cfg = cfg;
            _d = cfg.Derived();
            Body = new Body(start, cfg.playerSize);
            Health = cfg.maxHealth;
            AirDashesLeft = cfg.airDashes;
            _airJumpsLeft = cfg.airJumps;
        }

        public bool IsGrounded => _grounded;
        public bool IsInvulnerable => _invuln > 0;
        public bool IsDashing => _dashTicks > 0;
        /// <summary>Ticks of invulnerability left (views blink while it's above 0).</summary>
        public int InvulnTicks => _invuln;

        /// <summary>Puts the player at pos, at rest and full health (respawn).</summary>
        public void ResetAt(V2d pos)
        {
            Body.Pos = pos;
            Body.Vel = V2d.Zero;
            Health = _cfg.maxHealth;
            Current = State.Air;
            _jumping = false;
            _hurt = _buffer = _coyote = _dashTicks = _dashCooldown = 0;
            _invuln = GameConfig.Ticks(_cfg.invulnTime);
            _airJumpsLeft = _cfg.airJumps;
            AirDashesLeft = _cfg.airDashes;
        }

        /// <summary>Advances one tick. Appends what happened to <paramref name="events"/>; dash walls broken go to the grid.</summary>
        public void Step(int input, TileGrid grid, IReadOnlyList<Box> surfaces, List<SimEvent> events)
        {
            int prev = PrevInput;
            PrevInput = input;
            if (Current == State.Dead) return;
            double dt = GameConfig.Dt;
            bool jumpPressed = (input & InputFrame.Jump) != 0 && (prev & InputFrame.Jump) == 0;
            bool jumpReleased = (input & InputFrame.Jump) == 0 && (prev & InputFrame.Jump) != 0;
            bool dashPressed = (input & InputFrame.Dash) != 0 && (prev & InputFrame.Dash) == 0;
            int dir = InputFrame.Dir(input);

            _wallLock = Math.Max(0, _wallLock - 1);
            _invuln = Math.Max(0, _invuln - 1);
            _hurt = Math.Max(0, _hurt - 1);
            _drop = Math.Max(0, _drop - 1);
            _dashCooldown = Math.Max(0, _dashCooldown - 1);
            if (jumpPressed) _buffer = GameConfig.Ticks(_cfg.jumpBufferTime);

            // Dash: a fixed-speed horizontal burst with no gravity.
            if (dashPressed && _cfg.dashEnabled && _hurt == 0 && _dashTicks == 0 && _dashCooldown == 0 && (_grounded || AirDashesLeft > 0))
            {
                _dashDir = dir != 0 ? dir : Facing;
                Facing = _dashDir;
                _dashTicks = GameConfig.Ticks(_cfg.dashTime);
                if (!_grounded) AirDashesLeft--;
                _jumping = false;
                events.Add(new SimEvent(SimEventType.Dash, Body.Pos, _dashDir));
            }
            if (_dashTicks > 0)
            {
                StepDash(grid, surfaces, events);
                return;
            }

            // Horizontal speed
            double vx = Body.Vel.X;
            if (_hurt == 0 && _wallLock == 0)
            {
                double target = dir * _cfg.runSpeed;
                double rate;
                if (dir != 0)
                {
                    rate = _d.GroundAccel * (_grounded ? 1.0 : _cfg.airAccelMult);
                    if (vx != 0 && Math.Sign(vx) != dir) rate *= _cfg.turnAccelMult;
                }
                else rate = _d.GroundDecel * (_grounded ? 1.0 : _cfg.airDecelMult);
                vx = M.MoveToward(vx, target, rate * dt);
            }
            else if (_hurt > 0) vx = M.MoveToward(vx, 0, _d.GroundDecel * _cfg.airDecelMult * dt);
            Body.Vel.X = vx;
            if (dir != 0 && _hurt == 0) Facing = dir;

            // Dropping through a one-way platform: down + jump while standing on one.
            if (_grounded && jumpPressed && (input & InputFrame.Down) != 0 && StandingOnOneWayOnly(grid))
            {
                _drop = 10;
                _buffer = 0;
                _grounded = false;
                jumpPressed = false; // the press was used to drop, not to (air) jump
            }

            // Jumps, in priority order: ground (or coyote), wall, air.
            if (_buffer > 0 && _hurt == 0 && (_grounded || _coyote > 0))
            {
                Body.Vel.Y = -_d.JumpVelocity;
                AfterJump();
                events.Add(new SimEvent(SimEventType.Jump, Body.Pos));
            }
            else if (_buffer > 0 && _hurt == 0 && _cfg.wallJumpEnabled && !_grounded && (_wallDir != 0 || _wallCoyote > 0))
            {
                int away = _wallDir != 0 ? -_wallDir : Facing;
                Body.Vel = new V2d(away * _cfg.wallJumpVelocity.X, _cfg.wallJumpVelocity.Y);
                Facing = away;
                _wallLock = GameConfig.Ticks(_cfg.wallJumpLockTime);
                AfterJump();
                events.Add(new SimEvent(SimEventType.WallJump, Body.Pos));
            }
            else if (jumpPressed && _hurt == 0 && _cfg.doubleJumpEnabled && !_grounded && _coyote == 0 && _airJumpsLeft > 0)
            {
                Body.Vel.Y = -_d.AirJumpVelocity;
                _airJumpsLeft--;
                AfterJump();
                events.Add(new SimEvent(SimEventType.AirJump, Body.Pos));
            }

            // Short hop: releasing jump early cuts the rise.
            if (jumpReleased && _jumping && Body.Vel.Y < -_d.MinJumpVelocity) Body.Vel.Y = -_d.MinJumpVelocity;
            if ((input & InputFrame.Jump) == 0) _jumping = false;

            // Gravity, heavier when falling, lighter at the top of a held jump.
            double g = _d.Gravity;
            if (Body.Vel.Y > 0) g *= _cfg.fallGravityMult;
            else if (_jumping && Math.Abs(Body.Vel.Y) < _cfg.apexHangThreshold) g *= _cfg.apexGravityMult;
            Body.Vel.Y += g * dt;

            // Wall slide
            bool sliding = false;
            if (_cfg.wallJumpEnabled && !_grounded && Body.Vel.Y > 0 && dir != 0 && Collide.TouchingWall(Body, grid, dir))
            {
                Body.Vel.Y = Math.Min(Body.Vel.Y, _cfg.wallSlideSpeed);
                sliding = true;
            }
            double maxFall = (input & InputFrame.Down) != 0 ? _cfg.fastFallSpeed : _cfg.maxFallSpeed;
            Body.Vel.Y = Math.Min(Body.Vel.Y, maxFall);

            Move(grid, surfaces, events, Collide.WallBreak.None);

            // Walls (for wall jumps)
            int onWall = 0;
            if (_cfg.wallJumpEnabled && !_grounded)
            {
                if (Collide.TouchingWall(Body, grid, 1)) onWall = 1;
                else if (Collide.TouchingWall(Body, grid, -1)) onWall = -1;
            }
            if (_wallDir != 0 && onWall == 0) _wallCoyote = GameConfig.Ticks(_cfg.wallCoyoteTime);
            if (onWall != 0) _wallCoyote = 0;
            _wallDir = onWall;

            if (_hurt > 0) Current = State.Hurt;
            else if (_grounded) Current = State.Ground;
            else if (sliding) Current = State.WallSlide;
            else Current = State.Air;
        }

        void StepDash(TileGrid grid, IReadOnlyList<Box> surfaces, List<SimEvent> events)
        {
            _dashTicks--;
            Body.Vel = new V2d(_dashDir * _d.DashSpeed, 0);
            Move(grid, surfaces, events, Collide.WallBreak.Break);
            bool blocked = _hit.Left || _hit.Right;
            if (_dashTicks == 0 || blocked)
            {
                _dashTicks = 0;
                _dashCooldown = GameConfig.Ticks(_cfg.dashCooldown);
                Body.Vel.X = blocked ? 0 : _dashDir * _cfg.runSpeed;
            }
            Current = _dashTicks > 0 ? State.Dash : (_grounded ? State.Ground : State.Air);
        }

        void Move(TileGrid grid, IReadOnlyList<Box> surfaces, List<SimEvent> events, Collide.WallBreak mode)
        {
            double fallSpeed = Body.Vel.Y;
            _hit.Clear();
            Collide.MoveX(Body, grid, Body.Vel.X * GameConfig.Dt, _cfg.ledgeCorrection, !_grounded, _hit, mode);
            Collide.MoveY(Body, grid, Body.Vel.Y * GameConfig.Dt, _cfg.cornerCorrection, _drop > 0, surfaces, _hit);
            if (_hit.Ceiling) _jumping = false;
            if (_hit.Broken != null)
                foreach (var c in _hit.Broken) events.Add(new SimEvent(SimEventType.WallBreak, new V2d(c.X + 0.5, c.Y + 1)));

            // Grace timers count down after use, so a value of N gives N ticks.
            _coyote = Math.Max(0, _coyote - 1);
            _buffer = Math.Max(0, _buffer - 1);
            _wallCoyote = Math.Max(0, _wallCoyote - 1);

            bool wasGrounded = _grounded;
            int surf = -1;
            _grounded = Body.Vel.Y >= 0 && (_hit.Floor || Collide.OnGround(Body, grid, surfaces, _drop > 0, out surf));
            Surface = _hit.Surface >= 0 ? _hit.Surface : surf;
            if (_grounded)
            {
                _airJumpsLeft = _cfg.airJumps;
                AirDashesLeft = _cfg.airDashes;
                _coyote = 0;
                if (!wasGrounded) events.Add(new SimEvent(SimEventType.Land, Body.Pos, fallSpeed));
            }
            else if (wasGrounded && Body.Vel.Y >= 0 && _dashTicks == 0)
            {
                _coyote = GameConfig.Ticks(_cfg.coyoteTime);
            }
        }

        /// <summary>Launches straight up at speed tiles per second (springs, stomps).</summary>
        public void Launch(double speed)
        {
            Body.Vel.Y = -speed;
            _grounded = false;
            _jumping = false;
            _coyote = 0;
            _buffer = 0;
            _dashTicks = 0;
        }

        /// <summary>Takes a hit from something at fromX. Returns true if it landed (not invulnerable).</summary>
        public bool Hurt(double fromX, List<SimEvent> events)
        {
            if (_invuln > 0 || Current == State.Dead) return false;
            Health--;
            _invuln = GameConfig.Ticks(_cfg.invulnTime);
            _hurt = 12;
            _dashTicks = 0;
            double away = Body.Pos.X >= fromX ? 1.0 : -1.0;
            Body.Vel = new V2d(_cfg.knockback.X * away, _cfg.knockback.Y);
            _grounded = false;
            _jumping = false;
            Current = State.Hurt;
            events.Add(new SimEvent(SimEventType.Hurt, Body.Pos, Health));
            return true;
        }

        public void Kill()
        {
            Current = State.Dead;
            Body.Vel = V2d.Zero;
            _dashTicks = 0;
        }

        void AfterJump()
        {
            _buffer = 0;
            _coyote = 0;
            _wallCoyote = 0;
            _jumping = true;
            _grounded = false;
        }

        bool StandingOnOneWayOnly(TileGrid grid)
        {
            int row = M.Round(Body.Bottom);
            bool any = false;
            for (int col = M.Floor(Body.Left + Collide.Eps); col <= M.Floor(Body.Right - Collide.Eps); col++)
            {
                if (grid.IsSolid(col, row)) return false;
                if (grid.IsOneWay(col, row)) any = true;
            }
            return any;
        }

        /// <summary>Sets whether the player counts as standing (the Reach linter records paths from a standing start).</summary>
        public void ForceGrounded(bool grounded) => _grounded = grounded;
    }
}

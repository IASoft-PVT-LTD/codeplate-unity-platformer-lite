using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    /// <summary>
    /// Base class for everything in a level that isn't a tile or the player: enemies, bosses,
    /// moving platforms, springs, pickups, checkpoints, the goal, projectiles. Pure logic. World
    /// calls Step() once per tick, then Touched() when the player overlaps Rect. To add a kind,
    /// subclass this and register it in LevelData.ActorTypes and Levels/legend.json
    /// (docs/recipes/add-enemy.md).
    /// </summary>
    public abstract class Actor
    {
        /// <summary>The legend name, e.g. "walker". Views pick art by it.</summary>
        public string Kind;
        /// <summary>Where it was placed: the bottom-center of its map cell, in tiles.</summary>
        public V2d Origin;
        /// <summary>Feet position (bottom-center), in tiles. Views draw here.</summary>
        public V2d Pos;
        public V2d Size = V2d.One;
        /// <summary>False once collected or defeated. Inactive actors are skipped and hidden.</summary>
        public bool Active = true;
        /// <summary>Spawned during play (projectiles). World removes it once inactive.</summary>
        public bool Transient;
        /// <summary>Parameters from an `at x,y: kind key=value ...` line in the level header.</summary>
        public Dictionary<string, string> Params = new Dictionary<string, string>();

        public Box Rect => new Box(Pos.X - Size.X * 0.5, Pos.Y - Size.Y, Size.X, Size.Y);

        /// <summary>A number from the `at` line, or the fallback.</summary>
        public double Num(string key, double fallback) =>
            Params.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;

        /// <summary>Called once when the level starts, after every actor exists.</summary>
        public virtual void Setup(World world) { }

        public virtual void Step(World world) { }

        /// <summary>The player overlaps this actor. <paramref name="stomp"/>: the player came down onto its top while falling.</summary>
        public virtual void Touched(World world, bool stomp) { }

        /// <summary>A one-way surface the player can stand on (moving platforms, springs, landed blocks), or an empty box.</summary>
        public virtual Box Surface => default;

        /// <summary>How far the surface moved this tick; World carries a standing player by it.</summary>
        public V2d SurfaceDelta;

        /// <summary>For views: a short state name that picks the animation ("walk", "squashed"...).</summary>
        public virtual string Anim => "idle";

        /// <summary>For views: 1 right, -1 left.</summary>
        public virtual int Facing => 1;

        /// <summary>For the Autopilot bot: touching it from the side hurts right now.</summary>
        public virtual bool IsDangerous => false;

        /// <summary>
        /// For `npm run replay`'s enemy list: beaten. By default, gone (inactive); enemies that stay
        /// on screen for a moment once beaten (a squash animation) say so sooner.
        /// </summary>
        public virtual bool IsDefeated => !Active;

        /// <summary>The player died and respawns: reset fights, clear projectiles.</summary>
        public virtual void OnPlayerRespawn(World world) { }

        /// <summary>A compact state string for determinism checks.</summary>
        public virtual string Snapshot() => string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2:F4},{3:F4}", Kind, Active ? 1 : 0, Pos.X, Pos.Y);
    }
}

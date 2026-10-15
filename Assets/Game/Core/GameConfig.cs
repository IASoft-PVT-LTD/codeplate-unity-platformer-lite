using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Game.Core
{
    /// <summary>
    /// Every movement, combat, camera and feel number, loaded from
    /// Assets/Resources/Content/config.json. Change values THERE, not here: the defaults below
    /// only apply when a key is missing. Movement is in TILE units (1.0 = one tile) and seconds,
    /// so art of any tile size keeps the same feel. Jumps are set by height and time to the top;
    /// gravity and jump speeds are derived (see <see cref="Derived"/>, or run `npm run feel`).
    /// docs/movement.md explains what each number feels like.
    /// </summary>
    public sealed class GameConfig
    {
        public const int TicksPerSecond = 60;
        public const double Dt = 1.0 / 60.0;

        // ---- run
        /// <summary>Top running speed, tiles per second.</summary>
        public double runSpeed = 8.5;
        /// <summary>Seconds from standing to top speed on the ground.</summary>
        public double groundAccelTime = 0.10;
        /// <summary>Seconds from top speed to standing on the ground.</summary>
        public double groundDecelTime = 0.06;
        /// <summary>Acceleration multiplier when pushing against your current direction.</summary>
        public double turnAccelMult = 1.8;
        /// <summary>In-air acceleration and deceleration, as multipliers of the ground values.</summary>
        public double airAccelMult = 0.65;
        public double airDecelMult = 0.35;

        // ---- jump
        /// <summary>Jump height in tiles when the button is held.</summary>
        public double jumpHeight = 3.25;
        /// <summary>Jump height in tiles for a quick tap.</summary>
        public double jumpMinHeight = 1.2;
        /// <summary>Seconds from jump to the top of a full jump.</summary>
        public double jumpTimeToApex = 0.36;
        /// <summary>Gravity multiplier while falling (snappier falls).</summary>
        public double fallGravityMult = 1.7;
        /// <summary>Near the top of a held jump (vertical speed below this), gravity is multiplied by apexGravityMult.</summary>
        public double apexHangThreshold = 2.5;
        public double apexGravityMult = 0.5;
        /// <summary>Fastest fall in tiles per second; holding down allows fastFallSpeed.</summary>
        public double maxFallSpeed = 18.0;
        public double fastFallSpeed = 24.0;

        // ---- forgiveness
        /// <summary>Seconds after running off a ledge when a jump still works.</summary>
        public double coyoteTime = 0.10;
        /// <summary>Seconds a jump press is remembered before landing.</summary>
        public double jumpBufferTime = 0.12;
        /// <summary>Bumping a ceiling corner by at most this many tiles nudges you past it.</summary>
        public double cornerCorrection = 0.30;
        /// <summary>Clipping a ledge top by at most this many tiles pops you up onto it.</summary>
        public double ledgeCorrection = 0.15;

        // ---- dash
        /// <summary>A quick horizontal burst (X / Shift / gamepad B). Breaks dash walls ('d').</summary>
        public bool dashEnabled = true;
        /// <summary>How far a dash goes, in tiles.</summary>
        public double dashDistance = 3.5;
        /// <summary>How long a dash lasts, in seconds (no gravity meanwhile).</summary>
        public double dashTime = 0.14;
        /// <summary>Dashes allowed in the air before landing (ground dashes are unlimited).</summary>
        public int airDashes = 1;
        /// <summary>Seconds after a dash before the next one.</summary>
        public double dashCooldown = 0.25;

        // ---- double jump
        public bool doubleJumpEnabled = false;
        /// <summary>Extra jumps in the air before landing.</summary>
        public int airJumps = 1;
        public double airJumpHeight = 2.4;

        // ---- wall jump
        public bool wallJumpEnabled = false;
        /// <summary>Slowest-allowed slide down a wall, tiles per second.</summary>
        public double wallSlideSpeed = 4.0;
        /// <summary>Push off the wall: x away from the wall, y upward (negative = up).</summary>
        public V2d wallJumpVelocity = new V2d(9.0, -15.5);
        /// <summary>Seconds of reduced steering right after a wall jump.</summary>
        public double wallJumpLockTime = 0.15;
        /// <summary>Seconds after leaving a wall when a wall jump still works.</summary>
        public double wallCoyoteTime = 0.08;

        // ---- health
        /// <summary>Hearts. 1 = one-hit mode.</summary>
        public int maxHealth = 3;
        /// <summary>Seconds of invulnerability after a hit.</summary>
        public double invulnTime = 1.0;
        /// <summary>Knockback on a hit: x away from the danger, y upward (negative = up).</summary>
        public V2d knockback = new V2d(6.0, -9.0);
        /// <summary>Seconds between dying and respawning at the last checkpoint.</summary>
        public double respawnDelay = 0.9;

        // ---- combat
        /// <summary>Bounce speed after stomping an enemy (held jump bounces higher).</summary>
        public double stompBounce = 11.0;
        public double stompBounceHeld = 17.0;

        // ---- blocks
        /// <summary>Spring launch height in tiles.</summary>
        public double springHeight = 7.0;
        /// <summary>Seconds a crumbling block holds after you step on it, and until it returns.</summary>
        public double crumbleDelay = 0.45;
        public double crumbleRespawn = 3.0;

        // ---- body
        /// <summary>Player hitbox in tiles (width, height). The sprite is drawn larger.</summary>
        public V2d playerSize = new V2d(0.625, 0.875);

        // ---- display (views only)
        /// <summary>Pixels per tile in the art. Change it (and viewWidth/viewHeight) for bigger art.</summary>
        public int tilePx = 16;
        /// <summary>The game's reference resolution in pixels: how much of the level fits on screen.</summary>
        public int viewWidth = 480;
        public int viewHeight = 270;
        /// <summary>On-screen touch buttons: "auto" (only on touch screens), "always" or "off". Settings can override.</summary>
        public string touchControls = "auto";

        // ---- camera (views only)
        /// <summary>How far ahead of the player the camera looks, in tiles.</summary>
        public double camLookahead = 2.5;
        /// <summary>The player can move this far (tiles) from the camera center before it follows.</summary>
        public V2d camDeadzone = new V2d(1.5, 2.0);
        /// <summary>Higher = snappier camera.</summary>
        public double camSmoothing = 8.0;

        // ---- feel (views only)
        /// <summary>Squash on landing (0 = none).</summary>
        public double landSquash = 0.25;
        public double shakeHurt = 4.0;
        public double shakeStomp = 2.0;
        public double shakeDie = 6.0;
        public double shakeBoss = 5.0;
        /// <summary>Seconds the game freezes on a stomp, for impact.</summary>
        public double hitstopStomp = 0.05;
        public int dustParticles = 6;

        /// <summary>Seconds to whole simulation ticks.</summary>
        public static int Ticks(double seconds) => Math.Max(0, M.Round(seconds * TicksPerSecond));

        // ------------------------------------------------------------------ derived values

        /// <summary>Speeds the simulation derives from the settings above.</summary>
        public sealed class DerivedValues
        {
            public double Gravity;
            public double JumpVelocity;
            public double MinJumpVelocity;
            public double AirJumpVelocity;
            public double SpringVelocity;
            public double GroundAccel;
            public double GroundDecel;
            public double DashSpeed;
        }

        DerivedValues _derived;
        string _derivedKey;

        /// <summary>
        /// Gravity from jumpHeight and jumpTimeToApex, and launch speeds solved numerically with the
        /// same tick-by-tick integration Player uses, so jumpHeight, airJumpHeight and springHeight
        /// (and jumpMinHeight, measured from the moment jump is released) are reached exactly.
        /// Recomputed whenever an input number changes.
        /// </summary>
        public DerivedValues Derived()
        {
            string key = string.Join("|", jumpHeight, jumpTimeToApex, jumpMinHeight, airJumpHeight, springHeight,
                apexHangThreshold, apexGravityMult, runSpeed, groundAccelTime, groundDecelTime, dashDistance, dashTime);
            if (_derived != null && _derivedKey == key) return _derived;
            double g = 2.0 * jumpHeight / (jumpTimeToApex * jumpTimeToApex);
            _derived = new DerivedValues
            {
                Gravity = g,
                JumpVelocity = Solve(jumpHeight, g, true),
                MinJumpVelocity = Solve(jumpMinHeight, g, false),
                AirJumpVelocity = Solve(airJumpHeight, g, true),
                SpringVelocity = Solve(springHeight, g, false),
                GroundAccel = runSpeed / Math.Max(groundAccelTime, 0.001),
                GroundDecel = runSpeed / Math.Max(groundDecelTime, 0.001),
                DashSpeed = dashDistance / Math.Max(Ticks(dashTime), 1) * TicksPerSecond,
            };
            _derivedKey = key;
            return _derived;
        }

        double Solve(double height, double g, bool hang)
        {
            double lo = 0, hi = 200;
            for (int i = 0; i < 60; i++)
            {
                double mid = (lo + hi) * 0.5;
                if (Peak(mid, g, hang) < height) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5;
        }

        /// <summary>How high a launch at speed v rises (tiles), with or without apex hang.</summary>
        public double Peak(double v, double g, bool hang)
        {
            double y = 0, vy = -v, top = 0;
            for (int i = 0; i < 4000; i++)
            {
                double gg = g;
                if (hang && Math.Abs(vy) < apexHangThreshold) gg *= apexGravityMult;
                vy += gg * Dt;
                if (vy >= 0) break;
                y += vy * Dt;
                if (y < top) top = y;
            }
            return -top;
        }

        public GameConfig Clone() => (GameConfig)MemberwiseClone();

        // ------------------------------------------------------------------ loading

        /// <summary>Loads config.json text. Problems (unknown keys, wrong types) go to <paramref name="errors"/>.</summary>
        public static GameConfig FromJson(string text, List<string> errors, string where = "config.json")
        {
            var cfg = new GameConfig();
            var root = Json.TryParse(text, where, out string err) as JsonObject;
            if (err != null) errors.Add(err);
            else if (root == null) errors.Add($"{where}: expected an object");
            else ConfigReader.Fill(cfg, root, where, errors, flattenGroups: true);
            return cfg;
        }
    }

    /// <summary>
    /// Fills an object's public fields from a JSON object by name. Used for config.json and
    /// enemies.json. Supports double, int, bool, string, V2d ([x, y]), double[] and nested classes.
    /// With flattenGroups, a JSON object whose key isn't a field is read into the same target (so
    /// config.json can group keys under "run", "jump" ...).
    /// </summary>
    public static class ConfigReader
    {
        public static void Fill(object target, JsonObject json, string where, List<string> errors, bool flattenGroups = false)
        {
            var type = target.GetType();
            foreach (var key in json.Keys)
            {
                if (key.StartsWith("_")) continue; // "_comment" keys are notes for people and agents
                var value = json[key];
                var f = type.GetField(key, BindingFlags.Public | BindingFlags.Instance);
                if (f == null)
                {
                    if (flattenGroups && value is JsonObject group) Fill(target, group, $"{where}/{key}", errors, true);
                    else errors.Add($"{where}: unknown key \"{key}\" (no field {type.Name}.{key})");
                    continue;
                }
                if (!Assign(target, f, value, out string why)) errors.Add($"{where}: \"{key}\" {why}");
            }
        }

        static bool Assign(object target, FieldInfo f, object value, out string why)
        {
            why = null;
            var t = f.FieldType;
            if (t == typeof(double) && value is double d) { f.SetValue(target, d); return true; }
            if (t == typeof(int) && value is double di)
            {
                if (di != Math.Floor(di)) { why = "must be a whole number"; return false; }
                f.SetValue(target, (int)di);
                return true;
            }
            if (t == typeof(bool) && value is bool b) { f.SetValue(target, b); return true; }
            if (t == typeof(string) && value is string s) { f.SetValue(target, s); return true; }
            if (t == typeof(V2d) && value is List<object> v && v.Count == 2 && v[0] is double x && v[1] is double y)
            {
                f.SetValue(target, new V2d(x, y));
                return true;
            }
            if (t == typeof(double[]) && value is List<object> arr && arr.TrueForAll(o => o is double))
            {
                f.SetValue(target, arr.ConvertAll(o => (double)o).ToArray());
                return true;
            }
            if (t.IsClass && t != typeof(string) && value is JsonObject obj)
            {
                var inner = f.GetValue(target) ?? Activator.CreateInstance(t);
                var errs = new List<string>();
                Fill(inner, obj, f.Name, errs);
                f.SetValue(target, inner);
                if (errs.Count > 0) { why = string.Join("; ", errs); return false; }
                return true;
            }
            why = $"should be {Describe(t)}, got {Describe(value)}";
            return false;
        }

        static string Describe(Type t)
        {
            if (t == typeof(double)) return "a number";
            if (t == typeof(int)) return "a whole number";
            if (t == typeof(bool)) return "true or false";
            if (t == typeof(string)) return "a string";
            if (t == typeof(V2d)) return "[x, y]";
            if (t == typeof(double[])) return "a list of numbers";
            return "an object";
        }

        static string Describe(object v) => v switch
        {
            null => "null",
            double d => d.ToString(CultureInfo.InvariantCulture),
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            List<object> _ => "a list",
            _ => "an object",
        };
    }
}

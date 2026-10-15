using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Enemy and boss numbers, loaded from Assets/Resources/Content/enemies.json (edit them there).
    /// One nested class per enemy kind; a level's `at x,y:` line can override some per placement.
    /// Speeds are tiles per second, times are seconds, sizes are tiles.
    /// </summary>
    public sealed class EnemyConfig
    {
        public sealed class WalkerInfo
        {
            public double speed = 2.0;
            public V2d size = new V2d(0.8, 0.75);
        }

        public sealed class FlyerInfo
        {
            public double speed = 1.5;
            /// <summary>Tiles each way from its start.</summary>
            public double range = 3.0;
            /// <summary>Bob height in tiles, and seconds per bob.</summary>
            public double amplitude = 1.5;
            public double period = 2.0;
            public V2d size = new V2d(0.8, 0.7);
        }

        public sealed class ShooterInfo
        {
            /// <summary>Seconds between shots.</summary>
            public double interval = 2.4;
            /// <summary>Seconds of warning (the barrel glows) before each shot.</summary>
            public double windup = 0.5;
            public double projectileSpeed = 4.5;
            /// <summary>A projectile vanishes after this many tiles, or on hitting a wall.</summary>
            public double projectileRange = 16.0;
            public V2d projectileSize = new V2d(0.45, 0.45);
            /// <summary>Only shoots while the player is within this many tiles.</summary>
            public double wakeDistance = 14.0;
            public V2d size = new V2d(0.9, 0.9);
        }

        public sealed class BeetleInfo
        {
            /// <summary>Stomps to defeat it. Each hit starts the next phase.</summary>
            public int health = 3;
            public V2d size = new V2d(2.25, 1.4);
            /// <summary>The fight starts when the player is this close (tiles).</summary>
            public double wakeDistance = 11.0;
            /// <summary>Per phase (hits taken 0, 1, 2): charge speed, seconds stunned after hitting a wall.</summary>
            public double[] chargeSpeed = { 9.0, 11.0, 13.0 };
            public double[] stunTime = { 1.8, 1.5, 1.2 };
            /// <summary>Seconds it waits, then paws the ground (the warning), before each charge.</summary>
            public double idleTime = 0.7;
            public double telegraphTime = 0.6;
        }

        public WalkerInfo walker = new WalkerInfo();
        /// <summary>The Spiker is a Walker you mustn't stomp; it shares the walker's fields.</summary>
        public WalkerInfo spiker = new WalkerInfo { speed = 1.6 };
        public FlyerInfo flyer = new FlyerInfo();
        public ShooterInfo shooter = new ShooterInfo();
        public BeetleInfo beetle = new BeetleInfo();

        public static EnemyConfig FromJson(string text, List<string> errors, string where = "enemies.json")
        {
            var cfg = new EnemyConfig();
            var root = Json.TryParse(text, where, out string err) as JsonObject;
            if (err != null) errors.Add(err);
            else if (root == null) errors.Add($"{where}: expected an object");
            else ConfigReader.Fill(cfg, root, where, errors);
            return cfg;
        }
    }
}

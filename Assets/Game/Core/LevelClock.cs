using System;

namespace Game.Core
{
    /// <summary>
    /// The HUD's level clock, without the drawing: the text it shows and its warning stage.
    /// Calm above timeWarning seconds, amber from timeWarning down, red (and pulsing) from
    /// timeDanger down. Stages go by the whole seconds the clock shows, so the colour changes
    /// exactly when the number does. docs/recipes/level-clock.md.
    /// </summary>
    public static class LevelClock
    {
        public enum Stage { Calm, Warning, Danger }

        /// <summary>The whole seconds the clock shows for this much time left (rounded up: 0:01 until it's gone).</summary>
        public static int Shown(double secondsLeft) => Math.Max(0, (int)Math.Ceiling(secondsLeft - 1e-9));

        /// <summary>"1:05", "0:09".</summary>
        public static string Text(double secondsLeft)
        {
            int s = Shown(secondsLeft);
            return $"{s / 60}:{s % 60:00}";
        }

        public static Stage StageFor(double secondsLeft, GameConfig cfg)
        {
            int s = Shown(secondsLeft);
            if (s <= cfg.timeDanger) return Stage.Danger;
            if (s <= cfg.timeWarning) return Stage.Warning;
            return Stage.Calm;
        }
    }
}

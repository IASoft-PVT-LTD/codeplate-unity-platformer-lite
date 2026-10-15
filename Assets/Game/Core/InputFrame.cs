namespace Game.Core
{
    /// <summary>
    /// One tick of player input as a bitmask, so input can be recorded, replayed and simulated
    /// exactly. Game.Runtime's InputSource builds it from the Input System actions (keyboard,
    /// gamepad) and the touch buttons.
    /// </summary>
    public static class InputFrame
    {
        public const int Left = 1;
        public const int Right = 2;
        public const int Up = 4;
        public const int Down = 8;
        public const int Jump = 16;
        public const int Dash = 32;

        /// <summary>Letters used in replay files, in bit order: L R U D J X (X = dash).</summary>
        public const string Letters = "LRUDJX";

        public static string ToText(int bits)
        {
            var s = "";
            for (int i = 0; i < Letters.Length; i++)
                if ((bits & (1 << i)) != 0) s += Letters[i];
            return s.Length > 0 ? s : ".";
        }

        /// <summary>Bits from letters. Returns -1 if a letter is unknown.</summary>
        public static int FromText(string text)
        {
            int bits = 0;
            foreach (char ch in text)
            {
                if (ch == '.') continue;
                int i = Letters.IndexOf(ch);
                if (i < 0) return -1;
                bits |= 1 << i;
            }
            return bits;
        }

        public static int Dir(int bits) => ((bits & Right) != 0 ? 1 : 0) - ((bits & Left) != 0 ? 1 : 0);
    }
}

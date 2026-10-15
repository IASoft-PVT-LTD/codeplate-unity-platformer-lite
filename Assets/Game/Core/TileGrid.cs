namespace Game.Core
{
    /// <summary>
    /// The level's tiles as flags per cell. Pure data. Cell (x, y) covers x..x+1, y..y+1 in tile
    /// units; y grows downward. Outside the map: solid walls left and right, open above and below
    /// (you can jump off the top, and falling below the bottom row is a pit).
    /// </summary>
    public sealed class TileGrid
    {
        public const byte Solid = 1;
        /// <summary>Stand on it from above, jump up through it, drop through with down + jump.</summary>
        public const byte OneWay = 2;
        /// <summary>Hurts on touch (the lower half of the cell).</summary>
        public const byte Spikes = 4;
        /// <summary>Solid until stood on for a moment, then breaks and comes back later.</summary>
        public const byte Crumble = 8;
        /// <summary>Solid, but a dash breaks it for good.</summary>
        public const byte DashWall = 16;

        public readonly int Width;
        public readonly int Height;
        readonly byte[] _flags;

        public TileGrid(int width, int height)
        {
            Width = width;
            Height = height;
            _flags = new byte[width * height];
        }

        /// <summary>
        /// A grid from text rows: '#' solid, '=' one-way, '^' spikes, 'x' crumble, 'd' dash wall,
        /// anything else empty. Tests use this for exact little rooms.
        /// </summary>
        public static TileGrid FromRows(params string[] rows)
        {
            var g = new TileGrid(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                switch (rows[y][x])
                {
                    case '#': g.Set(x, y, Solid); break;
                    case '=': g.Set(x, y, OneWay); break;
                    case '^': g.Set(x, y, Spikes); break;
                    case 'x': g.Set(x, y, Solid | Crumble); break;
                    case 'd': g.Set(x, y, Solid | DashWall); break;
                }
            }
            return g;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public int Flags(int x, int y)
        {
            if (x < 0 || x >= Width) return Solid;
            if (y < 0 || y >= Height) return 0;
            return _flags[y * Width + x];
        }

        public void Set(int x, int y, int flags)
        {
            if (InBounds(x, y)) _flags[y * Width + x] = (byte)flags;
        }

        public bool IsSolid(int x, int y) => (Flags(x, y) & Solid) != 0;
        public bool IsOneWay(int x, int y) => (Flags(x, y) & OneWay) != 0;
        public bool Has(int x, int y, int flag) => (Flags(x, y) & flag) != 0;

        /// <summary>Something you can stand on top of: solid or one-way.</summary>
        public bool IsFloor(int x, int y) => (Flags(x, y) & (Solid | OneWay)) != 0;

        public TileGrid Clone()
        {
            var g = new TileGrid(Width, Height);
            System.Array.Copy(_flags, g._flags, _flags.Length);
            return g;
        }
    }
}

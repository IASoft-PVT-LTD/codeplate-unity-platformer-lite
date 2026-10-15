using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Moves a Body through the TileGrid one axis at a time (x, then y) and reports what it hit.
    /// Moves are split into steps of at most <see cref="Step"/> tiles, so fast bodies can't tunnel
    /// through 1-tile walls. Pure logic.
    /// </summary>
    public static class Collide
    {
        public const double Eps = 0.0001;
        public const double Step = 0.45;

        /// <summary>What a dash does to dash walls ('d').</summary>
        public enum WallBreak
        {
            /// <summary>Dash walls are solid.</summary>
            None,
            /// <summary>Running into dash walls breaks them (clears the cells and reports them).</summary>
            Break,
            /// <summary>Dash walls count as empty, and the grid isn't changed (the Reach linter uses this).</summary>
            PassThrough,
        }

        public sealed class Hit
        {
            public bool Left, Right, Floor, Ceiling;
            /// <summary>Index into the surfaces list of the surface landed on, or -1.</summary>
            public int Surface = -1;
            /// <summary>True if a ledge or corner correction nudged the body.</summary>
            public bool Corrected;
            /// <summary>Dash-wall cells broken during this move.</summary>
            public List<Cell> Broken;

            public void Clear()
            {
                Left = Right = Floor = Ceiling = Corrected = false;
                Surface = -1;
                Broken?.Clear();
            }
        }

        static bool BlocksX(TileGrid g, int x, int y, WallBreak mode)
        {
            int f = g.Flags(x, y);
            if ((f & TileGrid.Solid) == 0) return false;
            return !(mode == WallBreak.PassThrough && (f & TileGrid.DashWall) != 0);
        }

        /// <summary>Moves horizontally by dx. <paramref name="airborne"/> enables ledge correction.</summary>
        public static void MoveX(Body b, TileGrid grid, double dx, double ledgeCorrection, bool airborne, Hit hit,
            WallBreak mode = WallBreak.None)
        {
            int steps = Math.Max(1, M.Ceil(Math.Abs(dx) / Step));
            double s = dx / steps;
            var blocked = new List<int>(4);
            for (int i = 0; i < steps; i++)
            {
                double nx = b.Pos.X + s;
                int lead = s > 0 ? M.Floor(nx + b.Size.X * 0.5 - Eps) : M.Floor(nx - b.Size.X * 0.5 + Eps);
                int rowTop = M.Floor(b.Top + Eps);
                int rowBottom = M.Floor(b.Bottom - Eps);
                blocked.Clear();
                for (int row = rowTop; row <= rowBottom; row++)
                    if (BlocksX(grid, lead, row, mode)) blocked.Add(row);
                if (blocked.Count == 0)
                {
                    b.Pos.X = nx;
                    continue;
                }
                // A dash breaks dash walls when every blocking cell is one.
                if (mode == WallBreak.Break && blocked.TrueForAll(r => grid.Has(lead, r, TileGrid.DashWall) && grid.InBounds(lead, r)))
                {
                    foreach (int r in blocked)
                    {
                        grid.Set(lead, r, 0);
                        (hit.Broken ??= new List<Cell>()).Add(new Cell(lead, r));
                    }
                    b.Pos.X = nx;
                    continue;
                }
                // Ledge correction: only the lowest row blocks, its top is just above our feet,
                // and there's room to stand on it.
                if (airborne && blocked.Count == 1 && blocked[0] == rowBottom)
                {
                    double stepUp = b.Bottom - rowBottom;
                    if (stepUp > 0 && stepUp <= ledgeCorrection && BoxFree(grid, nx, rowBottom, b.Size))
                    {
                        b.Pos = new V2d(nx, rowBottom);
                        hit.Corrected = true;
                        continue;
                    }
                }
                if (s > 0)
                {
                    b.Pos.X = lead - b.Size.X * 0.5;
                    hit.Right = true;
                }
                else
                {
                    b.Pos.X = lead + 1 + b.Size.X * 0.5;
                    hit.Left = true;
                }
                b.Vel.X = 0;
                return;
            }
        }

        /// <summary>
        /// Moves vertically by dy. One-way tiles and <paramref name="surfaces"/> (box tops) only stop
        /// downward movement that starts above them; <paramref name="dropThrough"/> ignores them.
        /// </summary>
        public static void MoveY(Body b, TileGrid grid, double dy, double cornerCorrection, bool dropThrough,
            IReadOnlyList<Box> surfaces, Hit hit)
        {
            int steps = Math.Max(1, M.Ceil(Math.Abs(dy) / Step));
            double s = dy / steps;
            for (int i = 0; i < steps; i++)
            {
                double oldBottom = b.Bottom;
                double ny = b.Pos.Y + s;
                int colLeft = M.Floor(b.Left + Eps);
                int colRight = M.Floor(b.Right - Eps);
                if (s > 0)
                {
                    int row = M.Floor(ny - Eps);
                    bool land = false;
                    for (int col = colLeft; col <= colRight; col++)
                    {
                        int f = grid.Flags(col, row);
                        if ((f & TileGrid.Solid) != 0) land = true;
                        else if ((f & TileGrid.OneWay) != 0 && !dropThrough && oldBottom <= row + Eps) land = true;
                    }
                    if (land)
                    {
                        b.Pos.Y = row;
                        b.Vel.Y = 0;
                        hit.Floor = true;
                        return;
                    }
                    if (!dropThrough && surfaces != null)
                    {
                        for (int p = 0; p < surfaces.Count; p++)
                        {
                            var r = surfaces[p];
                            if (b.Right > r.X + Eps && b.Left < r.Right - Eps && oldBottom <= r.Y + Eps && ny >= r.Y)
                            {
                                b.Pos.Y = r.Y;
                                b.Vel.Y = 0;
                                hit.Floor = true;
                                hit.Surface = p;
                                return;
                            }
                        }
                    }
                    b.Pos.Y = ny;
                }
                else
                {
                    int row = M.Floor(ny - b.Size.Y + Eps);
                    int blockCount = 0, blockCol = 0;
                    for (int col = colLeft; col <= colRight; col++)
                    {
                        if (grid.IsSolid(col, row))
                        {
                            blockCount++;
                            blockCol = col;
                        }
                    }
                    if (blockCount == 0)
                    {
                        b.Pos.Y = ny;
                        continue;
                    }
                    // Corner correction: slide past a ceiling corner we only just clip.
                    if (blockCount == 1)
                    {
                        double overlapLeft = blockCol + 1 - b.Left;
                        double overlapRight = b.Right - blockCol;
                        if (blockCol == colLeft && overlapLeft <= cornerCorrection && BoxFree(grid, b.Pos.X + overlapLeft + Eps, ny, b.Size))
                        {
                            b.Pos = new V2d(b.Pos.X + overlapLeft + Eps, ny);
                            hit.Corrected = true;
                            continue;
                        }
                        if (blockCol == colRight && overlapRight <= cornerCorrection && BoxFree(grid, b.Pos.X - overlapRight - Eps, ny, b.Size))
                        {
                            b.Pos = new V2d(b.Pos.X - overlapRight - Eps, ny);
                            hit.Corrected = true;
                            continue;
                        }
                    }
                    b.Pos.Y = row + 1 + b.Size.Y;
                    b.Vel.Y = 0;
                    hit.Ceiling = true;
                    return;
                }
            }
        }

        /// <summary>True if a box with feet at (x, y) overlaps no solid tile.</summary>
        public static bool BoxFree(TileGrid grid, double x, double y, V2d size)
        {
            for (int row = M.Floor(y - size.Y + Eps); row <= M.Floor(y - Eps); row++)
            for (int col = M.Floor(x - size.X * 0.5 + Eps); col <= M.Floor(x + size.X * 0.5 - Eps); col++)
                if (grid.IsSolid(col, row)) return false;
            return true;
        }

        /// <summary>
        /// True if the body stands on something: a solid or one-way tile right below its feet, or one
        /// of <paramref name="surfaces"/>. <paramref name="surface"/> is the surface index, or -1.
        /// </summary>
        public static bool OnGround(Body b, TileGrid grid, IReadOnlyList<Box> surfaces, bool dropThrough, out int surface)
        {
            surface = -1;
            double feet = b.Bottom;
            int row = M.Round(feet);
            if (Math.Abs(feet - row) < 0.002)
            {
                for (int col = M.Floor(b.Left + Eps); col <= M.Floor(b.Right - Eps); col++)
                {
                    int f = grid.Flags(col, row);
                    if ((f & TileGrid.Solid) != 0 || ((f & TileGrid.OneWay) != 0 && !dropThrough)) return true;
                }
            }
            if (!dropThrough && surfaces != null)
            {
                for (int p = 0; p < surfaces.Count; p++)
                {
                    var r = surfaces[p];
                    if (Math.Abs(feet - r.Y) < 0.002 && b.Right > r.X + Eps && b.Left < r.Right - Eps)
                    {
                        surface = p;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>True if a solid tile touches the body's side: dir -1 = left, +1 = right.</summary>
        public static bool TouchingWall(Body b, TileGrid grid, int dir)
        {
            int col = dir > 0 ? M.Floor(b.Right + 0.01) : M.Floor(b.Left - 0.01);
            for (int row = M.Floor(b.Top + 0.1); row <= M.Floor(b.Bottom - 0.1); row++)
                if (grid.IsSolid(col, row)) return true;
            return false;
        }

        /// <summary>True if the body overlaps the dangerous part (lower half) of a spike tile.</summary>
        public static bool TouchingSpikes(Body b, TileGrid grid)
        {
            for (int row = M.Floor(b.Top + Eps); row <= M.Floor(b.Bottom - Eps); row++)
            for (int col = M.Floor(b.Left + Eps); col <= M.Floor(b.Right - Eps); col++)
            {
                if (grid.Has(col, row, TileGrid.Spikes) && b.Bottom > row + 0.5 && b.Right > col + 0.15 && b.Left < col + 0.85)
                    return true;
            }
            return false;
        }
    }
}

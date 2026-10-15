namespace Game.Core
{
    /// <summary>
    /// An axis-aligned box that moves through the tile grid: the player, walkers, the bosses.
    /// <see cref="Pos"/> is the FEET: the bottom-center of the box, in tile units.
    /// </summary>
    public sealed class Body
    {
        public V2d Pos;
        public V2d Vel;
        public V2d Size;

        public Body(V2d pos, V2d size)
        {
            Pos = pos;
            Size = size;
        }

        public double Left => Pos.X - Size.X * 0.5;
        public double Right => Pos.X + Size.X * 0.5;
        public double Top => Pos.Y - Size.Y;
        public double Bottom => Pos.Y;
        public Box Rect => new Box(Left, Top, Size.X, Size.Y);
    }
}

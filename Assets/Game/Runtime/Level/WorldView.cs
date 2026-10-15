using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Runtime
{
    /// <summary>
    /// Draws the level's tiles with a Tilemap built at runtime from the art strips, so levels stay
    /// plain text. Terrain picks one of 16 frames from its neighbours:
    /// frame = N*1 + E*2 + S*4 + W*8 (a bit is set when that neighbour is solid ground too).
    /// Keeps crumbling blocks and dash walls in step with the simulation.
    /// Coordinates: simulation cell (x, y) with y down is tilemap cell (x, -y - 1).
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        World _world;
        Tilemap _map;
        readonly Dictionary<string, Tile[]> _tiles = new Dictionary<string, Tile[]>();
        readonly List<Cell> _crumbles = new List<Cell>();
        readonly Dictionary<Cell, int> _crumbleShown = new Dictionary<Cell, int>();
        readonly List<Cell> _dashWalls = new List<Cell>();

        public Tilemap Tilemap => _map;

        public static Vector3Int ToTilemap(int x, int y) => new Vector3Int(x, -y - 1, 0);

        public void Setup(World world, int worldNumber)
        {
            _world = world;
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(transform, false);
            gridGo.AddComponent<Grid>();
            var mapGo = new GameObject("Tiles");
            mapGo.transform.SetParent(gridGo.transform, false);
            _map = mapGo.AddComponent<Tilemap>();
            var r = mapGo.AddComponent<TilemapRenderer>();
            r.sortingOrder = 0;
            r.mode = TilemapRenderer.Mode.Chunk;

            string terrain = $"tiles/terrain_w{worldNumber}";
            if (Resources.Load<Texture2D>("Art/" + terrain) == null) terrain = "tiles/terrain_w1";
            var g = world.Grid;
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                int f = g.Flags(x, y);
                var c = ToTilemap(x, y);
                if ((f & TileGrid.Crumble) != 0)
                {
                    _crumbles.Add(new Cell(x, y));
                    _map.SetTile(c, Tiles("blocks/crumble")[0]);
                }
                else if ((f & TileGrid.DashWall) != 0)
                {
                    _dashWalls.Add(new Cell(x, y));
                    _map.SetTile(c, Tiles("blocks/dash_wall")[0]);
                }
                else if ((f & TileGrid.Solid) != 0) _map.SetTile(c, Tiles(terrain)[TerrainFrame(x, y)]);
                else if ((f & TileGrid.OneWay) != 0)
                {
                    bool l = g.IsOneWay(x - 1, y), rr = g.IsOneWay(x + 1, y);
                    _map.SetTile(c, Tiles("blocks/one_way")[l && rr ? 1 : l ? 2 : rr ? 0 : 1]);
                }
                else if ((f & TileGrid.Spikes) != 0) _map.SetTile(c, Tiles("blocks/spikes")[0]);
            }
        }

        Tile[] Tiles(string path)
        {
            if (_tiles.TryGetValue(path, out var t)) return t;
            var strip = App.Art.Tiles(path);
            t = new Tile[Mathf.Max(16, strip.Frames.Length)];
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = ScriptableObject.CreateInstance<Tile>();
                t[i].sprite = strip.Frames[Mathf.Min(i, strip.Frames.Length - 1)];
                t[i].colliderType = Tile.ColliderType.None;
            }
            _tiles[path] = t;
            return t;
        }

        int TerrainFrame(int x, int y)
        {
            var g = _world.Grid;
            bool Solid(int cx, int cy)
            {
                if (cy >= g.Height) return true; // the ground continues below the map
                if (cx < 0 || cx >= g.Width) return g.IsSolid(M.Clamp(cx, 0, g.Width - 1), cy);
                int f = g.Flags(cx, cy);
                return (f & TileGrid.Solid) != 0 && (f & (TileGrid.Crumble | TileGrid.DashWall)) == 0;
            }
            return (Solid(x, y - 1) ? 1 : 0) + (Solid(x + 1, y) ? 2 : 0) + (Solid(x, y + 1) ? 4 : 0) + (Solid(x - 1, y) ? 8 : 0);
        }

        /// <summary>Call once per frame: crumbling blocks crack, break and return; broken dash walls vanish.</summary>
        public void Sync()
        {
            foreach (var c in _crumbles)
            {
                int st = _world.CrumbleState(c);
                if (_crumbleShown.TryGetValue(c, out int shown) && shown == st) continue;
                _crumbleShown[c] = st;
                _map.SetTile(ToTilemap(c.X, c.Y), st == 2 ? null : Tiles("blocks/crumble")[st == 1 ? 1 : 0]);
            }
            for (int i = _dashWalls.Count - 1; i >= 0; i--)
            {
                var c = _dashWalls[i];
                if (_world.Grid.IsSolid(c.X, c.Y)) continue;
                _map.SetTile(ToTilemap(c.X, c.Y), null);
                _dashWalls.RemoveAt(i);
            }
        }
    }
}

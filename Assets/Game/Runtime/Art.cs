using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// The art table: every image slot the game draws, with its frame size and animation speed.
    /// Paths are under Assets/Resources/Art/ without ".png". Strips are horizontal (frame 0 on the
    /// left). Keep this table, the PNGs and assets.json in step: a reskin with a different frame
    /// size or count changes the PNG, its assets.json entry and its line here
    /// (docs/recipes/reskin-and-rebrand.md). A test checks this table against assets.json.
    /// </summary>
    public static class ArtSlots
    {
        public readonly struct Slot
        {
            public readonly string Path;
            public readonly int W, H;
            public readonly float Fps;
            public readonly bool Loop;

            public Slot(string path, int w, int h, float fps = 0, bool loop = true)
            {
                Path = path;
                W = w;
                H = h;
                Fps = fps;
                Loop = loop;
            }
        }

        public static readonly Slot[] All =
        {
            // The player (feet on the bottom row, facing right).
            new Slot("player/idle", 24, 24, 6),
            new Slot("player/run", 24, 24, 14),
            new Slot("player/jump", 24, 24),
            new Slot("player/fall", 24, 24, 8),
            new Slot("player/dash", 24, 24, 20),
            new Slot("player/hurt", 24, 24, 10),
            new Slot("player/land", 24, 24, 14, false),
            new Slot("player/wall_slide", 24, 24),
            // Enemies.
            new Slot("enemies/walker_walk", 16, 16, 8),
            new Slot("enemies/walker_squashed", 16, 16),
            new Slot("enemies/spiker_walk", 16, 16, 8),
            new Slot("enemies/flyer_fly", 16, 16, 12),
            new Slot("enemies/flyer_squashed", 16, 16),
            new Slot("enemies/shooter", 16, 16, 6),
            new Slot("enemies/shooter_squashed", 16, 16),
            new Slot("enemies/projectile", 8, 8, 10),
            // Bosses.
            new Slot("bosses/beetle_idle", 48, 32, 4),
            new Slot("bosses/beetle_charge", 48, 32, 16),
            new Slot("bosses/beetle_stunned", 48, 32, 6),
            new Slot("bosses/beetle_hurt", 48, 32),
            // Tiles and blocks (16 px = one tile).
            new Slot("tiles/terrain_w1", 16, 16),
            new Slot("blocks/one_way", 16, 16),
            new Slot("blocks/spikes", 16, 16),
            new Slot("blocks/crumble", 16, 16),
            new Slot("blocks/dash_wall", 16, 16),
            new Slot("blocks/spring", 16, 16),
            new Slot("blocks/platform", 16, 16),
            // Pickups and markers.
            new Slot("pickups/coin", 16, 16, 10),
            new Slot("pickups/gem", 16, 16, 6),
            new Slot("pickups/heart", 16, 16, 3),
            new Slot("pickups/clock", 16, 16, 6),
            new Slot("markers/checkpoint", 16, 32, 8, false),
            new Slot("markers/goal", 32, 48),
            // Effects.
            new Slot("fx/dust", 8, 8, 14, false),
            new Slot("fx/sparkle", 8, 8, 14, false),
            new Slot("fx/pop", 16, 16, 16, false),
            new Slot("fx/debris", 8, 8, 10),
            new Slot("fx/steam", 16, 16, 10, false),
        };

        static Dictionary<string, Slot> _byPath;

        public static bool TryGet(string path, out Slot slot)
        {
            if (_byPath == null)
            {
                _byPath = new Dictionary<string, Slot>();
                foreach (var s in All) _byPath[s.Path] = s;
            }
            return _byPath.TryGetValue(path, out slot);
        }
    }

    /// <summary>An animation: its frames and speed.</summary>
    public sealed class Strip
    {
        public Sprite[] Frames;
        public float Fps;
        public bool Loop;

        public Sprite FrameAt(float seconds)
        {
            if (Frames.Length == 1 || Fps <= 0) return Frames[0];
            int i = (int)(seconds * Fps);
            return Frames[Loop ? i % Frames.Length : Mathf.Min(i, Frames.Length - 1)];
        }
    }

    /// <summary>
    /// Loads textures from Resources/Art and cuts them into sprites (point-filtered, 16 px per
    /// tile unit). Feet-anchored strips get a bottom-center pivot; tiles a centered one.
    /// </summary>
    public sealed class Art
    {
        readonly Dictionary<string, Strip> _strips = new Dictionary<string, Strip>();
        readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        readonly int _ppu;
        Sprite _missing;

        public Art(int pixelsPerTile) => _ppu = pixelsPerTile;

        public int PixelsPerTile => _ppu;

        public Texture2D Texture(string path)
        {
            if (_textures.TryGetValue(path, out var t)) return t;
            t = Resources.Load<Texture2D>("Art/" + path);
            if (t == null) Debug.LogError($"[CodePlate] missing art: {Game.Core.ProjectPaths.Art}/{path}.png (see assets.json)");
            else t.filterMode = FilterMode.Point;
            _textures[path] = t;
            return t;
        }

        /// <summary>An animation strip from the art table, feet-anchored (pivot bottom-center).</summary>
        public Strip Strip(string path) => StripWithPivot(path, new Vector2(0.5f, 0f));

        /// <summary>A strip with centered pivots (tiles, blocks).</summary>
        public Strip Tiles(string path) => StripWithPivot(path, new Vector2(0.5f, 0.5f));

        Strip StripWithPivot(string path, Vector2 pivot)
        {
            string key = path + "|" + pivot;
            if (_strips.TryGetValue(key, out var s)) return s;
            var tex = Texture(path);
            ArtSlots.TryGet(path, out var slot);
            s = new Strip { Fps = slot.Fps, Loop = slot.Loop };
            if (tex == null)
            {
                s.Frames = new[] { Missing() };
            }
            else
            {
                int w = slot.W > 0 ? slot.W : tex.height;
                int h = slot.H > 0 ? slot.H : tex.height;
                int n = Mathf.Max(1, tex.width / w);
                s.Frames = new Sprite[n];
                for (int i = 0; i < n; i++)
                {
                    var sp = Sprite.Create(tex, new Rect(i * w, tex.height - h, w, h), pivot, _ppu, 0, SpriteMeshType.FullRect);
                    sp.name = $"{path}#{i}";
                    s.Frames[i] = sp;
                }
            }
            _strips[key] = s;
            return s;
        }

        /// <summary>A single sprite from a frame of a strip (or a plain image), feet-anchored.</summary>
        public Sprite Frame(string path, int frame = 0)
        {
            var s = Strip(path);
            return s.Frames[Mathf.Clamp(frame, 0, s.Frames.Length - 1)];
        }

        Sprite Missing()
        {
            if (_missing != null) return _missing;
            var t = new Texture2D(2, 2) { filterMode = FilterMode.Point };
            t.SetPixels(new[] { Color.magenta, Color.black, Color.black, Color.magenta });
            t.Apply();
            _missing = UnityEngine.Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0), 2);
            return _missing;
        }
    }
}

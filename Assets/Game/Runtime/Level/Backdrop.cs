using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Three background layers per world (Art/bg/w&lt;N&gt;_sky, _far, _near): the sky stays put,
    /// the far and near layers scroll slower than the level (parallax) and repeat sideways.
    /// A world without its own backgrounds uses world 1's; the world's "tint" (worlds.json) colors all three.
    /// View only. Call Follow(cameraPosition) every frame.
    /// </summary>
    public sealed class Backdrop : MonoBehaviour
    {
        /// <summary>How fast each layer scrolls compared to the level: 0 = fixed to the screen, 1 = moves with the level.</summary>
        public static readonly float[] Parallax = { 0f, 0.25f, 0.5f };
        static readonly string[] Layers = { "sky", "far", "near" };

        readonly SpriteRenderer[] _layers = new SpriteRenderer[3];
        float _width, _height;

        public static Backdrop Create(Transform parent, int world)
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Backdrop>();
            b.Build(world);
            return b;
        }

        void Build(int world)
        {
            string prefix = Resources.Load<Texture2D>($"Art/bg/w{world}_sky") != null ? $"bg/w{world}_" : "bg/w1_";
            // The world's "tint" in worlds.json (white = as drawn): a world borrowing world 1's art can still look its own.
            var info = App.Content?.World(world);
            var tint = info != null && ColorUtility.TryParseHtmlString(info.Tint, out var t) ? t : Color.white;
            for (int i = 0; i < 3; i++)
            {
                var tex = App.Art.Texture(prefix + Layers[i]);
                var go = new GameObject(Layers[i]);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = -100 + i;
                sr.color = tint;
                if (tex == null) continue;
                tex.wrapMode = TextureWrapMode.Repeat;
                float ppu = App.Art.PixelsPerTile;
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
                _width = tex.width / ppu;
                _height = tex.height / ppu;
                if (i > 0)
                {
                    // Repeat sideways: three copies wide, re-centred as the camera moves.
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.size = new Vector2(_width * 3, _height);
                }
                _layers[i] = sr;
            }
        }

        /// <summary>Places the layers for a camera at <paramref name="cam"/> (world units).</summary>
        public void Follow(Vector3 cam, float viewWidth, float viewHeight)
        {
            // Cover a taller view (big screens show more): scale up, never down.
            float scale = Mathf.Max(1f, Mathf.Max(viewHeight / Mathf.Max(_height, 0.01f), viewWidth / Mathf.Max(_width, 0.01f)));
            float w = _width * scale;
            float ppu = App.Art.PixelsPerTile;
            for (int i = 0; i < 3; i++)
            {
                var sr = _layers[i];
                if (sr == null) continue;
                float x = cam.x;
                if (i > 0)
                {
                    // The layer scrolls at Parallax[i] of the level's speed and repeats every w.
                    float baseX = cam.x * (1 - Parallax[i]);
                    x = baseX + w * Mathf.Round((cam.x - baseX) / w);
                }
                sr.transform.position = new Vector3(Mathf.Round(x * ppu) / ppu, Mathf.Round(cam.y * ppu) / ppu, 10 - i);
                sr.transform.localScale = new Vector3(scale, scale, 1);
            }
        }
    }
}

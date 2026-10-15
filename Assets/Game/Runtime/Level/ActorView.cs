using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Draws one actor: picks its strip from the actor's Kind and Anim, flips with Facing, hides
    /// when inactive. ART maps each kind to its art; add a line there for a new actor kind
    /// (docs/recipes/add-enemy.md). Never changes the simulation.
    /// </summary>
    public sealed class ActorView : MonoBehaviour
    {
        /// <summary>
        /// kind → (anim → strip path). "*" is the fallback for anims not listed.
        /// Paths are Art/ slots from ArtSlots (Art.cs).
        /// </summary>
        public static readonly Dictionary<string, Dictionary<string, string>> ART = new Dictionary<string, Dictionary<string, string>>
        {
            ["walker"] = new Dictionary<string, string> { ["*"] = "enemies/walker_walk", ["squashed"] = "enemies/walker_squashed" },
            ["spiker"] = new Dictionary<string, string> { ["*"] = "enemies/spiker_walk" },
            ["flyer"] = new Dictionary<string, string> { ["*"] = "enemies/flyer_fly", ["squashed"] = "enemies/flyer_squashed" },
            ["shooter"] = new Dictionary<string, string> { ["*"] = "enemies/shooter", ["squashed"] = "enemies/shooter_squashed" },
            ["projectile"] = new Dictionary<string, string> { ["*"] = "enemies/projectile" },
            ["spring"] = new Dictionary<string, string> { ["*"] = "blocks/spring" },
            ["coin"] = new Dictionary<string, string> { ["*"] = "pickups/coin" },
            ["gem"] = new Dictionary<string, string> { ["*"] = "pickups/gem" },
            ["heart"] = new Dictionary<string, string> { ["*"] = "pickups/heart" },
            ["clock"] = new Dictionary<string, string> { ["*"] = "pickups/clock" },
            ["checkpoint"] = new Dictionary<string, string> { ["*"] = "markers/checkpoint" },
            ["goal"] = new Dictionary<string, string> { ["*"] = "markers/goal" },
            ["beetle"] = new Dictionary<string, string>
            {
                ["*"] = "bosses/beetle_idle", ["charge"] = "bosses/beetle_charge", ["stunned"] = "bosses/beetle_stunned",
                ["hurt"] = "bosses/beetle_hurt", ["defeated"] = "bosses/beetle_stunned",
            },
        };

        public Actor Actor { get; private set; }
        SpriteAnim _anim;
        readonly List<SpriteRenderer> _parts = new List<SpriteRenderer>();
        float _flash;

        public static ActorView Create(Actor actor, Transform parent)
        {
            var go = new GameObject(actor.Kind);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ActorView>();
            v.Actor = actor;
            v.Build();
            return v;
        }

        void Build()
        {
            int order = Actor is Pickup || Actor is Checkpoint ? 5 : Actor is Boss ? 12 : 10;
            if (Actor is MovingPlatform)
            {
                // A row of tiles: left end, middle..., right end.
                var strip = App.Art.Strip("blocks/platform");
                int w = Mathf.Max(1, Mathf.RoundToInt((float)Actor.Size.X));
                for (int i = 0; i < w; i++)
                {
                    var sr = new GameObject("part").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(transform, false);
                    sr.sprite = strip.Frames[Mathf.Min(w == 1 ? 1 : i == 0 ? 0 : i == w - 1 ? 2 : 1, strip.Frames.Length - 1)];
                    // Platform art sits at the top of its 16 px tile; feet-anchored sprites, so drop by 1 - height.
                    sr.transform.localPosition = new Vector3(i - (w - 1) / 2f, (float)Actor.Size.Y - 1f, 0);
                    sr.sortingOrder = 8;
                    _parts.Add(sr);
                }
                return;
            }
            _anim = SpriteAnim.Create("sprite", transform, order);
            Show(0);
        }

        /// <summary>A hit flash (bosses), seconds.</summary>
        public void Flash(float seconds) => _flash = seconds;

        /// <summary>Places the view at the interpolated feet position (world units) and picks the frame.</summary>
        public void Show(float dt, Vector2? feet = null)
        {
            bool active = Actor.Active;
            gameObject.SetActive(active);
            if (!active) return;
            var p = feet ?? new Vector2((float)Actor.Pos.X, -(float)Actor.Pos.Y);
            transform.position = new Vector3(p.x, p.y, 0);
            if (_anim == null) return;
            var r = _anim.Renderer;
            string anim = Actor.Anim;
            ART.TryGetValue(Actor.Kind, out var table);
            string path = table == null ? null : table.TryGetValue(anim, out var s) ? s : table["*"];
            if (path == null)
            {
                r.sprite = null;
                return;
            }
            var strip = App.Art.Strip(path);
            int first = 0, count = 0;
            switch (Actor.Kind)
            {
                case "shooter" when anim != "squashed":
                    first = anim == "charge" ? 2 : 0;
                    count = 2;
                    break;
                case "spring":
                    first = anim == "launched" ? 1 : 0;
                    count = 1;
                    break;
                case "checkpoint":
                    first = anim == "up" ? 2 : 0;
                    count = 1;
                    break;
                case "goal":
                    first = App.Level != null && App.Level.World.GoalOpen ? 1 : 0;
                    count = 1;
                    break;
            }
            _anim.Play(strip, first, count);
            r.flipX = Actor.Facing < 0;
            // Small view-only touches: warning shakes, a hit flash, fading out when defeated.
            var shakeOffset = Vector3.zero;
            if (anim == "telegraph" || anim == "windup" || anim == "warn") shakeOffset.x = (Mathf.PingPong(Time.time * 40f, 2f) - 1f) / 16f;
            _anim.transform.localPosition = shakeOffset;
            _flash = Mathf.Max(0, _flash - dt);
            var color = Color.white;
            if (_flash > 0 && (int)(_flash * 20) % 2 == 0) color = new Color(1f, 0.45f, 0.4f);
            if (anim == "defeated") color.a = Mathf.Clamp01(1f - _anim.Elapsed / 1.4f) * ((int)(Time.time * 12) % 2 == 0 ? 1 : 0.5f);
            if (anim == "sleep") color = new Color(0.8f, 0.8f, 0.85f);
            r.color = color;
        }
    }
}

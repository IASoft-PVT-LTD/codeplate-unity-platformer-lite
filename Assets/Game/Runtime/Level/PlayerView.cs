using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Draws the player: picks the animation from the simulation state, flips with facing,
    /// squashes on landing and stretches on jumps, blinks while invulnerable, fades out on death.
    /// Never changes the simulation. Animations are the player/* slots in Art.cs.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        Player _player;
        SpriteAnim _anim;
        float _landing, _squashT = 1;
        Vector2 _squashFrom = Vector2.one;
        double _squash;

        public static PlayerView Create(Player player, Transform parent, double landSquash)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PlayerView>();
            v._player = player;
            v._squash = landSquash;
            v._anim = SpriteAnim.Create("sprite", go.transform, 20);
            v._anim.Play(App.Art.Strip("player/idle"));
            return v;
        }

        /// <summary>The animation playing now (tests and screenshots read it).</summary>
        public string AnimName { get; private set; } = "idle";

        public void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Land:
                    if (e.Value > 4)
                    {
                        _landing = 0.12f;
                        SquashTo(new Vector2(1 + (float)_squash, 1 - (float)_squash * 0.8f));
                    }
                    break;
                case SimEventType.Jump:
                case SimEventType.AirJump:
                case SimEventType.WallJump:
                case SimEventType.Spring:
                    SquashTo(new Vector2(1 - (float)_squash * 0.6f, 1 + (float)_squash));
                    break;
                case SimEventType.Dash:
                    SquashTo(new Vector2(1 + (float)_squash, 1 - (float)_squash * 0.5f));
                    break;
            }
        }

        void SquashTo(Vector2 s)
        {
            _squashFrom = s;
            _squashT = 0;
        }

        /// <summary>Called every frame with the interpolated feet position (world units).</summary>
        public void Show(Vector2 feet, float dt)
        {
            transform.position = new Vector3(feet.x, feet.y, 0);
            _landing = Mathf.Max(0, _landing - dt);
            var b = _player.Body;
            string anim = "idle";
            switch (_player.Current)
            {
                case Player.State.Ground:
                    if (_landing > 0) anim = "land";
                    else if (System.Math.Abs(b.Vel.X) > 0.5) anim = "run";
                    break;
                case Player.State.Dash:
                    anim = "dash";
                    break;
                case Player.State.Air:
                    anim = b.Vel.Y < 0 ? "jump" : "fall";
                    break;
                case Player.State.WallSlide:
                    anim = "wall_slide";
                    break;
                case Player.State.Hurt:
                case Player.State.Dead:
                    anim = "hurt";
                    break;
            }
            AnimName = anim;
            _anim.Play(App.Art.Strip("player/" + anim));
            _anim.Renderer.flipX = _player.Facing < 0;

            // Squash and stretch, easing back with a little overshoot.
            _squashT = Mathf.Min(1, _squashT + dt / 0.18f);
            float t = 1 - Mathf.Pow(1 - _squashT, 3);
            var s = Vector2.LerpUnclamped(_squashFrom, Vector2.one, t + Mathf.Sin(t * Mathf.PI) * 0.15f);
            _anim.transform.localScale = new Vector3(s.x, s.y, 1);

            var c = Color.white;
            if (_player.Current == Player.State.Dead) c.a = Mathf.Max(0, _anim.Renderer.color.a - dt * 2);
            else if (_player.IsInvulnerable && (int)(Time.time * 12.5f) % 2 == 0) c.a = 0.35f;
            _anim.Renderer.color = c;
        }
    }
}

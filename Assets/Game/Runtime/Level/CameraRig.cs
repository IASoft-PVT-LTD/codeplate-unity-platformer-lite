using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Follows the player with a dead zone and a look-ahead in the facing direction, stays inside
    /// the level sideways and above its bottom, and shakes on impacts. View only; numbers are the
    /// cam* and shake* keys in config.json.
    /// </summary>
    public sealed class CameraRig
    {
        readonly Camera _cam;
        readonly GameConfig _cfg;
        readonly float _levelW, _levelH;
        Vector2 _target, _pos;
        float _lead, _shake;

        public CameraRig(Camera cam, GameConfig cfg, int levelWidth, int levelHeight)
        {
            _cam = cam;
            _cfg = cfg;
            _levelW = levelWidth;
            _levelH = levelHeight;
        }

        public Vector3 Position => _cam.transform.position;
        public float ViewHeight => _cam.orthographicSize * 2;
        public float ViewWidth => ViewHeight * _cam.aspect;

        /// <summary>Jumps straight to the player's feet (start, respawn).</summary>
        public void Snap(Vector2 feet, int facing)
        {
            _lead = facing * (float)_cfg.camLookahead;
            _target = feet + new Vector2(_lead, 1.5f);
            _pos = Clamp(_target);
            Apply();
        }

        /// <summary>Called every frame with the player's interpolated feet position (world units).</summary>
        public void Follow(Vector2 feet, int facing, float dt)
        {
            _lead = Mathf.Lerp(_lead, facing * (float)_cfg.camLookahead, 1 - Mathf.Exp(-3f * dt));
            var focus = feet + new Vector2(_lead, 1.5f);
            var dz = new Vector2((float)_cfg.camDeadzone.X, (float)_cfg.camDeadzone.Y);
            if (focus.x > _target.x + dz.x) _target.x = focus.x - dz.x;
            else if (focus.x < _target.x - dz.x) _target.x = focus.x + dz.x;
            if (focus.y > _target.y + dz.y) _target.y = focus.y - dz.y;
            else if (focus.y < _target.y - dz.y) _target.y = focus.y + dz.y;
            _pos = Vector2.Lerp(_pos, Clamp(_target), 1 - Mathf.Exp(-(float)_cfg.camSmoothing * dt));
            _shake = Mathf.Max(0, _shake - dt * 30f);
            Apply();
        }

        public void Shake(double strength)
        {
            if (App.Progress != null && !App.Progress.Settings.ScreenShake) return;
            _shake = Mathf.Max(_shake, (float)strength);
        }

        Vector2 Clamp(Vector2 p)
        {
            float hw = ViewWidth / 2, hh = ViewHeight / 2;
            // Sideways: inside the level (centred if the level is narrower than the view).
            p.x = _levelW <= hw * 2 ? _levelW / 2 : Mathf.Clamp(p.x, hw, _levelW - hw);
            // Up: open sky is fine. Down: never below the map's bottom row (y = -height in world units).
            p.y = Mathf.Max(p.y, -_levelH + hh);
            return p;
        }

        void Apply()
        {
            var off = _shake > 0 ? Random.insideUnitCircle * _shake / App.Art.PixelsPerTile : Vector2.zero;
            _cam.transform.position = new Vector3(_pos.x + off.x, _pos.y + off.y, -10);
        }
    }
}

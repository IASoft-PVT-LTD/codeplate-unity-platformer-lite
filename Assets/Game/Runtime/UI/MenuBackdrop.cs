using UnityEngine;

namespace Game.Runtime
{
    /// <summary>Behind the title and the world map: a world's parallax backdrop drifting slowly.</summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        Backdrop _backdrop;
        int _world;
        float _x;

        public static MenuBackdrop Create(Transform parent)
        {
            var go = new GameObject("MenuBackdrop");
            go.transform.SetParent(parent, false);
            return go.AddComponent<MenuBackdrop>();
        }

        public void Show(int world)
        {
            if (world == _world && _backdrop != null) return;
            if (_backdrop != null) Destroy(_backdrop.gameObject);
            _world = world;
            _backdrop = Backdrop.Create(transform, world);
            Update();
        }

        void Update()
        {
            if (_backdrop == null || Boot.Instance == null) return;
            _x += Time.unscaledDeltaTime * 1.2f;
            var cam = Boot.Instance.Camera;
            cam.transform.position = new Vector3(_x, 0, -10);
            float h = cam.orthographicSize * 2;
            _backdrop.Follow(cam.transform.position, h * cam.aspect, h);
        }
    }
}

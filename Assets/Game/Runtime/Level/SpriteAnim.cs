using UnityEngine;

namespace Game.Runtime
{
    /// <summary>Plays a Strip on a SpriteRenderer. Play() restarts only when the strip changes.</summary>
    public sealed class SpriteAnim : MonoBehaviour
    {
        public SpriteRenderer Renderer { get; private set; }
        public Strip Current { get; private set; }
        float _t;
        /// <summary>Optional frame range inside the strip (e.g. a shooter's "charge" frames 2..3).</summary>
        int _first, _count;

        public static SpriteAnim Create(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<SpriteAnim>();
            a.Renderer = go.AddComponent<SpriteRenderer>();
            a.Renderer.sortingOrder = order;
            return a;
        }

        public void Play(Strip strip, int first = 0, int count = 0)
        {
            if (strip == Current && first == _first && count == _count) return;
            Current = strip;
            _first = first;
            _count = count;
            _t = 0;
            Show();
        }

        /// <summary>Seconds since the strip started.</summary>
        public float Elapsed => _t;

        void Update()
        {
            _t += Time.deltaTime;
            Show();
        }

        void Show()
        {
            if (Current == null || Current.Frames.Length == 0) return;
            int n = _count > 0 ? _count : Current.Frames.Length - _first;
            if (n <= 1 || Current.Fps <= 0)
            {
                Renderer.sprite = Current.Frames[Mathf.Min(_first, Current.Frames.Length - 1)];
                return;
            }
            int i = (int)(_t * Current.Fps);
            i = Current.Loop ? i % n : Mathf.Min(i, n - 1);
            Renderer.sprite = Current.Frames[Mathf.Min(_first + i, Current.Frames.Length - 1)];
        }
    }
}

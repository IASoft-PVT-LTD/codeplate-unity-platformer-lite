using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Little sprite effects: dust puffs, sparkles, pops, debris, steam. Pooled, view only. The
    /// randomness here is cosmetic (UnityEngine.Random is fine outside Game.Core).
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        sealed class Bit
        {
            public SpriteAnim Anim;
            public Vector2 Vel;
            public float Gravity, Life, Age;
        }

        readonly List<Bit> _live = new List<Bit>();
        readonly Stack<Bit> _free = new Stack<Bit>();

        Bit Spawn(string strip, Vector2 at, Vector2 vel, float life, float gravity = 0)
        {
            var b = _free.Count > 0 ? _free.Pop() : new Bit { Anim = SpriteAnim.Create("fx", transform, 30) };
            b.Anim.gameObject.SetActive(true);
            b.Anim.Play(App.Art.Strip(strip));
            b.Anim.transform.position = new Vector3(at.x, at.y, 0);
            b.Anim.Renderer.color = Color.white;
            b.Vel = vel;
            b.Gravity = gravity;
            b.Life = life;
            b.Age = 0;
            _live.Add(b);
            return b;
        }

        /// <summary>A puff of dust at the feet (jumps, landings).</summary>
        public void Dust(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float dir = i % 2 == 0 ? 1 : -1;
                Spawn("fx/dust", at + new Vector2(dir * Random.Range(0.1f, 0.4f), 0.05f), new Vector2(dir * Random.Range(0.8f, 2.2f), Random.Range(0.2f, 1f)), 0.3f);
            }
        }

        public void Sparkle(Vector2 at, int count = 5)
        {
            for (int i = 0; i < count; i++)
            {
                var v = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f);
                Spawn("fx/sparkle", at, v, 0.32f);
            }
        }

        public void Pop(Vector2 at) => Spawn("fx/pop", at, Vector2.zero, 0.25f);

        /// <summary>Metal shards flying out (broken dash walls, crumbling blocks).</summary>
        public void Debris(Vector2 at, int count = 6)
        {
            for (int i = 0; i < count; i++)
                Spawn("fx/debris", at + Random.insideUnitCircle * 0.3f, new Vector2(Random.Range(-4f, 4f), Random.Range(3f, 8f)), 0.7f, -30f);
        }

        public void Steam(Vector2 at, int count = 3)
        {
            for (int i = 0; i < count; i++)
                Spawn("fx/steam", at + new Vector2(Random.Range(-0.6f, 0.6f), 0), new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(1f, 2.5f)), 0.6f);
        }

        void Update() => Advance(Time.deltaTime);

        /// <summary>Ages every effect by dt seconds (LevelScreen.StepTicks calls it when it fast-forwards).</summary>
        public void Advance(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var b = _live[i];
                b.Age += dt;
                if (b.Age >= b.Life)
                {
                    b.Anim.gameObject.SetActive(false);
                    _live.RemoveAt(i);
                    _free.Push(b);
                    continue;
                }
                b.Vel.y += b.Gravity * dt;
                b.Anim.transform.position += (Vector3)(b.Vel * dt);
                if (b.Age > b.Life * 0.6f) b.Anim.Renderer.color = new Color(1, 1, 1, 1 - (b.Age - b.Life * 0.6f) / (b.Life * 0.4f));
            }
        }
    }
}

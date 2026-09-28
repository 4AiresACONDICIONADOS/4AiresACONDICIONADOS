using System.Collections.Generic;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// Anime afterimages: snapshots of a character's meshes drawn with a fading fresnel "ghost" shader.
    /// Uses Graphics.RenderMesh (no GameObjects), so dozens of ghosts are cheap.
    /// </summary>
    public sealed class AfterimageSystem : MonoBehaviour
    {
        private sealed class Ghost
        {
            public readonly List<Mesh> Meshes = new List<Mesh>(64);
            public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(64);
            public readonly MaterialPropertyBlock Props = new MaterialPropertyBlock();
            public float Age;
            public float Life;
            public float Delay;
            public Color Color;
        }

        private static AfterimageSystem _instance;
        private readonly List<Ghost> _active = new List<Ghost>();
        private readonly Stack<Ghost> _pool = new Stack<Ghost>();
        private readonly List<Mesh> _tmpMeshes = new List<Mesh>(64);
        private readonly List<Matrix4x4> _tmpMatrices = new List<Matrix4x4>(64);
        private Material _material;

        private static AfterimageSystem Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[Afterimages]");
            _instance = go.AddComponent<AfterimageSystem>();
            return _instance;
        }

        private void Awake()
        {
            _material = MaterialFactory.Ghost(Color.white);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Freezes the current pose of <paramref name="rig"/> as a fading ghost.</summary>
        public static void Spawn(CharacterRig rig, Color color, float lifetime = 0.35f, float delay = 0f)
        {
            if (rig == null) return;
            var sys = Ensure();
            var g = sys.Rent();
            rig.Snapshot(g.Meshes, g.Matrices);
            g.Age = 0f;
            g.Life = Mathf.Max(0.05f, lifetime);
            g.Delay = delay;
            g.Color = color;
            sys._active.Add(g);
        }

        /// <summary>Ghosts distributed along a straight path (flash steps / teleports).</summary>
        public static void SpawnAlongPath(CharacterRig rig, Vector3 from, Vector3 to, int count, Color color, float lifetime = 0.45f)
        {
            if (rig == null || count <= 0) return;
            var sys = Ensure();
            rig.Snapshot(sys._tmpMeshes, sys._tmpMatrices);
            Vector3 current = rig.transform.position;
            for (int i = 0; i < count; i++)
            {
                float k = count == 1 ? 0f : i / (float)(count - 1);
                Vector3 offset = Vector3.Lerp(from, to, k) - current;
                var g = sys.Rent();
                g.Meshes.AddRange(sys._tmpMeshes);
                for (int m = 0; m < sys._tmpMatrices.Count; m++)
                    g.Matrices.Add(Matrix4x4.Translate(offset) * sys._tmpMatrices[m]);
                g.Age = 0f;
                g.Life = lifetime * (0.6f + 0.4f * k);
                g.Delay = 0f;
                g.Color = color;
                sys._active.Add(g);
            }
        }

        private Ghost Rent()
        {
            var g = _pool.Count > 0 ? _pool.Pop() : new Ghost();
            g.Meshes.Clear();
            g.Matrices.Clear();
            return g;
        }

        private void LateUpdate()
        {
            if (_active.Count == 0) return;
            float dt = Time.deltaTime;
            var rp = new RenderParams(_material)
            {
                layer = Layers.VFX,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var g = _active[i];
                if (g.Delay > 0f)
                {
                    g.Delay -= dt;
                    continue;
                }
                g.Age += dt;
                float t = g.Age / g.Life;
                if (t >= 1f)
                {
                    _active.RemoveAt(i);
                    _pool.Push(g);
                    continue;
                }
                Color c = g.Color;
                c.a *= (1f - t) * (1f - t);
                g.Props.SetColor(ShaderIds.BaseColor, c);
                rp.matProps = g.Props;
                for (int m = 0; m < g.Meshes.Count; m++)
                {
                    if (g.Meshes[m] == null) continue;
                    Graphics.RenderMesh(rp, g.Meshes[m], 0, g.Matrices[m]);
                }
            }
        }
    }
}

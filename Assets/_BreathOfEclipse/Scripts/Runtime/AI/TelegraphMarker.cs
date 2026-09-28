using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.AI
{
    /// <summary>
    /// Ground warning for big enemy attacks: a red ring that fills up until the moment of impact, so the player
    /// can read where and when to dodge.
    /// </summary>
    public sealed class TelegraphMarker : MonoBehaviour, IPoolable
    {
        private const string PoolKey = "telegraph_marker";
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private float _duration;
        private float _age;
        private float _linger;

        public static TelegraphMarker Show(Vector3 position, float radius, float duration, Color color)
        {
            var pool = PoolManager.Instance;
            if (pool == null) return null;
            var go = pool.Spawn(PoolKey, Create, position + Vector3.up * 0.06f, Quaternion.identity);
            DevTelemetry.ReportTelegraph(position, radius, duration);
            var marker = go.GetComponent<TelegraphMarker>();
            go.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            marker._duration = Mathf.Max(0.05f, duration);
            marker._age = 0f;
            marker._linger = 0.25f;
            marker._mpb.SetColor(ShaderIds.BaseColor, color);
            go.GetComponent<PooledObject>().Lifetime = duration + marker._linger;
            return marker;
        }

        private static GameObject Create()
        {
            var go = new GameObject("Telegraph", typeof(MeshFilter), typeof(MeshRenderer));
            go.SetActive(false);
            go.layer = Layers.VFX;
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad();
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = MaterialFactory.Telegraph(Color.red);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var m = go.AddComponent<TelegraphMarker>();
            m._renderer = r;
            m._mpb = new MaterialPropertyBlock();
            return go;
        }

        public void OnSpawned() { }
        public void OnReleased() { }

        private void Update()
        {
            _age += Time.deltaTime;
            float fill = Mathf.Clamp01(_age / _duration);
            float alpha = _age > _duration ? Mathf.Clamp01(1f - (_age - _duration) / _linger) : 1f;
            _mpb.SetFloat(ShaderIds.Fill, fill);
            _mpb.SetFloat(ShaderIds.Opacity, alpha);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}

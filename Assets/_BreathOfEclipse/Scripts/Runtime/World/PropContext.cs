using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.VFX;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Builds props in a local frame: static parts go into a <see cref="MeshBatcher"/> (merged per material),
    /// colliders and live parts (lights, smoke, doors, banners) are real GameObjects under <see cref="Root"/>.
    /// </summary>
    public sealed class PropContext
    {
        public readonly MeshBatcher Batch;
        public readonly Transform Root;
        private Matrix4x4 _frame = Matrix4x4.identity;
        private readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        public PropContext(MeshBatcher batch, Transform root)
        {
            Batch = batch;
            Root = root;
        }

        public Matrix4x4 Frame => _frame;
        public Vector3 WorldPoint(Vector3 local) => _frame.MultiplyPoint3x4(local);
        public Quaternion WorldRotation(Vector3 localEuler) => _frame.rotation * Quaternion.Euler(localEuler);

        /// <summary>Enters a child frame (position in the current frame, yaw in degrees, uniform scale).</summary>
        public void Push(Vector3 localPos, float yaw = 0f, float scale = 1f)
        {
            _stack.Push(_frame);
            _frame = _frame * Matrix4x4.TRS(localPos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
        }

        public void PushWorld(Vector3 worldPos, float yaw = 0f, float scale = 1f)
        {
            _stack.Push(_frame);
            _frame = Matrix4x4.TRS(worldPos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
        }

        public void PushRotation(Vector3 localPos, Quaternion rotation)
        {
            _stack.Push(_frame);
            _frame = _frame * Matrix4x4.TRS(localPos, rotation, Vector3.one);
        }

        public void Pop() => _frame = _stack.Count > 0 ? _stack.Pop() : Matrix4x4.identity;

        public void Mesh(Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale, bool shadows = true) =>
            Batch.Add(mesh, _frame * Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale), mat, shadows);

        public void Box(Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, bool shadows = true) =>
            Mesh(ProceduralMeshes.Primitive(PrimitiveType.Cube), mat, pos, euler, scale, shadows);

        public void Cyl(Material mat, Vector3 pos, float radius, float height, Vector3 euler = default, bool shadows = true) =>
            Mesh(ProceduralMeshes.Primitive(PrimitiveType.Cylinder), mat, pos, euler, new Vector3(radius * 2f, height * 0.5f, radius * 2f), shadows);

        public void Ball(Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, bool shadows = true) =>
            Mesh(ProceduralMeshes.Primitive(PrimitiveType.Sphere), mat, pos, euler, scale, shadows);

        public void Cone(Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, float bend = 0f, int segments = 8, bool shadows = true) =>
            Mesh(ProceduralMeshes.Cone(segments, bend), mat, pos, euler, scale, shadows);

        /// <summary>A box collider in the current frame (static world geometry, Default layer).</summary>
        public BoxCollider Collider(Vector3 center, Vector3 size, Vector3 euler = default)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(Root, false);
            go.transform.SetPositionAndRotation(WorldPoint(center), WorldRotation(euler));
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            return c;
        }

        public CapsuleCollider Capsule(Vector3 bottom, float radius, float height)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(Root, false);
            go.transform.position = WorldPoint(bottom + Vector3.up * height * 0.5f);
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = radius;
            c.height = height;
            return c;
        }

        /// <summary>A live renderer (glowing window, banner, shutter) in the current frame.</summary>
        public GameObject Live(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale, bool shadows = false)
        {
            var go = ProceduralMeshes.CreatePart(name, mesh, mat, Root, Vector3.zero, Quaternion.identity, Vector3.one);
            var t = go.transform;
            t.SetPositionAndRotation(WorldPoint(pos), WorldRotation(euler));
            t.localScale = Vector3.Scale(scale, _frame.lossyScale);
            go.GetComponent<MeshRenderer>().shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Lantern / window light in the current frame.</summary>
        public NightLight Light(Vector3 pos, Color color, float intensity, float range, Color glow, params Renderer[] glowRenderers) =>
            NightLight.Create(Root, WorldPoint(pos), color, intensity, range, glow, glowRenderers);

        public ParticleSystem Smoke(Vector3 pos, float rate = 2.5f, Color? color = null)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(Root, false);
            go.transform.position = WorldPoint(pos);
            var ps = ParticleBuilder.Create("Smoke", go.transform, MaterialFactory.Vfx(ProceduralTextures.Smoke, VfxBlend.AlphaBlend, 0.5f))
                .Duration(5f, true).Rate(rate).Shape(ParticleSystemShapeType.Cone, 0.15f, 8f).Life(4f, 6f).Speed(0.5f, 0.9f)
                .Size(0.6f, 1.2f).Rotation(0f, 360f).Spin(-15f, 15f).Color(color ?? new Color(0.85f, 0.85f, 0.88f, 0.22f))
                .Noise(0.3f, 0.3f, 0.2f).Fade(0.15f, 0.4f).Grow(0.5f, 2.2f).MaxParticles(40).PlayOnAwake().Done();
            return ps;
        }

        public ParticleSystem Fire(Vector3 pos, float size = 1f)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(Root, false);
            go.transform.position = WorldPoint(pos);
            var ps = ParticleBuilder.Create("Flames", go.transform, MaterialFactory.Vfx(ProceduralTextures.Flame, VfxBlend.Additive, 0.3f))
                .Duration(2f, true).Rate(18f * size).Shape(ParticleSystemShapeType.Circle, 0.25f * size).Life(0.5f, 0.9f).Speed(0.6f, 1.2f)
                .Size(0.35f * size, 0.7f * size).Color(new Color(2.4f, 1.1f, 0.3f), new Color(1.8f, 0.5f, 0.15f)).Noise(0.4f, 1.2f)
                .Fade(0.05f, 0.4f).Shrink(1f, 0.2f).MaxParticles(60).PlayOnAwake().Done();
            ParticleBuilder.Create("Embers", go.transform, MaterialFactory.Vfx(ProceduralTextures.Glow, VfxBlend.Additive, 0f))
                .Duration(2f, true).Rate(4f * size).Shape(ParticleSystemShapeType.Circle, 0.3f * size).Life(1.2f, 2f).Speed(0.8f, 1.8f)
                .Size(0.03f, 0.06f).Color(new Color(3f, 1.4f, 0.4f)).Noise(0.8f, 1.5f).Fade(0.05f, 0.6f).MaxParticles(30).PlayOnAwake();
            return ps;
        }
    }

    /// <summary>Shared materials of the region (cached toon materials).</summary>
    public static class RegionMats
    {
        public static Material Wood => MaterialFactory.Toon(new Color(0.42f, 0.28f, 0.19f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.WoodDetail, 1f);
        public static Material DarkWood => MaterialFactory.Toon(new Color(0.2f, 0.13f, 0.1f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.WoodDetail, 1f);
        public static Material FreshWood => MaterialFactory.Toon(new Color(0.78f, 0.62f, 0.42f), 1f, false, null, 0.55f, 0.2f, 0f, ProceduralTextures.WoodDetail, 1f);
        public static Material Plaster => MaterialFactory.Toon(new Color(0.93f, 0.9f, 0.82f), 1f, false, null, 0.6f, 0.15f, 0f, RegionMeshes.Plaster, 1f);
        public static Material Stone => MaterialFactory.Toon(new Color(0.5f, 0.5f, 0.54f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.StoneDetail, 1f);
        public static Material DarkStone => MaterialFactory.Toon(new Color(0.3f, 0.3f, 0.34f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.StoneDetail, 1f);
        public static Material Roof => MaterialFactory.Toon(new Color(0.24f, 0.27f, 0.36f), 1.1f, false, null, 0.5f, 0.25f, 0.15f, RegionMeshes.RoofTiles, 3f);
        public static Material Thatch => MaterialFactory.Toon(new Color(0.66f, 0.55f, 0.33f), 1.1f, false, null, 0.55f, 0.2f, 0f, ProceduralTextures.WoodDetail, 4f);
        public static Material Shoji => MaterialFactory.Toon(new Color(1f, 0.97f, 0.9f), 0.6f, false, null, 0.75f, 0.1f, 0f, RegionMeshes.Shoji, 1f);
        public static Material Red => MaterialFactory.Toon(new Color(0.62f, 0.12f, 0.12f), 1f, false, null, 0.5f, 0.3f);
        public static Material Cloth(Color c) => MaterialFactory.Toon(c, 0.6f, false, null, 0.6f, 0.2f);
        public static Material Straw => MaterialFactory.Toon(new Color(0.8f, 0.68f, 0.38f), 0.8f, false, null, 0.55f, 0.2f);
        public static Material Iron => MaterialFactory.Toon(new Color(0.14f, 0.14f, 0.16f), 1f, false, null, 0.5f, 0.3f, 0.3f);
        public static Material Bronze => MaterialFactory.Toon(new Color(0.42f, 0.3f, 0.16f), 1f, false, null, 0.5f, 0.4f, 0.5f);
        public static Material Paper => MaterialFactory.Toon(new Color(0.98f, 0.96f, 0.9f), 0.5f, false, null, 0.8f, 0.1f);
        public static Material Lantern => MaterialFactory.Toon(new Color(1f, 0.85f, 0.6f), 0.5f, false, null, 0.8f, 0.1f);
        public static Material RedLantern => MaterialFactory.Toon(new Color(0.85f, 0.2f, 0.15f), 0.5f, false, null, 0.7f, 0.1f);
        public static Material Coals => MaterialFactory.Toon(new Color(0.25f, 0.08f, 0.05f), 0f, false, null, 0.6f, 0f);
        public static Material Leaves(Color c) => MaterialFactory.Toon(c, 0.8f, false, null, 0.45f, 0.35f);
        public static Material Plant(Color c) => MaterialFactory.Toon(c, 0f, false, null, 0.5f, 0.3f);
        public static Material Bark => MaterialFactory.Toon(new Color(0.22f, 0.15f, 0.13f), 0.9f, false, null, 0.5f, 0.15f, 0f, ProceduralTextures.WoodDetail, 2f);
        public static Material Rock => MaterialFactory.Toon(new Color(0.42f, 0.43f, 0.47f), 0.8f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.StoneDetail, 1f);
        public static Material Mountain => MaterialFactory.Toon(new Color(0.36f, 0.42f, 0.5f), 0f, false, null, 0.55f, 0.15f, 0f, ProceduralTextures.StoneDetail, 6f);
        public static Material Snow => MaterialFactory.Toon(new Color(0.92f, 0.94f, 1f), 0f, false, null, 0.7f, 0.2f);
    }
}

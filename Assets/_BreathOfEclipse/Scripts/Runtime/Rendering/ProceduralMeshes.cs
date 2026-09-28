using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Rendering
{
    /// <summary>
    /// Mesh generation for placeholder art and VFX shapes. Every mesh is cached by its parameters.
    /// Unity primitives are extracted once (without colliders) so characters never self-collide.
    /// </summary>
    public static class ProceduralMeshes
    {
        private static readonly Dictionary<PrimitiveType, Mesh> Primitives = new Dictionary<PrimitiveType, Mesh>();
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Primitives.Clear();
            Cache.Clear();
        }

        public static Mesh Primitive(PrimitiveType type)
        {
            if (Primitives.TryGetValue(type, out var mesh) && mesh != null) return mesh;
            var temp = GameObject.CreatePrimitive(type);
            mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            Primitives[type] = mesh;
            return mesh;
        }

        /// <summary>Creates a child with a mesh renderer (no collider).</summary>
        public static GameObject CreatePart(string name, Mesh mesh, Material material, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = parent != null ? parent.gameObject.layer : 0;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = localRot;
            t.localScale = localScale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return go;
        }

        public static GameObject CreatePart(string name, Mesh mesh, Material material, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
        {
            return CreatePart(name, mesh, material, parent, localPos, Quaternion.Euler(localEuler), localScale);
        }

        public static GameObject CreatePart(string name, PrimitiveType type, Material material, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            return CreatePart(name, Primitive(type), material, parent, localPos, localRot, localScale);
        }

        public static GameObject CreatePart(string name, PrimitiveType type, Material material, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
        {
            return CreatePart(name, Primitive(type), material, parent, localPos, Quaternion.Euler(localEuler), localScale);
        }

        /// <summary>Cone along +Y from 0 to height (horns, hair spikes, claws).</summary>
        public static Mesh Cone(int segments = 10, float bend = 0f)
        {
            string key = $"cone_{segments}_{bend:F2}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var normals = new List<Vector3>();
            const int rings = 4;
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float radius = 0.5f * (1f - v);
                float y = v;
                float offsetZ = bend * v * v;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Cos(a), 0.45f, Mathf.Sin(a)).normalized;
                    verts.Add(new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius + offsetZ));
                    normals.Add(n);
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s;
                    int b = a + stride;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            }
            // base cap
            int center = verts.Count;
            verts.Add(Vector3.zero);
            normals.Add(Vector3.down);
            for (int s = 0; s < segments; s++)
            {
                tris.Add(center); tris.Add(s + 1); tris.Add(s);
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Curved single-edged blade along +Z starting at the guard (z=0) with a pointed tip.
        /// Width is along +Y (edge up), thickness along X.
        /// </summary>
        public static Mesh KatanaBlade(float length, float width, float thickness, float curvature)
        {
            string key = $"blade_{length:F3}_{width:F3}_{thickness:F3}_{curvature:F3}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            const int segments = 12;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            // Cross-section: spine (back, -Y) thicker, edge (+Y) sharp. 4 points: edge, right, spine, left.
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float z = t * length;
                float taper = t < 0.86f ? Mathf.Lerp(1f, 0.82f, t / 0.86f) : Mathf.Lerp(0.82f, 0f, (t - 0.86f) / 0.14f);
                float w = width * taper;
                float bendY = -curvature * t * t * length; // sori: the blade curves toward the spine
                float th = thickness * Mathf.Max(0.15f, taper);
                // Tip (kissaki) slants: the edge meets the spine at the end.
                float edgeY = bendY + w * 0.5f;
                float spineY = bendY - w * 0.5f;
                if (t > 0.86f) spineY = Mathf.Lerp(bendY - width * 0.41f, bendY + w * 0.5f, (t - 0.86f) / 0.14f);
                verts.Add(new Vector3(0f, edgeY, z));
                verts.Add(new Vector3(th * 0.5f, (edgeY + spineY) * 0.5f - w * 0.1f, z));
                verts.Add(new Vector3(0f, spineY, z));
                verts.Add(new Vector3(-th * 0.5f, (edgeY + spineY) * 0.5f - w * 0.1f, z));
                uvs.Add(new Vector2(t, 1f)); uvs.Add(new Vector2(t, 0.5f)); uvs.Add(new Vector2(t, 0f)); uvs.Add(new Vector2(t, 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 4;
                int b = a + 4;
                for (int k = 0; k < 4; k++)
                {
                    int k1 = (k + 1) % 4;
                    tris.Add(a + k); tris.Add(b + k); tris.Add(a + k1);
                    tris.Add(a + k1); tris.Add(b + k); tris.Add(b + k1);
                }
            }
            // cap at guard
            tris.Add(0); tris.Add(1); tris.Add(2);
            tris.Add(0); tris.Add(2); tris.Add(3);
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat crescent in the XZ plane (moon blades, air cuts), spanning <paramref name="arcDegrees"/>.</summary>
        public static Mesh Crescent(float arcDegrees = 150f, float thickness = 0.28f, int segments = 32)
        {
            string key = $"crescent_{arcDegrees:F0}_{thickness:F2}_{segments}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float half = arcDegrees * 0.5f * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = Mathf.Lerp(-half, half, t);
                // Thickest in the middle, pointed ends.
                float th = thickness * Mathf.Sin(t * Mathf.PI);
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts.Add(dir * 1f);
                verts.Add(dir * (1f - th));
                uvs.Add(new Vector2(t, 1f));
                uvs.Add(new Vector2(t, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat ring in XZ (shockwaves, telegraph circles). UV.y = 0 inner, 1 outer; UV.x around.</summary>
        public static Mesh Ring(float innerRadius = 0.8f, int segments = 64)
        {
            string key = $"ring_{innerRadius:F2}_{segments}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = t * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(dir * innerRadius);
                verts.Add(dir);
                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                // Clockwise seen from above (+Y) = front face.
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 2); tris.Add(a + 3); tris.Add(a + 1);
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Unit quad in XZ facing up, centered (ground decals). UV 0..1.</summary>
        public static Mesh GroundQuad()
        {
            const string key = "ground_quad";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mesh = new Mesh { name = key };
            mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Open cylinder/cone from y=0 (bottomRadius) to y=1 (topRadius) for light shafts and tornado shells.</summary>
        public static Mesh OpenCylinder(float bottomRadius, float topRadius, int segments = 32, int rings = 8, float twist = 0f)
        {
            string key = $"opencyl_{bottomRadius:F2}_{topRadius:F2}_{segments}_{rings}_{twist:F2}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float radius = Mathf.Lerp(bottomRadius, topRadius, v);
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments;
                    float a = u * Mathf.PI * 2f + twist * v;
                    verts.Add(new Vector3(Mathf.Cos(a) * radius, v, Mathf.Sin(a) * radius));
                    uvs.Add(new Vector2(u, v));
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s;
                    int b = a + stride;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Inward-facing sphere for the sky dome.</summary>
        public static Mesh SkySphere(int lon = 48, int lat = 24)
        {
            string key = $"sky_{lon}_{lat}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon;
                    float theta = u * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)));
                    uvs.Add(new Vector2(u, 1f - v));
                }
            }
            int stride = lon + 1;
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int a = y * stride + x;
                    int b = a + stride;
                    // Clockwise seen from the center = visible from inside.
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Mesh builder for tubes along a polyline (serpents, dragons, wind spirals).</summary>
        public static void BuildTube(Mesh mesh, IList<Vector3> points, IList<float> radii, int radialSegments, Vector3 upHint)
        {
            int n = points.Count;
            if (n < 2)
            {
                mesh.Clear();
                return;
            }
            var verts = new Vector3[n * (radialSegments + 1)];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[(n - 1) * radialSegments * 6];
            Vector3 prevNormal = upHint;
            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = i < n - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1];
                if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.forward;
                tangent.Normalize();
                // Parallel transport keeps the tube from twisting.
                Vector3 normal = Vector3.ProjectOnPlane(prevNormal, tangent);
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
                normal.Normalize();
                prevNormal = normal;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                float u = i / (float)(n - 1);
                for (int s = 0; s <= radialSegments; s++)
                {
                    float a = s / (float)radialSegments * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    int idx = i * (radialSegments + 1) + s;
                    verts[idx] = points[i] + dir * radii[i];
                    normals[idx] = dir;
                    uvs[idx] = new Vector2(u, s / (float)radialSegments);
                }
            }
            int ti = 0;
            int stride = radialSegments + 1;
            for (int i = 0; i < n - 1; i++)
            {
                for (int s = 0; s < radialSegments; s++)
                {
                    int a = i * stride + s;
                    int b = a + stride;
                    tris[ti++] = a; tris[ti++] = a + 1; tris[ti++] = b;
                    tris[ti++] = a + 1; tris[ti++] = b + 1; tris[ti++] = b;
                }
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}

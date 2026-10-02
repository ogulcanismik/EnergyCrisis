using System.Collections.Generic;
using UnityEngine;

namespace MinistryOfPower.UI.Map
{
    /// <summary>Builds flat XZ meshes + outline verts from simple polygon rings.</summary>
    public static class PolygonMeshUtil
    {
        public static Mesh BuildFlatMesh(Vector2[] ring, float y, string meshName)
            => BuildExtrudedMesh(ring, y, 0.08f, meshName);

        /// <summary>
        /// Thin prism so MeshCollider has non-zero volume (flat tris often miss raycasts).
        /// </summary>
        public static Mesh BuildExtrudedMesh(Vector2[] ring, float yBottom, float height, string meshName)
        {
            if (ring == null || ring.Length < 3)
                return new Mesh { name = meshName };

            int n = ring.Length;
            float yTop = yBottom + height;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts[i] = new Vector3(ring[i].x, yTop, ring[i].y);
                verts[i + n] = new Vector3(ring[i].x, yBottom, ring[i].y);
                uvs[i] = uvs[i + n] = new Vector2(ring[i].x * 0.05f + 0.5f, ring[i].y * 0.05f + 0.5f);
            }

            int[] topTris = Triangulate(ring);
            var tris = new System.Collections.Generic.List<int>(topTris.Length * 2 + n * 6);

            // Top (CCW from above)
            for (int i = 0; i < topTris.Length; i++)
                tris.Add(topTris[i]);

            // Bottom (reverse winding)
            for (int i = 0; i < topTris.Length; i += 3)
            {
                tris.Add(topTris[i] + n);
                tris.Add(topTris[i + 2] + n);
                tris.Add(topTris[i + 1] + n);
            }

            // Sides
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int t0 = i;
                int t1 = j;
                int b0 = i + n;
                int b1 = j + n;
                tris.Add(t0); tris.Add(b0); tris.Add(t1);
                tris.Add(t1); tris.Add(b0); tris.Add(b1);
            }

            var mesh = new Mesh
            {
                name = meshName,
                vertices = verts,
                triangles = tris.ToArray(),
                uv = uvs
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector3[] OutlineWorld(Vector2[] ring, float y, bool closed)
        {
            int n = ring.Length;
            int count = closed ? n + 1 : n;
            var pts = new Vector3[count];
            for (int i = 0; i < n; i++)
                pts[i] = new Vector3(ring[i].x, y, ring[i].y);
            if (closed) pts[n] = pts[0];
            return pts;
        }

        /// <summary>Ear-clip triangulation for simple (non-self-intersecting) polygons.</summary>
        public static int[] Triangulate(Vector2[] ring)
        {
            int n = ring.Length;
            if (n < 3) return System.Array.Empty<int>();

            var indices = new List<int>(n);
            for (int i = 0; i < n; i++) indices.Add(i);

            // Ensure CCW winding for consistent ear tests.
            if (SignedArea(ring) < 0f) indices.Reverse();

            var tris = new List<int>((n - 2) * 3);
            int guard = 0;
            while (indices.Count > 3 && guard++ < n * n)
            {
                bool clipped = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int i0 = indices[(i + indices.Count - 1) % indices.Count];
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % indices.Count];
                    Vector2 a = ring[i0];
                    Vector2 b = ring[i1];
                    Vector2 c = ring[i2];

                    if (!IsConvex(a, b, c)) continue;
                    if (ContainsAny(ring, indices, i0, i1, i2, a, b, c)) continue;

                    tris.Add(i0);
                    tris.Add(i1);
                    tris.Add(i2);
                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped) break;
            }

            if (indices.Count == 3)
            {
                tris.Add(indices[0]);
                tris.Add(indices[1]);
                tris.Add(indices[2]);
            }

            return tris.ToArray();
        }

        private static float SignedArea(Vector2[] ring)
        {
            float a = 0f;
            for (int i = 0; i < ring.Length; i++)
            {
                Vector2 p = ring[i];
                Vector2 q = ring[(i + 1) % ring.Length];
                a += p.x * q.y - q.x * p.y;
            }

            return a * 0.5f;
        }

        private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
            => ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) > 1e-6f;

        private static bool ContainsAny(
            Vector2[] ring, List<int> indices, int i0, int i1, int i2,
            Vector2 a, Vector2 b, Vector2 c)
        {
            for (int k = 0; k < indices.Count; k++)
            {
                int idx = indices[k];
                if (idx == i0 || idx == i1 || idx == i2) continue;
                if (PointInTri(ring[idx], a, b, c)) return true;
            }

            return false;
        }

        private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}

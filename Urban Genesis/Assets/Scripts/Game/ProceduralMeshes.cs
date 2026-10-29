using System.Collections.Generic;
using UnityEngine;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// The two solids Unity has no built-in primitive for: a pyramid and a gable (pitched) roof.
    /// Both are one unit wide, deep and tall, sitting on the ground with their middle at the origin, and are
    /// meant to be stretched with the object's scale. Faces are flat shaded, so roofs show crisp edges.
    /// </summary>
    public static class ProceduralMeshes
    {
        private static Mesh pyramid;
        private static Mesh gableRidgeAlongX;
        private static Mesh gableRidgeAlongZ;

        public static Mesh Pyramid()
        {
            if (pyramid == null)
            {
                pyramid = BuildPyramid();
            }

            return pyramid;
        }

        /// <summary>A roof with two sloping sides. The ridge runs along the object's X axis or its Z axis.</summary>
        public static Mesh GableRoof(bool ridgeAlongX)
        {
            if (ridgeAlongX)
            {
                if (gableRidgeAlongX == null)
                {
                    gableRidgeAlongX = BuildGable(true);
                }

                return gableRidgeAlongX;
            }

            if (gableRidgeAlongZ == null)
            {
                gableRidgeAlongZ = BuildGable(false);
            }

            return gableRidgeAlongZ;
        }

        private static Mesh BuildPyramid()
        {
            var b = new Builder();
            Vector3 apex = new Vector3(0f, 1f, 0f);
            Vector3 c0 = new Vector3(-0.5f, 0f, -0.5f);
            Vector3 c1 = new Vector3(-0.5f, 0f, 0.5f);
            Vector3 c2 = new Vector3(0.5f, 0f, 0.5f);
            Vector3 c3 = new Vector3(0.5f, 0f, -0.5f);

            b.Triangle(c0, c1, apex);
            b.Triangle(c1, c2, apex);
            b.Triangle(c2, c3, apex);
            b.Triangle(c3, c0, apex);
            return b.Build("Pyramid");
        }

        private static Mesh BuildGable(bool ridgeAlongX)
        {
            var b = new Builder();
            Vector3 c0 = new Vector3(-0.5f, 0f, -0.5f);
            Vector3 c1 = new Vector3(-0.5f, 0f, 0.5f);
            Vector3 c2 = new Vector3(0.5f, 0f, 0.5f);
            Vector3 c3 = new Vector3(0.5f, 0f, -0.5f);

            if (ridgeAlongX)
            {
                Vector3 r0 = new Vector3(-0.5f, 1f, 0f);
                Vector3 r1 = new Vector3(0.5f, 1f, 0f);

                b.Quad(c0, r0, r1, c3);   // front slope
                b.Quad(c1, c2, r1, r0);   // back slope
                b.Triangle(c0, c1, r0);   // left end
                b.Triangle(c3, r1, c2);   // right end
                return b.Build("Gable Roof (ridge along X)");
            }

            Vector3 s0 = new Vector3(0f, 1f, -0.5f);
            Vector3 s1 = new Vector3(0f, 1f, 0.5f);

            b.Quad(c0, c1, s1, s0);       // left slope
            b.Quad(c3, s0, s1, c2);       // right slope
            b.Triangle(c0, s0, c3);       // front end
            b.Triangle(c1, c2, s1);       // back end
            return b.Build("Gable Roof (ridge along Z)");
        }

        /// <summary>
        /// Collects flat-shaded triangles. Every triangle gets its own vertices, and its winding is flipped if
        /// needed so it faces away from the middle of the solid (all solids built here are convex).
        /// </summary>
        private sealed class Builder
        {
            private static readonly Vector3 Middle = new Vector3(0f, 0.3f, 0f);

            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 centre = (a + b + c) * (1f / 3f);
                if (Vector3.Dot(normal, centre - Middle) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                }

                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(a, b, c);
                Triangle(a, c, d);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh();
                mesh.name = name;
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}

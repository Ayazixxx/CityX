using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A crystal of ice built in code (Glacier Spikes, docs/GLACIER_SPIKES.md): a faceted column on a unit footprint, base at 0 and tip at 1,
    /// tapering to a point, its facets and rings a little uneven and its spine bent to one side, all from a seed. A port of
    /// createCrystalGeometry in achrefelouafi's LinearAbiltyCastingThreeJS (MIT).
    ///
    /// Flat shaded: every triangle has its own vertices and its face's normal. A second, smooth normal goes in uv1 (round the spine, tilted up
    /// at the tip) for the depth-normals pass, so the cel look's black lines trace the crystal's outline and not every facet.
    /// </summary>
    public static class IceCrystalMesh
    {
        /// <summary>The rings' heights up the column; the tip is at 1.</summary>
        private static readonly float[] RingHeights = { 0.0f, 0.22f, 0.5f, 0.75f, 0.92f };

        /// <summary>How far the smooth normal tilts up on the sides and at the top ring's tip faces.</summary>
        private const float SideTilt = 0.25f;
        private const float TipTilt = 0.8f;

        public sealed class Shape
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector3> SmoothNormals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
        }

        /// <summary>The crystal's triangles for <paramref name="seed"/>: <paramref name="facets"/> sides, <paramref name="taper"/> the top ring's width share.</summary>
        public static Shape Build(float seed, int facets, float taper, float roughness, float bend)
        {
            int sides = Mathf.Max(3, facets);
            float tip = Mathf.Clamp(taper, 0.01f, 0.9f);
            float bendAngle = Hash(seed * 1.77f) * Mathf.PI * 2.0f;
            var bendWay = new Vector2(Mathf.Cos(bendAngle), Mathf.Sin(bendAngle));

            var angles = new float[sides];
            for (int i = 0; i < sides; i++)
            {
                float jitter = (Hash(seed * 3.13f + i * 7.7f) - 0.5f) * (Mathf.PI * 2.0f / sides) * 0.55f * roughness * 3.0f;
                angles[i] = i * Mathf.PI * 2.0f / sides + jitter;
            }

            var rings = new Vector3[RingHeights.Length, sides];
            for (int k = 0; k < RingHeights.Length; k++)
            {
                float t = RingHeights[k];
                float radius = 0.5f * (tip + (1.0f - tip) * Mathf.Pow(1.0f - t, 1.15f));
                float y = t + (k > 0 ? (Hash(seed * 5.9f + k * 2.3f) - 0.5f) * 0.06f * roughness : 0.0f);
                Vector2 drift = bendWay * Drift(t, bend);
                for (int i = 0; i < sides; i++)
                {
                    float wobble = 1.0f + (Hash(seed * 11.1f + k * 13.7f + i * 3.9f) - 0.5f) * roughness * 1.3f * (0.35f + 0.65f * t);
                    float r = Mathf.Max(0.002f, radius * wobble);
                    rings[k, i] = new Vector3(r * Mathf.Cos(angles[i]) + drift.x, y, r * Mathf.Sin(angles[i]) + drift.y);
                }
            }
            Vector2 top = bendWay * Drift(1.0f, bend);
            var apex = new Vector3(top.x + (Hash(seed * 17.3f) - 0.5f) * 0.09f * roughness, 1.0f, top.y + (Hash(seed * 19.7f) - 0.5f) * 0.09f * roughness);

            var shape = new Shape();
            for (int k = 0; k < RingHeights.Length - 1; k++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Side(shape, rings[k, i], rings[k, j], rings[k + 1, i], bendWay, bend);
                    Side(shape, rings[k, j], rings[k + 1, j], rings[k + 1, i], bendWay, bend);
                }
            }
            int last = RingHeights.Length - 1;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Vector3 a = rings[last, i];
                Vector3 b = rings[last, j];
                Vector3 middle = (a + b + apex) / 3.0f;
                Vector3 outward = middle - Spine(middle.y, bendWay, bend);
                outward.y = 0.0f;
                Triangle(shape, a, b, apex, outward.normalized + Vector3.up,
                    Smooth(a, bendWay, bend, TipTilt), Smooth(b, bendWay, bend, TipTilt), Vector3.up);
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Triangle(shape, Vector3.zero, rings[0, j], rings[0, i], Vector3.down, Vector3.down, Vector3.down, Vector3.down);
            }
            return shape;
        }

        /// <summary>The crystal as a mesh, named <paramref name="name"/>.</summary>
        public static Mesh ToMesh(Shape shape, string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(shape.Vertices);
            mesh.SetNormals(shape.Normals);
            mesh.SetUVs(1, shape.SmoothNormals);
            mesh.SetTriangles(shape.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static float Drift(float t, float bend) => bend * 0.5f * Mathf.Pow(t, 1.6f);

        private static Vector3 Spine(float t, Vector2 bendWay, float bend)
        {
            Vector2 drift = bendWay * Drift(t, bend);
            return new Vector3(drift.x, t, drift.y);
        }

        /// <summary>A side face, wound to face away from the spine.</summary>
        private static void Side(Shape shape, Vector3 a, Vector3 b, Vector3 c, Vector2 bendWay, float bend)
        {
            Vector3 middle = (a + b + c) / 3.0f;
            Vector3 outward = middle - Spine(middle.y, bendWay, bend);
            outward.y = 0.0f;
            Triangle(shape, a, b, c, outward, Smooth(a, bendWay, bend, SideTilt), Smooth(b, bendWay, bend, SideTilt), Smooth(c, bendWay, bend, SideTilt));
        }

        /// <summary>The smooth normal at <paramref name="point"/>: straight out from the spine, tilted up by <paramref name="tilt"/>.</summary>
        private static Vector3 Smooth(Vector3 point, Vector2 bendWay, float bend, float tilt)
        {
            Vector3 radial = point - Spine(point.y, bendWay, bend);
            radial.y = 0.0f;
            radial = radial.sqrMagnitude > 1e-8f ? radial.normalized : Vector3.forward;
            return (radial + Vector3.up * tilt).normalized;
        }

        /// <summary>One flat triangle, its winding turned so its face points along <paramref name="outward"/>.</summary>
        private static void Triangle(Shape shape, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector3 smoothA, Vector3 smoothB, Vector3 smoothC)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, outward) < 0.0f)
            {
                (b, c) = (c, b);
                (smoothB, smoothC) = (smoothC, smoothB);
                normal = -normal;
            }
            normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : outward.normalized;
            int first = shape.Vertices.Count;
            shape.Vertices.Add(a);
            shape.Vertices.Add(b);
            shape.Vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                shape.Normals.Add(normal);
            }
            shape.SmoothNormals.Add(smoothA);
            shape.SmoothNormals.Add(smoothB);
            shape.SmoothNormals.Add(smoothC);
            shape.Triangles.Add(first);
            shape.Triangles.Add(first + 1);
            shape.Triangles.Add(first + 2);
        }

        /// <summary>A repeatable number in [0, 1) from <paramref name="x"/>.</summary>
        private static float Hash(float x)
        {
            double s = System.Math.Sin(x * 127.1) * 43758.5453123;
            return (float)(s - System.Math.Floor(s));
        }
    }
}

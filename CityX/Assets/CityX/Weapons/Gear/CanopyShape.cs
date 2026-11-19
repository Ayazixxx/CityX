using System.Collections.Generic;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE CANOPY'S SHAPE (docs/PARACHUTE.md): a ram-air wing, the kind skydivers and the GTA games fly, at the body's scale (1.3 times a person's):
    /// a span arched down at its tips, an airfoil along its chord (a blunt nose, thickest a quarter of the way back, thin at the tail), cut into
    /// cells whose tops bulge between the ribs, two colours cell by cell, the mouths along the nose dark. Its frame: the origin where the lines
    /// meet over the shoulders, x along the span (right), y up, z forward; the lower surface's middle is <see cref="LineLength"/> above.
    /// Pure: the builder saves it as a mesh, and the lines' anchors come from here too.
    /// </summary>
    public static class CanopyShape
    {
        /// <summary>Tip to tip along the arch, front to back, and the thickest the airfoil gets, metres.</summary>
        public const float Span = 7.0f;

        public const float Chord = 2.6f;

        public const float Thickness = 0.38f;

        /// <summary>How far round the arch each tip comes, degrees.</summary>
        public const float ArcHalfDegrees = 32.0f;

        /// <summary>Cells across the span (seven or nine on a real canopy).</summary>
        public const int Cells = 9;

        /// <summary>From where the lines meet over the shoulders up to the middle of the lower surface, metres.</summary>
        public const float LineLength = 3.6f;

        /// <summary>The nose's height as a share of the airfoil's thickest (the mouths), and how much a cell's top bulges between its ribs.</summary>
        public const float NoseShare = 0.55f;

        public const float Bulge = 0.12f;

        /// <summary>Submeshes: the cells in one colour, in the other, and the mouths.</summary>
        public const int ColourA = 0, ColourB = 1, Mouths = 2;

        private const int StationsPerCell = 4;
        private const int ChordStations = 14;

        /// <summary>
        /// Where the suspension lines meet the lower surface, a third of the way back from the nose, and the ribs they hang from on the right half
        /// (the left mirrors them): three a side, and one steering line from the tip's tail, eight in all (owner, 2026-11-19: "the parachute has a
        /// lot of strings lower it to maybe 8 total"; a real canopy has dozens, cascaded into four risers).
        /// </summary>
        private const float LineRow = 0.32f;

        private static readonly int[] LineRibs = { 5, 7, 9 };

        private static readonly int[] SteeringRibs = { 9 };

        private static float ArcRadius => Span * 0.5f / (ArcHalfDegrees * Mathf.Deg2Rad);

        /// <summary>The point of the arch at span share <paramref name="s"/> (-1 the left tip, 1 the right), on the lower surface's line.</summary>
        public static Vector3 ArchPoint(float s)
        {
            float phi = s * ArcHalfDegrees * Mathf.Deg2Rad;
            float r = ArcRadius;
            return new Vector3(r * Mathf.Sin(phi), LineLength + r * Mathf.Cos(phi) - r, 0.0f);
        }

        /// <summary>The way out of the arch there (up at the middle, out and up at the tips).</summary>
        public static Vector3 ArchNormal(float s)
        {
            float phi = s * ArcHalfDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0.0f);
        }

        /// <summary>The upper surface's height over the lower at chord share <paramref name="c"/> (0 the nose, 1 the tail), as a share of the thickest.</summary>
        public static float UpperShare(float c)
        {
            const float thickest = 0.25f;
            if (c < thickest)
            {
                return NoseShare + (1.0f - NoseShare) * Mathf.Sin(0.5f * Mathf.PI * c / thickest);
            }
            return Mathf.Pow(Mathf.Max(0.0f, Mathf.Cos(0.5f * Mathf.PI * (c - thickest) / (1.0f - thickest))), 1.2f);
        }

        private static float ChordAt(int k) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * k / ChordStations);

        private static float Z(float c) => Chord * 0.5f - c * Chord;

        private static float SpanAt(int i) => -1.0f + 2.0f * i / (Cells * StationsPerCell);

        /// <summary>A point of the canopy: span share, chord share, height over the lower surface (metres).</summary>
        private static Vector3 Point(float s, float c, float height) => ArchPoint(s) + ArchNormal(s) * height + Vector3.forward * Z(c);

        /// <summary>The rib a span share is closest to, and how far between two ribs it is (0 on a rib, 1 halfway).</summary>
        private static float BetweenRibs(int i) => Mathf.Sin(Mathf.PI * (i % StationsPerCell) / StationsPerCell);

        public static Mesh Build()
        {
            int spanStations = Cells * StationsPerCell + 1;
            var vertices = new List<Vector3>();
            var cells = new[] { new List<int>(), new List<int>() };
            var mouths = new List<int>();

            // The upper surface, bulging between the ribs.
            int upper = vertices.Count;
            for (int i = 0; i < spanStations; i++)
            {
                for (int k = 0; k <= ChordStations; k++)
                {
                    float c = ChordAt(k);
                    vertices.Add(Point(SpanAt(i), c, Thickness * UpperShare(c) * (1.0f + Bulge * BetweenRibs(i))));
                }
            }
            // The lower surface, flat.
            int lower = vertices.Count;
            for (int i = 0; i < spanStations; i++)
            {
                for (int k = 0; k <= ChordStations; k++)
                {
                    vertices.Add(Point(SpanAt(i), ChordAt(k), 0.0f));
                }
            }
            int Up(int i, int k) => upper + i * (ChordStations + 1) + k;
            int Down(int i, int k) => lower + i * (ChordStations + 1) + k;

            for (int i = 0; i < spanStations - 1; i++)
            {
                List<int> into = cells[(i / StationsPerCell) % 2];
                Vector3 outward = ArchNormal(0.5f * (SpanAt(i) + SpanAt(i + 1)));
                for (int k = 0; k < ChordStations; k++)
                {
                    Quad(vertices, into, Up(i, k), Up(i + 1, k), Up(i + 1, k + 1), Up(i, k + 1), outward);
                    Quad(vertices, into, Down(i, k), Down(i + 1, k), Down(i + 1, k + 1), Down(i, k + 1), -outward);
                }
            }
            // The tips' walls, each the colour of its cell.
            for (int k = 0; k < ChordStations; k++)
            {
                Vector3 leftOut = -Tangent(-1.0f), rightOut = Tangent(1.0f);
                int last = spanStations - 1;
                QuadOwn(vertices, cells[0], Up(0, k), Up(0, k + 1), Down(0, k + 1), Down(0, k), leftOut);
                QuadOwn(vertices, cells[(Cells - 1) % 2], Up(last, k), Up(last, k + 1), Down(last, k + 1), Down(last, k), rightOut);
            }
            // The mouths along the nose.
            for (int i = 0; i < spanStations - 1; i++)
            {
                QuadOwn(vertices, mouths, Up(i, 0), Up(i + 1, 0), Down(i + 1, 0), Down(i, 0), Vector3.forward);
            }

            // The surfaces are smooth across the cells; the tips and the mouths have their own corners, so the edges round them stay sharp.
            var mesh = new Mesh { name = "ParachuteCanopy" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(cells[0], ColourA);
            mesh.SetTriangles(cells[1], ColourB);
            mesh.SetTriangles(mouths, Mouths);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Tangent(float s)
        {
            float phi = s * ArcHalfDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(phi), -Mathf.Sin(phi), 0.0f);
        }

        /// <summary>Two triangles a-b-c-d, wound to face <paramref name="outward"/>.</summary>
        private static void Quad(List<Vector3> v, List<int> into, int a, int b, int c, int d, Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(normal, outward) < 0.0f)
            {
                (b, d) = (d, b);
            }
            into.Add(a);
            into.Add(b);
            into.Add(c);
            into.Add(a);
            into.Add(c);
            into.Add(d);
        }

        /// <summary>A quad of its own four corners (copies of those at a, b, c, d), wound to face <paramref name="outward"/>.</summary>
        private static void QuadOwn(List<Vector3> v, List<int> into, int a, int b, int c, int d, Vector3 outward)
        {
            int first = v.Count;
            v.Add(v[a]);
            v.Add(v[b]);
            v.Add(v[c]);
            v.Add(v[d]);
            Quad(v, into, first, first + 1, first + 2, first + 3, outward);
        }

        /// <summary>Where the suspension lines of one side meet the lower surface, in the canopy's frame.</summary>
        public static List<Vector3> LineAnchors(bool right)
        {
            var anchors = new List<Vector3>();
            foreach (int rib in LineRibs)
            {
                anchors.Add(Point(RibSpan(right ? rib : Cells - rib), LineRow, 0.0f));
            }
            return anchors;
        }

        /// <summary>Where one side's steering lines leave the tail, in the canopy's frame.</summary>
        public static List<Vector3> SteeringAnchors(bool right)
        {
            var anchors = new List<Vector3>();
            foreach (int rib in SteeringRibs)
            {
                anchors.Add(Point(RibSpan(right ? rib : Cells - rib), 1.0f, 0.0f));
            }
            return anchors;
        }

        private static float RibSpan(int rib) => -1.0f + 2.0f * rib / Cells;
    }
}

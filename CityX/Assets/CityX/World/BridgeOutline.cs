using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>One stretch of a bridge's edge where a rail or a corridor's wall stands (world): <see cref="P"/> to <see cref="Q"/> at the walking surface.</summary>
    public readonly struct BridgeEdgeStretch
    {
        public readonly Vector3 P, Q;

        /// <summary>Level, toward the deck's middle.</summary>
        public readonly Vector3 Inward;

        /// <summary>0 the walking line's left edge, 1 its right.</summary>
        public readonly int Side;

        /// <summary>Which stretch of the path it runs along.</summary>
        public readonly int Stretch;

        public BridgeEdgeStretch(Vector3 p, Vector3 q, Vector3 inward, int side, int stretch)
        {
            P = p;
            Q = q;
            Inward = inward;
            Side = side;
            Stretch = stretch;
        }
    }

    /// <summary>
    /// A footbridge's shape in plan, in the world: its walking line pushed a little into each wall it meets, its two edges (mitred at each turn, cut
    /// along the walls at its ends), and the stretches of those edges a rail or a corridor's wall stands on (open where a street stair leaves the
    /// deck). What CityMeshBuilder draws a bridge from and what its glass is cut from (BridgeGlass), so the two agree.
    /// </summary>
    public static class BridgeOutline
    {
        /// <summary>How far the deck reaches into each wall it meets, so no gap opens at the threshold.</summary>
        public const float IntoWall = 0.06f;

        /// <summary>The walking line and its two edges (world), point for point.</summary>
        public static void Of(in Footbridge b, out Vector3[] centre, out Vector3[] left, out Vector3[] right)
        {
            Vector3[] points = b.Path.Points;
            int n = points.Length;
            centre = (Vector3[])points.Clone();
            centre[0] -= FlatOf(centre[1] - centre[0]).normalized * IntoWall;
            centre[n - 1] += FlatOf(centre[n - 1] - centre[n - 2]).normalized * IntoWall;
            Edges(centre, Direction(b.Path.TangentA), Direction(b.Path.TangentB), b.Width * 0.5f, out left, out right);
        }

        /// <summary>
        /// The stretches of both edges a rail or a corridor's wall stands on, left edge first: where a street stair leaves the deck by A's wall,
        /// that side is open for <see cref="Footbridges.StreetStairOpening"/> out from the wall.
        /// </summary>
        public static void EdgeStretches(in Footbridge b, Vector3[] centre, Vector3[] left, Vector3[] right, List<BridgeEdgeStretch> into)
        {
            int n = centre.Length;
            Vector3 outA = FlatOf(centre[1] - centre[0]).normalized;
            Vector3 stairSide = b.Stair != null ? Direction(b.Stair.Along) : Vector3.zero;
            for (int side = 0; side < 2; side++)
            {
                bool isLeft = side == 0;
                Vector3[] edge = isLeft ? left : right;
                bool open = b.Stair != null && Vector3.Dot(isLeft ? LeftOf(outA) : -LeftOf(outA), stairSide) > 0.5f;
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector3 p = edge[i], q = edge[i + 1];
                    Vector3 inward = (isLeft ? -1.0f : 1.0f) * LeftOf(FlatOf(q - p).normalized);
                    if (open)
                    {
                        // Clip off the stretch within the opening of A's wall.
                        float dp = Vector3.Dot(p - centre[0], outA), dq = Vector3.Dot(q - centre[0], outA);
                        float opening = Footbridges.StreetStairOpening;
                        if (dp < opening && dq < opening) continue;
                        if (dp < opening) p = Vector3.Lerp(p, q, (opening - dp) / (dq - dp));
                    }
                    into.Add(new BridgeEdgeStretch(p, q, inward, side, i));
                }
            }
        }

        /// <summary>How many bays a corridor's wall <paramref name="length"/> metres long is glazed (or opened) in, between its mullions or piers.</summary>
        public static int CorridorBays(float length) => Mathf.Max(1, Mathf.RoundToInt(length / Footbridges.CorridorBay));

        /// <summary>The horizontal unit to the left of a direction in plan.</summary>
        public static Vector3 LeftOf(Vector3 d) => new Vector3(-d.z, 0.0f, d.x);

        public static Vector3 FlatOf(Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        private static Vector3 Direction(Vector2 plan) => new Vector3(plan.x, 0.0f, plan.y).normalized;

        /// <summary>
        /// A walking line's edges at <paramref name="half"/> either side: mitred at each turn (never more than twice the half width out), and at each
        /// end cut along the wall it meets (<paramref name="tangentA"/>, <paramref name="tangentB"/>; zero for an end square to the line).
        /// </summary>
        private static void Edges(Vector3[] c, Vector3 tangentA, Vector3 tangentB, float half, out Vector3[] left, out Vector3[] right)
        {
            int n = c.Length;
            left = new Vector3[n];
            right = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 dIn = i > 0 ? FlatOf(c[i] - c[i - 1]).normalized : Vector3.zero;
                Vector3 dOut = i + 1 < n ? FlatOf(c[i + 1] - c[i]).normalized : Vector3.zero;
                Vector3 offset;
                if (i == 0 || i == n - 1)
                {
                    Vector3 d = i == 0 ? dOut : dIn;
                    Vector3 s = LeftOf(d);
                    Vector3 t = i == 0 ? tangentA : tangentB;
                    if (t.sqrMagnitude < 1e-6f)
                    {
                        offset = s * half;
                    }
                    else
                    {
                        float k = Vector3.Dot(t, s);
                        if (Mathf.Abs(k) < 0.35f) k = k < 0.0f ? -0.35f : 0.35f;
                        offset = t * (half / k);
                    }
                }
                else
                {
                    Vector3 m = LeftOf(dIn) + LeftOf(dOut);
                    if (m.sqrMagnitude < 1e-6f) m = LeftOf(dOut);
                    m.Normalize();
                    offset = m * (half / Mathf.Max(0.5f, Vector3.Dot(m, LeftOf(dOut))));
                }
                left[i] = c[i] + offset;
                right[i] = c[i] - offset;
            }
        }
    }
}

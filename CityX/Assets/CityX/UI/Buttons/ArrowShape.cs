using System.Collections.Generic;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The arrow buttons' shapes (owner, 2026-11-10: the creator's ENTER THE CITY and BACK remade "something like these images": a fat green
    /// arrow pointing on, labelled PROCEED, and a red one curling back, labelled BACK). Each shape is a list of convex pieces that meet edge
    /// to edge and never overlap, in their own units; <see cref="ArrowGraphic"/> fits them into a button and draws them. Pure: tested
    /// without a canvas.
    /// </summary>
    public static class ArrowShape
    {
        // --- the straight arrow, pointing right: its length 10 units ---

        public const float StraightLength = 10.0f;
        public const float StraightShaftHalfHeight = 1.15f;
        public const float StraightHeadHalfHeight = 2.17f;
        public const float StraightHeadLength = 2.7f;

        /// <summary>The arrow pointing right: the shaft, then the head on its end.</summary>
        public static List<Vector2[]> Straight()
        {
            float back = StraightLength - StraightHeadLength;
            return new List<Vector2[]>
            {
                new[]
                {
                    new Vector2(0.0f, -StraightShaftHalfHeight), new Vector2(0.0f, StraightShaftHalfHeight),
                    new Vector2(back, StraightShaftHalfHeight), new Vector2(back, -StraightShaftHalfHeight),
                },
                new[] { new Vector2(back, -StraightHeadHalfHeight), new Vector2(back, StraightHeadHalfHeight), new Vector2(StraightLength, 0.0f) },
            };
        }

        // --- the curled arrow: a band round a centre, turning anticlockwise to a head pointing down and back to the left ---

        public const float CurveInner = 3.0f;
        public const float CurveOuter = 4.6f;
        public const float CurveFromDegrees = 30.0f;
        public const float CurveToDegrees = 122.0f;
        public const float CurveHeadOverhang = 0.75f;
        public const float CurveHeadLength = 1.8f;
        private const int CurveSegments = 18;

        /// <summary>Where the band's middle runs: its radius.</summary>
        public static float CurveMiddle => 0.5f * (CurveInner + CurveOuter);

        /// <summary>The curled arrow: the band in short pieces, then the head.</summary>
        public static List<Vector2[]> Curved()
        {
            var parts = new List<Vector2[]>();
            for (int i = 0; i < CurveSegments; i++)
            {
                float a0 = Mathf.Lerp(CurveFromDegrees, CurveToDegrees, i / (float)CurveSegments) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(CurveFromDegrees, CurveToDegrees, (i + 1) / (float)CurveSegments) * Mathf.Deg2Rad;
                parts.Add(new[] { Polar(CurveInner, a0), Polar(CurveOuter, a0), Polar(CurveOuter, a1), Polar(CurveInner, a1) });
            }
            float end = CurveToDegrees * Mathf.Deg2Rad;
            var along = new Vector2(-Mathf.Sin(end), Mathf.Cos(end));   // the way it turns, at its end
            parts.Add(new[]
            {
                Polar(CurveInner - CurveHeadOverhang, end), Polar(CurveOuter + CurveHeadOverhang, end), Polar(CurveMiddle, end) + along * CurveHeadLength,
            });
            return parts;
        }

        /// <summary>A point <paramref name="radius"/> from the curl's centre at <paramref name="radians"/>.</summary>
        public static Vector2 Polar(float radius, float radians) => new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;

        // --- outlines, the gloss, hits ---

        /// <summary>
        /// A convex piece grown outward by <paramref name="by"/> with round corners (<paramref name="cornerSteps"/> points a corner): the pieces of
        /// a shape grown alike overlap into one outline of the whole.
        /// </summary>
        public static Vector2[] Grown(Vector2[] convex, float by, int cornerSteps = 4)
        {
            if (by <= 0.0f)
            {
                return convex;
            }
            bool clockwise = SignedArea(convex) < 0.0f;
            var points = new List<Vector2>();
            int n = convex.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 previous = convex[(i - 1 + n) % n], here = convex[i], next = convex[(i + 1) % n];
                Vector2 inNormal = OutwardNormal(previous, here, clockwise), outNormal = OutwardNormal(here, next, clockwise);
                float from = Mathf.Atan2(inNormal.y, inNormal.x), to = Mathf.Atan2(outNormal.y, outNormal.x);
                float turn = Mathf.DeltaAngle(from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                for (int s = 0; s <= cornerSteps; s++)
                {
                    float a = from + turn * s / cornerSteps;
                    points.Add(here + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * by);
                }
            }
            return points.ToArray();
        }

        private static Vector2 OutwardNormal(Vector2 a, Vector2 b, bool clockwise)
        {
            Vector2 edge = (b - a).normalized;
            return clockwise ? new Vector2(-edge.y, edge.x) : new Vector2(edge.y, -edge.x);
        }

        public static float SignedArea(Vector2[] polygon)
        {
            float area = 0.0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                area += a.x * b.y - b.x * a.y;
            }
            return 0.5f * area;
        }

        /// <summary>The part of a convex piece above <paramref name="y"/> (none left: an empty array).</summary>
        public static Vector2[] Above(Vector2[] convex, float y)
        {
            var kept = new List<Vector2>();
            for (int i = 0; i < convex.Length; i++)
            {
                Vector2 a = convex[i], b = convex[(i + 1) % convex.Length];
                bool aIn = a.y >= y, bIn = b.y >= y;
                if (aIn)
                {
                    kept.Add(a);
                }
                if (aIn != bIn)
                {
                    kept.Add(Vector2.Lerp(a, b, (y - a.y) / (b.y - a.y)));
                }
            }
            return kept.ToArray();
        }

        /// <summary>Whether <paramref name="point"/> is inside any of the pieces, each grown by <paramref name="by"/>.</summary>
        public static bool Contains(List<Vector2[]> parts, Vector2 point, float by = 0.0f)
        {
            foreach (Vector2[] part in parts)
            {
                if (InsideConvex(Grown(part, by, 2), point))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool InsideConvex(Vector2[] convex, Vector2 point)
        {
            float side = 0.0f;
            for (int i = 0; i < convex.Length; i++)
            {
                Vector2 a = convex[i], b = convex[(i + 1) % convex.Length];
                float cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                if (Mathf.Abs(cross) < 1e-6f)
                {
                    continue;
                }
                if (side == 0.0f)
                {
                    side = Mathf.Sign(cross);
                }
                else if (Mathf.Sign(cross) != side)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The box round every piece, each grown by <paramref name="by"/>.</summary>
        public static Rect Bounds(List<Vector2[]> parts, float by)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2[] part in parts)
            {
                foreach (Vector2 p in part)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
            return Rect.MinMaxRect(min.x - by, min.y - by, max.x + by, max.y + by);
        }
    }
}

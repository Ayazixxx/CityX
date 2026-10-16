using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// A 2D point or vector in metres. World X is X, world Z is Y.
    ///
    /// ITS OWN STRUCT, NOT UnityEngine.Vector2 (a float) OR System.Numerics. The city plan is grown by a
    /// chaotic process -- one road placed a hair differently changes every road after it -- so every machine
    /// has to compute bit-identical numbers. That rules out floats that an engine may fuse, and every
    /// transcendental (sin, cos, atan2, pow): the plan code uses double +, -, *, / and sqrt only, which IEEE 754
    /// defines exactly. See PlanConfig for the rest of the determinism rules.
    /// </summary>
    public struct Vec2
    {
        public double X;
        public double Y;

        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, double s) => new Vec2(a.X / s, a.Y / s);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

        public double SqrLength => X * X + Y * Y;
        public double Length => Math.Sqrt(X * X + Y * Y);

        public Vec2 Normalized
        {
            get
            {
                double l = Length;
                return l > 1e-12 ? new Vec2(X / l, Y / l) : new Vec2(1.0, 0.0);
            }
        }

        /// <summary>This vector turned 90 degrees counter-clockwise.</summary>
        public Vec2 Perp => new Vec2(-Y, X);

        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

        /// <summary>
        /// This vector turned by an angle given as its cosine and sine -- from a table of literals, never from
        /// Math.Cos/Math.Sin, which are not guaranteed bit-identical across platforms.
        /// </summary>
        public Vec2 Rotated(double cos, double sin) => new Vec2(X * cos - Y * sin, X * sin + Y * cos);

        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }

    /// <summary>Plane geometry for the city plan. Pure arithmetic, no allocations beyond the lists it is given.</summary>
    public static class PlanMath
    {
        /// <summary>
        /// Rotations by a fixed set of angles, as (cos, sin) literals. Index 0..: 0, 5, 10, ... 90 degrees. The
        /// negative of an angle is the same cosine and the negated sine, so one table serves both directions.
        /// </summary>
        public static readonly double[] CosTable =
        {
            1.0, 0.9961946980917455, 0.984807753012208, 0.9659258262890683, 0.9396926207859084,
            0.9063077870366499, 0.8660254037844387, 0.8191520442889918, 0.7660444431189780, 0.7071067811865476,
            0.6427876096865394, 0.5735764363510461, 0.5, 0.4226182617406994, 0.3420201433256688,
            0.2588190451025208, 0.1736481776669304, 0.0871557427476582, 0.0,
        };

        public static readonly double[] SinTable =
        {
            0.0, 0.0871557427476582, 0.1736481776669304, 0.2588190451025207, 0.3420201433256687,
            0.4226182617406994, 0.5, 0.5735764363510461, 0.6427876096865394, 0.7071067811865476,
            0.7660444431189780, 0.8191520442889918, 0.8660254037844386, 0.9063077870366499, 0.9396926207859084,
            0.9659258262890683, 0.984807753012208, 0.9961946980917455, 1.0,
        };

        /// <summary>A direction rotated by <paramref name="steps"/> multiples of 5 degrees (negative turns clockwise).</summary>
        public static Vec2 RotateSteps(Vec2 v, int steps)
        {
            int index = steps < 0 ? -steps : steps;
            if (index > 18)
            {
                index = 18;
            }
            double sin = steps < 0 ? -SinTable[index] : SinTable[index];
            return v.Rotated(CosTable[index], sin);
        }

        /// <summary>
        /// A number that increases monotonically with the counter-clockwise angle of a direction, 0 to 4,
        /// computed with no trig. Sorting by this is sorting by angle.
        /// </summary>
        public static double PseudoAngle(double dx, double dy)
        {
            double p = dx / (Math.Abs(dx) + Math.Abs(dy) + 1e-300);   // -1 .. 1
            return dy < 0.0 ? 3.0 + p : 1.0 - p;
        }

        public static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>
        /// Intersection of segments a-b and c-d. t and u are the fractions along each (0 at the first point).
        /// False when parallel or when they miss.
        /// </summary>
        public static bool SegmentIntersect(Vec2 a, Vec2 b, Vec2 c, Vec2 d, out double t, out double u)
        {
            Vec2 r = b - a;
            Vec2 s = d - c;
            double denom = Vec2.Cross(r, s);
            t = 0.0;
            u = 0.0;
            if (Math.Abs(denom) < 1e-12)
            {
                return false;
            }
            Vec2 ca = c - a;
            t = Vec2.Cross(ca, s) / denom;
            u = Vec2.Cross(ca, r) / denom;
            return t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0;
        }

        /// <summary>Distance from a point to a segment; <paramref name="t"/> is where along it the nearest point is (0..1).</summary>
        public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b, out double t)
        {
            Vec2 ab = b - a;
            double len2 = ab.SqrLength;
            t = len2 < 1e-12 ? 0.0 : Clamp01(Vec2.Dot(p - a, ab) / len2);
            Vec2 q = a + ab * t;
            return (p - q).Length;
        }

        /// <summary>Signed area: positive for counter-clockwise polygons.</summary>
        public static double SignedArea(IList<Vec2> poly)
        {
            double sum = 0.0;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vec2 a = poly[i];
                Vec2 b = poly[(i + 1) % n];
                sum += a.X * b.Y - b.X * a.Y;
            }
            return sum * 0.5;
        }

        public static Vec2 Centroid(IList<Vec2> poly)
        {
            double area = SignedArea(poly);
            int n = poly.Count;
            if (Math.Abs(area) < 1e-9)
            {
                var mean = new Vec2(0.0, 0.0);
                for (int i = 0; i < n; i++)
                {
                    mean += poly[i];
                }
                return n > 0 ? mean / n : mean;
            }
            double cx = 0.0, cy = 0.0;
            for (int i = 0; i < n; i++)
            {
                Vec2 a = poly[i];
                Vec2 b = poly[(i + 1) % n];
                double cross = a.X * b.Y - b.X * a.Y;
                cx += (a.X + b.X) * cross;
                cy += (a.Y + b.Y) * cross;
            }
            double k = 1.0 / (6.0 * area);
            return new Vec2(cx * k, cy * k);
        }

        public static bool PointInPolygon(Vec2 p, IList<Vec2> poly)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vec2 a = poly[i];
                Vec2 b = poly[j];
                if ((a.Y > p.Y) != (b.Y > p.Y) &&
                    p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Cuts a simple polygon (either winding, convex or not) into triangles by ear clipping: three vertex indices per triangle, wound like the polygon.
        /// Plain arithmetic only, so every machine cuts a block the same way. A polygon too degenerate to clip completely is finished as a fan.
        /// </summary>
        public static int[] Triangulate(IList<Vec2> poly)
        {
            int n = poly.Count;
            if (n < 3)
            {
                return new int[0];
            }
            double sign = SignedArea(poly) >= 0.0 ? 1.0 : -1.0;
            var remaining = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                // A vertex on top of the one before it (a collapsed edge, see InsetPolygon) is no corner: left out, or it would sit on every
                // ear's corner and block it.
                Vec2 last = poly[remaining.Count > 0 ? remaining[remaining.Count - 1] : (n - 1)];
                if (remaining.Count > 0 && (poly[i] - last).Length < 1e-7)
                {
                    continue;
                }
                remaining.Add(i);
            }
            if (remaining.Count > 1 && (poly[remaining[0]] - poly[remaining[remaining.Count - 1]]).Length < 1e-7)
            {
                remaining.RemoveAt(remaining.Count - 1);
            }
            if (remaining.Count < 3)
            {
                return new int[0];
            }
            var triangles = new List<int>((n - 2) * 3);
            int guard = 0;
            while (remaining.Count > 3 && guard++ < n * n)
            {
                bool clipped = false;
                for (int k = 0; k < remaining.Count; k++)
                {
                    int ia = remaining[(k + remaining.Count - 1) % remaining.Count];
                    int ib = remaining[k];
                    int ic = remaining[(k + 1) % remaining.Count];
                    Vec2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if (Vec2.Cross(b - a, c - b) * sign <= 1e-12)
                    {
                        continue;   // a reflex (or flat) corner is not an ear
                    }
                    bool empty = true;
                    for (int m = 0; m < remaining.Count && empty; m++)
                    {
                        int im = remaining[m];
                        if (im == ia || im == ib || im == ic)
                        {
                            continue;
                        }
                        empty = !InTriangle(poly[im], a, b, c, sign);
                    }
                    if (!empty)
                    {
                        continue;
                    }
                    triangles.Add(ia);
                    triangles.Add(ib);
                    triangles.Add(ic);
                    remaining.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    break;
                }
            }
            for (int k = 1; k + 1 < remaining.Count; k++)
            {
                triangles.Add(remaining[0]);
                triangles.Add(remaining[k]);
                triangles.Add(remaining[k + 1]);
            }
            return triangles.ToArray();
        }

        /// <summary>Whether p is inside (or on the edge of) the triangle a, b, c wound the way <paramref name="sign"/> says.</summary>
        private static bool InTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c, double sign)
        {
            return Vec2.Cross(b - a, p - a) * sign >= 0.0
                && Vec2.Cross(c - b, p - b) * sign >= 0.0
                && Vec2.Cross(a - c, p - c) * sign >= 0.0;
        }

        /// <summary>Andrew's monotone chain: the convex hull of a few points, counter-clockwise. Arithmetic only.</summary>
        public static Vec2[] ConvexHull(List<Vec2> points)
        {
            points.Sort((a, b) =>
            {
                int c = a.X.CompareTo(b.X);
                return c != 0 ? c : a.Y.CompareTo(b.Y);
            });
            int n = points.Count;
            if (n < 3)
            {
                return points.ToArray();
            }
            var hull = new Vec2[2 * n];
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                while (k >= 2 && Vec2.Cross(hull[k - 1] - hull[k - 2], points[i] - hull[k - 2]) <= 0.0) k--;
                hull[k++] = points[i];
            }
            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && Vec2.Cross(hull[k - 1] - hull[k - 2], points[i] - hull[k - 2]) <= 0.0) k--;
                hull[k++] = points[i];
            }
            var result = new Vec2[k - 1];
            System.Array.Copy(hull, result, k - 1);
            return result;
        }

        /// <summary>Whether two polygons overlap: a corner of either inside the other, or an edge of one crossing an edge of the other.</summary>
        public static bool PolygonsIntersect(IList<Vec2> a, IList<Vec2> b)
        {
            for (int i = 0; i < a.Count; i++)
            {
                if (PointInPolygon(a[i], b)) return true;
            }
            for (int i = 0; i < b.Count; i++)
            {
                if (PointInPolygon(b[i], a)) return true;
            }
            int na = a.Count, nb = b.Count;
            for (int i = 0; i < na; i++)
            {
                Vec2 p = a[i], q = a[(i + 1) % na];
                for (int j = 0; j < nb; j++)
                {
                    if (SegmentIntersect(p, q, b[j], b[(j + 1) % nb], out double t, out double u)
                        && t > 1e-9 && t < 1.0 - 1e-9 && u > 1e-9 && u < 1.0 - 1e-9)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>True if any two non-adjacent edges of the polygon cross.</summary>
        public static bool SelfIntersects(IList<Vec2> poly)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vec2 a = poly[i];
                Vec2 b = poly[(i + 1) % n];
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1)
                    {
                        continue;
                    }
                    Vec2 c = poly[j];
                    Vec2 d = poly[(j + 1) % n];
                    if (SegmentIntersect(a, b, c, d, out double t, out double u) &&
                        t > 1e-6 && t < 1.0 - 1e-6 && u > 1e-6 && u < 1.0 - 1e-6)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Moves every edge of a counter-clockwise polygon inwards by its own distance, joining the moved edges
        /// at their intersections (a mitre). Returns false if the result folds over itself or vanishes -- the
        /// caller then drops the block. Distances are per edge, edge i running from vertex i to vertex i+1.
        /// </summary>
        public static bool InsetPolygon(IList<Vec2> poly, IList<double> distances, List<Vec2> result, out List<int> sourceEdge)
        {
            result.Clear();
            sourceEdge = null;
            int n = poly.Count;
            if (n < 3)
            {
                return false;
            }

            var lineP = new Vec2[n];
            var lineD = new Vec2[n];
            for (int i = 0; i < n; i++)
            {
                Vec2 a = poly[i];
                Vec2 b = poly[(i + 1) % n];
                Vec2 dir = (b - a).Normalized;
                // Counter-clockwise polygon: the interior is on the left of each edge.
                lineP[i] = a + dir.Perp * distances[i];
                lineD[i] = dir;
            }

            for (int i = 0; i < n; i++)
            {
                int prev = (i + n - 1) % n;
                // Vertex i joins edge prev (ending at i) and edge i (starting at i).
                double denom = Vec2.Cross(lineD[prev], lineD[i]);
                Vec2 v;
                if (Math.Abs(denom) < 1e-9)
                {
                    v = lineP[i];   // parallel: just step along the new edge's own start
                }
                else
                {
                    double t = Vec2.Cross(lineP[i] - lineP[prev], lineD[i]) / denom;
                    v = lineP[prev] + lineD[prev] * t;
                    // Limit the mitre: a very sharp corner would otherwise throw the vertex far away.
                    Vec2 origin = poly[i];
                    double limit = 3.0 * Math.Max(distances[prev], distances[i]) + 1.0;
                    if ((v - origin).Length > limit * 2.5)
                    {
                        return false;
                    }
                }
                result.Add(v);
            }

            // A moved edge shorter than the shift of its ends comes out BACKWARDS: at a sharp corner the mitre slides the vertex a long way
            // along the edge, and when the edge is shorter than that (a block corner cut off by a stub of road three metres long) its two ends
            // cross over. The result is folded -- a lobe wound the wrong way -- and when the fold lies flat along the next edge neither the
            // area check nor the crossing check below sees it (seed 58203917 at the classic scale: a kerb line whose paving triangles
            // overlapped by a tenth, BlockPavingTests). Such an edge is collapsed: both its ends go to where the edges either side of it meet,
            // a zero-length edge the pavement ring draws as nothing and Triangulate skips; the count stays one vertex per road corner, which
            // the ring (CitySurfaceBuilder.BuildPavement pairs kerb, lot line and road class by index) relies on.
            for (int pass = 0; pass < n; pass++)
            {
                bool folded = false;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Vec2 edge = result[j] - result[i];
                    if (Vec2.Dot(edge, lineD[i]) >= 0.0)
                    {
                        continue;   // runs the way its road edge does (or has no length)
                    }
                    int prev = (i + n - 1) % n;
                    double denom = Vec2.Cross(lineD[prev], lineD[j]);
                    if (Math.Abs(denom) < 1e-9)
                    {
                        return false;   // its neighbours are parallel: nowhere for the corner to go
                    }
                    double t = Vec2.Cross(lineP[j] - lineP[prev], lineD[j]) / denom;
                    Vec2 meet = lineP[prev] + lineD[prev] * t;
                    double limit = 3.0 * Math.Max(distances[prev], Math.Max(distances[i], distances[j])) + 1.0;
                    if ((meet - poly[i]).Length > limit * 2.5)
                    {
                        return false;
                    }
                    result[i] = meet;
                    result[j] = meet;
                    folded = true;
                }
                if (!folded)
                {
                    break;
                }
            }

            double before = SignedArea(poly);
            double after = SignedArea(result);
            if (after <= 1.0 || after > before || SelfIntersects(result))
            {
                return false;
            }

            sourceEdge = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                sourceEdge.Add(i);
            }
            return true;
        }
    }
}

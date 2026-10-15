using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>One piece of a broken pane: a convex polygon in the pane's own frame, and where in the break it came from.</summary>
    public readonly struct GlassPiece
    {
        /// <summary>The outline, counter-clockwise, metres from the pane's bottom-left corner (x across, y up).</summary>
        public readonly Vector2[] Corners;

        /// <summary>Which ring of the break: 0 is the star of slivers round the blow, higher is further out.</summary>
        public readonly int Ring;

        /// <summary>True when it touches the frame: such a piece hangs on in the putty a moment before it drops.</summary>
        public readonly bool OnTheFrame;

        public GlassPiece(Vector2[] corners, int ring, bool onTheFrame)
        {
            Corners = corners;
            Ring = ring;
            OnTheFrame = onTheFrame;
        }
    }

    /// <summary>
    /// HOW A WINDOW BREAKS APART (2026-10-15, the owner: "research how we can improve the window fracturing. would procedurally window
    /// fracturing effect performance and be not worth it? ... either way improve upon the window fracturing system"). Struck glass breaks in a
    /// pattern every glazier knows: RADIAL cracks run out from the blow, CONCENTRIC rings cross them, so the pieces are slivers round the blow
    /// and big shards towards the frame. This cuts a pane (a width x height rectangle, origin at its bottom-left corner) into that pattern round
    /// the impact, as convex polygons that cover it exactly once.
    ///
    /// AT THE MOMENT OF THE BREAK, NOT PRE-FRACTURED: the pattern has to centre on where the blow landed, and for a flat pane it is 2D clipping
    /// of a few dozen polygons -- microseconds (GlassFractureBenchTests measures it). What costs is what the pieces become (meshes, bodies,
    /// draws): View's GlassFractureBurst pools those and caps them.
    /// </summary>
    public static class GlassFracture
    {
        /// <summary>
        /// The physics layer broken glass falls on (an unnamed spare): it lands on the world but never on another piece, and bodies walk through it
        /// (<see cref="LetThrough"/>) -- a piece lying on a pavement tripped the body, and one standing in a broken window's frame held it 0.58 m up.
        /// </summary>
        public const int ShardLayer = 29;

        /// <summary>Everything but broken glass, for a ray that is after the world (a camera's, a probe's).</summary>
        public const int AllButShards = ~(1 << ShardLayer);

        /// <summary>Lets a body's controller walk through broken glass instead of over it.</summary>
        public static void LetThrough(CharacterController controller)
        {
            if (controller != null)
            {
                controller.excludeLayers |= 1 << ShardLayer;
            }
        }

        /// <summary>How many radial cracks run out from the impact, at least and at most.</summary>
        public const int MinRadials = 7;

        public const int MaxRadials = 11;

        /// <summary>The rings' radii, metres from the impact, before jitter: slivers near the blow, bigger further out.</summary>
        public static readonly float[] RingRadii = { 0.1f, 0.3f, 0.7f };

        /// <summary>How much a ring's radius wanders from crack to crack (a fraction), so the rings are not circles.</summary>
        public const float RingJitter = 0.3f;

        /// <summary>A piece bigger than this, square metres (the outer corners of a big pane), is cut across again, up to twice.</summary>
        public const float MaxPieceArea = 0.35f;

        /// <summary>No more cutting across once a pane is in this many pieces (a shop window stays a few dozen, not a hundred).</summary>
        public const int MaxPieces = 56;

        /// <summary>
        /// The pieces of a <paramref name="width"/> x <paramref name="height"/> pane struck at <paramref name="impact"/> (pane coordinates,
        /// clamped into the pane). The same seed gives the same break.
        /// </summary>
        public static List<GlassPiece> Fracture(float width, float height, Vector2 impact, int seed)
        {
            var pieces = new List<GlassPiece>();
            if (width <= 1e-3f || height <= 1e-3f)
            {
                return pieces;
            }
            impact = new Vector2(Mathf.Clamp(impact.x, 0.0f, width), Mathf.Clamp(impact.y, 0.0f, height));
            uint state = unchecked((uint)seed * 2654435761u + 0x7F4A7C15u);
            if (state == 0)
            {
                state = 1;
            }

            // The radial cracks: evenly spread, each nudged, so no wedge is much wider than its share.
            int radials = Mathf.Clamp(MinRadials + (int)(Unit(ref state) * (MaxRadials - MinRadials + 1)), MinRadials, MaxRadials);
            var angles = new float[radials];
            float start = Unit(ref state) * Mathf.PI * 2.0f;
            float step = Mathf.PI * 2.0f / radials;
            for (int i = 0; i < radials; i++)
            {
                angles[i] = start + step * i + (Unit(ref state) - 0.5f) * step * 0.5f;
            }

            // Where each ring crosses each crack (a jittered radius per crossing); past the last ring, far beyond the pane.
            float reach = Mathf.Sqrt(width * width + height * height) * 2.0f + 1.0f;
            int rings = RingRadii.Length;
            var radius = new float[rings + 1, radials];
            for (int k = 0; k < rings; k++)
            {
                for (int i = 0; i < radials; i++)
                {
                    radius[k, i] = RingRadii[k] * (1.0f + (Unit(ref state) * 2.0f - 1.0f) * RingJitter);
                }
            }
            for (int i = 0; i < radials; i++)
            {
                radius[rings, i] = reach;
            }

            var rectangle = new[] { new Vector2(0.0f, 0.0f), new Vector2(width, 0.0f), new Vector2(width, height), new Vector2(0.0f, height) };
            // Every wedge of every ring is at most one piece before any cutting across; the cuts may add the rest, up to MaxPieces.
            int spare = MaxPieces - radials * (rings + 1);
            for (int i = 0; i < radials; i++)
            {
                int j = (i + 1) % radials;
                Vector2 a = new Vector2(Mathf.Cos(angles[i]), Mathf.Sin(angles[i]));
                Vector2 b = new Vector2(Mathf.Cos(angles[j]), Mathf.Sin(angles[j]));
                for (int k = 0; k <= rings; k++)
                {
                    // The innermost piece is a sliver with its tip on the blow; the rest lie between two rings.
                    Vector2[] wedge = k == 0
                        ? new[] { impact, impact + a * radius[0, i], impact + b * radius[0, j] }
                        : new[] { impact + a * radius[k - 1, i], impact + a * radius[k, i], impact + b * radius[k, j], impact + b * radius[k - 1, j] };
                    Vector2[] piece = Clip(Ccw(wedge), rectangle);
                    if (piece != null && Area(piece) > 1e-6f)
                    {
                        AddCut(pieces, piece, k, width, height, ref state, ref spare, 2);
                    }
                }
            }
            return pieces;
        }

        /// <summary>Adds a piece, first cut across (once, or twice for a very big one) when it is too big and the pane is not in too many already.</summary>
        private static void AddCut(List<GlassPiece> into, Vector2[] piece, int ring, float width, float height, ref uint state, ref int spare,
            int cutsLeft)
        {
            if (cutsLeft <= 0 || Area(piece) <= MaxPieceArea || spare <= 0)
            {
                into.Add(new GlassPiece(piece, ring, TouchesTheFrame(piece, width, height)));
                return;
            }
            // A crack across it: through a point near its middle, at a random angle.
            Vector2 middle = Centroid(piece);
            float angle = Unit(ref state) * Mathf.PI;
            var normal = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2[] one = ClipHalfPlane(piece, middle, normal);
            Vector2[] two = ClipHalfPlane(piece, middle, -normal);
            if (one == null || two == null || Area(one) < 1e-4f || Area(two) < 1e-4f)
            {
                into.Add(new GlassPiece(piece, ring, TouchesTheFrame(piece, width, height)));
                return;
            }
            spare--;   // one piece becomes two
            AddCut(into, one, ring, width, height, ref state, ref spare, cutsLeft - 1);
            AddCut(into, two, ring, width, height, ref state, ref spare, cutsLeft - 1);
        }

        private static bool TouchesTheFrame(Vector2[] piece, float width, float height)
        {
            const float near = 0.002f;
            foreach (Vector2 p in piece)
            {
                if (p.x <= near || p.y <= near || p.x >= width - near || p.y >= height - near)
                {
                    return true;
                }
            }
            return false;
        }

        // --- polygon helpers ---

        /// <summary>The signed area: positive for counter-clockwise.</summary>
        public static float SignedArea(Vector2[] polygon)
        {
            float sum = 0.0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                sum += a.x * b.y - b.x * a.y;
            }
            return sum * 0.5f;
        }

        public static float Area(Vector2[] polygon) => Mathf.Abs(SignedArea(polygon));

        public static Vector2 Centroid(Vector2[] polygon)
        {
            float area = SignedArea(polygon);
            if (Mathf.Abs(area) < 1e-9f)
            {
                Vector2 mean = Vector2.zero;
                foreach (Vector2 p in polygon)
                {
                    mean += p;
                }
                return mean / Mathf.Max(1, polygon.Length);
            }
            float cx = 0.0f, cy = 0.0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                float cross = a.x * b.y - b.x * a.y;
                cx += (a.x + b.x) * cross;
                cy += (a.y + b.y) * cross;
            }
            return new Vector2(cx, cy) / (6.0f * area);
        }

        private static Vector2[] Ccw(Vector2[] polygon)
        {
            if (SignedArea(polygon) >= 0.0f)
            {
                return polygon;
            }
            var reversed = (Vector2[])polygon.Clone();
            System.Array.Reverse(reversed);
            return reversed;
        }

        /// <summary>Sutherland-Hodgman: <paramref name="subject"/> cut to the convex, counter-clockwise <paramref name="clip"/>.</summary>
        private static Vector2[] Clip(Vector2[] subject, Vector2[] clip)
        {
            Vector2[] output = subject;
            for (int e = 0; e < clip.Length && output != null; e++)
            {
                Vector2 a = clip[e];
                Vector2 b = clip[(e + 1) % clip.Length];
                Vector2 edge = b - a;
                output = ClipHalfPlane(output, a, new Vector2(-edge.y, edge.x));   // inside is to the left of a counter-clockwise edge
            }
            return output;
        }

        /// <summary>The part of a polygon on the side of the line through <paramref name="point"/> that <paramref name="inward"/> points to.</summary>
        private static Vector2[] ClipHalfPlane(Vector2[] polygon, Vector2 point, Vector2 inward)
        {
            if (polygon == null || polygon.Length < 3)
            {
                return null;
            }
            var result = new List<Vector2>(polygon.Length + 2);
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Length];
                float dc = Vector2.Dot(current - point, inward);
                float dn = Vector2.Dot(next - point, inward);
                if (dc >= 0.0f)
                {
                    result.Add(current);
                }
                if ((dc >= 0.0f) != (dn >= 0.0f))
                {
                    float t = dc / (dc - dn);
                    result.Add(Vector2.Lerp(current, next, t));
                }
            }
            return result.Count >= 3 ? result.ToArray() : null;
        }

        private static float Unit(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state >> 8) / 16777216.0f;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BOTTLE'S MODELS, made from its shape (BottleShape, docs/GLASS_BOTTLE.md): the whole bottle turned from its profile as on a potter's
    /// wheel; the half a blow leaves, the same bottle ended in a ring of sharp points (<see cref="Fracture"/>); and the pieces it flies into
    /// when it shatters, the same bottle again cut into shards. All of it works on any bottle standing on its base (+Y up), the owner's model
    /// included, so a broken bottle is always the bottle it was. Pure mesh maths: BottleBuilder saves the results.
    /// </summary>
    public static class BottleMesh
    {
        /// <summary>A model as plain lists: points, which way each faces, and triangles.</summary>
        public sealed class Model
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public int TriangleCount => Triangles.Count / 3;
        }

        /// <summary>
        /// The profile turned round the bottle's middle: (radius, height) points bottom to top, joined by a smooth curve with a ring at least
        /// every <paramref name="ringEvery"/> metres, <paramref name="sides"/> round.
        /// </summary>
        public static Model Turned(Vector2[] profile, int sides, float ringEvery)
        {
            List<Vector2> curve = Smoothed(profile, ringEvery);
            var model = new Model();
            for (int i = 0; i < curve.Count; i++)
            {
                Vector2 along = curve[Mathf.Min(i + 1, curve.Count - 1)] - curve[Mathf.Max(i - 1, 0)];
                Vector2 outward = new Vector2(along.y, -along.x).normalized;
                for (int s = 0; s < sides; s++)
                {
                    float angle = s * Mathf.PI * 2.0f / sides;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    model.Vertices.Add(new Vector3(curve[i].x * cos, curve[i].y, curve[i].x * sin));
                    model.Normals.Add(new Vector3(outward.x * cos, outward.y, outward.x * sin).normalized);
                }
            }
            for (int i = 0; i + 1 < curve.Count; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = i * sides + s, b = i * sides + (s + 1) % sides, c = a + sides, d = b + sides;
                    AddFacing(model, a, c, d);
                    AddFacing(model, a, d, b);
                }
            }
            return model;
        }

        private static List<Vector2> Smoothed(Vector2[] profile, float ringEvery)
        {
            var curve = new List<Vector2>();
            for (int i = 0; i + 1 < profile.Length; i++)
            {
                Vector2 p0 = profile[Mathf.Max(i - 1, 0)], p1 = profile[i], p2 = profile[i + 1], p3 = profile[Mathf.Min(i + 2, profile.Length - 1)];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / ringEvery));
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    Vector2 point = 0.5f * ((2.0f * p1) + (-p0 + p2) * t + (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * (t * t)
                        + (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * (t * t * t));
                    curve.Add(new Vector2(Mathf.Max(0.0f, point.x), point.y));
                }
            }
            curve.Add(profile[profile.Length - 1]);
            return curve;
        }

        /// <summary>A triangle wound so it faces the way its points do (out of the bottle); a sliver with no area is left out.</summary>
        private static void AddFacing(Model model, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(model.Vertices[b] - model.Vertices[a], model.Vertices[c] - model.Vertices[a]);
            if (face.sqrMagnitude < 1e-16f)
            {
                return;
            }
            bool outward = Vector3.Dot(face, model.Normals[a] + model.Normals[b] + model.Normals[c]) >= 0.0f;
            model.Triangles.Add(a);
            model.Triangles.Add(outward ? b : c);
            model.Triangles.Add(outward ? c : b);
        }

        // ---- the break ----

        /// <summary>
        /// The broken edge's points round the bottle: each (how many degrees wide, how long as a share of the longest, where along it the tip
        /// is). No two neighbours alike, long ones beside short ones: glass breaks along its own lines, never evenly.
        /// </summary>
        private static readonly Vector3[] Points =
        {
            new Vector3(52.0f, 1.00f, 0.40f), new Vector3(34.0f, 0.45f, 0.60f), new Vector3(61.0f, 0.80f, 0.35f), new Vector3(40.0f, 0.30f, 0.50f),
            new Vector3(48.0f, 0.95f, 0.65f), new Vector3(30.0f, 0.50f, 0.45f), new Vector3(57.0f, 0.70f, 0.30f), new Vector3(38.0f, 0.35f, 0.55f),
        };

        /// <summary>
        /// How far past the break's level the glass still reaches at <paramref name="degrees"/> round the bottle, as a share of the longest
        /// point (0 in the notches between points, 1 at the longest one's tip). The sides of a point curve in a little, as a shard's do.
        /// </summary>
        public static float Fracture(float degrees)
        {
            degrees = Mathf.Repeat(degrees, 360.0f);
            foreach (Vector3 point in Points)
            {
                if (degrees < point.x)
                {
                    float u = degrees / point.x;
                    float rise = u < point.z ? u / point.z : (1.0f - u) / (1.0f - point.z);
                    return point.y * Mathf.Pow(rise, 1.35f);
                }
                degrees -= point.x;
            }
            return 0.0f;
        }

        /// <summary>
        /// The bottle from its mouth down to a break at <paramref name="level"/> above its base, its edge a ring of points up to
        /// <paramref name="longestPoint"/> long (<see cref="Fracture"/>). Open at the break: drawn from both sides, its inside shows.
        /// </summary>
        public static Model BrokenOff(Model whole, float level, float longestPoint)
        {
            var past = new float[whole.Vertices.Count];
            for (int i = 0; i < past.Length; i++)
            {
                Vector3 v = whole.Vertices[i];
                float degrees = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
                past[i] = (level - longestPoint * Fracture(degrees)) - v.y;   // negative: kept
            }
            var kept = new Model();
            var original = new Dictionary<int, int>();
            var onEdge = new Dictionary<long, int>();
            var polygon = new List<int>(4);
            for (int t = 0; t + 2 < whole.Triangles.Count; t += 3)
            {
                polygon.Clear();
                for (int k = 0; k < 3; k++)
                {
                    int from = whole.Triangles[t + k], to = whole.Triangles[t + (k + 1) % 3];
                    if (past[from] < 0.0f)
                    {
                        if (!original.TryGetValue(from, out int made))
                        {
                            made = kept.Vertices.Count;
                            kept.Vertices.Add(whole.Vertices[from]);
                            kept.Normals.Add(whole.Normals[from]);
                            original[from] = made;
                        }
                        polygon.Add(made);
                    }
                    if ((past[from] < 0.0f) != (past[to] < 0.0f))
                    {
                        long key = from < to ? ((long)from << 32) | (uint)to : ((long)to << 32) | (uint)from;
                        if (!onEdge.TryGetValue(key, out int made))
                        {
                            float share = past[from] / (past[from] - past[to]);
                            made = kept.Vertices.Count;
                            kept.Vertices.Add(Vector3.Lerp(whole.Vertices[from], whole.Vertices[to], share));
                            kept.Normals.Add(Vector3.Lerp(whole.Normals[from], whole.Normals[to], share).normalized);
                            onEdge[key] = made;
                        }
                        polygon.Add(made);
                    }
                }
                for (int k = 1; k + 1 < polygon.Count; k++)
                {
                    kept.Triangles.Add(polygon[0]);
                    kept.Triangles.Add(polygon[k]);
                    kept.Triangles.Add(polygon[k + 1]);
                }
            }
            return kept;
        }

        // ---- the pieces ----

        /// <summary>A piece of a shattered bottle: its model about its own middle, and where that middle was in the bottle.</summary>
        public sealed class Piece
        {
            public Model Model;
            public Vector3 Centre;
        }

        /// <summary>
        /// The bottle cut into <paramref name="count"/> pieces: the neck whole, the foot whole, and the walls between in shards, each the
        /// glass nearest one of a scatter of points on the bottle (the same scatter every time for the same <paramref name="seed"/>).
        /// </summary>
        public static List<Piece> Pieces(Model whole, int count, float neckBase, int seed)
        {
            int highest = 0, lowest = 0;
            var onTheWalls = new List<int>();
            for (int i = 0; i < whole.Vertices.Count; i++)
            {
                float y = whole.Vertices[i].y;
                if (y > whole.Vertices[highest].y) highest = i;
                if (y < whole.Vertices[lowest].y) lowest = i;
                if (y < neckBase)
                {
                    onTheWalls.Add(i);
                }
            }
            var middles = new List<Vector3> { whole.Vertices[highest], whole.Vertices[lowest] };
            var random = new System.Random(seed);
            // Each new point is the best of a few tries: the one furthest from those already there, so the shards are of a size.
            while (middles.Count < count && onTheWalls.Count > 0)
            {
                Vector3 best = default;
                float bestAway = -1.0f;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    Vector3 candidate = whole.Vertices[onTheWalls[random.Next(onTheWalls.Count)]];
                    float away = float.MaxValue;
                    foreach (Vector3 middle in middles)
                    {
                        away = Mathf.Min(away, (candidate - middle).sqrMagnitude);
                    }
                    if (away > bestAway)
                    {
                        bestAway = away;
                        best = candidate;
                    }
                }
                middles.Add(best);
            }

            var models = new Model[middles.Count];
            var remap = new Dictionary<int, int>[middles.Count];
            for (int t = 0; t + 2 < whole.Triangles.Count; t += 3)
            {
                int a = whole.Triangles[t], b = whole.Triangles[t + 1], c = whole.Triangles[t + 2];
                Vector3 centre = (whole.Vertices[a] + whole.Vertices[b] + whole.Vertices[c]) / 3.0f;
                int nearest = 0;
                float least = float.MaxValue;
                for (int m = 0; m < middles.Count; m++)
                {
                    float away = (centre - middles[m]).sqrMagnitude;
                    if (away < least)
                    {
                        least = away;
                        nearest = m;
                    }
                }
                models[nearest] ??= new Model();
                remap[nearest] ??= new Dictionary<int, int>();
                foreach (int corner in new[] { a, b, c })
                {
                    if (!remap[nearest].TryGetValue(corner, out int made))
                    {
                        made = models[nearest].Vertices.Count;
                        models[nearest].Vertices.Add(whole.Vertices[corner]);
                        models[nearest].Normals.Add(whole.Normals[corner]);
                        remap[nearest][corner] = made;
                    }
                    models[nearest].Triangles.Add(made);
                }
            }

            var pieces = new List<Piece>();
            foreach (Model model in models)
            {
                if (model == null || model.TriangleCount == 0)
                {
                    continue;
                }
                Vector3 centre = Vector3.zero;
                foreach (Vector3 v in model.Vertices)
                {
                    centre += v;
                }
                centre /= model.Vertices.Count;
                for (int i = 0; i < model.Vertices.Count; i++)
                {
                    model.Vertices[i] -= centre;
                }
                pieces.Add(new Piece { Model = model, Centre = centre });
            }
            return pieces;
        }

        // ---- in the hand ----

        /// <summary>
        /// A point of a bottle standing on its base, where it is in the HELD bottle's own frame: the fist's middle on the neck at the origin,
        /// the bottle running out along +Y to its base (a club's way: BottleWeapon), and <paramref name="scale"/> times the size.
        /// </summary>
        public static Vector3 Held(Vector3 standing, float gripHeight, float scale) =>
            new Vector3(standing.x, gripHeight - standing.y, -standing.z) * scale;

        /// <summary>A direction of a standing bottle, in the held bottle's frame.</summary>
        public static Vector3 HeldDirection(Vector3 standing) => new Vector3(standing.x, -standing.y, -standing.z);

        /// <summary>The whole model carried into the held bottle's frame (<see cref="Held"/>).</summary>
        public static Model InHand(Model standing, float gripHeight, float scale)
        {
            var held = new Model();
            foreach (Vector3 v in standing.Vertices)
            {
                held.Vertices.Add(Held(v, gripHeight, scale));
            }
            foreach (Vector3 n in standing.Normals)
            {
                held.Normals.Add(HeldDirection(n));
            }
            held.Triangles.AddRange(standing.Triangles);   // a half turn about X: no mirror, the triangles keep their winding
            return held;
        }
    }
}

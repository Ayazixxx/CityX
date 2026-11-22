using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A thin cap of hair over the scalp, in the shape of where hair grows on a head: down to the forehead in front, above the ears at the
    /// sides, round the back of the head at its middle (owner, 2026-10-22, of the dreads: "there is some spots where the head pokes through if you look from top
    /// down. could you make a thin layer of material covering the top of the head in the shape of a humans typical hair growth pattern"). It
    /// lies just off the head sphere, under the locks, so a gap between them shows hair, not skin. Pure: the points in the head's own space
    /// (its centre at the origin, +y up, +z the way the face looks); HairAssets merges it into a style's mesh.
    /// </summary>
    public static class HairScalp
    {
        /// <summary>
        /// How far off the head it lies, as a share of the head's radius: just clear of the skin, under the locks (at 1.012 the head showed
        /// through Hyde's cap low on the side).
        /// </summary>
        public const float Lift = 1.03f;

        /// <summary>Points round the head, and rings from the crown down to the hairline: about 500 triangles.</summary>
        public const int Around = 32;
        public const int Rings = 8;

        /// <summary>
        /// The most a ring may step down the head: a cap reaching lower gets more rings, or its flat facets dip under the head between
        /// the points (white specks through Hyde's cap, 140 degrees deep on 8 rings).
        /// </summary>
        public const float MostRingStep = 90.0f / Rings;

        // The hairline's height, degrees above the head's middle, round the head: a + b cos(turn) + c cos(2 turn), so it is FrontHairline over the
        // forehead, SideHairline over the ears and BackHairline behind. Behind it stops level with the head's middle, where the owner drew it
        // ("i like the look of the dreads hanging down on the back so make the scalp cap not too low in the back"), not down at the nape.
        public const float FrontHairline = 40.0f;
        public const float SideHairline = 15.0f;
        public const float BackHairline = 0.0f;

        /// <summary>
        /// The hairline's height above the head's middle, in degrees, <paramref name="turnDegrees"/> round from straight ahead (0 the forehead,
        /// 90 over an ear, 180 the back).
        /// </summary>
        public static float Hairline(float turnDegrees)
        {
            float b = (FrontHairline - BackHairline) * 0.5f;
            float a = (FrontHairline + BackHairline + 2.0f * SideHairline) * 0.25f;
            float c = (FrontHairline + BackHairline) * 0.5f - a;
            float t = turnDegrees * Mathf.Deg2Rad;
            return a + b * Mathf.Cos(t) + c * Mathf.Cos(2.0f * t);
        }

        /// <summary>The cap on a head of <paramref name="headRadius"/>, down to <see cref="Hairline"/>, added to the lists (triangles wound to face out).</summary>
        public static void Build(float headRadius, List<Vector3> vertices, List<Vector3> normals, List<int> triangles) =>
            Build(headRadius, Hairline, vertices, normals, triangles);

        /// <summary>The cap down to <paramref name="hairline"/> (degrees above the head's middle, by degrees round from straight ahead).</summary>
        public static void Build(float headRadius, System.Func<float, float> hairline, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            float deepest = 0.0f;
            for (int i = 0; i < Around; i++)
            {
                deepest = Mathf.Max(deepest, 90.0f - hairline(360.0f * i / Around));
            }
            int rings = Mathf.Max(Rings, Mathf.CeilToInt(deepest / MostRingStep - 1e-3f));

            int crown = vertices.Count;
            Add(Vector3.up, headRadius, vertices, normals);
            for (int ring = 1; ring <= rings; ring++)
            {
                for (int i = 0; i < Around; i++)
                {
                    float turn = 360.0f * i / Around;
                    float down = (90.0f - hairline(turn)) * ring / rings;
                    float height = (90.0f - down) * Mathf.Deg2Rad;
                    float round = turn * Mathf.Deg2Rad;
                    var direction = new Vector3(Mathf.Cos(height) * Mathf.Sin(round), Mathf.Sin(height), Mathf.Cos(height) * Mathf.Cos(round));
                    Add(direction, headRadius, vertices, normals);
                }
            }
            for (int i = 0; i < Around; i++)
            {
                int next = (i + 1) % Around;
                triangles.Add(crown);
                triangles.Add(crown + 1 + i);
                triangles.Add(crown + 1 + next);
            }
            for (int ring = 1; ring < rings; ring++)
            {
                int upper = crown + 1 + (ring - 1) * Around, lower = upper + Around;
                for (int i = 0; i < Around; i++)
                {
                    int next = (i + 1) % Around;
                    triangles.Add(upper + i);
                    triangles.Add(lower + i);
                    triangles.Add(lower + next);
                    triangles.Add(upper + i);
                    triangles.Add(lower + next);
                    triangles.Add(upper + next);
                }
            }
        }

        /// <summary>How far under the hair a raised cap stops, as a share of the head's radius: hidden under it, never through it.</summary>
        public const float UnderTheHair = 0.02f;

        /// <summary>The cone round each point of the cap the hair over it is looked for in, degrees: wider than the space between two locks.</summary>
        public const float ConeDegrees = 8.0f;

        /// <summary>The most a point of the cap is raised, as a share of the head's radius: hair further off than this is a lock hanging away, not a crown.</summary>
        public const float MostRaise = 0.35f;

        /// <summary>The cap's edge, raised, goes this far into the head (a share of its radius): its rim tucked into the scalp, so nothing is seen under it.</summary>
        public const float EdgeInTheHead = 0.98f;

        /// <summary>Rings up from the edge over which the cap rises from in the head to its full height under the hair.</summary>
        public const int RiseOverRings = 2;

        /// <summary>
        /// THE CAP RAISED UP UNDER THE HAIR (owner, 2026-11-21, of the dreads: "make the hairstyle curl inward underneath and clip into the head
        /// slightly ... to hide the visible white bit that is a result of being able to see underneath the hairstyle"). On the crown the locks
        /// stand 5 to 6 cm off the head, over the cap; looking up at the parting from in front, the eye went in through it, over the cap, under
        /// the locks and out the back to the sky. Each point of the cap is raised to just under the nearest hair over it (the lowest found in a
        /// cone round it, so it never stands up between two locks), and the space under them is closed: what shows through a parting is hair. Its
        /// edge goes into the head and it rises from there over <see cref="RiseOverRings"/> rings: raised whole, its own rim stood off the forehead.
        /// The cap's points as <see cref="Build(float, List{Vector3}, List{Vector3}, List{int})"/> lays them out: the crown, then ring after ring of
        /// <see cref="Around"/>.
        /// <paramref name="hairVertices"/> are in the head's space, as the cap is.
        /// </summary>
        public static void RaiseUnderTheHair(List<Vector3> capVertices, IReadOnlyList<Vector3> hairVertices, IReadOnlyList<int> hairTriangles,
            float headRadius)
        {
            var cone = new Vector3[7];
            int rings = (capVertices.Count - 1) / Around;
            for (int i = 0; i < capVertices.Count; i++)
            {
                Vector3 direction = capVertices[i].normalized;
                Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
                cone[0] = direction;
                for (int k = 0; k < 6; k++)
                {
                    cone[k + 1] = Quaternion.AngleAxis(ConeDegrees, Quaternion.AngleAxis(60.0f * k, direction) * side) * direction;
                }
                float from = capVertices[i].magnitude;
                float nearest = headRadius * (1.0f + MostRaise);
                bool found = false;
                foreach (Vector3 ray in cone)
                {
                    if (NearestHit(ray, from, hairVertices, hairTriangles, out float distance) && distance < nearest)
                    {
                        nearest = distance;
                        found = true;
                    }
                }
                if (!found)
                {
                    continue;
                }
                int ring = i == 0 ? 0 : (i - 1) / Around + 1;
                float rise = Mathf.Clamp01((rings - ring) / (float)RiseOverRings);
                rise = rise * rise * (3.0f - 2.0f * rise);
                float raised = Mathf.Lerp(headRadius * EdgeInTheHead, Mathf.Max(from, nearest - headRadius * UnderTheHair), rise);
                capVertices[i] = direction * raised;
            }
        }

        /// <summary>How far from the head's centre along <paramref name="ray"/> the first triangle past <paramref name="from"/> is met.</summary>
        private static bool NearestHit(Vector3 ray, float from, IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, out float distance)
        {
            distance = float.MaxValue;
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Vector3 ab = b - a, ac = c - a;
                Vector3 p = Vector3.Cross(ray, ac);
                float det = Vector3.Dot(ab, p);
                if (Mathf.Abs(det) < 1e-9f)
                {
                    continue;
                }
                float inverse = 1.0f / det;
                Vector3 s = -a;
                float u = Vector3.Dot(s, p) * inverse;
                if (u < 0.0f || u > 1.0f)
                {
                    continue;
                }
                Vector3 q = Vector3.Cross(s, ab);
                float v = Vector3.Dot(ray, q) * inverse;
                if (v < 0.0f || u + v > 1.0f)
                {
                    continue;
                }
                float along = Vector3.Dot(ac, q) * inverse;
                if (along > from && along < distance)
                {
                    distance = along;
                }
            }
            return distance < float.MaxValue;
        }

        private static void Add(Vector3 direction, float headRadius, List<Vector3> vertices, List<Vector3> normals)
        {
            vertices.Add(direction * headRadius * Lift);
            normals.Add(direction);
        }
    }
}

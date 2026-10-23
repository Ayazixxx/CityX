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

        private static void Add(Vector3 direction, float headRadius, List<Vector3> vertices, List<Vector3> normals)
        {
            vertices.Add(direction * headRadius * Lift);
            normals.Add(direction);
        }
    }
}

using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Where each point of the folded T-Shirt (the shirt lying on the ground) lies on the WORN shirt's texture, so one texture dresses both:
    /// whatever is printed on the worn shirt (a logo now, a player's own picture later) shows on the folded one, in the same place.
    ///
    /// The folded shirt lies on y = 0, front up, its collar toward +z, centred on x = 0. Its top is the worn front as a folded shirt shows it:
    /// the bottom of the neck opening at the bottom of the worn one's, the chest across its width as the worn shirt is seen from in front
    /// (scaled to fit, the picture's shape kept); its underside is the worn back the same way, from the top of the collar, seen from behind.
    /// </summary>
    public static class FoldedShirtLayout
    {
        /// <summary>One side of the worn shirt as the folded shirt shows it.</summary>
        public struct WornSide
        {
            /// <summary>
            /// Where the side's anchor is on the texture, at the middle: on the front the bottom of the neck opening, on the back the top of
            /// the collar (the folded shirt's top edge).
            /// </summary>
            public Vector2 AnchorUv;

            /// <summary>How far a metre of cloth goes on the texture.</summary>
            public float UvPerMetre;

            /// <summary>How wide the side is across the chest, in metres, straight across (as the worn shirt is seen from in front).</summary>
            public float ChestWidth;
        }

        /// <summary>How near the middle line a point of the folded shirt counts as on it, metres.</summary>
        private const float MiddleBand = 0.02f;

        /// <summary>How much lower than the front's top surface the neck opening's floor lies (it shows the shirt's back through it), metres.</summary>
        private const float NeckDip = 0.005f;

        /// <summary>The steps the middle line is walked in, from the collar, metres.</summary>
        private const float NeckStep = 0.005f;

        /// <summary>
        /// The texture coordinates of the folded shirt's points (<paramref name="vertices"/>, with their <paramref name="normals"/>): on the
        /// worn <paramref name="front"/> where the surface faces up, on the worn <paramref name="back"/> where it faces down.
        /// </summary>
        public static Vector2[] Uvs(Vector3[] vertices, Vector3[] normals, WornSide front, WornSide back)
        {
            float minX = float.MaxValue, maxX = float.MinValue, collarZ = float.MinValue;
            foreach (Vector3 p in vertices)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                collarZ = Mathf.Max(collarZ, p.z);
            }
            float width = maxX - minX;
            float necklineZ = NecklineZ(vertices, normals, collarZ);
            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                bool up = normals[i].y >= 0.0f;
                WornSide side = up ? front : back;
                float scale = side.ChestWidth / width * side.UvPerMetre;   // the folded width spans the chest as seen from in front
                float right = up ? vertices[i].x : -vertices[i].x;           // seen from behind, the shirt's +x is on the left
                float below = vertices[i].z - (up ? necklineZ : collarZ);
                uvs[i] = side.AnchorUv + new Vector2(right, below) * scale;
            }
            return uvs;
        }

        /// <summary>
        /// The bottom of the folded shirt's neck opening at the middle. Walking down the middle line of its top from the collar's end (in
        /// steps of <see cref="NeckStep"/>): the back of the collar, then the opening, lower than the front's surface (it shows the shirt's
        /// back through it), then the front. The end of the first lowered stretch; the collar's end where there is none.
        /// </summary>
        public static float NecklineZ(Vector3[] vertices, Vector3[] normals, float collarZ)
        {
            float minZ = float.MaxValue;
            foreach (Vector3 p in vertices)
            {
                minZ = Mathf.Min(minZ, p.z);
            }
            float halfway = 0.5f * (minZ + collarZ);
            int steps = Mathf.CeilToInt((collarZ - halfway) / NeckStep);
            var highest = new float[steps];
            for (int s = 0; s < steps; s++)
            {
                highest[s] = float.MinValue;
            }
            float frontY = float.MinValue;   // the front's surface: the highest of the middle line below the collar's half
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                if (normals[i].y <= 0.5f || Mathf.Abs(p.x) >= MiddleBand)
                {
                    continue;
                }
                if (p.z <= halfway)
                {
                    frontY = Mathf.Max(frontY, p.y);
                    continue;
                }
                int step = Mathf.Min(steps - 1, (int)((collarZ - p.z) / NeckStep));
                highest[step] = Mathf.Max(highest[step], p.y);
            }
            bool inOpening = false;
            for (int s = 0; s < steps; s++)
            {
                if (highest[s] == float.MinValue)
                {
                    continue;   // no point of the middle line in this step: as the step before
                }
                bool lowered = highest[s] < frontY - NeckDip;
                if (inOpening && !lowered)
                {
                    return collarZ - s * NeckStep;
                }
                inOpening |= lowered;
            }
            return collarZ;
        }
    }
}

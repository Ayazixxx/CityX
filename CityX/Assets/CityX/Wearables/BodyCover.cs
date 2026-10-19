using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The body's skin with a garment's hidden triangles left out (SkinnedGarment.HiddenBodyTriangles, one bit per triangle). Made once per body
    /// mesh and garment, shared by everyone wearing it.
    /// </summary>
    public static class BodyCover
    {
        private static readonly Dictionary<(Mesh body, byte[] mask), Mesh> Made = new Dictionary<(Mesh, byte[]), Mesh>();

        /// <summary>Whether triangle <paramref name="triangle"/> is set in <paramref name="mask"/>.</summary>
        public static bool IsHidden(byte[] mask, int triangle) => (mask[triangle / 8] & (1 << (triangle % 8))) != 0;

        /// <summary>
        /// <paramref name="body"/> without the masked triangles; the body itself when the mask was made for a body with another triangle count
        /// (a rebuilt body the garment was not rebuilt for), so a stale mask never cuts the wrong holes.
        /// </summary>
        public static Mesh Cut(Mesh body, byte[] mask, int madeForTriangles)
        {
            if (body == null || mask == null || mask.Length == 0)
            {
                return body;
            }
            if (Made.TryGetValue((body, mask), out Mesh made) && made != null)
            {
                return made;
            }
            int[] triangles = body.triangles;
            if (triangles.Length / 3 != madeForTriangles || mask.Length * 8 < madeForTriangles)
            {
                return body;
            }
            var shown = new List<int>(triangles.Length);
            for (int t = 0; t < madeForTriangles; t++)
            {
                if (!IsHidden(mask, t))
                {
                    shown.Add(triangles[t * 3]);
                    shown.Add(triangles[t * 3 + 1]);
                    shown.Add(triangles[t * 3 + 2]);
                }
            }
            made = Object.Instantiate(body);
            made.name = body.name + "Covered";
            made.SetTriangles(shown, 0);
            Made[(body, mask)] = made;
            return made;
        }
    }
}

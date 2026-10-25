using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The body's skin with the worn garments' hidden triangles left out (SkinnedGarment.HiddenBodyTriangles, one bit per triangle). Made once
    /// per body mesh and set of garments, shared by everyone wearing them.
    /// </summary>
    public static class BodyCover
    {
        private static readonly Dictionary<(Mesh body, byte[] mask), Mesh> Made = new Dictionary<(Mesh, byte[]), Mesh>();

        /// <summary>The masks combined for several garments worn at once, kept so the same set cuts the same mesh again.</summary>
        private static readonly List<(byte[][] masks, byte[] combined)> Combined = new List<(byte[][], byte[])>();

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
            if (!Fits(triangles.Length / 3, mask, madeForTriangles))
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

        /// <summary>
        /// <paramref name="body"/> without what any of <paramref name="garments"/> hides (a shirt and jeans worn together): their masks made
        /// for this body combined. Masks made for another body are left out, as <see cref="Cut(Mesh, byte[], int)"/> leaves out a lone one.
        /// </summary>
        public static Mesh Cut(Mesh body, IReadOnlyList<(byte[] mask, int madeForTriangles)> garments)
        {
            if (body == null)
            {
                return null;
            }
            int bodyTriangles = body.triangles.Length / 3;
            var masks = new List<byte[]>();
            foreach ((byte[] mask, int madeFor) in garments)
            {
                if (mask != null && Fits(bodyTriangles, mask, madeFor) && !masks.Contains(mask))
                {
                    masks.Add(mask);
                }
            }
            if (masks.Count == 0)
            {
                return body;
            }
            return Cut(body, masks.Count == 1 ? masks[0] : Union(masks), bodyTriangles);
        }

        private static bool Fits(int bodyTriangles, byte[] mask, int madeForTriangles) =>
            mask.Length > 0 && bodyTriangles == madeForTriangles && mask.Length * 8 >= madeForTriangles;

        /// <summary>Every triangle any of <paramref name="masks"/> hides; the same array for the same set of masks, in any order.</summary>
        private static byte[] Union(List<byte[]> masks)
        {
            foreach ((byte[][] set, byte[] combined) in Combined)
            {
                if (set.Length == masks.Count && System.Array.TrueForAll(set, masks.Contains))
                {
                    return combined;
                }
            }
            int length = 0;
            foreach (byte[] mask in masks)
            {
                length = Mathf.Max(length, mask.Length);
            }
            var union = new byte[length];
            foreach (byte[] mask in masks)
            {
                for (int i = 0; i < mask.Length; i++)
                {
                    union[i] |= mask[i];
                }
            }
            Combined.Add((masks.ToArray(), union));
            return union;
        }
    }
}

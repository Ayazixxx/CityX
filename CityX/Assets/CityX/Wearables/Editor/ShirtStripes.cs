using System.Collections.Generic;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Paints a shirt's texture in level stripes, as a striped tee is knitted: each texel takes its band from how high its cloth sits on the body
    /// standing (bind pose), so the stripes run straight across the front, the back and round the sleeves, and meet at every seam. The crew neck's
    /// band is one colour of its own. Texels outside every piece take their neighbours' colour, so no seam shows a line.
    /// </summary>
    public sealed class ShirtStripes
    {
        /// <summary>How far out past the pieces their colours are carried, in texels: wider than any bleed the texture's smaller mips need.</summary>
        private const int Gutter = 16;

        /// <summary>How high one band is, metres.</summary>
        public float BandHeight;

        /// <summary>The height of the hem: the first band starts here and goes up.</summary>
        public float Hem;

        /// <summary>The colour of the bands counted from the hem: the first, third, ...; and the second, fourth, ...</summary>
        public Color32 First;
        public Color32 Second;

        /// <summary>The crew neck's band, and how far from the neck opening's rim it reaches over the cloth.</summary>
        public Color32 Collar;
        public float CollarReach;

        /// <summary>The colour of the band at <paramref name="height"/> (bind pose, metres).</summary>
        public Color32 BandAt(float height)
        {
            int band = Mathf.FloorToInt((height - Hem) / BandHeight);
            return (band & 1) == 0 ? First : Second;
        }

        /// <summary>The striped texture's texels, <paramref name="size"/> square, over <paramref name="shirt"/>'s texture layout.</summary>
        public Color32[] Paint(Mesh shirt, IReadOnlyList<Vector3> neckRim, int size)
        {
            Vector3[] vertices = shirt.vertices;
            Vector2[] uvs = shirt.uv;
            int[] triangles = shirt.triangles;
            var fromNeck = new float[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                fromNeck[i] = float.MaxValue;
                foreach (Vector3 p in neckRim)
                {
                    fromNeck[i] = Mathf.Min(fromNeck[i], Vector3.Distance(vertices[i], p));
                }
            }

            var texels = new Color32[size * size];
            var painted = new bool[size * size];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                Vector2 ua = uvs[a] * size, ub = uvs[b] * size, uc = uvs[c] * size;
                float area = Cross(ub - ua, uc - ua);
                if (Mathf.Abs(area) < 1e-9f)
                {
                    continue;
                }
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x))));
                int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x))));
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.y, Mathf.Min(ub.y, uc.y))));
                int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(ua.y, Mathf.Max(ub.y, uc.y))));
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float wa = Cross(ub - p, uc - p) / area;
                        float wb = Cross(uc - p, ua - p) / area;
                        float wc = 1.0f - wa - wb;
                        if (wa < 0.0f || wb < 0.0f || wc < 0.0f)
                        {
                            continue;
                        }
                        float height = wa * vertices[a].y + wb * vertices[b].y + wc * vertices[c].y;
                        float neck = wa * fromNeck[a] + wb * fromNeck[b] + wc * fromNeck[c];
                        texels[y * size + x] = neck < CollarReach ? Collar : BandAt(height);
                        painted[y * size + x] = true;
                    }
                }
            }
            Bleed(texels, painted, size);
            return texels;
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        /// <summary>Carries the painted texels' colours out over the unpainted ones round them, Gutter texels deep; the rest takes the first band's.</summary>
        private void Bleed(Color32[] texels, bool[] painted, int size)
        {
            var next = new bool[painted.Length];
            for (int pass = 0; pass < Gutter; pass++)
            {
                System.Array.Copy(painted, next, painted.Length);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x;
                        if (painted[i])
                        {
                            continue;
                        }
                        int from = x > 0 && painted[i - 1] ? i - 1
                            : x < size - 1 && painted[i + 1] ? i + 1
                            : y > 0 && painted[i - size] ? i - size
                            : y < size - 1 && painted[i + size] ? i + size
                            : -1;
                        if (from >= 0)
                        {
                            texels[i] = texels[from];
                            next[i] = true;
                        }
                    }
                }
                System.Array.Copy(next, painted, painted.Length);
            }
            for (int i = 0; i < texels.Length; i++)
            {
                if (!painted[i])
                {
                    texels[i] = First;
                }
            }
        }
    }
}

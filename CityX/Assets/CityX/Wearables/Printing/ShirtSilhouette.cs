using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// THE SHIRT AS THE HEAT PRESS SCREEN SHOWS IT (docs/HEAT_PRESS.md): the shirt's own mesh seen straight from the front or the back, flat
    /// (no perspective, so a metre is the same number of pixels everywhere and the print area lands exactly where it prints), in its own
    /// cloth (its texture read at each point, or its colour), shaded a little by how the cloth faces, with a dark edge round it as the game's
    /// cel lines draw one. Pure, so it is tested without a screen; drawn once when a shirt is chosen.
    /// </summary>
    public static class ShirtSilhouette
    {
        /// <summary>How much of the cloth's colour shows where it faces away: the rest comes as it turns to face the eye.</summary>
        public const float SideShade = 0.72f;

        public static readonly Color32 Edge = new Color32(18, 18, 20, 255);

        /// <summary>
        /// The shirt seen from its <paramref name="front"/> (or back), <paramref name="width"/> by <paramref name="height"/> pixels (rows bottom
        /// up), <paramref name="pixelsPerMetre"/> to the metre, centred at <paramref name="centre"/> (metres: right as seen, and height).
        /// <paramref name="cloth"/> is the cloth's texture (<paramref name="clothSize"/> square, rows bottom up) or null for
        /// <paramref name="plain"/>. See-through where there is no shirt.
        /// </summary>
        public static Color32[] Draw(Mesh mesh, bool front, Color32[] cloth, int clothSize, Color plain, int width, int height, float pixelsPerMetre,
            Vector2 centre)
        {
            var pixels = new Color32[width * height];
            var depth = new float[width * height];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.NegativeInfinity;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            bool textured = cloth != null && uvs != null && uvs.Length == vertices.Length && clothSize > 0;
            var screen = new Vector3[vertices.Length];   // x, y in pixels; z towards the eye
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                float right = front ? -p.x : p.x;   // bind pose, +z ahead: seen from in front, the wearer's left is on the right
                screen[i] = new Vector3((right - centre.x) * pixelsPerMetre + width * 0.5f, (p.y - centre.y) * pixelsPerMetre + height * 0.5f,
                    front ? p.z : -p.z);
            }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int ia = triangles[t], ib = triangles[t + 1], ic = triangles[t + 2];
                Vector3 a = screen[ia], b = screen[ib], c = screen[ic];
                Vector3 normal = Vector3.Cross(vertices[ib] - vertices[ia], vertices[ic] - vertices[ia]).normalized;
                float facing = Mathf.Abs(front ? normal.z : -normal.z);
                float shade = Mathf.Lerp(SideShade, 1.0f, facing);
                Fill(pixels, depth, width, height, a, b, c, (u, v, w) =>
                {
                    Color colour = plain;
                    if (textured)
                    {
                        Vector2 uv = uvs[ia] * u + uvs[ib] * v + uvs[ic] * w;
                        int x = Mathf.Clamp((int)(uv.x * clothSize), 0, clothSize - 1), y = Mathf.Clamp((int)(uv.y * clothSize), 0, clothSize - 1);
                        colour = cloth[y * clothSize + x];
                    }
                    return new Color(colour.r * shade, colour.g * shade, colour.b * shade, 1.0f);
                });
            }
            Outline(pixels, width, height);
            return pixels;
        }

        private delegate Color TexelOf(float u, float v, float w);

        /// <summary>Fills a triangle, nearest to the eye kept.</summary>
        private static void Fill(Color32[] pixels, float[] depth, int width, int height, Vector3 a, Vector3 b, Vector3 c, TexelOf shade)
        {
            float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
            if (Mathf.Abs(area) < 1e-6f)
            {
                return;
            }
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.x - px) * (c.y - py) - (c.x - px) * (b.y - py)) / area;
                    float w1 = ((c.x - px) * (a.y - py) - (a.x - px) * (c.y - py)) / area;
                    float w2 = 1.0f - w0 - w1;
                    if (w0 < 0.0f || w1 < 0.0f || w2 < 0.0f)
                    {
                        continue;
                    }
                    float z = a.z * w0 + b.z * w1 + c.z * w2;
                    int i = y * width + x;
                    if (z <= depth[i])
                    {
                        continue;
                    }
                    depth[i] = z;
                    pixels[i] = shade(w0, w1, w2);
                }
            }
        }

        /// <summary>A dark line round the shirt: every shirt pixel beside an empty one.</summary>
        private static void Outline(Color32[] pixels, int width, int height)
        {
            var edge = new bool[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a == 0)
                    {
                        continue;
                    }
                    edge[y * width + x] = Empty(pixels, width, height, x - 1, y) || Empty(pixels, width, height, x + 1, y)
                                          || Empty(pixels, width, height, x, y - 1) || Empty(pixels, width, height, x, y + 1);
                }
            }
            for (int i = 0; i < pixels.Length; i++)
            {
                if (edge[i]) pixels[i] = Edge;
            }
        }

        private static bool Empty(Color32[] pixels, int width, int height, int x, int y) =>
            x < 0 || y < 0 || x >= width || y >= height || pixels[y * width + x].a == 0;
    }
}

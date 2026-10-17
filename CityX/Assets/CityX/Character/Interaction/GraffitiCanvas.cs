using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE PAINT OF ONE PIECE OF GRAFFITI: a square of RGBA pixels, transparent where nothing is painted, sprayed on with a soft round brush.
    /// Pure (no textures, no scene), so the spraying is tested on its own; GraffitiPanel shows it on a wall and the menu shows it while you
    /// draw. Saved and sent as raw RGBA, gzipped (as the body paint is: no image module a build might strip), so a piece is a few kilobytes and
    /// can be written to disk as it is when graffiti starts to last past a session.
    /// </summary>
    public sealed class GraffitiCanvas
    {
        /// <summary>Pixels along a side. A 2.6 m piece at 256 is a centimetre a pixel: finer than spray paint is.</summary>
        public const int Size = 256;

        /// <summary>The most a piece may weigh encoded, bytes: enough for a busy piece, small enough to send to everyone.</summary>
        public const int MaxEncodedBytes = 96 * 1024;

        public Color32[] Pixels { get; } = new Color32[Size * Size];

        /// <summary>Bumped on every change, so whatever shows it knows to upload it again.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Where paint sticks, one byte a pixel (255: the wall, 0: off it -- past its corner, on a pillar standing out of it, in a window's hole),
        /// or null for everywhere. Only the painter's canvas has one (GraffitiWall.WallMask); what is sent is the paint as it stuck.
        /// </summary>
        public byte[] Mask
        {
            get => _mask;
            set => _mask = value != null && value.Length == Size * Size ? value : null;
        }

        private byte[] _mask;

        public void Clear()
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = new Color32(0, 0, 0, 0);
            }
            Version++;
        }

        public bool IsEmpty
        {
            get
            {
                foreach (Color32 p in Pixels)
                {
                    if (p.a > 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// One puff of the can at (<paramref name="x"/>, <paramref name="y"/>) pixels, <paramref name="radius"/> across: full in the middle,
        /// fading to nothing at the edge, laid over what is there (paint over paint, as a can does).
        /// </summary>
        public void Spray(float x, float y, float radius, Color colour, float strength = 1.0f)
        {
            radius = Mathf.Max(0.5f, radius);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(x - radius));
            int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(y - radius));
            int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(y + radius));
            float r2 = radius * radius;
            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    float dx = px + 0.5f - x;
                    float dy = py + 0.5f - y;
                    float d2 = (dx * dx + dy * dy) / r2;
                    if (d2 >= 1.0f)
                    {
                        continue;
                    }
                    float fall = 1.0f - d2;
                    int index = py * Size + px;
                    float a = Mathf.Clamp01(fall * fall * 1.6f) * Mathf.Clamp01(strength) * colour.a;
                    if (_mask != null)
                    {
                        a *= _mask[index] * (1.0f / 255.0f);
                    }
                    if (a <= 0.004f)
                    {
                        continue;
                    }
                    Color32 under = Pixels[index];
                    float ua = under.a / 255.0f;
                    float outA = a + ua * (1.0f - a);
                    // Over: the new paint's colour weighted by its cover, the old by what shows through.
                    float k = outA > 1e-4f ? a / outA : 1.0f;
                    Pixels[index] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(under.r / 255.0f, colour.r, k) * 255.0f),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(under.g / 255.0f, colour.g, k) * 255.0f),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(under.b / 255.0f, colour.b, k) * 255.0f),
                        (byte)Mathf.RoundToInt(outA * 255.0f));
                }
            }
            Version++;
        }

        /// <summary>A stroke from one point to the next: puffs a third of the brush apart, so a drag is a smooth line.</summary>
        public void Stroke(Vector2 from, Vector2 to, float radius, Color colour)
        {
            float step = Mathf.Max(0.5f, radius * 0.33f);
            float length = Vector2.Distance(from, to);
            int puffs = Mathf.Max(1, Mathf.CeilToInt(length / step));
            for (int i = 1; i <= puffs; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)puffs);
                Spray(p.x, p.y, radius, colour, 0.55f);
            }
        }

        public byte[] Encode()
        {
            var raw = new byte[Pixels.Length * 4];
            for (int i = 0; i < Pixels.Length; i++)
            {
                Color32 c = Pixels[i];
                raw[i * 4] = c.r;
                raw[i * 4 + 1] = c.g;
                raw[i * 4 + 2] = c.b;
                raw[i * 4 + 3] = c.a;
            }
            using (var output = new System.IO.MemoryStream())
            {
                using (var zip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
                {
                    zip.Write(raw, 0, raw.Length);
                }
                return output.ToArray();
            }
        }

        /// <summary>Takes a piece from its bytes; false (and nothing changed) when they are not one.</summary>
        public bool Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxEncodedBytes)
            {
                return false;
            }
            var raw = new byte[Pixels.Length * 4];
            try
            {
                using (var input = new System.IO.MemoryStream(bytes))
                using (var zip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
                {
                    int read = 0;
                    while (read < raw.Length)
                    {
                        int n = zip.Read(raw, read, raw.Length - read);
                        if (n <= 0)
                        {
                            break;
                        }
                        read += n;
                    }
                    if (read != raw.Length || zip.ReadByte() != -1)
                    {
                        return false;
                    }
                }
            }
            catch (System.Exception)
            {
                return false;
            }
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = new Color32(raw[i * 4], raw[i * 4 + 1], raw[i * 4 + 2], raw[i * 4 + 3]);
            }
            Version++;
            return true;
        }
    }
}

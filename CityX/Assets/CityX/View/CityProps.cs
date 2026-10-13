using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The small things the city is strewn with, built as boxes straight into a chunk's own buffers (no prefabs, no extra draws): barricades,
    /// rubble, the benches and planters of a plaza, kiosks, the gantry signs of the expressways.
    ///
    /// Everything is sized for the 2.6 m player (about 1.3x real). Every piece is a TURNED BOX about the vertical (<see cref="AddTurnedBox"/>),
    /// so it can stand at any angle inside a frame a building has already set on the buffer.
    ///
    /// No cars (owner, 2026-10-12: "get rid of the cars"): abandoned cars were built on the decks, streets and car parks, then taken out.
    /// </summary>
    public static class CityProps
    {
        public static readonly Color32 Concrete = SurfaceLook.Pack(new Color32(206, 204, 198, 255), (int)WallMaterial.Concrete, 0.55f);
        public static readonly Color32 DarkConcrete = SurfaceLook.Pack(new Color32(150, 148, 144, 255), (int)WallMaterial.Concrete, 0.7f);
        public static readonly Color32 Stone = SurfaceLook.Pack(new Color32(190, 186, 178, 255), (int)WallMaterial.Stone, 0.45f);
        public static readonly Color32 DarkMetal = SurfaceLook.Pack(new Color(0.13f, 0.13f, 0.14f), (int)WallMaterial.Metal, 0.5f);
        public static readonly Color32 White = SurfaceLook.Pack(new Color(0.97f, 0.97f, 0.95f), (int)WallMaterial.Plaster, 0.1f);
        public static readonly Color32 SignGreen = SurfaceLook.Pack(new Color(0.05f, 0.40f, 0.20f), (int)WallMaterial.Metal, 0.35f);
        public static readonly Color32 SignBlue = SurfaceLook.Pack(new Color(0.08f, 0.22f, 0.65f), (int)WallMaterial.Metal, 0.35f);
        public static readonly Color32 Wood = SurfaceLook.Pack(new Color(0.42f, 0.30f, 0.19f), (int)WallMaterial.Metal, 0.6f);
        public static readonly Color32 Earth = SurfaceLook.Pack(new Color(0.55f, 0.50f, 0.44f), SurfaceLook.Earth, 0.6f);

        /// <summary>Painted-metal colours for the kiosks.</summary>
        private static readonly Color[] Palette =
        {
            new Color(0.82f, 0.82f, 0.80f), new Color(0.72f, 0.72f, 0.74f), new Color(0.20f, 0.20f, 0.22f), new Color(0.35f, 0.36f, 0.40f),
            new Color(0.55f, 0.12f, 0.10f), new Color(0.12f, 0.20f, 0.45f), new Color(0.10f, 0.32f, 0.28f), new Color(0.78f, 0.62f, 0.25f),
            new Color(0.80f, 0.80f, 0.80f), new Color(0.26f, 0.26f, 0.28f), new Color(0.60f, 0.58f, 0.52f), new Color(0.40f, 0.14f, 0.30f),
        };

        public static uint Hash(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393) + (uint)(b * 668265263) + (uint)(salt * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A hash as a number from 0 to 1.</summary>
        public static float Unit(int a, int b, int salt) => (Hash(a, b, salt) & 0xFFFFFFu) / 16777216.0f;

        /// <summary>
        /// A box turned about the vertical: centred on (<paramref name="cx"/>, <paramref name="cz"/>), half <paramref name="hx"/> along its own +x
        /// (the direction (<paramref name="cos"/>, <paramref name="sin"/>)), half <paramref name="hz"/> across, from <paramref name="y0"/> up to
        /// <paramref name="y1"/>. The bottom is left out unless asked for (it is on the ground, or on another box).
        /// </summary>
        public static void AddTurnedBox(MeshBuffer b, float cx, float cz, float hx, float hz, float cos, float sin, float y0, float y1, bool bottom = false)
        {
            Vector3 P(float x, float z, float y) => new Vector3(cx + x * cos - z * sin, y, cz + x * sin + z * cos);
            var nx = new Vector3(cos, 0.0f, sin);
            var nz = new Vector3(-sin, 0.0f, cos);
            float w = hx * 2.0f, d = hz * 2.0f, h = y1 - y0;
            b.AddQuad(P(-hx, -hz, y1), P(-hx, hz, y1), P(hx, hz, y1), P(hx, -hz, y1), Vector3.up,
                new Vector2(0, 0), new Vector2(0, d), new Vector2(w, d), new Vector2(w, 0));
            b.AddQuad(P(hx, -hz, y0), P(hx, hz, y0), P(hx, hz, y1), P(hx, -hz, y1), nx,
                new Vector2(0, 0), new Vector2(d, 0), new Vector2(d, h), new Vector2(0, h));
            b.AddQuad(P(-hx, hz, y0), P(-hx, -hz, y0), P(-hx, -hz, y1), P(-hx, hz, y1), -nx,
                new Vector2(0, 0), new Vector2(d, 0), new Vector2(d, h), new Vector2(0, h));
            b.AddQuad(P(hx, hz, y0), P(-hx, hz, y0), P(-hx, hz, y1), P(hx, hz, y1), nz,
                new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
            b.AddQuad(P(-hx, -hz, y0), P(hx, -hz, y0), P(hx, -hz, y1), P(-hx, -hz, y1), -nz,
                new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
            if (bottom)
            {
                b.AddQuad(P(-hx, -hz, y0), P(hx, -hz, y0), P(hx, hz, y0), P(-hx, hz, y0), Vector3.down,
                    new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, d), new Vector2(0, d));
            }
        }

        /// <summary>A concrete jersey barrier, 3 m long, lying across (<paramref name="cos"/>, <paramref name="sin"/>).</summary>
        public static void AddBarrier(MeshBuffer b, float cx, float cz, float y, float cos, float sin)
        {
            Color32 saved = b.Look;
            b.Look = Concrete;
            AddTurnedBox(b, cx, cz, 1.5f, 0.32f, cos, sin, y, y + 0.3f);
            AddTurnedBox(b, cx, cz, 1.5f, 0.17f, cos, sin, y + 0.3f, y + 1.1f);
            b.Look = saved;
        }

        /// <summary>A heap of broken concrete about <paramref name="radius"/> across: a few chunks at odd angles, highest in the middle.</summary>
        public static void AddRubble(MeshBuffer b, float cx, float cz, float y, float radius, int seed)
        {
            Color32 saved = b.Look;
            b.Look = DarkConcrete;
            int count = 4 + (int)(Hash(seed, 1, 5) % 4u);
            for (int i = 0; i < count; i++)
            {
                float u1 = Unit(seed, i, 11), u2 = Unit(seed, i, 13), u3 = Unit(seed, i, 17), u4 = Unit(seed, i, 19);
                float dist = radius * (0.15f + 0.75f * u1);
                float angle = u2 * 6.2831853f;
                float px = cx + Mathf.Cos(angle) * dist, pz = cz + Mathf.Sin(angle) * dist;
                float size = 0.3f + 0.9f * u3;
                float yaw = u4 * 3.14159f;
                float height = size * (0.9f - 0.5f * dist / radius);
                AddTurnedBox(b, px, pz, size * 0.5f, size * 0.35f, Mathf.Cos(yaw), Mathf.Sin(yaw), y - 0.05f, y + height);
            }
            b.Look = saved;
        }

        /// <summary>A park bench 2.2 m long, its seat facing +z of (<paramref name="cos"/>, <paramref name="sin"/>)'s frame (the back is on the -z side).</summary>
        public static void AddBench(MeshBuffer b, float cx, float cz, float y, float cos, float sin)
        {
            Color32 saved = b.Look;
            b.Look = DarkMetal;
            for (int side = -1; side <= 1; side += 2)
            {
                float lx = side * 0.9f;
                AddTurnedBox(b, cx + lx * cos, cz + lx * sin, 0.06f, 0.24f, cos, sin, y, y + 0.5f);
            }
            b.Look = Wood;
            AddTurnedBox(b, cx, cz, 1.1f, 0.26f, cos, sin, y + 0.5f, y + 0.58f);
            float bz = -0.24f;
            AddTurnedBox(b, cx - bz * sin, cz + bz * cos, 1.1f, 0.04f, cos, sin, y + 0.58f, y + 1.05f);
            b.Look = saved;
        }

        /// <summary>A concrete planter, dry earth in it (nothing has grown on Terraplex 9 since the rapture).</summary>
        public static void AddPlanter(MeshBuffer b, float cx, float cz, float y, float hx, float hz, float height, float cos = 1.0f, float sin = 0.0f)
        {
            Color32 saved = b.Look;
            b.Look = Concrete;
            AddTurnedBox(b, cx, cz, hx, hz, cos, sin, y, y + height);
            b.Look = Earth;
            AddTurnedBox(b, cx, cz, hx - 0.15f, hz - 0.15f, cos, sin, y + height - 0.12f, y + height - 0.07f);
            b.Look = saved;
        }

        public static void AddBollard(MeshBuffer b, float cx, float cz, float y)
        {
            Color32 saved = b.Look;
            b.Look = DarkMetal;
            AddTurnedBox(b, cx, cz, 0.14f, 0.14f, 1.0f, 0.0f, y, y + 1.1f);
            b.Look = saved;
        }

        /// <summary>A dry fountain: an eight-sided basin wall round a plinth, and the tapering stone needle that stood in it.</summary>
        public static void AddFountain(MeshBuffer b, float cx, float cz, float y, float radius, bool needle)
        {
            Color32 saved = b.Look;
            b.Look = Stone;
            float segment = radius * 0.4142f + 0.12f;   // tan(22.5 deg), plus overlap
            for (int k = 0; k < 8; k++)
            {
                float angle = k * 0.7853982f;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                float px = cx + c * radius, pz = cz + s * radius;
                AddTurnedBox(b, px, pz, segment, 0.22f, -s, c, y, y + 0.55f);
            }
            AddTurnedBox(b, cx, cz, 0.75f, 0.75f, 1.0f, 0.0f, y, y + 0.9f);
            if (needle)
            {
                AddTurnedBox(b, cx, cz, 0.45f, 0.45f, 1.0f, 0.0f, y + 0.9f, y + 3.0f);
                AddTurnedBox(b, cx, cz, 0.30f, 0.30f, 1.0f, 0.0f, y + 3.0f, y + 5.0f);
                AddTurnedBox(b, cx, cz, 0.15f, 0.15f, 1.0f, 0.0f, y + 5.0f, y + 6.2f);
            }
            b.Look = saved;
        }

        /// <summary>A shuttered kiosk (coffee, papers, noodles -- nobody knows any more) with its sign band, facing +z of its frame.</summary>
        public static void AddKiosk(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed)
        {
            Color32 saved = b.Look;
            Color body = Palette[(int)(Hash(seed, 3, 29) % (uint)Palette.Length)];
            b.Look = SurfaceLook.Pack(body, (int)WallMaterial.Metal, 0.6f);
            AddTurnedBox(b, cx, cz, 1.6f, 1.2f, cos, sin, y, y + 2.9f);
            b.Look = DarkMetal;
            float fz = 1.22f;
            AddTurnedBox(b, cx - fz * sin, cz + fz * cos, 1.4f, 0.03f, cos, sin, y + 0.9f, y + 2.1f);
            b.Look = White;
            AddTurnedBox(b, cx, cz, 1.65f, 1.25f, cos, sin, y + 2.9f, y + 3.3f);
            b.Look = saved;
        }

        /// <summary>
        /// An expressway's overhead sign: two posts either side of the deck, a beam across, and a green panel over the lanes on the ramp's side,
        /// lettered in white bars (there is no font; from the deck they read as a sign). The panel's own +z of (<paramref name="cos"/>,
        /// <paramref name="sin"/>)'s frame faces the traffic it is for; it is lettered on both faces.
        /// </summary>
        public static void AddGantrySign(MeshBuffer b, float cx, float cz, float deckY, float cos, float sin, float halfSpan, float panelOffset, int seed)
        {
            Color32 saved = b.Look;
            b.Look = DarkMetal;
            for (int side = -1; side <= 1; side += 2)
            {
                float lx = side * (halfSpan + 0.3f);
                AddTurnedBox(b, cx + lx * cos, cz + lx * sin, 0.22f, 0.22f, cos, sin, deckY, deckY + 7.2f);
            }
            AddTurnedBox(b, cx, cz, halfSpan + 0.5f, 0.2f, cos, sin, deckY + 6.7f, deckY + 7.1f);
            float px = cx + panelOffset * cos, pz = cz + panelOffset * sin;
            b.Look = SignGreen;
            AddTurnedBox(b, px, pz, 3.5f, 0.08f, cos, sin, deckY + 4.2f, deckY + 6.6f);
            b.Look = White;
            int words = 2 + (int)(Hash(seed, 5, 31) % 2u);
            for (int w = 0; w < words; w++)
            {
                float length = 1.2f + 1.6f * Unit(seed, w, 37);
                float ox = -2.6f + length + 0.2f;
                float oy = deckY + 6.0f - w * 0.55f;
                AddTurnedBox(b, px + ox * cos, pz + ox * sin, length, 0.12f, cos, sin, oy - 0.16f, oy + 0.16f);
            }
            float ax = 2.4f;
            AddTurnedBox(b, px + ax * cos, pz + ax * sin, 0.6f, 0.12f, cos, sin, deckY + 4.6f, deckY + 5.0f);
            b.Look = saved;
        }

        /// <summary>A blue "P" sign panel, lettered in white boxes, hung flat against +z of its frame.</summary>
        public static void AddParkingSign(MeshBuffer b, float cx, float cz, float y, float cos, float sin, float size)
        {
            Color32 saved = b.Look;
            b.Look = SignBlue;
            AddTurnedBox(b, cx, cz, size * 0.5f, 0.06f, cos, sin, y, y + size);
            b.Look = White;
            float t = size * 0.11f;   // the stroke
            float fz = 0.08f;
            float px = cx - fz * sin, pz = cz + fz * cos;
            // The stem, the top bar, the bowl's right side and the middle bar. Seen from the front (standing on the +z side, looking back at the
            // panel) the frame's +x is on the viewer's LEFT, so the stem goes at +x and the bowl opens toward -x, or the P reads mirrored.
            float stemX = size * 0.18f;
            AddTurnedBox(b, px + stemX * cos, pz + stemX * sin, t * 0.5f, 0.02f, cos, sin, y + size * 0.15f, y + size * 0.85f);
            AddTurnedBox(b, px + (stemX - size * 0.17f) * cos, pz + (stemX - size * 0.17f) * sin, size * 0.17f + t * 0.5f, 0.02f, cos, sin, y + size * 0.85f - t, y + size * 0.85f);
            AddTurnedBox(b, px + (stemX - size * 0.34f) * cos, pz + (stemX - size * 0.34f) * sin, t * 0.5f, 0.02f, cos, sin, y + size * 0.5f, y + size * 0.85f);
            AddTurnedBox(b, px + (stemX - size * 0.17f) * cos, pz + (stemX - size * 0.17f) * sin, size * 0.17f + t * 0.5f, 0.02f, cos, sin, y + size * 0.5f, y + size * 0.5f + t);
            b.Look = saved;
        }
    }
}

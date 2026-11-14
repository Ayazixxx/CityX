using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The furniture of the open spaces (docs/OPEN_SPACE.md, 2026-10-15): seat blocks, shade sails, umbrella tables, chairs (some fallen), cube
    /// seats, bollard lights, dead trees in their planters, a round tree bench, a pergola, a tiered tower, a chessboard and its pieces, a bike
    /// rack, a name wall, a hoarding. All boxes and beams into the chunk's own buffers, as everything in CityProps is; a lit band goes into the
    /// lit-glass buffer (SurfaceLook.PackFloodlit) where there is one and the block has power.
    /// </summary>
    public static partial class CityProps
    {
        public static readonly Color32 Bark = SurfaceLook.Pack(new Color(0.34f, 0.28f, 0.22f), (int)WallMaterial.Concrete, 0.8f);
        public static readonly Color32 PaleStone = SurfaceLook.Pack(new Color32(222, 218, 208, 255), (int)WallMaterial.Stone, 0.3f);
        public static readonly Color32 DarkStone = SurfaceLook.Pack(new Color32(96, 94, 92, 255), (int)WallMaterial.Stone, 0.4f);
        public static readonly Color32 Steel = SurfaceLook.Pack(new Color(0.40f, 0.42f, 0.45f), (int)WallMaterial.Metal, 0.35f);
        public static readonly Color32 LampWarm = SurfaceLook.PackFloodlit(new Color(1.0f, 0.86f, 0.62f));

        /// <summary>Sail and umbrella fabrics: rust, sand, slate, bottle green, faded blue.</summary>
        private static readonly Color[] Fabrics =
        {
            new Color(0.62f, 0.27f, 0.18f), new Color(0.80f, 0.70f, 0.50f), new Color(0.42f, 0.45f, 0.50f), new Color(0.16f, 0.36f, 0.28f), new Color(0.30f, 0.42f, 0.62f),
        };

        /// <summary>Cube seats: yellow, red, blue.</summary>
        private static readonly Color[] CubeColours = { new Color(0.95f, 0.75f, 0.15f), new Color(0.80f, 0.15f, 0.12f), new Color(0.15f, 0.35f, 0.75f) };

        private static Color32 Fabric(int seed) => SurfaceLook.Pack(Fabrics[(int)(Hash(seed, 7, 31) % (uint)Fabrics.Length)], (int)WallMaterial.Plaster, 0.5f);

        /// <summary>
        /// A beam of square section from <paramref name="a"/> to <paramref name="b"/>, <paramref name="half"/> either side: branches, posts at a
        /// tilt, slats, rails, a fallen piece. Four long faces and two ends.
        /// </summary>
        public static void AddBeam(MeshBuffer buffer, Vector3 a, Vector3 b, float half)
        {
            Vector3 axis = b - a;
            float length = axis.magnitude;
            if (length < 1e-4f) return;
            axis /= length;
            Vector3 side = Mathf.Abs(axis.y) < 0.9f ? Vector3.Cross(axis, Vector3.up).normalized : Vector3.Cross(axis, Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, axis).normalized;
            Vector3 s = side * half, u = up * half;
            Vector2 uv0 = Vector2.zero, uv1 = new Vector2(length, 0.0f), uv2 = new Vector2(length, half * 2.0f), uv3 = new Vector2(0.0f, half * 2.0f);
            buffer.AddQuad(a + s - u, b + s - u, b + s + u, a + s + u, side, uv0, uv1, uv2, uv3);
            buffer.AddQuad(a - s + u, b - s + u, b - s - u, a - s - u, -side, uv0, uv1, uv2, uv3);
            buffer.AddQuad(a + u + s, b + u + s, b + u - s, a + u - s, up, uv0, uv1, uv2, uv3);
            buffer.AddQuad(a - u - s, b - u - s, b - u + s, a - u + s, -up, uv0, uv1, uv2, uv3);
            Vector2 e = new Vector2(half * 2.0f, half * 2.0f);
            buffer.AddQuad(b - s - u, b + s - u, b + s + u, b - s + u, axis, Vector2.zero, new Vector2(e.x, 0), e, new Vector2(0, e.y));
            buffer.AddQuad(a + s - u, a - s - u, a - s + u, a + s + u, -axis, Vector2.zero, new Vector2(e.x, 0), e, new Vector2(0, e.y));
        }

        /// <summary>A triangle seen from both sides (a sail, a canopy panel).</summary>
        private static void AddDoubleTriangle(MeshBuffer buffer, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Vector2 ua = new Vector2(a.x, a.z), ub = new Vector2(b.x, b.z), uc = new Vector2(c.x, c.z);
            buffer.AddTriangle(a, b, c, n, ua, ub, uc);
            buffer.AddTriangle(a, c, b, -n, ua, uc, ub);
        }

        /// <summary>A concrete seat block 1.2 x 0.6 m, 0.45 m high (seat height); some with a timber top.</summary>
        public static void AddSeatBlock(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed)
        {
            Color32 saved = b.Look;
            bool timber = Unit(seed, 2, 41) < 0.35f;
            b.Look = Concrete;
            AddTurnedBox(b, cx, cz, 0.6f, 0.3f, cos, sin, y, y + (timber ? 0.40f : 0.45f));
            if (timber)
            {
                b.Look = Wood;
                AddTurnedBox(b, cx, cz, 0.62f, 0.32f, cos, sin, y + 0.40f, y + 0.45f);
            }
            b.Look = saved;
        }

        /// <summary>A shade sail: three steel posts round a point and a canted triangle of fabric between their tops; torn means the posts stand bare.</summary>
        public static void AddShadeSail(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed, bool torn)
        {
            Color32 saved = b.Look;
            b.Look = Steel;
            var tops = new Vector3[3];
            for (int k = 0; k < 3; k++)
            {
                float angle = k * 2.0943951f + Unit(seed, k, 43) * 0.6f;
                float r = 2.4f + 0.5f * Unit(seed, k, 47);
                float lx = Mathf.Cos(angle) * r, lz = Mathf.Sin(angle) * r;
                float px = cx + lx * cos - lz * sin, pz = cz + lx * sin + lz * cos;
                float height = 3.2f + 0.8f * Unit(seed, k, 53);
                AddBeam(b, new Vector3(px, y, pz), new Vector3(px, y + height, pz), 0.07f);
                tops[k] = new Vector3(px, y + height - 0.1f, pz);
            }
            if (!torn)
            {
                b.Look = Fabric(seed);
                AddDoubleTriangle(b, tops[0], tops[1], tops[2]);
            }
            b.Look = saved;
        }

        /// <summary>A round table with an umbrella over it and three or four chairs round it; fallen means the umbrella is down on the ground and a chair over.</summary>
        public static void AddUmbrellaTable(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed, bool fallen)
        {
            Color32 saved = b.Look;
            b.Look = DarkMetal;
            AddTurnedBox(b, cx, cz, 0.05f, 0.05f, cos, sin, y, y + 0.74f);
            AddTurnedBox(b, cx, cz, 0.30f, 0.30f, cos, sin, y, y + 0.03f);
            b.Look = Wood;
            AddTurnedBox(b, cx, cz, 0.42f, 0.42f, cos, sin, y + 0.74f, y + 0.78f);
            Color32 fabric = Fabric(seed);
            if (!fallen)
            {
                b.Look = DarkMetal;
                AddTurnedBox(b, cx, cz, 0.025f, 0.025f, cos, sin, y + 0.78f, y + 2.35f);
                b.Look = fabric;
                var apex = new Vector3(cx, y + 2.45f, cz);
                for (int k = 0; k < 8; k++)
                {
                    float a0 = k * 0.7853982f, a1 = (k + 1) * 0.7853982f;
                    var p = new Vector3(cx + Mathf.Cos(a0) * 1.25f, y + 2.0f, cz + Mathf.Sin(a0) * 1.25f);
                    var q = new Vector3(cx + Mathf.Cos(a1) * 1.25f, y + 2.0f, cz + Mathf.Sin(a1) * 1.25f);
                    AddDoubleTriangle(b, apex, q, p);
                }
            }
            else
            {
                // The umbrella lies folded beside the table.
                b.Look = fabric;
                float lx = 1.1f, lz = 0.6f;
                float px = cx + lx * cos - lz * sin, pz = cz + lx * sin + lz * cos;
                AddBeam(b, new Vector3(px, y + 0.08f, pz), new Vector3(px + 2.2f * sin, y + 0.08f, pz - 2.2f * cos), 0.09f);
            }
            int chairs = 3 + (int)(Hash(seed, 5, 59) % 2u);
            for (int k = 0; k < chairs; k++)
            {
                float angle = k * 6.2831853f / chairs + 0.4f;
                float lx = Mathf.Cos(angle) * 0.95f, lz = Mathf.Sin(angle) * 0.95f;
                float px = cx + lx * cos - lz * sin, pz = cz + lx * sin + lz * cos;
                // Facing the table: the chair's +x points at it.
                float fx = -(lx * cos - lz * sin), fz = -(lx * sin + lz * cos);
                float fl = Mathf.Sqrt(fx * fx + fz * fz);
                AddChair(b, px, pz, y, fx / fl, fz / fl, seed * 7 + k, fallen && k == 0);
            }
            b.Look = saved;
        }

        /// <summary>A cafe chair: a seat on four legs and a back behind it (the back is on the -x side; +x faces where it was looking). Fallen: on its side.</summary>
        public static void AddChair(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed, bool fallen)
        {
            Color32 saved = b.Look;
            b.Look = DarkMetal;
            if (!fallen)
            {
                for (int k = 0; k < 4; k++)
                {
                    float lx = (k & 1) == 0 ? -0.19f : 0.19f, lz = (k & 2) == 0 ? -0.19f : 0.19f;
                    AddTurnedBox(b, cx + lx * cos - lz * sin, cz + lx * sin + lz * cos, 0.02f, 0.02f, cos, sin, y, y + 0.44f);
                }
                b.Look = Wood;
                AddTurnedBox(b, cx, cz, 0.22f, 0.22f, cos, sin, y + 0.44f, y + 0.48f);
                float bx = -0.21f;
                AddTurnedBox(b, cx + bx * cos, cz + bx * sin, 0.02f, 0.22f, cos, sin, y + 0.48f, y + 0.90f);
            }
            else
            {
                // On its side: the seat stands on edge, the back lies flat, the legs point along the ground.
                b.Look = Wood;
                AddTurnedBox(b, cx, cz, 0.22f, 0.02f, cos, sin, y, y + 0.44f);
                float bx = 0.23f;
                AddTurnedBox(b, cx + bx * cos, cz + bx * sin, 0.21f, 0.44f, cos, sin, y + 0.4f, y + 0.44f);
                b.Look = DarkMetal;
                for (int k = 0; k < 2; k++)
                {
                    // Two legs lying along the ground, pointing back along -x from the seat's edge.
                    float lz = k == 0 ? -0.19f : 0.19f;
                    float sx = -0.02f * cos - lz * sin, sz = -0.02f * sin + lz * cos;
                    AddBeam(b, new Vector3(cx + sx, y + 0.05f, cz + sz), new Vector3(cx + sx - 0.44f * cos, y + 0.05f, cz + sz - 0.44f * sin), 0.02f);
                }
            }
            b.Look = saved;
        }

        /// <summary>A picnic table: a top, a bench either side, A-frame legs.</summary>
        public static void AddPicnicTable(MeshBuffer b, float cx, float cz, float y, float cos, float sin)
        {
            Color32 saved = b.Look;
            b.Look = Wood;
            AddTurnedBox(b, cx, cz, 0.9f, 0.38f, cos, sin, y + 0.72f, y + 0.77f);
            for (int side = -1; side <= 1; side += 2)
            {
                float lz = side * 0.62f;
                AddTurnedBox(b, cx - lz * sin, cz + lz * cos, 0.9f, 0.15f, cos, sin, y + 0.44f, y + 0.48f);
            }
            b.Look = DarkMetal;
            for (int end = -1; end <= 1; end += 2)
            {
                // An A-frame at each end: two legs from the ground, 0.75 m either side across, up to the middle under the top, and a brace across.
                float lx = end * 0.7f;
                float px = cx + lx * cos, pz = cz + lx * sin;
                for (int side = -1; side <= 1; side += 2)
                {
                    float lz = side * 0.75f;
                    AddBeam(b, new Vector3(px - lz * sin, y, pz + lz * cos), new Vector3(px, y + 0.72f, pz), 0.025f);
                }
                AddTurnedBox(b, px, pz, 0.025f, 0.75f, cos, sin, y + 0.42f, y + 0.46f);
            }
            b.Look = saved;
        }

        /// <summary>A painted cube seat, 0.45 m.</summary>
        public static void AddCubeSeat(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed)
        {
            Color32 saved = b.Look;
            b.Look = SurfaceLook.Pack(CubeColours[(int)(Hash(seed, 9, 61) % (uint)CubeColours.Length)], (int)WallMaterial.Metal, 0.45f);
            AddTurnedBox(b, cx, cz, 0.225f, 0.225f, cos, sin, y, y + 0.45f);
            b.Look = saved;
        }

        /// <summary>A bollard light 0.9 m tall with a band that glows at night on a powered block (into <paramref name="lit"/>), dark otherwise.</summary>
        public static void AddBollardLight(MeshBuffer b, MeshBuffer lit, float cx, float cz, float y, bool powered)
        {
            Color32 saved = b.Look;
            b.Look = DarkMetal;
            AddTurnedBox(b, cx, cz, 0.12f, 0.12f, 1.0f, 0.0f, y, y + 0.68f);
            AddTurnedBox(b, cx, cz, 0.12f, 0.12f, 1.0f, 0.0f, y + 0.82f, y + 0.92f);
            MeshBuffer band = lit != null && powered ? lit : b;
            Color32 savedBand = band.Look;
            band.Look = lit != null && powered ? LampWarm : SurfaceLook.Pack(new Color(0.70f, 0.68f, 0.60f), (int)WallMaterial.Plaster, 0.2f);
            AddTurnedBox(band, cx, cz, 0.11f, 0.11f, 1.0f, 0.0f, y + 0.68f, y + 0.82f);
            band.Look = savedBand;
            b.Look = saved;
        }

        /// <summary>A dead tree: a tapering trunk and a few bare branches going out and up, with twigs off them. Nothing has grown; what stood, stands.</summary>
        public static void AddDeadTree(MeshBuffer b, float cx, float cz, float y, int seed)
        {
            Color32 saved = b.Look;
            b.Look = Bark;
            float height = 5.0f + 3.5f * Unit(seed, 1, 67);
            var foot = new Vector3(cx, y, cz);
            var mid = new Vector3(cx + 0.15f * (Unit(seed, 2, 67) - 0.5f), y + height * 0.5f, cz + 0.15f * (Unit(seed, 3, 67) - 0.5f));
            var top = new Vector3(mid.x + 0.3f * (Unit(seed, 4, 67) - 0.5f), y + height, mid.z + 0.3f * (Unit(seed, 5, 67) - 0.5f));
            AddBeam(b, foot, mid, 0.16f);
            AddBeam(b, mid, top, 0.10f);
            int branches = 4 + (int)(Hash(seed, 6, 67) % 4u);
            for (int k = 0; k < branches; k++)
            {
                float at = 0.45f + 0.5f * Unit(seed, 10 + k, 71);
                Vector3 from = Vector3.Lerp(foot, top, at);
                float yaw = Unit(seed, 20 + k, 71) * 6.2831853f;
                float rise = 0.5f + 0.9f * Unit(seed, 30 + k, 71);
                float length = 1.4f + 1.6f * Unit(seed, 40 + k, 71);
                var dir = new Vector3(Mathf.Cos(yaw), rise, Mathf.Sin(yaw)).normalized;
                Vector3 end = from + dir * length;
                AddBeam(b, from, end, 0.05f);
                // Two twigs off each branch.
                for (int t = 0; t < 2; t++)
                {
                    Vector3 start = Vector3.Lerp(from, end, 0.55f + 0.3f * t);
                    float tyaw = yaw + (t == 0 ? 0.9f : -0.9f) + 0.4f * (Unit(seed, 50 + k * 2 + t, 71) - 0.5f);
                    var tdir = new Vector3(Mathf.Cos(tyaw), rise + 0.4f, Mathf.Sin(tyaw)).normalized;
                    AddBeam(b, start, start + tdir * (0.6f + 0.6f * Unit(seed, 60 + k * 2 + t, 71)), 0.025f);
                }
            }
            b.Look = saved;
        }

        /// <summary>A raised square planter 1.2 m across with a dead tree in it.</summary>
        public static void AddTreePlanter(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed)
        {
            AddPlanter(b, cx, cz, y, 0.6f, 0.6f, 0.6f, cos, sin);
            AddDeadTree(b, cx, cz, y + 0.5f, seed);
        }

        /// <summary>A round bench of eight timber segments on a concrete ring round a dead tree.</summary>
        public static void AddRoundBench(MeshBuffer b, float cx, float cz, float y, int seed)
        {
            Color32 saved = b.Look;
            float radius = 1.0f;
            float segment = radius * 0.4142f + 0.1f;
            for (int k = 0; k < 8; k++)
            {
                float angle = k * 0.7853982f;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                b.Look = Concrete;
                AddTurnedBox(b, cx + c * radius, cz + s * radius, segment, 0.24f, -s, c, y, y + 0.40f);
                b.Look = Wood;
                AddTurnedBox(b, cx + c * radius, cz + s * radius, segment, 0.26f, -s, c, y + 0.40f, y + 0.46f);
            }
            b.Look = Earth;
            AddTurnedBox(b, cx, cz, 0.7f, 0.7f, 1.0f, 0.0f, y, y + 0.3f);
            b.Look = saved;
            AddDeadTree(b, cx, cz, y + 0.25f, seed);
        }

        /// <summary>A pergola 6 x 4 m: six posts, two beams along, slats across.</summary>
        public static void AddPergola(MeshBuffer b, float cx, float cz, float y, float cos, float sin)
        {
            Color32 saved = b.Look;
            b.Look = Steel;
            Vector3 P(float lx, float lz, float h) => new Vector3(cx + lx * cos - lz * sin, y + h, cz + lx * sin + lz * cos);
            for (int i = -1; i <= 1; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    AddBeam(b, P(i * 2.8f, side * 1.8f, 0.0f), P(i * 2.8f, side * 1.8f, 2.9f), 0.09f);
                }
            }
            b.Look = Wood;
            for (int side = -1; side <= 1; side += 2)
            {
                AddBeam(b, P(-3.0f, side * 1.8f, 2.9f), P(3.0f, side * 1.8f, 2.9f), 0.10f);
            }
            for (int k = 0; k < 9; k++)
            {
                float lx = -2.8f + k * 0.7f;
                AddBeam(b, P(lx, -2.1f, 3.05f), P(lx, 2.1f, 3.05f), 0.06f);
            }
            b.Look = saved;
        }

        /// <summary>A tiered tower (the Peace Plaza pagoda's shape): a core with five concrete tiers narrowing as they rise, and a finial.</summary>
        public static void AddTower(MeshBuffer b, float cx, float cz, float y, float height)
        {
            Color32 saved = b.Look;
            b.Look = PaleStone;
            AddTurnedBox(b, cx, cz, 1.4f, 1.4f, 1.0f, 0.0f, y, y + height);
            b.Look = Concrete;
            const int tiers = 5;
            for (int k = 0; k < tiers; k++)
            {
                float h = y + height * (0.18f + 0.19f * k);
                float half = 3.2f - 0.3f * k;
                AddTurnedBox(b, cx, cz, half, half, 1.0f, 0.0f, h, h + 0.28f, true);
                b.Look = DarkStone;
                AddTurnedBox(b, cx, cz, half, half, 1.0f, 0.0f, h + 0.28f, h + 0.55f);
                AddTurnedBox(b, cx, cz, half - 0.25f, half - 0.25f, 1.0f, 0.0f, h + 0.55f, h + 0.65f);
                b.Look = Concrete;
            }
            b.Look = DarkMetal;
            AddBeam(b, new Vector3(cx, y + height, cz), new Vector3(cx, y + height + 2.4f, cz), 0.09f);
            b.Look = saved;
        }

        /// <summary>A chessboard of 8 x 8 squares, 0.6 m each, laid on the paving.</summary>
        public static void AddChessboard(MeshBuffer b, float cx, float cz, float y, float cos, float sin)
        {
            Color32 saved = b.Look;
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    b.Look = (i + j) % 2 == 0 ? PaleStone : DarkStone;
                    float lx = (i - 3.5f) * 0.6f, lz = (j - 3.5f) * 0.6f;
                    AddTurnedBox(b, cx + lx * cos - lz * sin, cz + lx * sin + lz * cos, 0.3f, 0.3f, cos, sin, y, y + 0.025f);
                }
            }
            b.Look = saved;
        }

        /// <summary>A big chess piece 0.9 m tall, light or dark; fallen: lying on the board.</summary>
        public static void AddChessPiece(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed, bool fallen)
        {
            Color32 saved = b.Look;
            b.Look = (Hash(seed, 3, 73) & 1u) == 0u ? PaleStone : DarkStone;
            if (!fallen)
            {
                AddTurnedBox(b, cx, cz, 0.2f, 0.2f, cos, sin, y, y + 0.12f);
                AddTurnedBox(b, cx, cz, 0.11f, 0.11f, cos, sin, y + 0.12f, y + 0.72f);
                AddTurnedBox(b, cx, cz, 0.16f, 0.16f, cos, sin, y + 0.72f, y + 0.9f);
            }
            else
            {
                AddBeam(b, new Vector3(cx - 0.4f * cos, y + 0.12f, cz - 0.4f * sin), new Vector3(cx + 0.5f * cos, y + 0.12f, cz + 0.5f * sin), 0.11f);
            }
            b.Look = saved;
        }

        /// <summary>A bike rack: three steel hoops in a row along +x.</summary>
        public static void AddBikeRack(MeshBuffer b, float cx, float cz, float y, float cos, float sin)
        {
            Color32 saved = b.Look;
            b.Look = Steel;
            for (int k = -1; k <= 1; k++)
            {
                float lx = k * 0.8f;
                float px = cx + lx * cos, pz = cz + lx * sin;
                for (int side = -1; side <= 1; side += 2)
                {
                    float lz = side * 0.3f;
                    AddTurnedBox(b, px - lz * sin, pz + lz * cos, 0.025f, 0.025f, cos, sin, y, y + 0.8f);
                }
                AddTurnedBox(b, px, pz, 0.025f, 0.33f, cos, sin, y + 0.8f, y + 0.85f);
            }
            b.Look = saved;
        }

        /// <summary>A low name wall with a white band and dark lettering bars on its +z face (there is no font in the city: names are bars).</summary>
        public static void AddNameWall(MeshBuffer b, float cx, float cz, float y, float cos, float sin, int seed)
        {
            Color32 saved = b.Look;
            b.Look = DarkConcrete;
            AddTurnedBox(b, cx, cz, 1.5f, 0.15f, cos, sin, y, y + 1.4f);
            b.Look = White;
            float fz = 0.16f;
            AddTurnedBox(b, cx - fz * sin, cz + fz * cos, 1.3f, 0.01f, cos, sin, y + 0.75f, y + 1.15f);
            b.Look = DarkMetal;
            int letters = 4 + (int)(Hash(seed, 2, 79) % 4u);
            float gz = 0.18f;
            for (int k = 0; k < letters; k++)
            {
                float lx = -1.1f + k * (2.2f / (letters - 1 + 0.0001f)) * 0.92f;
                float tall = 0.16f + 0.1f * Unit(seed, k, 83);
                AddTurnedBox(b, cx + lx * cos - gz * sin, cz + lx * sin + gz * cos, 0.06f, 0.005f, cos, sin, y + 0.82f, y + 0.82f + tall);
            }
            b.Look = saved;
        }

        /// <summary>A hoarding along a vacant lot's street: painted panels 2.4 m wide on posts, with a gap of <paramref name="gap"/> metres in the middle.</summary>
        public static void AddHoarding(MeshBuffer b, float cx, float cz, float y, float cos, float sin, float halfLength, float gap, int seed)
        {
            Color32 saved = b.Look;
            Color paint = Palette[(int)(Hash(seed, 4, 89) % (uint)Palette.Length)];
            Color32 panel = SurfaceLook.Pack(Color.Lerp(paint, new Color(0.6f, 0.58f, 0.55f), 0.45f), (int)WallMaterial.Metal, 0.7f);
            for (float lx = -halfLength; lx < halfLength; lx += 2.4f)
            {
                float x0 = lx, x1 = Mathf.Min(halfLength, lx + 2.4f);
                if (gap <= 0.0f || x1 < -gap * 0.5f || x0 > gap * 0.5f)
                {
                    float mx = (x0 + x1) * 0.5f;
                    b.Look = panel;
                    AddTurnedBox(b, cx + mx * cos, cz + mx * sin, (x1 - x0) * 0.5f - 0.02f, 0.03f, cos, sin, y, y + 2.2f);
                }
                b.Look = DarkMetal;
                AddTurnedBox(b, cx + x0 * cos, cz + x0 * sin, 0.05f, 0.05f, cos, sin, y, y + 2.3f);
            }
            b.Look = saved;
        }

        /// <summary>A street bin, the same one as on the pavements.</summary>
        public static void AddBin(MeshBuffer b, float cx, float cz, float y, float cos, float sin)
        {
            Color32 saved = b.Look;
            b.Look = SurfaceLook.Pack(new Color(0.22f, 0.24f, 0.26f), (int)WallMaterial.Metal, 0.5f);
            AddTurnedBox(b, cx, cz, 0.36f, 0.36f, cos, sin, y, y + 1.1f);
            b.Look = DarkMetal;
            AddTurnedBox(b, cx, cz, 0.40f, 0.40f, cos, sin, y + 1.1f, y + 1.2f);
            b.Look = saved;
        }
    }
}

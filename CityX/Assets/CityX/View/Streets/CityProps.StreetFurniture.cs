using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// What stands in an American sidewalk's furnishing zone (World/StreetFurniture, docs/AMERICAN_STREETS.md): the hydrant, the parking
    /// meter, the mailbox, the newspaper boxes, the street tree in its grate, the utility cabinet. Each is built of turned boxes, square to
    /// its kerb, sized for the 2.6 m player (about 1.3x real).
    /// </summary>
    public static partial class CityProps
    {
        private static readonly Color32 HydrantRed = SurfaceLook.Pack(new Color(0.72f, 0.13f, 0.10f), (int)WallMaterial.Metal, 0.25f);
        private static readonly Color32 HydrantYellow = SurfaceLook.Pack(new Color(0.86f, 0.68f, 0.12f), (int)WallMaterial.Metal, 0.25f);
        private static readonly Color32 HydrantGreen = SurfaceLook.Pack(new Color(0.14f, 0.40f, 0.22f), (int)WallMaterial.Metal, 0.3f);
        private static readonly Color32 MailBlue = SurfaceLook.Pack(new Color(0.09f, 0.19f, 0.45f), (int)WallMaterial.Metal, 0.3f);
        private static readonly Color32 MeterGrey = SurfaceLook.Pack(new Color(0.40f, 0.42f, 0.44f), (int)WallMaterial.Metal, 0.3f);
        private static readonly Color32 CabinetGrey = SurfaceLook.Pack(new Color(0.55f, 0.57f, 0.58f), (int)WallMaterial.Metal, 0.55f);
        private static readonly Color32 CabinetCap = SurfaceLook.Pack(new Color(0.30f, 0.31f, 0.32f), (int)WallMaterial.Metal, 0.5f);

        /// <summary>The colours newspaper boxes come in.</summary>
        private static readonly Color[] NewsBoxPaint =
        {
            new Color(0.70f, 0.12f, 0.10f), new Color(0.10f, 0.22f, 0.55f), new Color(0.85f, 0.68f, 0.10f), new Color(0.10f, 0.36f, 0.20f),
            new Color(0.84f, 0.84f, 0.82f), new Color(0.90f, 0.42f, 0.08f),
        };

        public const float HydrantHeight = 0.98f, MeterHeight = 1.67f, MailboxHeight = 1.55f, NewsBoxHeight = 1.36f, TreeGrateHalf = 0.75f;

        /// <summary>
        /// A place beside a kerb to build at: where, which way the kerb runs and which way the road is. Its boxes are given by how far
        /// along the kerb and how far toward the road their middles are.
        /// </summary>
        private readonly struct KerbSide
        {
            private readonly float _x, _z, _y, _alongX, _alongZ, _roadX, _roadZ;

            public KerbSide(Vector3 at, StreetFurnishing thing)
            {
                _x = at.x;
                _y = at.y;
                _z = at.z;
                _alongX = (float)thing.Along.X;
                _alongZ = (float)thing.Along.Y;
                _roadX = (float)thing.ToRoad.X;
                _roadZ = (float)thing.ToRoad.Y;
            }

            public float X(float along, float toRoad) => _x + along * _alongX + toRoad * _roadX;

            public float Z(float along, float toRoad) => _z + along * _alongZ + toRoad * _roadZ;

            public float Y => _y;

            public float Cos => _alongX;

            public float Sin => _alongZ;

            /// <summary>A box square to the kerb: half <paramref name="halfAlong"/> along it and half <paramref name="halfAcross"/> across.</summary>
            public void Box(MeshBuffer b, float along, float toRoad, float halfAlong, float halfAcross, float y0, float y1, bool bottom = false)
            {
                AddTurnedBox(b, X(along, toRoad), Z(along, toRoad), halfAlong, halfAcross, _alongX, _alongZ, _y + y0, _y + y1, bottom);
            }

            /// <summary>A square box and the same turned an eighth of a turn through it: as near a round barrel as boxes come.</summary>
            public void Barrel(MeshBuffer b, float half, float y0, float y1)
            {
                Box(b, 0.0f, 0.0f, half, half, y0, y1);
                const float eighth = 0.70710678f;
                AddTurnedBox(b, _x, _z, half, half, (_alongX - _alongZ) * eighth, (_alongX + _alongZ) * eighth, _y + y0, _y + y1);
            }
        }

        /// <summary>One thing of a sidewalk's furnishing zone, standing at <paramref name="at"/> (on the pavement, chunk-local).</summary>
        public static void AddStreetFurnishing(MeshBuffer b, Vector3 at, StreetFurnishing thing)
        {
            Color32 saved = b.Look;
            var side = new KerbSide(at, thing);
            switch (thing.Kind)
            {
                case StreetFurnitureKind.Hydrant: Hydrant(b, side, thing.Seed); break;
                case StreetFurnitureKind.Meter: Meter(b, side); break;
                case StreetFurnitureKind.Mailbox: Mailbox(b, side); break;
                case StreetFurnitureKind.NewsBoxes: NewsBoxes(b, side, thing.Seed); break;
                case StreetFurnitureKind.Bin: AddBin(b, at.x, at.z, at.y, side.Cos, side.Sin); break;
                case StreetFurnitureKind.Cabinet: Cabinet(b, side); break;
                case StreetFurnitureKind.Tree:
                    b.Look = DarkMetal;
                    side.Box(b, 0.0f, 0.0f, TreeGrateHalf, TreeGrateHalf, 0.0f, 0.025f);
                    AddDeadTree(b, at.x, at.z, at.y, thing.Seed);
                    break;
            }
            b.Look = saved;
        }

        /// <summary>The American hydrant: a flanged barrel, a bonnet with its nut, a hose nozzle each side and the pumper's nozzle toward the road. Red, or yellow with green caps.</summary>
        private static void Hydrant(MeshBuffer b, KerbSide side, int seed)
        {
            bool yellow = (Hash(seed, 1, 83) & 1u) == 0u;
            Color32 body = yellow ? HydrantYellow : HydrantRed, caps = yellow ? HydrantGreen : HydrantRed;
            b.Look = body;
            side.Barrel(b, 0.19f, 0.0f, 0.06f);
            side.Barrel(b, 0.13f, 0.06f, 0.66f);
            side.Barrel(b, 0.17f, 0.66f, 0.71f);
            side.Box(b, 0.17f, 0.0f, 0.05f, 0.055f, 0.50f, 0.61f);
            side.Box(b, -0.17f, 0.0f, 0.05f, 0.055f, 0.50f, 0.61f);
            side.Box(b, 0.0f, 0.18f, 0.075f, 0.06f, 0.44f, 0.59f);
            b.Look = caps;
            side.Barrel(b, 0.14f, 0.71f, 0.82f);
            side.Barrel(b, 0.10f, 0.82f, 0.90f);
            side.Box(b, 0.235f, 0.0f, 0.02f, 0.065f, 0.49f, 0.62f);
            side.Box(b, -0.235f, 0.0f, 0.02f, 0.065f, 0.49f, 0.62f);
            side.Box(b, 0.0f, 0.25f, 0.085f, 0.02f, 0.43f, 0.60f);
            b.Look = DarkMetal;
            side.Box(b, 0.0f, 0.0f, 0.04f, 0.04f, 0.90f, HydrantHeight);
        }

        /// <summary>A parking meter: a post, a head with a rounded top, its dark window toward the walk.</summary>
        private static void Meter(MeshBuffer b, KerbSide side)
        {
            b.Look = MeterGrey;
            side.Box(b, 0.0f, 0.0f, 0.035f, 0.035f, 0.0f, 1.32f);
            side.Box(b, 0.0f, 0.0f, 0.11f, 0.07f, 1.32f, 1.60f, true);
            side.Box(b, 0.0f, 0.0f, 0.08f, 0.07f, 1.60f, MeterHeight);
            b.Look = DarkMetal;
            side.Box(b, 0.0f, -0.074f, 0.075f, 0.008f, 1.46f, 1.58f);
            side.Box(b, 0.0f, 0.074f, 0.075f, 0.008f, 1.46f, 1.58f);
        }

        /// <summary>The blue collection box: four short legs, a square body, a rounded top with its ridge along the kerb, the pull-down door toward the walk.</summary>
        private static void Mailbox(MeshBuffer b, KerbSide side)
        {
            b.Look = MailBlue;
            for (int i = 0; i < 4; i++)
            {
                side.Box(b, (i % 2 == 0 ? -1.0f : 1.0f) * 0.26f, (i < 2 ? -1.0f : 1.0f) * 0.26f, 0.04f, 0.04f, 0.0f, 0.22f);
            }
            side.Box(b, 0.0f, 0.0f, 0.33f, 0.33f, 0.22f, 1.25f, true);
            side.Box(b, 0.0f, 0.0f, 0.33f, 0.29f, 1.25f, 1.40f);
            side.Box(b, 0.0f, 0.0f, 0.33f, 0.21f, 1.40f, 1.50f);
            side.Box(b, 0.0f, 0.0f, 0.33f, 0.11f, 1.50f, MailboxHeight);
            b.Look = DarkMetal;
            side.Box(b, 0.0f, -0.335f, 0.22f, 0.01f, 1.17f, 1.24f);
            b.Look = White;
            side.Box(b, 0.0f, -0.335f, 0.20f, 0.008f, 0.74f, 0.94f);
        }

        /// <summary>Two or three newspaper boxes in a row, each its own colour: a box on a post, its window toward the walk, a coin head on top.</summary>
        private static void NewsBoxes(MeshBuffer b, KerbSide side, int seed)
        {
            int count = 2 + (int)(Hash(seed, 2, 89) % 2u);
            const float pitch = 0.58f;
            for (int i = 0; i < count; i++)
            {
                float along = (i - (count - 1) * 0.5f) * pitch;
                Color paint = NewsBoxPaint[(int)(Hash(seed, 10 + i, 89) % (uint)NewsBoxPaint.Length)];
                b.Look = DarkMetal;
                side.Box(b, along, 0.0f, 0.20f, 0.18f, 0.0f, 0.04f);
                side.Box(b, along, 0.0f, 0.06f, 0.06f, 0.04f, 0.45f);
                b.Look = SurfaceLook.Pack(paint, (int)WallMaterial.Metal, 0.35f);
                side.Box(b, along, 0.0f, 0.24f, 0.21f, 0.45f, 1.25f, true);
                side.Box(b, along, 0.0f, 0.10f, 0.08f, 1.25f, NewsBoxHeight);
                b.Look = DarkMetal;
                side.Box(b, along, -0.214f, 0.19f, 0.008f, 0.80f, 1.18f);
            }
        }

        /// <summary>A utility cabinet: a grey steel box with a darker cap.</summary>
        private static void Cabinet(MeshBuffer b, KerbSide side)
        {
            b.Look = CabinetGrey;
            side.Box(b, 0.0f, 0.0f, 0.8f, 0.35f, 0.0f, 1.8f);
            b.Look = CabinetCap;
            side.Box(b, 0.0f, 0.0f, 0.85f, 0.4f, 1.8f, 1.9f);
        }
    }
}

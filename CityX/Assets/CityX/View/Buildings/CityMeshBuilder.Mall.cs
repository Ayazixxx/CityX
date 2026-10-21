using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A shopping mall, in the builder's frame (x along the piece's length, +z its "front", y absolute). A mall is a complex of pieces
    /// (Amenities.MallLayout): CORRIDORS (SpecialTypes.Mall) -- a concourse down the middle with small shops either side on two or three
    /// levels, open through the middle so the galleries look down onto the ground floor -- and ANCHOR department stores (SpecialTypes.MallAnchor)
    /// capping their ends and flanks. Where two pieces touch they share a wall (a WallContact on each), and a PASSAGE opens through it at the
    /// middle of the shared stretch, the same point seen from both sides: where a side corridor or an anchor meets a corridor's flank, the shops
    /// there make way for it. The main corridor (the spine) has the FOOD COURT and the fountain; every corridor has its escalators; an end with
    /// nothing against it is an entrance under a canopy.
    ///
    /// Research (2026-10-12): inline stores are 20-40 ft across and 40-60 ft deep; anchors 20,000-200,000 sq ft at the ends and flanks; the food
    /// court where people converge; real plans are "wacky, with a few large stores and a ton of smaller ones" (the owner, with a real mall's plan).
    /// Abandoned: most shutters are down, some shelving is over, the fountain is dry, there is rubble in the concourse.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        public const float MallWallThickness = 0.4f;
        public const float MallConcourseWidth = 14.0f;
        public const float MallGalleryWidth = 3.5f;
        public const float MallStoreDepth = 20.0f;
        public const float MallSlabThickness = 0.4f;
        public const float MallEntranceWidth = 10.0f;

        /// <summary>A passage between two pieces of a mall: as wide as the concourse less its shopfronts, and under the first floor's slab.</summary>
        public const float MallPassageWidth = 12.0f;
        public const float MallPassageHeight = 5.6f;

        /// <summary>What share of the stores have their shutter down.</summary>
        public const float MallShutteredShare = 0.55f;

        /// <summary>A corridor this long or longer has an anchor of its own at an end with nothing against it.</summary>
        public const float MallInternalAnchorLength = 130.0f;

        /// <summary>A mall is clad light (render and concrete panel) over a darker base band: a street shop's dark cladding on a 170 m box read as a black slab.</summary>
        private static readonly Color32 MallWallLook = SurfaceLook.Pack(new Color(0.80f, 0.78f, 0.74f), (int)WallMaterial.Plaster, 0.45f);
        private static readonly Color32 MallBaseLook = SurfaceLook.Pack(new Color(0.46f, 0.46f, 0.47f), (int)WallMaterial.Concrete, 0.6f);
        private static readonly Color32 MallDarkLook = SurfaceLook.Pack(new Color(0.12f, 0.12f, 0.13f), (int)WallMaterial.Concrete, 0.8f);

        private static readonly Color32 MallFloorLook = SurfaceLook.Pack(new Color(0.86f, 0.85f, 0.82f), SurfaceLook.Paving, 0.3f);
        private static readonly Color32 MallCeilingLook = SurfaceLook.Pack(new Color(0.80f, 0.80f, 0.78f), (int)WallMaterial.Plaster, 0.4f);
        private static readonly Color32 ShutterLook = SurfaceLook.Pack(new Color(0.42f, 0.43f, 0.45f), (int)WallMaterial.Metal, 0.7f);
        private static readonly Color32 CounterLook = SurfaceLook.Pack(new Color(0.55f, 0.50f, 0.42f), (int)WallMaterial.Metal, 0.5f);
        private static readonly Color32 ShelfLook = SurfaceLook.Pack(new Color(0.75f, 0.74f, 0.70f), (int)WallMaterial.Metal, 0.55f);

        /// <summary>Brand colours: a mall's signs shout, louder still than a street shop's (ShopSignColours).</summary>
        private static readonly Color[] MallSignColours =
        {
            new Color(0.80f, 0.12f, 0.10f), new Color(0.10f, 0.25f, 0.70f), new Color(0.90f, 0.45f, 0.08f), new Color(0.08f, 0.45f, 0.42f),
            new Color(0.55f, 0.10f, 0.45f), new Color(0.20f, 0.20f, 0.22f), new Color(0.85f, 0.70f, 0.15f),
        };

        private static Color32 MallSignLook(int seed, int salt) =>
            SurfaceLook.Pack(MallSignColours[(int)(CityProps.Hash(seed, salt, 131) % (uint)MallSignColours.Length)], (int)WallMaterial.Metal, 0.45f);

        /// <summary>Where a piece of a mall meets another: the wall, and the middle of the shared stretch along it (x for north/south, z for east/west, from the piece's centre).</summary>
        private readonly struct MallJoin
        {
            public readonly WindowGrid.Wall Wall;
            public readonly float Centre;

            public MallJoin(WindowGrid.Wall wall, float centre)
            {
                Wall = wall;
                Centre = centre;
            }
        }

        private static List<MallJoin> MallJoins(BuildingSite site)
        {
            var joins = new List<MallJoin>();
            if (site.Contacts == null) return joins;
            foreach (WallContact c in site.Contacts)
            {
                bool northSouth = c.Wall == WindowGrid.Wall.North || c.Wall == WindowGrid.Wall.South;
                float half = northSouth ? site.Params.Width * 0.5f : site.Params.Depth * 0.5f;
                joins.Add(new MallJoin(c.Wall, (c.U0 + c.U1) * 0.5f - half));
            }
            return joins;
        }

        /// <summary>An opening in a wall, along it (a to b), up to <see cref="Top"/>.</summary>
        private struct WallOpening
        {
            public float A, B, Top;

            public WallOpening(float a, float b, float top)
            {
                A = a;
                B = b;
                Top = top;
            }
        }

        private static void BuildMall(BuildingSite site, Vector3 origin, MeshBuffer structures, MeshBuffer concrete, MeshBuffer darkGlass, SiteLooks looks, bool far)
        {
            float x0 = (float)(site.MinX - origin.x), x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z), z1 = (float)(site.MaxZ - origin.z);
            float podiumY = site.PodiumBase - origin.y;
            float floorY = site.BaseHeight - origin.y;
            float height = site.Params.Height;
            int levels = Mathf.Max(2, Mathf.RoundToInt(height / (float)CityX.World.Plan.Amenities.MallLevelHeight));
            float levelH = height / levels;
            float roofY = floorY + height;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            float length = x1 - x0;
            float t = MallWallThickness;
            int seed = site.Seed;
            bool spine = !site.Wing;
            float half = MallConcourseWidth * 0.5f;
            float entranceTop = floorY + Mathf.Min(levelH - 1.2f, MallPassageHeight - 0.4f);
            float passageTop = floorY + MallPassageHeight;

            // Where it meets the rest of the mall.
            var northPassages = new List<float>();
            var southPassages = new List<float>();
            float westJoin = float.NaN, eastJoin = float.NaN;
            foreach (MallJoin j in MallJoins(site))
            {
                switch (j.Wall)
                {
                    case WindowGrid.Wall.North: northPassages.Add(cx + j.Centre); break;
                    case WindowGrid.Wall.South: southPassages.Add(cx + j.Centre); break;
                    case WindowGrid.Wall.West: westJoin = cz + j.Centre; break;
                    default: eastJoin = cz + j.Centre; break;
                }
            }

            // Its own doors to the outside: an open end, and one on the front (or the back) where no other piece stands against it.
            bool westDoor = float.IsNaN(westJoin), eastDoor = float.IsNaN(eastJoin);
            float frontDoor = FreeSpot(cx, length, x0, x1, northPassages);
            float backDoor = float.NaN;
            if (float.IsNaN(frontDoor) || (!westDoor && !eastDoor && length > 120.0f))
            {
                backDoor = FreeSpot(cx, length, x0, x1, southPassages);
            }

            if (floorY - podiumY > 0.02f)
            {
                concrete.Look = MallBaseLook;
                concrete.AddBox(new Vector3(x0, podiumY, z0), new Vector3(x1, floorY, z1));
            }

            // ---- the outside: blank walls with their openings, a base band, canopies over the doors ----
            var northOpen = new List<WallOpening>();
            foreach (float p in northPassages) northOpen.Add(new WallOpening(p - MallPassageWidth * 0.5f, p + MallPassageWidth * 0.5f, passageTop));
            if (!float.IsNaN(frontDoor)) northOpen.Add(new WallOpening(frontDoor - MallEntranceWidth * 0.5f, frontDoor + MallEntranceWidth * 0.5f, entranceTop));
            var southOpen = new List<WallOpening>();
            foreach (float p in southPassages) southOpen.Add(new WallOpening(p - MallPassageWidth * 0.5f, p + MallPassageWidth * 0.5f, passageTop));
            if (!float.IsNaN(backDoor)) southOpen.Add(new WallOpening(backDoor - MallEntranceWidth * 0.5f, backDoor + MallEntranceWidth * 0.5f, entranceTop));
            var westOpen = new List<WallOpening>
            {
                westDoor ? new WallOpening(cz - 4.0f, cz + 4.0f, entranceTop) : new WallOpening(westJoin - MallPassageWidth * 0.5f, westJoin + MallPassageWidth * 0.5f, passageTop),
            };
            var eastOpen = new List<WallOpening>
            {
                eastDoor ? new WallOpening(cz - 4.0f, cz + 4.0f, entranceTop) : new WallOpening(eastJoin - MallPassageWidth * 0.5f, eastJoin + MallPassageWidth * 0.5f, passageTop),
            };
            AddMallWallX(structures, z1 - t, z1, x0, x1, floorY, roofY, northOpen, true);
            AddMallWallX(structures, z0, z0 + t, x0, x1, floorY, roofY, southOpen, false);
            AddMallWallZ(structures, x0, x0 + t, z0 + t, z1 - t, floorY, roofY, westOpen, false);
            AddMallWallZ(structures, x1 - t, x1, z0 + t, z1 - t, floorY, roofY, eastOpen, true);

            concrete.Look = looks.Trim;
            if (!float.IsNaN(frontDoor))
            {
                concrete.AddBox(new Vector3(frontDoor - MallEntranceWidth * 0.5f - 1.5f, entranceTop, z1), new Vector3(frontDoor + MallEntranceWidth * 0.5f + 1.5f, entranceTop + 0.5f, z1 + 4.5f), includeBottom: true);
            }
            if (!float.IsNaN(backDoor))
            {
                concrete.AddBox(new Vector3(backDoor - MallEntranceWidth * 0.5f - 1.5f, entranceTop, z0 - 4.5f), new Vector3(backDoor + MallEntranceWidth * 0.5f + 1.5f, entranceTop + 0.5f, z0), includeBottom: true);
            }
            if (westDoor) concrete.AddBox(new Vector3(x0 - 4.0f, entranceTop, cz - 5.5f), new Vector3(x0, entranceTop + 0.5f, cz + 5.5f), includeBottom: true);
            if (eastDoor) concrete.AddBox(new Vector3(x1, entranceTop, cz - 5.5f), new Vector3(x1 + 4.0f, entranceTop + 0.5f, cz + 5.5f), includeBottom: true);

            // A corridor long enough keeps an anchor of its own at an open end, signed on the front.
            float anchorDepth = Mathf.Clamp(length * 0.2f, 24.0f, 45.0f);
            bool westAnchor = westDoor && length >= MallInternalAnchorLength;
            bool eastAnchor = eastDoor && length >= MallInternalAnchorLength;
            float xa0 = westAnchor ? x0 + anchorDepth : x0 + t;
            float xa1 = eastAnchor ? x1 - anchorDepth : x1 - t;
            float signY = roofY - 3.6f;
            for (int end = 0; end < 2; end++)
            {
                if (end == 0 ? !westAnchor : !eastAnchor) continue;
                float sx = end == 0 ? x0 + anchorDepth * 0.5f : x1 - anchorDepth * 0.5f;
                structures.Look = MallSignLook(seed, end + 1);
                structures.AddBox(new Vector3(sx - 6.0f, signY, z1), new Vector3(sx + 6.0f, signY + 2.6f, z1 + 0.3f));
                structures.Look = CityProps.White;
                structures.AddBox(new Vector3(sx - 4.5f, signY + 0.9f, z1 + 0.3f), new Vector3(sx + 3.2f, signY + 1.7f, z1 + 0.45f));
            }
            if (spine && !float.IsNaN(frontDoor))
            {
                // The mall's own name over its main door.
                structures.Look = MallSignLook(seed, 3);
                structures.AddBox(new Vector3(frontDoor - 8.0f, roofY - 0.2f, z1 - 0.6f), new Vector3(frontDoor + 8.0f, roofY + 2.4f, z1 + 0.1f));
                structures.Look = CityProps.White;
                structures.AddBox(new Vector3(frontDoor - 6.4f, roofY + 0.6f, z1 + 0.1f), new Vector3(frontDoor + 5.0f, roofY + 1.5f, z1 + 0.25f));
            }

            // ---- the roof: slab, parapet, skylights over the concourse, plant ----
            structures.Look = MallWallLook;
            concrete.Look = looks.Roof;
            structures.AddBox(new Vector3(x0, roofY - MallSlabThickness, z0), new Vector3(x1, roofY, z1), concrete, includeBottom: true);
            AddParapetRing(structures, x0, x1, z0, z1, roofY, roofY + 1.3f, 0.3f);
            if (darkGlass != null)
            {
                darkGlass.Look = SurfaceLook.PackGlass(looks.GlassTint, 0, 0.0f);
                for (float sx = xa0 + 2.0f; sx + 8.0f <= xa1 - 2.0f; sx += 10.0f)
                {
                    float gx1 = sx + 8.0f;
                    darkGlass.AddQuad(new Vector3(sx, roofY + 0.35f, cz - half + 1.0f), new Vector3(sx, roofY + 0.35f, cz + half - 1.0f),
                        new Vector3(gx1, roofY + 0.35f, cz + half - 1.0f), new Vector3(gx1, roofY + 0.35f, cz - half + 1.0f), Vector3.up,
                        new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                    concrete.Look = looks.Trim;
                    AddWallRing(concrete, sx, gx1, cz - half + 1.0f, cz + half - 1.0f, roofY, roofY + 0.35f);
                }
            }
            concrete.Look = CityProps.DarkMetal;
            int plant = 3 + (int)(length / 40.0f) + (int)(CityProps.Hash(seed, 2, 37) % 3u);
            for (int i = 0; i < plant; i++)
            {
                float px = Mathf.Lerp(x0 + 6.0f, x1 - 6.0f, CityProps.Unit(seed, i, 139));
                float pz = CityProps.Unit(seed, i, 149) < 0.5f ? Mathf.Lerp(z0 + 4.0f, cz - half - 3.0f, CityProps.Unit(seed, i, 151)) : Mathf.Lerp(cz + half + 3.0f, z1 - 4.0f, CityProps.Unit(seed, i, 151));
                float w = 2.0f + 2.5f * CityProps.Unit(seed, i, 157);
                concrete.AddBox(new Vector3(px - w * 0.5f, roofY, pz - 1.2f), new Vector3(px + w * 0.5f, roofY + 1.6f + CityProps.Unit(seed, i, 163), pz + 1.2f));
            }

            if (far)
            {
                // Nothing to see inside from a distance: a dark backing behind each door so the far shell is not see-through.
                structures.Look = MallDarkLook;
                if (!float.IsNaN(frontDoor)) structures.AddBox(new Vector3(frontDoor - MallEntranceWidth * 0.5f - 0.5f, floorY, z1 - 2.0f), new Vector3(frontDoor + MallEntranceWidth * 0.5f + 0.5f, entranceTop + 0.2f, z1 - 1.8f));
                if (!float.IsNaN(backDoor)) structures.AddBox(new Vector3(backDoor - MallEntranceWidth * 0.5f - 0.5f, floorY, z0 + 1.8f), new Vector3(backDoor + MallEntranceWidth * 0.5f + 0.5f, entranceTop + 0.2f, z0 + 2.0f));
                if (westDoor) structures.AddBox(new Vector3(x0 + 1.8f, floorY, cz - 4.5f), new Vector3(x0 + 2.0f, entranceTop + 0.2f, cz + 4.5f));
                if (eastDoor) structures.AddBox(new Vector3(x1 - 2.0f, floorY, cz - 4.5f), new Vector3(x1 - 1.8f, entranceTop + 0.2f, cz + 4.5f));
                structures.Look = SurfaceLook.Default;
                concrete.Look = SurfaceLook.Default;
                return;
            }

            // ---- inside ----
            concrete.Look = MallFloorLook;
            concrete.AddQuad(new Vector3(x0 + t, floorY + 0.02f, z0 + t), new Vector3(x0 + t, floorY + 0.02f, z1 - t), new Vector3(x1 - t, floorY + 0.02f, z1 - t), new Vector3(x1 - t, floorY + 0.02f, z0 + t),
                Vector3.up, new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));
            concrete.Look = MallCeilingLook;
            concrete.AddQuad(new Vector3(x0 + t, roofY - MallSlabThickness - 0.01f, z0 + t), new Vector3(x1 - t, roofY - MallSlabThickness - 0.01f, z0 + t),
                new Vector3(x1 - t, roofY - MallSlabThickness - 0.01f, z1 - t), new Vector3(x0 + t, roofY - MallSlabThickness - 0.01f, z1 - t), Vector3.down,
                new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1));

            float storeDepth = Mathf.Min(MallStoreDepth, (z1 - z0 - MallConcourseWidth) * 0.5f - t - 1.0f);
            bool foodCourt = spine && xa1 - xa0 >= 90.0f;
            float fcWidth = foodCourt ? Mathf.Min(44.0f, (xa1 - xa0) * 0.3f) : 0.0f;
            float fcCentre = FreeSpot(cx, xa1 - xa0, xa0, xa1, northPassages, southPassages, fcWidth * 0.5f + 8.0f);
            if (float.IsNaN(fcCentre)) fcCentre = cx;
            float fc0 = foodCourt ? fcCentre - fcWidth * 0.5f : float.NaN, fc1 = foodCourt ? fcCentre + fcWidth * 0.5f : float.NaN;
            float run = levelH / 0.577f;   // a 30 degree climb

            // Escalators along the back gallery's edge: two on the spine either side of the food court, one in the middle of a shorter corridor,
            // none where a passage opens behind them.
            var escalatorX = new List<float>();
            var wanted = new List<float>();
            if (foodCourt)
            {
                wanted.Add(Mathf.Lerp(xa0, fc0, 0.5f));
                wanted.Add(Mathf.Lerp(fc1, xa1, 0.5f));
            }
            else if (xa1 - xa0 >= run + 24.0f)
            {
                wanted.Add((xa0 + xa1) * 0.5f);
                if (xa1 - xa0 >= 2.0f * run + 60.0f)
                {
                    wanted[0] = Mathf.Lerp(xa0, xa1, 0.3f);
                    wanted.Add(Mathf.Lerp(xa0, xa1, 0.7f));
                }
            }
            foreach (float ex in wanted)
            {
                bool clear = ex - run * 0.5f - 4.0f > xa0 && ex + run * 0.5f + 4.0f < xa1;
                foreach (float p in southPassages)
                {
                    if (Mathf.Abs(p - ex) < run * 0.5f + MallPassageWidth * 0.5f + 3.0f) clear = false;
                }
                if (clear) escalatorX.Add(ex);
            }
            float[] escalators = escalatorX.ToArray();

            for (int k = 0; k < levels; k++)
            {
                float levelY = floorY + k * levelH;
                float ceilingY = levelY + levelH - MallSlabThickness;

                // The level's slab (levels above the ground): the store rows and galleries, open over the concourse.
                if (k > 0)
                {
                    concrete.Look = MallCeilingLook;
                    AddMallSlab(concrete, x0 + t, x1 - t, z0 + t, cz - half + MallGalleryWidth, levelY - MallSlabThickness, levelY, MallFloorLook);
                    AddMallSlab(concrete, x0 + t, x1 - t, cz + half - MallGalleryWidth, z1 - t, levelY - MallSlabThickness, levelY, MallFloorLook);
                    if (westAnchor) AddMallSlab(concrete, x0 + t, xa0, cz - half + MallGalleryWidth, cz + half - MallGalleryWidth, levelY - MallSlabThickness, levelY, MallFloorLook);
                    if (eastAnchor) AddMallSlab(concrete, xa1, x1 - t, cz - half + MallGalleryWidth, cz + half - MallGalleryWidth, levelY - MallSlabThickness, levelY, MallFloorLook);
                    AddMallBalustrade(structures, xa0, xa1, cz - half + MallGalleryWidth, levelY, escalators, run, k, true);
                    AddMallBalustrade(structures, xa0, xa1, cz + half - MallGalleryWidth, levelY, escalators, run, k, false);
                }

                // The internal anchors: an opening onto the concourse at each level, shelving inside.
                for (int end = 0; end < 2; end++)
                {
                    if (end == 0 ? !westAnchor : !eastAnchor) continue;
                    float ax = end == 0 ? xa0 : xa1;
                    structures.Look = MallWallLook;
                    AddMallWallZ(structures, ax - 0.2f, ax + 0.2f, z0 + t, z1 - t, levelY, ceilingY,
                        new List<WallOpening> { new WallOpening(cz - 6.0f, cz + 6.0f, ceilingY) }, end == 1, false);
                    float in0 = end == 0 ? x0 + t + 3.0f : xa1 + 3.0f, in1 = end == 0 ? xa0 - 3.0f : x1 - t - 3.0f;
                    structures.Look = ShelfLook;
                    for (float sx = in0 + 3.0f; sx + 1.0f < in1; sx += 7.0f)
                    {
                        structures.AddBox(new Vector3(sx, levelY, z0 + t + 6.0f), new Vector3(sx + 1.0f, levelY + 2.0f, cz - half - 2.0f));
                        structures.AddBox(new Vector3(sx, levelY, cz + half + 2.0f), new Vector3(sx + 1.0f, levelY + 2.0f, z1 - t - 6.0f));
                    }
                    structures.Look = MallSignLook(seed, end + 1);
                    structures.AddBox(new Vector3(ax - 0.3f, ceilingY - 1.6f, cz - 6.0f), new Vector3(ax + 0.3f, ceilingY - 0.2f, cz + 6.0f));
                }

                // The store rows either side of the concourse, making way on the ground floor for the passages to the pieces beside this one
                // (and, on the spine, for the food court's counters in the middle stretch).
                for (int side = -1; side <= 1; side += 2)
                {
                    float zIn = cz + side * half;
                    float zBack = cz + side * (half + storeDepth);
                    List<float> passages = side > 0 ? northPassages : southPassages;
                    var spans = new List<(float, float)> { (xa0, xa1) };
                    if (k == 0)
                    {
                        foreach (float p in passages)
                        {
                            spans = Cut(spans, p - MallPassageWidth * 0.5f - 1.0f, p + MallPassageWidth * 0.5f + 1.0f);
                        }
                    }
                    int unit = 0;
                    foreach ((float s0, float s1) in spans)
                    {
                        if (s1 - s0 < 3.0f) continue;
                        structures.Look = MallWallLook;
                        // The row's back wall, and its closing walls (a passage's sides, where one cuts through).
                        float bz0 = side > 0 ? zBack - 0.3f : zBack, bz1 = side > 0 ? zBack : zBack + 0.3f;
                        structures.AddBox(new Vector3(s0, levelY, bz0), new Vector3(s1, ceilingY, bz1));
                        float pz0 = Mathf.Min(zIn, zBack), pz1 = Mathf.Max(zIn, zBack);
                        structures.AddBox(new Vector3(s1 - 0.1f, levelY, pz0), new Vector3(s1 + 0.1f, ceilingY, pz1));
                        float ux0 = s0;
                        while (ux0 < s1 - 3.0f)
                        {
                            float width = 10.0f + 4.0f * CityProps.Unit(seed, unit * 2 + side + 1, 171 + k);
                            float ux1 = Mathf.Min(s1, ux0 + width);
                            if (s1 - ux1 < 6.0f) ux1 = s1;
                            bool food = foodCourt && k == 0 && ux0 >= fc0 - 0.1f && ux1 <= fc1 + 0.1f;
                            if (foodCourt && k == 0 && ux0 < fc1 && ux1 > fc0 && !food)
                            {
                                // Straddles the food court's edge: end this unit at it.
                                ux1 = ux0 < fc0 ? fc0 : fc1;
                                food = ux0 >= fc0;
                            }
                            if (ux1 - ux0 > 0.5f)
                            {
                                AddMallUnit(structures, darkGlass, ux0, ux1, zIn, zBack, side, levelY, ceilingY, looks, seed * 7 + unit * 13 + k * 101 + side, food);
                            }
                            ux0 = ux1;
                            unit++;
                        }
                    }
                }

                // Escalators up from this level.
                if (k < levels - 1)
                {
                    foreach (float ex in escalators)
                    {
                        bool rises = k % 2 == 0;
                        float lowX = rises ? ex - run * 0.5f : ex + run * 0.5f;
                        float highX = rises ? ex + run * 0.5f : ex - run * 0.5f;
                        float ez0 = cz - half + MallGalleryWidth + 0.2f, ez1 = ez0 + 1.6f;
                        AddMallEscalator(concrete, structures, lowX, highX, ez0, ez1, levelY, levelY + levelH, cz - half + MallGalleryWidth);
                    }
                }
            }

            // ---- the concourse's furniture: the spine's dry fountain and food court, planters and benches, and what the rapture left ----
            float yc = floorY + 0.02f;
            float fountainX = float.NaN;
            if (spine)
            {
                fountainX = foodCourt ? Mathf.Lerp(xa0, fc0, 0.3f) : Mathf.Lerp(xa0, xa1, 0.3f);
                CityProps.AddFountain(structures, fountainX, cz, yc, 3.0f, true);
            }
            for (float px = xa0 + 8.0f; px < xa1 - 6.0f; px += 16.0f)
            {
                if (foodCourt && px > fc0 - 4.0f && px < fc1 + 4.0f) continue;
                if (!float.IsNaN(fountainX) && Mathf.Abs(px - fountainX) < 6.0f) continue;
                if (NearAny(px, northPassages, 9.0f) || NearAny(px, southPassages, 9.0f)) continue;
                CityProps.AddPlanter(structures, px, cz, yc, 1.3f, 0.7f, 0.7f);
                CityProps.AddBench(structures, px, cz + 1.3f, yc, 1.0f, 0.0f);
                CityProps.AddBench(structures, px, cz - 1.3f, yc, -1.0f, 0.0f);
            }
            if (foodCourt)
            {
                AddFoodCourtTables(structures, fc0, fc1, cz, yc, seed);
            }
            CityProps.AddRubble(structures, Mathf.Lerp(xa0, xa1, 0.78f), cz + 2.5f, yc, 1.8f, seed + 3);
            CityProps.AddRubble(structures, Mathf.Lerp(xa0, xa1, 0.22f), cz - 3.0f, yc, 1.2f, seed + 5);
            structures.Look = SurfaceLook.Default;
            concrete.Look = SurfaceLook.Default;
            if (darkGlass != null) darkGlass.Look = SurfaceLook.Default;
        }

        /// <summary>
        /// An anchor department store, a wing of a mall (SpecialTypes.MallAnchor): one big hall, with a passage into every piece of the mall it
        /// touches and its own door to the car park on a free wall under a canopy and its big sign; shelving in long rows (some of it over), counters
        /// by the doors, a mezzanine over the back half, rubble.
        /// </summary>
        private static void BuildMallAnchor(BuildingSite site, Vector3 origin, MeshBuffer structures, MeshBuffer concrete, MeshBuffer darkGlass, SiteLooks looks, bool far)
        {
            float x0 = (float)(site.MinX - origin.x), x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z), z1 = (float)(site.MaxZ - origin.z);
            float podiumY = site.PodiumBase - origin.y;
            float floorY = site.BaseHeight - origin.y;
            float roofY = floorY + site.Params.Height;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            float t = MallWallThickness;
            int seed = site.Seed * 31 + site.Id;
            float passageTop = floorY + MallPassageHeight;
            float entranceTop = floorY + MallPassageHeight - 0.4f;

            if (floorY - podiumY > 0.02f)
            {
                concrete.Look = MallBaseLook;
                concrete.AddBox(new Vector3(x0, podiumY, z0), new Vector3(x1, floorY, z1));
            }

            // The passages into the mall, and the door: the middle of the first wall (north, west, east, south) with no passage near it.
            var open = new List<WallOpening>[4];   // by WindowGrid.Wall
            for (int w = 0; w < 4; w++) open[w] = new List<WallOpening>();
            foreach (MallJoin j in MallJoins(site))
            {
                float c = (j.Wall == WindowGrid.Wall.North || j.Wall == WindowGrid.Wall.South ? cx : cz) + j.Centre;
                open[(int)j.Wall].Add(new WallOpening(c - MallPassageWidth * 0.5f, c + MallPassageWidth * 0.5f, passageTop));
            }
            WindowGrid.Wall doorWall = WindowGrid.Wall.North;
            float door = float.NaN;
            foreach (WindowGrid.Wall w in new[] { WindowGrid.Wall.North, WindowGrid.Wall.West, WindowGrid.Wall.East, WindowGrid.Wall.South })
            {
                bool northSouth = w == WindowGrid.Wall.North || w == WindowGrid.Wall.South;
                float mid = northSouth ? cx : cz;
                float len = northSouth ? x1 - x0 : z1 - z0;
                if (len < MallEntranceWidth + 10.0f) continue;
                bool clear = true;
                foreach (WallOpening o in open[(int)w])
                {
                    if (o.A < mid + MallEntranceWidth && o.B > mid - MallEntranceWidth) clear = false;
                }
                if (!clear) continue;
                doorWall = w;
                door = mid;
                open[(int)w].Add(new WallOpening(mid - MallEntranceWidth * 0.5f, mid + MallEntranceWidth * 0.5f, entranceTop));
                break;
            }

            AddMallWallX(structures, z1 - t, z1, x0, x1, floorY, roofY, open[(int)WindowGrid.Wall.North], true);
            AddMallWallX(structures, z0, z0 + t, x0, x1, floorY, roofY, open[(int)WindowGrid.Wall.South], false);
            AddMallWallZ(structures, x0, x0 + t, z0 + t, z1 - t, floorY, roofY, open[(int)WindowGrid.Wall.West], false);
            AddMallWallZ(structures, x1 - t, x1, z0 + t, z1 - t, floorY, roofY, open[(int)WindowGrid.Wall.East], true);

            // The door's canopy and the store's name over it.
            if (!float.IsNaN(door))
            {
                float signW = Mathf.Min(16.0f, (doorWall == WindowGrid.Wall.North || doorWall == WindowGrid.Wall.South ? x1 - x0 : z1 - z0) * 0.5f);
                float signY = Mathf.Max(entranceTop + 2.5f, roofY - 4.5f);
                concrete.Look = looks.Trim;
                AddOnWall(concrete, doorWall, x0, x1, z0, z1, door, MallEntranceWidth * 0.5f + 2.0f, entranceTop, entranceTop + 0.5f, 0.0f, 5.0f, true);
                structures.Look = MallSignLook(seed, 1);
                AddOnWall(structures, doorWall, x0, x1, z0, z1, door, signW * 0.5f, signY, signY + 3.2f, 0.0f, 0.35f, false);
                structures.Look = CityProps.White;
                AddOnWall(structures, doorWall, x0, x1, z0, z1, door - signW * 0.08f, signW * 0.32f, signY + 1.1f, signY + 2.1f, 0.35f, 0.5f, false);
            }

            // The roof.
            structures.Look = MallWallLook;
            concrete.Look = looks.Roof;
            structures.AddBox(new Vector3(x0, roofY - MallSlabThickness, z0), new Vector3(x1, roofY, z1), concrete, includeBottom: true);
            AddParapetRing(structures, x0, x1, z0, z1, roofY, roofY + 1.3f, 0.3f);
            concrete.Look = CityProps.DarkMetal;
            for (int i = 0; i < 3; i++)
            {
                float px = Mathf.Lerp(x0 + 6.0f, x1 - 6.0f, CityProps.Unit(seed, i, 211));
                float pz = Mathf.Lerp(z0 + 6.0f, z1 - 6.0f, CityProps.Unit(seed, i, 223));
                concrete.AddBox(new Vector3(px - 2.0f, roofY, pz - 1.5f), new Vector3(px + 2.0f, roofY + 2.0f, pz + 1.5f));
            }

            if (far)
            {
                if (!float.IsNaN(door))
                {
                    structures.Look = MallDarkLook;
                    AddOnWall(structures, doorWall, x0, x1, z0, z1, door, MallEntranceWidth * 0.5f + 0.5f, floorY, entranceTop + 0.2f, -2.0f, -1.8f, false);
                }
                structures.Look = SurfaceLook.Default;
                concrete.Look = SurfaceLook.Default;
                return;
            }

            // ---- inside ----
            concrete.Look = MallFloorLook;
            concrete.AddQuad(new Vector3(x0 + t, floorY + 0.02f, z0 + t), new Vector3(x0 + t, floorY + 0.02f, z1 - t), new Vector3(x1 - t, floorY + 0.02f, z1 - t), new Vector3(x1 - t, floorY + 0.02f, z0 + t),
                Vector3.up, new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));
            concrete.Look = MallCeilingLook;
            concrete.AddQuad(new Vector3(x0 + t, roofY - MallSlabThickness - 0.01f, z0 + t), new Vector3(x1 - t, roofY - MallSlabThickness - 0.01f, z0 + t),
                new Vector3(x1 - t, roofY - MallSlabThickness - 0.01f, z1 - t), new Vector3(x0 + t, roofY - MallSlabThickness - 0.01f, z1 - t), Vector3.down,
                new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1));

            // A mezzanine over the half of the hall away from the door (the door's wall decides which half), its edge railed.
            float mezzanineY = floorY + (float)CityX.World.Plan.Amenities.MallLevelHeight;
            if (roofY - mezzanineY > 4.0f)
            {
                concrete.Look = MallCeilingLook;
                switch (doorWall)
                {
                    case WindowGrid.Wall.North:
                        AddMallSlab(concrete, x0 + t, x1 - t, z0 + t, cz - 2.0f, mezzanineY - MallSlabThickness, mezzanineY, MallFloorLook);
                        structures.Look = CityProps.DarkMetal;
                        structures.AddBox(new Vector3(x0 + t, mezzanineY, cz - 2.1f), new Vector3(x1 - t, mezzanineY + 1.15f, cz - 2.0f));
                        break;
                    case WindowGrid.Wall.South:
                        AddMallSlab(concrete, x0 + t, x1 - t, cz + 2.0f, z1 - t, mezzanineY - MallSlabThickness, mezzanineY, MallFloorLook);
                        structures.Look = CityProps.DarkMetal;
                        structures.AddBox(new Vector3(x0 + t, mezzanineY, cz + 2.0f), new Vector3(x1 - t, mezzanineY + 1.15f, cz + 2.1f));
                        break;
                    case WindowGrid.Wall.West:
                        AddMallSlab(concrete, cx + 2.0f, x1 - t, z0 + t, z1 - t, mezzanineY - MallSlabThickness, mezzanineY, MallFloorLook);
                        structures.Look = CityProps.DarkMetal;
                        structures.AddBox(new Vector3(cx + 2.0f, mezzanineY, z0 + t), new Vector3(cx + 2.1f, mezzanineY + 1.15f, z1 - t));
                        break;
                    default:
                        AddMallSlab(concrete, x0 + t, cx - 2.0f, z0 + t, z1 - t, mezzanineY - MallSlabThickness, mezzanineY, MallFloorLook);
                        structures.Look = CityProps.DarkMetal;
                        structures.AddBox(new Vector3(cx - 2.1f, mezzanineY, z0 + t), new Vector3(cx - 2.0f, mezzanineY + 1.15f, z1 - t));
                        break;
                }
            }

            // Shelving in long rows across the hall, clear of the aisles that lead in from every opening, some of it pushed over.
            var lanes = new List<(WindowGrid.Wall wall, float a, float b)>();
            for (int w = 0; w < 4; w++)
            {
                foreach (WallOpening o in open[w]) lanes.Add(((WindowGrid.Wall)w, o.A - 2.0f, o.B + 2.0f));
            }
            int shelf = 0;
            for (float sz = z0 + 7.0f; sz + 1.0f < z1 - 7.0f; sz += 6.0f)
            {
                for (float sx = x0 + 6.0f; sx + 9.0f < x1 - 6.0f; sx += 13.0f)
                {
                    float sx1 = sx + 9.0f;
                    if (InLane(lanes, sx, sx1, sz, sz + 1.0f, x0, x1, z0, z1)) continue;
                    if (Mathf.Abs((sx + sx1) * 0.5f - cx) < 5.0f || Mathf.Abs(sz + 0.5f - cz) < 4.0f) continue;   // the cross aisles
                    bool over = CityProps.Unit(seed, shelf++, 227) < 0.18f;
                    structures.Look = ShelfLook;
                    if (over)
                    {
                        structures.AddBox(new Vector3(sx, floorY, sz - 0.6f), new Vector3(sx1, floorY + 0.5f, sz + 1.4f));
                    }
                    else
                    {
                        structures.AddBox(new Vector3(sx, floorY, sz), new Vector3(sx1, floorY + 2.0f, sz + 1.0f));
                    }
                }
            }
            // Counters by the door, rubble in the aisles.
            if (!float.IsNaN(door))
            {
                structures.Look = CounterLook;
                AddOnWall(structures, doorWall, x0, x1, z0, z1, door + MallEntranceWidth, 2.5f, floorY, floorY + 1.1f, -7.0f, -5.8f, false);
                AddOnWall(structures, doorWall, x0, x1, z0, z1, door - MallEntranceWidth, 2.5f, floorY, floorY + 1.1f, -7.0f, -5.8f, false);
            }
            CityProps.AddRubble(structures, Mathf.Lerp(x0, x1, 0.3f), cz, floorY + 0.02f, 1.6f, seed + 11);
            CityProps.AddRubble(structures, Mathf.Lerp(x0, x1, 0.7f), Mathf.Lerp(z0, z1, 0.65f), floorY + 0.02f, 1.2f, seed + 13);
            structures.Look = SurfaceLook.Default;
            concrete.Look = SurfaceLook.Default;
        }

        /// <summary>
        /// A box against one wall of the box x0..x1, z0..z1: centred at <paramref name="along"/> on the wall, <paramref name="halfAlong"/> either
        /// side, from y0 to y1, and from <paramref name="out0"/> to <paramref name="out1"/> metres out from the wall's outer face (negative: inside).
        /// </summary>
        private static void AddOnWall(MeshBuffer b, WindowGrid.Wall wall, float x0, float x1, float z0, float z1, float along, float halfAlong,
            float y0, float y1, float out0, float out1, bool bottom)
        {
            float o0 = Mathf.Min(out0, out1), o1 = Mathf.Max(out0, out1);
            switch (wall)
            {
                case WindowGrid.Wall.North: b.AddBox(new Vector3(along - halfAlong, y0, z1 + o0), new Vector3(along + halfAlong, y1, z1 + o1), bottom); break;
                case WindowGrid.Wall.South: b.AddBox(new Vector3(along - halfAlong, y0, z0 - o1), new Vector3(along + halfAlong, y1, z0 - o0), bottom); break;
                case WindowGrid.Wall.East: b.AddBox(new Vector3(x1 + o0, y0, along - halfAlong), new Vector3(x1 + o1, y1, along + halfAlong), bottom); break;
                default: b.AddBox(new Vector3(x0 - o1, y0, along - halfAlong), new Vector3(x0 - o0, y1, along + halfAlong), bottom); break;
            }
        }

        /// <summary>Whether a shelf's box lies in an aisle leading 18 m in from one of the hall's openings.</summary>
        private static bool InLane(List<(WindowGrid.Wall wall, float a, float b)> lanes, float sx0, float sx1, float sz0, float sz1, float x0, float x1, float z0, float z1)
        {
            foreach ((WindowGrid.Wall wall, float a, float b) in lanes)
            {
                switch (wall)
                {
                    case WindowGrid.Wall.North: if (sx1 > a && sx0 < b && sz1 > z1 - 18.0f) return true; break;
                    case WindowGrid.Wall.South: if (sx1 > a && sx0 < b && sz0 < z0 + 18.0f) return true; break;
                    case WindowGrid.Wall.East: if (sz1 > a && sz0 < b && sx1 > x1 - 18.0f) return true; break;
                    default: if (sz1 > a && sz0 < b && sx0 < x0 + 18.0f) return true; break;
                }
            }
            return false;
        }

        /// <summary>The first of the middle, a third of the way either side, ... of a stretch that is clear of every listed point by <paramref name="clearance"/>; NaN if none.</summary>
        private static float FreeSpot(float centre, float length, float from, float to, List<float> points, List<float> more = null, float clearance = 16.0f)
        {
            float[] offsets = { 0.0f, -0.3f, 0.3f, -0.15f, 0.15f, -0.4f, 0.4f };
            foreach (float f in offsets)
            {
                float p = centre + f * length;
                if (p - clearance * 0.6f < from + 6.0f || p + clearance * 0.6f > to - 6.0f) continue;
                if (NearAny(p, points, clearance) || (more != null && NearAny(p, more, clearance))) continue;
                return p;
            }
            return float.NaN;
        }

        private static bool NearAny(float x, List<float> points, float distance)
        {
            foreach (float p in points)
            {
                if (Mathf.Abs(p - x) < distance) return true;
            }
            return false;
        }

        /// <summary>Spans with the stretch a..b taken out of them.</summary>
        private static List<(float, float)> Cut(List<(float, float)> spans, float a, float b)
        {
            var result = new List<(float, float)>(spans.Count + 1);
            foreach ((float s0, float s1) in spans)
            {
                if (b <= s0 || a >= s1)
                {
                    result.Add((s0, s1));
                    continue;
                }
                if (a > s0) result.Add((s0, a));
                if (b < s1) result.Add((b, s1));
            }
            return result;
        }

        /// <summary>
        /// A wall along x between zNear and zFar from xFrom to xTo, with its openings (each up to its own top, a lintel above it), and the base
        /// band in its own material standing a hair proud of the outer face (+z when <paramref name="outerFar"/>) on the solid stretches only.
        /// </summary>
        private static void AddMallWallX(MeshBuffer buffer, float zNear, float zFar, float xFrom, float xTo, float y0, float y1, List<WallOpening> openings, bool outerFar, bool band = true)
        {
            openings.Sort((p, q) => p.A.CompareTo(q.A));
            float x = xFrom;
            foreach (WallOpening o in openings)
            {
                float a = Mathf.Max(o.A, xFrom), b = Mathf.Min(o.B, xTo);
                if (b <= a + 0.05f) continue;
                if (a > x) SolidX(buffer, x, a, zNear, zFar, y0, y1, outerFar, band);
                buffer.Look = MallWallLook;
                if (o.Top < y1 - 0.05f)
                {
                    buffer.AddBox(new Vector3(a, o.Top, zNear), new Vector3(b, y1, zFar), includeBottom: true);
                }
                x = Mathf.Max(x, b);
            }
            if (x < xTo) SolidX(buffer, x, xTo, zNear, zFar, y0, y1, outerFar, band);
        }

        private static void SolidX(MeshBuffer buffer, float s0, float s1, float zNear, float zFar, float y0, float y1, bool outerFar, bool band)
        {
            buffer.Look = MallWallLook;
            buffer.AddBox(new Vector3(s0, y0, zNear), new Vector3(s1, y1, zFar));
            if (!band) return;
            buffer.Look = MallBaseLook;
            float bz0 = outerFar ? zFar : zNear - 0.05f, bz1 = outerFar ? zFar + 0.05f : zNear;
            buffer.AddBox(new Vector3(s0, y0, bz0), new Vector3(s1, y0 + 1.6f, bz1));
            buffer.Look = MallWallLook;
        }

        /// <summary>The same for a wall along z between xNear and xFar (outer face at +x when <paramref name="outerFar"/>).</summary>
        private static void AddMallWallZ(MeshBuffer buffer, float xNear, float xFar, float zFrom, float zTo, float y0, float y1, List<WallOpening> openings, bool outerFar, bool band = true)
        {
            openings.Sort((p, q) => p.A.CompareTo(q.A));
            float z = zFrom;
            foreach (WallOpening o in openings)
            {
                float a = Mathf.Max(o.A, zFrom), b = Mathf.Min(o.B, zTo);
                if (b <= a + 0.05f) continue;
                if (a > z) SolidZ(buffer, z, a, xNear, xFar, y0, y1, outerFar, band);
                buffer.Look = MallWallLook;
                if (o.Top < y1 - 0.05f)
                {
                    buffer.AddBox(new Vector3(xNear, o.Top, a), new Vector3(xFar, y1, b), includeBottom: true);
                }
                z = Mathf.Max(z, b);
            }
            if (z < zTo) SolidZ(buffer, z, zTo, xNear, xFar, y0, y1, outerFar, band);
        }

        private static void SolidZ(MeshBuffer buffer, float s0, float s1, float xNear, float xFar, float y0, float y1, bool outerFar, bool band)
        {
            buffer.Look = MallWallLook;
            buffer.AddBox(new Vector3(xNear, y0, s0), new Vector3(xFar, y1, s1));
            if (!band) return;
            buffer.Look = MallBaseLook;
            float bx0 = outerFar ? xFar : xNear - 0.05f, bx1 = outerFar ? xFar + 0.05f : xNear;
            buffer.AddBox(new Vector3(bx0, y0, s0), new Vector3(bx1, y0 + 1.6f, s1));
            buffer.Look = MallWallLook;
        }

        /// <summary>The food court's tables and stools in the open between its counters, some over-turned, some dragged away.</summary>
        private static void AddFoodCourtTables(MeshBuffer structures, float fc0, float fc1, float cz, float yc, int seed)
        {
            int tableIndex = 0;
            for (float tx = fc0 + 3.0f; tx + 2.0f < fc1; tx += 4.2f)
            {
                for (float tz = cz - 4.5f; tz <= cz + 4.5f; tz += 4.5f)
                {
                    float u = CityProps.Unit(seed, tableIndex++, 181);
                    if (u < 0.12f)
                    {
                        continue;   // dragged away
                    }
                    bool over = u < 0.24f;
                    structures.Look = CounterLook;
                    if (over)
                    {
                        CityProps.AddTurnedBox(structures, tx + 0.6f, tz, 0.75f, 0.55f, 0.94f, 0.34f, yc, yc + 0.4f);
                    }
                    else
                    {
                        CityProps.AddTurnedBox(structures, tx, tz, 0.55f, 0.55f, 1.0f, 0.0f, yc + 0.72f, yc + 0.78f);
                        CityProps.AddTurnedBox(structures, tx, tz, 0.08f, 0.08f, 1.0f, 0.0f, yc, yc + 0.72f);
                    }
                    structures.Look = CityProps.DarkMetal;
                    for (int s = 0; s < 4; s++)
                    {
                        if (over && s > 1) break;
                        float ox = (s & 1) == 0 ? -0.95f : 0.95f, oz = (s & 2) == 0 ? -0.95f : 0.95f;
                        CityProps.AddTurnedBox(structures, tx + ox, tz + oz, 0.2f, 0.2f, 1.0f, 0.0f, yc, yc + 0.5f);
                    }
                }
            }
        }

        /// <summary>A floor slab: its top in the floor look, its underside (a ceiling) and edges in the buffer's current look.</summary>
        private static void AddMallSlab(MeshBuffer buffer, float x0, float x1, float z0, float z1, float bottom, float top, Color32 floorLook)
        {
            if (x1 - x0 < 0.1f || z1 - z0 < 0.1f) return;
            Color32 side = buffer.Look;
            buffer.AddQuad(new Vector3(x0, bottom, z0), new Vector3(x1, bottom, z0), new Vector3(x1, bottom, z1), new Vector3(x0, bottom, z1), Vector3.down,
                new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1));
            AddWallRing(buffer, x0, x1, z0, z1, bottom, top);
            buffer.Look = floorLook;
            buffer.AddQuad(new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0), Vector3.up,
                new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));
            buffer.Look = side;
        }

        /// <summary>A gallery's balustrade along the void edge, 1.15 m high, left open where an escalator arrives.</summary>
        private static void AddMallBalustrade(MeshBuffer buffer, float xFrom, float xTo, float z, float y, float[] escalatorX, float run, int level, bool backSide)
        {
            buffer.Look = CityProps.DarkMetal;
            var gaps = new List<(float, float)>();
            if (backSide)
            {
                foreach (float ex in escalatorX)
                {
                    // The escalator from the level below lands here; the one up to the next leaves here.
                    bool fromBelowRose = (level - 1) % 2 == 0;
                    float landing = fromBelowRose ? ex + run * 0.5f : ex - run * 0.5f;
                    gaps.Add((landing - 0.3f, landing + 2.8f));
                    bool thisRises = level % 2 == 0;
                    float start = thisRises ? ex - run * 0.5f : ex + run * 0.5f;
                    gaps.Add((start - 2.8f, start + 0.3f));
                }
            }
            float x = xFrom;
            gaps.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            foreach ((float g0, float g1) in gaps)
            {
                if (g0 > x)
                {
                    buffer.AddBox(new Vector3(x, y, z - 0.05f), new Vector3(Mathf.Min(g0, xTo), y + 1.15f, z + 0.05f));
                }
                x = Mathf.Max(x, g1);
            }
            if (x < xTo)
            {
                buffer.AddBox(new Vector3(x, y, z - 0.05f), new Vector3(xTo, y + 1.15f, z + 0.05f));
            }
        }

        /// <summary>
        /// One store unit between two party walls: a shopfront on the concourse with its sign band, and either its glass with a doorway and a
        /// counter and shelving inside, or its shutter down. A food court unit is a counter with its hood instead.
        /// </summary>
        private static void AddMallUnit(MeshBuffer structures, MeshBuffer darkGlass, float ux0, float ux1, float zIn, float zBack, int side,
            float levelY, float ceilingY, SiteLooks looks, int seed, bool food)
        {
            float zFront = zIn;   // the concourse edge
            float dir = side;     // +1: the unit lies at greater z than the concourse
            // Party walls.
            structures.Look = MallWallLook;
            float pz0 = Mathf.Min(zIn, zBack), pz1 = Mathf.Max(zIn, zBack);
            structures.AddBox(new Vector3(ux0 - 0.1f, levelY, pz0), new Vector3(ux0 + 0.1f, ceilingY, pz1));
            float signBottom = ceilingY - 1.3f;
            if (food)
            {
                structures.Look = CounterLook;
                float cz0 = zFront + dir * 0.4f, cz1 = zFront + dir * 1.6f;
                structures.AddBox(new Vector3(ux0 + 0.8f, levelY, Mathf.Min(cz0, cz1)), new Vector3(ux1 - 0.8f, levelY + 1.1f, Mathf.Max(cz0, cz1)));
                structures.Look = CityProps.DarkMetal;
                structures.AddBox(new Vector3(ux0 + 0.6f, signBottom - 0.6f, Mathf.Min(zFront, zFront + dir * 2.2f)), new Vector3(ux1 - 0.6f, signBottom, Mathf.Max(zFront, zFront + dir * 2.2f)), includeBottom: true);
                // A wall behind the counter (the kitchen), with its menu board.
                structures.Look = MallWallLook;
                float kz0 = zFront + dir * 4.5f, kz1 = zFront + dir * 4.8f;
                structures.AddBox(new Vector3(ux0, levelY, Mathf.Min(kz0, kz1)), new Vector3(ux1, ceilingY, Mathf.Max(kz0, kz1)));
            }
            else
            {
                bool shut = CityProps.Unit(seed, 1, 191) < MallShutteredShare;
                float doorX = (ux0 + ux1) * 0.5f;
                if (shut)
                {
                    structures.Look = ShutterLook;
                    structures.AddBox(new Vector3(ux0 + 0.1f, levelY, zFront - 0.05f), new Vector3(ux1 - 0.1f, signBottom, zFront + 0.05f));
                }
                else
                {
                    // Stall riser, glass either side of the doorway, and what the shop still holds.
                    structures.Look = MallBaseLook;
                    structures.AddBox(new Vector3(ux0 + 0.1f, levelY, zFront - 0.08f), new Vector3(doorX - 1.4f, levelY + 0.9f, zFront + 0.08f));
                    structures.AddBox(new Vector3(doorX + 1.4f, levelY, zFront - 0.08f), new Vector3(ux1 - 0.1f, levelY + 0.9f, zFront + 0.08f));
                    if (darkGlass != null)
                    {
                        darkGlass.Look = SurfaceLook.PackGlass(looks.GlassTint, 0, 0.0f, clear: true);
                        AddDoubleGlass(darkGlass, ux0 + 0.1f, doorX - 1.4f, zFront, levelY + 0.9f, signBottom);
                        AddDoubleGlass(darkGlass, doorX + 1.4f, ux1 - 0.1f, zFront, levelY + 0.9f, signBottom);
                    }
                    structures.Look = CounterLook;
                    float c0 = zFront + dir * 3.0f, c1 = zFront + dir * 3.9f;
                    structures.AddBox(new Vector3(ux1 - 4.0f, levelY, Mathf.Min(c0, c1)), new Vector3(ux1 - 1.0f, levelY + 1.1f, Mathf.Max(c0, c1)));
                    structures.Look = ShelfLook;
                    float s0 = zFront + dir * 5.0f, s1 = zFront + dir * (Mathf.Abs(zBack - zFront) - 2.5f);
                    if (CityProps.Unit(seed, 2, 193) < 0.3f)
                    {
                        structures.AddBox(new Vector3(ux0 + 1.5f, levelY, Mathf.Min(s0, s1)), new Vector3(ux0 + 3.4f, levelY + 0.6f, Mathf.Max(s0, s1)));   // knocked over
                    }
                    else
                    {
                        structures.AddBox(new Vector3(ux0 + 1.5f, levelY, Mathf.Min(s0, s1)), new Vector3(ux0 + 2.1f, levelY + 1.9f, Mathf.Max(s0, s1)));
                    }
                }
            }
            // The sign band over the front, lettered.
            structures.Look = MallSignLook(seed, 4);
            structures.AddBox(new Vector3(ux0 + 0.1f, signBottom, zFront - 0.15f), new Vector3(ux1 - 0.1f, ceilingY - 0.35f, zFront + 0.15f), includeBottom: true);
            structures.Look = CityProps.White;
            float textLength = Mathf.Min((ux1 - ux0) * 0.5f, 2.0f + 2.5f * CityProps.Unit(seed, 3, 197));
            float textX = (ux0 + ux1) * 0.5f;
            structures.AddBox(new Vector3(textX - textLength * 0.5f, signBottom + 0.35f, zFront - 0.22f), new Vector3(textX + textLength * 0.5f, signBottom + 0.7f, zFront + 0.22f), includeBottom: true);
        }

        /// <summary>A pane in the x-y plane at z, drawn on both faces (both sides of a shopfront are walked past).</summary>
        private static void AddDoubleGlass(MeshBuffer glass, float gx0, float gx1, float z, float y0, float y1)
        {
            if (gx1 - gx0 < 0.1f || y1 - y0 < 0.1f) return;
            float w = gx1 - gx0, h = y1 - y0;
            glass.AddQuad(new Vector3(gx0, y0, z), new Vector3(gx1, y0, z), new Vector3(gx1, y1, z), new Vector3(gx0, y1, z), Vector3.back,
                new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
            glass.AddQuad(new Vector3(gx1, y0, z), new Vector3(gx0, y0, z), new Vector3(gx0, y1, z), new Vector3(gx1, y1, z), Vector3.forward,
                new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
        }

        /// <summary>
        /// A dead escalator: a sloped slab 1.6 m wide from (lowX, lowY) up to (highX, highY) between two z lines, balustrades along it, a landing at
        /// each end joining the gallery at <paramref name="galleryZ"/>.
        /// </summary>
        private static void AddMallEscalator(MeshBuffer concrete, MeshBuffer structures, float lowX, float highX, float ez0, float ez1, float lowY, float highY, float galleryZ)
        {
            AddGarageRamp(concrete, lowX, highX, ez0, ez1, lowY, highY, CityProps.DarkMetal, ShelfLook);
            float dir = highX > lowX ? 1.0f : -1.0f;
            // Landings: a short slab at the top and the bottom, reaching back to the gallery's edge.
            concrete.Look = CityProps.DarkMetal;
            AddMallSlab(concrete, Mathf.Min(highX, highX + dir * 2.5f), Mathf.Max(highX, highX + dir * 2.5f), galleryZ - 0.6f, ez1, highY - MallSlabThickness, highY, MallFloorLook);
            if (lowY > 0.5f)
            {
                AddMallSlab(concrete, Mathf.Min(lowX, lowX - dir * 2.5f), Mathf.Max(lowX, lowX - dir * 2.5f), galleryZ - 0.6f, ez1, lowY - MallSlabThickness, lowY, MallFloorLook);
            }
            // Balustrades along both sides of the flight.
            structures.Look = CityProps.DarkMetal;
            Vector3 a0 = new Vector3(lowX, lowY, ez0), b0 = new Vector3(highX, highY, ez0);
            Vector3 a1 = new Vector3(lowX, lowY, ez1), b1 = new Vector3(highX, highY, ez1);
            float len = Vector3.Distance(a0, b0);
            structures.AddQuad(a0, b0, b0 + Vector3.up * 1.1f, a0 + Vector3.up * 1.1f, Vector3.back, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1.1f), new Vector2(0, 1.1f));
            structures.AddQuad(b0, a0, a0 + Vector3.up * 1.1f, b0 + Vector3.up * 1.1f, Vector3.forward, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1.1f), new Vector2(0, 1.1f));
            structures.AddQuad(b1, a1, a1 + Vector3.up * 1.1f, b1 + Vector3.up * 1.1f, Vector3.forward, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1.1f), new Vector2(0, 1.1f));
            structures.AddQuad(a1, b1, b1 + Vector3.up * 1.1f, a1 + Vector3.up * 1.1f, Vector3.back, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1.1f), new Vector2(0, 1.1f));
        }
    }
}

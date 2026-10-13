using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A shopping mall (SpecialTypes.Mall), built in the builder's frame (x along the frontage, +z the front, y absolute).
    ///
    /// The shape every regional mall shares (research, 2026-10-12): a CONCOURSE down the middle with the stores either side of it, on two or
    /// three levels, open through the middle so the galleries above look down onto the ground floor; an ANCHOR store at each end (a department
    /// store, 20,000-200,000 sq ft, taking the ends of the plan); a FOOD COURT where people naturally gather, with its counters along the walls and
    /// its seating in the open; escalators between the levels; skylights over the concourse. Inline stores are 20-40 ft across and 40-60 ft deep.
    ///
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

        /// <summary>What share of the stores have their shutter down.</summary>
        public const float MallShutteredShare = 0.55f;

        /// <summary>A mall is clad light (render and concrete panel) over a darker base band: a street shop's dark cladding on a 170 m box read as a black slab.</summary>
        private static readonly Color32 MallWallLook = SurfaceLook.Pack(new Color(0.80f, 0.78f, 0.74f), (int)WallMaterial.Plaster, 0.45f);
        private static readonly Color32 MallBaseLook = SurfaceLook.Pack(new Color(0.46f, 0.46f, 0.47f), (int)WallMaterial.Concrete, 0.6f);

        private static readonly Color32 MallFloorLook = SurfaceLook.Pack(new Color(0.86f, 0.85f, 0.82f), SurfaceLook.Paving, 0.3f);
        private static readonly Color32 MallCeilingLook = SurfaceLook.Pack(new Color(0.80f, 0.80f, 0.78f), (int)WallMaterial.Plaster, 0.4f);
        private static readonly Color32 ShutterLook = SurfaceLook.Pack(new Color(0.42f, 0.43f, 0.45f), (int)WallMaterial.Metal, 0.7f);
        private static readonly Color32 CounterLook = SurfaceLook.Pack(new Color(0.55f, 0.50f, 0.42f), (int)WallMaterial.Metal, 0.5f);
        private static readonly Color32 ShelfLook = SurfaceLook.Pack(new Color(0.75f, 0.74f, 0.70f), (int)WallMaterial.Metal, 0.55f);

        /// <summary>Brand colours: a mall's signs shout where a street shop's (SignColours) murmur.</summary>
        private static readonly Color[] MallSignColours =
        {
            new Color(0.80f, 0.12f, 0.10f), new Color(0.10f, 0.25f, 0.70f), new Color(0.90f, 0.45f, 0.08f), new Color(0.08f, 0.45f, 0.42f),
            new Color(0.55f, 0.10f, 0.45f), new Color(0.20f, 0.20f, 0.22f), new Color(0.85f, 0.70f, 0.15f),
        };

        private static Color32 MallSignLook(int seed, int salt) =>
            SurfaceLook.Pack(MallSignColours[(int)(CityProps.Hash(seed, salt, 131) % (uint)MallSignColours.Length)], (int)WallMaterial.Metal, 0.45f);

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
            float t = MallWallThickness;
            int seed = site.Seed;

            if (floorY - podiumY > 0.02f)
            {
                concrete.Look = MallBaseLook;
                concrete.AddBox(new Vector3(x0, podiumY, z0), new Vector3(x1, floorY, z1));
            }

            // ---- the outside: blank walls with a glazed entrance in the middle of the front and of each side, under a canopy ----
            float entranceTop = floorY + levelH - 1.2f;
            structures.Look = MallWallLook;
            AddWallAlongX(structures, z1 - t, z1, x0, x1, floorY, roofY, cx - MallEntranceWidth * 0.5f, cx + MallEntranceWidth * 0.5f, entranceTop);
            AddWallAlongX(structures, z0, z0 + t, x0, x1, floorY, roofY, float.NaN, float.NaN, 0.0f);
            AddWallAlongZ(structures, x0, x0 + t, z0, z1, floorY, roofY, cz - 4.0f, cz + 4.0f, entranceTop);
            AddWallAlongZ(structures, x1 - t, x1, z0, z1, floorY, roofY, cz - 4.0f, cz + 4.0f, entranceTop);
            // The base band in its own material round the bottom.
            structures.Look = MallBaseLook;
            AddWallRing(structures, x0 - 0.05f, x1 + 0.05f, z0 - 0.05f, z1 + 0.05f, floorY, floorY + 1.6f);
            // Canopies.
            concrete.Look = looks.Trim;
            concrete.AddBox(new Vector3(cx - MallEntranceWidth * 0.5f - 1.5f, entranceTop, z1), new Vector3(cx + MallEntranceWidth * 0.5f + 1.5f, entranceTop + 0.5f, z1 + 4.5f), includeBottom: true);
            concrete.AddBox(new Vector3(x0 - 4.0f, entranceTop, cz - 5.5f), new Vector3(x0, entranceTop + 0.5f, cz + 5.5f), includeBottom: true);
            concrete.AddBox(new Vector3(x1, entranceTop, cz - 5.5f), new Vector3(x1 + 4.0f, entranceTop + 0.5f, cz + 5.5f), includeBottom: true);
            // Anchor signs near each end of the front, and the mall's own sign over the entrance.
            float signY = roofY - 3.6f;
            for (int end = 0; end < 2; end++)
            {
                float sx = end == 0 ? x0 + (x1 - x0) * 0.16f : x1 - (x1 - x0) * 0.16f;
                structures.Look = MallSignLook(seed, end + 1);
                structures.AddBox(new Vector3(sx - 6.0f, signY, z1), new Vector3(sx + 6.0f, signY + 2.6f, z1 + 0.3f));
                structures.Look = CityProps.White;
                structures.AddBox(new Vector3(sx - 4.5f, signY + 0.9f, z1 + 0.3f), new Vector3(sx + 3.2f, signY + 1.7f, z1 + 0.45f));
            }
            structures.Look = MallSignLook(seed, 3);
            structures.AddBox(new Vector3(cx - 8.0f, roofY - 0.2f, z1 - 0.6f), new Vector3(cx + 8.0f, roofY + 2.4f, z1 + 0.1f));
            structures.Look = CityProps.White;
            structures.AddBox(new Vector3(cx - 6.4f, roofY + 0.6f, z1 + 0.1f), new Vector3(cx + 5.0f, roofY + 1.5f, z1 + 0.25f));

            // ---- the roof: slab, parapet, skylights over the concourse, plant ----
            structures.Look = MallWallLook;
            concrete.Look = looks.Roof;
            structures.AddBox(new Vector3(x0, roofY - MallSlabThickness, z0), new Vector3(x1, roofY, z1), concrete, includeBottom: true);
            AddParapetRing(structures, x0, x1, z0, z1, roofY, roofY + 1.3f, 0.3f);
            float anchorDepth = Mathf.Clamp((x1 - x0) * 0.2f, 24.0f, 45.0f);
            float xa0 = x0 + anchorDepth, xa1 = x1 - anchorDepth;
            float half = MallConcourseWidth * 0.5f;
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
            int plant = 5 + (int)(CityProps.Hash(seed, 2, 37) % 5u);
            for (int i = 0; i < plant; i++)
            {
                float px = Mathf.Lerp(x0 + 6.0f, x1 - 6.0f, CityProps.Unit(seed, i, 139));
                float pz = CityProps.Unit(seed, i, 149) < 0.5f ? Mathf.Lerp(z0 + 4.0f, cz - half - 3.0f, CityProps.Unit(seed, i, 151)) : Mathf.Lerp(cz + half + 3.0f, z1 - 4.0f, CityProps.Unit(seed, i, 151));
                float w = 2.0f + 2.5f * CityProps.Unit(seed, i, 157);
                concrete.AddBox(new Vector3(px - w * 0.5f, roofY, pz - 1.2f), new Vector3(px + w * 0.5f, roofY + 1.6f + CityProps.Unit(seed, i, 163), pz + 1.2f));
            }

            if (far)
            {
                // Nothing to see inside from a distance: a dark backing behind each entrance so the far shell is not see-through.
                structures.Look = SurfaceLook.Pack(new Color(0.12f, 0.12f, 0.13f), (int)WallMaterial.Concrete, 0.8f);
                structures.AddBox(new Vector3(cx - MallEntranceWidth * 0.5f - 0.5f, floorY, z1 - 2.0f), new Vector3(cx + MallEntranceWidth * 0.5f + 0.5f, entranceTop + 0.2f, z1 - 1.8f));
                structures.AddBox(new Vector3(x0 + 1.8f, floorY, cz - 4.5f), new Vector3(x0 + 2.0f, entranceTop + 0.2f, cz + 4.5f));
                structures.AddBox(new Vector3(x1 - 2.0f, floorY, cz - 4.5f), new Vector3(x1 - 1.8f, entranceTop + 0.2f, cz + 4.5f));
                structures.Look = SurfaceLook.Default;
                concrete.Look = SurfaceLook.Default;
                return;
            }

            // ---- inside ----
            // The ground floor.
            concrete.Look = MallFloorLook;
            concrete.AddQuad(new Vector3(x0 + t, floorY + 0.02f, z0 + t), new Vector3(x0 + t, floorY + 0.02f, z1 - t), new Vector3(x1 - t, floorY + 0.02f, z1 - t), new Vector3(x1 - t, floorY + 0.02f, z0 + t),
                Vector3.up, new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));
            // The underside of the roof over everything (the ceiling of the top level).
            concrete.Look = MallCeilingLook;
            concrete.AddQuad(new Vector3(x0 + t, roofY - MallSlabThickness - 0.01f, z0 + t), new Vector3(x1 - t, roofY - MallSlabThickness - 0.01f, z0 + t),
                new Vector3(x1 - t, roofY - MallSlabThickness - 0.01f, z1 - t), new Vector3(x0 + t, roofY - MallSlabThickness - 0.01f, z1 - t), Vector3.down,
                new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1));

            float storeDepth = Mathf.Min(MallStoreDepth, (z1 - z0 - MallConcourseWidth) * 0.5f - t - 1.0f);
            float foodCourt = Mathf.Min(44.0f, (xa1 - xa0) * 0.3f);
            float fc0 = cx - foodCourt * 0.5f, fc1 = cx + foodCourt * 0.5f;
            // Escalators: two positions along the concourse, clear of the food court.
            float[] escalatorX = { Mathf.Lerp(xa0, fc0, 0.5f), Mathf.Lerp(fc1, xa1, 0.5f) };
            float run = levelH / 0.577f;   // a 30 degree climb

            for (int k = 0; k < levels; k++)
            {
                float levelY = floorY + k * levelH;
                float ceilingY = levelY + levelH - MallSlabThickness;

                // The level's slab (levels above the ground): the store rows and galleries, open over the concourse, with the escalator landings.
                if (k > 0)
                {
                    concrete.Look = MallCeilingLook;
                    AddMallSlab(concrete, x0 + t, x1 - t, z0 + t, cz - half + MallGalleryWidth, levelY - MallSlabThickness, levelY, MallFloorLook);
                    AddMallSlab(concrete, x0 + t, x1 - t, cz + half - MallGalleryWidth, z1 - t, levelY - MallSlabThickness, levelY, MallFloorLook);
                    // The anchors' floors span the whole depth at both ends.
                    AddMallSlab(concrete, x0 + t, xa0, cz - half + MallGalleryWidth, cz + half - MallGalleryWidth, levelY - MallSlabThickness, levelY, MallFloorLook);
                    AddMallSlab(concrete, xa1, x1 - t, cz - half + MallGalleryWidth, cz + half - MallGalleryWidth, levelY - MallSlabThickness, levelY, MallFloorLook);
                    // Balustrades along the void, with gaps where the escalators land.
                    AddMallBalustrade(structures, xa0, xa1, cz - half + MallGalleryWidth, levelY, escalatorX, run, k, true);
                    AddMallBalustrade(structures, xa0, xa1, cz + half - MallGalleryWidth, levelY, escalatorX, run, k, false);
                }

                // The anchors: an opening onto the concourse at each level, shelving inside.
                for (int end = 0; end < 2; end++)
                {
                    float ax = end == 0 ? xa0 : xa1;
                    structures.Look = MallWallLook;
                    AddWallAlongZ(structures, ax - 0.2f, ax + 0.2f, z0 + t, z1 - t, levelY, ceilingY, cz - 6.0f, cz + 6.0f, ceilingY);
                    float in0 = end == 0 ? x0 + t + 3.0f : xa1 + 3.0f, in1 = end == 0 ? xa0 - 3.0f : x1 - t - 3.0f;
                    structures.Look = ShelfLook;
                    for (float sx = in0 + 3.0f; sx + 1.0f < in1; sx += 7.0f)
                    {
                        structures.AddBox(new Vector3(sx, levelY, z0 + t + 6.0f), new Vector3(sx + 1.0f, levelY + 2.0f, cz - half - 2.0f));
                        structures.AddBox(new Vector3(sx, levelY, cz + half + 2.0f), new Vector3(sx + 1.0f, levelY + 2.0f, z1 - t - 6.0f));
                    }
                    // The anchor's own sign over its opening.
                    structures.Look = MallSignLook(seed, end + 1);
                    structures.AddBox(new Vector3(ax - 0.3f, ceilingY - 1.6f, cz - 6.0f), new Vector3(ax + 0.3f, ceilingY - 0.2f, cz + 6.0f));
                }

                // The store rows either side of the concourse (the food court's counters in the middle stretch of the ground floor).
                for (int side = -1; side <= 1; side += 2)
                {
                    float zIn = cz + side * half;
                    float zBack = cz + side * (half + storeDepth);
                    structures.Look = MallWallLook;
                    structures.AddBox(new Vector3(xa0, levelY, Mathf.Min(zIn, zBack) + (side > 0 ? storeDepth - 0.3f : 0.0f)),
                        new Vector3(xa1, ceilingY, Mathf.Max(zIn, zBack) - (side > 0 ? 0.0f : storeDepth - 0.3f)));   // the back wall
                    float ux0 = xa0;
                    int unit = 0;
                    while (ux0 < xa1 - 6.0f)
                    {
                        float width = 10.0f + 4.0f * CityProps.Unit(seed, unit * 2 + side + 1, 171 + k);
                        float ux1 = Mathf.Min(xa1, ux0 + width);
                        if (xa1 - ux1 < 6.0f) ux1 = xa1;
                        bool food = k == 0 && ux0 >= fc0 - 0.1f && ux1 <= fc1 + 0.1f;
                        if (k == 0 && ux0 < fc1 && ux1 > fc0 && !food)
                        {
                            // Straddles the food court's edge: end this unit at it.
                            ux1 = ux0 < fc0 ? fc0 : fc1;
                            food = ux0 >= fc0;
                        }
                        AddMallUnit(structures, darkGlass, ux0, ux1, zIn, zBack, side, levelY, ceilingY, looks, seed * 7 + unit * 13 + k * 101 + side, food);
                        ux0 = ux1;
                        unit++;
                    }
                }

                // Escalators up from this level.
                if (k < levels - 1)
                {
                    foreach (float ex in escalatorX)
                    {
                        bool rises = k % 2 == 0;
                        float lowX = rises ? ex - run * 0.5f : ex + run * 0.5f;
                        float highX = rises ? ex + run * 0.5f : ex - run * 0.5f;
                        float ez0 = cz - half + MallGalleryWidth + 0.2f, ez1 = ez0 + 1.6f;
                        AddMallEscalator(concrete, structures, lowX, highX, ez0, ez1, levelY, levelY + levelH, cz - half + MallGalleryWidth);
                    }
                }
            }

            // ---- the concourse's furniture: the dry fountain, planters and benches, the food court's tables, and what the rapture left ----
            float yc = floorY + 0.02f;
            float fountainX = Mathf.Lerp(xa0, fc0, 0.3f);
            CityProps.AddFountain(structures, fountainX, cz, yc, 3.0f, true);
            for (float px = xa0 + 8.0f; px < xa1 - 6.0f; px += 16.0f)
            {
                if (px > fc0 - 4.0f && px < fc1 + 4.0f) continue;
                if (Mathf.Abs(px - fountainX) < 6.0f) continue;
                CityProps.AddPlanter(structures, px, cz, yc, 1.3f, 0.7f, 0.7f);
                CityProps.AddBench(structures, px, cz + 1.3f, yc, 1.0f, 0.0f);
                CityProps.AddBench(structures, px, cz - 1.3f, yc, -1.0f, 0.0f);
            }
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
            CityProps.AddRubble(structures, Mathf.Lerp(fc1, xa1, 0.6f), cz + 2.5f, yc, 1.8f, seed + 3);
            CityProps.AddRubble(structures, Mathf.Lerp(xa0, fc0, 0.7f), cz - 3.0f, yc, 1.2f, seed + 5);
            structures.Look = SurfaceLook.Default;
            concrete.Look = SurfaceLook.Default;
            if (darkGlass != null) darkGlass.Look = SurfaceLook.Default;
        }

        /// <summary>A wall along x between two z lines, from <paramref name="xFrom"/> to <paramref name="xTo"/>, with an opening (NaN for none) up to <paramref name="openTop"/>.</summary>
        private static void AddWallAlongX(MeshBuffer buffer, float zNear, float zFar, float xFrom, float xTo, float y0, float y1, float open0, float open1, float openTop)
        {
            if (float.IsNaN(open0))
            {
                buffer.AddBox(new Vector3(xFrom, y0, zNear), new Vector3(xTo, y1, zFar));
                return;
            }
            buffer.AddBox(new Vector3(xFrom, y0, zNear), new Vector3(open0, y1, zFar));
            buffer.AddBox(new Vector3(open1, y0, zNear), new Vector3(xTo, y1, zFar));
            buffer.AddBox(new Vector3(open0, openTop, zNear), new Vector3(open1, y1, zFar), includeBottom: true);
        }

        private static void AddWallAlongZ(MeshBuffer buffer, float xNear, float xFar, float zFrom, float zTo, float y0, float y1, float open0, float open1, float openTop)
        {
            if (float.IsNaN(open0))
            {
                buffer.AddBox(new Vector3(xNear, y0, zFrom), new Vector3(xFar, y1, zTo));
                return;
            }
            buffer.AddBox(new Vector3(xNear, y0, zFrom), new Vector3(xFar, y1, open0));
            buffer.AddBox(new Vector3(xNear, y0, open1), new Vector3(xFar, y1, zTo));
            if (openTop < y1 - 0.05f)
            {
                buffer.AddBox(new Vector3(xNear, openTop, open0), new Vector3(xFar, y1, open1), includeBottom: true);
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

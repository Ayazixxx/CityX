using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Car parks (SpecialTypes.ParkingGarage, SpecialTypes.ParkingLot). Research, 2026-10-12: a stand-alone garage has 10.5-11.5 ft floors
    /// (here 4 m, the game's 1.3x), parked-on ramps at 5-6%, double-loaded bays 54-60 ft wide, structural bays 45-48 ft, open sides with
    /// 42 in spandrels, two stairs; a surface lot is rows of 2.6 x 5 m stalls either side of a 6-7 m aisle.
    ///
    /// Abandoned: rubble, a barrier dragged aside, the bays empty.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        public const float GarageSpandrelHeight = 1.4f;
        public const float GarageSlabThickness = 0.35f;
        public const float GarageColumnSize = 0.6f;
        public const float GarageColumnBayX = 8.0f;
        public const float GarageColumnBayZ = 16.0f;
        public const float GarageRampWidth = 6.0f;

        /// <summary>Half the lane kept clear of columns on the lowest deck straight in from each entrance, metres (a 3.4 m car and room).</summary>
        public const float GarageEntranceLaneHalf = 2.6f;
        public const float GarageEntranceWidth = GarageEntrances.OpeningWidth;

        /// <summary>An entrance ramp dropping more than this from the deck gets a wall along each side of its cut.</summary>
        public const float GarageCutWallRise = 0.45f;

        /// <summary>A parking stall: a real 2.6 x 5.0 m at 1.3x.</summary>
        public const float StallWidth = 3.2f;
        public const float StallDepth = 6.2f;
        public const float AisleWidth = 8.0f;


        private static readonly Color32 GarageFloorLook = SurfaceLook.Pack(new Color(0.62f, 0.62f, 0.63f), SurfaceLook.Asphalt, 0.7f);
        public static readonly Color32 LotLook = SurfaceLook.Pack(new Color(0.70f, 0.70f, 0.70f), SurfaceLook.Asphalt, 0.75f);
        public static readonly Color32 StallPaint = SurfaceLook.Pack(new Color(0.95f, 0.95f, 0.92f), SurfaceLook.Paving, 0.3f);

        /// <summary>
        /// A multi-storey car park in the builder's frame (x along the frontage, +z the front, y absolute): decks on a column grid, each
        /// with a spandrel round it and open air above the spandrel, a parked-on ramp along the back wall zigzagging up to the roof, two
        /// entrance openings on the front, and bays painted on every deck. The distant version is the outside only.
        /// </summary>
        private static void BuildGarage(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer structures, MeshBuffer concrete, SiteLooks looks, bool far)
        {
            float x0 = (float)(site.MinX - origin.x), x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z), z1 = (float)(site.MaxZ - origin.z);
            float podiumY = site.PodiumBase - origin.y;
            float padY = site.BaseHeight - origin.y;   // the plan's floor: the top of the lot's ground
            float height = site.Params.Height;
            float roofY = padY + height;
            Color32 wall = looks.Base;   // the ground-floor material: a garage is all base
            Color32 frame = CityProps.Concrete;

            // THE LOWEST DECK IS AT THE STREET (GarageEntrances): on its podium over the top of its lot the deck stood up to 16 m over the street
            // and no car could get in. The decks run from the street up to the roof the plan gave it, walled solid below the plan's floor (the hill
            // is behind them); where an opening's paving is lower than the deck, a short driveway ramp inside it. The frontage in strips across:
            // beside, an opening, the pier, an opening, beside; an opening's strip is the deck only behind its ramp.
            GarageEntrance[] entrances = GarageEntrances.For(world.Plan, site);
            float floorY = entrances[0].DeckY - origin.y;
            int levels = Mathf.Max(2, Mathf.RoundToInt((roofY - floorY) / (float)CityX.World.Plan.Amenities.GarageFloorHeight));
            float floorH = (roofY - floorY) / levels;
            float cx = (x0 + x1) * 0.5f;
            float[] stripX = { x0, cx + entrances[0].X0, cx + entrances[0].X1, cx + entrances[1].X0, cx + entrances[1].X1, x1 };
            float StripRamp(int strip) => strip == 1 ? entrances[0].Length : strip == 3 ? entrances[1].Length : 0.0f;

            // The podium fills the ground under the lowest deck where it falls away below it (a metre into the ground: the ground is cut away
            // under the whole garage, CitySurfaceBuilder), notched for the ramps, each on a wedge of its own down to the paving.
            concrete.Look = wall;
            for (int s = 0; s < 5; s++)
            {
                float sx0 = Mathf.Max(x0, stripX[s]), sx1 = Mathf.Min(x1, stripX[s + 1]);
                if (sx1 - sx0 < 0.01f || floorY - podiumY <= 0.02f) continue;
                concrete.AddBox(new Vector3(sx0, podiumY - 1.0f, z0), new Vector3(sx1, floorY, z1 - StripRamp(s)));
            }
            for (int i = 0; i < 2; i++)
            {
                GarageEntrance e = entrances[i];
                if (!e.HasRamp) continue;
                float ex0 = cx + e.X0, ex1 = cx + e.X1;
                float top = z1 - e.Length;
                float foot = e.StreetY - origin.y, deck = floorY;
                float bottom = Mathf.Min(podiumY, foot) - 0.3f;
                // The ramp's own surface, a wedge's sloped top, from the deck at its top down to the paving at the front wall.
                concrete.Look = GarageFloorLook;
                Vector3 slope = new Vector3(0.0f, foot - deck, e.Length).normalized;
                Vector3 up = new Vector3(0.0f, slope.z, -slope.y);
                concrete.AddQuad(new Vector3(ex0, deck + GarageEntrances.FloorLift, top), new Vector3(ex0, foot + GarageEntrances.FloorLift, z1), new Vector3(ex1, foot + GarageEntrances.FloorLift, z1), new Vector3(ex1, deck + GarageEntrances.FloorLift, top),
                    up, new Vector2(ex0, top), new Vector2(ex0, z1), new Vector2(ex1, z1), new Vector2(ex1, top));
                concrete.Look = wall;
                // Its front face (down from the paving into the ground) and its two sides (against the podium beside it, below it where the podium stops).
                concrete.AddQuad(new Vector3(ex1, bottom, z1), new Vector3(ex0, bottom, z1), new Vector3(ex0, foot, z1), new Vector3(ex1, foot, z1), Vector3.forward,
                    new Vector2(0, 0), new Vector2(ex1 - ex0, 0), new Vector2(ex1 - ex0, foot - bottom), new Vector2(0, foot - bottom));
                concrete.AddQuad(new Vector3(ex0, bottom, top), new Vector3(ex0, bottom, z1), new Vector3(ex0, foot, z1), new Vector3(ex0, deck, top), Vector3.left,
                    new Vector2(0, 0), new Vector2(e.Length, 0), new Vector2(e.Length, foot - bottom), new Vector2(0, deck - bottom));
                concrete.AddQuad(new Vector3(ex1, bottom, z1), new Vector3(ex1, bottom, top), new Vector3(ex1, deck, top), new Vector3(ex1, foot, z1), Vector3.right,
                    new Vector2(0, 0), new Vector2(e.Length, 0), new Vector2(e.Length, deck - bottom), new Vector2(0, foot - bottom));
                // A wall along each side of the cut on the deck, where it drops more than a kerb: nothing drives or walks off into it.
                if (e.Rise > GarageCutWallRise)
                {
                    concrete.AddBox(new Vector3(ex0 - 0.25f, deck, top), new Vector3(ex0, deck + 0.9f, z1 - 0.3f));
                    concrete.AddBox(new Vector3(ex1, deck, top), new Vector3(ex1 + 0.25f, deck + 0.9f, z1 - 0.3f));
                }
            }

            // The ground deck's surface and every deck above it (a slab with the ramp's arrival cut out of it), with the roof on top.
            float rampZ0 = z0 + 0.6f, rampZ1 = rampZ0 + GarageRampWidth;
            for (int k = 0; k <= levels; k++)
            {
                float slabTop = floorY + k * floorH;
                bool roof = k == levels;
                if (k == 0)
                {
                    // In strips, each behind its ramp.
                    concrete.Look = GarageFloorLook;
                    for (int s = 0; s < 5; s++)
                    {
                        float sx0 = Mathf.Max(x0, stripX[s]), sx1 = Mathf.Min(x1, stripX[s + 1]), sz1 = z1 - StripRamp(s);
                        if (sx1 - sx0 < 0.01f) continue;
                        concrete.AddQuad(new Vector3(sx0, slabTop + GarageEntrances.FloorLift, z0), new Vector3(sx0, slabTop + GarageEntrances.FloorLift, sz1), new Vector3(sx1, slabTop + GarageEntrances.FloorLift, sz1), new Vector3(sx1, slabTop + GarageEntrances.FloorLift, z0),
                            Vector3.up, new Vector2(sx0, z0), new Vector2(sx0, sz1), new Vector2(sx1, sz1), new Vector2(sx1, z0));
                    }
                }
                else if (!far || roof)
                {
                    // Where the ramp from the deck below arrives: the top of that ramp, 10 m of it, is open in this slab.
                    bool rampRises = (k - 1) % 2 == 0;
                    float holeX0 = rampRises ? x1 - 12.0f : x0 + 2.0f;
                    float holeX1 = holeX0 + 10.0f;
                    concrete.Look = roof ? looks.Roof : frame;
                    AddSlabWithHole(concrete, x0, x1, z0, z1, slabTop - GarageSlabThickness, slabTop, holeX0, holeX1, rampZ0, rampZ1, roof ? looks.Roof : GarageFloorLook);
                }
                if (roof)
                {
                    // Parapet round the roof.
                    concrete.Look = wall;
                    AddParapetRing(concrete, x0, x1, z0, z1, slabTop, slabTop + 1.2f, 0.3f);
                    break;
                }

                // The spandrel round this level (open above it), with the entrance openings on the front at street level. Below the plan's floor
                // the walls are solid up to it (to the next deck when it is higher still): the hill the garage is dug into stands behind them.
                float bandTop = slabTop + GarageSpandrelHeight;
                float wallTop = Mathf.Max(bandTop, Mathf.Min(padY, slabTop + floorH - GarageSlabThickness));
                structures.Look = wall;
                structures.AddBox(new Vector3(x0, slabTop, z0), new Vector3(x1, wallTop, z0 + 0.3f));             // back
                structures.AddBox(new Vector3(x0, slabTop, z0), new Vector3(x0 + 0.3f, wallTop, z1));             // left
                structures.AddBox(new Vector3(x1 - 0.3f, slabTop, z0), new Vector3(x1, wallTop, z1));             // right
                if (k == 0)
                {
                    // Two openings either side of the middle for cars, with the band between them carrying the P sign.
                    float gap = GarageEntrances.OpeningWidth;
                    float pier = GarageEntrances.HalfPier;
                    structures.AddBox(new Vector3(x0, slabTop, z1 - 0.3f), new Vector3(cx - pier - gap, wallTop, z1));
                    structures.AddBox(new Vector3(cx - pier, slabTop, z1 - 0.3f), new Vector3(cx + pier, wallTop, z1));
                    structures.AddBox(new Vector3(cx + pier + gap, slabTop, z1 - 0.3f), new Vector3(x1, wallTop, z1));
                    // A lintel over each opening, so the deck above has something to sit on.
                    structures.AddBox(new Vector3(cx - pier - gap, floorY + floorH - 0.9f, z1 - 0.3f), new Vector3(cx - pier, floorY + floorH, z1));
                    structures.AddBox(new Vector3(cx + pier, floorY + floorH - 0.9f, z1 - 0.3f), new Vector3(cx + pier + gap, floorY + floorH, z1));
                    CityProps.AddParkingSign(structures, cx, z1 + 0.05f, bandTop + 0.4f, 1.0f, 0.0f, 2.4f);
                }
                else
                {
                    structures.AddBox(new Vector3(x0, slabTop, z1 - 0.3f), new Vector3(x1, wallTop, z1));
                }

                if (far)
                {
                    // From afar the open air between the spandrels is a dark band: the deck's shadowed inside.
                    structures.Look = SurfaceLook.Pack(new Color(0.16f, 0.16f, 0.17f), (int)WallMaterial.Concrete, 0.8f);
                    float voidTop = slabTop + floorH - GarageSlabThickness;
                    AddWallRing(structures, x0 + 0.1f, x1 - 0.1f, z0 + 0.1f, z1 - 0.1f, wallTop, voidTop);
                    continue;
                }

                // Columns on the grid, this level's height.
                float levelTop = slabTop + floorH - GarageSlabThickness;
                concrete.Look = frame;
                int nx = Mathf.Max(2, Mathf.RoundToInt((x1 - x0) / GarageColumnBayX));
                int nz = Mathf.Max(2, Mathf.RoundToInt((z1 - z0) / GarageColumnBayZ));
                for (int ix = 0; ix <= nx; ix++)
                {
                    float px = Mathf.Lerp(x0 + 0.5f, x1 - 0.5f, ix / (float)nx);
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        float pz = Mathf.Lerp(z0 + 0.5f, z1 - 0.5f, iz / (float)nz);
                        if (k == 0 && (Mathf.Abs(px - cx) < GarageEntranceWidth + 3.5f && pz > z1 - 1.0f || OnEntranceRamp(px, pz, 0.6f)))
                        {
                            continue;   // not in the entrance, nor on its ramp
                        }
                        if (pz > rampZ0 - 0.4f && pz < rampZ1 + 0.4f && iz != 0)
                        {
                            continue;   // not on the ramp
                        }
                        if (k == 0 && InEntranceLane(px))
                        {
                            continue;   // not in the lane straight in from an entrance (2026-10-16: a column 16 m in stopped the car on seed 19077151)
                        }
                        concrete.AddBox(new Vector3(px - GarageColumnSize * 0.5f, slabTop, pz - GarageColumnSize * 0.5f),
                            new Vector3(px + GarageColumnSize * 0.5f, levelTop, pz + GarageColumnSize * 0.5f));
                    }
                }

                // The ramp up to the next deck, along the back wall: rising to the right on even levels, to the left on odd.
                {
                    bool rampRises = k % 2 == 0;
                    float lowX = rampRises ? x0 + 2.0f : x1 - 2.0f;
                    float highX = rampRises ? x1 - 2.0f : x0 + 2.0f;
                    AddGarageRamp(concrete, lowX, highX, rampZ0, rampZ1, slabTop, slabTop + floorH, frame, GarageFloorLook);
                }

                // Bays along the front half of the deck (the back strip is the ramp).
                float rowZ0 = rampZ1 + AisleWidth, rowZ1 = rowZ0 + StallDepth;
                float rowZ2 = z1 - 0.5f - StallDepth, rowZ3 = z1 - 0.5f;
                if (rowZ2 > rowZ1 + AisleWidth * 0.5f)
                {
                    if (k == 0)
                    {
                        // The ground deck's rows stop either side of an entrance ramp that reaches into them.
                        PaintStallRowBesideRamps(rowZ0, rowZ1, slabTop + 0.02f);
                        PaintStallRowBesideRamps(rowZ2, rowZ3, slabTop + 0.02f);
                    }
                    else
                    {
                        PaintStallRow(concrete, x0 + 1.0f, x1 - 1.0f, rowZ0, rowZ1, slabTop + 0.02f);
                        PaintStallRow(concrete, x0 + 1.0f, x1 - 1.0f, rowZ2, rowZ3, slabTop + 0.02f);
                    }
                }
            }

            // The lowest deck's lane in from each entrance, as wide as a car (Vehicles/Car, 3.4 m at the player's scale) and room either side.
            bool InEntranceLane(float px)
            {
                foreach (GarageEntrance e in entrances)
                {
                    if (e.HasDriveway && System.Math.Abs(px - (cx + (e.X0 + e.X1) * 0.5f)) < GarageEntranceLaneHalf) return true;
                }
                return false;
            }

            bool OnEntranceRamp(float px, float pz, float margin)
            {
                foreach (GarageEntrance e in entrances)
                {
                    if (e.HasRamp && px > cx + e.X0 - margin && px < cx + e.X1 + margin && pz > z1 - e.Length - margin) return true;
                }
                return false;
            }

            void PaintStallRowBesideRamps(float rz0, float rz1, float y)
            {
                float from = x0 + 1.0f;
                foreach (GarageEntrance e in entrances)
                {
                    if (!e.HasRamp || z1 - e.Length >= rz1) continue;
                    if (cx + e.X0 - 0.5f - from > StallWidth) PaintStallRow(concrete, from, cx + e.X0 - 0.5f, rz0, rz1, y);
                    from = cx + e.X1 + 0.5f;
                }
                if (x1 - 1.0f - from > StallWidth) PaintStallRow(concrete, from, x1 - 1.0f, rz0, rz1, y);
            }

            // The roof's own bays.
            if (!far)
            {
                float rowZ0 = rampZ1 + AisleWidth, rowZ1 = rowZ0 + StallDepth;
                if (z1 - 0.5f - StallDepth > rowZ1)
                {
                    PaintStallRow(concrete, x0 + 1.0f, x1 - 1.0f, rowZ0, rowZ1, roofY + 0.02f);
                }
                // A few things left on the ground deck: a barrier knocked over at the entrance, rubble.
                CityProps.AddRubble(structures, x0 + 4.0f, z0 + 10.0f, floorY, 1.6f, site.Seed + 55);
            }
            structures.Look = SurfaceLook.Default;
            concrete.Look = SurfaceLook.Default;
        }

        /// <summary>A slab between two heights with a rectangular hole in it, as the boxes round the hole; the top face in its own look.</summary>
        private static void AddSlabWithHole(MeshBuffer buffer, float x0, float x1, float z0, float z1, float bottom, float top,
            float hx0, float hx1, float hz0, float hz1, Color32 topLook)
        {
            Color32 side = buffer.Look;
            var topBuffer = buffer;
            void Box(float ax, float bx, float az, float bz)
            {
                if (bx - ax < 0.05f || bz - az < 0.05f) return;
                buffer.Look = side;
                buffer.AddQuad(new Vector3(ax, bottom, az), new Vector3(bx, bottom, az), new Vector3(bx, bottom, bz), new Vector3(ax, bottom, bz), Vector3.down,
                    new Vector2(ax, az), new Vector2(bx, az), new Vector2(bx, bz), new Vector2(ax, bz));
                buffer.Look = topLook;
                buffer.AddQuad(new Vector3(ax, top, az), new Vector3(ax, top, bz), new Vector3(bx, top, bz), new Vector3(bx, top, az), Vector3.up,
                    new Vector2(ax, az), new Vector2(ax, bz), new Vector2(bx, bz), new Vector2(bx, az));
            }
            hx0 = Mathf.Clamp(hx0, x0, x1);
            hx1 = Mathf.Clamp(hx1, x0, x1);
            hz0 = Mathf.Clamp(hz0, z0, z1);
            hz1 = Mathf.Clamp(hz1, z0, z1);
            Box(x0, hx0, z0, z1);
            Box(hx1, x1, z0, z1);
            Box(hx0, hx1, z0, hz0);
            Box(hx0, hx1, hz1, z1);
            // The hole's four inner faces, and the slab's outer edges.
            buffer.Look = side;
            AddWallRing(buffer, x0, x1, z0, z1, bottom, top);
            AddWallRing(buffer, hx0, hx1, hz0, hz1, bottom, top, inward: true);
            buffer.Look = side;
        }

        /// <summary>The four vertical faces of a box's outline between two heights, facing out (or in, for a hole).</summary>
        private static void AddWallRing(MeshBuffer buffer, float x0, float x1, float z0, float z1, float y0, float y1, bool inward = false)
        {
            if (x1 - x0 < 0.05f || z1 - z0 < 0.05f || y1 - y0 < 0.01f) return;
            float w = x1 - x0, d = z1 - z0, h = y1 - y0;
            Vector3 f = inward ? Vector3.forward : Vector3.back, bk = inward ? Vector3.back : Vector3.forward;
            Vector3 r = inward ? Vector3.right : Vector3.left, l = inward ? Vector3.left : Vector3.right;
            buffer.AddQuad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), f,
                new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
            buffer.AddQuad(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), bk,
                new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
            buffer.AddQuad(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), r,
                new Vector2(0, 0), new Vector2(d, 0), new Vector2(d, h), new Vector2(0, h));
            buffer.AddQuad(new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), l,
                new Vector2(0, 0), new Vector2(d, 0), new Vector2(d, h), new Vector2(0, h));
        }

        private static void AddParapetRing(MeshBuffer buffer, float x0, float x1, float z0, float z1, float y0, float y1, float thickness)
        {
            buffer.AddBox(new Vector3(x0, y0, z0), new Vector3(x1, y1, z0 + thickness));
            buffer.AddBox(new Vector3(x0, y0, z1 - thickness), new Vector3(x1, y1, z1));
            buffer.AddBox(new Vector3(x0, y0, z0), new Vector3(x0 + thickness, y1, z1));
            buffer.AddBox(new Vector3(x1 - thickness, y0, z0), new Vector3(x1, y1, z1));
        }

        /// <summary>A parked-on ramp: a sloped slab from (lowX, lowY) up to (highX, highY) between two z lines, with its sides and underside.</summary>
        private static void AddGarageRamp(MeshBuffer buffer, float lowX, float highX, float rz0, float rz1, float lowY, float highY, Color32 side, Color32 top)
        {
            float thick = 0.3f;
            Vector3 a0 = new Vector3(lowX, lowY, rz0), a1 = new Vector3(lowX, lowY, rz1);
            Vector3 b0 = new Vector3(highX, highY, rz0), b1 = new Vector3(highX, highY, rz1);
            Vector3 slope = (b0 - a0).normalized;
            Vector3 up = Vector3.Cross(Vector3.forward, slope).normalized;
            if (up.y < 0.0f) up = -up;
            float len = Vector3.Distance(a0, b0);
            buffer.Look = top;
            buffer.AddQuad(a0, a1, b1, b0, up, new Vector2(0, 0), new Vector2(0, rz1 - rz0), new Vector2(len, rz1 - rz0), new Vector2(len, 0));
            Vector3 down = -up * thick;
            buffer.Look = side;
            buffer.AddQuad(a0 + down, b0 + down, b1 + down, a1 + down, -up, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, rz1 - rz0), new Vector2(0, rz1 - rz0));
            buffer.AddQuad(a0, b0, b0 + down, a0 + down, Vector3.back, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, thick), new Vector2(0, thick));
            buffer.AddQuad(b1, a1, a1 + down, b1 + down, Vector3.forward, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, thick), new Vector2(0, thick));
            // A low kerb along the open side so a car reads as held on.
            float kerbSign = lowX < highX ? 1.0f : -1.0f;
            for (int i = 0; i < 6; i++)
            {
                float t0 = i / 6.0f, t1 = (i + 1) / 6.0f;
                Vector3 p0 = Vector3.Lerp(a1, b1, t0), p1 = Vector3.Lerp(a1, b1, t1);
                buffer.AddQuad(p0, p1, p1 + Vector3.up * 0.3f, p0 + Vector3.up * 0.3f, Vector3.forward, new Vector2(0, 0), new Vector2(len / 6.0f, 0), new Vector2(len / 6.0f, 0.3f), new Vector2(0, 0.3f));
                buffer.AddQuad(p0 + new Vector3(0, 0, -0.25f), p1 + new Vector3(0, 0, -0.25f), p1 + new Vector3(0, 0.3f, -0.25f), p0 + new Vector3(0, 0.3f, -0.25f), Vector3.back,
                    new Vector2(0, 0), new Vector2(len / 6.0f, 0), new Vector2(len / 6.0f, 0.3f), new Vector2(0, 0.3f));
                buffer.AddQuad(p0 + new Vector3(0, 0.3f, -0.25f), p1 + new Vector3(0, 0.3f, -0.25f), p1 + Vector3.up * 0.3f, p0 + Vector3.up * 0.3f, Vector3.up,
                    new Vector2(0, 0), new Vector2(len / 6.0f, 0), new Vector2(len / 6.0f, 0.25f), new Vector2(0, 0.25f));
            }
            _ = kerbSign;
        }

        /// <summary>A row of stalls between two z lines, divided by painted lines every StallWidth along x.</summary>
        private static void PaintStallRow(MeshBuffer paint, float x0, float x1, float rz0, float rz1, float y)
        {
            int stalls = Mathf.FloorToInt((x1 - x0) / StallWidth);
            if (stalls < 1) return;
            float start = x0 + ((x1 - x0) - stalls * StallWidth) * 0.5f;
            paint.Look = StallPaint;
            for (int i = 0; i <= stalls; i++)
            {
                float lx = start + i * StallWidth;
                paint.AddQuad(new Vector3(lx - 0.06f, y, rz0), new Vector3(lx - 0.06f, y, rz1), new Vector3(lx + 0.06f, y, rz1), new Vector3(lx + 0.06f, y, rz0), Vector3.up,
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            }
        }

        /// <summary>
        /// An open car park in the builder's frame: the lot paved, a low wall round it with the entrance on the front, stalls either side of an
        /// aisle, a ticket booth and barrier at the entrance. The distant version is just the paving and the wall.
        /// </summary>
        private static void BuildParkingLot(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer structures, MeshBuffer concrete, bool far)
        {
            float x0 = (float)(site.MinX - origin.x), x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z), z1 = (float)(site.MaxZ - origin.z);

            // A lot is graded with the street, not built up on a podium like a building: its surface follows the ground the way the block's
            // paving round it does (CitySurfaceBuilder.BuildBlockPaving), a hair above the paving, and its wall steps down the slope with it.
            float Top(float bx, float bz)
            {
                site.LocalToWorld(bx + origin.x - site.CentreX, bz + origin.z - site.CentreZ, out double wx, out double wz);
                return (float)(world.Plan.GroundHeight(wx, wz) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight) + LotLift - origin.y;
            }

            concrete.Look = LotLook;
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / LotCell)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / LotCell));
            for (int i = 0; i < nx; i++)
            {
                float ax = Mathf.Lerp(x0, x1, (float)i / nx), bx = Mathf.Lerp(x0, x1, (float)(i + 1) / nx);
                for (int j = 0; j < nz; j++)
                {
                    float az = Mathf.Lerp(z0, z1, (float)j / nz), bz = Mathf.Lerp(z0, z1, (float)(j + 1) / nz);
                    concrete.AddQuad(new Vector3(ax, Top(ax, az), az), new Vector3(ax, Top(ax, bz), bz), new Vector3(bx, Top(bx, bz), bz), new Vector3(bx, Top(bx, az), az),
                        Vector3.up, new Vector2(ax, az), new Vector2(ax, bz), new Vector2(bx, bz), new Vector2(bx, az));
                }
            }

            // The asphalt's own height anywhere on it: on the very triangles just laid (each cell split corner (ax, az) to (bx, bz), as AddQuad
            // splits it), so the paint lies exactly on it. Sampled on its own, the paint dipped under the asphalt between its ends and flickered
            // in and out (owner, 2026-10-16: "the parking spot decals are glitching like fading in and out").
            float cellW = (x1 - x0) / nx, cellD = (z1 - z0) / nz;
            float Surface(float px, float pz)
            {
                int i = Mathf.Clamp(Mathf.FloorToInt((px - x0) / cellW), 0, nx - 1);
                int j = Mathf.Clamp(Mathf.FloorToInt((pz - z0) / cellD), 0, nz - 1);
                float ax = x0 + i * cellW, az = z0 + j * cellD;
                float u = Mathf.Clamp01((px - ax) / cellW), v = Mathf.Clamp01((pz - az) / cellD);
                float ha = Top(ax, az), hb = Top(ax, az + cellD), hc = Top(ax + cellW, az + cellD), hd = Top(ax + cellW, az);
                return v >= u ? ha + (hc - hb) * u + (hb - ha) * v : ha + (hd - ha) * u + (hc - hd) * v;
            }

            // The wall: 0.6 m, running with the ground, with the entrance gap on the front wall.
            float cx = (x0 + x1) * 0.5f;
            float gap = GarageEntranceWidth;
            structures.Look = CityProps.Concrete;
            // Across the entrance gap, the asphalt's edge face down into the ground (the ground is cut away under the lot: CitySurfaceBuilder).
            for (int k = 0; k < 4; k++)
            {
                float ea = cx - gap * 0.5f + gap * k / 4.0f, eb = cx - gap * 0.5f + gap * (k + 1) / 4.0f;
                float ya = Top(ea, z1), yb = Top(eb, z1);
                structures.AddQuad(new Vector3(ea, ya - 0.5f, z1), new Vector3(eb, yb - 0.5f, z1), new Vector3(eb, yb, z1), new Vector3(ea, ya, z1), Vector3.forward,
                    new Vector2(0, 0), new Vector2(eb - ea, 0), new Vector2(eb - ea, 0.5f), new Vector2(0, 0.5f));
            }
            AddSlopingWall(structures, x0, z0, x1, z0 + 0.25f, Top);
            AddSlopingWall(structures, x0, z0, x0 + 0.25f, z1, Top);
            AddSlopingWall(structures, x1 - 0.25f, z0, x1, z1, Top);
            AddSlopingWall(structures, x0, z1 - 0.25f, cx - gap * 0.5f, z1, Top);
            AddSlopingWall(structures, cx + gap * 0.5f, z1 - 0.25f, x1, z1, Top);
            if (far)
            {
                structures.Look = SurfaceLook.Default;
                concrete.Look = SurfaceLook.Default;
                return;
            }

            // The booth and barrier arm at the entrance.
            float bxc = cx + gap * 0.5f + 1.6f;
            float yEntrance = Top(cx, z1 - 1.5f);
            if (bxc + 1.2f < x1 - 0.5f)
            {
                float yb = Top(bxc, z1 - 1.8f);
                structures.Look = CityProps.DarkMetal;
                structures.AddBox(new Vector3(bxc - 1.1f, yb - 0.3f, z1 - 3.0f), new Vector3(bxc + 1.1f, yb + 2.7f, z1 - 0.6f));
                structures.Look = CityProps.White;
                structures.AddBox(new Vector3(bxc - 1.15f, yb + 2.7f, z1 - 3.05f), new Vector3(bxc + 1.15f, yb + 2.85f, z1 - 0.55f));
                structures.Look = CityProps.DarkMetal;
                structures.AddBox(new Vector3(cx + gap * 0.5f - 0.3f, yEntrance - 0.2f, z1 - 1.6f), new Vector3(cx + gap * 0.5f + 0.1f, yEntrance + 1.1f, z1 - 1.2f));
                structures.Look = SurfaceLook.Pack(new Color(0.85f, 0.15f, 0.12f), (int)WallMaterial.Metal, 0.4f);
                structures.AddBox(new Vector3(cx - gap * 0.5f, yEntrance + 1.0f, z1 - 1.45f), new Vector3(cx + gap * 0.5f - 0.3f, yEntrance + 1.1f, z1 - 1.35f));
            }
            float ySign = Top(cx - gap * 0.5f - 1.6f, z1 - 0.4f);
            CityProps.AddParkingSign(structures, cx - gap * 0.5f - 1.6f, z1 - 0.3f, ySign + 2.2f, 1.0f, 0.0f, 1.6f);
            structures.Look = CityProps.DarkMetal;
            structures.AddBox(new Vector3(cx - gap * 0.5f - 1.7f, ySign - 0.2f, z1 - 0.45f), new Vector3(cx - gap * 0.5f - 1.5f, ySign + 2.2f, z1 - 0.3f));

            // Stall rows along z, two rows either side of an aisle, repeated across the lot.
            float rowDepth = StallDepth * 2.0f + AisleWidth;
            float rowZ = z0 + 1.5f;
            int row = 0;
            while (rowZ + StallDepth <= z1 - 5.0f)
            {
                PaintStallRowOnGround(concrete, x0 + 1.0f, x1 - 1.0f, rowZ, rowZ + StallDepth, Surface, z0, cellD, x0, cellW);
                float second = rowZ + StallDepth + AisleWidth;
                if (second + StallDepth <= z1 - 5.0f)
                {
                    PaintStallRowOnGround(concrete, x0 + 1.0f, x1 - 1.0f, second, second + StallDepth, Surface, z0, cellD, x0, cellW);
                }
                rowZ += rowDepth;
                row++;
            }
            // What is left on an abandoned lot: a heap of rubbish, a barrier dragged aside.
            CityProps.AddRubble(structures, x0 + 2.5f, z0 + 2.5f, Top(x0 + 2.5f, z0 + 2.5f), 1.4f, site.Seed + 77);
            if (CityProps.Unit(site.Seed, 9, 59) < 0.5f)
            {
                CityProps.AddBarrier(structures, x1 - 4.0f, z1 - 2.2f, Top(x1 - 4.0f, z1 - 2.2f), 0.94f, 0.34f);
            }
            structures.Look = SurfaceLook.Default;
            concrete.Look = SurfaceLook.Default;
        }

        /// <summary>The lot's surface is sampled on cells this big, so it follows the hills the paving round it follows.</summary>
        public const float LotCell = 4.0f;

        /// <summary>
        /// How far the lot's asphalt stands above the block's paving. A kerb's worth (0.12) until 2026-10-16, so the paving (planar across each
        /// terrain cell) did not poke through the lot (sampled on its own, turned grid) where the ground bulges between the two; the ground and
        /// the paving are cut away under the lot since (CitySurfaceBuilder), so it lies all but flush with the paving at its entrance.
        /// </summary>
        public const float LotLift = 0.02f;

        /// <summary>
        /// A 0.6 m wall between two corners of a box outline, in pieces of about 3 m, each a prism whose top and bottom run with the ground
        /// (sunk 0.4 m into it, so a slope shows no gap): faces out, in, on top and at both ends.
        /// </summary>
        private static void AddSlopingWall(MeshBuffer buffer, float x0, float z0, float x1, float z1, System.Func<float, float, float> top)
        {
            bool alongX = x1 - x0 > z1 - z0;
            float length = alongX ? x1 - x0 : z1 - z0;
            if (length < 0.05f) return;
            int pieces = Mathf.Max(1, Mathf.CeilToInt(length / 3.0f));
            for (int i = 0; i < pieces; i++)
            {
                float a = (float)i / pieces, b = (float)(i + 1) / pieces;
                float px0 = alongX ? Mathf.Lerp(x0, x1, a) : x0, px1 = alongX ? Mathf.Lerp(x0, x1, b) : x1;
                float pz0 = alongX ? z0 : Mathf.Lerp(z0, z1, a), pz1 = alongX ? z1 : Mathf.Lerp(z0, z1, b);
                // The four corners' ground, named by their place: near/far along the wall, and the two sides across it.
                float yA = top(px0, pz0), yB = alongX ? top(px1, pz0) : top(px0, pz1);
                float yC = top(px1, pz1), yD = alongX ? top(px0, pz1) : top(px1, pz0);
                Vector3 a0 = new Vector3(px0, yA - 0.4f, pz0), a1 = new Vector3(px0, yA + 0.6f, pz0);
                Vector3 b0 = alongX ? new Vector3(px1, yB - 0.4f, pz0) : new Vector3(px0, yB - 0.4f, pz1), b1 = b0 + Vector3.up;
                Vector3 c0 = new Vector3(px1, yC - 0.4f, pz1), c1 = c0 + Vector3.up;
                Vector3 d0 = alongX ? new Vector3(px0, yD - 0.4f, pz1) : new Vector3(px1, yD - 0.4f, pz0), d1 = d0 + Vector3.up;
                float w = alongX ? px1 - px0 : pz1 - pz0, t = alongX ? pz1 - pz0 : px1 - px0;
                // a-b runs along the wall on one side, d-c on the other; the normals are only hints, the quad corrects its winding.
                buffer.AddQuad(a0, b0, b1, a1, alongX ? Vector3.back : Vector3.left, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, 1), new Vector2(0, 1));
                buffer.AddQuad(c0, d0, d1, c1, alongX ? Vector3.forward : Vector3.right, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, 1), new Vector2(0, 1));
                buffer.AddQuad(a1, b1, c1, d1, Vector3.up, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, t), new Vector2(0, t));
                buffer.AddQuad(d0, a0, a1, d1, alongX ? Vector3.left : Vector3.back, new Vector2(0, 0), new Vector2(t, 0), new Vector2(t, 1), new Vector2(0, 1));
                buffer.AddQuad(b0, c0, c1, b1, alongX ? Vector3.right : Vector3.forward, new Vector2(0, 0), new Vector2(t, 0), new Vector2(t, 1), new Vector2(0, 1));
            }
        }

        /// <summary>PaintStallRow on sloping ground: each line in two pieces that follow the surface.</summary>
        /// <summary>How far the paint stands over the lot's asphalt.</summary>
        public const float LotPaintLift = 0.03f;

        /// <summary>
        /// PaintStallRow on a sloping lot: each line cut where it crosses a cell of the asphalt (every <paramref name="cellD"/> from
        /// <paramref name="gridZ0"/>) and a cell's diagonal, each corner of each piece <see cref="LotPaintLift"/> over the asphalt's own triangles
        /// (<paramref name="surface"/>): a piece then runs parallel to the triangle under it, never into it.
        /// </summary>
        private static void PaintStallRowOnGround(MeshBuffer paint, float x0, float x1, float rz0, float rz1, System.Func<float, float, float> surface,
            float gridZ0, float cellD, float gridX0, float cellW)
        {
            int stalls = Mathf.FloorToInt((x1 - x0) / StallWidth);
            if (stalls < 1) return;
            float start = x0 + ((x1 - x0) - stalls * StallWidth) * 0.5f;
            paint.Look = StallPaint;
            var cuts = new System.Collections.Generic.List<float>(8);
            for (int i = 0; i <= stalls; i++)
            {
                float lx = start + i * StallWidth;
                cuts.Clear();
                cuts.Add(rz0);
                cuts.Add(rz1);
                // The cells' edges across the line, and where the line crosses each cell's diagonal (v = u within the cell).
                float u = (lx - gridX0) / cellW - Mathf.Floor((lx - gridX0) / cellW);
                for (int j = Mathf.FloorToInt((rz0 - gridZ0) / cellD); j <= Mathf.CeilToInt((rz1 - gridZ0) / cellD); j++)
                {
                    float edge = gridZ0 + j * cellD;
                    float diagonal = edge + u * cellD;
                    if (edge > rz0 && edge < rz1) cuts.Add(edge);
                    if (diagonal > rz0 && diagonal < rz1) cuts.Add(diagonal);
                }
                cuts.Sort();
                for (int k = 0; k + 1 < cuts.Count; k++)
                {
                    float za = cuts[k], zb = cuts[k + 1];
                    if (zb - za < 0.01f) continue;
                    paint.AddQuad(
                        new Vector3(lx - 0.06f, surface(lx - 0.06f, za) + LotPaintLift, za), new Vector3(lx - 0.06f, surface(lx - 0.06f, zb) + LotPaintLift, zb),
                        new Vector3(lx + 0.06f, surface(lx + 0.06f, zb) + LotPaintLift, zb), new Vector3(lx + 0.06f, surface(lx + 0.06f, za) + LotPaintLift, za), Vector3.up,
                        new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                }
            }
        }
    }
}

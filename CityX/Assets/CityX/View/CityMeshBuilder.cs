using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Turns one chunk of the world model into triangles.
    ///
    /// WHY THIS IS A SEPARATE ASSEMBLY FROM CityX.World. A dedicated server
    /// runs the same world model -- it has to, or it could not tell a client
    /// what is where -- but it must never build a mesh. Keeping the view in its
    /// own assembly makes that a compile-time fact rather than a convention
    /// somebody remembers. CityX.World has no idea this exists.
    ///
    /// Static and stateless. A chunk's geometry is a pure function of its
    /// coordinate, exactly like everything it is built from, so it can be
    /// rebuilt at any time from nothing.
    /// </summary>
    public static class CityMeshBuilder
    {
        /// <summary>
        /// How far a terrace slab is extruded downward when its neighbour is
        /// lower. Sized from the world's own proven worst case rather than
        /// picked, so a retaining wall can never come up short and leave a
        /// slot of daylight along a terrace edge.
        /// </summary>
        public static float SkirtDepth =>
            TerrainField.MaxNeighbourTerraceSteps * TerrainField.TerraceStep + 1.0f;

        /// <summary>Walkway deck width and thickness, in metres.</summary>
        public const float BridgeWidth = 4.0f;

        public const float BridgeThickness = 0.6f;

        /// <summary>
        /// Depth of the raised pavement along a street's building edges.
        ///
        /// Built as real geometry rather than painted on, because the point is
        /// that there is somewhere to stand that is not the road. Combined with
        /// the 3m lot setback this gives 7m of pedestrian space each side and
        /// leaves 10m of carriageway in a 24m street chunk.
        /// </summary>
        public const float SidewalkWidth = 4.0f;

        /// <summary>Kerb height. Enough to read as a step, low enough to walk up.</summary>
        public const float KerbHeight = 0.18f;

        /// <summary>
        /// Splits a chunk across three buffers, one per material, which become
        /// three submeshes on one object -- cheaper than three objects sharing
        /// a transform.
        ///
        /// The split is by SURFACE, not by what the thing is:
        ///   ground        road and bare terrain
        ///   flatConcrete  pavement, roofs and walkway decks
        ///   structures    building walls, which are the only thing that wants
        ///                 the window facade
        ///
        /// Roofs go with pavement rather than with their own building because
        /// they are the same material in the real world, and because putting
        /// the window texture on the top of every building is glaring the
        /// moment the camera gets above roof height.
        /// </summary>
        public static void BuildChunk(CityWorld world, SkybridgeLayout bridges, int chunkX, int chunkY,
            Vector3 origin, MeshBuffer ground, MeshBuffer flatConcrete, MeshBuffer structures,
            MeshBuffer litGlass = null, MeshBuffer darkGlass = null,
            MeshBuffer crackedGlass = null, GlassDamage damage = null,
            List<CrackedOpening> crackedOpenings = null, MeshBuffer sign = null)
        {
            BuildGround(world, chunkX, chunkY, origin, ground, flatConcrete);

            BuildingSite site = world.BuildingAt(chunkX, chunkY);
            if (!site.Exists || site.AnchorX != chunkX || site.AnchorY != chunkY)
            {
                // Either nothing here, or it belongs to a neighbouring anchor
                // and that chunk will build it. Building it twice would double
                // every multi-chunk tower.
                return;
            }

            BuildBuilding(world, site, origin, structures, flatConcrete,
                new Glazing(litGlass, darkGlass, crackedGlass, damage, crackedOpenings));
            AddSign(site, origin, sign);
            AddWing(world, site, origin, structures, flatConcrete);
            AddBalconies(world, site, origin, flatConcrete);
            AddRibs(world, site, origin, structures);
            BuildBridges(world, bridges, site, origin, flatConcrete);
        }

        private static void BuildGround(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            MeshBuffer ground, MeshBuffer pavement)
        {
            float pad = world.Terrain.PadHeightFor(chunkX, chunkY);
            float size = CityLayout.ChunkSize;

            float x0 = (float)(chunkX * (double)size - origin.x);
            float z0 = (float)(chunkY * (double)size - origin.z);
            float x1 = x0 + size;
            float z1 = z0 + size;
            float y = pad - origin.y;

            // TAKE THE BUILDING'S FOOTPRINT OUT OF THE GROUND. A building's own
            // lobby floor sits at BaseHeight, and for a lot on level ground
            // that is exactly this quad's own height -- two coplanar upward
            // faces, which is a z-fight rather than a floor. Cutting the
            // footprint leaves one surface, and the ground under a building was
            // never visible anyway.
            //
            // Every chunk cuts only its OWN share of the footprint, which is
            // its square intersected with the building, so a building covering
            // four chunks is handled by four separate chunks agreeing without
            // talking to each other -- the same rule the rest of this file
            // works by.
            BuildingSite standing = world.BuildingAt(chunkX, chunkY);
            Rect covered = Rect.zero;
            if (standing.Exists)
            {
                covered = Rect.MinMaxRect(
                    Mathf.Max(x0, (float)(standing.MinX - origin.x)),
                    Mathf.Max(z0, (float)(standing.MinZ - origin.z)),
                    Mathf.Min(x1, (float)(standing.MaxX - origin.x)),
                    Mathf.Min(z1, (float)(standing.MaxZ - origin.z)));
                if (covered.width <= 0.0f || covered.height <= 0.0f)
                {
                    covered = Rect.zero;
                }
            }
            AddSlabFace(ground, x0, x1, z0, z1, covered, y, Vector3.up);

            // Retaining walls, but only where the neighbour is actually lower.
            // Skirting every edge unconditionally would be simpler and would
            // bury a wall inside solid ground on roughly three quarters of
            // them -- invisible, but paid for on every chunk in the world.
            AddSkirtIfLower(world, chunkX - 1, chunkY, pad,
                new Vector3(x0, y, z0), new Vector3(x0, y, z1), Vector3.left, origin, ground);
            AddSkirtIfLower(world, chunkX + 1, chunkY, pad,
                new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.right, origin, ground);
            AddSkirtIfLower(world, chunkX, chunkY - 1, pad,
                new Vector3(x1, y, z0), new Vector3(x0, y, z0), Vector3.back, origin, ground);
            AddSkirtIfLower(world, chunkX, chunkY + 1, pad,
                new Vector3(x0, y, z1), new Vector3(x1, y, z1), Vector3.forward, origin, ground);

            AddSidewalks(chunkX, chunkY, x0, z0, x1, z1, y, pavement);
        }

        /// <summary>
        /// Raised pavement along the edges of a street where it meets buildings.
        ///
        /// A junction gets none, and that falls out of the grid rather than
        /// needing a special case: a street chunk has x or y divisible by the
        /// street period, so a straight run has buildable neighbours on exactly
        /// two OPPOSITE sides and a junction has street on all four. The two
        /// strips can therefore never meet at a corner and overlap.
        /// </summary>
        private static void AddSidewalks(int chunkX, int chunkY,
            float x0, float z0, float x1, float z1, float y, MeshBuffer pavement)
        {
            if (!CityLayout.IsStreetChunk(chunkX, chunkY))
            {
                return;
            }

            float top = y + KerbHeight;

            if (!CityLayout.IsStreetChunk(chunkX - 1, chunkY))
            {
                pavement.AddBox(new Vector3(x0, y, z0), new Vector3(x0 + SidewalkWidth, top, z1));
            }
            if (!CityLayout.IsStreetChunk(chunkX + 1, chunkY))
            {
                pavement.AddBox(new Vector3(x1 - SidewalkWidth, y, z0), new Vector3(x1, top, z1));
            }
            if (!CityLayout.IsStreetChunk(chunkX, chunkY - 1))
            {
                pavement.AddBox(new Vector3(x0, y, z0), new Vector3(x1, top, z0 + SidewalkWidth));
            }
            if (!CityLayout.IsStreetChunk(chunkX, chunkY + 1))
            {
                pavement.AddBox(new Vector3(x0, y, z1 - SidewalkWidth), new Vector3(x1, top, z1));
            }
        }

        private static void AddSkirtIfLower(CityWorld world, int neighbourX, int neighbourY, float pad,
            Vector3 top0, Vector3 top1, Vector3 normal, Vector3 origin, MeshBuffer ground)
        {
            float neighbourPad = world.Terrain.PadHeightFor(neighbourX, neighbourY);
            if (neighbourPad >= pad)
            {
                return;
            }

            // Overshoot past the neighbour's surface. Meeting it exactly leaves
            // a hairline of z-fighting along every terrace, which reads as the
            // world flickering when the camera moves.
            float drop = pad - neighbourPad + 0.5f;
            float bottomY = top0.y - Mathf.Min(drop, SkirtDepth);

            Vector3 bottom0 = new Vector3(top0.x, bottomY, top0.z);
            Vector3 bottom1 = new Vector3(top1.x, bottomY, top1.z);
            float width = Vector3.Distance(top0, top1);
            float height = top0.y - bottomY;

            ground.AddQuad(bottom0, bottom1, top1, top0, normal,
                new Vector2(0, 0), new Vector2(width, 0), new Vector2(width, height), new Vector2(0, height));
        }

        /// <summary>
        /// One building: a facade you can see from the street and a stack of
        /// floors you can walk up inside it.
        ///
        /// NOT A SOLID BOX ANY MORE. It used to be one AddBox, which is all a
        /// skyline needs -- but city_chunk.gd's buildings are enterable, with
        /// a door, floor slabs, a stairwell hole through each slab and a
        /// flight climbing to it, and the roof cap is holed so the top flight
        /// actually gets you out onto the roof. This is that, ported.
        ///
        /// WHAT GOES IN WHICH BUFFER. Only the outward facade goes to
        /// structures, because structures is the buffer wearing the window
        /// material. Everything inside -- interior walls, slabs, steps --
        /// goes to the concrete buffer, which is the closest two buffers get
        /// to city_chunk.gd's own separate INTERIOR_COLOR/INTERIOR_FLOOR_COLOR
        /// materials. Printing windows down the inside of a stairwell would
        /// be the same mistake as printing them across a roof.
        ///
        /// COLLISION COMES FREE AND IS NOT THE SAME AS GODOT'S. The chunk gets
        /// one MeshCollider over exactly these triangles, so there is no
        /// separate collision pass to keep in step -- which is the whole
        /// reason city_chunk.gd builds its interior wall by wall. It does mean
        /// the steps are collided individually rather than as Godot's single
        /// sloped ramp box; at a 0.25m riser against the controller's 0.4m
        /// step offset that walks, and it is a truer surface than the ramp.
        /// </summary>
        /// <summary>
        /// Where a building's windows go, and what has already been done to
        /// them.
        ///
        /// Bundled because these four always travel together and every wall
        /// needs all of them -- four more parameters on each of eight calls
        /// says nothing the name does not. An instance with no buffers is the
        /// INWARD copy of a wall: same openings, no glass, because the outward
        /// pass already hung it.
        /// </summary>
        private readonly struct Glazing
        {
            public readonly MeshBuffer Lit;
            public readonly MeshBuffer Dark;
            public readonly MeshBuffer Cracked;
            public readonly GlassDamage Damage;
            public readonly List<CrackedOpening> Openings;

            public Glazing(MeshBuffer lit, MeshBuffer dark, MeshBuffer cracked, GlassDamage damage,
                List<CrackedOpening> openings)
            {
                Lit = lit;
                Dark = dark;
                Cracked = cracked;
                Damage = damage;
                Openings = openings;
            }

            public bool HangsGlass => Lit != null;

            public static readonly Glazing OpeningsOnly = default;
        }

        /// <summary>
        /// A window somebody has already cracked, as a box you could walk
        /// through.
        ///
        /// HANDED OUT RATHER THAN KEPT, because a cracked pane has one more
        /// thing to do than be drawn: shoving a body through the opening
        /// finishes it off, which is city_chunk.gd's own second way of
        /// shattering a window. That needs a trigger at the pane, and the only
        /// place that knows exactly where a pane is, is the code that just put
        /// one there. Rederiving it in the streamer would be a second copy of
        /// the window grid, which is the mistake this whole file exists to
        /// avoid making twice.
        ///
        /// Positions are chunk-local, like every vertex the builder writes.
        /// </summary>
        public readonly struct CrackedOpening
        {
            public readonly PaneKey Key;
            public readonly Vector3 Centre;
            public readonly Vector3 Size;

            public CrackedOpening(PaneKey key, Vector3 centre, Vector3 size)
            {
                Key = key;
                Centre = centre;
                Size = size;
            }
        }

        /// <summary>
        /// How deep through the wall a cracked opening reaches for the purpose
        /// of noticing somebody in it. A pane is a flat sheet; a trigger with
        /// no thickness would be stepped over between two physics frames by
        /// anyone moving at a run.
        /// </summary>
        public const float CrackedOpeningDepth = 0.5f;

        private static void BuildBuilding(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer structures, MeshBuffer concrete, Glazing glazing)
        {
            // The stretch below BaseHeight is buried on the uphill side and
            // exposed downhill, which is what a hillside podium actually is.
            float x0 = (float)(site.MinX - origin.x);
            float x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z);
            float z1 = (float)(site.MaxZ - origin.z);
            float podiumY = site.PodiumBase - origin.y;
            float lobbyY = site.BaseHeight - origin.y;

            float height = site.Params.Height;
            float storey = BuildingInterior.StoreyHeight(height);
            int floors = BuildingInterior.FloorCountFor(height);
            float halfX = site.Params.Width * 0.5f;
            float halfZ = site.Params.Depth * 0.5f;
            float run = BuildingInterior.RunFor(halfX, halfZ);
            float centreX = (x0 + x1) * 0.5f;
            float centreZ = (z0 + z1) * 0.5f;
            float roofY = lobbyY + height;

            // The doorway, centred on the wall at maximum Z -- city_chunk.gd's
            // own door wall -- and standing on the lobby floor.
            float doorU0 = centreX - BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = centreX + BuildingInterior.DoorWidth * 0.5f;
            float doorTop = lobbyY + BuildingInterior.DoorHeight(height);

            var west = new Wall(true, x0, z0, z1);
            var east = new Wall(true, x1, z0, z1);
            var south = new Wall(false, z0, x0, x1);
            var north = new Wall(false, z1, x0, x1);

            int seed = world.ChunkSeed(site.AnchorX, site.AnchorY);
            CellLayout rows = WindowGrid.RowsFor(height);

            // The podium, below the lobby floor. Solid: it is buried on the
            // uphill side and exposed downhill, so glazing it would put windows
            // in a retaining wall.
            AddWallPanel(structures, west, z0, z1, podiumY, lobbyY, Vector3.left);
            AddWallPanel(structures, east, z0, z1, podiumY, lobbyY, Vector3.right);
            AddWallPanel(structures, south, x0, x1, podiumY, lobbyY, Vector3.back);
            AddWallPanel(structures, north, x0, x1, podiumY, lobbyY, Vector3.forward);

            // Outward, for the street, with real openings in it.
            AddWindowedWall(structures, glazing, site, west, z0, z1, lobbyY, roofY,
                Vector3.left, seed, WindowGrid.Wall.West, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, east, z0, z1, lobbyY, roofY,
                Vector3.right, seed, WindowGrid.Wall.East, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, south, x0, x1, lobbyY, roofY,
                Vector3.back, seed, WindowGrid.Wall.South, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, north, x0, x1, lobbyY, roofY,
                Vector3.forward, seed, WindowGrid.Wall.North, rows, doorU0, doorU1, true);

            // Inward, for the room. Same planes and the same openings, opposite
            // normals -- back to back, so neither is ever seen from the wrong
            // side, and a window is a hole through BOTH or it is not a hole.
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, west, z0, z1, lobbyY, roofY,
                Vector3.right, seed, WindowGrid.Wall.West, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, east, z0, z1, lobbyY, roofY,
                Vector3.left, seed, WindowGrid.Wall.East, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, south, x0, x1, lobbyY, roofY,
                Vector3.forward, seed, WindowGrid.Wall.South, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, north, x0, x1, lobbyY, roofY,
                Vector3.back, seed, WindowGrid.Wall.North, rows, doorU0, doorU1, true);

            // The lobby floor, and NOT a port: city_chunk.gd leans on the
            // chunk's own ground body for the ground floor, and here that
            // ground is cut away under the footprint (see BuildGround) so this
            // is the only thing holding anybody up. Solid, unless there is a
            // basement, in which case it is opened for the flight down.
            BuildingInterior.Stairwell basement = BuildingInterior.StairwellFor(
                BuildingInterior.BasementStairCorner, halfX, halfZ, run);
            Rect basementHole = site.Params.HasBasement
                ? new Rect(basement.Hole.x + centreX, basement.Hole.y + centreZ,
                    basement.Hole.width, basement.Hole.height)
                : Rect.zero;
            AddSlabFace(concrete, x0, x1, z0, z1, basementHole, lobbyY, Vector3.up);

            if (site.Params.HasBasement)
            {
                BuildBasement(site, concrete, x0, x1, z0, z1, lobbyY, storey,
                    centreX, centreZ, basement, basementHole, run);
            }

            for (int floor = 0; floor < floors; floor++)
            {
                float bottom = floor * storey;
                float top = BuildingInterior.FloorTop(floor, floors, height, storey);
                BuildingInterior.Stairwell well =
                    BuildingInterior.StairwellFor(floor, halfX, halfZ, run);
                Rect hole = new Rect(well.Hole.x + centreX, well.Hole.y + centreZ,
                    well.Hole.width, well.Hole.height);

                // This floor's ceiling. The last one is the roof itself, holed
                // exactly like the rest -- that opening is how the top flight
                // gets you outside.
                AddSlab(concrete, x0, x1, z0, z1, hole, lobbyY + top);

                AddRoomPartition(concrete, BuildingInterior.RoomFor(floor, halfX, halfZ),
                    centreX, centreZ, lobbyY + bottom, lobbyY + top, doorTop - lobbyY);

                if (floor == floors - 1)
                {
                    AddRoofDetail(concrete, site.Params.Roof, x0, x1, z0, z1, lobbyY + top);
                }

                foreach (BuildingInterior.Step step in
                         BuildingInterior.Steps(well, run, lobbyY + bottom, top - bottom))
                {
                    Vector3 half = step.Size * 0.5f;
                    Vector3 centre = new Vector3(
                        step.Centre.x + centreX, step.Centre.y, step.Centre.z + centreZ);
                    concrete.AddBox(centre - half, centre + half);
                }
            }
        }

        /// <summary>
        /// Balcony slabs and their railings, hung off the facade.
        ///
        /// Concrete rather than facade, like everything else that is not the
        /// windowed wall itself -- a balcony reading as a sheet of windows
        /// stuck to the side of a building is worse than no balcony.
        /// </summary>
        private static void AddBalconies(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer concrete)
        {
            List<BuildingDecor.Balcony> balconies = BuildingDecor.Balconies(
                world.ChunkSeed(site.AnchorX, site.AnchorY),
                site.Params.Width, site.Params.Depth, site.Params.Height);
            if (balconies.Count == 0)
            {
                return;
            }

            float centreX = (float)(site.CentreX - origin.x);
            float centreZ = (float)(site.CentreZ - origin.z);
            float baseY = site.BaseHeight - origin.y;

            foreach (BuildingDecor.Balcony balcony in balconies)
            {
                Vector3 centre = new Vector3(
                    balcony.Centre.x + centreX,
                    balcony.Centre.y + baseY,
                    balcony.Centre.z + centreZ);

                // Width runs one way and depth the other depending on which
                // wall it hangs off, which is what WidthAlongX settles.
                float sizeX = balcony.WidthAlongX
                    ? BuildingDecor.BalconyWidth : BuildingDecor.BalconyDepth;
                float sizeZ = balcony.WidthAlongX
                    ? BuildingDecor.BalconyDepth : BuildingDecor.BalconyWidth;

                AddCentredBox(concrete, centre.x, centre.z,
                    centre.y - BuildingDecor.BalconySlabThickness * 0.5f,
                    sizeX, BuildingDecor.BalconySlabThickness, sizeZ);

                // The rail along the open edge, set in by its own thickness so
                // it stands ON the slab rather than off the end of it.
                Vector3 outer = centre + balcony.Normal
                    * (BuildingDecor.BalconyDepth * 0.5f
                       - BuildingDecor.BalconyRailThickness * 0.5f);
                AddCentredBox(concrete, outer.x, outer.z, centre.y,
                    balcony.WidthAlongX
                        ? BuildingDecor.BalconyWidth : BuildingDecor.BalconyRailThickness,
                    BuildingDecor.BalconyRailHeight,
                    balcony.WidthAlongX
                        ? BuildingDecor.BalconyRailThickness : BuildingDecor.BalconyWidth);

                // And one down each side, so it reads as an enclosure rather
                // than a shelf with a fence on the far edge.
                Vector3 sideStep = balcony.WidthAlongX
                    ? new Vector3(BuildingDecor.BalconyWidth * 0.5f, 0.0f, 0.0f)
                    : new Vector3(0.0f, 0.0f, BuildingDecor.BalconyWidth * 0.5f);
                foreach (float sign in new[] { -1.0f, 1.0f })
                {
                    Vector3 side = centre + sideStep * sign;
                    AddCentredBox(concrete, side.x, side.z, centre.y,
                        balcony.WidthAlongX
                            ? BuildingDecor.BalconyRailThickness : BuildingDecor.BalconyDepth,
                        BuildingDecor.BalconyRailHeight,
                        balcony.WidthAlongX
                            ? BuildingDecor.BalconyDepth : BuildingDecor.BalconyRailThickness);
                }
            }
        }

        public const float SignWidth = 3.0f;
        public const float SignHeight = 1.1f;

        /// <summary>Clearance above the door's own fixed height.</summary>
        public const float SignBottomMargin = 0.6f;

        /// <summary>Pulled off the facade so the two do not z-fight.</summary>
        public const float SignWallOffset = 0.05f;

        /// <summary>
        /// The band over a special building's door.
        ///
        /// THE ONLY THING THAT MAKES A SPECIAL BUILDING LOOK SPECIAL right now.
        /// The generator has rolled supermarkets, banks, police stations and
        /// hospitals since it was ported -- with their own footprints, their own
        /// heights, their own roofs -- and every one of them has been rendered
        /// as an ordinary block, because nothing read the type. Same gap the
        /// roof types had.
        ///
        /// NOT A HOSPITAL CROSS. city_chunk.gd used to draw one and does not any
        /// more: its preset comment says that colour "was the red cross's own
        /// color; now used for its plain sign like every other special
        /// building". Porting the cross would have been porting something the
        /// original deleted.
        ///
        /// The COLOUR is the streamer's business, not this file's, which is the
        /// same split CityChunkGenerator's own facade weights already use:
        /// indices and geometry here, colours where the materials are.
        /// </summary>
        private static void AddSign(BuildingSite site, Vector3 origin, MeshBuffer sign)
        {
            if (sign == null || !site.Params.IsSpecial)
            {
                return;
            }

            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            float y0 = rows.Size * BuildingInterior.DoorHeightRows + SignBottomMargin;
            float y1 = y0 + SignHeight;

            // A preset right at its own minimum height can be short enough that
            // the sign would poke through its own roof. Godot skips it rather
            // than drawing past the roofline, and so does this.
            if (y1 > site.Params.Height - 0.3f)
            {
                return;
            }

            float centreX = (float)(site.CentreX - origin.x);
            float z = (float)(site.MaxZ - origin.z) + SignWallOffset;
            float baseY = site.BaseHeight - origin.y;
            float half = SignWidth * 0.5f;

            AddFace(sign, Vector3.forward,
                new Vector3(centreX - half, baseY + y0, z),
                new Vector3(centreX + half, baseY + y0, z),
                new Vector3(centreX + half, baseY + y1, z),
                new Vector3(centreX - half, baseY + y1, z));
        }

        /// <summary>
        /// The attached secondary volume that turns a box into an L.
        ///
        /// A SOLID BLOCK, NOT A SECOND ENTERABLE BUILDING, which is
        /// city_chunk.gd's own choice: a wing has no doorway, no stairwell and
        /// no interior, so there is nothing to walk into and nothing to leave
        /// a hole open for. Four windowed faces and a roof cap.
        ///
        /// IT STARTS AT THE PODIUM, not at the lobby floor Godot uses, and that
        /// is terrain rather than drift. A Godot building stands on flat
        /// ground, so its floor and the ground are the same height; here a lot
        /// spans terraces and the main walls already reach down to the lowest
        /// pad they cover. A wing stopping at the lobby floor would hang in the
        /// air on the downhill side.
        /// </summary>
        private static void AddWing(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer facade, MeshBuffer roof)
        {
            if (!TryWingBox(world, site, out Bounds box))
            {
                return;
            }
            facade.AddBox(box.min - origin, box.max - origin, roof);
        }

        /// <summary>
        /// Where a building's wing stands, in world space, or false if it has
        /// none.
        ///
        /// PUBLIC BECAUSE A TEST HAS TO ASK. The clamp that stops a wing from
        /// growing through the building next door is the whole reason this
        /// differs from Godot, and the only honest way to check it is to take
        /// every wing in a sweep and hold it against every neighbouring
        /// footprint. Rederiving these six numbers inside the test would be
        /// testing a copy of the arithmetic rather than the arithmetic.
        /// </summary>
        public static bool TryWingBox(CityWorld world, BuildingSite site, out Bounds box)
        {
            box = default;

            // How much clear ground each of the three candidate walls has: the
            // slack the building leaves inside its own lot, plus whatever lies
            // beyond that lot edge.
            CityChunkGenerator.LotRect lot = CityChunkGenerator.LotRectFor(site.Role);
            double lotCentreX = CityLayout.SpanCentre(site.AnchorX, site.Role.SpanX) + lot.CentreOffsetX;
            double lotCentreZ = CityLayout.SpanCentre(site.AnchorY, site.Role.SpanY) + lot.CentreOffsetZ;

            float westRoom = (float)(site.MinX - (lotCentreX - lot.Width * 0.5))
                             + CityChunkGenerator.MarginBeyond(site.Role, -1, 0);
            float eastRoom = (float)(lotCentreX + lot.Width * 0.5 - site.MaxX)
                             + CityChunkGenerator.MarginBeyond(site.Role, 1, 0);
            float southRoom = (float)(site.MinZ - (lotCentreZ - lot.Depth * 0.5))
                              + CityChunkGenerator.MarginBeyond(site.Role, 0, -1);

            BuildingDecor.Wing wing = BuildingDecor.WingFor(
                world.ChunkSeed(site.AnchorX, site.AnchorY),
                site.Params.Width, site.Params.Depth, site.Params.Height,
                westRoom, eastRoom, southRoom);
            if (!wing.Exists)
            {
                return false;
            }

            float centreX = (float)site.CentreX;
            float centreZ = (float)site.CentreZ;
            float halfX = site.Params.Width * 0.5f;
            float halfZ = site.Params.Depth * 0.5f;

            // Bite into the wall it grows from. Meeting it exactly leaves two
            // coincident faces, which z-fight along the whole join.
            const float Bite = 0.05f;

            float minX, maxX, minZ, maxZ;
            if (Mathf.Abs(wing.Normal.x) > 0.5f)
            {
                float face = centreX + wing.Normal.x * halfX;
                float outer = face + wing.Depth * wing.Normal.x;
                float inner = face - Bite * wing.Normal.x;
                minX = Mathf.Min(inner, outer);
                maxX = Mathf.Max(inner, outer);
                minZ = centreZ + wing.Along - wing.Width * 0.5f;
                maxZ = centreZ + wing.Along + wing.Width * 0.5f;
            }
            else
            {
                float face = centreZ + wing.Normal.z * halfZ;
                float outer = face + wing.Depth * wing.Normal.z;
                float inner = face - Bite * wing.Normal.z;
                minZ = Mathf.Min(inner, outer);
                maxZ = Mathf.Max(inner, outer);
                minX = centreX + wing.Along - wing.Width * 0.5f;
                maxX = centreX + wing.Along + wing.Width * 0.5f;
            }

            box = new Bounds();
            box.SetMinMax(
                new Vector3(minX, site.PodiumBase, minZ),
                new Vector3(maxX, site.BaseHeight + wing.Height, maxZ));
            return true;
        }

        /// <summary>
        /// Vertical concrete fins standing proud of the facade.
        ///
        /// FACADE MATERIAL, NOT CONCRETE, which is city_chunk.gd's own choice
        /// and worth stating because the opposite looks more obviously right.
        /// A fin's own faces are UV'd in metres like every other surface here,
        /// so a fin picks up the same strip of wall texture as the wall
        /// directly behind it -- it reads as a piece of the facade pulled
        /// forward, which is what a cast fin is. Putting them in the concrete
        /// buffer would give a cleaner fin and a visible seam where it meets
        /// the wall it grew out of.
        ///
        /// NO COLLISION IS NOT A DECISION HERE, unlike in Godot where it was:
        /// the chunk's own MeshCollider takes whatever triangles it is given.
        /// A fin is a thin ledge on a wall that is already solid behind it, so
        /// nothing becomes passable or impassable either way.
        /// </summary>
        private static void AddRibs(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer facade)
        {
            float halfX = site.Params.Width * 0.5f;
            float halfZ = site.Params.Depth * 0.5f;

            BuildingDecor.RibbedFacade ribbed = BuildingDecor.Ribs(
                world.ChunkSeed(site.AnchorX, site.AnchorY),
                site.Params.Width, site.Params.Depth, site.Params.Height,
                -BuildingInterior.DoorWidth * 0.5f, BuildingInterior.DoorWidth * 0.5f);
            if (ribbed.Fins.Count == 0)
            {
                return;
            }

            float centreX = (float)(site.CentreX - origin.x);
            float centreZ = (float)(site.CentreZ - origin.z);
            float y0 = site.BaseHeight - origin.y + ribbed.BaseY;
            float y1 = site.BaseHeight - origin.y + site.Params.Height;

            foreach (BuildingDecor.Rib rib in ribbed.Fins)
            {
                bool runsInZ = Mathf.Abs(rib.Normal.x) > 0.5f;
                Wall wall = runsInZ
                    ? new Wall(true, centreX + rib.Normal.x * halfX, rib.U0, rib.U1)
                    : new Wall(false, centreZ + rib.Normal.z * halfZ, rib.U0, rib.U1);
                float u0 = rib.U0 + (runsInZ ? centreZ : centreX);
                float u1 = rib.U1 + (runsInZ ? centreZ : centreX);

                AddFin(facade, wall, u0, u1, y0, y1, rib.Normal, ribbed.Depth,
                    runsInZ ? Vector3.forward : Vector3.right);
            }
        }

        /// <summary>
        /// One fin: a rectangular prism standing proud of its wall plane.
        ///
        /// FOUR FACES, NOT SIX, and this is Godot's own reasoning carried over
        /// rather than a shortcut. The back face is flush against the wall it
        /// stands on and the underside is only visible from directly below a
        /// fin that starts one storey up. A densely ribbed tower is a hundred
        /// fins, and this file's cost is already what dominates streaming.
        /// </summary>
        private static void AddFin(MeshBuffer buffer, in Wall wall, float u0, float u1,
            float y0, float y1, Vector3 normal, float depth, Vector3 along)
        {
            Vector3 out_ = normal * depth;

            Vector3 baseLow0 = wall.At(u0, y0);
            Vector3 baseLow1 = wall.At(u1, y0);
            Vector3 baseHigh0 = wall.At(u0, y1);
            Vector3 baseHigh1 = wall.At(u1, y1);

            AddFace(buffer, normal,
                baseLow0 + out_, baseLow1 + out_, baseHigh1 + out_, baseHigh0 + out_);
            AddFace(buffer, -along, baseLow0, baseLow0 + out_, baseHigh0 + out_, baseHigh0);
            AddFace(buffer, along, baseLow1, baseLow1 + out_, baseHigh1 + out_, baseHigh1);
            AddFace(buffer, Vector3.up, baseHigh0, baseHigh1, baseHigh1 + out_, baseHigh0 + out_);
        }

        /// <summary>
        /// Whatever is standing on this building's roof.
        ///
        /// city_chunk.gd has rolled a roof type per building since the start
        /// and CityChunkGenerator ports it -- it is generated, it is tested,
        /// and until now nothing read it, so every rooftop in the city was a
        /// bare plane. That is most visible from exactly the two angles the
        /// art direction cares about: the skyline, and standing on a roof.
        ///
        /// Sizes are Godot's own and are deliberately SMALL against a 20m
        /// roof. These are props, not massing: a water tank a person could
        /// stand beside, not a second storey.
        /// </summary>
        private static void AddRoofDetail(MeshBuffer buffer, string roof,
            float x0, float x1, float z0, float z1, float roofY)
        {
            float centreX = (x0 + x1) * 0.5f;
            float centreZ = (z0 + z1) * 0.5f;

            switch (roof)
            {
                case "shed":
                    AddCentredBox(buffer, centreX, centreZ, roofY, 1.1f, 0.6f, 1.1f);
                    break;

                case "antenna":
                    AddMast(buffer, centreX, centreZ, roofY, 2.0f);
                    break;

                case "chimney":
                    AddCentredBox(buffer, centreX, centreZ, roofY, 0.9f, 1.2f, 0.9f);
                    AddMast(buffer, centreX + 0.3f, centreZ, roofY + 1.2f, 1.5f);
                    break;

                case "parapet":
                    // A rim wall round all four edges. Part of the roofline
                    // rather than a prop sitting on it, which is why it takes
                    // the building's own footprint rather than a fixed size.
                    AddCentredBox(buffer, centreX, z0 + ParapetThickness * 0.5f, roofY,
                        x1 - x0, ParapetHeight, ParapetThickness);
                    AddCentredBox(buffer, centreX, z1 - ParapetThickness * 0.5f, roofY,
                        x1 - x0, ParapetHeight, ParapetThickness);
                    AddCentredBox(buffer, x0 + ParapetThickness * 0.5f, centreZ, roofY,
                        ParapetThickness, ParapetHeight, z1 - z0);
                    AddCentredBox(buffer, x1 - ParapetThickness * 0.5f, centreZ, roofY,
                        ParapetThickness, ParapetHeight, z1 - z0);
                    break;

                case "water_tower":
                    // Four thin legs and a tank, which is the silhouette that
                    // reads as a rooftop from a long way off.
                    foreach (float dx in new[] { -0.55f, 0.55f })
                    {
                        foreach (float dz in new[] { -0.55f, 0.55f })
                        {
                            AddCentredBox(buffer, centreX + dx, centreZ + dz, roofY,
                                0.1f, TankLegHeight, 0.1f);
                        }
                    }
                    AddCentredBox(buffer, centreX, centreZ, roofY + TankLegHeight,
                        TankRadius * 2.0f, TankHeight, TankRadius * 2.0f);
                    break;
            }
        }

        private const float ParapetHeight = 0.5f;
        private const float ParapetThickness = 0.15f;
        private const float TankLegHeight = 1.4f;
        private const float TankRadius = 0.9f;
        private const float TankHeight = 1.3f;

        /// <summary>A box standing ON a surface, given its centre in X and Z.</summary>
        private static void AddCentredBox(MeshBuffer buffer, float centreX, float centreZ,
            float baseY, float width, float height, float depth)
        {
            buffer.AddBox(
                new Vector3(centreX - width * 0.5f, baseY, centreZ - depth * 0.5f),
                new Vector3(centreX + width * 0.5f, baseY + height, centreZ + depth * 0.5f));
        }

        /// <summary>
        /// An aerial. A square post rather than Godot's tapered cylinder --
        /// at 6cm across it is under a pixel wide from anywhere you would see
        /// a skyline from, and a cylinder would cost sixteen times the
        /// triangles to say the same thing.
        /// </summary>
        private static void AddMast(MeshBuffer buffer, float centreX, float centreZ,
            float baseY, float height)
        {
            AddCentredBox(buffer, centreX, centreZ, baseY, 0.06f, height, 0.06f);
        }

        /// <summary>
        /// One storey below ground, reached down the hole in the lobby floor.
        ///
        /// DELIBERATELY WINDOWLESS, which is city_chunk.gd's own choice: it is
        /// underground, so it skips the facade treatment every storey above it
        /// gets and is four solid walls. Its own floor is solid too -- nothing
        /// goes below this -- and its ceiling is the lobby slab, which the
        /// caller has already opened for the flight.
        ///
        /// The stairwell is a FIXED corner rather than the rotating one every
        /// floor above uses. There is only one floor down here and nothing
        /// below it to meet, so there is nothing for a rotation to line up
        /// with.
        /// </summary>
        private static void BuildBasement(BuildingSite site, MeshBuffer concrete,
            float x0, float x1, float z0, float z1, float lobbyY, float storey,
            float centreX, float centreZ, BuildingInterior.Stairwell well, Rect hole, float run)
        {
            float floorY = lobbyY - storey;

            var west = new Wall(true, x0, z0, z1);
            var east = new Wall(true, x1, z0, z1);
            var south = new Wall(false, z0, x0, x1);
            var north = new Wall(false, z1, x0, x1);
            AddWallPanel(concrete, west, z0, z1, floorY, lobbyY, Vector3.right);
            AddWallPanel(concrete, east, z0, z1, floorY, lobbyY, Vector3.left);
            AddWallPanel(concrete, south, x0, x1, floorY, lobbyY, Vector3.forward);
            AddWallPanel(concrete, north, x0, x1, floorY, lobbyY, Vector3.back);

            AddSlabFace(concrete, x0, x1, z0, z1, Rect.zero, floorY, Vector3.up);

            // The underside of the lobby slab, seen from down here, and the
            // collar closing the opening's own edges.
            AddSlabFace(concrete, x0, x1, z0, z1, hole,
                lobbyY - BuildingInterior.SlabThickness, Vector3.down);
            AddHoleCollar(concrete, hole, lobbyY - BuildingInterior.SlabThickness, lobbyY);

            // The same flight the player walks DOWN to get here.
            foreach (BuildingInterior.Step step in
                     BuildingInterior.Steps(well, run, floorY, storey))
            {
                Vector3 half = step.Size * 0.5f;
                Vector3 centre = new Vector3(
                    step.Centre.x + centreX, step.Centre.y, step.Centre.z + centreZ);
                concrete.AddBox(centre - half, centre + half);
            }
        }

        /// <summary>
        /// The two walls that cut one floor's back corner into a separate room.
        ///
        /// BOTH SIDES OF EACH ARE REAL. Every other interior wall in a building
        /// has open air or solid podium behind it, so one face is enough. These
        /// two have walkable floor on both sides, and a partition you can see
        /// through from one side of a room is worse than no partition.
        /// </summary>
        private static void AddRoomPartition(MeshBuffer buffer, in BuildingInterior.Room room,
            float centreX, float centreZ, float floorY, float ceilingY, float doorHeight)
        {
            if (!room.Exists)
            {
                return;
            }

            float x0 = room.Plan.xMin + centreX;
            float x1 = room.Plan.xMax + centreX;
            float z0 = room.Plan.yMin + centreZ;
            float z1 = room.Plan.yMax + centreZ;
            float doorX = room.DoorWallX + centreX;
            float doorZ0 = room.DoorZ0 + centreZ;
            float doorZ1 = room.DoorZ1 + centreZ;

            var doorWall = new Wall(true, doorX, z0, z1);
            foreach (Vector3 normal in new[] { Vector3.right, Vector3.left })
            {
                AddWallAround(buffer, doorWall, floorY, ceilingY, normal,
                    doorZ0, doorZ1, floorY, floorY + doorHeight);
            }

            // The room's other inner side, solid: one doorway is enough for a
            // corner room this size.
            var backWall = new Wall(false, z1, x0, x1);
            foreach (Vector3 normal in new[] { Vector3.forward, Vector3.back })
            {
                AddWallPanel(buffer, backWall, x0, x1, floorY, ceilingY, normal);
            }
        }

        /// <summary>
        /// One axis-aligned wall plane and the mapping from "along the wall"
        /// to world, which is city_chunk.gd's own to_world callable. Wall
        /// geometry is written once against this rather than four times
        /// against hand-derived signs, which is where that file's own
        /// comments say the sign errors came from.
        /// </summary>
        private readonly struct Wall
        {
            /// <summary>True if the wall stands at a fixed X and runs in Z.</summary>
            public readonly bool RunsInZ;

            /// <summary>The wall's own X, or its own Z.</summary>
            public readonly float Plane;

            public readonly float U0;
            public readonly float U1;

            public Wall(bool runsInZ, float plane, float u0, float u1)
            {
                RunsInZ = runsInZ;
                Plane = plane;
                U0 = u0;
                U1 = u1;
            }

            public Vector3 At(float u, float y)
            {
                return RunsInZ ? new Vector3(Plane, y, u) : new Vector3(u, y, Plane);
            }
        }

        private static void AddWallPanel(MeshBuffer buffer, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal)
        {
            // A panel squeezed to nothing is normal here, not an error: a
            // doorway reaching a wall's edge leaves no strip beside it.
            if (u1 - u0 <= 0.0001f || y1 - y0 <= 0.0001f)
            {
                return;
            }
            AddFace(buffer, normal,
                wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1));
        }

        /// <summary>
        /// A wall with real openings cut through it, one per glazed cell.
        ///
        /// THE DECOMPOSITION IS THE WHOLE COST STORY. The obvious way to build
        /// this is four frame pieces around every cell, and that is what
        /// city_chunk.gd does -- but city_chunk.gd glazes every cell, so it has
        /// no blank concrete to waste quads on. Here 58% of cells are blank,
        /// and cell-at-a-time would have paid four quads each for nothing.
        ///
        /// Instead each ROW is cut into three horizontal bands. Every opening
        /// in a row starts and ends at the same two heights -- same margin
        /// ratio, same row height -- so the sill band below them and the header
        /// band above them each run the full width of the wall as ONE quad,
        /// however many windows the row has. Only the middle band is broken up,
        /// and there the blank cells between two windows merge into a single
        /// run. A twelve-by-twelve wall comes out at about eight quads a row
        /// rather than sixty.
        ///
        /// A <paramref name="glazing"/> with no buffers is the INWARD copy of
        /// the wall: same openings, opposite normal, and no glass, because the
        /// outward pass already hung it. A window has to be a hole through both
        /// or it is not a hole -- AND THE OPENING IS CUT WHATEVER STATE THE
        /// GLASS IS IN, so a shattered window leaves a hole rather than
        /// reinstating the wall it was cut from.
        /// </summary>
        private static void AddWindowedWall(MeshBuffer wallBuffer, in Glazing glazing,
            BuildingSite site, in Wall wall, float u0, float u1, float floorY, float topY,
            Vector3 normal, int seed, WindowGrid.Wall which, CellLayout rows,
            float doorU0, float doorU1, bool hasDoor)
        {
            CellLayout columns = WindowGrid.ColumnsFor(u1 - u0);
            float inset = WindowGrid.PaneInset;

            for (int row = 0; row < rows.Count; row++)
            {
                float y0 = floorY + row * rows.Size;
                float y1 = y0 + rows.Size;

                if (hasDoor && row == 0)
                {
                    AddDoorRow(wallBuffer, glazing, site, wall, u0, y0, y1, normal,
                        seed, which, columns, doorU0, doorU1);
                    continue;
                }

                float paneY0 = y0 + rows.Size * inset;
                float paneY1 = y1 - rows.Size * inset;

                AddWallPanel(wallBuffer, wall, u0, u1, y0, paneY0, normal);
                AddWallPanel(wallBuffer, wall, u0, u1, paneY1, y1, normal);

                float runStart = u0;
                for (int column = 0; column < columns.Count; column++)
                {
                    if (!WindowGrid.IsGlazed(seed, which, column, row))
                    {
                        continue;
                    }

                    float cellU0 = u0 + column * columns.Size;
                    float paneU0 = cellU0 + columns.Size * inset;
                    float paneU1 = cellU0 + columns.Size * (1.0f - inset);

                    AddWallPanel(wallBuffer, wall, runStart, paneU0, paneY0, paneY1, normal);
                    runStart = paneU1;

                    AddPane(glazing, site, wall, paneU0, paneU1, paneY0, paneY1, normal,
                        seed, which, column, row);
                }
                AddWallPanel(wallBuffer, wall, runStart, u1, paneY0, paneY1, normal);
            }
        }

        /// <summary>
        /// The entrance row: same grid, but a doorway the height of the whole
        /// row stands in it.
        ///
        /// NO BAND MERGING HERE, because the door reaches from the floor to the
        /// top of the row and cuts both the sill and the header band in two.
        /// One row per building pays cell-at-a-time; the eleven above it do not.
        ///
        /// A cell the door passes through keeps whatever is left of itself
        /// either side and gets no window, which is city_chunk.gd's own
        /// whole-cell exclusion. Narrowing the cells around the door instead --
        /// its earlier approach -- gives that row a different column grid from
        /// every row above it, and that shipped a visible seam there twice.
        /// </summary>
        private static void AddDoorRow(MeshBuffer wallBuffer, in Glazing glazing,
            BuildingSite site, in Wall wall, float u0, float y0, float y1, Vector3 normal,
            int seed, WindowGrid.Wall which, CellLayout columns, float doorU0, float doorU1)
        {
            float inset = WindowGrid.PaneInset;
            float paneY0 = y0 + (y1 - y0) * inset;
            float paneY1 = y1 - (y1 - y0) * inset;

            for (int column = 0; column < columns.Count; column++)
            {
                float cellU0 = u0 + column * columns.Size;
                float cellU1 = cellU0 + columns.Size;

                if (cellU1 > doorU0 && cellU0 < doorU1)
                {
                    // Whatever is left of this cell beside the opening.
                    AddWallPanel(wallBuffer, wall, cellU0, Mathf.Min(cellU1, doorU0), y0, y1, normal);
                    AddWallPanel(wallBuffer, wall, Mathf.Max(cellU0, doorU1), cellU1, y0, y1, normal);
                    continue;
                }

                if (!WindowGrid.IsGlazed(seed, which, column, 0))
                {
                    AddWallPanel(wallBuffer, wall, cellU0, cellU1, y0, y1, normal);
                    continue;
                }

                float paneU0 = cellU0 + columns.Size * inset;
                float paneU1 = cellU1 - columns.Size * inset;
                AddWallPanel(wallBuffer, wall, cellU0, paneU0, y0, y1, normal);
                AddWallPanel(wallBuffer, wall, paneU1, cellU1, y0, y1, normal);
                AddWallPanel(wallBuffer, wall, paneU0, paneU1, y0, paneY0, normal);
                AddWallPanel(wallBuffer, wall, paneU0, paneU1, paneY1, y1, normal);

                AddPane(glazing, site, wall, paneU0, paneU1, paneY0, paneY1, normal,
                    seed, which, column, 0);
            }
        }

        /// <summary>
        /// One sheet of glass, or none if somebody has already put a fist
        /// through it.
        ///
        /// THREE BUFFERS RATHER THAN A COLOUR PER PANE. Whether a window is lit
        /// and whether it is cracked both have to reach the shader somehow, and
        /// MeshBuffer has no colour channel -- adding one would put four bytes
        /// on every vertex in the city to say something about a few hundred of
        /// them. Submeshes cost nothing per vertex, and they let DayNightCycle
        /// bring every lit window up at dusk by touching one material rather
        /// than walking geometry.
        ///
        /// CRACKED GLASS IS THE ONE THAT MATTERS STRUCTURALLY. It has to still
        /// be drawn and NOT be collided with, because the first hit is what
        /// makes a window walkable. The chunk's collider is the render mesh, so
        /// the only way to have one without the other is to put it in a buffer
        /// the collider is not built from.
        /// </summary>
        private static void AddPane(in Glazing glazing, BuildingSite site, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal,
            int seed, WindowGrid.Wall which, int column, int row)
        {
            if (!glazing.HangsGlass)
            {
                return;
            }

            PaneState state = glazing.Damage == null
                ? PaneState.Intact
                : glazing.Damage.StateOf(
                    new PaneKey(site.AnchorX, site.AnchorY, which, column, row));
            if (state == PaneState.Shattered)
            {
                return;
            }

            MeshBuffer glass = state == PaneState.Cracked
                ? glazing.Cracked
                : WindowGrid.IsLit(seed, which, column, row) ? glazing.Lit : glazing.Dark;
            if (glass == null)
            {
                return;
            }

            AddFace(glass, normal,
                wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1));

            if (state == PaneState.Cracked && glazing.Openings != null)
            {
                Vector3 centre = (wall.At(u0, y0) + wall.At(u1, y1)) * 0.5f;
                Vector3 across = wall.At(u1, y0) - wall.At(u0, y0);
                var size = new Vector3(
                    Mathf.Max(Mathf.Abs(across.x), CrackedOpeningDepth),
                    y1 - y0,
                    Mathf.Max(Mathf.Abs(across.z), CrackedOpeningDepth));
                glazing.Openings.Add(new CrackedOpening(
                    new PaneKey(site.AnchorX, site.AnchorY, which, column, row), centre, size));
            }
        }

        /// <summary>A wall with a rectangular opening in it: strips each side, a sill and a header.</summary>
        private static void AddWallAround(MeshBuffer buffer, in Wall wall, float y0, float y1,
            Vector3 normal, float holeU0, float holeU1, float holeY0, float holeY1)
        {
            AddWallPanel(buffer, wall, wall.U0, holeU0, y0, y1, normal);
            AddWallPanel(buffer, wall, holeU1, wall.U1, y0, y1, normal);
            AddWallPanel(buffer, wall, holeU0, holeU1, y0, holeY0, normal);
            AddWallPanel(buffer, wall, holeU0, holeU1, holeY1, y1, normal);
        }

        /// <summary>
        /// A floor slab with real thickness and a stairwell hole through it.
        ///
        /// THE THICKNESS IS NOT DECORATION. Two coincident faces at one y let
        /// the staircase from the floor above bleed through what should read
        /// as a solid ceiling -- confirmed in a screenshot in the Godot build,
        /// not argued about -- so the underside sits a slab below the walkable
        /// top and a collar closes the gap around the opening.
        /// </summary>
        private static void AddSlab(MeshBuffer buffer, float x0, float x1, float z0, float z1,
            Rect hole, float topY)
        {
            float bottomY = topY - BuildingInterior.SlabThickness;
            AddSlabFace(buffer, x0, x1, z0, z1, hole, topY, Vector3.up);
            AddSlabFace(buffer, x0, x1, z0, z1, hole, bottomY, Vector3.down);
            AddHoleCollar(buffer, hole, bottomY, topY);
        }

        /// <summary>
        /// The vertical edges of a slab's opening, so it reads as a hole cut
        /// through solid material rather than a gap between two floating
        /// sheets. Each face points INTO the opening, which is the only side
        /// anyone ever sees it from -- the other faces solid slab.
        /// </summary>
        private static void AddHoleCollar(MeshBuffer buffer, Rect hole, float bottomY, float topY)
        {
            if (hole.width <= 0.0f || hole.height <= 0.0f)
            {
                return;
            }

            AddFace(buffer, Vector3.right,
                new Vector3(hole.xMin, bottomY, hole.yMin), new Vector3(hole.xMin, bottomY, hole.yMax),
                new Vector3(hole.xMin, topY, hole.yMax), new Vector3(hole.xMin, topY, hole.yMin));
            AddFace(buffer, Vector3.left,
                new Vector3(hole.xMax, bottomY, hole.yMin), new Vector3(hole.xMax, bottomY, hole.yMax),
                new Vector3(hole.xMax, topY, hole.yMax), new Vector3(hole.xMax, topY, hole.yMin));
            AddFace(buffer, Vector3.forward,
                new Vector3(hole.xMin, bottomY, hole.yMin), new Vector3(hole.xMax, bottomY, hole.yMin),
                new Vector3(hole.xMax, topY, hole.yMin), new Vector3(hole.xMin, topY, hole.yMin));
            AddFace(buffer, Vector3.back,
                new Vector3(hole.xMin, bottomY, hole.yMax), new Vector3(hole.xMax, bottomY, hole.yMax),
                new Vector3(hole.xMax, topY, hole.yMax), new Vector3(hole.xMin, topY, hole.yMax));
        }

        /// <summary>
        /// One face of a slab, as up to four strips around its opening. The
        /// Rect is in X and Z, so its own y is a z. Ported from
        /// _add_floor_with_hole: east and west take the slab's full depth and
        /// north and south fill only what is left between them, so no strip is
        /// ever emitted twice.
        /// </summary>
        private static void AddSlabFace(MeshBuffer buffer, float x0, float x1, float z0, float z1,
            Rect hole, float y, Vector3 normal)
        {
            float hx0 = Mathf.Clamp(hole.xMin, x0, x1);
            float hx1 = Mathf.Clamp(hole.xMax, x0, x1);
            float hz0 = Mathf.Clamp(hole.yMin, z0, z1);
            float hz1 = Mathf.Clamp(hole.yMax, z0, z1);

            if (hole.width <= 0.0f || hole.height <= 0.0f)
            {
                // No opening at all, so one quad rather than four strips.
                AddFace(buffer, normal, new Vector3(x0, y, z0), new Vector3(x1, y, z0),
                    new Vector3(x1, y, z1), new Vector3(x0, y, z1));
                return;
            }

            AddQuadIfReal(buffer, normal, x0, hx0, z0, z1, y);
            AddQuadIfReal(buffer, normal, hx1, x1, z0, z1, y);
            AddQuadIfReal(buffer, normal, hx0, hx1, z0, hz0, y);
            AddQuadIfReal(buffer, normal, hx0, hx1, hz1, z1, y);
        }

        private static void AddQuadIfReal(MeshBuffer buffer, Vector3 normal,
            float x0, float x1, float z0, float z1, float y)
        {
            if (x1 - x0 <= 0.0001f || z1 - z0 <= 0.0001f)
            {
                return;
            }
            AddFace(buffer, normal, new Vector3(x0, y, z0), new Vector3(x1, y, z0),
                new Vector3(x1, y, z1), new Vector3(x0, y, z1));
        }

        /// <summary>
        /// A quad UV'd in metres from the two world axes it actually lies in,
        /// so a floor slab and a stair riser read at the same scale as the
        /// street outside without anyone working out coordinates by hand.
        ///
        /// Corner order does not matter -- AddQuad winds to the normal.
        /// </summary>
        private static void AddFace(MeshBuffer buffer, Vector3 normal,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            buffer.AddQuad(a, b, c, d, normal,
                Uv(a, normal), Uv(b, normal), Uv(c, normal), Uv(d, normal));
        }

        private static Vector2 Uv(Vector3 v, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f)
            {
                return new Vector2(v.x, v.z);
            }
            return Mathf.Abs(normal.x) > 0.5f ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y);
        }

        private static void BuildBridges(CityWorld world, SkybridgeLayout bridges, BuildingSite site,
            Vector3 origin, MeshBuffer decks)
        {
            // Only east and north. Every pair has exactly one building that
            // sees it as east-or-north, so each walkway is built once -- the
            // west and south lookups exist for queries, not for geometry.
            BuildSpan(bridges, site, SkybridgeLayout.EastNeighbour(world, site), true, origin, decks);
            BuildSpan(bridges, site, SkybridgeLayout.NorthNeighbour(world, site), false, origin, decks);
        }

        private static void BuildSpan(SkybridgeLayout bridges, BuildingSite from, BuildingSite to,
            bool alongX, Vector3 origin, MeshBuffer decks)
        {
            if (!to.Exists)
            {
                return;
            }

            List<Skybridge> spans = bridges.BridgesBetween(from, to);
            if (spans.Count == 0)
            {
                return;
            }

            foreach (Skybridge bridge in spans)
            {
                float y = bridge.Height - origin.y;
                Vector3 min;
                Vector3 max;

                if (alongX)
                {
                    // Centred on the two buildings' shared middle in Z, which
                    // keeps the deck on the overlap rather than hanging off a
                    // corner when the neighbour is offset.
                    float centreZ = (float)((from.CentreZ + to.CentreZ) * 0.5 - origin.z);
                    min = new Vector3((float)(from.MaxX - origin.x), y - BridgeThickness,
                        centreZ - BridgeWidth * 0.5f);
                    max = new Vector3((float)(to.MinX - origin.x), y, centreZ + BridgeWidth * 0.5f);
                }
                else
                {
                    float centreX = (float)((from.CentreX + to.CentreX) * 0.5 - origin.x);
                    min = new Vector3(centreX - BridgeWidth * 0.5f, y - BridgeThickness,
                        (float)(from.MaxZ - origin.z));
                    max = new Vector3(centreX + BridgeWidth * 0.5f, y, (float)(to.MinZ - origin.z));
                }

                // A neighbour whose footprint starts before this one ends would
                // give an inside-out box, which renders as a hole rather than
                // an error.
                if (max.x <= min.x || max.z <= min.z)
                {
                    continue;
                }

                decks.AddBox(min, max, decks, includeBottom: true);
            }
        }
    }
}

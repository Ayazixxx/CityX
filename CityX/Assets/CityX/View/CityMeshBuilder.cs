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
        /// <summary>The depth of one tread of an entrance staircase, metres.</summary>
        public const float StreetStairTreadDepth = 0.32f;

        /// <summary>
        /// Builds one chunk: the ground (terrain, roads, pavement, paint, bridges -- see <see cref="CitySurfaceBuilder"/>) and every
        /// building whose centre is in it.
        ///
        /// A building is built in its OWN unturned frame, by the same generator that has always built them, and the buffers are given a
        /// frame (MeshBuffer.SetFrame) so everything written lands turned to face its street and moved to where it stands. That is why
        /// windows, glass, doors, stairs, rooms and roofs all survived the move from a square grid to rotated lots unchanged.
        ///
        /// Vertices are chunk-local (relative to <paramref name="origin"/>), so they stay small a long way from the origin.
        /// </summary>
        public static void BuildChunk(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            MeshBuffer ground, MeshBuffer flatConcrete, MeshBuffer structures,
            MeshBuffer litGlass = null, MeshBuffer darkGlass = null,
            MeshBuffer crackedGlass = null, GlassDamage damage = null,
            List<CrackedOpening> crackedOpenings = null, MeshBuffer markings = null,
            MeshBuffer guardRail = null, MeshBuffer laneDividers = null,
            MeshBuffer crackDecals = null, bool includeSurface = true, bool far = false)
        {
            // includeSurface false builds the BUILDINGS alone -- no terrain, road, pavement or paint. The title screen bakes a row of
            // real generated buildings that stand on a plane of its own.
            //
            // far builds the DISTANT version of the chunk: the same silhouette, ground and glass, but a building that is only its
            // shell (see BuildBuildingShell) and none of the small street furniture.
            if (includeSurface)
            {
                CitySurfaceBuilder.Build(world, chunkX, chunkY, origin, ground, flatConcrete, structures,
                    markings, laneDividers, guardRail, far);
            }

            List<int> owned = world.BuildingsOwnedBy(chunkX, chunkY);
            for (int i = 0; i < owned.Count; i++)
            {
                BuildSite(world, world.Building(owned[i]), origin, structures, flatConcrete, litGlass, darkGlass,
                    crackedGlass, damage, crackedOpenings, crackDecals, far);
            }
        }

        /// <summary>The yaw, in degrees, that turns a vector in a building's own frame into the world (Unity's Euler convention).</summary>
        public static float YawDegreesOf(BuildingSite site)
        {
            return Mathf.Atan2((float)-site.AxisZ, (float)site.AxisX) * Mathf.Rad2Deg;
        }

        /// <summary>The outward direction of one of a building's four walls, in the world.</summary>
        public static Vector3 WallNormal(BuildingSite site, WindowGrid.Wall wall)
        {
            return Quaternion.Euler(0.0f, YawDegreesOf(site), 0.0f) * WindowGrid.OutwardNormal(wall);
        }

        /// <summary>
        /// One building: built in its own frame into frame-carrying buffers. <paramref name="chunkOrigin"/> is the origin the buffers
        /// are relative to (the chunk's corner, or anything else the caller wants).
        /// </summary>
        public static void BuildSite(CityWorld world, BuildingSite site, Vector3 chunkOrigin,
            MeshBuffer structures, MeshBuffer concrete, MeshBuffer litGlass, MeshBuffer darkGlass,
            MeshBuffer crackedGlass, GlassDamage damage, List<CrackedOpening> crackedOpenings,
            MeshBuffer crackDecals, bool far)
        {
            if (!site.Exists)
            {
                return;
            }

            var offset = new Vector3((float)(site.CentreX - chunkOrigin.x), -chunkOrigin.y, (float)(site.CentreZ - chunkOrigin.z));
            structures.SetFrame(offset, site.AxisX, site.AxisZ);
            concrete.SetFrame(offset, site.AxisX, site.AxisZ);
            litGlass?.SetFrame(offset, site.AxisX, site.AxisZ);
            darkGlass?.SetFrame(offset, site.AxisX, site.AxisZ);
            crackedGlass?.SetFrame(offset, site.AxisX, site.AxisZ);
            crackDecals?.SetFrame(offset, site.AxisX, site.AxisZ);

            int firstOpening = crackedOpenings != null ? crackedOpenings.Count : 0;

            // The generator works in an unturned frame centred on the building, with real heights.
            var builderOrigin = new Vector3((float)site.CentreX, 0.0f, (float)site.CentreZ);
            double litShare = WindowGrid.LitShareFor(site.Zone);

            if (far)
            {
                BuildBuildingShell(world, site, builderOrigin, structures, concrete,
                    new Glazing(litGlass, darkGlass, null, damage, null, null, litShare));
            }
            else
            {
                BuildBuilding(world, site, builderOrigin, structures, concrete,
                    new Glazing(litGlass, darkGlass, crackedGlass, damage, crackedOpenings, crackDecals, litShare));
                AddBalconies(world, site, builderOrigin, concrete);
                AddRibs(world, site, builderOrigin, structures);
            }

            // A cracked opening's trigger box was written in the building's frame; hand it out turned and placed.
            if (crackedOpenings != null && crackedOpenings.Count > firstOpening)
            {
                Quaternion rotation = Quaternion.Euler(0.0f, YawDegreesOf(site), 0.0f);
                for (int i = firstOpening; i < crackedOpenings.Count; i++)
                {
                    CrackedOpening o = crackedOpenings[i];
                    crackedOpenings[i] = new CrackedOpening(o.Key, structures.FramePoint(o.Centre), o.Size, rotation);
                }
            }

            structures.ClearFrame();
            concrete.ClearFrame();
            litGlass?.ClearFrame();
            darkGlass?.ClearFrame();
            crackedGlass?.ClearFrame();
            crackDecals?.ClearFrame();
        }

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

            /// <summary>
            /// The real crack-texture decal quad a known impact point gets,
            /// on top of the base Cracked glass -- see AddCrackDecal. Optional
            /// (defaults null) so every existing caller that only cares about
            /// the state machine, not the visual, keeps compiling unchanged.
            /// </summary>
            public readonly MeshBuffer Decals;

            /// <summary>Fraction of glazed cells with a light on -- the building's zone decides it (WindowGrid.LitShareFor).</summary>
            public readonly double LitShare;

            public Glazing(MeshBuffer lit, MeshBuffer dark, MeshBuffer cracked, GlassDamage damage,
                List<CrackedOpening> openings, MeshBuffer decals = null,
                double litShare = WindowGrid.LitShare)
            {
                Lit = lit;
                Dark = dark;
                Cracked = cracked;
                Damage = damage;
                Openings = openings;
                Decals = decals;
                LitShare = litShare;
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

            /// <summary>How the box is turned: the building it belongs to is turned to face its street.</summary>
            public readonly Quaternion Rotation;

            public CrackedOpening(PaneKey key, Vector3 centre, Vector3 size)
                : this(key, centre, size, Quaternion.identity)
            {
            }

            public CrackedOpening(PaneKey key, Vector3 centre, Vector3 size, Quaternion rotation)
            {
                Key = key;
                Centre = centre;
                Size = size;
                Rotation = rotation;
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

            // EXPLICIT ASK: "the windows/entrances should never be cut off
            // always have like a stairway entrance." site.BaseHeight (what
            // the door stands on) is the HIGHEST pad the WHOLE footprint
            // covers (see PadRangeForSpan's own doc comment: "put it at the
            // lowest and the uphill corner is buried"), but a multi-chunk
            // building's door sits on one specific ROW of that footprint --
            // the one at MaxZ -- which is routinely NOT the row that set
            // BaseHeight. Comparing against the door's own row specifically
            // (not the whole footprint's PodiumBase, which could be a
            // completely different, uninvolved corner) is what tells a door
            // that is actually already flush apart from one that is
            // genuinely left floating over the real drop underneath it.
            // The ground just outside the door decides whether the entrance needs steps (see BuildingDecor.EntranceStyleFor).
            BuildingDecor.EntranceStyle entrance =
                BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight);
            if (entrance.NeedsStairs)
            {
                AddEntranceStairs(concrete, new Vector3(centreX, lobbyY, z1), entrance.Rise);
            }

            var west = new Wall(true, x0, z0, z1);
            var east = new Wall(true, x1, z0, z1);
            var south = new Wall(false, z0, x0, x1);
            var north = new Wall(false, z1, x0, x1);

            WindowPlan plan = WindowPlan.For(world, site);
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
                Vector3.left, plan, WindowGrid.Wall.West, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, east, z0, z1, lobbyY, roofY,
                Vector3.right, plan, WindowGrid.Wall.East, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, south, x0, x1, lobbyY, roofY,
                Vector3.back, plan, WindowGrid.Wall.South, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, north, x0, x1, lobbyY, roofY,
                Vector3.forward, plan, WindowGrid.Wall.North, rows, doorU0, doorU1, true);

            // Inward, for the room. Same planes and the same openings, opposite
            // normals -- back to back, so neither is ever seen from the wrong
            // side, and a window is a hole through BOTH or it is not a hole.
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, west, z0, z1, lobbyY, roofY,
                Vector3.right, plan, WindowGrid.Wall.West, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, east, z0, z1, lobbyY, roofY,
                Vector3.left, plan, WindowGrid.Wall.East, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, south, x0, x1, lobbyY, roofY,
                Vector3.forward, plan, WindowGrid.Wall.South, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, north, x0, x1, lobbyY, roofY,
                Vector3.back, plan, WindowGrid.Wall.North, rows, doorU0, doorU1, true);

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
        /// How far in front of the wall a distant window's glass floats. Big enough not to z-fight the wall at
        /// the distances it is seen from, small enough that no gap shows at a glancing angle.
        /// </summary>
        public const float ShellGlassLift = 0.06f;

        /// <summary>
        /// The DISTANT version of a building: its outside and nothing else.
        ///
        /// One panel per wall from the podium to the roof, the roof, and the glass laid over the wall as flat
        /// strips -- no openings cut through the wall, no back faces, no floors, stairs, rooms or inward copy of
        /// the walls. From beyond a few chunks the eye cannot tell, and about half of a downtown chunk's
        /// triangles were exactly those hidden rooms. It is still a closed solid, so the collider built from
        /// it keeps a fast flyer out of a building whose full-detail chunk has not streamed in yet.
        ///
        /// Neighbouring glazed cells that light the same way merge into ONE strip (the mullions between them
        /// are a pixel at this range), so a glass tower's row is a quad or two, not a quad per window.
        /// </summary>
        private static void BuildBuildingShell(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer structures, MeshBuffer concrete, Glazing glazing)
        {
            float x0 = (float)(site.MinX - origin.x);
            float x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z);
            float z1 = (float)(site.MaxZ - origin.z);
            float podiumY = site.PodiumBase - origin.y;
            float lobbyY = site.BaseHeight - origin.y;
            float height = site.Params.Height;
            float roofY = lobbyY + height;

            var west = new Wall(true, x0, z0, z1);
            var east = new Wall(true, x1, z0, z1);
            var south = new Wall(false, z0, x0, x1);
            var north = new Wall(false, z1, x0, x1);

            // The podium below the lobby floor, then the whole height of the wall in one panel each.
            AddWallPanel(structures, west, z0, z1, podiumY, roofY, Vector3.left);
            AddWallPanel(structures, east, z0, z1, podiumY, roofY, Vector3.right);
            AddWallPanel(structures, south, x0, x1, podiumY, roofY, Vector3.back);
            AddWallPanel(structures, north, x0, x1, podiumY, roofY, Vector3.forward);

            // The roof, and the antennas and parapets on it: they are the skyline.
            AddSlabFace(concrete, x0, x1, z0, z1, Rect.zero, roofY, Vector3.up);
            AddRoofDetail(concrete, site.Params.Roof, x0, x1, z0, z1, roofY);

            if (!glazing.HangsGlass)
            {
                return;
            }

            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(height);
            float centreX = (x0 + x1) * 0.5f;
            float doorU0 = centreX - BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = centreX + BuildingInterior.DoorWidth * 0.5f;

            AddShellGlass(glazing, site, west, z0, z1, lobbyY, Vector3.left, plan, WindowGrid.Wall.West, rows, 0.0f, 0.0f);
            AddShellGlass(glazing, site, east, z0, z1, lobbyY, Vector3.right, plan, WindowGrid.Wall.East, rows, 0.0f, 0.0f);
            AddShellGlass(glazing, site, south, x0, x1, lobbyY, Vector3.back, plan, WindowGrid.Wall.South, rows, 0.0f, 0.0f);
            AddShellGlass(glazing, site, north, x0, x1, lobbyY, Vector3.forward, plan, WindowGrid.Wall.North, rows, doorU0, doorU1);
        }

        /// <summary>One wall's glass for <see cref="BuildBuildingShell"/>: flat one-sided strips floating just off the wall.</summary>
        private static void AddShellGlass(in Glazing glazing, BuildingSite site, in Wall wall,
            float u0, float u1, float floorY, Vector3 normal, in WindowPlan plan, WindowGrid.Wall which,
            CellLayout rows, float doorU0, float doorU1)
        {
            CellLayout columns = plan.ColumnsFor(u1 - u0);
            float inset = WindowGrid.PaneInset;
            Vector3 lift = normal * ShellGlassLift;
            bool doorWall = doorU1 > doorU0;

            for (int row = 0; row < rows.Count; row++)
            {
                float y0 = floorY + row * rows.Size;
                float y1 = y0 + rows.Size;
                plan.Insets(WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height), out float insetBottom, out float insetTop);
                if (doorWall && row == 0)
                {
                    insetBottom = WindowGrid.PaneInsetVerticalClear;
                    insetTop = WindowGrid.PaneInsetVerticalClear;
                }
                float paneY0 = y0 + rows.Size * insetBottom;
                float paneY1 = y1 - rows.Size * insetTop;

                // The strip being grown: where it starts, where it ends, and which way its light is on.
                bool open = false;
                float runU0 = 0.0f;
                float runU1 = 0.0f;
                bool runLit = false;

                for (int column = 0; column < columns.Count; column++)
                {
                    float cellU0 = u0 + column * columns.Size;
                    float paneU0 = cellU0 + columns.Size * inset;
                    float paneU1 = cellU0 + columns.Size * (1.0f - inset);

                    bool glazed = plan.IsGlazed(which, column, row);
                    if (glazed && doorWall && row == 0 && cellU0 + columns.Size > doorU0 && cellU0 < doorU1)
                    {
                        glazed = false;   // the doorway
                    }
                    if (glazed)
                    {
                        var key = new PaneKey(site.Id, which, column, row);
                        if (glazing.Damage != null && glazing.Damage.StateOf(key) == PaneState.Shattered)
                        {
                            glazed = false;
                        }
                    }

                    bool lit = glazed && plan.IsLit(which, column, row, glazing.LitShare);
                    if (glazed && open && lit == runLit)
                    {
                        runU1 = paneU1;   // the next window of the same kind: the strip just gets longer
                        continue;
                    }

                    if (open)
                    {
                        AddShellStrip(runLit ? glazing.Lit : glazing.Dark, wall, runU0, runU1, paneY0, paneY1, normal, lift);
                        open = false;
                    }
                    if (glazed)
                    {
                        open = true;
                        runU0 = paneU0;
                        runU1 = paneU1;
                        runLit = lit;
                    }
                }
                if (open)
                {
                    AddShellStrip(runLit ? glazing.Lit : glazing.Dark, wall, runU0, runU1, paneY0, paneY1, normal, lift);
                }
            }
        }

        private static void AddShellStrip(MeshBuffer glass, in Wall wall, float u0, float u1, float y0, float y1,
            Vector3 normal, Vector3 lift)
        {
            if (glass == null || u1 - u0 <= 0.0001f || y1 - y0 <= 0.0001f)
            {
                return;
            }
            AddFace(glass, normal, wall.At(u0, y0) + lift, wall.At(u1, y0) + lift, wall.At(u1, y1) + lift, wall.At(u0, y1) + lift);
        }

        /// <summary>
        /// A run of real exterior steps from the ground outside a building's
        /// door up to the door's own threshold -- see
        /// BuildingDecor.EntranceStyleFor's own doc comment for the bug this
        /// fixes. Reuses the exact same BuildingInterior.Steps machinery a
        /// street's own terrace stairs already build from (see
        /// AddStreetEdgeFeatures) -- <paramref name="doorThreshold"/> is the
        /// TOP of the flight (Vector3.forward is the door's own outward
        /// facing, Vector3.right its own width axis, matching
        /// BuildingDoor.Panel.Facing), climbing down and outward by
        /// <paramref name="rise"/> to the real ground below it.
        /// </summary>
        private static void AddEntranceStairs(MeshBuffer concrete, Vector3 doorThreshold, float rise)
        {
            float run = BuildingInterior.StepsFor(rise) * StreetStairTreadDepth;
            float floorY = doorThreshold.y - rise;
            var well = new BuildingInterior.Stairwell(doorThreshold, Vector3.forward, Vector3.right, default);
            foreach (BuildingInterior.Step step in BuildingInterior.Steps(well, run, floorY, rise))
            {
                Vector3 half = step.Size * 0.5f;
                concrete.AddBox(step.Centre - half, step.Centre + half);
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
                site.Seed,
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
                site.Seed,
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
        /// AddWallPanel's own shape, but double-sided (see AddDoubleSidedFace's
        /// own doc comment) -- used ONLY for the glass frame strips
        /// AddPaneWithHole cuts around a bullet hole, so a cracked pane can
        /// still be finished off (shattered) by a second shot from inside
        /// the same way an intact one can now be cracked from inside.
        /// </summary>
        private static void AddDoubleSidedWallPanel(MeshBuffer buffer, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal)
        {
            if (u1 - u0 <= 0.0001f || y1 - y0 <= 0.0001f)
            {
                return;
            }
            AddDoubleSidedFace(buffer, normal,
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
            Vector3 normal, in WindowPlan plan, WindowGrid.Wall which, CellLayout rows,
            float doorU0, float doorU1, bool hasDoor)
        {
            CellLayout columns = plan.ColumnsFor(u1 - u0);
            float inset = WindowGrid.PaneInset;

            for (int row = 0; row < rows.Count; row++)
            {
                float y0 = floorY + row * rows.Size;
                float y1 = y0 + rows.Size;

                if (hasDoor && row == 0)
                {
                    AddDoorRow(wallBuffer, glazing, site, wall, u0, y0, y1, normal,
                        plan, which, columns, doorU0, doorU1);
                    continue;
                }

                // A row with no window in it (a blank party wall, the solid band between two ribbons, a
                // warehouse below its clerestory) is ONE panel, not the sill, lintel and infill of three.
                bool anyWindow = false;
                for (int column = 0; column < columns.Count && !anyWindow; column++)
                {
                    anyWindow = plan.IsGlazed(which, column, row);
                }
                if (!anyWindow)
                {
                    AddWallPanel(wallBuffer, wall, u0, u1, y0, y1, normal);
                    continue;
                }

                // ASYMMETRIC ON PURPOSE: AddSlab hangs a floor slab downward
                // from its own boundary, so a slab only ever eats into the
                // TOP of the row directly below it -- a row's OWN bottom
                // edge never has one, whatever the row index, because that
                // material is entirely on the far side of the boundary
                // below. Only the top edge needs checking. See
                // WindowGrid.TopEdgeHasSlab's own comment for why that is a
                // real per-building check and not a row-parity guess.
                plan.Insets(WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height),
                    out float insetBottom, out float insetTop);
                float paneY0 = y0 + rows.Size * insetBottom;
                float paneY1 = y1 - rows.Size * insetTop;

                AddWallPanel(wallBuffer, wall, u0, u1, y0, paneY0, normal);
                AddWallPanel(wallBuffer, wall, u0, u1, paneY1, y1, normal);

                float runStart = u0;
                for (int column = 0; column < columns.Count; column++)
                {
                    if (!plan.IsGlazed(which, column, row))
                    {
                        continue;
                    }

                    float cellU0 = u0 + column * columns.Size;
                    float paneU0 = cellU0 + columns.Size * inset;
                    float paneU1 = cellU0 + columns.Size * (1.0f - inset);

                    AddWallPanel(wallBuffer, wall, runStart, paneU0, paneY0, paneY1, normal);
                    runStart = paneU1;

                    AddPane(glazing, site, wall, paneU0, paneU1, paneY0, paneY1, normal,
                        plan, which, column, row);
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
            in WindowPlan plan, WindowGrid.Wall which, CellLayout columns, float doorU0, float doorU1)
        {
            float inset = WindowGrid.PaneInset;
            // The door row is always row 0, whose own top boundary (1) is
            // never a slab location (see WindowGrid.TopEdgeHasSlab) -- the
            // small clear margin is correct on both edges here, not just a
            // simplification.
            float paneY0 = y0 + (y1 - y0) * WindowGrid.PaneInsetVerticalClear;
            float paneY1 = y1 - (y1 - y0) * WindowGrid.PaneInsetVerticalClear;

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

                if (!plan.IsGlazed(which, column, 0))
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
                    plan, which, column, 0);
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
            in WindowPlan plan, WindowGrid.Wall which, int column, int row)
        {
            if (!glazing.HangsGlass)
            {
                return;
            }

            var key = new PaneKey(site.Id, which, column, row);
            PaneState state = glazing.Damage == null ? PaneState.Intact : glazing.Damage.StateOf(key);
            if (state == PaneState.Shattered)
            {
                return;
            }

            MeshBuffer glass = state == PaneState.Cracked
                ? glazing.Cracked
                : plan.IsLit(which, column, row, glazing.LitShare) ? glazing.Lit : glazing.Dark;
            if (glass == null)
            {
                return;
            }

            // A real hole where the blow that cracked it landed, EXPLICIT
            // ASK: "when you shoot the glass make it shatter but also have a
            // hole in it where you shot." Only cracked panes carry an
            // impact point at all (a fresh Intact pane was never hit), and
            // only cracked panes are still drawn (Shattered returned above),
            // so this is the only branch that ever needs one.
            if (state == PaneState.Cracked && glazing.Damage != null &&
                glazing.Damage.TryGetImpacts(key, out Vector2[] impacts))
            {
                // EVERY bullet's hole, each with its own crack pattern round it.
                var centres = new Vector2[impacts.Length];
                for (int i = 0; i < impacts.Length; i++)
                {
                    centres[i] = ClampedHoleCentre(u0, u1, y0, y1, impacts[i]);
                }
                AddPaneWithHoles(glass, wall, u0, u1, y0, y1, normal, centres);
                if (glazing.Decals != null)
                {
                    foreach (Vector2 centre in centres)
                    {
                        AddCrackDecal(glazing.Decals, wall, centre, normal);
                    }
                }
            }
            else
            {
                AddDoubleSidedFace(glass, normal,
                    wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1));
            }

            if (state == PaneState.Cracked && glazing.Openings != null)
            {
                Vector3 centre = (wall.At(u0, y0) + wall.At(u1, y1)) * 0.5f;
                Vector3 across = wall.At(u1, y0) - wall.At(u0, y0);
                var size = new Vector3(
                    Mathf.Max(Mathf.Abs(across.x), CrackedOpeningDepth),
                    y1 - y0,
                    Mathf.Max(Mathf.Abs(across.z), CrackedOpeningDepth));
                glazing.Openings.Add(new CrackedOpening(key, centre, size));
            }
        }

        /// <summary>
        /// The crack-texture decal's own size in metres -- EXPLICIT ASK:
        /// "shrink them to size," a real bullet-hole scale rather than the
        /// old 18cm square that read as oversized. Aspect matches
        /// BulletHoleCrack.png's own 1024x856 ratio so the image doesn't
        /// stretch.
        /// </summary>
        private const float BulletHoleDecalWidth = 0.34f;
        private const float BulletHoleDecalHeight = 0.28f;

        /// <summary>
        /// How far the decal sits proud of the base glass plane -- both are
        /// alpha-blended and coplanar otherwise, which is exactly the
        /// condition that z-fights.
        /// </summary>
        private const float BulletHoleDecalOffset = 0.003f;

        /// <summary>
        /// The REAL geometric opening at the decal's own centre, sized as a fraction of the decal itself.
        ///
        /// DELIBERATELY SMALLER THAN THE TEXTURE'S OWN TRANSPARENT CENTRE (a measured alpha=0 run 149px wide by
        /// 141px tall in a 1024x856 image; this is 60% of it) -- EXPLICIT BUG REPORT: "the bullet holes have a
        /// visible square around them due to the image i used." Sizing the real cut to match the transparent
        /// run EXACTLY left nothing standing between the hard-edged rectangular hole and whatever is really
        /// behind the glass (the building interior, at night far darker or far flatter than the frosted glass
        /// around it) -- there was no margin left for the decal's own dense crack shards, which start growing
        /// in only a little further out, to actually cover the cut's own straight edges and corners. Shrinking
        /// the real hole leaves a ring of ordinary frosted (Cracked) glass between the cut and the decal's own
        /// transparent centre -- itself still shown through the decal's transparency, so nothing new is hidden,
        /// but now it is glass being shown, not whatever is on the other side of it.
        /// </summary>
        private const float BulletHoleWidthFraction = 149.0f / 1024.0f * 0.6f;
        private const float BulletHoleHeightFraction = 141.0f / 856.0f * 0.6f;
        private const float BulletHoleHalfWidth = BulletHoleDecalWidth * BulletHoleWidthFraction * 0.5f;
        private const float BulletHoleHalfHeight = BulletHoleDecalHeight * BulletHoleHeightFraction * 0.5f;

        /// <summary>
        /// Where a hole actually lands within its own pane, clamped so it
        /// never pokes out past the pane's own edge -- shared between
        /// AddPaneWithHole (the real geometric opening) and AddCrackDecal
        /// (the crack texture sitting on top of it), so the two always
        /// agree on centre even when an impact near a pane's edge gets
        /// pulled inward.
        /// </summary>
        private static Vector2 ClampedHoleCentre(float u0, float u1, float y0, float y1, Vector2 impactUV)
        {
            float halfW = Mathf.Min(BulletHoleHalfWidth, (u1 - u0) * 0.4f);
            float halfH = Mathf.Min(BulletHoleHalfHeight, (y1 - y0) * 0.4f);
            float centreU = Mathf.Clamp(Mathf.Lerp(u0, u1, impactUV.x), u0 + halfW, u1 - halfW);
            float centreY = Mathf.Clamp(Mathf.Lerp(y0, y1, impactUV.y), y0 + halfH, y1 - halfH);
            return new Vector2(centreU, centreY);
        }

        /// <summary>
        /// A cracked pane with a real opening punched through it at
        /// `centre` -- four strips framing a hole, the exact same technique
        /// AddWallAround already uses to cut a window's own opening into
        /// the wall around it, just nested one level down into the pane
        /// itself.
        ///
        /// STILL THE SAME `glass` (Cracked) BUFFER for the frame, so the
        /// rest of the pane keeps reading as broken/frosted glass around a
        /// hole rather than clean glass with a hole in it. THE WALKTHROUGH
        /// TRIGGER STAYS WHOLE-PANE, deliberately -- shrinking it to just
        /// this hole is a bigger, separate change to CrackedOpening/
        /// CrackedPane that nothing here asked for.
        /// </summary>
        private static void AddPaneWithHole(MeshBuffer glass, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal, Vector2 centre)
        {
            AddPaneWithHoles(glass, wall, u0, u1, y0, y1, normal, new[] { centre });
        }

        /// <summary>
        /// A pane with a real opening at EACH of <paramref name="centres"/> -- a window somebody has been
        /// shooting at.
        ///
        /// The pane is cut into vertical strips at every hole's left and right edge, and each strip is filled
        /// EXCEPT where holes are: within a strip the holes that cross it are merged into runs and the glass is
        /// the gaps between them. That handles any number of holes, holes that overlap (a second bullet in the
        /// same spot) and holes side by side, with no quad emitted twice. One hole comes out as exactly the
        /// four strips it always was.
        /// </summary>
        private static void AddPaneWithHoles(MeshBuffer glass, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal, Vector2[] centres)
        {
            float halfW = Mathf.Min(BulletHoleHalfWidth, (u1 - u0) * 0.4f);
            float halfH = Mathf.Min(BulletHoleHalfHeight, (y1 - y0) * 0.4f);

            int count = centres.Length;
            var holes = new Rect[count];
            var edges = new List<float>(count * 2 + 2) { u0, u1 };
            for (int i = 0; i < count; i++)
            {
                holes[i] = Rect.MinMaxRect(centres[i].x - halfW, centres[i].y - halfH, centres[i].x + halfW, centres[i].y + halfH);
                edges.Add(holes[i].xMin);
                edges.Add(holes[i].xMax);
            }
            edges.Sort();

            var spans = new List<Vector2>(count);
            for (int e = 0; e + 1 < edges.Count; e++)
            {
                float left = edges[e];
                float right = edges[e + 1];
                if (right - left <= 0.0001f)
                {
                    continue;
                }
                float middle = (left + right) * 0.5f;

                // The vertical spans of this strip that are hole.
                spans.Clear();
                for (int i = 0; i < count; i++)
                {
                    if (middle > holes[i].xMin && middle < holes[i].xMax)
                    {
                        spans.Add(new Vector2(holes[i].yMin, holes[i].yMax));
                    }
                }
                spans.Sort((a, b) => a.x.CompareTo(b.x));

                // Glass in the gaps between them.
                float cursor = y0;
                foreach (Vector2 span in spans)
                {
                    if (span.x > cursor)
                    {
                        AddDoubleSidedWallPanel(glass, wall, left, right, cursor, span.x, normal);
                    }
                    cursor = Mathf.Max(cursor, span.y);
                }
                if (cursor < y1)
                {
                    AddDoubleSidedWallPanel(glass, wall, left, right, cursor, y1, normal);
                }
            }
        }

        /// <summary>
        /// The real crack-texture decal (BulletHoleCrack.png, via
        /// CrackDecalMaterial) sitting on top of the base glass, centred on
        /// the same `centre` AddPaneWithHole just cut its own real opening
        /// at -- EXPLICIT ASK: replacing the old plain square hole with the
        /// user's own reference image, "radial cracks with a real see-
        /// through hole in the middle" instead of a bare geometric notch.
        /// One quad, explicit 0..1 UVs (not MeshBuffer's usual metre-scale
        /// planar projection -- this has to be the WHOLE image across the
        /// WHOLE quad, not tiled), offset slightly along the wall's own
        /// normal so its alpha blending never z-fights the coplanar Cracked
        /// quad behind it.
        /// </summary>
        private static void AddCrackDecal(MeshBuffer decals, in Wall wall, Vector2 centre, Vector3 normal)
        {
            float halfW = BulletHoleDecalWidth * 0.5f;
            float halfH = BulletHoleDecalHeight * 0.5f;
            Vector3 offset = normal * BulletHoleDecalOffset;

            Vector3 a = wall.At(centre.x - halfW, centre.y - halfH) + offset;
            Vector3 b = wall.At(centre.x + halfW, centre.y - halfH) + offset;
            Vector3 c = wall.At(centre.x + halfW, centre.y + halfH) + offset;
            Vector3 d = wall.At(centre.x - halfW, centre.y + halfH) + offset;

            decals.AddQuad(a, b, c, d, normal,
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f),
                new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f));
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

        /// <summary>
        /// The same quad, plus a second copy wound the other way so it also
        /// faces (and renders/collides) from behind -- EXPLICIT BUG REPORT:
        /// "when shooting windows from inside the building the are not
        /// breaking." Every other wall surface in the city is genuinely
        /// solid (the interior gets its own separate inward-facing pass, see
        /// BuildBuilding's own structures/darkGlass/litGlass split), but
        /// glass is a single, one-sided quad at the outward facade with
        /// nothing behind it -- confirmed directly: the whole city (ground,
        /// walls, glass) shares ONE non-convex MeshCollider
        /// (CityStreamer.cs), and this project's own DynamicsManager.asset
        /// has queriesHitBackfaces off (Unity's own default), so
        /// Physics.Raycast against a triangle's back face silently reports
        /// no hit at all. A shot from outside hits the pane's front face and
        /// works; the identical shot from inside hits its back face and is
        /// invisible to PhysX -- exactly the reported asymmetry.
        /// GlassDamage.TryPaneAt itself is already direction-agnostic (see
        /// its own doc comment), so making the RAYCAST able to land from
        /// either side is the whole fix; nothing downstream needs to change.
        /// Only used for glass -- ordinary walls already have their own
        /// separate interior pass and doubling THEIR panel strips too would
        /// just add redundant, potentially z-fighting geometry they don't
        /// need.
        /// </summary>
        private static void AddDoubleSidedFace(MeshBuffer buffer, Vector3 normal,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // Same corners both times, on purpose -- MeshBuffer.AddQuad's own
            // doc comment: winding is corrected FROM the normal, not trusted
            // from vertex order, so simply negating the normal (rather than
            // also reversing a/b/c/d) is already enough to wind the back
            // copy correctly.
            AddFace(buffer, normal, a, b, c, d);
            AddFace(buffer, -normal, a, b, c, d);
        }

        private static Vector2 Uv(Vector3 v, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f)
            {
                return new Vector2(v.x, v.z);
            }
            return Mathf.Abs(normal.x) > 0.5f ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y);
        }

    }
}

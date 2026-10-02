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

            // What the building is made of: one look for its walls, one for its ground floor, one for its roof and trim.
            SiteLooks looks = SiteLooks.For(site, WindowPlan.For(world, site).Style);
            structures.UvOffset = looks.UvOffset;

            if (far)
            {
                BuildBuildingShell(world, site, builderOrigin, structures, concrete,
                    new Glazing(litGlass, darkGlass, null, damage, null, null, litShare), looks);
            }
            else
            {
                BuildBuilding(world, site, builderOrigin, structures, concrete,
                    new Glazing(litGlass, darkGlass, crackedGlass, damage, crackedOpenings, crackDecals, litShare), looks);
                AddBalconies(world, site, builderOrigin, structures, looks);
                AddRibs(world, site, builderOrigin, structures, looks);
            }
            structures.Look = SurfaceLook.Default;
            structures.UvOffset = Vector2.zero;
            if (litGlass != null)
            {
                litGlass.Look = SurfaceLook.Default;
            }
            if (darkGlass != null)
            {
                darkGlass.Look = SurfaceLook.Default;
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
        /// One floor's stairwell. The top floor's flight comes out on the roof; where another volume of the building stands on that roof it
        /// must not come out under it.
        /// </summary>
        private static BuildingInterior.Stairwell WellFor(BuildingSite site, int floor, int floors, float halfX, float halfZ, float run)
        {
            if (floor == floors - 1 && site.HasRoofObstacle)
            {
                return BuildingInterior.StairwellFor(floor, halfX, halfZ, run, Rect.MinMaxRect(
                    site.ObstacleX0 - 0.4f, site.ObstacleZ0 - 0.4f, site.ObstacleX1 + 0.4f, site.ObstacleZ1 + 0.4f));
            }
            return BuildingInterior.StairwellFor(floor, halfX, halfZ, run);
        }

        /// <summary>
        /// The packed vertex looks (see SurfaceLook) a building's parts are stamped with: its walls, its ground floor and podium, its roof,
        /// and its trim. Computed once per building from BuildingLook.
        /// </summary>
        private readonly struct SiteLooks
        {
            public readonly Color32 Wall;
            public readonly Color32 Base;
            public readonly Color32 Roof;
            public readonly Color32 Trim;
            public readonly Color32 Frame;
            public readonly Color32 Band;
            public readonly Color32 Appliance;
            public readonly float AcDensity;
            public readonly int GlassTint;
            public readonly Vector2 UvOffset;

            private SiteLooks(Color32 wall, Color32 baseLook, Color32 roof, Color32 trim, Color32 frame, Color32 band, Color32 appliance,
                float acDensity, int glassTint, Vector2 uvOffset)
            {
                Wall = wall;
                Base = baseLook;
                Roof = roof;
                Trim = trim;
                Frame = frame;
                Band = band;
                Appliance = appliance;
                AcDensity = acDensity;
                GlassTint = glassTint;
                UvOffset = uvOffset;
            }

            public static SiteLooks For(BuildingSite site, FacadeStyle style)
            {
                BuildingLook look = BuildingLook.For(site, style);
                return new SiteLooks(
                    SurfaceLook.Pack(look.WallTint, (int)look.Wall, look.WallWear),
                    SurfaceLook.Pack(look.BaseTint, (int)look.Base, look.BaseWear),
                    SurfaceLook.Pack(look.RoofTint, (int)WallMaterial.Roof, look.RoofWear),
                    SurfaceLook.Pack(look.TrimTint, (int)WallMaterial.Concrete, look.WallWear * 0.8f),
                    SurfaceLook.Pack(look.FrameTint, (int)WallMaterial.Metal, 0.15f),
                    SurfaceLook.Pack(look.BandTint, (int)look.BandMaterial, look.WallWear),
                    SurfaceLook.Pack(new Color(0.86f, 0.87f, 0.88f), (int)WallMaterial.Metal, 0.55f),
                    look.AcDensity,
                    look.GlassTint,
                    look.UvOffset);
            }
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
            MeshBuffer structures, MeshBuffer concrete, Glazing glazing, SiteLooks looks)
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
            structures.Look = looks.Base;
            AddWallPanel(structures, west, z0, z1, podiumY, lobbyY, Vector3.left);
            AddWallPanel(structures, east, z0, z1, podiumY, lobbyY, Vector3.right);
            AddWallPanel(structures, south, x0, x1, podiumY, lobbyY, Vector3.back);
            AddWallPanel(structures, north, x0, x1, podiumY, lobbyY, Vector3.forward);

            // Outward, for the street, with real openings in it.
            AddWindowedWall(structures, glazing, site, west, z0, z1, lobbyY, roofY,
                Vector3.left, plan, WindowGrid.Wall.West, rows, 0.0f, 0.0f, false, looks);
            AddWindowedWall(structures, glazing, site, east, z0, z1, lobbyY, roofY,
                Vector3.right, plan, WindowGrid.Wall.East, rows, 0.0f, 0.0f, false, looks);
            AddWindowedWall(structures, glazing, site, south, x0, x1, lobbyY, roofY,
                Vector3.back, plan, WindowGrid.Wall.South, rows, 0.0f, 0.0f, false, looks);
            AddWindowedWall(structures, glazing, site, north, x0, x1, lobbyY, roofY,
                Vector3.forward, plan, WindowGrid.Wall.North, rows, doorU0, doorU1, true, looks);

            // Inward, for the room. Same planes and the same openings, opposite
            // normals -- back to back, so neither is ever seen from the wrong
            // side, and a window is a hole through BOTH or it is not a hole.
            //
            // The wall is BuildingInterior.WallThickness thick: the inside face is that far in from the outside one, over the same span
            // so every window column still lines up. Each opening's reveals (AddWindowReveals) join the two faces.
            float t = BuildingInterior.WallThickness;
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, new Wall(true, x0 + t, z0, z1), z0, z1, lobbyY, roofY,
                Vector3.right, plan, WindowGrid.Wall.West, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, new Wall(true, x1 - t, z0, z1), z0, z1, lobbyY, roofY,
                Vector3.left, plan, WindowGrid.Wall.East, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, new Wall(false, z0 + t, x0, x1), x0, x1, lobbyY, roofY,
                Vector3.forward, plan, WindowGrid.Wall.South, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, new Wall(false, z1 - t, x0, x1), x0, x1, lobbyY, roofY,
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
                BuildingInterior.Stairwell well = WellFor(site, floor, floors, halfX, halfZ, run);
                Rect hole = new Rect(well.Hole.x + centreX, well.Hole.y + centreZ,
                    well.Hole.width, well.Hole.height);

                // This floor's ceiling. The last one is the roof itself, holed
                // exactly like the rest -- that opening is how the top flight
                // gets you outside. The roof's top face is made of roofing, in the
                // wall buffer so it draws with the facade material (no extra draw).
                bool isRoof = floor == floors - 1;
                if (isRoof)
                {
                    structures.Look = looks.Roof;
                }
                AddSlab(concrete, x0, x1, z0, z1, hole, lobbyY + top, isRoof ? structures : null);

                AddRoomPartition(concrete, BuildingInterior.RoomFor(floor, halfX, halfZ),
                    centreX, centreZ, lobbyY + bottom, lobbyY + top, doorTop - lobbyY);

                if (isRoof)
                {
                    AddRoof(structures, site, x0, x1, z0, z1, lobbyY + top, looks, hole);
                    structures.Look = SurfaceLook.Default;
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
            MeshBuffer structures, MeshBuffer concrete, Glazing glazing, SiteLooks looks)
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

            // The podium and the ground floor in the building's base material, then the rest of the wall in one panel each. The
            // distant building must wear the same look as the near one (the same two materials, split at the same height), or it
            // would change as you walk up to it.
            float baseTop = Mathf.Min(roofY, lobbyY + BuildingInterior.RowHeight(height));
            structures.Look = looks.Base;
            AddWallPanel(structures, west, z0, z1, podiumY, baseTop, Vector3.left);
            AddWallPanel(structures, east, z0, z1, podiumY, baseTop, Vector3.right);
            AddWallPanel(structures, south, x0, x1, podiumY, baseTop, Vector3.back);
            AddWallPanel(structures, north, x0, x1, podiumY, baseTop, Vector3.forward);
            structures.Look = looks.Wall;
            AddWallPanel(structures, west, z0, z1, baseTop, roofY, Vector3.left);
            AddWallPanel(structures, east, z0, z1, baseTop, roofY, Vector3.right);
            AddWallPanel(structures, south, x0, x1, baseTop, roofY, Vector3.back);
            AddWallPanel(structures, north, x0, x1, baseTop, roofY, Vector3.forward);

            // The roof, and the antennas and parapets on it: they are the skyline. Roofing and plant are written into the wall
            // buffer so they draw with the facade material.
            structures.Look = looks.Roof;
            AddSlabFace(structures, x0, x1, z0, z1, Rect.zero, roofY, Vector3.up);
            {
                float shellCentreX = (x0 + x1) * 0.5f;
                float shellCentreZ = (z0 + z1) * 0.5f;
                float shellHalfX = site.Params.Width * 0.5f;
                float shellHalfZ = site.Params.Depth * 0.5f;
                int shellFloors = BuildingInterior.FloorCountFor(height);
                BuildingInterior.Stairwell topWell = WellFor(site, shellFloors - 1, shellFloors, shellHalfX, shellHalfZ,
                    BuildingInterior.RunFor(shellHalfX, shellHalfZ));
                var hatch = new Rect(topWell.Hole.x + shellCentreX, topWell.Hole.y + shellCentreZ, topWell.Hole.width, topWell.Hole.height);
                AddRoof(structures, site, x0, x1, z0, z1, roofY, looks, hatch);
            }
            structures.Look = SurfaceLook.Default;

            if (!glazing.HangsGlass)
            {
                return;
            }

            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(height);
            float centreX = (x0 + x1) * 0.5f;
            float doorU0 = centreX - BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = centreX + BuildingInterior.DoorWidth * 0.5f;

            // The distant glass wears the same tint as the near windows (no blind, one kind of light: a strip is many windows).
            Color32 shellGlass = SurfaceLook.PackGlass(looks.GlassTint, 0, 0.0f);
            glazing.Lit.Look = shellGlass;
            glazing.Dark.Look = shellGlass;

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
            MeshBuffer concrete, SiteLooks looks)
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
            concrete.Look = looks.Trim;

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
            MeshBuffer facade, SiteLooks looks)
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

            facade.Look = looks.Wall;
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
        /// The roof of a building: a parapet round its edge with a coping on top, and the plant a real flat roof carries -- air handlers,
        /// fans, vents, skylights, a stair or lift head, a water tank, a mast. Placed by the building's seed, kept off the hatch the stair comes
        /// out of and off the volume standing on the roof if there is one.
        ///
        /// It used to be one prop a metre across on a roof twenty metres wide, which no one could see from anywhere. The skyline is the roofs.
        /// A parapet makes every roof end in a clean edge (it was a raw slab cut) and is a wall high enough that nobody walks off a roof by
        /// accident; the climb still tops out over it (PlayerClimbing mantles forward onto whatever is past the wall).
        /// </summary>
        private static void AddRoof(MeshBuffer buffer, BuildingSite site, float x0, float x1, float z0, float z1, float roofY,
            SiteLooks looks, Rect hatch)
        {
            float t = ParapetWallThickness;
            float h = ParapetWallHeight;
            float o = CopingOverhang;
            Color32 saved = buffer.Look;

            // The wall, in the walls' own look so it reads as the facade carried up past the roof.
            buffer.Look = looks.Wall;
            buffer.AddBox(new Vector3(x0, roofY, z0), new Vector3(x1, roofY + h, z0 + t));
            buffer.AddBox(new Vector3(x0, roofY, z1 - t), new Vector3(x1, roofY + h, z1));
            buffer.AddBox(new Vector3(x0, roofY, z0 + t), new Vector3(x0 + t, roofY + h, z1 - t));
            buffer.AddBox(new Vector3(x1 - t, roofY, z0 + t), new Vector3(x1, roofY + h, z1 - t));

            // The coping on top of it, a little proud.
            buffer.Look = looks.Trim;
            float c0 = roofY + h;
            float c1 = c0 + CopingHeight;
            buffer.AddBox(new Vector3(x0 - o, c0, z0 - o), new Vector3(x1 + o, c1, z0 + t + o));
            buffer.AddBox(new Vector3(x0 - o, c0, z1 - t - o), new Vector3(x1 + o, c1, z1 + o));
            buffer.AddBox(new Vector3(x0 - o, c0, z0 + t + o), new Vector3(x0 + t + o, c1, z1 - t - o));
            buffer.AddBox(new Vector3(x1 - t - o, c0, z0 + t + o), new Vector3(x1 + o, c1, z1 - t - o));

            AddRoofPlant(buffer, site, x0, x1, z0, z1, roofY, looks, hatch);
            buffer.Look = saved;
        }

        private const float ParapetWallHeight = 1.2f;
        private const float ParapetWallThickness = 0.3f;
        private const float CopingHeight = 0.10f;
        private const float CopingOverhang = 0.06f;
        private const ulong RoofStream = 0x524F4F46UL;   // "ROOF"

        private static Rect Grown(Rect r, float by)
        {
            return Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
        }

        /// <summary>
        /// What stands on a flat roof. Boxes, for the same reason everything else here is (a skyline is read from far off and a cylinder costs
        /// sixteen times the triangles to say the same thing), but at the scale of the real thing.
        /// </summary>
        private static void AddRoofPlant(MeshBuffer buffer, BuildingSite site, float x0, float x1, float z0, float z1, float roofY,
            SiteLooks looks, Rect hatch)
        {
            var rng = new DeterministicRandom(unchecked((ulong)(uint)site.Seed), RoofStream);
            float margin = ParapetWallThickness + 0.9f;
            float rx0 = x0 + margin, rx1 = x1 - margin, rz0 = z0 + margin, rz1 = z1 - margin;
            if (rx1 - rx0 < 2.5f || rz1 - rz0 < 2.5f)
            {
                return;
            }

            // Ground that is taken: the hatch the stair comes out of, and whatever stands on this roof.
            var taken = new List<Rect>();
            if (hatch.width > 0.0f)
            {
                taken.Add(Grown(hatch, 1.2f));
            }
            if (site.HasRoofObstacle)
            {
                float cx = (x0 + x1) * 0.5f;
                float cz = (z0 + z1) * 0.5f;
                taken.Add(Grown(Rect.MinMaxRect(site.ObstacleX0 + cx, site.ObstacleZ0 + cz, site.ObstacleX1 + cx, site.ObstacleZ1 + cz), 0.9f));
            }

            string roof = site.Params.Roof ?? "";
            float density = roof == "shed" ? 0.9f : (roof == "" || roof == "parapet" ? 0.45f : 0.6f);
            float area = (rx1 - rx0) * (rz1 - rz0);
            int units = Mathf.Clamp(Mathf.RoundToInt(area / 150.0f * density * (0.5f + (float)rng.NextDouble())), 0, 12);

            Color32 plant = SurfaceLook.Pack(new Color(0.84f, 0.85f, 0.86f), (int)WallMaterial.Metal, 0.45f);
            Color32 darkPlant = SurfaceLook.Pack(new Color(0.40f, 0.42f, 0.44f), (int)WallMaterial.Metal, 0.5f);

            // The signature piece of the roof's type first, so it gets its place.
            switch (roof)
            {
                case "water_tower":
                    TryPlace(buffer, rng, taken, rx0, rx1, rz0, rz1, 4.4f, 4.4f, (cx, cz) =>
                    {
                        buffer.Look = darkPlant;
                        foreach (float dx in new[] { -1.5f, 1.5f })
                        {
                            foreach (float dz in new[] { -1.5f, 1.5f })
                            {
                                AddCentredBox(buffer, cx + dx, cz + dz, roofY, 0.28f, 3.0f, 0.28f);
                            }
                        }
                        AddCentredBox(buffer, cx, cz, roofY + 3.0f, 3.8f, 3.4f, 3.8f);
                        buffer.Look = looks.Trim;
                        AddCentredBox(buffer, cx, cz, roofY + 6.4f, 4.1f, 0.22f, 4.1f);
                    });
                    break;

                case "antenna":
                    TryPlace(buffer, rng, taken, rx0, rx1, rz0, rz1, 1.6f, 1.6f, (cx, cz) =>
                    {
                        float mast = Mathf.Lerp(6.0f, 30.0f, Mathf.Clamp01(site.Params.Height / 220.0f));
                        buffer.Look = looks.Trim;
                        AddCentredBox(buffer, cx, cz, roofY, 1.4f, 0.6f, 1.4f);
                        buffer.Look = darkPlant;
                        AddCentredBox(buffer, cx, cz, roofY + 0.6f, 0.16f, mast, 0.16f);
                        AddCentredBox(buffer, cx, cz, roofY + 0.6f + mast * 0.62f, 1.6f, 0.07f, 0.07f);
                        AddCentredBox(buffer, cx, cz, roofY + 0.6f + mast * 0.85f, 1.0f, 0.07f, 0.07f);
                    });
                    break;

                case "chimney":
                    TryPlace(buffer, rng, taken, rx0, rx1, rz0, rz1, 2.3f, 2.3f, (cx, cz) =>
                    {
                        float stack = 4.5f + 3.9f * (float)rng.NextDouble();
                        buffer.Look = looks.Trim;
                        AddCentredBox(buffer, cx, cz, roofY, 1.7f, stack, 1.7f);
                        AddCentredBox(buffer, cx, cz, roofY + stack, 2.1f, 0.26f, 2.1f);
                    });
                    break;

                case "shed":
                    TryPlace(buffer, rng, taken, rx0, rx1, rz0, rz1, 5.0f, 6.2f, (cx, cz) =>
                    {
                        // The head of a stair or a lift: a small room on the roof.
                        buffer.Look = looks.Trim;
                        AddCentredBox(buffer, cx, cz, roofY, 4.4f, 3.6f, 5.6f);
                        buffer.Look = darkPlant;
                        AddCentredBox(buffer, cx, cz, roofY + 3.6f, 4.7f, 0.18f, 5.9f);
                    });
                    break;
            }

            // Then the ordinary plant.
            for (int i = 0; i < units; i++)
            {
                double kind = rng.NextDouble();
                if (kind < 0.50)
                {
                    float w = 2.1f + 2.0f * (float)rng.NextDouble();
                    float d = 1.3f + 0.9f * (float)rng.NextDouble();
                    float hh = 1.3f + 0.9f * (float)rng.NextDouble();
                    TryPlace(buffer, rng, taken, rx0, rx1, rz0, rz1, w + 0.4f, d + 0.4f, (cx, cz) =>
                    {
                        buffer.Look = plant;
                        AddCentredBox(buffer, cx, cz, roofY, w, hh, d);
                        buffer.Look = darkPlant;
                        AddCentredBox(buffer, cx, cz, roofY + hh, w * 0.7f, 0.12f, d * 0.7f);
                    });
                }
                else if (kind < 0.66)
                {
                    TryPlace(buffer, rng, taken, rx0, rx1, rz0, rz1, 1.5f, 1.5f, (cx, cz) =>
                    {
                        buffer.Look = plant;
                        AddCentredBox(buffer, cx - 0.3f, cz, roofY, 0.35f, 1.8f + (float)rng.NextDouble(), 0.35f);
                        AddCentredBox(buffer, cx + 0.3f, cz + 0.2f, roofY, 0.35f, 1.4f + (float)rng.NextDouble(), 0.35f);
                    });
                }
                else if (kind < 0.82)
                {
                    TryPlace(buffer, rng, taken, rx0, rx1, rz0, rz1, 3.0f, 2.2f, (cx, cz) =>
                    {
                        // A skylight: a low curb with a dark lid.
                        buffer.Look = looks.Trim;
                        AddCentredBox(buffer, cx, cz, roofY, 2.6f, 0.5f, 1.8f);
                        buffer.Look = darkPlant;
                        AddCentredBox(buffer, cx, cz, roofY + 0.5f, 2.3f, 0.12f, 1.5f);
                    });
                }
                else
                {
                    TryPlace(buffer, rng, taken, rx0, rx1, rz0, rz1, 4.5f, 0.9f, (cx, cz) =>
                    {
                        // A run of ductwork.
                        buffer.Look = plant;
                        AddCentredBox(buffer, cx, cz, roofY + 0.35f, 4.1f, 0.5f, 0.5f);
                        AddCentredBox(buffer, cx - 1.7f, cz, roofY, 0.15f, 0.35f, 0.15f);
                        AddCentredBox(buffer, cx + 1.7f, cz, roofY, 0.15f, 0.35f, 0.15f);
                    });
                }
            }
        }

        /// <summary>
        /// Finds a spot of the given footprint on the roof that is clear of everything taken so far, and builds there. A few tries, then
        /// gives up: a crowded roof simply has one thing fewer.
        /// </summary>
        private static void TryPlace(MeshBuffer buffer, DeterministicRandom rng, List<Rect> taken,
            float rx0, float rx1, float rz0, float rz1, float width, float depth, System.Action<float, float> build)
        {
            if (rx1 - rx0 < width || rz1 - rz0 < depth)
            {
                return;
            }
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float cx = Mathf.Lerp(rx0 + width * 0.5f, rx1 - width * 0.5f, (float)rng.NextDouble());
                float cz = Mathf.Lerp(rz0 + depth * 0.5f, rz1 - depth * 0.5f, (float)rng.NextDouble());
                Rect footprint = Rect.MinMaxRect(cx - width * 0.5f, cz - depth * 0.5f, cx + width * 0.5f, cz + depth * 0.5f);
                bool clear = true;
                foreach (Rect other in taken)
                {
                    if (footprint.Overlaps(other))
                    {
                        clear = false;
                        break;
                    }
                }
                if (!clear)
                {
                    continue;
                }
                taken.Add(Grown(footprint, 0.5f));
                build(cx, cz);
                return;
            }
        }

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
            float doorU0, float doorU1, bool hasDoor, SiteLooks? looks = null)
        {
            CellLayout columns = plan.ColumnsFor(u1 - u0);
            float inset = WindowGrid.PaneInset;

            for (int row = 0; row < rows.Count; row++)
            {
                // The ground floor row is made of something sturdier, darker and dirtier than the storeys above it.
                if (looks.HasValue)
                {
                    wallBuffer.Look = row == 0 ? looks.Value.Base : looks.Value.Wall;
                }
                float y0 = floorY + row * rows.Size;
                float y1 = y0 + rows.Size;

                if (hasDoor && row == 0)
                {
                    AddDoorRow(wallBuffer, glazing, site, wall, u0, y0, y1, normal,
                        plan, which, columns, doorU0, doorU1, looks);
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

                // The strips above and below the windows may be a different material from the piers between them.
                Color32 rowLook = wallBuffer.Look;
                if (looks.HasValue)
                {
                    wallBuffer.Look = looks.Value.Band;
                }
                AddWallPanel(wallBuffer, wall, u0, u1, y0, paneY0, normal);
                AddWallPanel(wallBuffer, wall, u0, u1, paneY1, y1, normal);
                wallBuffer.Look = rowLook;

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

                    AddOpening(wallBuffer, glazing, site, wall, paneU0, paneU1, paneY0, paneY1, normal,
                        plan, which, column, row, looks);
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
            in WindowPlan plan, WindowGrid.Wall which, CellLayout columns, float doorU0, float doorU1, SiteLooks? looks = null)
        {
            bool storefront = plan.Storefront && which == WindowGrid.Wall.North;
            float rowHeight = y1 - y0;

            // A real door height, not the whole row: the doorway is 2.4 m and the rest of the row's height above it is a lintel (the
            // sign band, on a shop).
            float doorTop = y0 + Mathf.Min(BuildingInterior.DoorHeight(site.Params.Height), rowHeight);

            // A shopfront is glass nearly cell-wide, from a stall riser up to a sign band; any other ground floor keeps the facade's
            // own window proportions.
            float inset = storefront ? StorefrontPierShare : WindowGrid.PaneInset;

            // The door row is always row 0, whose own top boundary (1) is
            // never a slab location (see WindowGrid.TopEdgeHasSlab) -- the
            // small clear margin is correct on both edges here, not just a
            // simplification.
            float paneY0 = storefront ? y0 + StallRiser : y0 + rowHeight * WindowGrid.PaneInsetVerticalClear;
            float paneY1 = storefront ? y0 + rowHeight * StorefrontGlassTop : y1 - rowHeight * WindowGrid.PaneInsetVerticalClear;

            // The doorway is a recess the thickness of the wall (no floor face: the lobby slab is already there).
            if (glazing.HangsGlass)
            {
                AddWindowReveals(wallBuffer, wall, doorU0, doorU1, y0, doorTop, normal, null, false, false);
            }
            AddWallPanel(wallBuffer, wall, doorU0, doorU1, doorTop, y1, normal);

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

                AddOpening(wallBuffer, glazing, site, wall, paneU0, paneU1, paneY0, paneY1, normal,
                    plan, which, column, 0, looks);
            }

            // What marks the entrance from the street. Only the outward pass builds it.
            if (glazing.HangsGlass && looks.HasValue)
            {
                if (storefront)
                {
                    AddStorefrontTrim(wallBuffer, site, wall, u0, columns, y0, y1, paneY1, doorU0, doorU1, normal);
                }
                else
                {
                    AddEntranceCanopy(wallBuffer, wall, doorU0, doorU1, doorTop, normal, looks.Value);
                }
                AddBladeSigns(wallBuffer, glazing, site, wall, u0, u0 + columns.Count * columns.Size, y0, doorU0, doorU1, normal, looks.Value, storefront);
            }
        }

        /// <summary>The solid base under a shop window, metres.</summary>
        private const float StallRiser = 0.5f;

        /// <summary>Where a shop window's glass ends, as a share of the row. Above it, to the top of the row, is a transom the awning shades.</summary>
        private const float StorefrontGlassTop = 0.88f;

        /// <summary>The share of a cell's width left solid either side of a shop window.</summary>
        private const float StorefrontPierShare = 0.035f;

        private const ulong StorefrontStream = 0x53544F52UL;   // "STOR"

        // Muted shop colours: the awnings and the sign boards. Strong colour is the exception here, not the rule.
        private static readonly Color[] AwningColours =
        {
            new Color(0.55f, 0.20f, 0.18f), new Color(0.16f, 0.30f, 0.22f), new Color(0.15f, 0.24f, 0.42f),
            new Color(0.78f, 0.72f, 0.58f), new Color(0.52f, 0.36f, 0.18f), new Color(0.24f, 0.24f, 0.26f),
        };

        private static readonly Color[] SignColours =
        {
            new Color(0.13f, 0.13f, 0.14f), new Color(0.82f, 0.80f, 0.74f), new Color(0.40f, 0.12f, 0.12f),
            new Color(0.12f, 0.20f, 0.36f), new Color(0.14f, 0.28f, 0.20f), new Color(0.62f, 0.50f, 0.20f),
        };

        /// <summary>
        /// The sign board over a shopfront and (usually) an awning over each side of its door: the two things that make a ground floor
        /// read as a shop. Colours come from the building's seed, from a short muted list.
        ///
        /// SCALED FOR A 2.6 M PLAYER. The ground row is only 3.2 m, so an awning under a sign band would hang at head height: nobody could
        /// walk along the pavement. The awning is mounted at the very top of the row and its lowest edge is clear of a tall body; the sign
        /// board sits on the wall of the storey above, in the band under the first row of windows.
        /// </summary>
        private static void AddStorefrontTrim(MeshBuffer buffer, BuildingSite site, in Wall wall, float u0, CellLayout columns,
            float y0, float y1, float glassTop, float doorU0, float doorU1, Vector3 outward)
        {
            var rng = new DeterministicRandom(unchecked((ulong)(uint)site.Seed), StorefrontStream);
            Color sign = SignColours[rng.Below(SignColours.Length)];
            Color awning = AwningColours[rng.Below(AwningColours.Length)];
            bool hasAwning = rng.NextDouble() < 0.6;

            float frontU0 = u0 + 0.35f;
            float frontU1 = u0 + columns.Count * columns.Size - 0.35f;
            Color32 saved = buffer.Look;

            // The sign board, proud of the wall, either side of the door.
            buffer.Look = SurfaceLook.Pack(sign, (int)WallMaterial.Metal, 0.25f);
            float boardBottom = y1 + 0.12f;
            float boardTop = y1 + 0.78f;
            foreach (float[] span in new[] { new[] { frontU0, doorU0 - 0.15f }, new[] { doorU1 + 0.15f, frontU1 } })
            {
                if (span[1] - span[0] < 0.8f)
                {
                    continue;
                }
                Vector3 b0 = wall.At(span[0], boardBottom);
                Vector3 b1 = wall.At(span[1], boardTop) + outward * 0.07f;
                buffer.AddBox(Vector3.Min(b0, b1), Vector3.Max(b0, b1));
            }

            if (hasAwning)
            {
                buffer.Look = SurfaceLook.Pack(awning, (int)WallMaterial.Plaster, 0.35f);
                float topY = y1 - 0.08f;
                AddAwning(buffer, wall, frontU0 + 0.1f, doorU0 - 0.25f, topY, outward);
                AddAwning(buffer, wall, doorU1 + 0.25f, frontU1 - 0.1f, topY, outward);
            }
            buffer.Look = saved;
        }

        /// <summary>A sloped awning between two points along a wall: a top, an underside and a short front flap, open at the ends.</summary>
        private static void AddAwning(MeshBuffer buffer, in Wall wall, float u0, float u1, float topY, Vector3 outward)
        {
            if (u1 - u0 < 0.8f)
            {
                return;
            }
            const float projection = 1.1f;
            const float drop = 0.28f;
            const float flap = 0.10f;
            Vector3 wallTop0 = wall.At(u0, topY);
            Vector3 wallTop1 = wall.At(u1, topY);
            Vector3 edgeTop0 = wallTop0 + outward * projection + Vector3.down * drop;
            Vector3 edgeTop1 = wallTop1 + outward * projection + Vector3.down * drop;
            Vector3 flapBottom0 = edgeTop0 + Vector3.down * flap;
            Vector3 flapBottom1 = edgeTop1 + Vector3.down * flap;

            Vector3 up = (outward * drop + Vector3.up * projection).normalized;
            AddFace(buffer, up, wallTop0, wallTop1, edgeTop1, edgeTop0);
            AddFace(buffer, -up, wallTop0, wallTop1, edgeTop1, edgeTop0);
            AddFace(buffer, outward, edgeTop0, edgeTop1, flapBottom1, flapBottom0);
        }

        private const ulong BladeStream = 0x424C4144UL;   // "BLAD"

        /// <summary>
        /// Vertical blade signs: a lit panel on an arm, standing out from the wall at right angles so it is read from along the street. The
        /// shape of a dense Asian street at night. Few: about half the shopfronts have one, some two, and a downtown building sometimes.
        /// The panels are in the lit glass, so they come on at dusk like the windows and are dark glass by day; the frame is metal.
        /// </summary>
        private static void AddBladeSigns(MeshBuffer wallBuffer, in Glazing glazing, BuildingSite site, in Wall wall, float u0, float u1,
            float y0, float doorU0, float doorU1, Vector3 outward, SiteLooks looks, bool storefront)
        {
            if (glazing.Lit == null)
            {
                return;
            }
            var rng = new DeterministicRandom(unchecked((ulong)(uint)site.Seed), BladeStream);
            double chance = storefront ? 0.55 : (site.Zone == ZoneKind.Core || site.Zone == ZoneKind.Urban ? 0.12 : 0.0);
            if (rng.NextDouble() >= chance)
            {
                return;
            }
            int count = storefront && rng.NextDouble() < 0.30 ? 2 : 1;
            Vector3 along = (wall.At(u0 + 1.0f, 0.0f) - wall.At(u0, 0.0f)).normalized;
            Color32 saved = wallBuffer.Look;
            Color32 savedGlass = glazing.Lit.Look;

            for (int i = 0; i < count; i++)
            {
                float height = Mathf.Lerp(3.2f, 5.8f, (float)rng.NextDouble());
                float bottom = y0 + 3.8f + 1.2f * (float)rng.NextDouble();
                float reach = 1.0f + 0.6f * (float)rng.NextDouble();

                // Somewhere along the front, clear of the door and the corners.
                float u = 0.0f;
                bool found = false;
                for (int attempt = 0; attempt < 8 && !found; attempt++)
                {
                    u = Mathf.Lerp(u0 + 1.6f, u1 - 1.6f, (float)rng.NextDouble());
                    found = u < doorU0 - 2.2f || u > doorU1 + 2.2f;
                }
                if (!found)
                {
                    continue;
                }

                // The arm back to the wall, and the frame the panels sit in.
                wallBuffer.Look = looks.Frame;
                Vector3 a0 = wall.At(u - 0.06f, bottom + height * 0.5f - 0.05f);
                Vector3 a1 = wall.At(u + 0.06f, bottom + height * 0.5f + 0.05f) + outward * 0.5f;
                wallBuffer.AddBox(Vector3.Min(a0, a1), Vector3.Max(a0, a1));
                Vector3 f0 = wall.At(u - 0.14f, bottom) + outward * 0.4f;
                Vector3 f1 = wall.At(u + 0.14f, bottom + height) + outward * reach;
                wallBuffer.AddBox(Vector3.Min(f0, f1), Vector3.Max(f0, f1), true);

                // The two lit faces, one colour: a neon that is not the colour of the window next to it.
                int light = 2 + rng.Below(5);
                glazing.Lit.Look = SurfaceLook.PackGlass(0, light, 0.0f);
                float panelWidth = reach - 0.4f - 0.2f;
                float panelHeight = height - 0.4f;
                foreach (float side in new[] { 1.0f, -1.0f })
                {
                    float x = u + side * 0.145f;
                    Vector3 c00 = wall.At(x, bottom + 0.2f) + outward * 0.5f;
                    Vector3 c10 = wall.At(x, bottom + 0.2f) + outward * (reach - 0.1f);
                    Vector3 c11 = wall.At(x, bottom + 0.2f + panelHeight) + outward * (reach - 0.1f);
                    Vector3 c01 = wall.At(x, bottom + 0.2f + panelHeight) + outward * 0.5f;
                    glazing.Lit.AddQuad(c00, c10, c11, c01, along * side,
                        new Vector2(0.0f, 0.0f), new Vector2(panelWidth, 0.0f), new Vector2(panelWidth, panelHeight), new Vector2(0.0f, panelHeight));
                }
            }
            wallBuffer.Look = saved;
            glazing.Lit.Look = savedGlass;
        }

        /// <summary>A flat canopy over an ordinary entrance: a slab on the wall above the door.</summary>
        private static void AddEntranceCanopy(MeshBuffer buffer, in Wall wall, float doorU0, float doorU1, float doorTop,
            Vector3 outward, SiteLooks looks)
        {
            Color32 saved = buffer.Look;
            buffer.Look = looks.Trim;
            Vector3 c0 = wall.At(doorU0 - 0.55f, doorTop + 0.12f);
            Vector3 c1 = wall.At(doorU1 + 0.55f, doorTop + 0.28f) + outward * 1.15f;
            buffer.AddBox(Vector3.Min(c0, c1), Vector3.Max(c0, c1));
            buffer.Look = saved;
        }

        /// <summary>
        /// One window: the reveals that make the opening a recess (and a sill under it), then the glass set back inside it.
        ///
        /// THE REVEALS ARE BUILT WHATEVER STATE THE GLASS IS IN, like the opening itself: a shattered window is still a window-shaped
        /// recess in a thick wall. Only the outward pass hangs glass, so only it builds them -- the one set of four faces serves both
        /// sides, because each faces into the opening.
        /// </summary>
        private static void AddOpening(MeshBuffer wallBuffer, in Glazing glazing, BuildingSite site, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal,
            in WindowPlan plan, WindowGrid.Wall which, int column, int row, SiteLooks? looks)
        {
            if (glazing.HangsGlass)
            {
                AddWindowReveals(wallBuffer, wall, u0, u1, y0, y1, normal, looks, true);
                if (looks.HasValue)
                {
                    bool intact = glazing.Damage == null || glazing.Damage.StateOf(new PaneKey(site.Id, which, column, row)) == PaneState.Intact;
                    AddWindowFrame(wallBuffer, Inward(wall, normal, BuildingInterior.GlassSetBack), u0, u1, y0, y1, normal, looks.Value, intact);
                    if (looks.Value.AcDensity > 0.0f && row > 0 && !(plan.Storefront && which == WindowGrid.Wall.North && row == 1)
                        && AirConditionerAt(site, which, column, row) < looks.Value.AcDensity)
                    {
                        AddAirConditioner(wallBuffer, wall, u0, u1, y0, normal, looks.Value);
                    }
                }
            }
            AddPane(glazing, site, wall, u0, u1, y0, y1, normal, plan, which, column, row, looks);
        }

        /// <summary>The same wall, moved <paramref name="distance"/> in from its outward face (<paramref name="outward"/> points out of it).</summary>
        private static Wall Inward(in Wall wall, Vector3 outward, float distance)
        {
            float sign = wall.RunsInZ ? Mathf.Sign(outward.x) : Mathf.Sign(outward.z);
            return new Wall(wall.RunsInZ, wall.Plane - sign * distance, wall.U0, wall.U1);
        }

        /// <summary>
        /// The four faces that join an opening's outside edge to its inside one across the thickness of the wall (left, right, sill, head),
        /// each facing into the opening, so they are seen from the street and from the room alike. With a sill: a small stone ledge under
        /// the opening that throws a line of shadow, in the building's trim.
        /// </summary>
        private static void AddWindowReveals(MeshBuffer buffer, in Wall wall, float u0, float u1, float y0, float y1,
            Vector3 outward, SiteLooks? looks, bool withSill, bool floorFace = true)
        {
            Vector3 inward = -outward * BuildingInterior.WallThickness;
            Vector3 a0 = wall.At(u0, y0);
            Vector3 a1 = wall.At(u1, y0);
            Vector3 b0 = wall.At(u0, y1);
            Vector3 b1 = wall.At(u1, y1);
            Vector3 along = (a1 - a0).normalized;

            AddFace(buffer, along, a0, a0 + inward, b0 + inward, b0);
            AddFace(buffer, -along, a1, a1 + inward, b1 + inward, b1);
            if (floorFace)
            {
                AddFace(buffer, Vector3.up, a0, a1, a1 + inward, a0 + inward);
            }
            AddFace(buffer, Vector3.down, b0, b1, b1 + inward, b0 + inward);

            if (withSill && looks.HasValue)
            {
                Color32 saved = buffer.Look;
                buffer.Look = looks.Value.Trim;
                Vector3 p0 = wall.At(u0 - SillSideExtra, y0 - SillThickness);
                Vector3 p1 = wall.At(u1 + SillSideExtra, y0) + outward * SillOverhang;
                buffer.AddBox(Vector3.Min(p0, p1), Vector3.Max(p0, p1));
                buffer.Look = saved;
            }
        }

        /// <summary>
        /// The frame round a window and the bars across it: four thin faces, plus a mullion down a wide window and a transom across a
        /// tall one. They are in the WALL buffer, standing a hair proud of the glass, so a window that is cracked or shot out keeps its
        /// frame. <paramref name="glassWall"/> is the wall at the glass plane.
        /// </summary>
        private static void AddWindowFrame(MeshBuffer buffer, in Wall glassWall, float u0, float u1, float y0, float y1,
            Vector3 outward, SiteLooks looks, bool crossBars)
        {
            const float bar = 0.05f;
            const float lift = 0.012f;
            float width = u1 - u0;
            float height = y1 - y0;
            Vector3 raise = outward * lift;
            Color32 saved = buffer.Look;
            buffer.Look = looks.Frame;

            // The four sides.
            AddFace(buffer, outward, glassWall.At(u0, y0) + raise, glassWall.At(u0 + bar, y0) + raise,
                glassWall.At(u0 + bar, y1) + raise, glassWall.At(u0, y1) + raise);
            AddFace(buffer, outward, glassWall.At(u1 - bar, y0) + raise, glassWall.At(u1, y0) + raise,
                glassWall.At(u1, y1) + raise, glassWall.At(u1 - bar, y1) + raise);
            AddFace(buffer, outward, glassWall.At(u0 + bar, y0) + raise, glassWall.At(u1 - bar, y0) + raise,
                glassWall.At(u1 - bar, y0 + bar) + raise, glassWall.At(u0 + bar, y0 + bar) + raise);
            AddFace(buffer, outward, glassWall.At(u0 + bar, y1 - bar) + raise, glassWall.At(u1 - bar, y1 - bar) + raise,
                glassWall.At(u1 - bar, y1) + raise, glassWall.At(u0 + bar, y1) + raise);

            // A wide window is two leaves; a tall one has a transom light above.
            // (Only while the window is whole: a broken one can be walked through, and the bars across it must not stop you.)
            if (crossBars && width > 1.25f)
            {
                float mid = (u0 + u1) * 0.5f;
                AddFace(buffer, outward, glassWall.At(mid - bar * 0.4f, y0 + bar) + raise, glassWall.At(mid + bar * 0.4f, y0 + bar) + raise,
                    glassWall.At(mid + bar * 0.4f, y1 - bar) + raise, glassWall.At(mid - bar * 0.4f, y1 - bar) + raise);
            }
            if (crossBars && height > 2.3f)
            {
                float rail = y0 + height * 0.72f;
                AddFace(buffer, outward, glassWall.At(u0 + bar, rail - bar * 0.4f) + raise, glassWall.At(u1 - bar, rail - bar * 0.4f) + raise,
                    glassWall.At(u1 - bar, rail + bar * 0.4f) + raise, glassWall.At(u0 + bar, rail + bar * 0.4f) + raise);
            }
            buffer.Look = saved;
        }

        /// <summary>
        /// One window's glass look: the building's tint, a kind of light of its own, and a blind drawn part-way down it. From the window's
        /// place in the building, so it is the same every time the chunk is built and no two neighbours match.
        /// </summary>
        private static Color32 GlassLookFor(BuildingSite site, WindowGrid.Wall which, int column, int row, int tint)
        {
            uint h = unchecked((uint)(site.Id * 73856093) ^ (uint)((int)which * 19349663) ^ (uint)(column * 83492791) ^ (uint)(row * 2654435761u));
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            int light = (int)(h % 8u);
            // Most windows are warm white; the blue of a screen and the colours are the exception.
            if (light >= 3 && (h >> 8) % 3u != 0u)
            {
                light = (int)((h >> 4) % 2u);
            }
            float blind = ((h >> 16) % 100u) < 55u ? ((h >> 24) % 100u) / 100.0f * 0.75f : 0.0f;
            return SurfaceLook.PackGlass(tint, light, blind);
        }

        /// <summary>A number 0 .. 1 from a window's place in its building: whether it has an air conditioner under it.</summary>
        private static float AirConditionerAt(BuildingSite site, WindowGrid.Wall which, int column, int row)
        {
            uint h = unchecked((uint)(site.Id * 2654435761u) ^ (uint)((int)which * 40503) ^ (uint)(column * 9973) ^ (uint)(row * 31337));
            h ^= h >> 16;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFu) / 65536.0f;
        }

        /// <summary>A split-system condenser on a bracket under a window: the thing that hangs on every flat in a dense city.</summary>
        private static void AddAirConditioner(MeshBuffer buffer, in Wall wall, float u0, float u1, float y0, Vector3 outward, SiteLooks looks)
        {
            const float width = 1.05f;
            const float height = 0.46f;
            const float depth = 0.38f;
            float centre = (u0 + u1) * 0.5f;
            float bottom = y0 - 0.78f;
            Color32 saved = buffer.Look;
            buffer.Look = looks.Appliance;
            Vector3 a = wall.At(centre - width * 0.5f, bottom);
            Vector3 b = wall.At(centre + width * 0.5f, bottom + height) + outward * depth;
            buffer.AddBox(Vector3.Min(a, b), Vector3.Max(a, b));
            // The dark grille on its face.
            buffer.Look = looks.Frame;
            Vector3 g0 = wall.At(centre - width * 0.40f, bottom + height * 0.18f) + outward * (depth + 0.004f);
            Vector3 g1 = wall.At(centre + width * 0.40f, bottom + height * 0.82f) + outward * (depth + 0.004f);
            AddFace(buffer, outward, g0, wall.At(centre + width * 0.40f, bottom + height * 0.18f) + outward * (depth + 0.004f), g1,
                wall.At(centre - width * 0.40f, bottom + height * 0.82f) + outward * (depth + 0.004f));
            buffer.Look = saved;
        }

        private const float SillThickness = 0.07f;
        private const float SillOverhang = 0.08f;
        private const float SillSideExtra = 0.05f;

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
        private static void AddPane(in Glazing glazing, BuildingSite site, in Wall outerWall,
            float u0, float u1, float y0, float y1, Vector3 normal,
            in WindowPlan plan, WindowGrid.Wall which, int column, int row, SiteLooks? looks = null)
        {
            if (!glazing.HangsGlass)
            {
                return;
            }

            // The glass sits GlassSetBack in from the outside face, inside the wall's thickness.
            Wall wall = Inward(outerWall, normal, BuildingInterior.GlassSetBack);

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
                // The glass is UV'd in metres from its own corner, which is what CityX/Glass wants: blinds hang from the top of THIS
                // window, whatever height it is at. Each window's look (tint, the kind of light it shows, how far its blind is down) is
                // its own, from its place in the building, so no two neighbours are quite alike.
                Color32 savedLook = glass.Look;
                if (looks.HasValue && state != PaneState.Cracked)
                {
                    glass.Look = GlassLookFor(site, which, column, row, looks.Value.GlassTint);
                }
                float w = u1 - u0;
                float h = y1 - y0;
                glass.AddQuad(wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1), normal,
                    new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
                glass.AddQuad(wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1), -normal,
                    new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
                glass.Look = savedLook;
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
            Rect hole, float topY, MeshBuffer topBuffer = null)
        {
            float bottomY = topY - BuildingInterior.SlabThickness;
            AddSlabFace(topBuffer ?? buffer, x0, x1, z0, z1, hole, topY, Vector3.up);
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

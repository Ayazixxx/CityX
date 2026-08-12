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
            Vector3 origin, MeshBuffer ground, MeshBuffer flatConcrete, MeshBuffer structures)
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

            BuildBuilding(site, origin, structures, flatConcrete);
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

            ground.AddQuad(
                new Vector3(x0, y, z0), new Vector3(x0, y, z1),
                new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up,
                new Vector2(0, 0), new Vector2(0, size), new Vector2(size, size), new Vector2(size, 0));

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
        private static void BuildBuilding(BuildingSite site, Vector3 origin, MeshBuffer structures,
            MeshBuffer concrete)
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

            // Outward, for the street. The podium is part of the same span, so
            // a building on a slope still has a face all the way down.
            AddWallPanel(structures, west, z0, z1, podiumY, roofY, Vector3.left);
            AddWallPanel(structures, east, z0, z1, podiumY, roofY, Vector3.right);
            AddWallPanel(structures, south, x0, x1, podiumY, roofY, Vector3.back);
            AddWallAround(structures, north, podiumY, roofY, Vector3.forward,
                doorU0, doorU1, lobbyY, doorTop);

            // Inward, for the room. Same planes, opposite normals -- back to
            // back, so neither is ever seen from the wrong side.
            AddWallPanel(concrete, west, z0, z1, lobbyY, roofY, Vector3.right);
            AddWallPanel(concrete, east, z0, z1, lobbyY, roofY, Vector3.left);
            AddWallPanel(concrete, south, x0, x1, lobbyY, roofY, Vector3.forward);
            AddWallAround(concrete, north, lobbyY, roofY, Vector3.back,
                doorU0, doorU1, lobbyY, doorTop);

            // The lobby floor, and NOT a port: city_chunk.gd leans on the
            // chunk's own ground body for the ground floor. Here the podium is
            // hollow and the box has no underside, so without this you walk in
            // the door and fall down the inside of the podium. It is solid --
            // no hole -- because nothing in this port goes below it yet.
            AddSlabFace(concrete, x0, x1, z0, z1, Rect.zero, lobbyY, Vector3.up);

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

            // The hole's own inner edges, facing into the opening, which is the
            // only side anyone ever sees them from.
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

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
        /// Builds ground into <paramref name="ground"/> and everything built by
        /// people into <paramref name="structures"/>. Two buffers because they
        /// want different materials, and two submeshes on one object is one
        /// draw call cheaper than two objects.
        /// </summary>
        public static void BuildChunk(CityWorld world, SkybridgeLayout bridges, int chunkX, int chunkY,
            Vector3 origin, MeshBuffer ground, MeshBuffer structures)
        {
            BuildGround(world, chunkX, chunkY, origin, ground);

            BuildingSite site = world.BuildingAt(chunkX, chunkY);
            if (!site.Exists || site.AnchorX != chunkX || site.AnchorY != chunkY)
            {
                // Either nothing here, or it belongs to a neighbouring anchor
                // and that chunk will build it. Building it twice would double
                // every multi-chunk tower.
                return;
            }

            BuildBuilding(site, origin, structures);
            BuildBridges(world, bridges, site, origin, structures);
        }

        private static void BuildGround(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            MeshBuffer ground)
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

        private static void BuildBuilding(BuildingSite site, Vector3 origin, MeshBuffer structures)
        {
            // One box from the podium base to the roof. The stretch below
            // BaseHeight is buried on the uphill side and exposed downhill,
            // which is what a hillside podium actually is.
            Vector3 min = new Vector3(
                (float)(site.MinX - origin.x),
                site.PodiumBase - origin.y,
                (float)(site.MinZ - origin.z));
            Vector3 max = new Vector3(
                (float)(site.MaxX - origin.x),
                site.Top - origin.y,
                (float)(site.MaxZ - origin.z));

            structures.AddBox(min, max);
        }

        private static void BuildBridges(CityWorld world, SkybridgeLayout bridges, BuildingSite site,
            Vector3 origin, MeshBuffer structures)
        {
            // Only east and north. Every pair has exactly one building that
            // sees it as east-or-north, so each walkway is built once -- the
            // west and south lookups exist for queries, not for geometry.
            BuildSpan(bridges, site, SkybridgeLayout.EastNeighbour(world, site), true, origin, structures);
            BuildSpan(bridges, site, SkybridgeLayout.NorthNeighbour(world, site), false, origin, structures);
        }

        private static void BuildSpan(SkybridgeLayout bridges, BuildingSite from, BuildingSite to,
            bool alongX, Vector3 origin, MeshBuffer structures)
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

                structures.AddBox(min, max, includeBottom: true);
            }
        }
    }
}

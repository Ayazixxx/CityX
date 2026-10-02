using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Helpers for tests that need a real building from the plan city.
    ///
    /// A building is built in its own unturned frame and turned to face its street, so a position a test works out from the
    /// building's frame (MinX, MaxZ, a window cell's centre) has to be converted to a world position before anything outside the
    /// builder is asked about it.
    /// </summary>
    internal static class TestSites
    {
        /// <summary>The first building a chunk builds, or <see cref="BuildingSite.None"/>.</summary>
        public static BuildingSite FirstOwnedBy(CityWorld world, int chunkX, int chunkY)
        {
            List<int> owned = world.BuildingsOwnedBy(chunkX, chunkY);
            return owned.Count > 0 ? world.Building(owned[0]) : BuildingSite.None;
        }

        /// <summary>Every building within a few chunks of the world's spawn point, nearest chunks first.</summary>
        public static IEnumerable<BuildingSite> NearSpawn(CityWorld world, int chunkRadius = 12)
        {
            world.Plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanChunk(sx), cy = PlanChunk(sz);
            for (int r = 0; r <= chunkRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
                        List<int> owned = world.BuildingsOwnedBy(cx + dx, cy + dy);
                        for (int i = 0; i < owned.Count; i++)
                        {
                            yield return world.Building(owned[i]);
                        }
                    }
                }
            }
        }

        private static int PlanChunk(double v) => (int)System.Math.Floor(v / CityLayout.ChunkSize);

        /// <summary>
        /// The world position of a point given in the builder's unturned frame -- the frame <see cref="BuildingSite.MinX"/> ..
        /// <see cref="BuildingSite.MaxZ"/> are in.
        /// </summary>
        public static Vector3 At(BuildingSite site, double frameX, double y, double frameZ)
        {
            site.LocalToWorld(frameX - site.CentreX, frameZ - site.CentreZ, out double wx, out double wz);
            return new Vector3((float)wx, (float)y, (float)wz);
        }
    }
}

using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// A body of standing or flowing water: an area on the ground (a box in plan), its floor and its surface, and the way it flows
    /// (docs/WATER.md). The water a sewer tunnel holds, a puddle a spell leaves, a flood rising over the streets: all of them are one of these,
    /// asked "how deep is it here" through <see cref="CurrentWater"/>. Its surface may move (a flood rises and recedes: <see cref="FloodTimeline"/>).
    /// </summary>
    public sealed class WaterBody
    {
        public float MinX, MinZ, MaxX, MaxZ;

        /// <summary>Its floor: below this there is no water in it (a body standing under a pool is not in the pool).</summary>
        public float BottomY;

        /// <summary>Its surface, world height.</summary>
        public float SurfaceY;

        /// <summary>The way it flows on the ground, metres a second (zero for still water): it carries what is in it along.</summary>
        public Vector2 Flow;

        public WaterBody(float minX, float minZ, float maxX, float maxZ, float bottomY, float surfaceY)
        {
            MinX = Mathf.Min(minX, maxX);
            MaxX = Mathf.Max(minX, maxX);
            MinZ = Mathf.Min(minZ, maxZ);
            MaxZ = Mathf.Max(minZ, maxZ);
            BottomY = bottomY;
            SurfaceY = surfaceY;
        }

        /// <summary>A square of water <paramref name="size"/> across round a point on the ground, filled to <paramref name="depth"/>.</summary>
        public static WaterBody Square(Vector3 groundCentre, float size, float depth)
        {
            float half = size * 0.5f;
            return new WaterBody(groundCentre.x - half, groundCentre.z - half, groundCentre.x + half, groundCentre.z + half,
                groundCentre.y - CurrentWater.FloorAllowance, groundCentre.y + depth);
        }

        public bool CoversPlan(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        /// <summary>How deep the water is over a point (its surface above it), 0 or less where the point is out of it.</summary>
        public float DepthOver(Vector3 point)
        {
            if (!CoversPlan(point.x, point.z) || point.y < BottomY - CurrentWater.FloorAllowance)
            {
                return 0.0f;
            }
            return SurfaceY - point.y;
        }
    }
}

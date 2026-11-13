using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// The sewers' water as the game asks about water (<see cref="CurrentWater"/>): the world being played's (<see cref="CurrentWorld"/>)
    /// <see cref="SewerPlan"/>, asked at a point. Still water for the body: it carries nothing along (whether the sewer current pushes is
    /// the owner's call, docs/WATER.md); its ripples run downstream only in the look.
    /// </summary>
    public sealed class SewerWaterSource : IWaterSource
    {
        public static readonly SewerWaterSource Instance = new SewerWaterSource();

        public bool TryWaterAt(Vector3 point, out WaterHere water)
        {
            water = default;
            CityWorld world = CurrentWorld.Instance;
            if (world == null || !SewerPlan.For(world.Plan).TryWaterAt(point.x, point.y, point.z, out double surface, out double bottom))
            {
                return false;
            }
            if (point.y < bottom - CurrentWater.FloorAllowance)
            {
                return false;
            }
            water = new WaterHere { Depth = Mathf.Max(0.0f, (float)surface - point.y), SurfaceY = (float)surface };
            return water.Wet;
        }
    }
}

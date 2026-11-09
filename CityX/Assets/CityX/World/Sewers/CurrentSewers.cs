using UnityEngine;

namespace CityX.World
{
    /// <summary>The sewers of the world being played (<see cref="CurrentWorld"/>), for whatever only has a place to ask about.</summary>
    public static class CurrentSewers
    {
        /// <summary>How far out of the sky's light a place is, 0 (under the sky) to 1 (deep in a tunnel): SewerPlan.DarknessAt. 0 with no world.</summary>
        public static float DarknessAt(Vector3 point)
        {
            CityWorld world = CurrentWorld.Instance;
            return world == null ? 0.0f : (float)SewerPlan.For(world.Plan).DarknessAt(point.x, point.y, point.z);
        }
    }
}

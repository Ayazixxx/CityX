using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Water that is not a box: a place the game asks "is there water over this point" and answers from its own shape, as the sewers'
    /// channels do (SewerPlan.Water). Asked through <see cref="CurrentWater"/> beside its <see cref="WaterBody"/> list.
    /// </summary>
    public interface IWaterSource
    {
        /// <summary>The water over a point: false where there is none (above its surface is not "none": the depth is then 0 or less).</summary>
        bool TryWaterAt(Vector3 point, out WaterHere water);
    }

    /// <summary>The water at a point: how deep over it, where its surface is, and the way it carries what is in it.</summary>
    public struct WaterHere
    {
        /// <summary>Metres of water over the point; 0 where it is dry.</summary>
        public float Depth;

        /// <summary>The surface over the point, world height (meaningless where <see cref="Depth"/> is 0).</summary>
        public float SurfaceY;

        /// <summary>How the water carries what is in it, metres a second on the ground.</summary>
        public Vector2 Flow;

        public bool Wet => Depth > 0.0f;
    }
}

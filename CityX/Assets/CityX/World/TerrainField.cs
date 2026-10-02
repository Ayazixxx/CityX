using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// Ground height anywhere in the world: the plan's smooth hills, with a dry trench where a river would run.
    ///
    /// (It used to be a terraced height field quantised to one platform per chunk. The city now has free-form roads and lots,
    /// so the ground is continuous and the buildings fill the difference with a podium -- see BuildingSite.PodiumBase.)
    /// </summary>
    public sealed class TerrainField
    {
        /// <summary>Lowest the ground ever sits for the purposes of a fall check: the trench floor is a few metres below this.</summary>
        public const float SeaLevel = 0.0f;

        private readonly PlanWorld _plan;

        public TerrainField(PlanWorld plan)
        {
            _plan = plan;
        }

        /// <summary>The ground height at a world position, metres.</summary>
        public double ContinuousHeightAt(double x, double z) => _plan.GroundHeight(x, z);

        public float HeightAt(float x, float z) => (float)_plan.GroundHeight(x, z);
    }
}

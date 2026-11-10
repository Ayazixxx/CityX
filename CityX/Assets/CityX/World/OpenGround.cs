using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// The most open ground near a point: the place within reach that is furthest from any building, out from under the expressways and off
    /// the water. The new character's fall lands there, so that its cameras, which swing round the body a few metres off, never go into a
    /// wall (owner, 2026-11-10: "make him fall far enough away from the buildings that the camera changes dont clip into the building").
    /// From the plan alone, so the answer is there before any of the city has been built around it.
    /// </summary>
    public static class OpenGround
    {
        /// <summary>How finely the ground is searched, metres between rings and between tries round each ring.</summary>
        private const double Step = 2.0;

        /// <summary>The widest clear distance tried, metres: anything at least this open is open enough.</summary>
        public const double MostClearance = 24.0;

        /// <summary>How far out from under an expressway a spot must be, metres: a camera a few metres up would go into the deck.</summary>
        private const double ExpresswayClearance = 10.0;

        /// <summary>
        /// The most open spot within <paramref name="reach"/> metres of (<paramref name="x"/>, <paramref name="z"/>), and how far it is from
        /// the nearest building (up to <see cref="MostClearance"/>); of spots equally open, the nearest. Kept to the plan's buildings: what
        /// else stands in the street (stairs, raised plazas) the cutscene's camera keeps clear of itself. Kept to the roads, it landed on
        /// a stair the plan does not count as a building. <paramref name="standable"/>, if given, has the last word on each spot.
        /// </summary>
        public static (double X, double Z, double Clearance) Near(CityWorld world, double x, double z, double reach,
            System.Func<double, double, bool> standable = null)
        {
            (double X, double Z, double Clearance) best = (x, z, Clearance(world, x, z));
            for (double ring = Step; ring <= reach; ring += Step)
            {
                int tries = System.Math.Max(8, (int)(2.0 * System.Math.PI * ring / Step));
                for (int i = 0; i < tries; i++)
                {
                    double a = 2.0 * System.Math.PI * i / tries;
                    double px = x + System.Math.Cos(a) * ring, pz = z + System.Math.Sin(a) * ring;
                    double clear = Clearance(world, px, pz);
                    if (clear > best.Clearance && (standable == null || standable(px, pz)))
                    {
                        best = (px, pz, clear);
                    }
                }
            }
            return best;
        }

        /// <summary>How far from the nearest building a spot is, metres, to the nearest whole one and at most <see cref="MostClearance"/>; 0 under an expressway or on water.</summary>
        public static double Clearance(CityWorld world, double x, double z)
        {
            PlanWorld plan = world.Plan;
            var at = new Vec2(x, z);
            if (plan.Fields.IsWater(at) || plan.Data.Expressways.Covers(at, ExpresswayClearance))
            {
                return 0.0;
            }
            double clear = 0.0;
            // Old Guy's house is not one of the plan's buildings, and the player starts on its driveway.
            while (clear < MostClearance && !world.TryBuildingAt(x, z, out _, clear + 1.0) && !plan.TutorialHouse.Contains(x, z, clear + 1.0))
            {
                clear += 1.0;
            }
            return clear;
        }
    }
}

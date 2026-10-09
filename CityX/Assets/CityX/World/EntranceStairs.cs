using System;

namespace CityX.World
{
    /// <summary>
    /// The outside stairs up to a front door that stands above the paving (a building on a slope: its floor is set by the highest ground under it, so
    /// on the downhill side the door can be metres up).
    ///
    /// WHY THIS SHAPE (2026-10-09, reported with screenshots: "floating staircases, some blocking the streets"): the old flight ran straight out from
    /// the door, as long as the rise measured 2 m out needed. Further out the slope kept falling, so its foot hung in the air, and a tall rise
    /// carried it across the pavement into the road. Now a LANDING stands in front of the door and the FLIGHT runs down along the wall, the way stairs
    /// up a hillside building's front are built. Each step is a solid block down to the paving under it, and the flight stops at the first step the
    /// ground has risen to meet -- so it can never float, and it stays against the building.
    ///
    /// Pure: the caller says how high the paving is at a distance along the wall on either side; this decides the side and how many steps.
    /// </summary>
    public static class EntranceStairs
    {
        /// <summary>The going of one step, metres.</summary>
        public const float Tread = 0.32f;

        /// <summary>The rise of one step, metres (a 2.6 m player's stairs; BuildingStairs.MaxRiser is 0.235).</summary>
        public const float Riser = 0.22f;

        /// <summary>How wide the flight is, and how far the landing at the door stands out from the wall.</summary>
        public const float Width = 1.6f;

        /// <summary>The landing in front of the door, along the wall (the flight starts at its edge).</summary>
        public const float LandingLength = 2.0f;

        /// <summary>A flight never has more steps than this, whatever the ground does.</summary>
        public const int MaxSteps = 60;

        /// <summary>Each step block reaches this far below the paving under it, so a slope never shows a gap beneath one.</summary>
        public const float Footing = 0.3f;

        public readonly struct Flight
        {
            /// <summary>+1: the flight runs toward +x from the landing (the door wall's own along axis); -1 toward -x.</summary>
            public readonly int Side;

            /// <summary>How many steps, the first one Riser below the landing.</summary>
            public readonly int Steps;

            public Flight(int side, int steps)
            {
                Side = side;
                Steps = steps;
            }

            /// <summary>The top of step k (1-based), given the landing's height.</summary>
            public float TopOf(int k, float landing) => landing - k * Riser;

            /// <summary>Where step k runs along the wall, metres from the landing's edge: [k-1, k] treads.</summary>
            public float StartOf(int k) => (k - 1) * Tread;
        }

        /// <summary>
        /// The flight from a landing at <paramref name="landing"/>: <paramref name="pavingAt"/>(side, along) is the paving's height on the flight's line
        /// <paramref name="along"/> metres from the landing's edge on that side. The side whose ground comes up to meet it sooner wins; the flight ends at
        /// the first step that would be at or under the paving (so the last riser, onto the ground, is never more than one step).
        /// </summary>
        public static Flight Plan(float landing, Func<int, float, float> pavingAt)
        {
            int best = 0, bestSide = 1;
            foreach (int side in new[] { 1, -1 })
            {
                int steps = StepsDown(landing, along => pavingAt(side, along));
                if (side == 1 || steps < best)
                {
                    best = steps;
                    bestSide = side;
                }
            }
            return new Flight(bestSide, best);
        }

        /// <summary>How many steps down a flight takes before the ground meets it.</summary>
        public static int StepsDown(float landing, Func<float, float> pavingAlong)
        {
            for (int k = 1; k <= MaxSteps; k++)
            {
                float top = landing - k * Riser;
                if (top <= pavingAlong((k - 0.5f) * Tread) + 0.02f)
                {
                    return k - 1;
                }
            }
            return MaxSteps;
        }
    }
}

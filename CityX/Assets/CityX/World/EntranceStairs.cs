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

        /// <summary>
        /// The flights of a TIGHT folded stair, for a door too high for the broad one that fits before the road (2026-10-16: three doors near the
        /// spawn on seed 19077151, 10-14 m up on 11-14 m fronts with room for two broad lanes, ended 2.7-3.8 m over the paving): three of these
        /// fit where two broad ones did. Still wide enough for the 2.6 m player to pass and turn on its platforms.
        /// </summary>
        public const float NarrowWidth = 1.2f;

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

        // ---- the folded stair: a flight that turns back along the wall rather than run past its end ----
        //
        // 2026-10-16: on the new hills a door can stand 10-14 m over the street (one building in thirty in the game's city), and a single flight
        // down along the wall ran 20 m, past the end of its own front and across whatever was beside it (a stairway between the blocks, found
        // when a body could not climb it). Hillside buildings fold such a stair: a flight along the wall to near its end, a platform, the next
        // flight back the other way a lane further out, and so on -- all of it in front of the building's own wall, none of it over the road.

        /// <summary>How far short of the wall's end a flight or a platform stops, metres.</summary>
        public const float EndMargin = 0.3f;

        /// <summary>The most lanes a stair folds into (the first against the wall, the others each a flight's width further out).</summary>
        public const int MaxLanes = 3;

        /// <summary>
        /// A folded stair never has more steps than this (22 m of rise; a single flight stops at <see cref="MaxSteps"/>): on the hills a door can
        /// stand 17 m up, and with a long front its flights have the room (seed 19077151: five doors near the spawn stopped at 60 steps, 0.7-5 m up).
        /// </summary>
        public const int MaxFoldedSteps = 100;

        /// <summary>One straight flight of a folded stair.</summary>
        public readonly struct Run
        {
            /// <summary>0 against the wall, 1 and 2 each a lane further out (<see cref="Folded.LaneWidth"/>).</summary>
            public readonly int Lane;

            /// <summary>+1: down toward +x along the wall; -1 toward -x.</summary>
            public readonly int Dir;

            /// <summary>Where the flight's top step begins along the wall, and the height of the floor it comes down from.</summary>
            public readonly float Start, From;

            public readonly int Steps;

            public Run(int lane, int dir, float start, float from, int steps)
            {
                Lane = lane;
                Dir = dir;
                Start = start;
                From = from;
                Steps = steps;
            }

            /// <summary>Step k (1-based): its span along the wall and its top.</summary>
            public float StepA(int k) => Start + Dir * (k - 1) * Tread;

            public float StepB(int k) => Start + Dir * k * Tread;

            public float TopOf(int k) => From - k * Riser;

            /// <summary>Where the flight ends along the wall, and the height there (its last step's top).</summary>
            public float End => Start + Dir * Steps * Tread;

            public float Bottom => From - Steps * Riser;
        }

        /// <summary>The platform at a flight's end where the stair turns: across its lane and the next, from the flight's end a flight's width on.</summary>
        public readonly struct Turn
        {
            public readonly int Lane;
            public readonly float X0, X1, Top;

            public Turn(int lane, float x0, float x1, float top)
            {
                Lane = lane;
                X0 = x0;
                X1 = x1;
                Top = top;
            }
        }

        /// <summary>A folded stair: its flights and turns in order down from the door's landing; whether it reaches the ground.</summary>
        public sealed class Folded
        {
            public readonly System.Collections.Generic.List<Run> Runs = new System.Collections.Generic.List<Run>();
            public readonly System.Collections.Generic.List<Turn> Turns = new System.Collections.Generic.List<Turn>();
            public bool Reaches;
            public int Steps;

            /// <summary>How many lanes it uses.</summary>
            public int Lanes => Runs.Count == 0 ? 0 : Runs[Runs.Count - 1].Lane + 1;

            /// <summary>The height its last flight (or turn) ends at.</summary>
            public float Bottom;

            /// <summary>How wide its flights are, and its lanes out from the wall: <see cref="Width"/>, or <see cref="NarrowWidth"/> for a tight one.</summary>
            public float LaneWidth = Width;
        }

        /// <summary>
        /// The stair from a door's landing (top <paramref name="landing"/>, along the wall from <paramref name="doorX"/> - <paramref name="halfLanding"/>
        /// to + it) down to the paving, never past the wall's ends (<paramref name="wallHalf"/> either side of its middle) nor more than
        /// <paramref name="lanes"/> lanes out: <paramref name="pavingAt"/>(lane, x) is the paving's height on a lane's centre line. Each flight runs
        /// along the wall until the ground comes up to meet a step (the stair is done) or the wall's end is near; there it turns on a platform and
        /// comes back a lane further out. Of the two ways it can start, the one that reaches the ground in fewer lanes and steps.
        /// Its flights and lanes are <paramref name="width"/> wide.
        /// </summary>
        public static Folded PlanFolded(float landing, float doorX, float halfLanding, float wallHalf, int lanes, Func<int, float, float> pavingAt,
            float width = Width)
        {
            lanes = Math.Max(1, Math.Min(MaxLanes, lanes));
            Folded best = null;
            foreach (int first in new[] { 1, -1 })
            {
                Folded f = FoldFrom(landing, doorX + first * halfLanding, first, wallHalf, lanes, pavingAt, width);
                if (best == null || Better(f, best)) best = f;
            }
            return best;
        }

        /// <summary>Half the landing at a building's door, along the wall: a pair of doors (BuildingInterior.HasDoubleEntrance) needs a longer one.</summary>
        public static float HalfLanding(BuildingSite site) => Math.Max(LandingLength, BuildingInterior.EntranceWidth(site) + 0.4f) * 0.5f;

        /// <summary>
        /// How many lanes a stair at a building's front may fold into: as many flights' widths as fit between the wall (<paramref name="wallZ"/> in
        /// the building's frame) and the road, the least across the frontage (a round tower's face stands in from its box, which the road is
        /// measured from); at least one.
        /// </summary>
        public static int LanesFor(CityX.World.Plan.PlanWorld plan, BuildingSite site, float doorX, float wallZ, float wallHalf, float width = Width)
        {
            int lanes = MaxLanes;
            float behind = site.Params.Depth * 0.5f - wallZ;
            foreach (float u in new[] { -wallHalf + 0.5f, 0.0f, wallHalf - 0.5f })
            {
                float room = GarageEntrances.CarriagewayOut(plan, site, doorX + u);
                if (!float.IsNaN(room)) lanes = Math.Min(lanes, (int)Math.Floor((room + behind - 0.4f) / width));
            }
            return Math.Max(1, lanes);
        }

        /// <summary>
        /// The stair at a building's front door as it is built (CityMeshBuilder.AddEntranceStairs): the door at <paramref name="doorX"/>,
        /// <paramref name="doorY"/> on the wall at <paramref name="wallZ"/> (the building's frame), the wall <paramref name="wallHalf"/> either side
        /// of the door; x in the result is measured from the door.
        /// </summary>
        public static Folded ForDoor(CityX.World.Plan.PlanWorld plan, BuildingSite site, float doorX, float doorY, float wallZ, float wallHalf)
        {
            float Paving(float x, float z)
            {
                site.LocalToWorld(x, z, out double wx, out double wz);
                return (float)(plan.GroundHeight(wx, wz) + CityX.World.Plan.PlanBuildings.PavingRise);
            }
            // The broad stair; where it cannot reach the ground in the lanes that fit, a tight one (more lanes in the same room), whichever ends lower.
            float half = HalfLanding(site);
            Folded broad = PlanFolded(doorY, 0.0f, half, wallHalf, LanesFor(plan, site, doorX, wallZ, wallHalf),
                (lane, x) => Paving(doorX + x, wallZ + (lane + 0.5f) * Width));
            if (broad.Reaches) return broad;
            Folded tight = PlanFolded(doorY, 0.0f, half, wallHalf, LanesFor(plan, site, doorX, wallZ, wallHalf, NarrowWidth),
                (lane, x) => Paving(doorX + x, wallZ + (lane + 0.5f) * NarrowWidth), NarrowWidth);
            return tight.Reaches || tight.Bottom < broad.Bottom ? tight : broad;
        }

        private static bool Better(Folded a, Folded b)
        {
            if (a.Reaches != b.Reaches) return a.Reaches;
            if (!a.Reaches) return a.Bottom < b.Bottom;
            if (a.Lanes != b.Lanes) return a.Lanes < b.Lanes;
            return a.Steps < b.Steps;
        }

        private static Folded FoldFrom(float landing, float x, int dir, float wallHalf, int lanes, Func<int, float, float> pavingAt, float width)
        {
            var f = new Folded { LaneWidth = width };
            float y = landing;
            for (int lane = 0; lane < lanes; lane++)
            {
                bool lastLane = lane == lanes - 1;
                // Room along the wall: up to its end, less a platform's length where the stair may yet turn.
                float limit = wallHalf - EndMargin - (lastLane ? 0.0f : width);
                int k = 0;
                bool reached = false;
                while (f.Steps + k < MaxFoldedSteps)
                {
                    float a = x + dir * k * Tread, b = a + dir * Tread;
                    if (Math.Max(Math.Abs(a), Math.Abs(b)) > limit) break;
                    if (y - (k + 1) * Riser <= pavingAt(lane, (a + b) * 0.5f) + 0.02f)
                    {
                        reached = true;
                        break;
                    }
                    k++;
                }
                var run = new Run(lane, dir, x, y, k);
                f.Runs.Add(run);
                f.Steps += k;
                y = run.Bottom;
                x = run.End;
                if (reached)
                {
                    f.Reaches = true;
                    break;
                }
                if (lastLane || f.Steps >= MaxFoldedSteps) break;
                // The turn: a platform a flight's width on, across this lane and the next; where the ground has come up to it, the stair ends on it.
                f.Turns.Add(new Turn(lane, x, x + dir * width, y));
                if (y - Riser <= Math.Max(pavingAt(lane, x + dir * width * 0.5f), pavingAt(lane + 1, x + dir * width * 0.5f)) + 0.02f)
                {
                    f.Reaches = true;
                    break;
                }
                dir = -dir;
            }
            f.Bottom = y;
            return f;
        }
    }
}

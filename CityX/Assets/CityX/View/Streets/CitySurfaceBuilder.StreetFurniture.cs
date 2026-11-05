using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;

namespace CityX.View
{
    /// <summary>
    /// What stands along each sidewalk's kerb (World/StreetFurniture, docs/AMERICAN_STREETS.md), stood chunk by chunk, and the marks of
    /// the parking stalls in the road beside the meters.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>A stall's mark: half its line's width, how far off the kerb its stem starts, and how far its bar reaches each way along the road, metres.</summary>
        public const double StallMarkHalfWidth = 0.05, StallMarkFromKerb = 0.15, StallMarkBar = 0.55;

        /// <summary>
        /// One road's furnishings that stand in this chunk. None where the pavement is cut away (a ramp's foot, a garage's drive, a
        /// rounded corner), at a stairway's mouth, or on Old Guy's lot.
        /// </summary>
        private static void BuildStreetFurniture(Context ctx, EdgeGeometry g, MeshBuffer structures, List<Vec2[]> cuts, List<StairLane> stairways,
            List<StreetFurnishing> scratch)
        {
            scratch.Clear();
            StreetFurniture.Along(ctx.Plan, g, scratch);
            foreach (StreetFurnishing thing in scratch)
            {
                if (!ctx.Contains(thing.At) || InCut(thing.At, cuts) || BeforeStairway(stairways, thing.At) || ctx.Plan.OnTutorialLot(thing.At.X, thing.At.Y, 1.0))
                {
                    continue;
                }
                double y = ctx.Plan.GroundHeight(thing.At.X, thing.At.Y) + RoadLift + KerbHeight;
                CityProps.AddStreetFurnishing(structures, ctx.Local(thing.At.X, y, thing.At.Y), thing);
            }
        }

        /// <summary>
        /// The parking stalls of a road's metered sides: a white T at each stall line, its stem from the kerb out into the road and its bar
        /// along the edge of the lane.
        /// </summary>
        private static void PaintStalls(Context ctx, EdgeGeometry g, MeshBuffer white, Func<double, double, double> height)
        {
            if (white == null)
            {
                return;
            }
            var lines = new List<double>();
            for (int side = 1; side >= -1; side -= 2)
            {
                lines.Clear();
                StreetFurniture.StallLines(ctx.Plan, g, side, lines);
                Vec2 aside = g.Dir.Perp * side;
                foreach (double along in lines)
                {
                    Vec2 atTheKerb = g.A + g.Dir * along + aside * (g.HalfWidth - StallMarkFromKerb);
                    Vec2 end = g.A + g.Dir * along + aside * (g.HalfWidth - StreetFurniture.StallDepth);
                    StripeAlong(ctx, white, atTheKerb, end, g.Dir, StallMarkHalfWidth, height);
                    StripeAlong(ctx, white, end - g.Dir * StallMarkBar, end + g.Dir * StallMarkBar, aside, StallMarkHalfWidth, height);
                }
            }
        }
    }
}

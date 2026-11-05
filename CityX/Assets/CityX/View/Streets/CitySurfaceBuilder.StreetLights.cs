using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>The street lights along each driven street (World/StreetLights, docs/AMERICAN_STREETS.md), stood on the pavement chunk by chunk.</summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>
        /// One road's street lights that stand in this chunk. None where the pavement is cut away (a ramp's foot, a garage's drive) or on Old
        /// Guy's lot. The powered ones' lamps are added to <paramref name="powered"/>, in the world's coordinates, for the lights lent at night.
        /// </summary>
        private static void BuildStreetLights(Context ctx, EdgeGeometry g, MeshBuffer structures, MeshBuffer lit, List<Vec2[]> cuts, List<StreetLamp> scratch,
            List<Vector3> powered)
        {
            scratch.Clear();
            StreetLights.Along(ctx.Plan, g, scratch);
            foreach (StreetLamp lamp in scratch)
            {
                if (!ctx.Contains(lamp.At) || InCut(lamp.At, cuts) || ctx.Plan.OnTutorialLot(lamp.At.X, lamp.At.Y, 1.0))
                {
                    continue;
                }
                double y = ctx.Plan.GroundHeight(lamp.At.X, lamp.At.Y) + RoadLift + KerbHeight;
                CityProps.AddStreetLight(structures, lit, ctx.Local(lamp.At.X, y, lamp.At.Y), lamp.Over, lamp.Height, lamp.Arm, lamp.Powered);
                if (lamp.Powered)
                {
                    Vec2 head = lamp.Head;
                    powered.Add(new Vector3((float)head.X, (float)y + lamp.Height - 0.1f, (float)head.Y));
                }
            }
        }
    }
}

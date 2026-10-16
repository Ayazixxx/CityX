using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The sidewalks that are steps (World/SteppedSidewalks: where the kerb climbs 14 % or more, as on San Francisco's 22nd Street): the strip
    /// from the kerb to the lot line is cut out of the block's paving and laid as treads following the slope, each a block of paving whose face to
    /// the road is the kerb there (the kerb is not drawn along it) and whose face to the next tread is the step; where the paving beyond the lot
    /// line stands over a tread, a retaining face goes up to it. The sidewalk's furniture keeps off it. Each tread is built by the chunk its
    /// middle is in.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        private static readonly Color32 SidewalkStepLook = SurfaceLook.Pack(new Color(0.80f, 0.79f, 0.76f), SurfaceLook.Paving, 0.45f);

        /// <summary>
        /// A tread reaches this far below the lowest ground at its corners, metres: deeper than a stairway's, so beside a step street whose treads
        /// sit up to 0.75 m under the road (StepStreets.MaxStray) its face still comes down past them.
        /// </summary>
        private const float SidewalkStepFooting = 1.0f;

        /// <summary>The stepped sidewalks of the blocks reaching into this chunk (block and edge), their strips added to <paramref name="cuts"/>.</summary>
        private static List<(int block, int edge)> SteppedSidewalksHere(Context ctx, List<int> blocks, List<Vec2[]> cuts)
        {
            var here = new List<(int, int)>();
            foreach (int b in blocks)
            {
                PlanBlock block = ctx.Plan.Data.Blocks[b];
                if (block.Inner == null || block.Outer == null) continue;
                for (int e = 0; e < block.Inner.Count; e++)
                {
                    if (!SteppedSidewalks.Is(ctx.Plan, b, e)) continue;
                    Vec2[] strip = SteppedSidewalks.Strip(block, e);
                    Bounds(strip, out double x0, out double z0, out double x1, out double z1);
                    if (!ctx.Rect.Overlaps(x0, z0, x1, z1)) continue;
                    here.Add((b, e));
                    cuts.Add(strip);
                }
            }
            return here;
        }

        private static void BuildSteppedSidewalks(Context ctx, List<(int block, int edge)> sidewalks, MeshBuffer buffer)
        {
            foreach ((int b, int e) in sidewalks)
            {
                PlanBlock block = ctx.Plan.Data.Blocks[b];
                int f = (e + 1) % block.Inner.Count;
                Vec2 i0 = block.Inner[e], i1 = block.Inner[f], o0 = block.Outer[e], o1 = block.Outer[f];
                Vec2 toRoad = (i0 + i1 - o0 - o1).Normalized;
                foreach ((double t0, double t1, double top) in SteppedSidewalks.Treads(ctx.Plan, block, e))
                {
                    Vec2 a = i0 + (i1 - i0) * t0, kb = i0 + (i1 - i0) * t1, c = o0 + (o1 - o0) * t1, d = o0 + (o1 - o0) * t0;
                    Vec2 middle = (a + kb + c + d) * 0.25;
                    if (!ctx.Contains(middle.X, middle.Y)) continue;
                    // Down below the road at the kerb and the ground at its corners, so its face to the road is the kerb there.
                    double low = top;
                    foreach (Vec2 p in new[] { a, kb, c, d }) low = System.Math.Min(low, ctx.Plan.GroundHeight(p.X, p.Y));
                    AddTreadBlock(ctx, buffer, a, kb, c, d, top, low - SidewalkStepFooting, SidewalkStepLook, SidewalkStepLook);
                    // Beyond the lot line, where the paving there stands over the tread: a retaining face up to it, facing the street.
                    double pc = ctx.Plan.GroundHeight(c.X, c.Y) + PlanBuildings.PavingRise, pd = ctx.Plan.GroundHeight(d.X, d.Y) + PlanBuildings.PavingRise;
                    if (pc <= top + 0.02 && pd <= top + 0.02) continue;
                    buffer.Look = StairLaneRetaining;
                    Vector3 d0 = ctx.Local(d.X, top, d.Y), c0 = ctx.Local(c.X, top, c.Y);
                    Vector3 d1 = ctx.Local(d.X, System.Math.Max(pd, top), d.Y), c1 = ctx.Local(c.X, System.Math.Max(pc, top), c.Y);
                    float len = (float)(c - d).Length;
                    buffer.AddQuad(d0, c0, c1, d1, new Vector3((float)toRoad.X, 0.0f, (float)toRoad.Y),
                        new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1), new Vector2(0, 1));
                }
            }
        }

        /// <summary>Whether a point on a block's pavement is in front of either end of the block's stairway (its furniture keeps clear).</summary>
        private static bool BeforeStairway(Context ctx, PlanBlock block, Vec2 at)
        {
            if (block.StairLane < 0) return false;
            StairLane lane = ctx.Plan.Data.StairLanes[block.StairLane];
            double reach = lane.Width * 0.5 + 1.5;
            return (at - lane.A).Length < reach || (at - lane.B).Length < reach;
        }
    }
}

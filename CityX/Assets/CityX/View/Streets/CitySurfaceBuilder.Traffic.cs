using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// STOP SIGNS AND TRAFFIC LIGHTS ON THE STREET (docs/TRAFFIC_CONTROL.md; owner, 2026-11-05: "realistically placed traffic lights and stop signs throughout the
    /// city"). World/TrafficPlan says which junction gets what; this stands the hardware where the American standard (the MUTCD) puts it:
    ///
    ///  - a STOP sign on the right of its approach, at the stop line, before the crossing; the stop line 4 ft before the crossing;
    ///  - a light's heads on the FAR side of the junction from the traffic they face, two or more to an approach: on a side street one on the pole at each far
    ///    corner; on a main road two more over the lanes on a mast arm from the far right corner;
    ///  - walk signals at each end of every crossing, facing along it.
    ///
    /// Each piece is built by the chunk its pole or post stands in.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>The zebra's near and far edge from the junction's plate, along the road, metres.</summary>
        public const double CrossingFrom = 0.6, CrossingTo = 3.9;

        /// <summary>A stop line: 12 to 24 in wide, 4 ft before the crossing (MUTCD 3B.16), at the city's 1.3 times life; or this far from the plate where no crossing is painted.</summary>
        public const double StopLineWidth = 0.5;

        public const double StopLineBeforeCrossing = 1.6;
        public const double StopLineFromPlate = 1.2;

        /// <summary>A sign's post and a light's pole stand this far behind the kerb (2 ft clear of the road).</summary>
        public const double SignFromKerb = 0.8;

        /// <summary>A sign beside a rounded corner stands this far past the end of the arc, along the straight kerb, metres.</summary>
        public const double SignPastArc = 0.3;

        public const double PoleFromKerb = 0.9;

        /// <summary>A post or pole keeps this clear of every kerb of its junction, metres.</summary>
        private const double ClearOfKerb = 0.3;

        /// <summary>Nothing stands where the ground is this far under its junction, metres: a bridge's end, a bank.</summary>
        private const double DropMetres = 1.5;

        /// <summary>A crossing's walk signal hangs on the corner's pole when that is within this of the crossing's end, metres; otherwise on a pole of its own there.</summary>
        private const double CornerServesCrossing = 7.0;

        /// <summary>Where an approach has no kerb corner on its far side, its own pole is looked for this far on past the junction, metres, until it is clear of the roads.</summary>
        private const double FarSideSearch = 14.0;

        /// <summary>A junction's hardware can stand this many chunks from the chunks its plate touches.</summary>
        private const int TrafficReachChunks = 2;

        private const int ZebraSalt = 41;
        private const float ZebraShare = 0.7f;

        [ThreadStatic] private static HashSet<int> _trafficSeen;

        /// <summary>
        /// Whether a road's end (0: at its A, 1: at its B) has a zebra painted across it. At lights, always. Where the side street stops for a main street, never across
        /// the main street (nothing there stops for a walker). Elsewhere at most of the corners, as before there were signs.
        /// </summary>
        public static bool HasCrossing(PlanWorld plan, EdgeGeometry g, int end)
        {
            double trim = end == 0 ? g.TrimA : g.TrimB;
            if (trim < 1.0 || g.Length - trim - CrossingTo < 0.5)
            {
                return false;
            }
            if (plan.Traffic.TryApproach(g.Edge, end == 0, out TrafficJunction junction, out int leg))
            {
                if (junction.Signalled(leg))
                {
                    return true;
                }
                if (junction.Control == JunctionControl.StopMinor && !junction.Stops(leg))
                {
                    return false;
                }
            }
            return LatticeHash(g.Edge.Id, end, ZebraSalt) <= ZebraShare;
        }

        /// <summary>How far from its node a road's end has its stop line (the line's edge nearer the junction); negative where nothing stops there, or the road is too short for one.</summary>
        public static double StopLineAt(PlanWorld plan, EdgeGeometry g, int end)
        {
            if (!plan.Traffic.TryApproach(g.Edge, end == 0, out TrafficJunction junction, out int leg) || !(junction.Stops(leg) || junction.Signalled(leg)))
            {
                return -1.0;
            }
            double trim = end == 0 ? g.TrimA : g.TrimB;
            double from = trim + (HasCrossing(plan, g, end) ? CrossingTo + StopLineBeforeCrossing : StopLineFromPlate);
            return from + StopLineWidth <= g.Length * 0.5 ? from : -1.0;
        }

        /// <summary>The stop line across the half of the road that comes in to the junction at that end.</summary>
        private static void StopLine(Context ctx, EdgeGeometry g, MeshBuffer white, Func<double, double, double> height, double at, int end)
        {
            if (white == null)
            {
                return;
            }
            // Traffic keeps right: coming in to A it is on the road's left as the road runs from A to B, and coming in to B on its right.
            double side = end == 0 ? 1.0 : -1.0;
            double ta = end == 0 ? at : g.Length - at - StopLineWidth;
            Stripe(ctx, g, white, height, side * g.HalfWidth * 0.5, g.HalfWidth * 0.5 - 0.3, ta, ta + StopLineWidth);
        }

        /// <summary>The signs and lights of every junction near enough for a piece of it to stand in this chunk.</summary>
        private static void BuildTrafficControl(Context ctx, int chunkX, int chunkY, MeshBuffer structures, SignalLampBuffer lamps)
        {
            TrafficPlan traffic = ctx.Plan.Traffic;
            HashSet<int> seen = _trafficSeen ??= new HashSet<int>();
            seen.Clear();
            for (int dy = -TrafficReachChunks; dy <= TrafficReachChunks; dy++)
            {
                for (int dx = -TrafficReachChunks; dx <= TrafficReachChunks; dx++)
                {
                    foreach (int nodeId in ctx.Plan.JunctionsIn(chunkX + dx, chunkY + dy))
                    {
                        if (!seen.Add(nodeId))
                        {
                            continue;
                        }
                        TrafficJunction junction = traffic.Junctions[nodeId];
                        if (junction == null || junction.Control == JunctionControl.None)
                        {
                            continue;
                        }
                        if (junction.Control == JunctionControl.Signal)
                        {
                            if (lamps != null)
                            {
                                BuildSignals(ctx, junction, structures, lamps);
                            }
                        }
                        else
                        {
                            BuildStopSigns(ctx, junction, structures);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Whether a post or pole of a junction can stand at a point: not in any of the junction's roads (on a street too short for its stop line, or where roads
        /// meet at a slant, the place worked out for one can fall on the asphalt), not over a drop (a bridge's end), not on Old Guy's land.
        /// </summary>
        private static bool CanStand(PlanWorld plan, TrafficJunction junction, Vec2 at)
        {
            if (plan.OnTutorialLot(at.X, at.Y, 1.0))
            {
                return false;
            }
            Vec2 from = at - junction.Node.P;
            for (int leg = 0; leg < junction.Legs.Length; leg++)
            {
                double along = Vec2.Dot(from, junction.Dirs[leg]);
                if (along > 0.0 && along < junction.Legs[leg].Length
                    && Math.Abs(Vec2.Dot(from, junction.Dirs[leg].Perp)) < junction.HalfWidths[leg] + ClearOfKerb)
                {
                    return false;
                }
            }
            return plan.GroundHeight(at.X, at.Y) > plan.GroundHeight(junction.Node.P.X, junction.Node.P.Y) - DropMetres;
        }

        /// <summary>Whether this chunk builds what stands at a point of the pavement, and where that is in the chunk.</summary>
        private static bool Stands(Context ctx, TrafficJunction junction, Vec2 at, out Vector3 local)
        {
            local = default;
            if (!ctx.Contains(at) || !CanStand(ctx.Plan, junction, at))
            {
                return false;
            }
            local = ctx.Local(at.X, ctx.Plan.GroundHeight(at.X, at.Y) + RoadLift + KerbHeight, at.Y);
            return true;
        }

        private static void BuildStopSigns(Context ctx, TrafficJunction junction, MeshBuffer structures)
        {
            for (int i = 0; i < junction.Legs.Length; i++)
            {
                if (!junction.Stops(i))
                {
                    continue;
                }
                EdgeGeometry g = ctx.Plan.Geometry.Edges[junction.Legs[i].Id];
                int end = junction.Legs[i].A == junction.Node ? 0 : 1;
                double line = StopLineAt(ctx.Plan, g, end);
                double along = line >= 0.0 ? line + StopLineWidth * 0.5 : junction.Trims[i] + StopLineFromPlate;
                Vec2 d = junction.Dirs[i];
                // Where the kerb goes round the corner on the sign's side in an arc (KerbCorners), the sign stands where the straight kerb
                // begins: nearer the junction it would be out in the road the arc gives up.
                if (junction.HasCorner[i] && KerbCorners.TryAt(ctx.Plan, junction.Corners[i], out KerbArc arc))
                {
                    along = Math.Max(along, Vec2.Dot(arc.Corner - junction.Node.P, d) + arc.TangentLength + SignPastArc);
                }
                Vec2 at = junction.Node.P + d * along + d.Perp * (junction.HalfWidths[i] + SignFromKerb);
                if (!Stands(ctx, junction, at, out Vector3 local))
                {
                    continue;
                }
                float size = junction.Legs[i].Class == RoadClass.Street ? TrafficProps.StopSignSize : TrafficProps.WideStopSignSize;
                TrafficProps.AddStopSign(structures, local, d, size, junction.Control == JunctionControl.AllWayStop);
            }
        }

        /// <summary>A junction's poles as its lights are hung: the ones at its kerb corners, and the ones stood where no corner served.</summary>
        private ref struct SignalPoles
        {
            public Span<bool> CornerUsed, CornerArmed;
            public Span<Vec2> Extra;
            public Span<bool> ExtraArmed;
            public int ExtraCount;

            /// <summary>A pole of its own at a place, or the one already stood within a stride of it.</summary>
            public Vec2 Own(Vec2 at, bool armed)
            {
                for (int e = 0; e < ExtraCount; e++)
                {
                    if ((Extra[e] - at).Length < 1.5)
                    {
                        ExtraArmed[e] |= armed;
                        return Extra[e];
                    }
                }
                if (ExtraCount >= Extra.Length)
                {
                    return at;
                }
                Extra[ExtraCount] = at;
                ExtraArmed[ExtraCount] = armed;
                ExtraCount++;
                return at;
            }
        }

        private static void BuildSignals(Context ctx, TrafficJunction junction, MeshBuffer structures, SignalLampBuffer lamps)
        {
            int k = junction.Legs.Length;
            var poles = new SignalPoles
            {
                CornerUsed = stackalloc bool[k],
                CornerArmed = stackalloc bool[k],
                Extra = stackalloc Vec2[k * 4],
                ExtraArmed = stackalloc bool[k * 4],
            };
            SignalTiming timing = junction.Timing;

            // The heads, on the far side of the junction from the traffic they face.
            for (int j = 0; j < k; j++)
            {
                if (!junction.Drives[j])
                {
                    continue;
                }
                Vec2 facing = junction.Dirs[j];          // the heads look back down the approach
                bool major = junction.Major[j];
                bool mainRoad = junction.Legs[j].Class >= RoadClass.Arterial;
                bool hasRight = FarSidePole(ctx.Plan, junction, j, true, mainRoad, ref poles, out Vec2 right, out bool rightIsOwn);
                bool hasLeft = FarSidePole(ctx.Plan, junction, j, false, false, ref poles, out Vec2 left, out _);

                if (hasRight && Stands(ctx, junction, right, out Vector3 rightLocal))
                {
                    SideHead(structures, lamps, rightLocal, facing, timing, major);
                    if (mainRoad || rightIsOwn)
                    {
                        // Over the lanes on a mast arm: two heads on a main road, one where a side street ends at the far kerb.
                        Vec2 over = facing.Perp * -1.0;
                        double half = junction.HalfWidths[j];
                        float length = (float)(PoleFromKerb + half * (mainRoad ? 0.85 : 0.55));
                        TrafficProps.AddMastArm(structures, rightLocal, over, length);
                        ArmHead(structures, lamps, rightLocal, over, (float)(PoleFromKerb + half * (mainRoad ? 0.75 : 0.5)), facing, timing, major);
                        if (mainRoad)
                        {
                            ArmHead(structures, lamps, rightLocal, over, (float)(PoleFromKerb + half * 0.3), facing, timing, major);
                        }
                    }
                }
                if (hasLeft && (!hasRight || (left - right).Length > 1.0) && Stands(ctx, junction, left, out Vector3 leftLocal))
                {
                    SideHead(structures, lamps, leftLocal, facing, timing, major);
                }
            }

            // The walk signals: at each end of every crossing, facing along it to those waiting at the other end.
            for (int i = 0; i < k; i++)
            {
                if (!junction.Drives[i])
                {
                    continue;
                }
                EdgeGeometry g = ctx.Plan.Geometry.Edges[junction.Legs[i].Id];
                if (!HasCrossing(ctx.Plan, g, junction.Legs[i].A == junction.Node ? 0 : 1))
                {
                    continue;
                }
                Vec2 d = junction.Dirs[i];
                Vec2 middle = junction.Node.P + d * (junction.Trims[i] + (CrossingFrom + CrossingTo) * 0.5);
                bool crossesMain = junction.Major[i];
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec2 end = middle + d.Perp * (side * (junction.HalfWidths[i] + PoleFromKerb));
                    // The corner beside this end of the crossing: on the leg's left it is the corner after the leg, on its right the one before.
                    int corner = side > 0 ? i : (i + k - 1) % k;
                    Vec2 pole;
                    if (junction.HasCorner[corner] && (CornerPole(ctx.Plan, junction, corner) - end).Length < CornerServesCrossing)
                    {
                        pole = CornerPole(ctx.Plan, junction, corner);
                        poles.CornerUsed[corner] = true;
                    }
                    else if (CanStand(ctx.Plan, junction, end))
                    {
                        pole = poles.Own(end, false);
                    }
                    else
                    {
                        continue;
                    }
                    if (Stands(ctx, junction, pole, out Vector3 local))
                    {
                        WalkHead(structures, lamps, local, d.Perp * -side, timing, crossesMain);
                    }
                }
            }

            for (int c = 0; c < k; c++)
            {
                if (poles.CornerUsed[c] && Stands(ctx, junction, CornerPole(ctx.Plan, junction, c), out Vector3 local))
                {
                    TrafficProps.AddPole(structures, local, poles.CornerArmed[c]);
                }
            }
            for (int e = 0; e < poles.ExtraCount; e++)
            {
                if (Stands(ctx, junction, poles.Extra[e], out Vector3 local))
                {
                    TrafficProps.AddPole(structures, local, poles.ExtraArmed[e]);
                }
            }
        }

        /// <summary>
        /// Where the pole at a kerb corner stands: back from the kerb into the pavement. Where the kerb goes round the corner in an arc
        /// (KerbCorners) that is back from the arc's middle, not from the sharp corner the plan has, which is out in the road.
        /// </summary>
        private static Vec2 CornerPole(PlanWorld plan, TrafficJunction junction, int corner)
        {
            if (KerbCorners.TryAt(plan, junction.Corners[corner], out KerbArc arc))
            {
                return arc.Apex + arc.Inward * PoleFromKerb;
            }
            Vec2 inward = junction.Dirs[corner] + junction.Dirs[(corner + 1) % junction.Legs.Length];
            return junction.Corners[corner] + (inward.Length > 1e-6 ? inward.Normalized : junction.Dirs[corner].Perp) * PoleFromKerb;
        }

        /// <summary>
        /// The pole on the far side of the junction for traffic coming in along a leg, on its right or its left: the kerb corner that stands beyond the junction on
        /// that side, the one nearest where the pole belongs; or, where there is none (a street that ends at another faces one unbroken kerb), a pole of the
        /// approach's own on the far pavement. False where nothing can stand there.
        /// </summary>
        private static bool FarSidePole(PlanWorld plan, TrafficJunction junction, int leg, bool onRight, bool armed, ref SignalPoles poles, out Vec2 pole, out bool own)
        {
            Vec2 travel = junction.Dirs[leg] * -1.0;
            Vec2 right = junction.Dirs[leg].Perp * (onRight ? 1.0 : -1.0);
            double across = 0.0;
            for (int m = 0; m < junction.Legs.Length; m++)
            {
                if (m != leg && junction.Drives[m])
                {
                    across = Math.Max(across, junction.HalfWidths[m] * Math.Abs(Vec2.Cross(junction.Dirs[m], travel)));
                }
            }
            Vec2 wanted = junction.Node.P + right * (junction.HalfWidths[leg] + PoleFromKerb) + travel * (across + PoleFromKerb);

            int best = -1;
            double nearest = double.MaxValue;
            for (int c = 0; c < junction.Legs.Length; c++)
            {
                if (!junction.HasCorner[c])
                {
                    continue;
                }
                Vec2 from = CornerPole(plan, junction, c) - junction.Node.P;
                if (Vec2.Dot(from, travel) <= 0.0 || Vec2.Dot(from, right) <= 0.0)
                {
                    continue;   // on the near side, or the other side of the road
                }
                double away = (CornerPole(plan, junction, c) - wanted).Length;
                if (away < nearest)
                {
                    nearest = away;
                    best = c;
                }
            }
            own = best < 0;
            if (best >= 0)
            {
                poles.CornerUsed[best] = true;
                poles.CornerArmed[best] |= armed;
                pole = CornerPole(plan, junction, best);
                return true;
            }

            // No corner there: on along the approach's own kerb line until the pavement beyond the junction.
            for (double further = 0.0; further <= FarSideSearch; further += 1.0)
            {
                Vec2 at = wanted + travel * further;
                if (CanStand(plan, junction, at))
                {
                    pole = poles.Own(at, onRight);   // a pole of an approach's own carries that approach's arm
                    return true;
                }
            }
            pole = wanted;
            return false;
        }

        private static void SideHead(MeshBuffer structures, SignalLampBuffer lamps, Vector3 pole, Vec2 facing, SignalTiming timing, bool major)
        {
            var at = new Vector3(pole.x + (float)facing.X * 0.3f, pole.y + TrafficProps.SideHeadBottom, pole.z + (float)facing.Y * 0.3f);
            TrafficProps.AddHead(structures, lamps, at, facing, false, timing, major);
        }

        private static void ArmHead(MeshBuffer structures, SignalLampBuffer lamps, Vector3 pole, Vec2 over, float along, Vec2 facing, SignalTiming timing, bool major)
        {
            var at = new Vector3(pole.x + (float)over.X * along + (float)facing.X * 0.12f, pole.y + TrafficProps.ArmHeight - 0.1f - TrafficProps.HeadHeight,
                pole.z + (float)over.Y * along + (float)facing.Y * 0.12f);
            TrafficProps.AddHead(structures, lamps, at, facing, true, timing, major);
        }

        private static void WalkHead(MeshBuffer structures, SignalLampBuffer lamps, Vector3 pole, Vec2 facing, SignalTiming timing, bool crossesMain)
        {
            var at = new Vector3(pole.x + (float)facing.X * 0.36f, pole.y + TrafficProps.WalkHeadBottom, pole.z + (float)facing.Y * 0.36f);
            TrafficProps.AddWalkHead(structures, lamps, at, facing, timing, crossesMain);
        }
    }
}

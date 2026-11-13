using System;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// THE WATER IN THE SEWERS (docs/SEWERS.md; owner, 2026-11-09: water "ankle deep mostly, up to waist deep in some areas"; 2026-11-13: "add
    /// water to the sewers right now there isnt any"). Like the tunnels it is a pure function of the plan: every machine pours the same water,
    /// and nothing of it is sent.
    ///
    /// A room's water is ankle deep over its floor, or waist deep in a flooded neighbourhood (one in <see cref="FloodedShare"/>, a few
    /// hundred metres of it). A tunnel's water runs from the depth in the room at one end to the depth in the room at the other, so a
    /// flooded quarter deepens along the tunnels into it, never in a step. A mouth's tunnel drains: its water falls to the channel alone by
    /// the headwall. The surface follows the floor, as running water down a sloping street's tunnel does.
    /// </summary>
    public sealed partial class SewerPlan
    {
        /// <summary>The water over the walkways almost everywhere: round a 2.6 m body's ankles (its legs are a 1.8 m person's).</summary>
        public const double AnkleDeep = 0.14;

        /// <summary>The water over the walkways in a flooded neighbourhood: at the body's waist.</summary>
        public const double WaistDeep = 0.95;

        /// <summary>The share of neighbourhoods whose sewers are flooded waist deep.</summary>
        public const double FloodedShare = 0.2;

        /// <summary>How big a neighbourhood that floods together is, metres (at the classic scale).</summary>
        public const double FloodCell = 240.0;

        /// <summary>A mouth's water by its headwall: under the walkways, in the channel only.</summary>
        public const double DrainedAtMouth = -0.12;

        private void PourWater()
        {
            foreach (SewerRoom room in _rooms)
            {
                if (room != null) room.Water = WaterDepthAt(room.Node.P);
            }
            for (int edgeId = 0; edgeId < _tubes.Length; edgeId++)
            {
                SewerTube tube = _tubes[edgeId];
                if (tube == null) continue;
                RoadEdge edge = _plan.Data.Edges[edgeId];
                double own = WaterDepthAt((tube.At(tube.Start) + tube.At(tube.End)) * 0.5);
                tube.WaterStart = Room(edge.A.Id)?.Water ?? own;
                tube.WaterEnd = Room(edge.B.Id)?.Water ?? own;
            }
            foreach (SewerOutfall outfall in AllOutfalls)
            {
                outfall.Tube.WaterStart = Room(outfall.From.Id)?.Water ?? AnkleDeep;
                outfall.Tube.WaterEnd = DrainedAtMouth;
            }
        }

        /// <summary>How deep the water stands over the floor in the neighbourhood of a place: ankle deep, or waist deep where it is flooded.</summary>
        public double WaterDepthAt(Vec2 place)
        {
            double cell = FloodCell * _plan.Data.Config.Scale;
            int cx = (int)Math.Floor(place.X / cell), cy = (int)Math.Floor(place.Y / cell);
            return WorldHash.Unit01(cx, cy, Salt + 13UL, unchecked((ulong)(long)_plan.Seed)) < FloodedShare ? WaistDeep : AnkleDeep;
        }

        /// <summary>How deep a tunnel's water is over its walkways a distance along it.</summary>
        public static double WaterAlong(SewerTube tube, double along)
        {
            double t = PlanMath.Clamp01((along - tube.Start) / Math.Max(1e-9, tube.End - tube.Start));
            return tube.WaterStart + (tube.WaterEnd - tube.WaterStart) * t;
        }

        /// <summary>
        /// The water over a point in the sewers: its surface and its bottom (the channel's bed, or the floor), or false where the point is in
        /// no tunnel or room. A point above the surface is still "in" it (its depth is then negative): the caller takes the depth.
        /// </summary>
        public bool TryWaterAt(double x, double y, double z, out double surface, out double bottom)
        {
            var p = new Vec2(x, z);
            int cx = PlanWorld.ChunkOf(x), cy = PlanWorld.ChunkOf(z);
            foreach (int edgeId in _plan.EdgesIn(cx, cy))
            {
                if (TryTubeWater(Tube(edgeId), p, out surface, out bottom)) return true;
                foreach (SewerOutfall outfall in Outfalls(edgeId))
                {
                    if (TryTubeWater(outfall.Tube, p, out surface, out bottom)) return true;
                }
            }
            foreach (int nodeId in _plan.JunctionsIn(cx, cy))
            {
                SewerRoom room = Room(nodeId);
                if (room != null && TryRoomFloor(room, p, out double floor))
                {
                    bottom = floor;
                    surface = floor + room.Water;
                    return true;
                }
            }
            surface = bottom = 0.0;
            return false;
        }

        private static bool TryTubeWater(SewerTube tube, Vec2 p, out double surface, out double bottom)
        {
            surface = bottom = 0.0;
            if (tube == null) return false;
            double along = Vec2.Dot(p - tube.Origin, tube.Dir);
            if (along < tube.Start || along > tube.End) return false;
            double across = Math.Abs(Vec2.Cross(tube.Dir, p - tube.Origin));
            double water = WaterAlong(tube, along);
            if (across > tube.Section.HalfWidthAt(Math.Max(water, 0.01))) return false;
            double floor = tube.FloorAt(along);
            bool inChannel = across <= tube.Section.ChannelHalfWidth;
            if (!inChannel && water <= 0.0) return false;   // the walkway beside a drained channel is dry
            bottom = inChannel ? floor - tube.Section.ChannelDepth : floor;
            surface = floor + water;
            return true;
        }

        /// <summary>The floor of a room under a point of its plate (each wedge from its middle to a side is a plane, as it is built).</summary>
        public static bool TryRoomFloor(SewerRoom room, Vec2 p, out double floor)
        {
            Vec2 centre = room.Node.P;
            int n = room.Plate.Length;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vec2 a = room.Plate[i], b = room.Plate[next];
                double area = Vec2.Cross(a - centre, b - centre);
                if (Math.Abs(area) < 1e-9) continue;
                double u = Vec2.Cross(p - centre, b - centre) / area;   // the share of a
                double v = Vec2.Cross(a - centre, p - centre) / area;   // the share of b
                if (u < -1e-6 || v < -1e-6 || u + v > 1.0 + 1e-6) continue;
                floor = room.CentreFloor + (room.Floor[i] - room.CentreFloor) * u + (room.Floor[next] - room.CentreFloor) * v;
                return true;
            }
            floor = 0.0;
            return false;
        }
    }
}

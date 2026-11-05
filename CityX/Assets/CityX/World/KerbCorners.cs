using System;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>The arc a kerb takes round one street corner: from a point on the kerb coming in, round the corner, to a point on the kerb going out.</summary>
    public readonly struct KerbArc
    {
        /// <summary>Where the two kerb lines meet: the sharp corner the plan has.</summary>
        public readonly Vec2 Corner;

        /// <summary>The arc's centre, inside the block.</summary>
        public readonly Vec2 Centre;

        /// <summary>Where the arc leaves the kerb coming in to the corner, and where it joins the kerb going out (in the block's own order).</summary>
        public readonly Vec2 From, To;

        public readonly double Radius;

        public KerbArc(Vec2 corner, Vec2 centre, Vec2 from, Vec2 to, double radius)
        {
            Corner = corner;
            Centre = centre;
            From = from;
            To = to;
            Radius = radius;
        }

        /// <summary>How far along each kerb from the sharp corner the arc begins.</summary>
        public double TangentLength => (From - Corner).Length;

        /// <summary>How far the kerb has drawn back from the sharp corner, at the middle of the arc.</summary>
        public double SetBack => (Corner - Centre).Length - Radius;

        /// <summary>The middle of the arc: the kerb's corner as it is built.</summary>
        public Vec2 Apex => Centre + (Corner - Centre).Normalized * Radius;

        /// <summary>The way into the pavement from the corner (a unit vector).</summary>
        public Vec2 Inward => (Centre - Corner).Normalized;

        /// <summary>A point of the arc: 0 at <see cref="From"/>, 1 at <see cref="To"/>.</summary>
        public Vec2 PointAt(double share)
        {
            double a0 = Math.Atan2(From.Y - Centre.Y, From.X - Centre.X);
            double a1 = Math.Atan2(To.Y - Centre.Y, To.X - Centre.X);
            double turn = a1 - a0;
            while (turn > Math.PI) turn -= 2.0 * Math.PI;
            while (turn < -Math.PI) turn += 2.0 * Math.PI;
            double a = a0 + turn * share;
            return new Vec2(Centre.X + Math.Cos(a) * Radius, Centre.Y + Math.Sin(a) * Radius);
        }

        /// <summary>How many straight pieces the arc is built of: one for about every 15 degrees it turns.</summary>
        public int Pieces
        {
            get
            {
                double a0 = Math.Atan2(From.Y - Centre.Y, From.X - Centre.X);
                double a1 = Math.Atan2(To.Y - Centre.Y, To.X - Centre.X);
                double turn = Math.Abs(a1 - a0);
                if (turn > Math.PI) turn = 2.0 * Math.PI - turn;
                return Math.Max(3, (int)Math.Ceiling(turn / (Math.PI / 12.0)));
            }
        }

        /// <summary>
        /// Whether a point of the block's pavement, as the plan has it, is in the part this corner gives up to the road: beyond both ends of
        /// the arc toward the corner, and outside the arc.
        /// </summary>
        public bool GivesUp(Vec2 point)
        {
            return Vec2.Dot(point - From, Corner - From) > 0.0 && Vec2.Dot(point - To, Corner - To) > 0.0 && (point - Centre).Length > Radius;
        }
    }

    /// <summary>
    /// THE KERB ROUND A STREET CORNER (docs/AMERICAN_STREETS.md). On an American street the kerb does not turn a corner at a point: it goes
    /// round in an arc (a curb return) of 10 to 15 feet on city streets, more where bigger roads meet. The plan's blocks keep their sharp
    /// corners: lots, pavements and junction plates are all cut from those. This says what the built street does with each corner, as a
    /// pure rule of the plan, so the surface (CitySurfaceBuilder.Corners.cs) and whatever stands at a corner (the lights' poles) agree.
    /// </summary>
    public static class KerbCorners
    {
        /// <summary>The arc's radius by the smaller of the two roads that meet, metres (real sizes x 1.3: 10 ft, 15 ft, 19 ft).</summary>
        public const double StreetRadius = 4.0;

        public const double ArterialRadius = 6.0;
        public const double BoulevardRadius = 7.5;

        /// <summary>A corner that could only take an arc smaller than this is left sharp.</summary>
        public const double LeastRadius = 1.5;

        /// <summary>Only a real corner is rounded: sharper than this the block ends in a point, blunter and the road merely bends, degrees.</summary>
        public const double SharpestDegrees = 55.0;

        public const double BluntestDegrees = 135.0;

        /// <summary>The arc takes at most this share of either kerb it joins, and no more than <see cref="LongestTangent"/> metres of it (what stands along a pavement keeps 8 m off the corner).</summary>
        public const double MostOfAKerb = 0.3;

        public const double LongestTangent = 7.0;

        /// <summary>The pavement left between the arc's middle and the corner of the lots, metres.</summary>
        public const double PavementLeft = 1.5;

        /// <summary>A corner is the block's own when it lies this near one of its kerb's vertices, metres.</summary>
        private const double SamePlace = 0.25;

        public static double RadiusFor(RoadClass roadClass)
        {
            switch (roadClass)
            {
                case RoadClass.Boulevard: return BoulevardRadius;
                case RoadClass.Arterial: return ArterialRadius;
                default: return StreetRadius;
            }
        }

        /// <summary>The arc round one vertex of a block's kerb, or false where the kerb keeps its sharp corner.</summary>
        public static bool TryAt(PlanWorld plan, PlanBlock block, int vertex, out KerbArc arc)
        {
            arc = default;
            if (block.Inner == null || block.Outer == null)
            {
                return false;
            }
            int n = block.Poly.Count;
            int prev = (vertex + n - 1) % n, next = (vertex + 1) % n;
            RoadEdge into = plan.EdgeBetween(block.Poly[prev], block.Poly[vertex]);
            RoadEdge outOf = plan.EdgeBetween(block.Poly[vertex], block.Poly[next]);
            if (!Kerbed(plan, into) || !Kerbed(plan, outOf))
            {
                return false;
            }
            Vec2 corner = block.Inner[vertex];
            Vec2 back = block.Inner[prev] - corner, on = block.Inner[next] - corner;
            double backLength = back.Length, onLength = on.Length;
            if (backLength < 1e-6 || onLength < 1e-6)
            {
                return false;
            }
            back = back * (1.0 / backLength);
            on = on * (1.0 / onLength);
            // The block is counter-clockwise: at a corner of its own the kerb turns left. Where it turns right the block has a notch, and the
            // corner there is the road's, not the pavement's.
            if (Vec2.Cross(back * -1.0, on) <= 0.0)
            {
                return false;
            }
            double angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, Vec2.Dot(back, on))));
            if (angle < SharpestDegrees * Math.PI / 180.0 || angle > BluntestDegrees * Math.PI / 180.0)
            {
                return false;
            }
            double half = angle * 0.5;
            double radius = Math.Min(RadiusFor(into.Class), RadiusFor(outOf.Class));
            double tangentMost = Math.Min(LongestTangent, Math.Min(backLength, onLength) * MostOfAKerb);
            radius = Math.Min(radius, tangentMost * Math.Tan(half));
            double room = (block.Outer[vertex] - corner).Length - PavementLeft;
            radius = Math.Min(radius, room / (1.0 / Math.Sin(half) - 1.0));
            if (radius < LeastRadius)
            {
                return false;
            }
            double tangent = radius / Math.Tan(half);
            Vec2 inward = (back + on).Normalized;
            arc = new KerbArc(corner, corner + inward * (radius / Math.Sin(half)), corner + back * tangent, corner + on * tangent, radius);
            return true;
        }

        /// <summary>The arc round the kerb corner at a place (a junction's corner, say), or false where there is none or it is sharp.</summary>
        public static bool TryAt(PlanWorld plan, Vec2 corner, out KerbArc arc)
        {
            foreach (int index in plan.BlocksIn(PlanWorld.ChunkOf(corner.X), PlanWorld.ChunkOf(corner.Y)))
            {
                PlanBlock block = plan.Data.Blocks[index];
                if (block.Inner == null)
                {
                    continue;
                }
                for (int v = 0; v < block.Inner.Count; v++)
                {
                    if ((block.Inner[v] - corner).Length < SamePlace)
                    {
                        return TryAt(plan, block, v, out arc);
                    }
                }
            }
            arc = default;
            return false;
        }

        /// <summary>A road with a kerb along it: a street that is driven, on the ground.</summary>
        private static bool Kerbed(PlanWorld plan, RoadEdge road)
        {
            return road != null && !road.Bridge && !PedestrianStreets.Is(plan, road) && !StepStreets.Is(plan, road);
        }
    }
}

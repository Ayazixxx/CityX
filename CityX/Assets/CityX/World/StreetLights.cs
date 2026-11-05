using System;
using System.Collections.Generic;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>One street light: where its pole stands on the pavement, which way its arm reaches (over the road), how tall it is, and whether it has power.</summary>
    public readonly struct StreetLamp
    {
        public readonly Vec2 At;

        /// <summary>From the pole toward the middle of the road, a unit vector: the way the arm reaches.</summary>
        public readonly Vec2 Over;

        public readonly float Height;
        public readonly float Arm;
        public readonly bool Powered;

        public StreetLamp(Vec2 at, Vec2 over, float height, float arm, bool powered)
        {
            At = at;
            Over = over;
            Height = height;
            Arm = arm;
            Powered = powered;
        }

        /// <summary>Where its lamp hangs: out along the arm, at its height over the pole's foot.</summary>
        public Vec2 Head => At + Over * Arm;
    }

    /// <summary>
    /// THE STREET LIGHTS (docs/AMERICAN_STREETS.md): cobra-head lamps on tall poles with an arm over the road, as American streets have
    /// them, staggered from one side of the street to the other. A pure rule of the plan: every machine stands the same poles, and the
    /// lights that are lent to the nearest ones at night (View/StreetLampDirector) are found from the same rule, with nothing sent.
    /// MOST ARE DEAD: the city has little power (one building in ten, RoomLighting), and the nights are meant to be near black. A street
    /// in three has current for its lamps.
    /// </summary>
    public static class StreetLights
    {
        /// <summary>From one lamp to the next along a street, whichever side each is on, metres (real: 100 to 150 ft on a side street, closer on a main road; x 1.3).</summary>
        public const double StreetSpacing = 44.0;

        public const double ArterialSpacing = 36.0;
        public const double BoulevardSpacing = 32.0;

        /// <summary>No lamp nearer a junction's plate than this, metres: the corner's arc, its lights and its signs have that stretch.</summary>
        public const double FromJunction = 13.0;

        /// <summary>A street shorter than this between its junctions has no lamp of its own, metres.</summary>
        public const double ShortestLitStreet = 2.0 * FromJunction + 6.0;

        /// <summary>The pole's middle, in from the kerb, metres.</summary>
        public const double FromKerb = 0.75;

        /// <summary>The lamp's height and its arm's reach over the road, metres (25 ft and 30 ft poles x 1.3).</summary>
        public const float StreetHeight = 9.6f, MainHeight = 11.6f;

        public const float StreetArm = 2.2f, MainArm = 3.2f;

        /// <summary>The share of streets whose lamps have power.</summary>
        public const double PoweredShare = 0.34;

        private const ulong PowerSalt = 0x4C414D5053UL;   // "LAMPS"
        private const ulong SideSalt = 0x53494445UL;      // "SIDE"

        /// <summary>Whether a road has street lights: a street that is driven, on the ground.</summary>
        public static bool Has(PlanWorld plan, RoadEdge edge)
        {
            return edge != null && !edge.Bridge && !PedestrianStreets.Is(plan, edge) && !StepStreets.Is(plan, edge);
        }

        /// <summary>Whether a street's lamps come on at night.</summary>
        public static bool Powered(PlanWorld plan, RoadEdge edge)
        {
            return WorldHash.Unit01(edge.Id, 0, PowerSalt, unchecked((ulong)(long)plan.Seed)) < PoweredShare;
        }

        public static double SpacingFor(RoadClass roadClass)
        {
            switch (roadClass)
            {
                case RoadClass.Boulevard: return BoulevardSpacing;
                case RoadClass.Arterial: return ArterialSpacing;
                default: return StreetSpacing;
            }
        }

        /// <summary>The lamps along one road, added to <paramref name="into"/> in order from its A end; none where it has no lights.</summary>
        public static void Along(PlanWorld plan, EdgeGeometry g, List<StreetLamp> into)
        {
            if (g == null || !Has(plan, g.Edge))
            {
                return;
            }
            double from = g.TrimA + FromJunction, to = g.Length - g.TrimB - FromJunction;
            if (g.Length - g.TrimA - g.TrimB < ShortestLitStreet)
            {
                return;
            }
            bool main = g.Edge.Class != RoadClass.Street;
            // As many as keep every gap within the spacing; a street shorter than half of it has one lamp, at its middle.
            double span = to - from, spacing = SpacingFor(g.Edge.Class);
            int count = span < spacing * 0.5 ? 1 : (int)Math.Ceiling(span / spacing) + 1;
            bool powered = Powered(plan, g.Edge);
            int firstSide = WorldHash.Unit01(g.Edge.Id, 0, SideSalt, unchecked((ulong)(long)plan.Seed)) < 0.5 ? 1 : -1;
            for (int k = 0; k < count; k++)
            {
                double along = count == 1 ? (from + to) * 0.5 : from + (to - from) * k / (count - 1);
                int side = k % 2 == 0 ? firstSide : -firstSide;
                Vec2 aside = g.Dir.Perp * side;
                Vec2 at = g.A + g.Dir * along + aside * (g.HalfWidth + FromKerb);
                into.Add(new StreetLamp(at, aside * -1.0, main ? MainHeight : StreetHeight, main ? MainArm : StreetArm, powered));
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// Where the sky hub stands (docs/SKY_HUB.md; owner, 2026-11-02: "a giant circular sky island as high as the clouds. with a giant spiral staircase
    /// leading up to it that is the size of the circumference of the round island and spirals around as it leads between the island and floor. make
    /// the bottom of the staircase lead to a flat bit on the floor not clipping into any buildings or roads"). The flat bit is a lot inside one block,
    /// its long side on a street's lot line, as near the spawn as the island's ring leaves room for; the stair leaves it along the circle round the
    /// island. Whatever generated building the stair would pass through is not built (<see cref="PlanWorld"/>). A pure function of the plan, so every
    /// machine puts it in the same place.
    ///
    /// The stair is measured by s, metres along its middle line from its first step, rising at <see cref="Rise"/> a metre.
    /// </summary>
    public readonly struct SkyHubPlan
    {
        /// <summary>The island's top over the stair's foot, metres: cloud height (Character.CloudLayer.HeightMetres; the tests hold them together).</summary>
        public const double IslandHeight = 1500.0;

        public const double IslandRadius = 300.0;

        public const double StairWidth = 30.0;

        /// <summary>The gap between the island's edge and the stair's inner edge, so their faces are never one on the other.</summary>
        public const double StairGap = 0.25;

        /// <summary>How many times the stair goes round on its way up: at this radius, stairs as steep as a house's (28 degrees).</summary>
        public const double StairTurns = 1.5;

        /// <summary>About the steps of Old Guy's house and the city's buildings, so a body climbs them the same.</summary>
        private const double WantedRiser = 0.225;

        /// <summary>The flat ground before the first step, along the stair, metres.</summary>
        public const double LandingLength = 24.0;

        /// <summary>The flat ground beyond each side of the stair.</summary>
        public const double LandingSide = 4.0;

        /// <summary>The flat ground also runs on under the first of the stair, so nothing stands under it until it is this far along (8.5 m up).</summary>
        public const double LowRun = 16.0;

        /// <summary>How deep the stair is under its steps.</summary>
        public const double DeckThickness = 1.5;

        /// <summary>The stair's underside stays at least this far above the ground past the low run (a lamp post, a van, a body on another's shoulders).</summary>
        public const double ClearOverGround = 6.0;

        /// <summary>A generated building this close to the landing or under the stair is not built: room for what juts from its walls.</summary>
        public const double ClearOfBuildings = 2.0;

        /// <summary>The stair's underside stays this far above a building's top under it: what stands on a roof (tanks, signs, a head).</summary>
        public const double ClearOverRoofs = 10.0;

        /// <summary>No expressway runs under the stair before it is this far along (64 m up): their ramps and piers climb.</summary>
        public const double ClearOfExpressways = 120.0;

        /// <summary>The spawn and Old Guy's lot stay this far outside the stair's ring: a body falls from the sky to there.</summary>
        public const double ClearOfTheFall = 15.0;

        /// <summary>A footbridge is not hung under the stair before it is this far along: they are not built this high.</summary>
        private const double ClearOfBridges = 190.0;

        /// <summary>How far from the spawn the landing is looked for, metres.</summary>
        public const double SearchRadius = 600.0;

        /// <summary>The most the ground may rise and fall under the landing, metres (as Old Guy's lot).</summary>
        public const double MaxGroundSpread = 0.5;

        private const double FrontageStep = 2.0;
        private const double FrontInset = 0.05;
        private const double SampleMetres = 2.0;

        public static double StairInnerRadius => IslandRadius + StairGap;

        public static double StairOuterRadius => StairInnerRadius + StairWidth;

        public static double StairMidRadius => StairInnerRadius + StairWidth * 0.5;

        /// <summary>The stair's length along its middle line, in plan.</summary>
        public static double StairLength => 2.0 * Math.PI * StairMidRadius * StairTurns;

        public static int StepCount => (int)Math.Round(IslandHeight / WantedRiser);

        public static double Riser => IslandHeight / StepCount;

        /// <summary>How much the stair rises a metre along.</summary>
        public static double Rise => IslandHeight / StairLength;

        /// <summary>The landing's half width across the stair.</summary>
        public static double LandingHalfWidth => StairWidth * 0.5 + LandingSide;

        public readonly bool Found;

        /// <summary>The island's middle on the ground plan.</summary>
        public readonly double CentreX, CentreZ;

        /// <summary>The angle round the island of the first step, radians from the world's +x toward its +z.</summary>
        public readonly double StartAngle;

        /// <summary>+1 when the stair climbs with the angle increasing, else -1.</summary>
        public readonly int Direction;

        /// <summary>The world height of the landing's top, the stair's foot.</summary>
        public readonly double FootHeight;

        /// <summary>The lowest ground under the landing: its footing reaches down to it.</summary>
        public readonly double LowestGround;

        /// <summary>The block the landing is in.</summary>
        public readonly int Block;

        public SkyHubPlan(double centreX, double centreZ, double startAngle, int direction, double footHeight, double lowestGround, int block)
        {
            Found = true;
            CentreX = centreX;
            CentreZ = centreZ;
            StartAngle = startAngle;
            Direction = direction;
            FootHeight = footHeight;
            LowestGround = lowestGround;
            Block = block;
        }

        /// <summary>The world height of the island's top.</summary>
        public double IslandTop => FootHeight + IslandHeight;

        /// <summary>The angle round the island of a place <paramref name="s"/> along the stair, radians.</summary>
        public double AngleAt(double s) => StartAngle + Direction * s / StairMidRadius;

        /// <summary>The ground-plan place at <paramref name="s"/> along the stair and <paramref name="across"/> out from its middle line.</summary>
        public Vec2 PlaceAt(double s, double across = 0.0)
        {
            double angle = AngleAt(s), radius = StairMidRadius + across;
            return new Vec2(CentreX + radius * Math.Cos(angle), CentreZ + radius * Math.Sin(angle));
        }

        /// <summary>The height of the stair's middle line at <paramref name="s"/>.</summary>
        public double HeightAt(double s) => FootHeight + s * Rise;

        /// <summary>The way the stair climbs at its foot, on the ground plan (unit).</summary>
        public Vec2 FootTangent
        {
            get
            {
                double angle = StartAngle;
                return new Vec2(-Math.Sin(angle), Math.Cos(angle)) * Direction;
            }
        }

        /// <summary>From the foot's middle toward the island's middle (unit).</summary>
        public Vec2 FootInward => new Vec2(-Math.Cos(StartAngle), -Math.Sin(StartAngle));

        /// <summary>The ground-plan place in the landing's frame: <paramref name="x"/> along the stair from the first step, <paramref name="z"/> toward the island.</summary>
        public Vec2 LandingPoint(double x, double z) => PlaceAt(0.0) + FootTangent * x + FootInward * z;

        /// <summary>Whether a world point is on the landing, or within <paramref name="margin"/> of it.</summary>
        public bool OnLanding(double wx, double wz, double margin = 0.0)
        {
            if (!Found)
            {
                return false;
            }
            Vec2 d = new Vec2(wx, wz) - PlaceAt(0.0);
            double x = Vec2.Dot(d, FootTangent), z = Vec2.Dot(d, FootInward);
            return x >= -LandingLength - margin && x <= LowRun + margin && Math.Abs(z) <= LandingHalfWidth + margin;
        }

        /// <summary>
        /// The height of the lowest pass of the stair's middle line over a world point, when the stair passes over it (within <paramref name="margin"/>
        /// of its edges); false elsewhere.
        /// </summary>
        public bool StairOver(double wx, double wz, out double height, out double s, double margin = 0.0)
        {
            height = 0.0;
            s = 0.0;
            if (!Found)
            {
                return false;
            }
            double dx = wx - CentreX, dz = wz - CentreZ, radius = Math.Sqrt(dx * dx + dz * dz);
            if (radius < StairInnerRadius - margin || radius > StairOuterRadius + margin)
            {
                return false;
            }
            double turned = (Math.Atan2(dz, dx) - StartAngle) * Direction;
            turned -= 2.0 * Math.PI * Math.Floor(turned / (2.0 * Math.PI));
            s = turned * StairMidRadius;
            height = HeightAt(s);
            return true;
        }

        /// <summary>
        /// Whether a generated building would stand in the way: its footprint (its rectangle, or the square round a round one) comes within
        /// <see cref="ClearOfBuildings"/> of the landing, or its top comes within <see cref="ClearOverRoofs"/> of the stair's underside where the stair
        /// passes over it. Such a building is not built.
        /// </summary>
        public bool Overlaps(in BuildingSite site)
        {
            if (!Found)
            {
                return false;
            }
            double hw = site.Params.Width * 0.5 + ClearOfBuildings, hd = site.Params.Depth * 0.5 + ClearOfBuildings;
            double reach = Math.Sqrt(hw * hw + hd * hd);
            double dx = site.CentreX - CentreX, dz = site.CentreZ - CentreZ, distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance + reach < StairInnerRadius || distance - reach > StairOuterRadius)
            {
                return false;   // the landing is in the ring too
            }
            double top = site.Top + ClearOverRoofs + DeckThickness;
            int stepsX = Math.Max(1, (int)Math.Ceiling(2.0 * hw / SampleMetres)), stepsZ = Math.Max(1, (int)Math.Ceiling(2.0 * hd / SampleMetres));
            for (int ix = 0; ix <= stepsX; ix++)
            {
                for (int iz = 0; iz <= stepsZ; iz++)
                {
                    site.LocalToWorld(-hw + 2.0 * hw * ix / stepsX, -hd + 2.0 * hd * iz / stepsZ, out double wx, out double wz);
                    if (OnLanding(wx, wz) || (StairOver(wx, wz, out double height, out _) && height < top))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Whether a footbridge may not pass here: on the landing, or under the stair where the stair is still low enough to meet it.</summary>
        public bool BlocksABridge(double wx, double wz)
        {
            return OnLanding(wx, wz, ClearOfBuildings) || (StairOver(wx, wz, out _, out double s, ClearOfBuildings) && s < ClearOfBridges);
        }

        /// <summary>
        /// The landing nearest the spawn that fits: a whole lot inside one block's lot line, its long side on that line, flat, dry and clear of the
        /// expressways, the spawn and Old Guy's lot outside the ring, and the stair clear of the ground and the expressways where it leaves it. Blocks
        /// that are a plaza, a mall, enclose another block or have a stairway through them are passed over. Not found (default) when nothing within
        /// <see cref="SearchRadius"/> fits.
        /// </summary>
        /// <remarks>
        /// The places along the frontages are gathered first and tried nearest the spawn first, stopping at the first that fits: trying every frontage
        /// in the plan's order cost 3.9 s of the plan's 11 (2026-11-12, the title waiting on it). The same landing as before: the nearest point with a
        /// fit, the first in the plan's order of equally near ones, and of the four ways round at it the last that fits.
        /// </remarks>
        public static SkyHubPlan Find(CityPlanData data, PlanFields fields, double spawnX, double spawnZ, in TutorialLot house)
        {
            var spawn = new Vec2(spawnX, spawnZ);
            double length = LandingLength + LowRun;
            Vec2[] houseCorners = house.Found ? house.Corners() : Array.Empty<Vec2>();
            var places = new List<Frontage>();
            for (int b = 0; b < data.Blocks.Count; b++)
            {
                PlanBlock block = data.Blocks[b];
                if (block.Outer == null || block.Inner == null || block.Encloses || block.Plaza || block.Mall || block.StairLane >= 0)
                {
                    continue;
                }
                List<Vec2> poly = block.Outer;
                bool counterClockwise = PlanMath.SignedArea(poly) > 0.0;
                for (int i = 0, n = poly.Count; i < n; i++)
                {
                    Vec2 a = poly[i], c = poly[(i + 1) % n];
                    double side = (c - a).Length;
                    if (side < length)
                    {
                        continue;
                    }
                    Vec2 along = (c - a) / side;
                    Vec2 inward = counterClockwise ? along.Perp : -along.Perp;
                    for (double t = length * 0.5; t <= side - length * 0.5 + 1e-6; t += FrontageStep)
                    {
                        Vec2 front = a + along * t;
                        double distance = (front - spawn).Length;
                        if (distance <= SearchRadius)
                        {
                            places.Add(new Frontage(b, front, along, inward, distance, places.Count));
                        }
                    }
                }
            }
            places.Sort((p, q) => p.Distance != q.Distance ? p.Distance.CompareTo(q.Distance) : p.Order.CompareTo(q.Order));

            foreach (Frontage place in places)
            {
                List<Vec2> poly = data.Blocks[place.Block].Outer;
                SkyHubPlan found = default;
                bool fits = false;
                foreach (double climb in new[] { 1.0, -1.0 })
                {
                    foreach (double toIsland in new[] { 1.0, -1.0 })
                    {
                        Vec2 tangent = place.Along * climb;
                        // The middle of the landing's length is on the frontage point; the first step LandingLength from its start.
                        Vec2 foot = place.Front + tangent * ((LandingLength - LowRun) * 0.5) + place.Inward * (LandingHalfWidth + FrontInset);
                        Vec2 centre = foot + place.Inward * (toIsland * StairMidRadius);
                        if (!ClearOfTheRing(centre, spawn, houseCorners))
                        {
                            continue;
                        }
                        Vec2 fromCentre = foot - centre;
                        double startAngle = Math.Atan2(fromCentre.Y, fromCentre.X);
                        int direction = Vec2.Dot(new Vec2(-Math.Sin(startAngle), Math.Cos(startAngle)), tangent) > 0.0 ? 1 : -1;
                        var candidate = new SkyHubPlan(centre.X, centre.Y, startAngle, direction, 0.0, 0.0, place.Block);
                        if (candidate.Fits(data, fields, poly, house, out SkyHubPlan fitted))
                        {
                            found = fitted;
                            fits = true;
                        }
                    }
                }
                if (fits)
                {
                    return found;
                }
            }
            return default;
        }

        /// <summary>A place along a block's frontage the landing could stand at, and where it came in the plan's order.</summary>
        private readonly struct Frontage
        {
            public readonly int Block;
            public readonly Vec2 Front, Along, Inward;
            public readonly double Distance;
            public readonly int Order;

            public Frontage(int block, Vec2 front, Vec2 along, Vec2 inward, double distance, int order)
            {
                Block = block;
                Front = front;
                Along = along;
                Inward = inward;
                Distance = distance;
                Order = order;
            }
        }

        private static bool ClearOfTheRing(Vec2 centre, Vec2 spawn, Vec2[] houseCorners)
        {
            double clear = StairOuterRadius + ClearOfTheFall;
            if ((spawn - centre).Length < clear)
            {
                return false;
            }
            foreach (Vec2 corner in houseCorners)
            {
                if ((corner - centre).Length < clear)
                {
                    return false;
                }
            }
            return true;
        }

        private bool Fits(CityPlanData data, PlanFields fields, List<Vec2> poly, in TutorialLot house, out SkyHubPlan fitted)
        {
            fitted = default;
            double x0 = -LandingLength, x1 = LowRun, half = LandingHalfWidth;

            // Inside the lot line: points round the landing's edge a hair in from it, and no corner of the lot line poking into it between them.
            const double hair = 0.02;
            int stepsX = (int)Math.Ceiling((x1 - x0) / SampleMetres), stepsZ = (int)Math.Ceiling(2.0 * half / SampleMetres);
            for (int k = 0; k <= stepsX; k++)
            {
                double x = x0 + hair + (x1 - x0 - 2.0 * hair) * k / stepsX;
                if (!PlanMath.PointInPolygon(LandingPoint(x, half - hair), poly) || !PlanMath.PointInPolygon(LandingPoint(x, -half + hair), poly)) return false;
            }
            for (int k = 0; k <= stepsZ; k++)
            {
                double z = -half + hair + (2.0 * half - 2.0 * hair) * k / stepsZ;
                if (!PlanMath.PointInPolygon(LandingPoint(x0 + hair, z), poly) || !PlanMath.PointInPolygon(LandingPoint(x1 - hair, z), poly)) return false;
            }
            foreach (Vec2 v in poly)
            {
                if (OnLanding(v.X, v.Y, -0.05)) return false;
            }

            // Flat, dry and in the open, off Old Guy's lot.
            double high = double.MinValue, low = double.MaxValue;
            for (int iz = 0; iz <= stepsZ; iz++)
            {
                for (int ix = 0; ix <= stepsX; ix++)
                {
                    Vec2 p = LandingPoint(x0 + (x1 - x0) * ix / stepsX, -half + 2.0 * half * iz / stepsZ);
                    if (fields.IsWater(p.X, p.Y) || data.Expressways.Covers(p, 2.0) || house.Contains(p.X, p.Y, TutorialLot.ClearOfOthers)) return false;
                    double h = fields.Height(p.X, p.Y);
                    high = Math.Max(high, h);
                    low = Math.Min(low, h);
                    if (high - low > MaxGroundSpread) return false;
                }
            }
            double foot = Math.Ceiling((high + PlanBuildings.PavingRise + PlanBuildings.FloorClearance) * 10.0 - 1e-6) / 10.0;
            fitted = new SkyHubPlan(CentreX, CentreZ, StartAngle, Direction, foot, low, Block);
            return fitted.StairClear(data, fields);
        }

        /// <summary>Past the low run, the stair's underside is clear of the ground under its whole width, and no expressway runs under it while it is low.</summary>
        private bool StairClear(CityPlanData data, PlanFields fields)
        {
            const double stepMetres = 6.0;
            const double highEnough = 500.0;   // above any hill round the city
            for (double s = LowRun; s < StairLength && s * Rise < highEnough; s += stepMetres)
            {
                double underside = HeightAt(s) - DeckThickness;
                foreach (double across in new[] { -StairWidth * 0.5, 0.0, StairWidth * 0.5 })
                {
                    Vec2 p = PlaceAt(s, across);
                    if (fields.Height(p.X, p.Y) + ClearOverGround > underside) return false;
                    if (s < ClearOfExpressways && data.Expressways.Covers(p, 4.0)) return false;
                }
            }
            return true;
        }
    }
}

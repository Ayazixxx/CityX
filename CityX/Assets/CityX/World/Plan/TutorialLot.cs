using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// Where Old Guy's house (docs/OLD_GUYS_HOUSE.md) stands in the generated city: on the land nearest the spawn that holds its whole lot, flat, dry,
    /// clear of the expressways, its front lot line on a street's. The generated buildings whose footprints come near the lot are not built
    /// (<see cref="PlanWorld"/>), so it is the closest building to where the player lands and the rest of the city is generated as ever (owner,
    /// 2026-10-30: "it spawns as the closest building to you and the rest of the world is generated randomly"). A pure function of the plan, so
    /// every machine puts it in the same place.
    ///
    /// The house's own frame: x along the street, z away from it, y up, the front lot line at z = <see cref="LotZ0"/>.
    /// </summary>
    public readonly struct TutorialLot
    {
        /// <summary>The whole lot, the driveway and the porch included, in the house's frame (OldGuysHouse.Lot is these).</summary>
        public const double LotX0 = -1.5, LotX1 = 34.0, LotZ0 = -6.5, LotZ1 = 23.5;

        /// <summary>The house itself under its roof, in its frame (the map draws it): the main block, ridge 19.9 m up.</summary>
        public const double HouseX0 = 0.0, HouseX1 = 28.0, HouseZ0 = 0.0, HouseZ1 = 22.0, HouseHeight = 19.9;

        /// <summary>Where the player lands, in the house's frame: on the driveway, the house the nearest building (OldGuysHouse.Landing).</summary>
        public const double LandingX = 14.0, LandingZ = -3.5;

        /// <summary>How far from the spawn the land is looked for, metres.</summary>
        public const double SearchRadius = 300.0;

        /// <summary>
        /// The most the ground may rise and fall under the lot, metres. The house stands on its highest point, so this is about the step from the
        /// pavement onto the driveway: low enough to walk up.
        /// </summary>
        public const double MaxGroundSpread = 0.5;

        /// <summary>The lot is tried every this many metres along each frontage.</summary>
        public const double FrontageStep = 1.0;

        /// <summary>How far inside the street's lot line the front stands (a hair: the lot line itself belongs to the pavement).</summary>
        public const double FrontInset = 0.05;

        /// <summary>A generated building whose footprint comes this close to the lot is not built: room for what juts from its walls (balconies, canopies, entrance steps).</summary>
        public const double ClearOfOthers = 2.0;

        private const double SampleMetres = 2.0;

        public readonly bool Found;

        /// <summary>The house frame's origin on the ground plan.</summary>
        public readonly double OriginX, OriginZ;

        /// <summary>The world direction of the house's +x (unit); its +z, away from the street, is (-AxisZ, AxisX).</summary>
        public readonly double AxisX, AxisZ;

        /// <summary>The world height of the house's y = 0: over the highest ground under the lot and the paving on it, as every building's floor.</summary>
        public readonly double FloorHeight;

        /// <summary>The lowest ground under the lot: the house's footing reaches down to it.</summary>
        public readonly double LowestGround;

        /// <summary>The block the house stands in.</summary>
        public readonly int Block;

        public TutorialLot(double originX, double originZ, double axisX, double axisZ, double floorHeight, double lowestGround, int block)
        {
            Found = true;
            OriginX = originX;
            OriginZ = originZ;
            AxisX = axisX;
            AxisZ = axisZ;
            FloorHeight = floorHeight;
            LowestGround = lowestGround;
            Block = block;
        }

        public void ToWorld(double x, double z, out double wx, out double wz)
        {
            wx = OriginX + x * AxisX - z * AxisZ;
            wz = OriginZ + x * AxisZ + z * AxisX;
        }

        public void ToLocal(double wx, double wz, out double x, out double z)
        {
            double dx = wx - OriginX, dz = wz - OriginZ;
            x = dx * AxisX + dz * AxisZ;
            z = -dx * AxisZ + dz * AxisX;
        }

        /// <summary>Whether a world point is on the lot, or within <paramref name="margin"/> of it.</summary>
        public bool Contains(double wx, double wz, double margin = 0.0)
        {
            if (!Found)
            {
                return false;
            }
            ToLocal(wx, wz, out double x, out double z);
            return x >= LotX0 - margin && x <= LotX1 + margin && z >= LotZ0 - margin && z <= LotZ1 + margin;
        }

        /// <summary>The lot's corners on the ground plan, counter-clockwise.</summary>
        public Vec2[] Corners(double margin = 0.0)
        {
            var corners = new Vec2[4];
            double[] xs = { LotX0 - margin, LotX1 + margin, LotX1 + margin, LotX0 - margin };
            double[] zs = { LotZ0 - margin, LotZ0 - margin, LotZ1 + margin, LotZ1 + margin };
            for (int k = 0; k < 4; k++)
            {
                ToWorld(xs[k], zs[k], out double wx, out double wz);
                corners[k] = new Vec2(wx, wz);
            }
            return corners;
        }

        /// <summary>The middle of the lot's front, on the pavement's edge: where the player walks in from the street.</summary>
        public Vec2 FrontMiddle
        {
            get
            {
                ToWorld((LotX0 + LotX1) * 0.5, LotZ0, out double wx, out double wz);
                return new Vec2(wx, wz);
            }
        }

        /// <summary>
        /// The land where the house's walls come nearest (<paramref name="spawnX"/>, <paramref name="spawnZ"/>) that holds the whole lot inside
        /// one block's lot line with its front on that line, the ground under it within <see cref="MaxGroundSpread"/>, and no water or expressway on
        /// it. Blocks that are a plaza, a mall, enclose another block or have a stairway through them are passed over. Not found (default) when
        /// nothing within <see cref="SearchRadius"/> fits.
        /// </summary>
        public static TutorialLot Find(CityPlanData data, PlanFields fields, double spawnX, double spawnZ)
        {
            var spawn = new Vec2(spawnX, spawnZ);
            double width = LotX1 - LotX0, best = double.MaxValue;
            TutorialLot found = default;
            for (int b = 0; b < data.Blocks.Count; b++)
            {
                PlanBlock block = data.Blocks[b];
                if (block.Outer == null || block.Inner == null || block.Encloses || block.Plaza || block.Mall || block.StairLane >= 0)
                {
                    continue;
                }
                List<Vec2> poly = block.Outer;
                if (DistanceToBox(poly, spawn) > SearchRadius)
                {
                    continue;
                }
                bool counterClockwise = PlanMath.SignedArea(poly) > 0.0;
                for (int i = 0, n = poly.Count; i < n; i++)
                {
                    Vec2 a = poly[i], c = poly[(i + 1) % n];
                    double length = (c - a).Length;
                    if (length < width)
                    {
                        continue;
                    }
                    Vec2 along = (c - a) / length;
                    Vec2 inward = counterClockwise ? along.Perp : -along.Perp;
                    var axis = new Vec2(inward.Y, -inward.X);
                    for (double t = width * 0.5; t <= length - width * 0.5 + 1e-6; t += FrontageStep)
                    {
                        Vec2 front = a + along * t;
                        Vec2 origin = front - axis * ((LotX0 + LotX1) * 0.5) + inward * (FrontInset - LotZ0);
                        double distance = GapToHouse(origin, axis, spawn);
                        if (distance >= best || distance > SearchRadius)
                        {
                            continue;
                        }
                        if (!Fits(data, fields, poly, origin, axis, b, out TutorialLot lot))
                        {
                            continue;
                        }
                        best = distance;
                        found = lot;
                    }
                }
            }
            return found;
        }

        private static bool Fits(CityPlanData data, PlanFields fields, List<Vec2> poly, Vec2 origin, Vec2 axis, int block, out TutorialLot lot)
        {
            lot = new TutorialLot(origin.X, origin.Y, axis.X, axis.Y, 0.0, 0.0, block);

            // Inside the lot line: points round the lot's edge a hair in from it, and no corner of the lot line poking into it between them.
            const double hair = 0.02;
            int stepsX = (int)Math.Ceiling((LotX1 - LotX0) / SampleMetres), stepsZ = (int)Math.Ceiling((LotZ1 - LotZ0) / SampleMetres);
            for (int k = 0; k <= stepsX; k++)
            {
                double x = LotX0 + hair + (LotX1 - LotX0 - 2.0 * hair) * k / stepsX;
                if (!Inside(lot, poly, x, LotZ0 + hair) || !Inside(lot, poly, x, LotZ1 - hair)) return false;
            }
            for (int k = 0; k <= stepsZ; k++)
            {
                double z = LotZ0 + hair + (LotZ1 - LotZ0 - 2.0 * hair) * k / stepsZ;
                if (!Inside(lot, poly, LotX0 + hair, z) || !Inside(lot, poly, LotX1 - hair, z)) return false;
            }
            foreach (Vec2 v in poly)
            {
                lot.ToLocal(v.X, v.Y, out double x, out double z);
                if (x > LotX0 + 0.05 && x < LotX1 - 0.05 && z > LotZ0 + 0.05 && z < LotZ1 - 0.05) return false;
            }

            // Flat, dry and in the open.
            double high = double.MinValue, low = double.MaxValue;
            for (int iz = 0; iz <= stepsZ; iz++)
            {
                for (int ix = 0; ix <= stepsX; ix++)
                {
                    lot.ToWorld(LotX0 + (LotX1 - LotX0) * ix / stepsX, LotZ0 + (LotZ1 - LotZ0) * iz / stepsZ, out double wx, out double wz);
                    if (fields.IsWater(wx, wz) || data.Expressways.Covers(new Vec2(wx, wz), 2.0)) return false;
                    double h = fields.Height(wx, wz);
                    high = Math.Max(high, h);
                    low = Math.Min(low, h);
                    if (high - low > MaxGroundSpread) return false;
                }
            }
            double floor = Math.Ceiling((high + PlanBuildings.PavingRise + PlanBuildings.FloorClearance) * 10.0 - 1e-6) / 10.0;
            lot = new TutorialLot(origin.X, origin.Y, axis.X, axis.Y, floor, low, block);
            return true;
        }

        /// <summary>How far a point is from the house's walls (<see cref="HouseX0"/> to <see cref="HouseZ1"/>) with the frame at <paramref name="origin"/>, turned to <paramref name="axis"/>.</summary>
        private static double GapToHouse(Vec2 origin, Vec2 axis, Vec2 p)
        {
            Vec2 d = p - origin;
            double x = Vec2.Dot(d, axis), z = Vec2.Dot(d, axis.Perp);
            double dx = Math.Max(0.0, Math.Max(HouseX0 - x, x - HouseX1)), dz = Math.Max(0.0, Math.Max(HouseZ0 - z, z - HouseZ1));
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static bool Inside(in TutorialLot lot, List<Vec2> poly, double x, double z)
        {
            lot.ToWorld(x, z, out double wx, out double wz);
            return PlanMath.PointInPolygon(new Vec2(wx, wz), poly);
        }

        private static double DistanceToBox(List<Vec2> poly, Vec2 p)
        {
            double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
            foreach (Vec2 v in poly)
            {
                x0 = Math.Min(x0, v.X);
                x1 = Math.Max(x1, v.X);
                z0 = Math.Min(z0, v.Y);
                z1 = Math.Max(z1, v.Y);
            }
            double dx = Math.Max(0.0, Math.Max(x0 - p.X, p.X - x1)), dz = Math.Max(0.0, Math.Max(z0 - p.Y, p.Y - z1));
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Whether a generated building's footprint (its rectangle, or the square round a round one) comes within <see cref="ClearOfOthers"/> of the
        /// lot: such a building is not built.
        /// </summary>
        public bool Overlaps(in BuildingSite site)
        {
            if (!Found)
            {
                return false;
            }
            double hw = site.Params.Width * 0.5, hd = site.Params.Depth * 0.5;
            // Two rectangles miss each other when some edge's direction of one separates them.
            var corners = new Vec2[4];
            for (int k = 0; k < 4; k++)
            {
                site.LocalToWorld((k & 1) == 0 ? -hw : hw, (k & 2) == 0 ? -hd : hd, out double wx, out double wz);
                corners[k] = new Vec2(wx, wz);
            }
            Vec2[] lot = Corners(ClearOfOthers);
            Vec2 siteX = new Vec2(site.AxisX, site.AxisZ), siteZ = siteX.Perp, lotX = new Vec2(AxisX, AxisZ), lotZ = lotX.Perp;
            return !Separates(siteX, corners, lot) && !Separates(siteZ, corners, lot) && !Separates(lotX, corners, lot) && !Separates(lotZ, corners, lot);
        }

        private static bool Separates(Vec2 direction, Vec2[] a, Vec2[] b)
        {
            double a0 = double.MaxValue, a1 = double.MinValue, b0 = double.MaxValue, b1 = double.MinValue;
            for (int k = 0; k < 4; k++)
            {
                double pa = Vec2.Dot(a[k], direction), pb = Vec2.Dot(b[k], direction);
                a0 = Math.Min(a0, pa);
                a1 = Math.Max(a1, pa);
                b0 = Math.Min(b0, pb);
                b1 = Math.Max(b1, pb);
            }
            return a1 < b0 || b1 < a0;
        }
    }
}

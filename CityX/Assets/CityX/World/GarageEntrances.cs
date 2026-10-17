using System.Collections.Generic;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// One of a parking garage's two entrances: an opening in the front wall and the drive to it, from the edge of the carriageway in front,
    /// across the pavement (cut into the bank or built up over it), through the opening and on up (or down) to the lowest deck. In the
    /// building's frame: <see cref="X0"/>..<see cref="X1"/> across the frontage (from its centre); along the opening's middle, distances are
    /// metres IN from the front wall (negative: out in front of it). The drive is level with the road at <see cref="KerbOut"/> metres out, rises
    /// at a garage ramp's grade, and is level with the deck from <see cref="RampEnd"/> on.
    /// </summary>
    public readonly struct GarageEntrance
    {
        public readonly float X0, X1;

        /// <summary>How far out from the front wall the carriageway begins on the opening's middle (0: no road in front, the drive starts at the wall).</summary>
        public readonly float KerbOut;

        /// <summary>The road's surface at that edge (the paving at the wall when there is no road).</summary>
        public readonly float RoadY;

        /// <summary>The lowest deck.</summary>
        public readonly float DeckY;

        /// <summary>Where the drive reaches the deck, metres in from the front wall (negative: before the wall, out in front).</summary>
        public readonly float RampEnd;

        /// <summary>
        /// The drive's height at the front wall when the ramp runs on inside: straight from the road to here across the pavement, straight from here
        /// to the deck inside (where the road slopes along the frontage, the drive climbs less out there: <see cref="GarageEntrances.TwistAllowance"/>).
        /// </summary>
        public readonly float WallY;

        public GarageEntrance(float x0, float x1, float kerbOut, float roadY, float deckY, float rampEnd, float wallY)
        {
            X0 = x0;
            X1 = x1;
            KerbOut = kerbOut;
            RoadY = roadY;
            DeckY = deckY;
            RampEnd = rampEnd;
            WallY = wallY;
        }

        /// <summary>How far the ramp runs on inside the garage past its front wall (0: the drive is level with the deck at the wall).</summary>
        public float Length => RampEnd > 0.0f ? RampEnd : 0.0f;

        /// <summary>Whether the ramp runs on inside (the deck is cut away for it).</summary>
        public bool HasRamp => Length > 0.0f;

        /// <summary>The drive's height at the front wall.</summary>
        public float StreetY => SurfaceAt(0.0f);

        /// <summary>How far the deck stands over the drive at the front wall (the rise of the ramp inside).</summary>
        public float Rise => DeckY - StreetY;

        /// <summary>Whether a drive is cut across the pavement in front (a road was found in front of the opening).</summary>
        public bool HasDriveway => KerbOut > 0.0f;

        /// <summary>
        /// The drive's surface <paramref name="inFromFront"/> metres in from the front wall: the road's level out at the kerb, the deck's past the ramp;
        /// straight between, or bent at the wall (<see cref="WallY"/>) where the ramp runs on inside.
        /// </summary>
        public float SurfaceAt(float inFromFront)
        {
            float start = -KerbOut;
            if (inFromFront <= start) return RoadY;
            if (inFromFront >= RampEnd) return DeckY;
            if (RampEnd <= 0.0f || start >= 0.0f) return RoadY + (DeckY - RoadY) * ((inFromFront - start) / (RampEnd - start));
            if (inFromFront <= 0.0f) return RoadY + (WallY - RoadY) * ((inFromFront - start) / -start);
            return WallY + (DeckY - WallY) * (inFromFront / RampEnd);
        }
    }

    /// <summary>
    /// A parking garage's entrances (owner, 2026-10-16: "there was physically no way for the car to drive into it"): the plan stands a building
    /// on the highest ground of its lot, so on a hillside a garage's deck stood on a solid podium up to 16 m over the street in front of it, a
    /// ledge no car could climb; and the bank between the road and its wall could be 2 m high and as steep as 45 %. Now each opening has a DRIVE
    /// from the edge of the carriageway in front, level with the road there, rising (or falling) at a garage ramp's grade at most (entrance ramps
    /// are built to 12-20 %; here <see cref="Grade"/> unless the garage is too shallow for it), cut into the bank or built over it, and on
    /// through the opening to the LOWEST DECK, which stands where the drives reach it: as near the paving at the wall as a drive from the road
    /// can climb to, never over the plan's floor. The garage's levels stack from that deck up to the roof the plan gave it (CityMeshBuilder.
    /// BuildGarage walls the levels below the plan's floor solid, the hill being behind them). Pure: the mesh builder draws the decks and the
    /// ramps inside, the surface builder the drives and the ground cut away under the garage and them; the tests drive in.
    /// </summary>
    public static class GarageEntrances
    {
        /// <summary>Each opening's width, metres (a two-lane opening at 1.3x).</summary>
        public const float OpeningWidth = 7.0f;

        /// <summary>Half the band between the two openings, which carries the P sign.</summary>
        public const float HalfPier = 3.0f;

        /// <summary>
        /// The steepest a drive is built when there is room for it: 12 % (garage entrances are built to 12-20 %; the game's car, a box on its wheel rays
        /// with no suspension, climbs a clean 9 degree ramp but stalls on 12 degrees: CarFlatDriveProbe).
        /// </summary>
        public const float Grade = 0.12f;

        /// <summary>
        /// Half the game's car's width and a margin, metres (Vehicles/Car, 3.4 m wide at the player's scale). Where the road slopes along the
        /// frontage, a drive's mouth follows the road and the drive untwists to level at the wall (CitySurfaceBuilder.DriveHeight), which steepens
        /// it at a car's side by the road's slope times this over the pavement's width: the drive climbs that much less across the pavement, and the
        /// rest on the straight ramp inside (2026-10-16, seed 19077151: a 12 % drive from a road rising 11 % across the opening was 16.5 % under the
        /// car's left wheels, and the car, a box with no suspension, beached at its foot).
        /// </summary>
        public const float TwistAllowance = 1.8f;

        /// <summary>A rise at most this big is no ramp (a car rolls over it).</summary>
        public const float FlushRise = 0.05f;

        /// <summary>The back of the lowest deck kept for the ramp up to the next deck and an aisle to turn into it, metres.</summary>
        public const float BackStrip = 9.6f;

        /// <summary>The shortest ramp: a short rise still gets a slope a car rolls up, not a step.</summary>
        public const float ShortestRamp = 1.2f;

        /// <summary>A road's surface stands this far over the bare ground (CitySurfaceBuilder.RoadLift, which a test holds equal to this).</summary>
        public const float RoadLift = 0.07f;

        /// <summary>How far out in front of the wall a carriageway is looked for, metres.</summary>
        public const float SearchOut = 20.0f;

        /// <summary>
        /// The garage's own floors are drawn this far over their slabs (CityMeshBuilder.BuildGarage: the deck and the ramps inside), and a drive is
        /// drawn rising to meet them at the wall: the game's car is a box with no suspension, and a two-centimetre step stops it dead.
        /// </summary>
        public const float FloorLift = 0.02f;

        /// <summary>A drive is drawn this far under the road's level at the kerb, so a car rolls down onto it rather than into its edge.</summary>
        public const float UnderRoad = 0.015f;

        /// <summary>... and runs on this far into the carriageway, sinking a little more under the road's surface: no edge where they meet.</summary>
        public const float IntoRoad = 0.5f;
        public const float SinkIntoRoad = 0.045f;

        /// <summary>
        /// The height a drive is drawn at <paramref name="inFromFront"/> metres in from the front wall (from <see cref="IntoRoad"/> past the kerb to the
        /// wall): its surface, from just under the road at the kerb to <see cref="FloorLift"/> over it at the wall. Straight between the kerb, the
        /// ramp's top and the wall.
        /// </summary>
        public static float DrawnHeight(in GarageEntrance e, float inFromFront)
        {
            float start = -e.KerbOut;
            if (inFromFront < start)
            {
                return e.RoadY - UnderRoad - SinkIntoRoad * System.Math.Min(1.0f, (start - inFromFront) / IntoRoad);
            }
            float t = e.KerbOut > 1e-3f ? System.Math.Min(1.0f, (inFromFront - start) / e.KerbOut) : 1.0f;
            return e.SurfaceAt(inFromFront) - UnderRoad + (FloorLift + UnderRoad) * t;
        }

        /// <summary>The paving at the wall is sampled this far out from it, metres (at its foot and two metres out, as a door's is).</summary>
        private static readonly double[] OutMetres = { 0.3, 2.0 };

        /// <summary>The two entrances of a garage, left then right across its frontage (none for anything else).</summary>
        public static GarageEntrance[] For(PlanWorld plan, BuildingSite site)
        {
            if (!site.Exists || !site.IsParkingGarage) return System.Array.Empty<GarageEntrance>();
            float front = site.Params.Depth * 0.5f;
            float longest = System.Math.Max(ShortestRamp, site.Params.Depth - BackStrip);
            var x0s = new float[2];
            var kerbs = new float[2];
            var roads = new float[2];
            var slopes = new float[2];
            float deck = float.MinValue;
            for (int i = 0; i < 2; i++)
            {
                float x0 = i == 0 ? -HalfPier - OpeningWidth : HalfPier;
                float x1 = x0 + OpeningWidth;
                float mid = (x0 + x1) * 0.5f;
                // The paving at the wall: the LOWEST of it across the opening (both edges and the middle), at the wall's foot and two metres out.
                double lowest = double.MaxValue;
                foreach (double outward in OutMetres)
                {
                    foreach (double along in new[] { (double)x0, mid, (double)x1 })
                    {
                        site.LocalToWorld(along, front + outward, out double wx, out double wz);
                        lowest = System.Math.Min(lowest, plan.GroundHeight(wx, wz));
                    }
                }
                float paving = (float)(lowest + PlanBuildings.PavingRise);
                // The carriageway in front: its edge on the opening's middle, and the road's surface there.
                float kerb = CarriagewayOut(plan, site, mid);
                float road = paving;
                if (float.IsNaN(kerb))
                {
                    kerb = 0.0f;
                }
                else
                {
                    site.LocalToWorld(mid, front + kerb, out double kx, out double kz);
                    road = (float)(plan.GroundHeight(kx, kz) + RoadLift);
                }
                x0s[i] = x0;
                kerbs[i] = kerb;
                roads[i] = road;
                if (kerb > 0.0f)
                {
                    // The road's edge across the opening: how much it rises per metre from one side to the other (a street sloping along the frontage).
                    site.LocalToWorld(x0, front + kerb, out double lx, out double lz);
                    site.LocalToWorld(x1, front + kerb, out double rx, out double rz);
                    slopes[i] = (float)((plan.GroundHeight(rx, rz) - plan.GroundHeight(lx, lz)) / (x1 - x0));
                }
                // The deck this opening would have: the paving at the wall, as near as a drive from the road climbs (or falls) to over the pavement.
                float reach = Grade * kerb;
                deck = System.Math.Max(deck, System.Math.Min(road + reach, System.Math.Max(road - reach, paving)));
            }
            deck = System.Math.Min(deck, site.BaseHeight);
            var two = new GarageEntrance[2];
            for (int i = 0; i < 2; i++)
            {
                float x0 = x0s[i], kerb = kerbs[i], rise = deck - roads[i];
                if (System.Math.Abs(rise) <= FlushRise)
                {
                    two[i] = new GarageEntrance(x0, x0 + OpeningWidth, kerb, deck, deck, -kerb, deck);   // level with the road: no ramp
                    continue;
                }
                // Across the pavement at the grade, less what the twist adds at a car's side where the road slopes along the frontage; a drive that
                // reaches the deck out there ends there.
                float outGrade = kerb > 0.0f ? System.Math.Max(0.0f, Grade - System.Math.Abs(slopes[i]) * TwistAllowance / kerb) : Grade;
                float runOut = outGrade > 1e-3f ? System.Math.Max(ShortestRamp, System.Math.Abs(rise) / outGrade) : float.MaxValue;
                if (runOut <= kerb)
                {
                    two[i] = new GarageEntrance(x0, x0 + OpeningWidth, kerb, roads[i], deck, runOut - kerb, deck);
                    continue;
                }
                // The rest inside, on the straight ramp at the grade (steeper where the garage is too shallow for it).
                float wall = roads[i] + System.Math.Sign(rise) * outGrade * kerb;
                float inside = System.Math.Abs(deck - wall) / Grade;
                if (kerb <= 0.0f || outGrade <= 1e-3f) inside = System.Math.Max(ShortestRamp, inside);   // all of the climb is in here: a slope, never a step
                if (inside < 1e-3f)
                {
                    two[i] = new GarageEntrance(x0, x0 + OpeningWidth, kerb, roads[i], deck, 0.0f, deck);
                    continue;
                }
                two[i] = new GarageEntrance(x0, x0 + OpeningWidth, kerb, roads[i], deck, System.Math.Min(longest, inside), wall);
            }
            return two;
        }

        /// <summary>
        /// How far out from the front wall, on the line <paramref name="localX"/> across the frontage, the first carriageway begins (NaN: none within
        /// <see cref="SearchOut"/>, or the first is a bridge over the ground). Stepped half a metre at a time, then halved down to two centimetres.
        /// </summary>
        public static float CarriagewayOut(PlanWorld plan, BuildingSite site, float localX) => CarriagewayOut(plan, site, localX, out _);

        /// <summary>As above, with the road found (-1 for none).</summary>
        public static float CarriagewayOut(PlanWorld plan, BuildingSite site, float localX, out int road)
        {
            float front = site.Params.Depth * 0.5f;
            site.LocalToWorld(localX, front, out double ax, out double az);
            site.LocalToWorld(localX, front + SearchOut, out double bx, out double bz);
            var near = new List<int>();
            var seen = new HashSet<int>();
            int cx0 = PlanWorld.ChunkOf(System.Math.Min(ax, bx)) - 1, cx1 = PlanWorld.ChunkOf(System.Math.Max(ax, bx)) + 1;
            int cy0 = PlanWorld.ChunkOf(System.Math.Min(az, bz)) - 1, cy1 = PlanWorld.ChunkOf(System.Math.Max(az, bz)) + 1;
            for (int cy = cy0; cy <= cy1; cy++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    foreach (int e in plan.EdgesIn(cx, cy))
                    {
                        if (seen.Add(e) && plan.Geometry.Edges[e] != null) near.Add(e);
                    }
                }
            }
            int OnRoad(float o)
            {
                site.LocalToWorld(localX, front + o, out double x, out double z);
                var p = new Vec2(x, z);
                foreach (int e in near)
                {
                    EdgeGeometry g = plan.Geometry.Edges[e];
                    if (PlanMath.DistanceToSegment(p, g.A, g.B, out _) <= g.HalfWidth) return e;
                }
                return -1;
            }
            float clear = 0.0f;
            for (float o = 0.5f; o <= SearchOut; o += 0.5f)
            {
                int hit = OnRoad(o);
                if (hit < 0)
                {
                    clear = o;
                    continue;
                }
                float lo = clear, hi = o;
                for (int k = 0; k < 5; k++)
                {
                    float m = (lo + hi) * 0.5f;
                    if (OnRoad(m) >= 0) hi = m;
                    else lo = m;
                }
                road = plan.Data.Edges[hit].Bridge ? -1 : hit;
                return road < 0 ? float.NaN : hi;
            }
            road = -1;
            return float.NaN;
        }

        /// <summary>The ground a garage's drive crosses, as a world polygon (counter-clockwise): the opening's width from the front wall out to the kerb and <paramref name="past"/> metres on.</summary>
        public static Vec2[] Driveway(BuildingSite site, in GarageEntrance entrance, float past = 0.0f)
        {
            float front = site.Params.Depth * 0.5f;
            return Rectangle(site, entrance.X0, entrance.X1, front, front + entrance.KerbOut + past);
        }

        /// <summary>A rectangle in the building's frame (x across, z out from its centre) as a world polygon, counter-clockwise.</summary>
        public static Vec2[] Rectangle(BuildingSite site, float x0, float x1, float z0, float z1)
        {
            var poly = new Vec2[4];
            site.LocalToWorld(x0, z0, out double ax, out double az);
            site.LocalToWorld(x1, z0, out double bx, out double bz);
            site.LocalToWorld(x1, z1, out double cx, out double cz);
            site.LocalToWorld(x0, z1, out double dx, out double dz);
            poly[0] = new Vec2(ax, az);
            poly[1] = new Vec2(bx, bz);
            poly[2] = new Vec2(cx, cz);
            poly[3] = new Vec2(dx, dz);
            if (PlanMath.SignedArea(poly) < 0.0) System.Array.Reverse(poly);
            return poly;
        }
    }
}

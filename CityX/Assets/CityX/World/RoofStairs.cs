using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// A building's outside stair up to its roof (<see cref="RoofStairs"/>): the tower itself, top to foot, as a bridge's stair down to the
    /// street is (<see cref="StreetStair"/>); the opening in the roof's parapet it comes through, along its wall from the wall's start; and
    /// what it is made of.
    /// </summary>
    public sealed class RoofStair
    {
        public readonly StreetStair Stair;
        public readonly float GapU0, GapU1;
        public readonly BridgeRail Rail;
        public readonly BridgeDeck Deck;

        public RoofStair(StreetStair stair, float gapU0, float gapU1, BridgeRail rail, BridgeDeck deck)
        {
            Stair = stair;
            GapU0 = gapU0;
            GapU1 = gapU1;
            Rail = rail;
            Deck = deck;
        }
    }

    /// <summary>
    /// OUTSIDE STAIRS UP TO THE ROOFS (docs/LAYERED_CITY_PLAN.md, L3; owner, 2026-11-09: "improve upon ... layered chongqing look", and of the
    /// reference pictures, 2026-10-16: "zig-zag stairs climbing between buildings ... make more staircases throughout the city overall").
    /// Until now the only outside stairs were the few that come down from a footbridge. An ordinary flat-roofed block often has one of its
    /// own: a zig-zag tower against a side or back wall, from the paving up to the roof, where an opening in the parapet lets it on (flights
    /// doubling back on two lanes, a landing across both at each end, rails, posts to the ground: the bridges' own tower,
    /// Footbridges.TowerOn). So the roofs are somewhere to go from the street, and a street of blocks has stairs up its faces.
    ///
    /// A PURE FUNCTION OF THE BUILDING AND THE PLAN ROUND IT, as the bridges are: every machine puts the same stair on the same wall. Where:
    ///  * a block the bridges would take (Footbridges.Eligible), with a flat roof behind a parapet, no higher than <see cref="MaxRise"/>.
    ///    A block with another standing on its roof (a tower's podium) has one too, up onto the terrace round the upper block, where the
    ///    opening comes onto open roof: downtown the blocks are towers, and it is their podiums the stairs climb;
    ///  * a side wall first, then the back; never the front (its door, its shop, its billboard overhead), a party wall, a wall of
    ///    balconies or a walkway, or a stretch another block of the building stands against;
    ///  * clear of the wall's corners, off the roads with room left to walk past it, out of every other building, and where no footbridge
    ///    or bridge stair of the building is in its way;
    ///  * of two blocks facing each other across a gap too narrow for both their stairs, only the lower-numbered has one there.
    /// </summary>
    public static class RoofStairs
    {
        /// <summary>A switch for before/after renders and tests.</summary>
        public static bool On = true;

        private const ulong Salt = 0x52465354UL;   // "RFST"

        /// <summary>The opening in the parapet, and the landing outside it, metres along the wall.</summary>
        public const float GapWidth = Footbridges.StreetStairOpening;

        /// <summary>How far in from the roof's edge the stair's walking line starts, metres: past the parapet, on the roof.</summary>
        public const float OntoRoof = 1.5f;

        /// <summary>Room to walk past a tower's outer side, metres: it never stands this near a road, or another block's tower.</summary>
        public const float PassageWidth = 1.5f;

        /// <summary>The tower's footprint out from its wall, metres: two lanes and the gap between them.</summary>
        public static float Depth => Footbridges.StreetStairOffset + Footbridges.StreetStairWidth * 1.5f + Footbridges.TowerLaneGap;

        /// <summary>The highest roof a stair climbs to, metres over the paving at its foot: seventeen flights. (A bridge's tower stops at Footbridges.MaxTowerRise.)</summary>
        public const float MaxRise = 60.0f;

        /// <summary>
        /// The share of blocks that could have a stair up to the roof that are given one, by district. Fewer than one in five of those find a
        /// wall it fits (long enough, clear of the roads, of the neighbours and of the bridges).
        /// </summary>
        public static double ShareFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.85;
                case ZoneKind.Urban: return 0.8;
                case ZoneKind.OldTown: return 0.8;
                case ZoneKind.Residential: return 0.65;
                case ZoneKind.Industrial: return 0.75;
                default: return 0.5;
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, RoofStair> Cache =
            new System.Collections.Concurrent.ConcurrentDictionary<long, RoofStair>();

        private static long Key(BuildingSite site) => ((long)site.Seed << 32) ^ (uint)site.Id;

        /// <summary>The building's stair up to its roof, or null.</summary>
        public static RoofStair For(CityWorld world, BuildingSite site)
        {
            if (!On || !Eligible(site))
            {
                return null;
            }
            long key = Key(site);
            if (Cache.TryGetValue(key, out RoofStair stair))
            {
                return stair;
            }
            stair = Decide(world, site);
            if (Cache.Count >= Footbridges.CacheLimit)
            {
                Cache.Clear();
            }
            Cache[key] = stair;
            return stair;
        }

        /// <summary>The building's stair, if it has been worked out (CityMeshBuilder.BuildSite does, before its walls); null otherwise.</summary>
        public static RoofStair Cached(BuildingSite site)
        {
            return Cache.TryGetValue(Key(site), out RoofStair stair) ? stair : null;
        }

        /// <summary>Forgets every stair (a new world, or the switch turned).</summary>
        public static void Reset()
        {
            Cache.Clear();
        }

        /// <summary>
        /// A block the bridges would take, with a flat roof behind a parapet. A sealed roof with nothing known to stand on it has no open
        /// roof to come onto.
        /// </summary>
        public static bool Eligible(BuildingSite site)
        {
            return Footbridges.Eligible(site) && RoofShape.For(site).HasParapet && (!site.RoofSealed || site.HasRoofObstacle);
        }

        private static RoofStair Decide(CityWorld world, BuildingSite site)
        {
            double D(int i) => WorldHash.Unit01(i, site.Id, Salt, unchecked((ulong)(uint)site.Seed));
            if (D(0) >= ShareFor(site.Zone))
            {
                return null;
            }
            WindowPlan plan = WindowPlan.For(world, site);
            ApartmentFront front = ApartmentFront.For(site, plan);
            // A side wall first (which one by the dice), then the back.
            WindowGrid.Wall first = D(1) < 0.5 ? WindowGrid.Wall.East : WindowGrid.Wall.West;
            WindowGrid.Wall second = first == WindowGrid.Wall.East ? WindowGrid.Wall.West : WindowGrid.Wall.East;
            foreach (WindowGrid.Wall wall in new[] { first, second, WindowGrid.Wall.South })
            {
                if (plan.IsBlankWall(wall) || front.Covers(wall))
                {
                    continue;
                }
                Footbridges.WallFrame(site, wall, out _, out _, out _, out float length);
                // Toward one end of the wall or the other (the dice), the gap in the parapet a corner's margin in from that end and as far
                // again as the wall has to spare; then the other way.
                int way = D(2 + (int)wall) < 0.5 ? 1 : -1;
                float slack = (float)D(6 + (int)wall);
                for (int attempt = 0; attempt < 2; attempt++, way = -way)
                {
                    RoofStair stair = TryOn(world, site, wall, way, length, slack, D);
                    if (stair != null)
                    {
                        return stair;
                    }
                }
            }
            return null;
        }

        /// <summary>The stair on one wall, running one way along it, or null where it will not go.</summary>
        private static RoofStair TryOn(CityWorld world, BuildingSite site, WindowGrid.Wall wall, int way, float length, float slack, System.Func<int, double> dice)
        {
            // Along the wall, the stair's way: the gap and its landing, then the tower (a landing, a full flight, a landing).
            const float corner = 1.25f;
            float tower = Footbridges.FlightMax * Footbridges.Tread + Footbridges.FlightLanding;
            float needed = corner + GapWidth + tower + corner;
            if (length < needed)
            {
                return null;
            }
            float lead = corner + GapWidth + (length - needed) * slack * 0.6f;   // from the wall's end behind the stair to where its first flight starts
            float u = way > 0 ? lead : length - lead;
            float gapU0 = way > 0 ? u - GapWidth : u, gapU1 = way > 0 ? u : u + GapWidth;
            float stretch0 = Mathf.Min(gapU0, u + way * tower) - 0.2f, stretch1 = Mathf.Max(gapU1, u + way * tower) + 0.2f;
            if (site.ContactTop(wall, stretch0, stretch1) > 0.0f || InTheWayOfABridge(world, site, wall, stretch0, stretch1)
                || UnderWhatStandsOnTheRoof(site, wall, gapU0, gapU1))
            {
                return null;
            }

            StreetStair down = Footbridges.TowerOn(world, site, wall, u, way, site.Top, MaxRise);
            if (down == null || !RoomRoundIt(world, site, wall, stretch0, stretch1))
            {
                return null;
            }

            // The landing at the top, outside the gap, level with the roof: from the wall's face out across the inner lane, a rail along its
            // outer side and its far end. The walking line starts on the roof, goes out through the gap, and turns onto the first flight.
            float lane = Footbridges.StreetStairOffset, half = Footbridges.StreetStairWidth * 0.5f;
            float gapMiddle = (gapU0 + gapU1) * 0.5f, outer = lane + half;
            Vector3 At(float along, float o) => Flat(Footbridges.ByWall(site, wall, along, o), site.Top);
            var landings = new List<StairLanding>(down.Landings.Length + 1)
            {
                new StairLanding(At(gapMiddle, outer * 0.5f), down.Along, down.Out, GapWidth * 0.5f, outer * 0.5f, false, true, true),
            };
            landings.AddRange(down.Landings);
            var points = new List<Vector3>(down.Points.Length + 2) { At(gapMiddle, -OntoRoof), At(gapMiddle, lane) };
            points.AddRange(down.Points);
            var risers = new List<int>(down.Risers.Length + 2) { 0, 0 };
            risers.AddRange(down.Risers);

            var stair = new StreetStair(site.Id, wall, points.ToArray(), risers.ToArray(), down.Flights, landings.ToArray(), true, down.Out, down.Along,
                Mathf.Min(down.U0, gapU0 - 0.2f), Mathf.Max(down.U1, gapU1 + 0.2f), down.Bottom, down.Top, down.Width);
            BridgeRail rail = Footbridges.PickRail(site.Zone, dice(10));
            // A steel stair where its rails are open ones, most of the time in the works and half the time elsewhere; concrete behind a parapet.
            bool open = rail != BridgeRail.Parapet && rail != BridgeRail.Pickets;
            BridgeDeck deck = open && dice(11) < (site.Zone == ZoneKind.Industrial ? 0.8 : 0.5) ? BridgeDeck.Steel : BridgeDeck.Concrete;
            return new RoofStair(stair, gapU0, gapU1, rail, deck);
        }

        private static Vector3 Flat(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        /// <summary>Whether the roof just inside the opening is taken by another volume of the building standing on it (BuildingSite.Obstacle*).</summary>
        private static bool UnderWhatStandsOnTheRoof(BuildingSite site, WindowGrid.Wall wall, float gapU0, float gapU1)
        {
            if (!site.HasRoofObstacle)
            {
                return false;
            }
            const float deep = 3.0f, beside = 0.8f;
            Footbridges.WallFrame(site, wall, out Vector2 start, out Vector2 along, out Vector2 outward, out _);
            Vector2 a = start + along * (gapU0 - beside), b = start + along * (gapU1 + beside) - outward * deep;
            return Mathf.Min(a.x, b.x) < site.ObstacleX1 && Mathf.Max(a.x, b.x) > site.ObstacleX0
                && Mathf.Min(a.y, b.y) < site.ObstacleZ1 && Mathf.Max(a.y, b.y) > site.ObstacleZ0;
        }

        /// <summary>Whether a footbridge of the building lands on that stretch of the wall, or its stair down to the street runs along it.</summary>
        private static bool InTheWayOfABridge(CityWorld world, BuildingSite site, WindowGrid.Wall wall, float u0, float u1)
        {
            foreach (Footbridge b in Footbridges.For(world, site))
            {
                b.EndOn(site.Id, out WindowGrid.Wall w, out _, out _, out float u);
                if (w == wall && u > u0 - Footbridges.DeckWidth && u < u1 + Footbridges.DeckWidth)
                {
                    return true;
                }
                StreetStair s = b.Stair;
                if (s != null && s.Site == site.Id && s.Wall == wall && s.U0 < u1 + 0.5f && s.U1 > u0 - 0.5f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether there is room round the tower: the top landing's ground clear as the tower's own is (Footbridges.TowerOn has seen to that),
        /// a walk's width past its outer side off the roads, and no lower-numbered block near enough across the gap for its own tower to
        /// stand in this one's (the higher-numbered of two such blocks has none there).
        /// </summary>
        private static bool RoomRoundIt(CityWorld world, BuildingSite site, WindowGrid.Wall wall, float u0, float u1)
        {
            // Looked at every half metre, both ends of the stretch and the tower's own outer edge among them: a block whose corner came
            // 3.8 m from the wall at the tower's far end stood between two looks, and the tower's outer rail was in its wall.
            float depth = Depth + 0.15f, reach = depth * 2.0f + PassageWidth;
            int along = Mathf.CeilToInt((u1 - u0) / 0.5f), across = Mathf.CeilToInt(reach / 0.5f);
            for (int i = 0; i <= along; i++)
            {
                float u = Mathf.Lerp(u0, u1, i / (float)along);
                for (int k = 0; k <= across + 1; k++)
                {
                    // The last look is the tower's own outer edge, wherever the half metres fall.
                    float o = k <= across ? Mathf.Max(0.3f, reach * k / across) : depth;
                    Vector2 p = Footbridges.ByWall(site, wall, u, o);
                    if (o <= depth + PassageWidth && Footbridges.OnACarriageway(world, p.x, p.y, 0.2))
                    {
                        return false;
                    }
                    if (world.TryBuildingAt(p.x, p.y, out BuildingSite other) && other.Id != site.Id && (o <= depth || other.Id < site.Id))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}

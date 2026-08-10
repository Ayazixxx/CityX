using System.Collections.Generic;

namespace CityX.World
{
    /// <summary>One elevated walkway between two buildings.</summary>
    public readonly struct Skybridge
    {
        /// <summary>Height above sea level. Always a whole floor multiple.</summary>
        public readonly float Height;

        public readonly int FromAnchorX;
        public readonly int FromAnchorY;
        public readonly int ToAnchorX;
        public readonly int ToAnchorY;

        public Skybridge(float height, int fromAnchorX, int fromAnchorY, int toAnchorX, int toAnchorY)
        {
            Height = height;
            FromAnchorX = fromAnchorX;
            FromAnchorY = fromAnchorY;
            ToAnchorX = toAnchorX;
            ToAnchorY = toAnchorY;
        }
    }

    /// <summary>
    /// The elevated walkway network -- bridges between buildings, at several
    /// heights on the same pair.
    ///
    /// WHY MULTI-LEVEL RATHER THAN ONE BRIDGE PER PAIR. In the first reference
    /// set (photographs of Chongqing) walkways are a feature of the city. In
    /// the second set the user supplied -- the dark brutalist concept art --
    /// they are effectively the GROUND: three or four levels of bridges cross
    /// overhead in a single view and there is no single street level at all.
    /// That is the read the user asked for, so a pair of towers gets a stack of
    /// walkways, not one.
    ///
    /// It also lands on gameplay that already exists. Wall-climbing was built
    /// for fun; a city meant to be traversed vertically is the world it was
    /// made for.
    ///
    /// A PAYOFF FROM AN EARLIER DECISION. Two buildings connected by a bridge
    /// almost always stand on DIFFERENT terrace levels, so a bridge height has
    /// to land on a real floor slab in both of them or it meets a wall halfway
    /// up. That works for free here: terrace steps were deliberately made a
    /// whole number of floors, so any height that is a multiple of the floor
    /// height is a floor boundary in every building in the world, whatever it
    /// is standing on. Bridges are placed in whole floors for exactly that
    /// reason, and <see cref="SkybridgeLayoutTests"/> checks it.
    ///
    /// THE PROPERTY THAT MATTERS MOST: both ends must agree without talking.
    /// Chunks generate independently, so the building on the west side and the
    /// building on the east side each decide the bridge exists from their own
    /// seed alone. That is why every roll here is keyed on a CANONICALLY
    /// ORDERED pair -- swap the arguments and the answer must be identical, or
    /// half the bridges in the world are built from one end only.
    /// </summary>
    public sealed class SkybridgeLayout
    {
        /// <summary>Bridges land on whole floors so they meet a real slab at both ends.</summary>
        public const float FloorHeight = CellLayout.NominalFloorMetres;

        /// <summary>Below this, a building is part of the street, not the lattice.</summary>
        public const float MinBuildingHeight = 30.0f;

        /// <summary>Clearance above the higher of the two ground floors.</summary>
        public const float MinClearance = 9.6f;   // 3 floors

        /// <summary>Kept clear below the lower of the two roofs.</summary>
        public const float RoofMargin = 3.2f;     // 1 floor

        /// <summary>
        /// Roughly one walkway level per this much shared height.
        ///
        /// TUNED AGAINST MEASUREMENT, NOT GUESSED. The first attempt used 32m
        /// (ten floors) reasoning that it would put three or four levels on a
        /// pair of towers. It did -- but tower-to-tower pairs are rare, so the
        /// city came out 65.8% unbridged with only 1.7% of pairs carrying more
        /// than one level. Every test passed and the result was still a
        /// single-bridge city.
        ///
        /// The correction is 22m, about seven floors, which is what makes an
        /// ordinary mid-rise pair carry two levels rather than one.
        /// </summary>
        public const float MetresPerLevel = 22.0f;

        public const int MaxLevels = 5;

        /// <summary>
        /// Not every adjacent pair is bridged. Without this the city becomes a
        /// solid lattice with no open canyons in it, and the canyons are half
        /// the reference.
        /// </summary>
        public const double BridgedPairChance = 0.7;

        private const ulong BridgeSalt = 0x42524447UL;   // "BRDG"

        private readonly ulong _seed;

        public SkybridgeLayout(int worldSeed)
        {
            _seed = unchecked((ulong)(long)worldSeed);
        }

        /// <summary>
        /// Every walkway between two buildings, lowest first. Empty when the
        /// pair does not qualify.
        ///
        /// Order-independent: BridgesBetween(a, b) and BridgesBetween(b, a)
        /// return the same heights.
        /// </summary>
        public List<Skybridge> BridgesBetween(BuildingSite a, BuildingSite b)
        {
            var bridges = new List<Skybridge>();

            if (!a.Exists || !b.Exists)
            {
                return bridges;
            }
            if (a.AnchorX == b.AnchorX && a.AnchorY == b.AnchorY)
            {
                return bridges;   // a building does not bridge to itself
            }
            if (a.Params.Height < MinBuildingHeight || b.Params.Height < MinBuildingHeight)
            {
                return bridges;
            }

            // Canonical order, so both ends roll the same dice.
            BuildingSite low = a;
            BuildingSite high = b;
            if (b.AnchorX < a.AnchorX || (b.AnchorX == a.AnchorX && b.AnchorY < a.AnchorY))
            {
                low = b;
                high = a;
            }

            DeterministicRandom rng = PairRandom(low, high);
            if (rng.NextDouble() >= BridgedPairChance)
            {
                return bridges;
            }

            // The band where both buildings actually have wall to attach to.
            float bandLow = Max(low.BaseHeight, high.BaseHeight) + MinClearance;
            float bandHigh = Min(low.Top, high.Top) - RoofMargin;
            if (bandHigh <= bandLow)
            {
                return bridges;
            }

            // Work in whole floors from here. Snapping a metre value afterwards
            // is what lets a bridge drift out of the band or collide with the
            // one below it; staying in integer floors makes both impossible.
            int firstFloor = CeilToInt(bandLow / FloorHeight);
            int lastFloor = FloorToInt(bandHigh / FloorHeight);
            int availableFloors = lastFloor - firstFloor + 1;
            if (availableFloors < 1)
            {
                return bridges;
            }

            int levels = (int)((bandHigh - bandLow) / MetresPerLevel);
            if (levels < 1)
            {
                levels = 1;
            }
            if (levels > MaxLevels)
            {
                levels = MaxLevels;
            }
            if (levels > availableFloors)
            {
                levels = availableFloors;
            }

            int floorsPerLevel = availableFloors / levels;
            for (int i = 0; i < levels; i++)
            {
                int slotStart = firstFloor + i * floorsPerLevel;
                int floor = slotStart + rng.Below(floorsPerLevel);
                bridges.Add(new Skybridge(floor * FloorHeight,
                    low.AnchorX, low.AnchorY, high.AnchorX, high.AnchorY));
            }

            return bridges;
        }

        /// <summary>
        /// A generator keyed on the ordered pair. Both anchors are folded in,
        /// so moving either building changes the bridges -- and neither end can
        /// reach a different answer than the other.
        /// </summary>
        private DeterministicRandom PairRandom(BuildingSite low, BuildingSite high)
        {
            uint first = WorldHash.Coord(low.AnchorX, low.AnchorY, BridgeSalt, _seed);
            uint second = WorldHash.Coord(high.AnchorX, high.AnchorY, BridgeSalt ^ first, _seed);
            return new DeterministicRandom(((ulong)first << 32) | second, BridgeSalt);
        }

        /// <summary>
        /// The building on the far side of the gap to the east, skipping over a
        /// street if there is one between them. Bridges are meant to span the
        /// canyons, and the canyons are the streets.
        /// </summary>
        public static BuildingSite EastNeighbour(CityWorld world, BuildingSite site)
        {
            return NeighbourAt(world, site, site.AnchorX + site.Role.SpanX, site.AnchorY, 1, 0);
        }

        /// <summary>The building on the far side of the gap to the north.</summary>
        public static BuildingSite NorthNeighbour(CityWorld world, BuildingSite site)
        {
            return NeighbourAt(world, site, site.AnchorX, site.AnchorY + site.Role.SpanY, 0, 1);
        }

        private static BuildingSite NeighbourAt(CityWorld world, BuildingSite site,
            int probeX, int probeY, int stepX, int stepY)
        {
            if (CityLayout.IsStreetChunk(probeX, probeY))
            {
                probeX += stepX;
                probeY += stepY;
            }

            BuildingSite neighbour = world.BuildingAt(probeX, probeY);
            if (neighbour.Exists && neighbour.AnchorX == site.AnchorX && neighbour.AnchorY == site.AnchorY)
            {
                // Stepped over a street straight back into the same building.
                // Cannot happen with the current 1-chunk streets, but a wider
                // street period would make it possible and the failure would be
                // a bridge from a building to itself.
                return BuildingSite.None;
            }
            return neighbour;
        }

        private static float Min(float a, float b) => a < b ? a : b;
        private static float Max(float a, float b) => a > b ? a : b;

        private static int FloorToInt(float v)
        {
            int i = (int)v;
            return v < 0.0f && i != v ? i - 1 : i;
        }

        private static int CeilToInt(float v)
        {
            int i = (int)v;
            return v > 0.0f && i != v ? i + 1 : i;
        }
    }
}

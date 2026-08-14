using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Where a building's exterior decoration goes: balconies now, more later.
    ///
    /// LAYOUT ONLY, NO GEOMETRY, the same split BuildingInterior uses. The
    /// decisions are a pure function of a seed and a size, so a test can ask
    /// where every balcony on a given building landed without building a
    /// single triangle.
    ///
    /// Ported from city_chunk.gd's _add_balconies.
    /// </summary>
    public static class BuildingDecor
    {
        /// <summary>Chance that any one eligible row grows a balcony.</summary>
        public const double BalconyChancePerRow = 0.3;

        /// <summary>
        /// Highest window row that can carry one. Balconies near the top of a
        /// 300m tower are invisible from the street and cost the same as ones
        /// you can see.
        /// </summary>
        public const int BalconyMaxRows = 6;

        public const float BalconyWidth = 2.2f;
        public const float BalconyDepth = 1.1f;
        public const float BalconySlabThickness = 0.12f;
        public const float BalconyRailHeight = 0.9f;
        public const float BalconyRailThickness = 0.08f;

        /// <summary>
        /// Salt for the balcony stream. Independent so adding or removing
        /// balconies cannot shift any other seeded decision on the building --
        /// the same reason CityChunkGenerator keeps its own streams apart.
        /// </summary>
        private const ulong BalconyStream = 0x42414C43UL;   // "BALC"

        /// <summary>One balcony, in the building's own local space.</summary>
        public readonly struct Balcony
        {
            /// <summary>Outward normal of the wall it hangs off. Always axis-aligned.</summary>
            public readonly Vector3 Normal;

            /// <summary>Centre of the slab, already pushed clear of the wall.</summary>
            public readonly Vector3 Centre;

            /// <summary>
            /// True when the balcony's width runs along world X, which is the
            /// case on the walls at fixed Z. Every wall in this city is a plain
            /// +/-X or +/-Z plane, so the normal decides this outright and no
            /// box here ever needs rotating.
            /// </summary>
            public readonly bool WidthAlongX;

            public Balcony(Vector3 normal, Vector3 centre, bool widthAlongX)
            {
                Normal = normal;
                Centre = centre;
                WidthAlongX = widthAlongX;
            }
        }

        /// <summary>
        /// Every balcony on a building, in its own local space.
        ///
        /// ROW ZERO IS SKIPPED. A "balcony" at pavement level reads as a loading
        /// dock, not a balcony -- city_chunk.gd's own reasoning.
        ///
        /// THE DOOR WALL NEVER GETS ONE either, which is why only the first
        /// three walls are eligible: a balcony over the entrance would hang
        /// across the one part of the facade that has to stay readable.
        /// </summary>
        public static List<Balcony> Balconies(int buildingSeed, float width, float depth, float height)
        {
            var placed = new List<Balcony>();

            CellLayout rows = CellLayout.For(height, CellLayout.NominalFloorMetres);
            int topRow = Mathf.Min(rows.Count - 1, BalconyMaxRows);
            if (topRow < 1)
            {
                return placed;
            }

            float halfX = width * 0.5f;
            float halfZ = depth * 0.5f;
            float halfBalcony = BalconyWidth * 0.5f;

            var rng = new DeterministicRandom(unchecked((ulong)(long)buildingSeed), BalconyStream);

            for (int row = 1; row <= topRow; row++)
            {
                if (rng.NextDouble() >= BalconyChancePerRow)
                {
                    continue;
                }

                // The three walls that are not the entrance, in city_chunk.gd's
                // own order so a given seed picks the same one.
                int wall = rng.Below(3);
                Vector3 normal;
                float span;
                bool widthAlongX;
                switch (wall)
                {
                    case 0: normal = Vector3.right; span = halfZ; widthAlongX = false; break;
                    case 1: normal = Vector3.left; span = halfZ; widthAlongX = false; break;
                    default: normal = Vector3.back; span = halfX; widthAlongX = true; break;
                }

                // Too narrow to carry one without it running off the corner.
                if (span * 2.0f < BalconyWidth + 1.0f)
                {
                    continue;
                }

                float along = (float)rng.Range(
                    -span + halfBalcony + 0.5f, span - halfBalcony - 0.5f);
                float y = row * rows.Size + 0.05f;   // just clear of that row's floor line

                Vector3 onWall = widthAlongX
                    ? new Vector3(along, y, normal.z * halfZ)
                    : new Vector3(normal.x * halfX, y, along);

                placed.Add(new Balcony(
                    normal, onWall + normal * (BalconyDepth * 0.5f), widthAlongX));
            }

            return placed;
        }
    }
}

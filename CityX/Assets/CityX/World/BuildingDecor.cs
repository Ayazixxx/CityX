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

        // --- vertical concrete fins ---

        /// <summary>Chance a building is ribbed at all.</summary>
        public const double RibChance = 0.45;

        /// <summary>
        /// Of the ribbed ones, how many rib EVERY wall rather than some of
        /// them. The half that do not are the point: a building that reads as
        /// ribbed concrete from one side and ordinary windows from the next.
        /// </summary>
        public const double RibAllWallsChance = 0.5;

        public const float RibNominalSpacing = 1.15f;

        /// <summary>
        /// How much of each spacing cell the fin fills. The rest is the gap the
        /// windows behind stay visible through.
        /// </summary>
        public const float RibWidthRatio = 0.42f;

        public const float RibMinDepth = 0.18f;
        public const float RibMaxDepth = 0.42f;

        /// <summary>
        /// Fins start this many window rows up, so they never bracket the
        /// doorway or the street-level glass.
        /// </summary>
        public const int RibBaseRows = 1;

        /// <summary>
        /// Clearance kept either side of the door opening. A fin standing in
        /// front of the entrance is the one place the decoration blocks
        /// something the player has to walk through.
        /// </summary>
        public const float RibDoorClearance = 0.3f;

        private const ulong RibStream = 0x52494253UL;   // "RIBS"

        /// <summary>
        /// One fin, as a span along the wall it stands on.
        ///
        /// U runs along the wall's own horizontal extent in building-local
        /// space: world Z for the walls at fixed X, world X for the walls at
        /// fixed Z. Which one is settled by the normal, exactly as with
        /// <see cref="Balcony.WidthAlongX"/>.
        /// </summary>
        public readonly struct Rib
        {
            public readonly Vector3 Normal;
            public readonly float U0;
            public readonly float U1;

            public Rib(Vector3 normal, float u0, float u1)
            {
                Normal = normal;
                U0 = u0;
                U1 = u1;
            }
        }

        /// <summary>Every fin on a building, plus the two numbers they all share.</summary>
        public readonly struct RibbedFacade
        {
            public readonly List<Rib> Fins;

            /// <summary>Height above the lobby floor the fins start at.</summary>
            public readonly float BaseY;

            /// <summary>How far each fin stands proud of its wall.</summary>
            public readonly float Depth;

            public RibbedFacade(List<Rib> fins, float baseY, float depth)
            {
                Fins = fins;
                BaseY = baseY;
                Depth = depth;
            }
        }

        /// <summary>
        /// Vertical concrete fins for a building, or none.
        ///
        /// DECORATION LAYERED IN FRONT OF AN ORDINARY WINDOWED WALL, which is
        /// city_chunk.gd's own decision and its own stated reasoning: making a
        /// wall genuinely windowless would mean threading a per-wall style
        /// through the wall, the collision and the interior, and that
        /// interaction had already shipped two user-visible regressions there.
        /// A densely ribbed wall reads as concrete anyway, because at street
        /// angles the fins occlude most of the glass behind them.
        ///
        /// <paramref name="doorU0"/>/<paramref name="doorU1"/> are the door
        /// opening's span in building-local X, and only the +Z wall honours
        /// them, because that is the only wall with a door in it.
        /// </summary>
        public static RibbedFacade Ribs(int buildingSeed, float width, float depth, float height,
            float doorU0, float doorU1)
        {
            var none = new RibbedFacade(new List<Rib>(), 0.0f, 0.0f);

            var rng = new DeterministicRandom(unchecked((ulong)(long)buildingSeed), RibStream);
            if (rng.NextDouble() >= RibChance)
            {
                return none;
            }

            CellLayout rows = CellLayout.For(height, CellLayout.NominalFloorMetres);
            float baseY = rows.Size * RibBaseRows;
            if (height - baseY < rows.Size)
            {
                // Too short to read as a ribbed facade at all.
                return none;
            }

            float finDepth = (float)rng.Range(RibMinDepth, RibMaxDepth);
            bool ribAll = rng.NextDouble() < RibAllWallsChance;

            float halfX = width * 0.5f;
            float halfZ = depth * 0.5f;
            var fins = new List<Rib>();

            // city_chunk.gd's own wall order, so a wall skipped here is the
            // same wall skipped there for the same draw.
            foreach ((Vector3 normal, float span, bool hasDoor) in new[]
                     {
                         (Vector3.right, halfZ, false),
                         (Vector3.left, halfZ, false),
                         (Vector3.back, halfX, false),
                         (Vector3.forward, halfX, true),
                     })
            {
                if (!ribAll && rng.NextDouble() < 0.5)
                {
                    continue;
                }

                CellLayout cells = CellLayout.For(span * 2.0f, RibNominalSpacing);
                float half = cells.Size * RibWidthRatio * 0.5f;

                for (int i = 0; i < cells.Count; i++)
                {
                    float centre = -span + (i + 0.5f) * cells.Size;
                    if (hasDoor &&
                        centre + half > doorU0 - RibDoorClearance &&
                        centre - half < doorU1 + RibDoorClearance)
                    {
                        continue;
                    }
                    fins.Add(new Rib(normal, centre - half, centre + half));
                }
            }

            return fins.Count == 0 ? none : new RibbedFacade(fins, baseY, finDepth);
        }

        // --- attached secondary massing ---

        public const double WingChance = 0.35;
        public const float WingMinWidthFraction = 0.35f;
        public const float WingMaxWidthFraction = 0.65f;
        public const float WingMinDepth = 3.0f;
        public const float WingMaxDepth = 6.0f;
        public const float WingMinHeightFraction = 0.35f;
        public const float WingMaxHeightFraction = 0.8f;

        /// <summary>Keeps a wing's footprint from reaching past the main building's far corner.</summary>
        public const float WingMargin = 1.0f;

        /// <summary>
        /// Shallower than this and a wing is a bump on a wall rather than a
        /// second volume, so the building keeps its plain box instead. This is
        /// the floor the clearance clamp is tested against -- see
        /// <see cref="WingFor"/>.
        /// </summary>
        public const float WingLeastUsefulDepth = 1.5f;

        private const ulong WingStream = 0x57494E47UL;   // "WING"

        /// <summary>A secondary massing volume attached to one wall.</summary>
        public readonly struct Wing
        {
            public readonly bool Exists;

            /// <summary>Outward normal of the wall it grows from. Never the door wall.</summary>
            public readonly Vector3 Normal;

            /// <summary>Centre offset ALONG that wall, in building-local space.</summary>
            public readonly float Along;

            /// <summary>Span across the wall.</summary>
            public readonly float Width;

            /// <summary>How far it protrudes from the wall.</summary>
            public readonly float Depth;

            public readonly float Height;

            public Wing(Vector3 normal, float along, float width, float depth, float height)
            {
                Exists = true;
                Normal = normal;
                Along = along;
                Width = width;
                Depth = depth;
                Height = height;
            }
        }

        /// <summary>
        /// The L-shaped massing volume a third of buildings grow, or none.
        ///
        /// THE ONE PLACE THIS DEPARTS FROM city_chunk.gd, deliberately and for
        /// a structural reason. Godot picks a wall and protrudes 3 to 6 metres
        /// with no check, which is safe THERE because a Godot building is 9-18m
        /// inside its own 24m chunk and has metres of clear ground all round
        /// it. Here a building fills its lot, and its neighbour's wall is 0.8m
        /// away. Protruding six metres blind would push a solid volume through
        /// the building next door.
        ///
        /// So the wall is still picked blind, in Godot's own draw order, and
        /// the depth is then clamped to whatever room that wall actually has.
        /// A wall with no room grows nothing -- which is why a landlocked lot
        /// in the middle of a block stays a plain box, and a lot facing the
        /// street is the one that gets a wing. That is the right answer for
        /// this city rather than a compromise: the wings appear exactly where
        /// there is space for them to be seen.
        ///
        /// Clearances are the clear ground beyond each wall, which the caller
        /// measures -- this stays a pure function of numbers.
        /// </summary>
        public static Wing WingFor(int buildingSeed, float width, float depth, float height,
            float clearanceMinusX, float clearancePlusX, float clearanceMinusZ)
        {
            var rng = new DeterministicRandom(unchecked((ulong)(long)buildingSeed), WingStream);
            if (rng.NextDouble() >= WingChance)
            {
                return default;
            }

            float halfX = width * 0.5f;
            float halfZ = depth * 0.5f;
            float wingHeight = height *
                               (float)rng.Range(WingMinHeightFraction, WingMaxHeightFraction);
            float wingDepth = (float)rng.Range(WingMinDepth, WingMaxDepth);

            // Godot's own wall_choice: 0 is -X, 1 is +X, 2 is the back wall at
            // -Z. The door wall is never a choice.
            int wall = rng.Below(3);
            Vector3 normal;
            float clearance;
            float span;
            switch (wall)
            {
                case 0: normal = Vector3.left; clearance = clearanceMinusX; span = halfZ; break;
                case 1: normal = Vector3.right; clearance = clearancePlusX; span = halfZ; break;
                default: normal = Vector3.back; clearance = clearanceMinusZ; span = halfX; break;
            }

            wingDepth = Mathf.Min(wingDepth, clearance);
            if (wingDepth < WingLeastUsefulDepth)
            {
                return default;
            }

            float wingWidth = span * 2.0f *
                              (float)rng.Range(WingMinWidthFraction, WingMaxWidthFraction);
            float slack = Mathf.Max(0.0f, span - WingMargin - wingWidth * 0.5f);
            float along = (float)rng.Range(-slack, slack);

            return new Wing(normal, along, wingWidth, wingDepth, wingHeight);
        }
    }
}

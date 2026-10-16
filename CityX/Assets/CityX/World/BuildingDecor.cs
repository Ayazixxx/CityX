using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Where a building's exterior decoration goes: fins, the entrance's approach, and the like.
    ///
    /// LAYOUT ONLY, NO GEOMETRY, the same split BuildingInterior uses. The
    /// decisions are a pure function of a seed and a size, so a test can ask
    /// where every fin on a given building landed without building a
    /// single triangle.
    ///
    /// The random single balconies ported from city_chunk.gd's _add_balconies (a 2.2 m slab with a rail, no door,
    /// hung on a few random floors) were REMOVED on 2026-10-16 (owner: "get rid of these random pointless mini
    /// balconies"). Balconies belong to the apartment blocks now, one on every flat with its door
    /// (ApartmentFront, BalconyDoors); do not bring the random ones back.
    /// </summary>
    public static class BuildingDecor
    {
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

        /// <summary>
        /// Rolled INDEPENDENTLY per candidate wall (see WingsFor), not once
        /// per building -- EXPLICIT ASK: "different sized buildings too dont
        /// have to have 4 corners." One shared roll could only ever produce a
        /// plain box or a single L; checking each of the three walls on its
        /// own is what lets a roomy corner lot grow wings on two or even all
        /// three, reading as a T or a U instead of always an L.
        /// </summary>
        public const double WingChance = 0.4;
        public const float WingMinWidthFraction = 0.35f;
        public const float WingMaxWidthFraction = 0.65f;
        public const float WingMinDepth = 3.0f;
        public const float WingMaxDepth = 8.0f;
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
        /// The L/T/U-shaped massing volumes a building grows off its own
        /// walls, or none. Zero, one, two or all three of the candidate
        /// walls can grow one -- EXPLICIT ASK: "different sized buildings
        /// too dont have to have 4 corners", which a single shared pick
        /// could only ever answer with a plain box or one L.
        ///
        /// THE ONE PLACE THIS DEPARTS FROM city_chunk.gd, deliberately and for
        /// a structural reason. Godot picks a wall and protrudes 3 to 6 metres
        /// with no check, which is safe THERE because a Godot building is 9-18m
        /// inside its own 24m chunk and has metres of clear ground all round
        /// it. Here a building fills its lot, and its neighbour's wall is 0.8m
        /// away. Protruding six metres blind would push a solid volume through
        /// the building next door.
        ///
        /// So each wall is still checked in Godot's own draw order (-X, +X,
        /// back), and each one's depth is clamped to whatever room that wall
        /// actually has. A wall with no room grows nothing -- which is why a
        /// landlocked lot in the middle of a block stays a plain box, and a
        /// corner lot facing two open streets is the one that can grow wings
        /// on both. That is the right answer for this city rather than a
        /// compromise: wings appear exactly where there is space for them to
        /// be seen, and a lot with room on every side can genuinely read as a
        /// U rather than being capped at one protrusion.
        ///
        /// ONE SHARED RNG, DRAWN WALL BY WALL, not three independent
        /// generators -- so the whole sequence (and therefore every wall's
        /// own answer) still depends on nothing but the building's own seed,
        /// matching every other per-building roll in this file.
        ///
        /// Clearances are the clear ground beyond each wall, which the caller
        /// measures -- this stays a pure function of numbers.
        /// </summary>
        public static List<Wing> WingsFor(int buildingSeed, float width, float depth, float height,
            float clearanceMinusX, float clearancePlusX, float clearanceMinusZ)
        {
            var rng = new DeterministicRandom(unchecked((ulong)(long)buildingSeed), WingStream);
            var wings = new List<Wing>(3);

            float halfX = width * 0.5f;
            float halfZ = depth * 0.5f;

            var candidates = new (Vector3 normal, float clearance, float span)[]
            {
                (Vector3.left, clearanceMinusX, halfZ),
                (Vector3.right, clearancePlusX, halfZ),
                (Vector3.back, clearanceMinusZ, halfX),
            };

            foreach ((Vector3 normal, float clearance, float span) in candidates)
            {
                if (rng.NextDouble() >= WingChance)
                {
                    continue;
                }

                float wingHeight = height *
                                   (float)rng.Range(WingMinHeightFraction, WingMaxHeightFraction);
                float wingDepth = Mathf.Min(
                    (float)rng.Range(WingMinDepth, WingMaxDepth), clearance);
                if (wingDepth < WingLeastUsefulDepth)
                {
                    continue;
                }

                float wingWidth = span * 2.0f *
                                  (float)rng.Range(WingMinWidthFraction, WingMaxWidthFraction);
                float slack = Mathf.Max(0.0f, span - WingMargin - wingWidth * 0.5f);
                float along = (float)rng.Range(-slack, slack);

                wings.Add(new Wing(normal, along, wingWidth, wingDepth, wingHeight));
            }

            return wings;
        }

        // --- entrance variety ---

        /// <summary>
        /// Ground mismatch at or below this reads as flush -- an ordinary
        /// door standing right at real ground level, no stairs needed.
        /// </summary>
        public const float EntranceFlushMaxMismatch = 0.4f;

        /// <summary>
        /// Ceiling on how far a single entrance stoop climbs, regardless of
        /// how much bigger the real mismatch is -- the same "cap it and
        /// accept the residual" idiom CityMeshBuilder.SkirtDepth already
        /// uses for a retaining wall's own worst case. Past this and the
        /// stairs would run out over the sidewalk or into the street rather
        /// than reading as part of the building's own stoop -- the existing
        /// solid podium wall below the door (see BuildBuilding's own
        /// "buried on the uphill side" comment) already reads as
        /// intentional hillside construction for whatever gap is left.
        /// </summary>
        public const float EntranceMaxRise = 6.0f;

        /// <summary>Whether a building's own entrance sits flush with the real ground outside it, or needs stairs up to reach it, and by how much.</summary>
        public readonly struct EntranceStyle
        {
            /// <summary>True whenever real exterior steps are needed to reach the door.</summary>
            public readonly bool NeedsStairs;

            /// <summary>Total height the entrance climbs from real ground up to the door. Zero when NeedsStairs is false.</summary>
            public readonly float Rise;

            public EntranceStyle(bool needsStairs, float rise)
            {
                NeedsStairs = needsStairs;
                Rise = rise;
            }
        }

        /// <summary>
        /// EXPLICIT ASK: "generate a bunch of different types of entrances
        /// like san francisco style that fit with the steepness of the
        /// hill... the windows/entrances should never be cut off, always
        /// have like a stairway entrance or some transition to flatness."
        ///
        /// <paramref name="groundMismatch"/> is how far the door's own
        /// threshold sits above the real ground right outside it -- see
        /// CityMeshBuilder.BuildBuilding's own call site for how that is
        /// measured: the highest pad specifically along the door's own row,
        /// not the whole footprint's. BuildingSite.BaseHeight (what the door
        /// actually stands on) is the HIGHEST pad the WHOLE footprint
        /// covers, so a multi-chunk building on a slope routinely has its
        /// own front door standing on a row that is NOT the one that set
        /// BaseHeight -- a real drop between the door's threshold and the
        /// ground its own row actually sits on, exactly the "cut off"
        /// doorway the ask called out. This is deliberately NOT a
        /// randomised roll between invented look categories: the real
        /// mismatch itself already produces the variety asked for --
        /// nothing for a flush lot, a short stoop for a modest one, a real
        /// climbing staircase for a steep one -- without inventing a look a
        /// specific building's own real grade would not actually justify.
        /// </summary>
        public static EntranceStyle EntranceStyleFor(float groundMismatch)
        {
            float rise = Mathf.Clamp(groundMismatch, 0.0f, EntranceMaxRise);
            return new EntranceStyle(rise > EntranceFlushMaxMismatch, rise);
        }

        // --- entrance approach: sloped terrain vs. hard edge ---

        /// <summary>
        /// Independent of every other per-building stream (balconies, ribs,
        /// wings), same reasoning as <see cref="BalconyStream"/>: this
        /// building's own entrance-approach choice must not shift if a
        /// balcony or a rib is added or removed later.
        /// </summary>
        private const ulong EntranceApproachStream = 0x41505052UL;   // "APPR"

        /// <summary>
        /// EXPLICIT ASK: "lets do 60% have the terrain slope into the
        /// entrance... the other 40% of entrances will be more like how you
        /// have them now with hard edges and fencing and staircases." One
        /// roll per building (not per edge) -- a whole frontage reads as one
        /// consistent approach, the same way a real building's own entrance
        /// is one design, not a different one on each side.
        /// </summary>
        public const double EntranceApproachSlopedChance = 0.6;

        /// <summary>
        /// True 60% of the time, deterministically, per building. See
        /// CityMeshBuilder.AddSlopedEntranceApproach for what the two
        /// outcomes actually build.
        /// </summary>
        public static bool EntranceApproachIsSloped(int buildingSeed)
        {
            var rng = new DeterministicRandom(unchecked((ulong)(long)buildingSeed), EntranceApproachStream);
            return rng.NextDouble() < EntranceApproachSlopedChance;
        }
    }
}

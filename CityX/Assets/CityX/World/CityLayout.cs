namespace CityX.World
{
    /// <summary>
    /// The city's street grid. Ported from city_chunk.gd's own
    /// <c>is_street_chunk</c>.
    ///
    /// Pure integer maths over a chunk coordinate, with no state and no
    /// randomness, which is what lets any process answer "is there a road
    /// here" for anywhere in an infinite world without generating it.
    /// </summary>
    public static class CityLayout
    {
        /// <summary>Every Nth row and column is a through-street rather than a buildable lot.</summary>
        public const int StreetPeriod = 4;

        /// <summary>Chunk edge length in metres.</summary>
        public const float ChunkSize = 24.0f;

        /// <summary>
        /// True for a chunk kept clear as a through-street.
        ///
        /// ON posmod, HONESTLY: the original reaches for posmod here and says
        /// it is because GDScript's % goes negative. That is true of C#'s %
        /// too -- but for THIS predicate it makes no difference, and claiming
        /// otherwise would be a comment that sounds careful while being
        /// wrong.
        ///
        ///     x % P == 0   <=>   P divides x   <=>   posmod(x, P) == 0
        ///
        /// Both forms test divisibility; the sign of the remainder only
        /// matters once the VALUE is used rather than compared against zero.
        /// Verified against the Godot dump: 0 of 361 coordinates disagree
        /// between the two forms.
        ///
        /// PositiveMod is kept anyway, and used here, for two reasons that
        /// are real. It states the intent -- "which slot in the repeating
        /// grid" -- rather than relying on a coincidence of the == 0 case.
        /// And the rest of the generator DOES use modulo values directly
        /// (window columns, room indices), where the sign genuinely decides
        /// whether the west half of the world is right or wrong.
        /// </summary>
        public static bool IsStreetChunk(int x, int y)
        {
            return PositiveMod(x, StreetPeriod) == 0 || PositiveMod(y, StreetPeriod) == 0;
        }

        /// <summary>
        /// World X of a chunk's centre, in metres.
        ///
        /// Chunk (cx, cy) covers [cx*ChunkSize, (cx+1)*ChunkSize) on each axis,
        /// so chunk 0 is the square with its CORNER at the origin, not its
        /// centre. Nothing before terrain needed this pinned down -- the
        /// generator only ever took a chunk size, never a chunk position -- so
        /// it is stated once here and everything else asks.
        ///
        /// Double, not float: at the far edges of an effectively infinite world
        /// a float loses sub-metre precision, and a terrain sample that jitters
        /// between two lattice cells is a seam that only appears a long way from
        /// spawn, which is the worst possible place to debug one.
        /// </summary>
        public static double ChunkCentreX(int chunkX)
        {
            return SpanCentre(chunkX, 1);
        }

        /// <summary>World Z of a chunk's centre. See <see cref="ChunkCentreX"/>.</summary>
        public static double ChunkCentreZ(int chunkY)
        {
            return SpanCentre(chunkY, 1);
        }

        /// <summary>
        /// Centre of a run of <paramref name="span"/> chunks starting at
        /// <paramref name="firstChunk"/>, on either axis.
        ///
        /// A building that covers more than one chunk sits at the centre of what
        /// it covers, not at the centre of the chunk that happens to own it --
        /// getting that wrong offsets every multi-chunk building by half a chunk
        /// in a way that reads as "the city is slightly wrong" rather than as a
        /// bug with a location.
        /// </summary>
        public static double SpanCentre(int firstChunk, int span)
        {
            return (firstChunk + span * 0.5) * ChunkSize;
        }

        /// <summary>
        /// Which street-to-street block a chunk belongs to. Blocks tile the
        /// world including negative space, so this floors rather than truncating.
        /// </summary>
        public static int BlockIndex(int chunkCoord)
        {
            return (chunkCoord - PositiveMod(chunkCoord, StreetPeriod)) / StreetPeriod;
        }

        /// <summary>
        /// A chunk's slot inside its block, 0..StreetPeriod-1. Slot 0 is the
        /// street; the buildable interior is slots 1..StreetPeriod-1.
        /// </summary>
        public static int IndexInBlock(int chunkCoord)
        {
            return PositiveMod(chunkCoord, StreetPeriod);
        }

        /// <summary>
        /// Buildable chunks per axis inside one block -- 3, for a street period
        /// of 4. Named because "StreetPeriod - 1" appears in enough places that
        /// it stops being obviously the same 3 every time.
        /// </summary>
        public const int BuildableSpan = StreetPeriod - 1;

        /// <summary>
        /// Modulo that always returns a non-negative result, matching
        /// GDScript's posmod. Named for what it guarantees rather than what
        /// it wraps.
        /// </summary>
        public static int PositiveMod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}

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

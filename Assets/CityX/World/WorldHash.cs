namespace CityX.World
{
    /// <summary>
    /// One hash from a world coordinate to a number, shared by everything that
    /// needs to decide something about a place without generating it.
    ///
    /// WHY IT IS ITS OWN FILE. Terrain and block massing both need exactly this
    /// and arrived at it independently. Two copies of an avalanche hash is the
    /// kind of duplication that looks harmless right up until one of them is
    /// tuned and half the world quietly stops agreeing with the other half.
    ///
    /// This is NOT a replacement for <see cref="DeterministicRandom"/>. That is
    /// a sequence -- draw after draw from one seed, which is what a building
    /// wants when it is rolling a dozen related decisions in order. This is a
    /// lookup: ask about any coordinate, in any order, get the same answer,
    /// with no cursor to keep in sync. Streaming asks about chunks in whatever
    /// order the player walks, so a sequence would be the wrong shape.
    /// </summary>
    public static class WorldHash
    {
        /// <summary>
        /// Avalanche a coordinate pair, a salt and the world seed into 32 bits.
        ///
        /// The two per-input multipliers differ so (x, y) and (y, x) do not
        /// collide, which would mirror the whole world about its diagonal --
        /// the kind of bug that looks like a texture problem for a week.
        ///
        /// The (ulong)(uint) casts reinterpret rather than sign-extend, so
        /// negative coordinates land on distinct lattice points instead of
        /// folding onto positive ones.
        ///
        /// Finaliser constants are SplitMix64's.
        /// </summary>
        public static uint Coord(int x, int y, ulong salt, ulong worldSeed)
        {
            unchecked
            {
                ulong h = (ulong)(uint)x * 0x9E3779B97F4A7C15UL;
                h ^= (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL;
                h ^= (worldSeed + salt) * 0x165667B19E3779F9UL;
                h ^= h >> 29;
                h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 32;
                h *= 0x94D049BB133111EBUL;
                h ^= h >> 29;
                return (uint)(h >> 32);
            }
        }

        /// <summary>
        /// The same hash as a double in [0,1). Divides by 2^32 rather than
        /// 2^32-1 so the result can never reach exactly 1.0 and push a
        /// `(int)(r * count)` one past the end of its table.
        /// </summary>
        public static double Unit01(int x, int y, ulong salt, ulong worldSeed)
        {
            return Coord(x, y, salt, worldSeed) / 4294967296.0;
        }
    }
}

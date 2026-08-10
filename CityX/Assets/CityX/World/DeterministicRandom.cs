namespace CityX.World
{
    /// <summary>
    /// A PCG32 generator with a fully specified algorithm, written out here
    /// rather than delegated to the platform.
    ///
    /// WHY NOT System.Random OR UnityEngine.Random.
    ///
    /// PORT_PLAN's single biggest carried asset is "the world is a seed, not a
    /// level": server and client generate byte-identical cities, so no
    /// geometry is ever sent over the network. That entire guarantee rests on
    /// both processes drawing the same numbers.
    ///
    /// The failure mode is specific and nasty. A regional shard runs on LINUX
    /// while players run WINDOWS.
    ///
    ///   * System.Random's algorithm is explicitly NOT contractually stable --
    ///     Microsoft changed it between .NET Framework and .NET Core, and
    ///     documents that it may change again. Two runtimes, two cities.
    ///   * UnityEngine.Random is global mutable state shared by every system
    ///     in the process, so anything else drawing a number -- a particle
    ///     effect, a piece of UI -- silently shifts the world.
    ///
    /// Neither would fail loudly. Buildings would simply be somewhere else for
    /// some players, and every downstream system that trusts world geometry
    /// would be quietly wrong.
    ///
    /// So the algorithm is written out in full, in integer arithmetic with
    /// defined overflow. unchecked() is deliberate: PCG32 REQUIRES wrapping
    /// multiplication, and a checked context would throw instead.
    ///
    /// This is intentionally NOT bit-compatible with Godot's RandomNumberGenerator.
    /// Matching Godot exactly would pin this project to another engine's
    /// internals forever, and buys nothing -- the requirement is that Unity
    /// agrees with Unity.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong DefaultStream = 1442695040888963407UL;

        private ulong _state;
        private readonly ulong _increment;

        public DeterministicRandom(ulong seed, ulong stream = DefaultStream)
        {
            // The increment must be odd for PCG's period to be full.
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            unchecked
            {
                _state += seed;
            }
            NextUInt();
        }

        /// <summary>
        /// Convenience for the common case of seeding from a signed value.
        /// Cast rather than clamped, so negative seeds stay distinct instead
        /// of collapsing onto zero.
        /// </summary>
        public DeterministicRandom(int seed) : this(unchecked((ulong)(long)seed))
        {
        }

        /// <summary>One raw 32-bit draw. Everything else is built on this.</summary>
        public uint NextUInt()
        {
            ulong oldState = _state;
            unchecked
            {
                _state = oldState * Multiplier + _increment;
            }
            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rot = (int)(oldState >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>
        /// Uniform in [0,1). Divides by 2^32 rather than 2^32-1 so the result
        /// can never reach exactly 1.0 -- a value of 1.0 would push every
        /// `index = (int)(r * count)` in the codebase one past the end of its
        /// array, which is a rare crash that only shows up in the field.
        /// </summary>
        public double NextDouble()
        {
            return NextUInt() / 4294967296.0;
        }

        /// <summary>Uniform in [min,max).</summary>
        public double Range(double min, double max)
        {
            return min + NextDouble() * (max - min);
        }

        /// <summary>
        /// Uniform integer in [0,count). Uses rejection sampling rather than
        /// a plain modulo: modulo biases the low values whenever count does
        /// not divide 2^32, which for a small count like a roof-type list is
        /// a real, visible skew in what the city looks like.
        /// </summary>
        public int Below(int count)
        {
            if (count <= 1)
            {
                return 0;
            }

            uint bound = (uint)count;
            uint threshold = (uint)(-(int)bound) % bound;
            while (true)
            {
                uint draw = NextUInt();
                if (draw >= threshold)
                {
                    return (int)(draw % bound);
                }
            }
        }
    }
}

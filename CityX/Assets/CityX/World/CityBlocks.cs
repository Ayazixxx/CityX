namespace CityX.World
{
    /// <summary>What a chunk is, as far as building placement is concerned.</summary>
    public enum LotKind
    {
        /// <summary>Kept clear as a through-street.</summary>
        Street,

        /// <summary>Covered by a larger building anchored on a different chunk.</summary>
        Consumed,

        /// <summary>Builds here, covering Span chunks.</summary>
        Building,
    }

    /// <summary>
    /// A chunk's part in its block's massing. See <see cref="CityBlocks"/>.
    /// </summary>
    public readonly struct LotRole
    {
        public readonly LotKind Kind;

        /// <summary>Chunks covered on each axis. 1x1 for an ordinary lot, 0 for Street.</summary>
        public readonly int SpanX;
        public readonly int SpanY;

        /// <summary>
        /// The chunk that owns this building. Equals the queried chunk when
        /// Kind is Building; for Consumed it points at the anchor, so a chunk
        /// can find out what covers it without searching.
        /// </summary>
        public readonly int AnchorX;
        public readonly int AnchorY;

        public LotRole(LotKind kind, int spanX, int spanY, int anchorX, int anchorY)
        {
            Kind = kind;
            SpanX = spanX;
            SpanY = spanY;
            AnchorX = anchorX;
            AnchorY = anchorY;
        }

        public static LotRole Street => new LotRole(LotKind.Street, 0, 0, 0, 0);

        /// <summary>True when this chunk should actually produce geometry.</summary>
        public bool Builds => Kind == LotKind.Building;

        /// <summary>Covers more than one chunk.</summary>
        public bool IsMultiChunk => SpanX > 1 || SpanY > 1;
    }

    /// <summary>
    /// Decides how a city block is massed: whether a big building takes several
    /// chunks, and which ones.
    ///
    /// WHY THIS EXISTS AT ALL. A chunk is 24m, so after lot margins a
    /// single-chunk footprint is about 18m across. The art direction asks for
    /// towers of 150-200m. An 18m-wide building 200m tall is a 11:1 needle --
    /// not a tower, a mast. Real towers of that height stand on 35-50m
    /// footprints. So either the buildings get shorter or a building learns to
    /// cover more than one chunk, and the second is the one that matches the
    /// reference.
    ///
    /// The street grid already hands us the right unit to work in. With a
    /// street period of 4, every block is a 3x3 of buildable chunks. A 2x2
    /// building inside that is ~42m square -- a genuine tower at 4-5:1 -- and
    /// leaves an L of five ordinary lots around it, which is a far more
    /// Chongqing arrangement than a uniform grid of identical boxes.
    /// A 2x1 gives a ~42x18m slab, which is the long blank concrete face the
    /// brutalist reference is full of.
    ///
    /// HOW IT STAYS PURE, WHICH IS THE WHOLE TRICK. Chunks generate
    /// independently and never talk to each other -- that is what makes the
    /// world infinite and lets server and client agree without sending
    /// geometry. So a chunk cannot be TOLD it is covered by its neighbour's
    /// tower. Instead every chunk derives the same answer from the block seed:
    /// the anchor works out that it builds a 2x2, and the three chunks under it
    /// independently work out that they are covered. Nothing is communicated,
    /// and there is no order dependence to get wrong.
    ///
    /// This is the same shape the Godot build used for skybridges, where both
    /// ends had to agree a bridge existed from their own side alone.
    /// </summary>
    public sealed class CityBlocks
    {
        /// <summary>
        /// Relative weights for a block's massing. Deliberately does not sum to
        /// 1 in the "one of these always happens" sense -- see NoSuperlotChance.
        /// </summary>
        public const double TowerChance = 0.34;   // 2x2

        public const double SlabWideChance = 0.20;   // 2x1, long east-west
        public const double SlabDeepChance = 0.20;   // 1x2, long north-south

        /// <summary>Whatever is left: a block of nine ordinary single lots.</summary>
        public const double NoSuperlotChance =
            1.0 - TowerChance - SlabWideChance - SlabDeepChance;

        // Distinct salts so shape and the two anchor axes are independent.
        // Sharing one would correlate a block's shape with where it sits, and
        // every tower in the world would end up in the same corner.
        private const ulong ShapeSalt = 0x424C4B53UL;   // "BLKS"
        private const ulong AnchorXSalt = 0x424C4B41UL; // "BLKA"
        private const ulong AnchorYSalt = 0x424C4B42UL; // "BLKB"

        private readonly ulong _seed;

        public CityBlocks(int worldSeed)
        {
            _seed = unchecked((ulong)(long)worldSeed);
        }

        /// <summary>
        /// What this chunk should do. Total: every chunk in the world is
        /// exactly one of Street, Consumed or Building, with no gaps and no
        /// overlaps.
        /// </summary>
        public LotRole RoleFor(int chunkX, int chunkY)
        {
            if (CityLayout.IsStreetChunk(chunkX, chunkY))
            {
                return LotRole.Street;
            }

            int blockX = CityLayout.BlockIndex(chunkX);
            int blockY = CityLayout.BlockIndex(chunkY);

            // Position within the block's buildable interior, 0..2.
            int interiorX = CityLayout.IndexInBlock(chunkX) - 1;
            int interiorY = CityLayout.IndexInBlock(chunkY) - 1;

            ShapeFor(blockX, blockY, out int spanX, out int spanY);
            if (spanX == 1 && spanY == 1)
            {
                return new LotRole(LotKind.Building, 1, 1, chunkX, chunkY);
            }

            AnchorFor(blockX, blockY, spanX, spanY, out int anchorInteriorX, out int anchorInteriorY);

            bool insideX = interiorX >= anchorInteriorX && interiorX < anchorInteriorX + spanX;
            bool insideY = interiorY >= anchorInteriorY && interiorY < anchorInteriorY + spanY;
            if (!insideX || !insideY)
            {
                // Beside the big building, not under it.
                return new LotRole(LotKind.Building, 1, 1, chunkX, chunkY);
            }

            int anchorChunkX = blockX * CityLayout.StreetPeriod + 1 + anchorInteriorX;
            int anchorChunkY = blockY * CityLayout.StreetPeriod + 1 + anchorInteriorY;

            if (interiorX == anchorInteriorX && interiorY == anchorInteriorY)
            {
                return new LotRole(LotKind.Building, spanX, spanY, anchorChunkX, anchorChunkY);
            }

            return new LotRole(LotKind.Consumed, spanX, spanY, anchorChunkX, anchorChunkY);
        }

        /// <summary>
        /// The block's big-building shape, in chunks. 1x1 means the block has no
        /// superlot at all and is nine ordinary buildings.
        /// </summary>
        private void ShapeFor(int blockX, int blockY, out int spanX, out int spanY)
        {
            double roll = WorldHash.Unit01(blockX, blockY, ShapeSalt, _seed);

            if (roll < TowerChance)
            {
                spanX = 2;
                spanY = 2;
                return;
            }
            if (roll < TowerChance + SlabWideChance)
            {
                spanX = 2;
                spanY = 1;
                return;
            }
            if (roll < TowerChance + SlabWideChance + SlabDeepChance)
            {
                spanX = 1;
                spanY = 2;
                return;
            }

            spanX = 1;
            spanY = 1;
        }

        /// <summary>
        /// Where in the 3x3 interior the big building's low corner sits. The
        /// range is clamped by the span so it can never overhang into the
        /// street -- a tower half in the road would be a bug that only appears
        /// on some seeds, which is the worst kind.
        /// </summary>
        private void AnchorFor(int blockX, int blockY, int spanX, int spanY,
            out int anchorX, out int anchorY)
        {
            int slotsX = CityLayout.BuildableSpan - spanX + 1;
            int slotsY = CityLayout.BuildableSpan - spanY + 1;

            anchorX = (int)(WorldHash.Unit01(blockX, blockY, AnchorXSalt, _seed) * slotsX);
            anchorY = (int)(WorldHash.Unit01(blockX, blockY, AnchorYSalt, _seed) * slotsY);
        }
    }
}

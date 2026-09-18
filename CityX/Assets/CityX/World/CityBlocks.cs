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

        /// <summary>
        /// Part of a block left open -- a courtyard, plaza or yard. Distinct
        /// from a lot that merely rolled empty: a whole block of these is one
        /// continuous piece of open ground, and the renderer will eventually
        /// want to pave it rather than leaving bare terrain.
        /// </summary>
        Open,
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
        ///
        /// RAISED FROM 0.34 -- EXPLICIT ASK: "separate areas by neighborhood
        /// ... not everything should be a blocky grid" introduced districts
        /// whose own local block size varies (CityLayout.LocalStreetPeriodFor),
        /// and a single 2x2 tower covers a much smaller SHARE of a large,
        /// sparse district's own block than it does a small, dense one's --
        /// confirmed directly, live: the same 0.34 that used to clear the
        /// art direction's own "the skyline has nothing in it" floor measured
        /// only 1.7% city-wide once bigger blocks started diluting it.
        /// </summary>
        public const double TowerChance = 0.45;   // 2x2

        public const double SlabWideChance = 0.20;   // 2x1, long east-west
        public const double SlabDeepChance = 0.20;   // 1x2, long north-south

        /// <summary>Whatever is left: a block of nine ordinary single lots.</summary>
        public const double NoSuperlotChance =
            1.0 - TowerChance - SlabWideChance - SlabDeepChance;

        /// <summary>
        /// Share of blocks left entirely open -- a courtyard, plaza or yard
        /// instead of buildings.
        ///
        /// WHY A WHOLE BLOCK AND NOT JUST MORE EMPTY LOTS. The generator
        /// already leaves about one lot in ten empty, and that reads as a gap
        /// in a wall rather than as somewhere to be: 20m of dead ground boxed
        /// in on all sides. An open BLOCK is 72m square of continuous space
        /// with streets on every side, which is a place. Rolled per block for
        /// exactly that reason -- rolling it per lot would scatter the same
        /// area into nine holes and produce neither.
        ///
        /// This roll happens BEFORE the massing roll and short-circuits it, so
        /// tuning open space does not reshuffle which blocks get towers.
        /// </summary>
        public const double OpenBlockChance = 0.11;

        // Distinct salts so shape and the two anchor axes are independent.
        // Sharing one would correlate a block's shape with where it sits, and
        // every tower in the world would end up in the same corner.
        private const ulong ShapeSalt = 0x424C4B53UL;   // "BLKS"
        private const ulong AnchorXSalt = 0x424C4B41UL; // "BLKA"
        private const ulong AnchorYSalt = 0x424C4B42UL; // "BLKB"
        private const ulong OpenSalt = 0x4F50454EUL;    // "OPEN"

        private readonly ulong _seed;

        public CityBlocks(int worldSeed)
        {
            _seed = unchecked((ulong)(long)worldSeed);
        }

        /// <summary>
        /// What this chunk should do. Total: every chunk in the world is
        /// exactly one of Street, Consumed or Building, with no gaps and no
        /// overlaps.
        ///
        /// BLOCK IDENTITY IS NOW A REAL CHUNK COORDINATE, NOT A SEQUENTIAL
        /// INDEX -- since CityLayout.LocalStreetPeriodFor lets a
        /// neighbourhood's own block size vary, blocks in two different
        /// districts can no longer share one linearly-scaled index space
        /// without colliding (district A's "block 3" and district B's own
        /// "block 3" would otherwise roll identical buildings). Using each
        /// block's own low-corner chunk coordinate as the hash key instead
        /// is exactly as deterministic and never collides, because two
        /// blocks never share a low corner.
        /// </summary>
        /// <summary>
        /// Which block a (buildable) chunk belongs to, as the real chunk
        /// coordinate of that block's own low corner, plus how many chunks
        /// deep its buildable interior runs. Public so tests can enumerate
        /// and group blocks the same way RoleFor itself does, rather than
        /// re-deriving the district/period maths a second time and risking
        /// the two drifting apart.
        /// </summary>
        public void BlockOriginFor(int chunkX, int chunkY, out int blockX, out int blockY, out int buildableSpan)
        {
            int districtX = CityLayout.DistrictIndex(chunkX);
            int districtY = CityLayout.DistrictIndex(chunkY);
            int period = CityLayout.LocalStreetPeriodFor(districtX, districtY);
            buildableSpan = period - 1;

            int localX = chunkX - districtX * CityLayout.DistrictChunks;
            int localY = chunkY - districtY * CityLayout.DistrictChunks;
            blockX = chunkX - CityLayout.PositiveMod(localX, period);
            blockY = chunkY - CityLayout.PositiveMod(localY, period);
        }

        public LotRole RoleFor(int chunkX, int chunkY)
        {
            if (CityLayout.IsStreetChunk(chunkX, chunkY))
            {
                return LotRole.Street;
            }

            BlockOriginFor(chunkX, chunkY, out int blockX, out int blockY, out int buildableSpan);
            int inBlockX = chunkX - blockX;
            int inBlockY = chunkY - blockY;

            if (IsOpenBlock(blockX, blockY))
            {
                return new LotRole(LotKind.Open, 1, 1, chunkX, chunkY);
            }

            // Position within the block's buildable interior, 0..buildableSpan-1.
            int interiorX = inBlockX - 1;
            int interiorY = inBlockY - 1;

            ShapeFor(blockX, blockY, buildableSpan, out int spanX, out int spanY);
            if (spanX == 1 && spanY == 1)
            {
                return new LotRole(LotKind.Building, 1, 1, chunkX, chunkY);
            }

            AnchorFor(blockX, blockY, buildableSpan, spanX, spanY, out int anchorInteriorX, out int anchorInteriorY);

            bool insideX = interiorX >= anchorInteriorX && interiorX < anchorInteriorX + spanX;
            bool insideY = interiorY >= anchorInteriorY && interiorY < anchorInteriorY + spanY;
            if (!insideX || !insideY)
            {
                // Beside the big building, not under it.
                return new LotRole(LotKind.Building, 1, 1, chunkX, chunkY);
            }

            int anchorChunkX = blockX + 1 + anchorInteriorX;
            int anchorChunkY = blockY + 1 + anchorInteriorY;

            if (interiorX == anchorInteriorX && interiorY == anchorInteriorY)
            {
                return new LotRole(LotKind.Building, spanX, spanY, anchorChunkX, anchorChunkY);
            }

            return new LotRole(LotKind.Consumed, spanX, spanY, anchorChunkX, anchorChunkY);
        }

        /// <summary>
        /// Whether this whole block is left open. Public so a renderer can pave
        /// a plaza differently from bare ground, and so tests can count them.
        /// </summary>
        public bool IsOpenBlock(int blockX, int blockY)
        {
            return WorldHash.Unit01(blockX, blockY, OpenSalt, _seed) < OpenBlockChance;
        }

        /// <summary>
        /// The block's big-building shape, in chunks. 1x1 means the block has no
        /// superlot at all and is nine ordinary buildings.
        ///
        /// A superlot's own span (up to 2x2) never exceeds the smallest
        /// possible buildableSpan (2, from a period-3 district's own
        /// 2-chunk interior) -- so this needs no bound check itself, the
        /// same way it never did when every district shared one period.
        /// </summary>
        private void ShapeFor(int blockX, int blockY, int buildableSpan, out int spanX, out int spanY)
        {
            double roll = WorldHash.Unit01(blockX, blockY, ShapeSalt, _seed);

            if (roll < TowerChance && buildableSpan >= 2)
            {
                spanX = 2;
                spanY = 2;
                return;
            }
            if (roll < TowerChance + SlabWideChance && buildableSpan >= 2)
            {
                spanX = 2;
                spanY = 1;
                return;
            }
            if (roll < TowerChance + SlabWideChance + SlabDeepChance && buildableSpan >= 2)
            {
                spanX = 1;
                spanY = 2;
                return;
            }

            spanX = 1;
            spanY = 1;
        }

        /// <summary>
        /// Where in the block's own buildable interior the big building's low
        /// corner sits. The range is clamped by the span so it can never
        /// overhang into the street -- a tower half in the road would be a
        /// bug that only appears on some seeds, which is the worst kind.
        /// </summary>
        private void AnchorFor(int blockX, int blockY, int buildableSpan, int spanX, int spanY,
            out int anchorX, out int anchorY)
        {
            int slotsX = buildableSpan - spanX + 1;
            int slotsY = buildableSpan - spanY + 1;

            anchorX = (int)(WorldHash.Unit01(blockX, blockY, AnchorXSalt, _seed) * slotsX);
            anchorY = (int)(WorldHash.Unit01(blockX, blockY, AnchorYSalt, _seed) * slotsY);
        }
    }
}

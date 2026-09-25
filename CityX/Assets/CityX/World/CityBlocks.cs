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
    /// Decides how a city block is massed: which lots it splits into, whether a
    /// big building takes several chunks, and whether the whole block is left
    /// open as a park or plaza.
    ///
    /// WHY BIG BUILDINGS EXIST AT ALL. A chunk is 24m, so after lot margins a
    /// single-chunk footprint is about 18m across. The art direction asks for
    /// towers of 150-200m. An 18m-wide building 200m tall is a 11:1 needle --
    /// not a tower, a mast. Real towers of that height stand on 35-50m
    /// footprints. So either the buildings get shorter or a building learns to
    /// cover more than one chunk, and the second is the one that matches the
    /// reference. A 2x2 lot is ~42m square (a genuine tower at 4-5:1); a 2x1 is
    /// a ~42x18m slab, the long blank concrete face the brutalist reference is
    /// full of.
    ///
    /// LOTS COME FROM RECURSIVE SUBDIVISION, NOT A FIXED PATTERN. Parish and
    /// Muller's lot generator repeatedly splits a block along its longer axis
    /// until the pieces are building-sized. The same shape works here and now
    /// carries the whole thing: a block of ANY size from 2x2 to 9x9 chunks is
    /// split (the cut lands anywhere in the middle 30-70% of the long side)
    /// until every piece is at most 2x2 chunks, and each piece then either
    /// stays one big lot or breaks into smaller ones. That gives irregular
    /// mixed lots in a big block where the old fixed 3x3 block always got one
    /// superlot and nine singles.
    ///
    /// WHAT DECIDES A PIECE'S FATE IS THE ZONE (see CityZones). A downtown 2x2
    /// piece is nearly always a tower; the same piece at the edge of the city
    /// almost always breaks into small lots; an industrial 2x1 stays one big
    /// shed. The block's zone is read at its centre, so a whole block agrees.
    ///
    /// HOW IT STAYS PURE, WHICH IS THE WHOLE TRICK. Chunks generate
    /// independently and never talk to each other -- that is what makes the
    /// world infinite and lets server and client agree without sending
    /// geometry. So a chunk cannot be TOLD it is covered by its neighbour's
    /// tower. Instead every chunk re-runs the same subdivision from the block
    /// seed: the anchor works out that it builds a 2x2, and the three chunks
    /// under it independently work out that they are covered. Nothing is
    /// communicated, and there is no order dependence to get wrong.
    ///
    /// This is the same shape the Godot build used for skybridges, where both
    /// ends had to agree a bridge existed from their own side alone.
    /// </summary>
    public sealed class CityBlocks
    {
        /// <summary>
        /// The share of blocks left entirely open -- a courtyard, plaza or
        /// park instead of buildings -- by zone. Rolled per BLOCK for a real
        /// reason: an open block is 48-200m of continuous space with streets
        /// on every side, which is a place, where rolling it per lot would
        /// scatter the same area into a hole in every wall.
        ///
        /// Far more common at the edge of the city than downtown: the edge is
        /// where the green is, and a core keeps only the odd plaza.
        /// </summary>
        public static double OpenBlockChanceFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.05;
                case ZoneKind.Urban: return 0.08;
                case ZoneKind.OldTown: return 0.06;
                case ZoneKind.Industrial: return 0.05;
                case ZoneKind.Outskirts: return 0.30;
                default: return 0.11;
            }
        }

        /// <summary>
        /// Chance a 2x2 piece of a block stays ONE tower lot, by zone. The
        /// rest break into smaller lots (slabs or singles).
        /// </summary>
        public static double TowerChanceFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.66;
                case ZoneKind.Urban: return 0.50;
                case ZoneKind.Residential: return 0.24;
                case ZoneKind.OldTown: return 0.0;
                case ZoneKind.Industrial: return 0.50;
                default: return 0.06;
            }
        }

        /// <summary>
        /// Chance a 2x1 or 1x2 piece stays ONE slab lot, by zone. The rest
        /// split into two single lots.
        /// </summary>
        public static double SlabChanceFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.35;
                case ZoneKind.Urban: return 0.28;
                case ZoneKind.Residential: return 0.14;
                case ZoneKind.OldTown: return 0.05;
                case ZoneKind.Industrial: return 0.75;
                default: return 0.08;
            }
        }

        // Distinct salts so every decision is independent. Sharing one would
        // correlate, say, a block's split position with where it sits.
        private const ulong OpenSalt = 0x4F50454EUL;     // "OPEN"
        private const ulong AxisSalt = 0x41584953UL;     // "AXIS"
        private const ulong SplitSalt = 0x53504C54UL;    // "SPLT"
        private const ulong TowerSalt = 0x544F5752UL;    // "TOWR"
        private const ulong SlabSalt = 0x534C4142UL;     // "SLAB"
        private const ulong SlabAxisSalt = 0x534C4158UL; // "SLAX"

        /// <summary>A block is at most 9 chunks a side, so a subdivision this deep always terminates.</summary>
        private const int MaxDepth = 16;

        private readonly ulong _seed;

        public CityBlocks(int worldSeed)
        {
            _seed = unchecked((ulong)(long)worldSeed);
        }

        /// <summary>The zone a whole block belongs to, read at its centre so every chunk in it agrees.</summary>
        public static ZoneKind ZoneOfBlock(int minX, int minY, int width, int height)
        {
            return CityZones.ZoneAt(minX + width * 0.5, minY + height * 0.5);
        }

        /// <summary>
        /// What this chunk should do. Total: every chunk in the world is
        /// exactly one of Street, Consumed, Building or Open, with no gaps and
        /// no overlaps.
        ///
        /// BLOCK IDENTITY IS THE BLOCK INTERIOR'S OWN LOW-CORNER CHUNK
        /// COORDINATE -- blocks vary in size across districts, so a sequential
        /// index could not be shared between two districts without colliding.
        /// The low corner is exactly as deterministic and two blocks never
        /// share one.
        /// </summary>
        public LotRole RoleFor(int chunkX, int chunkY)
        {
            if (!CityLayout.TryGetBlockRect(chunkX, chunkY, out int minX, out int minY, out int width, out int height))
            {
                return LotRole.Street;
            }

            ZoneKind zone = ZoneOfBlock(minX, minY, width, height);
            if (IsOpenBlockOfZone(minX, minY, zone))
            {
                return new LotRole(LotKind.Open, 1, 1, chunkX, chunkY);
            }

            FindLot(minX, minY, width, height, chunkX - minX, chunkY - minY, zone,
                out int lotX, out int lotY, out int spanX, out int spanY);

            int anchorX = minX + lotX;
            int anchorY = minY + lotY;
            if (spanX == 1 && spanY == 1)
            {
                return new LotRole(LotKind.Building, 1, 1, chunkX, chunkY);
            }
            if (anchorX == chunkX && anchorY == chunkY)
            {
                return new LotRole(LotKind.Building, spanX, spanY, anchorX, anchorY);
            }
            return new LotRole(LotKind.Consumed, spanX, spanY, anchorX, anchorY);
        }

        /// <summary>
        /// Whether the block whose interior starts at this chunk is left open.
        /// Public so a renderer can treat a plaza differently from bare
        /// ground, and so tests can count them. The coordinate must be the
        /// interior's low corner (what <see cref="CityLayout.TryGetBlockRect"/> returns).
        /// </summary>
        public bool IsOpenBlock(int blockMinX, int blockMinY)
        {
            if (!CityLayout.TryGetBlockRect(blockMinX, blockMinY, out int minX, out int minY, out int width, out int height))
            {
                return false;
            }
            return IsOpenBlockOfZone(minX, minY, ZoneOfBlock(minX, minY, width, height));
        }

        private bool IsOpenBlockOfZone(int minX, int minY, ZoneKind zone)
        {
            return WorldHash.Unit01(minX, minY, OpenSalt, _seed) < OpenBlockChanceFor(zone);
        }

        /// <summary>
        /// One independent roll for one node of one block's subdivision.
        /// (nodeX, nodeY, nodeW, nodeH) is the node's rectangle inside the
        /// block interior; a block is at most 9 chunks a side, so the packing
        /// below cannot collide.
        /// </summary>
        private double Roll(int blockX, int blockY, int nodeX, int nodeY, int nodeW, int nodeH, ulong salt)
        {
            uint node = WorldHash.Coord(nodeX * 64 + nodeW, nodeY * 64 + nodeH, salt, _seed);
            return WorldHash.Unit01(blockX, blockY, salt ^ node, _seed);
        }

        /// <summary>
        /// The lot that covers interior cell (targetX, targetY) of a block:
        /// its low corner and size, in block-interior coordinates.
        /// </summary>
        private void FindLot(int blockX, int blockY, int width, int height, int targetX, int targetY,
            ZoneKind zone, out int lotX, out int lotY, out int spanX, out int spanY)
        {
            int rx = 0, ry = 0, rw = width, rh = height;

            // Cut along the longer side until the piece is at most 2x2.
            for (int depth = 0; depth < MaxDepth && (rw > 2 || rh > 2); depth++)
            {
                bool cutAcross;   // true: cut the X extent in two
                if (rw > 2 && rh <= 2)
                {
                    cutAcross = true;
                }
                else if (rh > 2 && rw <= 2)
                {
                    cutAcross = false;
                }
                else if (rw != rh)
                {
                    cutAcross = rw > rh;
                }
                else
                {
                    cutAcross = Roll(blockX, blockY, rx, ry, rw, rh, AxisSalt) < 0.5;
                }

                int length = cutAcross ? rw : rh;
                double where = Roll(blockX, blockY, rx, ry, rw, rh, SplitSalt);
                int first = (int)System.Math.Round(length * (0.35 + 0.30 * where));
                if (first < 1) first = 1;
                if (first > length - 1) first = length - 1;

                if (cutAcross)
                {
                    if (targetX < rx + first) { rw = first; }
                    else { rx += first; rw -= first; }
                }
                else
                {
                    if (targetY < ry + first) { rh = first; }
                    else { ry += first; rh -= first; }
                }
            }

            // A piece of at most 2x2: keep it whole or break it up.
            lotX = rx;
            lotY = ry;
            spanX = rw;
            spanY = rh;

            if (rw == 2 && rh == 2)
            {
                if (Roll(blockX, blockY, rx, ry, rw, rh, TowerSalt) < TowerChanceFor(zone))
                {
                    return;   // one tower lot
                }

                // Break into two slab-sized halves, the cut direction rolled.
                if (Roll(blockX, blockY, rx, ry, rw, rh, SlabAxisSalt) < 0.5)
                {
                    rw = 1;
                    rx += targetX - rx;   // the half (a 1x2 column) holding the target
                }
                else
                {
                    rh = 1;
                    ry += targetY - ry;   // the half (a 2x1 row) holding the target
                }
                lotX = rx;
                lotY = ry;
                spanX = rw;
                spanY = rh;
            }

            if (rw * rh == 2)
            {
                if (Roll(blockX, blockY, lotX, lotY, spanX, spanY, SlabSalt) < SlabChanceFor(zone))
                {
                    return;   // one slab lot
                }
                lotX = targetX;
                lotY = targetY;
                spanX = 1;
                spanY = 1;
            }
        }
    }
}

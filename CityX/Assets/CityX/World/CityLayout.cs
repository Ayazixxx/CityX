namespace CityX.World
{
    /// <summary>
    /// The city's street network.
    ///
    /// Pure integer maths over a chunk coordinate, with no state and no world
    /// seed (see <see cref="LayoutSeed"/> for the one salt), which is what
    /// lets any process answer "is there a road here" for anywhere in an
    /// infinite world without generating it.
    ///
    /// A THREE-TIER HIERARCHY, laid out the way the procedural-city research
    /// lays it out (Parish and Muller 2001; Chen et al. 2008; the Wave
    /// Function Collapse infinite-city work): a sparse skeleton of long
    /// through-roads first, then progressively finer streets filling what
    /// they enclose, the fineness following where the city is dense.
    ///
    ///   1. HIGHWAYS -- every fourth arterial line on one axis, elevated.
    ///   2. ARTERIALS -- long straight lines that bound the districts. Their
    ///      spacing is IRREGULAR (a jittered lattice, 14 to 34 chunks apart)
    ///      rather than every 24th chunk forever, so districts are a
    ///      patchwork of different rectangles, not one repeated square.
    ///   3. LOCAL STREETS -- inside each district, spaced by the district's
    ///      own <see cref="DistrictPeriod"/>, which is chosen by what the
    ///      district IS (see <see cref="CityZones"/>): a tight old-town grid,
    ///      medium blocks downtown, wide superblocks at the edge. Streets are
    ///      spread evenly across the district so no block is left a sliver.
    ///
    /// ORIGINALLY a straight port of city_chunk.gd (x or y divisible by 4,
    /// everywhere, always), then one global grid with a per-district period.
    /// EXPLICIT ASK behind both steps: "there are a bunch of these
    /// interlocking staircases and streets that makes no sense whatsoever
    /// ... separate areas by neighborhood and having main streets connecting
    /// them. not everything should be a blocky grid." This intentionally
    /// diverges from city_chunk.gd; Godot never had neighbourhoods to port.
    ///
    /// EVERY QUESTION IS ANSWERED FROM THE CHUNK ALONE, in bounded work: an
    /// arterial line is a hash of its own index, a district is looked up by
    /// the two lines around a chunk, and a district's street positions are
    /// integer arithmetic on its own extent. Nothing is generated, ordered or
    /// communicated, which is what keeps chunks independent and lets a client
    /// and server agree on the city without sending geometry.
    /// </summary>
    public static class CityLayout
    {
        /// <summary>
        /// Salt for every macro-layout decision (arterial jitter, district
        /// periods, urban centres, highway exits). Changing it re-rolls the
        /// whole map's shape, which is what "generate a new world" means for
        /// the parts of the city that are deliberately independent of the
        /// world seed.
        /// </summary>
        public const ulong LayoutSeed = 0x35A97F1BUL;

        /// <summary>
        /// The original city_chunk.gd street period. Still one of the
        /// periods a district can roll, and what the plan tools call "a
        /// standard block".
        /// </summary>
        public const int StreetPeriod = 4;

        /// <summary>Chunk edge length in metres.</summary>
        public const float ChunkSize = 24.0f;

        /// <summary>
        /// The NOMINAL spacing of arterial lines, in chunks. The real spacing
        /// varies between <see cref="MinDistrictChunks"/> and
        /// <see cref="MaxDistrictChunks"/>; this is the mean, and the pitch of
        /// the lattice the jitter is applied to.
        /// </summary>
        public const int DistrictChunks = 24;

        /// <summary>How far an arterial line may sit from its lattice position, in chunks.</summary>
        public const int ArterialJitterChunks = 5;

        /// <summary>The narrowest a district can be, arterial line to arterial line.</summary>
        public const int MinDistrictChunks = DistrictChunks - 2 * ArterialJitterChunks;

        /// <summary>The widest a district can be, arterial line to arterial line.</summary>
        public const int MaxDistrictChunks = DistrictChunks + 2 * ArterialJitterChunks;

        /// <summary>
        /// The closest two streets can be, in chunks, line to line. Three
        /// leaves a block interior of two chunks -- the smallest that holds a
        /// 2x2 tower -- so no block is ever a one-chunk strip between two roads.
        /// </summary>
        public const int MinStreetGap = 3;

        /// <summary>
        /// The furthest two streets can be. Bounds a block interior at 9
        /// chunks (216m) so even the sparsest district is walkable rather than
        /// a plain of unbroken lots.
        /// </summary>
        public const int MaxStreetGap = 10;

        private const ulong LineSaltX = 0x4C494E58UL;   // "LINX"
        private const ulong LineSaltY = 0x4C494E59UL;   // "LINY"
        private const ulong DistrictPeriodSalt = 0x4E424844UL; // "NBHD"

        /// <summary>
        /// Every district's street period is picked from a short list that
        /// depends on its zone. Weighted by repetition. Any value from
        /// MinStreetGap up works -- unlike the fixed-grid days, nothing has to
        /// divide the district width, because streets are spread evenly.
        /// </summary>
        private static readonly int[] CorePeriods = { 4, 4, 5 };
        private static readonly int[] UrbanPeriods = { 4, 5, 5, 6 };
        private static readonly int[] ResidentialPeriods = { 5, 6, 6 };
        private static readonly int[] OldTownPeriods = { 3, 3, 4 };
        private static readonly int[] IndustrialPeriods = { 6, 8 };
        private static readonly int[] OutskirtsPeriods = { 6, 8, 8 };

        /// <summary>
        /// The widest street period any district can roll -- public so
        /// anything that needs a safe upper bound on "how far can the nearest
        /// street possibly be" can state it in terms of the real data.
        /// </summary>
        public const int MaxLocalStreetPeriod = 8;

        // --- arterial lines: a jittered lattice on each axis ---

        /// <summary>
        /// The chunk coordinate of arterial line <paramref name="index"/>:
        /// its lattice position (index x DistrictChunks) nudged by a hashed
        /// jitter. Line 0 is never nudged, so the origin -- spawn, and the
        /// highway that runs through it -- stays exactly where it always was.
        /// Consecutive lines can never cross: the lattice pitch (24) is more
        /// than twice the jitter (5), so they stay at least 14 apart.
        /// </summary>
        private static int LineChunk(int index, ulong axisSalt)
        {
            if (index == 0)
            {
                return 0;
            }
            uint roll = WorldHash.Coord(index, 0, axisSalt, LayoutSeed);
            int jitter = (int)(roll % (uint)(2 * ArterialJitterChunks + 1)) - ArterialJitterChunks;
            return index * DistrictChunks + jitter;
        }

        /// <summary>The index of the last arterial line at or before this coordinate.</summary>
        private static int LineIndexAtOrBefore(int coord, ulong axisSalt)
        {
            int index = FloorDiv(coord, DistrictChunks);
            if (LineChunk(index + 1, axisSalt) <= coord)
            {
                return index + 1;
            }
            if (LineChunk(index, axisSalt) <= coord)
            {
                return index;
            }
            return index - 1;
        }

        /// <summary>
        /// True when this coordinate IS an arterial line, and which one.
        /// Lines live inside disjoint windows of width 2*jitter+1 around each
        /// lattice position, so the only candidate is the nearest lattice line.
        /// </summary>
        private static bool TryGetLineIndex(int coord, ulong axisSalt, out int index)
        {
            index = FloorDiv(coord + DistrictChunks / 2, DistrictChunks);
            return LineChunk(index, axisSalt) == coord;
        }

        // --- a district's own street grid ---

        private struct DistrictInfo
        {
            public int LoX, HiX, LoY, HiY;
            public int Period;
            public int CountX, CountY;   // blocks across each axis; streets inside = count - 1
        }

        private const int CacheSlots = 512;   // power of two

        [System.ThreadStatic] private static DistrictInfo[] _districtCache;
        [System.ThreadStatic] private static long[] _districtCacheKeys;

        private static DistrictInfo InfoFor(int indexX, int indexY)
        {
            if (_districtCache == null)
            {
                _districtCache = new DistrictInfo[CacheSlots];
                _districtCacheKeys = new long[CacheSlots];
                for (int i = 0; i < CacheSlots; i++)
                {
                    _districtCacheKeys[i] = long.MinValue;
                }
            }

            long key = ((long)indexX << 32) ^ (uint)indexY;
            int slot = unchecked((indexX * 73856093) ^ (indexY * 19349663)) & (CacheSlots - 1);
            if (_districtCacheKeys[slot] == key)
            {
                return _districtCache[slot];
            }

            DistrictInfo info = ComputeInfo(indexX, indexY);
            _districtCache[slot] = info;
            _districtCacheKeys[slot] = key;
            return info;
        }

        private static DistrictInfo ComputeInfo(int indexX, int indexY)
        {
            var info = new DistrictInfo
            {
                LoX = LineChunk(indexX, LineSaltX),
                HiX = LineChunk(indexX + 1, LineSaltX),
                LoY = LineChunk(indexY, LineSaltY),
                HiY = LineChunk(indexY + 1, LineSaltY),
            };

            // The period follows what the district IS, judged at its centre.
            ZoneKind zone = CityZones.ZoneAt((info.LoX + info.HiX) * 0.5, (info.LoY + info.HiY) * 0.5);
            int[] periods = PeriodsFor(zone);
            double roll = WorldHash.Unit01(indexX, indexY, DistrictPeriodSalt, LayoutSeed);
            int pick = (int)(roll * periods.Length);
            info.Period = periods[pick >= periods.Length ? periods.Length - 1 : pick];
            info.CountX = BlockCount(info.HiX - info.LoX, info.Period);
            info.CountY = BlockCount(info.HiY - info.LoY, info.Period);
            return info;
        }

        private static int[] PeriodsFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return CorePeriods;
                case ZoneKind.Urban: return UrbanPeriods;
                case ZoneKind.OldTown: return OldTownPeriods;
                case ZoneKind.Industrial: return IndustrialPeriods;
                case ZoneKind.Outskirts: return OutskirtsPeriods;
                default: return ResidentialPeriods;
            }
        }

        /// <summary>
        /// How many blocks fit across a district of this width at roughly this
        /// street period -- clamped so the gap between streets always lands in
        /// [MinStreetGap, MaxStreetGap] whatever width the jittered arterials
        /// left the district.
        /// </summary>
        private static int BlockCount(int width, int period)
        {
            int wanted = (int)System.Math.Round(width / (double)period);
            int fewest = (width + MaxStreetGap - 1) / MaxStreetGap;
            int most = width / MinStreetGap;
            if (wanted < fewest) wanted = fewest;
            if (wanted > most) wanted = most;
            return wanted < 1 ? 1 : wanted;
        }

        /// <summary>Position of local street k (1..count-1) in a district spanning [lo, lo+width].</summary>
        private static int LocalLinePosition(int lo, int width, int count, int k)
        {
            return lo + (int)((long)k * width / count);
        }

        private static bool IsLocalLine(int coord, int lo, int hi, int count)
        {
            if (count <= 1 || coord <= lo || coord >= hi)
            {
                return false;
            }
            int width = hi - lo;
            long offset = coord - lo;
            int k = (int)((offset * count + width - 1) / width);   // smallest k whose line is at or past coord
            return k >= 1 && k < count && LocalLinePosition(lo, width, count, k) == coord;
        }

        /// <summary>
        /// The block interior on one axis that contains a NON-street chunk:
        /// [first, first + length), between the two streets around it.
        /// </summary>
        private static void BlockSpanOn(int coord, int lo, int hi, int count, out int first, out int length)
        {
            int width = hi - lo;
            int k = count <= 1 ? 0 : (int)((long)(coord - lo) * count / width);
            int before = LocalLinePosition(lo, width, count, k);
            int after = LocalLinePosition(lo, width, count, k + 1);
            first = before + 1;
            length = after - before - 1;
        }

        /// <summary>
        /// The interior of the block a buildable chunk sits in: its low
        /// corner and its size, in chunks. False for a street chunk. A block
        /// is anywhere from 2x2 up to 9x9 chunks now that districts and their
        /// street periods vary.
        /// </summary>
        public static bool TryGetBlockRect(int chunkX, int chunkY,
            out int minX, out int minY, out int width, out int height)
        {
            minX = minY = width = height = 0;
            int indexX = LineIndexAtOrBefore(chunkX, LineSaltX);
            int indexY = LineIndexAtOrBefore(chunkY, LineSaltY);
            DistrictInfo d = InfoFor(indexX, indexY);
            if (chunkX == d.LoX || chunkY == d.LoY ||
                IsLocalLine(chunkX, d.LoX, d.HiX, d.CountX) || IsLocalLine(chunkY, d.LoY, d.HiY, d.CountY))
            {
                return false;
            }
            BlockSpanOn(chunkX, d.LoX, d.HiX, d.CountX, out minX, out width);
            BlockSpanOn(chunkY, d.LoY, d.HiY, d.CountY, out minY, out height);
            return true;
        }

        /// <summary>
        /// The street period of the district containing this chunk -- the
        /// nominal block pitch the district was laid out at.
        /// </summary>
        public static int DistrictPeriod(int chunkX, int chunkY)
        {
            return InfoFor(LineIndexAtOrBefore(chunkX, LineSaltX), LineIndexAtOrBefore(chunkY, LineSaltY)).Period;
        }

        /// <summary>
        /// The arterial-line-to-arterial-line extent of the district holding
        /// this chunk, on each axis: [loX, hiX] and [loY, hiY], both ends
        /// being arterial lines themselves.
        /// </summary>
        public static void GetDistrictExtent(int chunkX, int chunkY,
            out int loX, out int hiX, out int loY, out int hiY)
        {
            DistrictInfo d = InfoFor(LineIndexAtOrBefore(chunkX, LineSaltX), LineIndexAtOrBefore(chunkY, LineSaltY));
            loX = d.LoX;
            hiX = d.HiX;
            loY = d.LoY;
            hiY = d.HiY;
        }

        /// <summary>
        /// True for a chunk kept clear as a through-street -- either an
        /// arterial line, or a local street inside whichever district this
        /// chunk falls in.
        /// </summary>
        public static bool IsStreetChunk(int x, int y)
        {
            int indexX = LineIndexAtOrBefore(x, LineSaltX);
            int indexY = LineIndexAtOrBefore(y, LineSaltY);
            DistrictInfo d = InfoFor(indexX, indexY);
            if (x == d.LoX || y == d.LoY)
            {
                return true;
            }
            return IsLocalLine(x, d.LoX, d.HiX, d.CountX) || IsLocalLine(y, d.LoY, d.HiY, d.CountY);
        }

        /// <summary>
        /// The chunk coordinate of the arterial line nearest to this
        /// coordinate: an X coordinate on the north-south arterials
        /// (<paramref name="xAxis"/> true), a Y coordinate on the east-west ones.
        /// </summary>
        public static int NearestArterialLine(int coord, bool xAxis)
        {
            ulong salt = xAxis ? LineSaltX : LineSaltY;
            int index = LineIndexAtOrBefore(coord, salt);
            int before = LineChunk(index, salt);
            int after = LineChunk(index + 1, salt);
            return coord - before <= after - coord ? before : after;
        }

        /// <summary>
        /// True on a chunk of an arterial line -- the long straight road
        /// separating one district from the next, the "main street
        /// connecting neighbourhoods" this was explicitly asked for. Checked
        /// BEFORE anything district-specific, on either axis, so it is never
        /// overridden by a neighbourhood's own local grid and never depends on
        /// which of the (up to four) districts meeting at a corner gets asked:
        /// an arterial line is the same line from every side of it.
        /// </summary>
        public static bool IsArterialChunk(int x, int y)
        {
            return TryGetLineIndex(x, LineSaltX, out _) || TryGetLineIndex(y, LineSaltY, out _);
        }

        /// <summary>
        /// Every HighwaySpacing-th arterial line on the X axis is a highway --
        /// EXPLICIT ASK: "generate a highway system with exits... look at
        /// american cities with lots of highways." A real American city's
        /// freeway grid is sparser than its own arterial grid (I-90 crosses
        /// many ordinary main avenues before the next freeway), so a highway
        /// is never a line of its own: it is chosen BY ARTERIAL LINE INDEX,
        /// which guarantees IsHighwayChunk implies IsArterialChunk implies
        /// IsStreetChunk -- a highway line is ALWAYS a line the ordinary
        /// network already calls a real, connected street, so nothing about
        /// lot placement, pedestrian nav, or per-chunk independence changes
        /// for it. Only how THAT one line is built (permanently elevated,
        /// sweeping, with real exits -- see BuildGround's own highway deck)
        /// differs.
        /// </summary>
        private const int HighwaySpacing = 4;

        /// <summary>
        /// True on a highway -- a sparse subset of IsArterialChunk's own
        /// lines, permanently elevated rather than only bridging a real
        /// terrain dip (see CityMeshBuilder.TryGetHighwayDeckHeight).
        ///
        /// ONE AXIS ONLY -- EXPLICIT BUG REPORT: "im still seeing gaps in
        /// highways also make it so highways cant intersect only streets
        /// do." The original version fired on EITHER
        /// "is a highway line" OR the same test on `y`,
        /// which is two independent, unbounded grid lines -- and any
        /// unbounded vertical line and unbounded horizontal line on an
        /// infinite grid intersect exactly once no matter how the two
        /// conditions are phased against each other (there is no modulus
        /// offset that avoids it). `AddHighwayDeck` only ever built ONE
        /// axis's deck at such a chunk, leaving a real, reachable one-chunk
        /// gap in the other -- a known v1 limitation that turned out not
        /// to be rare enough to go unnoticed. Restricting to the X
        /// condition alone makes every highway line run the same
        /// direction, so two highway lines can never cross by
        /// construction; an ordinary street or arterial can still cross
        /// UNDER a highway exactly as before (that crossing was never the
        /// problem -- the elevated deck already handles it, via
        /// AddHighwayDeck's own pillar-and-frontage-road treatment).
        /// </summary>
        public static bool IsHighwayChunk(int x, int y)
        {
            return TryGetLineIndex(x, LineSaltX, out int index) && PositiveMod(index, HighwaySpacing) == 0;
        }

        /// <summary>
        /// A real, deliberate distance apart for a highway's own exits --
        /// EXPLICIT ASK: "utilize the bridges... as well as the curved and
        /// sloped roads." Real American freeways space interchanges roughly
        /// every mile or two in a dense urban grid; an exit candidate is
        /// every HighwayExitLines-th Y arterial line (~1.7km on average) --
        /// frequent enough that a player driving a real highway run actually
        /// finds one, rare enough that most of the run reads as a real
        /// elevated freeway rather than a ladder of on/off ramps. Candidates
        /// sit ON Y arterial lines, so every exit is a real cross-arterial
        /// (see HighwayClearanceTaperAt for why that matters).
        /// </summary>
        private const int HighwayExitLines = 3;

        /// <summary>
        /// How close to a real exit point a highway's own deck starts
        /// easing its clearance down to street level -- long enough that a
        /// real car has room to slow down over it, matching
        /// StreetCurveTaperDistanceMetres's own "one chunk's length is
        /// enough room" reasoning for the (unrelated) ordinary-street
        /// curve.
        /// </summary>
        public const double HighwayExitTaperDistanceMetres = ChunkSize * 3.0;

        private const ulong HighwayExitSalt = 0x48575859UL; // "HWXY"

        /// <summary>Chance a genuine highway-exit candidate chunk actually gets an exit.</summary>
        private const double HighwayExitChance = 0.6;

        /// <summary>
        /// True on the one chunk, every HighwayExitLines-th Y arterial line
        /// along this specific highway line, that is a real exit -- deterministic per
        /// candidate chunk (not a sliding search) so every chunk along the
        /// run that asks "is this along-position an exit" agrees, the same
        /// reasoning TryFindBlockBridgeCentre's own doc comment already
        /// gives for why a bridge centre has to be keyed on a fixed block
        /// rather than a sliding window. `alongChunk` is this highway
        /// line's own travel-axis coordinate, `laneKey` its fixed cross-axis
        /// one -- NOT raw (chunkX, chunkY) in either order, which would make
        /// the gate below ambiguous about which axis it is actually testing.
        /// </summary>
        public static bool HighwayHasExitAt(int alongChunk, int laneKey)
        {
            if (!TryGetLineIndex(alongChunk, LineSaltY, out int lineIndex) ||
                PositiveMod(lineIndex, HighwayExitLines) != 0)
            {
                return false;
            }
            double roll = WorldHash.Unit01(alongChunk, laneKey, HighwayExitSalt, LayoutSeed);
            return roll < HighwayExitChance;
        }

        /// <summary>
        /// Real-world distance from worldPosAlong to the nearest highway
        /// exit point along this highway line, searching outward chunk by
        /// chunk -- the exact same "search nearby, do not assume a fixed
        /// grid position" idiom DistanceToNearestJunction already
        /// establishes for the (unrelated, now-dormant) ordinary-street
        /// curve taper, reused here for the same reason: an exit's own
        /// exact chunk is a roll (HighwayHasExitAt), not a fixed offset,
        /// so a fixed-formula distance would disagree with where an exit
        /// actually rolled true.
        /// </summary>
        private static double DistanceToNearestHighwayExit(int laneKey, double worldPosAlong)
        {
            int chunkAlong = (int)System.Math.Floor(worldPosAlong / ChunkSize);
            int startLine = FloorDiv(chunkAlong, DistrictChunks);
            double nearest = double.MaxValue;
            const int searchLines = HighwayExitLines * 2 + 2;

            for (int direction = -1; direction <= 1; direction += 2)
            {
                for (int steps = 0; steps <= searchLines; steps++)
                {
                    int line = startLine + steps * direction;
                    if (PositiveMod(line, HighwayExitLines) != 0)
                    {
                        continue;
                    }
                    int probe = LineChunk(line, LineSaltY);
                    if (HighwayHasExitAt(probe, laneKey))
                    {
                        double centre = SpanCentre(probe, 1);
                        nearest = System.Math.Min(nearest, System.Math.Abs(worldPosAlong - centre));
                        break;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// `minimumClearanceFraction` right at a real exit chunk's own
        /// centre, smoothly ramping up to 1 (full highway clearance) by
        /// HighwayExitTaperDistanceMetres. NEVER BELOW THE FLOOR, ANYWHERE,
        /// NOT EVEN AT THE EXIT ITSELF -- deliberately, not an oversight.
        ///
        /// THIS USED TO ACTUALLY REACH 0 AT THE EXIT'S OWN CENTRE (a real
        /// ground-level touchdown, for a literal on/off merge), with a
        /// separate short "final touchdown" stretch confined tightly
        /// around it so ordinary cross-streets elsewhere stayed above the
        /// floor. EXPLICIT BUG REPORT, with a second screenshot, PROVED
        /// that design fundamentally cannot work: exit candidates are every
        /// `HighwayExitLines`-th Y arterial line (so a real highway run finds
        /// exits at meaningful intervals) -- which ALSO means, by
        /// construction, every exit row IS an arterial line, which makes
        /// `IsArterialChunk` (and so
        /// `IsStreetChunk`) true on BOTH sides of the highway at that exact
        /// row, EVERY time. Confirmed directly: a scratch diagnostic
        /// checked every real exit along a real highway line and found
        /// 100% of them sitting on a genuine cross-arterial, not a rare
        /// coincidence. A true ground-level touchdown at an exit was
        /// therefore ALWAYS a solid slab dropped directly onto a live
        /// cross-street's own driving lanes -- blocking it outright, not a
        /// case a tighter final-descent radius could ever dodge.
        ///
        /// This system has no real interchange geometry to route between
        /// two roads meeting at grade (a genuinely bigger, separate task --
        /// see this file's own closing note), so a literal at-grade merge
        /// was never achievable honestly. The floor now applies
        /// unconditionally: an "exit" reads as the highway dipping to a
        /// real, comfortable minimum height near an interchange rather
        /// than an actual on/off ramp -- a deliberate scope cut, not a
        /// half-fix. `minimumClearanceFraction` is passed in rather than
        /// known here, since it depends on CityMeshBuilder's own
        /// HighwayClearance value, which this pure World-layer method has
        /// no reference to.
        /// </summary>
        public static float HighwayClearanceTaperAt(int laneKey, double worldPosAlong,
            float minimumClearanceFraction)
        {
            double distance = DistanceToNearestHighwayExit(laneKey, worldPosAlong);
            float t = System.Math.Min(1.0f, (float)(distance / HighwayExitTaperDistanceMetres));
            t = System.Math.Max(0.0f, t);
            float eased = t * t * (3.0f - 2.0f * t);
            return minimumClearanceFraction + (1.0f - minimumClearanceFraction) * eased;
        }

        private const ulong PedestrianZoneSalt = 0x50455A4EUL; // "PEZN"

        /// <summary>Chance a genuinely qualifying local-street run chunk becomes a car-free pedestrian zone.</summary>
        private const double PedestrianZoneChance = 0.025;

        /// <summary>
        /// True on a real, car-free pedestrian zone -- EXPLICIT ASK: "make it
        /// city like with pedestrian areas, highways, neighborhoods etc." A
        /// whole street SEGMENT, not a junction (surrounding traffic still
        /// has to be able to pass through the junctions on either end even
        /// though the block between them is closed to cars, exactly how a
        /// real pedestrianised high-street block works) and never on an
        /// arterial or a highway (a district's own main connecting avenue,
        /// or a real freeway, closing to cars would defeat the entire point
        /// of either -- same reasoning IsClosedJunctionArm's own doc comment
        /// already gives for why THAT feature also excludes arterials).
        /// Deliberately rare (2.5%): a real city has a handful of
        /// pedestrianised blocks, not a a car-free grid.
        /// </summary>
        public static bool IsPedestrianZoneChunk(int x, int y)
        {
            if (IsArterialChunk(x, y) || !IsStreetChunk(x, y))
            {
                return false;
            }

            bool junction = IsStreetChunk(x - 1, y) && IsStreetChunk(x + 1, y) &&
                            IsStreetChunk(x, y - 1) && IsStreetChunk(x, y + 1);
            if (junction)
            {
                return false;
            }

            double roll = WorldHash.Unit01(x, y, PedestrianZoneSalt, LayoutSeed);
            return roll < PedestrianZoneChance;
        }

        private const ulong BookSalt = 0x424F4F4BUL; // "BOOK"

        /// <summary>Chance a pedestrian zone chunk also gets a readable book -- see IsBookChunk.</summary>
        private const double BookChance = 0.15;

        /// <summary>
        /// True where a Book (CityX.View.Book) gets spawned -- EXPLICIT
        /// ASK: "add books and when you press E on them it opens a page on
        /// screen with text ive prepared but just make the system for
        /// now." Piggy-backs on IsPedestrianZoneChunk the same way
        /// AddBenches already does (a book on a car-free plaza reads the
        /// same as a bench there does), with its own independent roll
        /// (different salt) so books and benches don't always land on the
        /// exact same set of chunks.
        /// </summary>
        public static bool IsBookChunk(int x, int y)
        {
            if (!IsPedestrianZoneChunk(x, y))
            {
                return false;
            }
            double roll = WorldHash.Unit01(x, y, BookSalt, LayoutSeed);
            return roll < BookChance;
        }

        private const ulong JunctionClosesSalt = 0x41524D43UL; // "ARMC"
        private const ulong JunctionArmSalt = 0x41524D53UL; // "ARMS"

        /// <summary>
        /// Chance that a real local-street 4-way junction has exactly one of
        /// its four arms closed off, turning it into a T-junction instead.
        /// </summary>
        private const double JunctionClosureChance = 0.35;

        /// <summary>
        /// True when the arm of junction (chunkX, chunkY) pointing toward
        /// (chunkX + dx, chunkY + dy) -- one of the four cardinal directions
        /// -- is closed rather than a normal through-connection.
        ///
        /// A REAL REASON FOR THE STREET NOT TO GO STRAIGHT THROUGH --
        /// EXPLICIT ASK, replacing CityMeshBuilder's own 2026-09-11/12
        /// cosmetic sine-wave wander after direct feedback: "the roads
        /// shouldnt be just wiggly for no reason... a turn or dead end or 4
        /// way intersection... they shouldnt just be uneven just for the
        /// sake of them being uneven." A real Manhattan grid has T-junctions
        /// and closed streets, not a 4-way crossing at literally every
        /// intersection -- this is what gives a turn an actual reason to
        /// exist instead of a decorative wobble.
        ///
        /// ARTERIALS NEVER CLOSE. IsArterialChunk's own doc comment is "the
        /// main street connecting neighbourhoods" -- closing an arm of it
        /// would defeat that purpose network-wide, not just at one corner.
        /// Checked on the JUNCTION's own coordinate only: a junction that
        /// sits on an arterial line has the arterial as one of its two
        /// crossing streets no matter which neighbour is being asked about.
        ///
        /// ONLY THE JUNCTION SIDE OF THIS EVER ACTS ON IT (see
        /// CityMeshBuilder.AddRoadMarkings/BuildGround) -- a closed arm is
        /// realised as a physical barrier built entirely within the
        /// junction chunk's own geometry, not by changing IsStreetChunk's
        /// grid or anything about the neighbour chunk one step further out
        /// (which keeps building itself as an entirely ordinary street
        /// chunk). That neighbour is still reachable, just not FROM this
        /// junction in this direction -- from the next junction over, the
        /// same way a real street permanently closed to through-traffic at
        /// one specific corner still has the rest of its own length
        /// reachable from the other end. This is deliberately NOT a change
        /// to the world's own building-lot grid: CityChunkGenerator's lot
        /// placement depends on IsStreetChunk's grid staying exactly as
        /// regular as it already is, and a barrier-only closure gets the
        /// "real turn/dead end" look this was asked for without touching
        /// that at all.
        /// </summary>
        public static bool IsClosedJunctionArm(int chunkX, int chunkY, int dx, int dy)
        {
            if (IsArterialChunk(chunkX, chunkY))
            {
                return false;
            }

            double closes = WorldHash.Unit01(chunkX, chunkY, JunctionClosesSalt, LayoutSeed);
            if (closes >= JunctionClosureChance)
            {
                return false;
            }

            // A second, independent roll off the same junction picks WHICH
            // single arm closes, so the "does it close at all" chance above
            // stays exactly JunctionClosureChance regardless of which of the
            // four arms ends up chosen.
            double armRoll = WorldHash.Unit01(chunkX, chunkY, JunctionArmSalt, LayoutSeed);
            int armIndex = (int)(armRoll * 4.0);
            if (armIndex > 3)
            {
                armIndex = 3;
            }
            int chosenDx = armIndex == 0 ? 1 : (armIndex == 1 ? -1 : 0);
            int chosenDy = armIndex == 2 ? 1 : (armIndex == 3 ? -1 : 0);
            return dx == chosenDx && dy == chosenDy;
        }

        /// <summary>
        /// The real-world treatments a closed junction arm (see
        /// IsClosedJunctionArm) can get, instead of always being the exact
        /// same plain barrier -- EXPLICIT ASK: "instead of ... colliding
        /// with each other you should have some that have some sort of
        /// pedestrian walkable plaza, some that are just dead ends and are
        /// rounded off ... 2% chance of there being a hidden skatepark ...
        /// a wide variety of solutions and make it so that they all can
        /// occur."
        /// </summary>
        public enum ClosedArmTreatment
        {
            /// <summary>The original: a plain guard rail straight across the arm's mouth.</summary>
            Barrier,

            /// <summary>A raised, walkable paved plaza filling the arm's mouth -- pedestrians only, no through-traffic.</summary>
            Plaza,

            /// <summary>A curved rail bulging into the arm's own mouth, reading as a real cul-de-sac a car could turn around in.</summary>
            RoundedCurb,

            /// <summary>Rare: a small hidden skatepark -- a paved pad with a ramp and a grind rail.</summary>
            Skatepark,
        }

        private const ulong JunctionTreatmentSalt = 0x41524D54UL; // "ARMT"

        /// <summary>Chance, among all closed arms, that the rare skatepark treatment is picked.</summary>
        private const double SkateparkChance = 0.02;

        /// <summary>
        /// Which treatment the ONE closed arm of this junction gets. Only
        /// meaningful when IsClosedJunctionArm is already true for the arm
        /// being asked about -- since exactly one arm ever closes per
        /// junction (see that method's own "second, independent roll"
        /// comment), this needs no per-arm disambiguation, just one more
        /// roll off the same junction coordinate. The remaining, far more
        /// common 98% splits evenly across the other three treatments so
        /// "a wide variety ... they all can occur" holds in practice, not
        /// just in principle.
        /// </summary>
        public static ClosedArmTreatment TreatmentForClosedArm(int chunkX, int chunkY)
        {
            double roll = WorldHash.Unit01(chunkX, chunkY, JunctionTreatmentSalt, LayoutSeed);
            if (roll < SkateparkChance)
            {
                return ClosedArmTreatment.Skatepark;
            }

            double remainder = (roll - SkateparkChance) / (1.0 - SkateparkChance);
            if (remainder < 1.0 / 3.0)
            {
                return ClosedArmTreatment.Barrier;
            }
            return remainder < 2.0 / 3.0 ? ClosedArmTreatment.Plaza : ClosedArmTreatment.RoundedCurb;
        }

        /// <summary>
        /// True when chunkY is what makes (chunkX, chunkY) a street -- i.e.
        /// the street itself runs along X, extending east-west as chunkX
        /// varies -- rather than chunkX being what made it one. Only a
        /// meaningful either/or for a real, non-junction street chunk (a
        /// junction is both at once, and callers already special-case that
        /// before asking this).
        /// </summary>
        public static bool StreetRunsAlongX(int chunkX, int chunkY)
        {
            int indexY = LineIndexAtOrBefore(chunkY, LineSaltY);
            if (LineChunk(indexY, LineSaltY) == chunkY)
            {
                return true;
            }

            DistrictInfo d = InfoFor(LineIndexAtOrBefore(chunkX, LineSaltX), indexY);
            return IsLocalLine(chunkY, d.LoY, d.HiY, d.CountY);
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
        /// Floor division -- rounds toward negative infinity rather than
        /// truncating toward zero, so a run of chunks tiles negative space
        /// exactly the same way it tiles positive space.
        /// </summary>
        private static int FloorDiv(int value, int modulus)
        {
            return (value - PositiveMod(value, modulus)) / modulus;
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

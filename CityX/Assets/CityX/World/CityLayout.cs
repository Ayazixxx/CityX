namespace CityX.World
{
    /// <summary>
    /// The city's street grid.
    ///
    /// Pure integer maths over a chunk coordinate, with no state and no
    /// randomness (not even a world seed -- see LocalStreetPeriodFor's own
    /// doc comment for why), which is what lets any process answer "is
    /// there a road here" for anywhere in an infinite world without
    /// generating it.
    ///
    /// NO LONGER ONE UNIFORM GRID FOREVER. Originally a straight port of
    /// city_chunk.gd's own is_street_chunk (x or y divisible by 4,
    /// everywhere, always) -- EXPLICIT ASK: "there are a bunch of these
    /// interlocking staircases and streets that makes no sense whatsoever
    /// ... separate areas by neighborhood and having main streets
    /// connecting them. not everything should be a blocky grid." A single
    /// endless period reads as a blocky grid because it IS one, in exactly
    /// the way no real city is. This intentionally diverges from
    /// city_chunk.gd's own layout for that reason -- Godot never had
    /// neighbourhoods to port, so nothing here is a botched port, it is new
    /// ground the Unity side is asked to cover on its own. See
    /// CityLayoutTests's own golden-comparison test for what that means for
    /// parity going forward.
    /// </summary>
    public static class CityLayout
    {
        /// <summary>
        /// Every Nth row and column is a through-street rather than a
        /// buildable lot -- the ORIGINAL, single global value city_chunk.gd
        /// used everywhere. Kept as a real constant (rather than deleted)
        /// because it is still meaningful in two ways: it is one of the
        /// LocalStreetPeriods a district can roll, and DistrictChunks is
        /// still built as a whole multiple of it, so an arterial boundary
        /// always falls on a line the original grid would also have called
        /// a street.
        /// </summary>
        public const int StreetPeriod = 4;

        /// <summary>Chunk edge length in metres.</summary>
        public const float ChunkSize = 24.0f;

        /// <summary>
        /// Chunks per side of one district -- the unit neighbourhoods are
        /// carved from. A whole multiple of StreetPeriod on purpose (see
        /// IsArterialChunk's own doc comment for why that matters).
        /// </summary>
        public const int DistrictChunks = StreetPeriod * 6;

        /// <summary>
        /// The local grades a district's own interior grid can come in.
        ///
        /// EVERY VALUE HERE MUST EVENLY DIVIDE DistrictChunks. If it does
        /// not, the last block along an axis inside that district is a
        /// "remainder" narrower than every other block's own (period - 1)
        /// interior -- confirmed directly, live: with 5 in this list (24
        /// does not divide by 5), CityBlocks' own anchor clamp kept assuming
        /// the FULL width was available on that short last block and let a
        /// 2x2 superlot overhang one chunk past the district's own edge,
        /// directly into the next arterial. Every value below divides 24
        /// exactly, so that failure mode cannot occur -- there is no
        /// remainder block to get wrong in the first place.
        ///
        /// Never below 3 -- CityBlocks' existing tower/slab massing needs a
        /// buildable interior of at least 2 chunks square for a 2x2 superlot
        /// to fit at all. Weighted toward 4 (twice) since that is the
        /// original city_chunk.gd density and stays the most common texture
        /// of the world, with a tighter (3) and two sparser (6, 8)
        /// neighbourhood grades mixed in around it.
        /// </summary>
        private static readonly int[] LocalStreetPeriods = { 3, 4, 4, 6, 8 };

        /// <summary>
        /// The widest gap LocalStreetPeriodFor can ever produce -- public so
        /// anything that needs a safe upper bound on "how far can the
        /// nearest street possibly be" (pedestrian spawn radius checks, in
        /// particular) can state it in terms of the real data instead of a
        /// separately-maintained guess.
        /// </summary>
        public static readonly int MaxLocalStreetPeriod = Max(LocalStreetPeriods);

        private static int Max(int[] values)
        {
            int result = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > result)
                {
                    result = values[i];
                }
            }
            return result;
        }

        private const ulong DistrictPeriodSalt = 0x4E424844UL; // "NBHD"

        /// <summary>
        /// True for a chunk kept clear as a through-street -- either the
        /// wide arterial between two districts, or a local street inside
        /// whichever single district this chunk falls in. See this class's
        /// own doc comment for why that second part is no longer one
        /// endless global period.
        /// </summary>
        public static bool IsStreetChunk(int x, int y)
        {
            if (IsArterialChunk(x, y))
            {
                return true;
            }

            int districtX = DistrictIndex(x);
            int districtY = DistrictIndex(y);
            int period = LocalStreetPeriodFor(districtX, districtY);
            int localX = x - districtX * DistrictChunks;
            int localY = y - districtY * DistrictChunks;
            return PositiveMod(localX, period) == 0 || PositiveMod(localY, period) == 0;
        }

        /// <summary>
        /// True on the wide street separating one district from the next --
        /// the "main street connecting neighbourhoods" this was explicitly
        /// asked for. Checked BEFORE anything district-specific, on either
        /// axis, so it is never overridden by a neighbourhood's own local
        /// grid and never depends on which of the (up to four) districts
        /// meeting at a corner gets asked -- an arterial line is the same
        /// line from every side of it, which is exactly what keeps two
        /// neighbourhoods whose own internal grids do not line up at all
        /// still genuinely connected.
        ///
        /// DistrictChunks is a whole multiple of StreetPeriod, so every
        /// arterial line is also a line the original city_chunk.gd grid
        /// would already have called a street -- the divergence from Godot
        /// is confined to each district's own interior, never here.
        /// </summary>
        public static bool IsArterialChunk(int x, int y)
        {
            return PositiveMod(x, DistrictChunks) == 0 || PositiveMod(y, DistrictChunks) == 0;
        }

        /// <summary>
        /// Highway chunks per side of the wider unit a highway grid line
        /// repeats on -- EXPLICIT ASK: "generate a highway system with
        /// exits... look at american cities with lots of highways." A real
        /// American city's freeway grid is sparser than its own arterial
        /// grid (I-90 crosses many ordinary main avenues before the next
        /// freeway), so a highway is every HighwaySpacing-th arterial line,
        /// never a line of its own: `HighwayChunks` (a whole multiple of
        /// DistrictChunks, same reasoning IsArterialChunk's own doc comment
        /// already gives for DistrictChunks itself being a multiple of
        /// StreetPeriod) guarantees IsHighwayChunk implies IsArterialChunk
        /// implies IsStreetChunk -- a highway line is ALWAYS a line the
        /// ordinary grid already calls a real, connected street, so nothing
        /// about lot placement, pedestrian nav, or per-chunk independence
        /// changes for it. Only how THAT one line is built (permanently
        /// elevated, sweeping, with real exits -- see BuildGround's own
        /// highway deck) differs.
        /// </summary>
        private const int HighwaySpacing = 4;

        private const int HighwayChunks = DistrictChunks * HighwaySpacing;

        /// <summary>
        /// True on a highway -- a sparse subset of IsArterialChunk's own
        /// lines, permanently elevated rather than only bridging a real
        /// terrain dip (see CityMeshBuilder.TryGetHighwayDeckHeight).
        ///
        /// ONE AXIS ONLY -- EXPLICIT BUG REPORT: "im still seeing gaps in
        /// highways also make it so highways cant intersect only streets
        /// do." The original version fired on EITHER
        /// `PositiveMod(x, HighwayChunks) == 0` OR the same test on `y`,
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
            return PositiveMod(x, HighwayChunks) == 0;
        }

        /// <summary>
        /// A real, deliberate distance apart for a highway's own exits --
        /// EXPLICIT ASK: "utilize the bridges... as well as the curved and
        /// sloped roads." Real American freeways space interchanges roughly
        /// every mile or two in a dense urban grid; HighwayExitChunks (a
        /// whole multiple of DistrictChunks) puts one every 3 districts
        /// (~1.7km) -- frequent enough that a player driving a real highway
        /// run actually finds one, rare enough that most of the run reads
        /// as a real elevated freeway rather than a ladder of on/off ramps.
        /// </summary>
        private const int HighwayExitChunks = DistrictChunks * 3;

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
        /// True on the one chunk, every HighwayExitChunks along this
        /// specific highway line, that is a real exit -- deterministic per
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
            if (PositiveMod(alongChunk, HighwayExitChunks) != 0)
            {
                return false;
            }
            double roll = WorldHash.Unit01(alongChunk, laneKey, HighwayExitSalt, 0UL);
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
            double nearest = double.MaxValue;
            const int searchChunks = HighwayExitChunks * 2;

            for (int direction = -1; direction <= 1; direction += 2)
            {
                for (int steps = 0; steps <= searchChunks; steps++)
                {
                    int probe = chunkAlong + steps * direction;
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
        /// that design fundamentally cannot work: `HighwayExitChunks` is a
        /// whole multiple of `DistrictChunks` (so a real highway run finds
        /// exits at meaningful intervals) -- which ALSO means, by
        /// construction, `PositiveMod(exitRow, DistrictChunks) == 0` for
        /// literally EVERY exit, which makes `IsArterialChunk` (and so
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

            double roll = WorldHash.Unit01(x, y, PedestrianZoneSalt, 0UL);
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
            double roll = WorldHash.Unit01(x, y, BookSalt, 0UL);
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

            double closes = WorldHash.Unit01(chunkX, chunkY, JunctionClosesSalt, 0UL);
            if (closes >= JunctionClosureChance)
            {
                return false;
            }

            // A second, independent roll off the same junction picks WHICH
            // single arm closes, so the "does it close at all" chance above
            // stays exactly JunctionClosureChance regardless of which of the
            // four arms ends up chosen.
            double armRoll = WorldHash.Unit01(chunkX, chunkY, JunctionArmSalt, 0UL);
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
            double roll = WorldHash.Unit01(chunkX, chunkY, JunctionTreatmentSalt, 0UL);
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

        /// <summary>Which district a chunk coordinate falls in, on one axis.</summary>
        public static int DistrictIndex(int chunkCoord)
        {
            return FloorDiv(chunkCoord, DistrictChunks);
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
            if (PositiveMod(chunkY, DistrictChunks) == 0)
            {
                return true;
            }

            int districtX = DistrictIndex(chunkX);
            int districtY = DistrictIndex(chunkY);
            int period = LocalStreetPeriodFor(districtX, districtY);
            int localY = chunkY - districtY * DistrictChunks;
            return PositiveMod(localY, period) == 0;
        }

        /// <summary>
        /// The one property that makes a district read as its own
        /// neighbourhood: how far apart its local streets are.
        ///
        /// FIXED PER (districtX, districtY), NOT SALTED BY THE WORLD SEED --
        /// deliberately, same as IsStreetChunk's own grid always has been
        /// (city_chunk.gd's street grid never depended on the seed either;
        /// only what gets built inside it does). Keeping the neighbourhood
        /// MAP itself seed-independent means CityLayout never needs a
        /// CityWorld/seed threaded through it just to answer "is this a
        /// street" -- every one of its 17-odd callers keeps working exactly
        /// as it already does. Regenerating a new world seed still reshuffles
        /// every building, terrace and lamp; it just does so on the same
        /// underlying neighbourhood shape every time.
        /// </summary>
        public static int LocalStreetPeriodFor(int districtX, int districtY)
        {
            double roll = WorldHash.Unit01(districtX, districtY, DistrictPeriodSalt, 0UL);
            int index = (int)(roll * LocalStreetPeriods.Length);
            if (index >= LocalStreetPeriods.Length)
            {
                index = LocalStreetPeriods.Length - 1;
            }
            return LocalStreetPeriods[index];
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

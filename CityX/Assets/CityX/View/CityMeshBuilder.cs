using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Turns one chunk of the world model into triangles.
    ///
    /// WHY THIS IS A SEPARATE ASSEMBLY FROM CityX.World. A dedicated server
    /// runs the same world model -- it has to, or it could not tell a client
    /// what is where -- but it must never build a mesh. Keeping the view in its
    /// own assembly makes that a compile-time fact rather than a convention
    /// somebody remembers. CityX.World has no idea this exists.
    ///
    /// Static and stateless. A chunk's geometry is a pure function of its
    /// coordinate, exactly like everything it is built from, so it can be
    /// rebuilt at any time from nothing.
    /// </summary>
    public static class CityMeshBuilder
    {
        /// <summary>
        /// How far a terrace slab is extruded downward when its neighbour is
        /// lower. Sized from the world's own proven worst case rather than
        /// picked, so a retaining wall can never come up short and leave a
        /// slot of daylight along a terrace edge.
        ///
        /// ONE EXTRA TerraceStep OF MARGIN, beyond MaxNeighbourTerraceSteps
        /// alone. That bound covers how far a chunk's own quantised PAD can
        /// differ from a CARDINAL neighbour's, sampled centre to centre a
        /// full ChunkSize apart -- but a wall's actual top now starts from
        /// this chunk's own tilted CORNER (AddTiltedStreetGround's h00/h10/
        /// h11/h01, via BuildGround's TopAt), not the flat pad, on a street
        /// chunk. A corner sits closer to centre than a full ChunkSize
        /// (at most ChunkSize/sqrt(2)), so the extra continuous drift it can
        /// carry versus the pad is smaller than one more MaxNeighbourTerrace
        /// Steps unit -- plus up to one whole TerraceStep more from PadHeight
        /// For itself flooring the centre's continuous height to its own
        /// terrace level. One additional TerraceStep of slack safely covers
        /// both on top of the existing bound.
        /// </summary>
        public static float SkirtDepth =>
            (TerrainField.MaxNeighbourTerraceSteps + 1) * TerrainField.TerraceStep + 1.0f;

        /// <summary>
        /// How far a diagonal corner post reaches back along each edge from
        /// the corner. A short pocket rather than a full edge -- the corner
        /// post only has to close the notch a diagonal drop leaves; the
        /// ordinary cardinal skirt already covers the rest of that edge if
        /// its own neighbour warrants one.
        /// </summary>
        public const float CornerSkirtReach = 3.0f;

        /// <summary>Walkway deck width and thickness, in metres.</summary>
        public const float BridgeWidth = 4.0f;

        public const float BridgeThickness = 0.6f;

        /// <summary>
        /// Depth of the raised pavement along a street's building edges.
        ///
        /// Built as real geometry rather than painted on, because the point is
        /// that there is somewhere to stand that is not the road. Combined with
        /// the 3m lot setback this gives 7m of pedestrian space each side and
        /// leaves 10m of carriageway in a 24m street chunk.
        /// </summary>
        public const float SidewalkWidth = 4.0f;

        /// <summary>Kerb height. Enough to read as a step, low enough to walk up.</summary>
        public const float KerbHeight = 0.18f;

        // --- street curve, EXPLICIT ASK ("and curve"), 2nd attempt ---
        //
        // A first attempt (2026-09-05) shifted the carriageway sideways
        // within its own straight corridor and was fully reverted the same
        // session: the user's own in-game screenshot showed the double-
        // yellow centreline as a jagged zigzag, not a smooth bend. That
        // code was never committed (confirmed via git history search) so
        // nothing survives to copy -- rebuilt fresh, same general shape
        // (lateral wave within the existing straight grid corridor, NOT a
        // different street topology -- see this session's own plan for why
        // a genuinely different, non-grid topology was ruled out as too
        // large/risky a rewrite), but fixing the ACTUAL diagnosed failure:
        // too few straight segments to read as smooth up close, which only
        // a top-down diagnostic screenshot could not see.

        /// <summary>
        /// How many real metres of straight corridor the reverted attempt
        /// cut into one segment -- implied ~4-6 per 24m chunk by its own
        /// memory ("only 4 straight segments... not enough to read as
        /// curved rather than broken"). THE ACTUAL FIX: roughly a
        /// segment every 1.5m instead, which is what makes a gentle wave
        /// read as a real curve at eye level rather than a series of kinks
        /// -- a top-down screenshot looked fine at the old density too, which
        /// is exactly why that number was wrong and nobody caught it before
        /// shipping.
        /// </summary>
        public const float StreetCurveSegmentLength = 1.0f;

        /// <summary>
        /// How far the street's own centreline swings sideways at the peak
        /// of its wave. Was 1.0m; EXPLICIT FEEDBACK zeroed it out: "the
        /// roads shouldnt be just wiggly for no reason... they shouldnt
        /// just be uneven just for the sake of them being uneven." A sine
        /// wave with no relationship to the street network's own topology
        /// is exactly that -- unevenness with no reason behind it. Replaced
        /// by CityLayout.IsClosedJunctionArm's own real T-junctions/closed
        /// arms instead, which give a street an actual reason not to run
        /// straight. Left at 0 rather than reverting the whole curving-
        /// ribbon machinery (AddCurvingStreetGround/AddCurvingSidewalk/
        /// AddCurvingCentreLine all still segment and sample the real
        /// terrain height per segment -- genuinely useful on its own for a
        /// street that isn't perfectly flat corner to corner) -- every
        /// StreetCurveOffsetAt call is now unconditionally 0 regardless of
        /// world position, so every one of those ribbons collapses to
        /// exactly straight, just still real-terrain-tilted.
        /// </summary>
        public const float StreetCurveAmplitudeMetres = 0.0f;

        /// <summary>
        /// Real metres of straight corridor one full lateral wave spans.
        /// Longer than the reverted attempt's own implied 96m (4 chunks) --
        /// a slower, lazier meander reads as a real road follows the land
        /// rather than a slalom.
        /// </summary>
        public const double StreetCurveWavelengthMetres = 130.0;

        /// <summary>
        /// How close to a junction the curve starts easing back to exactly
        /// zero -- one chunk's own length is enough room for a smooth
        /// ease and guarantees the curve never reaches the junction itself,
        /// so cross streets keep meeting at clean right angles. A local
        /// neighbourhood whose own street spacing (CityLayout.LocalStreet
        /// PeriodFor) is this short or shorter ends up consuming its whole
        /// straight run in taper and reading effectively straight --
        /// accepted, not a bug: tight grids reading straighter and wide
        /// ones reading curvier matches how real cities actually look.
        /// </summary>
        public const double StreetCurveTaperDistanceMetres = CityLayout.ChunkSize * 2.0;

        /// <summary>
        /// How far outward IsJunctionChunk is searched for before giving up
        /// and treating a lane as having no nearby junction at all (full
        /// amplitude, no taper) -- generous enough that a real arterial
        /// (CityLayout.IsArterialChunk fires at least every DistrictChunks)
        /// is always found well inside this bound.
        /// </summary>
        private const int StreetCurveMaxJunctionSearchChunks = CityLayout.DistrictChunks * 2;

        /// <summary>Independent draw stream from a lane's own chunk-seed, so this never collides with anything else that seed is already used for.</summary>
        private const ulong StreetCurveStream = 0x53545243555256UL; // "STRCURV"

        /// <summary>
        /// The pure wave itself -- no lane lookup, no taper, just a sine of
        /// real world position offset by a per-lane phase. Kept separate
        /// from StreetCurveLateralOffsetAt so a test can drive the exact
        /// shape (continuity, amplitude, wavelength) without needing a
        /// CityWorld at all.
        /// </summary>
        private static float StreetCurveOffsetAt(double worldPosAlong, double phase)
        {
            double t = (worldPosAlong + phase) / StreetCurveWavelengthMetres * (2.0 * System.Math.PI);
            return StreetCurveAmplitudeMetres * (float)System.Math.Sin(t);
        }

        /// <summary>
        /// One phase per lane (a fixed cross-axis chunk coordinate), drawn
        /// once from that lane's own chunk-seed -- so parallel streets
        /// meander on their own independent schedule instead of every one
        /// swinging left at exactly the same world Z, which would read as
        /// obviously artificial. World-seed-dependent on purpose (unlike
        /// CityLayout's own grid shape, which deliberately is not) --
        /// regenerating a new world seed reshuffling exactly which streets
        /// curve which way is the same "everything downstream of the grid
        /// reshuffles, the grid itself does not" precedent CityLayout's own
        /// doc comment already establishes for buildings/terraces/lamps.
        /// </summary>
        private static double StreetCurvePhaseFor(CityWorld world, int laneKey)
        {
            var roll = new DeterministicRandom(unchecked((ulong)(long)world.ChunkSeed(laneKey, 0)), StreetCurveStream);
            return roll.NextDouble() * StreetCurveWavelengthMetres;
        }

        /// <summary>
        /// True on a chunk that is a real junction (street on all four
        /// cardinal sides) -- the same definition AddRoadMarkings already
        /// uses to decide crosswalks vs a centre line.
        /// </summary>
        private static bool IsJunctionChunk(int chunkX, int chunkY)
        {
            return CityLayout.IsStreetChunk(chunkX - 1, chunkY) && CityLayout.IsStreetChunk(chunkX + 1, chunkY) &&
                   CityLayout.IsStreetChunk(chunkX, chunkY - 1) && CityLayout.IsStreetChunk(chunkX, chunkY + 1);
        }

        /// <summary>
        /// Real-world distance from worldPosAlong to the nearest junction
        /// along this lane, searching outward chunk by chunk in both
        /// directions -- the same "search nearby, do not assume a fixed
        /// grid position" idiom this session's own elevated-bridge work
        /// already established for an analogous problem (finding a real
        /// valley centre without assuming it lands on a fixed bucket).
        /// </summary>
        private static double DistanceToNearestJunction(int laneKey, double worldPosAlong, bool runsAlongX)
        {
            int chunkAlong = Mathf.FloorToInt((float)(worldPosAlong / CityLayout.ChunkSize));
            double nearest = double.MaxValue;

            for (int direction = -1; direction <= 1; direction += 2)
            {
                for (int steps = 0; steps <= StreetCurveMaxJunctionSearchChunks; steps++)
                {
                    int probe = chunkAlong + steps * direction;
                    bool junction = runsAlongX ? IsJunctionChunk(probe, laneKey) : IsJunctionChunk(laneKey, probe);
                    if (junction)
                    {
                        double boundary = direction > 0
                            ? probe * (double)CityLayout.ChunkSize
                            : (probe + 1) * (double)CityLayout.ChunkSize;
                        nearest = System.Math.Min(nearest, System.Math.Abs(worldPosAlong - boundary));
                        break;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// 0 at/inside a junction, smoothly ramping to 1 over
        /// StreetCurveTaperDistanceMetres -- see that constant's own doc
        /// comment. No special-casing needed for "no junction found within
        /// the search bound" (DistanceToNearestJunction's own double.MaxValue
        /// fallback): dividing it by a real distance and casting to float
        /// overflows to +Infinity, and Mathf.Clamp01 already clamps that to
        /// exactly 1.0 -- full amplitude, correctly, with no branch.
        /// </summary>
        private static float StreetCurveTaperAt(int laneKey, double worldPosAlong, bool runsAlongX)
        {
            double distance = DistanceToNearestJunction(laneKey, worldPosAlong, runsAlongX);
            float t = Mathf.Clamp01((float)(distance / StreetCurveTaperDistanceMetres));
            return t * t * (3.0f - 2.0f * t);
        }

        /// <summary>
        /// The real, combined lateral offset a piece of curving street
        /// geometry actually builds from: this lane's own phase, the pure
        /// wave, and the junction taper, all composed. A NON-JUNCTION
        /// street chunk only -- callers already know that from CityLayout.
        /// </summary>
        private static double StreetCurveLateralOffsetAt(CityWorld world, int laneKey, double worldPosAlong,
            bool runsAlongX)
        {
            double phase = StreetCurvePhaseFor(world, laneKey);
            float taper = StreetCurveTaperAt(laneKey, worldPosAlong, runsAlongX);
            return StreetCurveOffsetAt(worldPosAlong, phase) * taper;
        }

        /// <summary>
        /// Tread depth for a STREET stair, at the same
        /// <see cref="BuildingInterior.StairStepHeight"/> riser every interior
        /// flight already uses. An interior tread is whatever a room's own run
        /// divided by its own step count happens to give; a street stair has no
        /// room to derive that from, so this is a real number -- shallow enough
        /// that a flight climbing a full terrace step (6.4m, roughly 26 risers)
        /// reads as a real civic stairway rather than a steep fire-escape flight.
        /// </summary>
        public const float StreetStairTreadDepth = 0.32f;

        /// <summary>
        /// Guardrail post/rail height. Short on purpose -- a waist-high stop
        /// at a ledge, not a safety cage. Matches the "short metal fence" this
        /// was asked for rather than a tall barrier.
        /// </summary>
        public const float GuardRailHeight = 0.9f;

        /// <summary>Post cross-section, square.</summary>
        public const float GuardRailPostSize = 0.06f;

        /// <summary>Centre-to-centre spacing between posts along one run.</summary>
        public const float GuardRailPostSpacing = 2.0f;

        /// <summary>Top-rail cross-section.</summary>
        public const float GuardRailRailSize = 0.05f;

        /// <summary>
        /// How far a Plaza or Skatepark closed-arm treatment extends inward
        /// from the arm's own mouth, into the junction chunk's own paved
        /// footprint -- see CityLayout.ClosedArmTreatment's own doc comment.
        /// Comfortably inside the 12m to the chunk's own centre (a junction
        /// is paved corner to corner with no sidewalks to clip), so this
        /// never reaches, let alone crosses, the neighbouring chunk beyond
        /// the closed arm.
        /// </summary>
        public const float ClosedArmPlazaDepth = 5.0f;

        /// <summary>How far a RoundedCurb treatment's own apex bulges inward, as a fraction of the arm's mouth width.</summary>
        public const float ClosedArmCurbBulgeFactor = 0.35f;

        /// <summary>Straight guard-rail segments approximating a RoundedCurb's own curve -- enough to read as smooth, not faceted, at this scale.</summary>
        public const int ClosedArmCurbSegments = 6;

        /// <summary>Skatepark ramp: how wide (along the arm's own mouth) the sloped face is.</summary>
        public const float SkateparkRampWidth = 3.0f;

        /// <summary>Skatepark ramp: how far inward the sloped face runs before reaching its own full height.</summary>
        public const float SkateparkRampRun = 2.5f;

        /// <summary>Skatepark ramp: peak height at the top of its own run.</summary>
        public const float SkateparkRampHeight = 1.4f;

        /// <summary>Skatepark grind ledge: a low, simple raised box rather than a rail on posts, so it needs no per-orientation long axis.</summary>
        public const float SkateparkLedgeHeight = 0.45f;

        /// <summary>
        /// Steepest grade a building's own sloped entrance approach may be
        /// left as a plain smooth surface -- EXPLICIT ASK: "some sort of
        /// staircase that could be built into the terrain if needed but if
        /// its flat enough then no staircase." Comfortably under the
        /// CharacterController's own 50 degree slopeLimit (see
        /// SlopedStreetMinAngleDeg's own doc comment for that number's
        /// source) -- this is meant to read as an easy, ordinary walk-up
        /// incline, not merely "technically climbable." Past this, real
        /// steps get embedded into the same slope instead (see
        /// AddSlopedEntranceApproach).
        /// </summary>
        public const float EntranceRampMaxAngleDeg = 32.0f;

        /// <summary>
        /// Half the length, in chunks, of an elevated bridge span on each
        /// side of its own centre -- EXPLICIT ASK: "make some areas that
        /// the street generates so high that there is open space under the
        /// street at entrance level and theres essentially a second street
        /// under the street like a bridge." 5 chunks either side (an
        /// 11-chunk, ~264m span end to end) reads as a real landmark
        /// crossing.
        ///
        /// MEASURED, NOT GUESSED: a live diagnostic sweep (~12,000 real
        /// street-column candidates on this world) found the single
        /// deepest dip at a half-length of 3 chunks was only 6.8m -- almost
        /// no real population to work with. At 5 chunks the deepest dip
        /// found was 15.1m, with hundreds of candidates clearing a real,
        /// deliberate threshold -- see ElevatedBridgeMinDip's own doc
        /// comment for why that population size matters.
        /// </summary>
        public const int ElevatedBridgeHalfLengthChunks = 5;

        /// <summary>
        /// The span of chunks (along one street column) that a single
        /// elevated-bridge decision is allowed to claim -- EXPLICIT BUG,
        /// found live via a failing "no gap" test: two ADJACENT chunks each
        /// independently searching their own +/-ElevatedBridgeHalfLength
        /// Chunks window for a qualifying centre can lock onto two
        /// DIFFERENT genuine valleys when the windows overlap but aren't
        /// identical (chunk Y's window is Y's neighbour's window shifted by
        /// one), each one legitimately passing ElevatedBridgeMinDip and the
        /// chance roll on its own -- producing exactly the reported "larger
        /// gap" where one chunk's deck simply doesn't meet the next chunk's
        /// own differently-centred deck at all, distinct from the smaller
        /// "doesnt tilt properly" per-chunk-flatness bug DeckHeightAtWorldZ
        /// already fixes.
        ///
        /// FIXED BY PARTITIONING, not by picking a smarter window: chunkY's
        /// own block is `FloorDiv(chunkY, ElevatedBridgeBlockChunks)`, a
        /// value that depends only on chunkY itself, never on which nearby
        /// chunk is asking -- so every chunk in the same block always
        /// searches the exact same fixed candidate range and necessarily
        /// picks the exact same centre. Any sliding, chunkY-relative window
        /// (even "closest centre" or "deepest dip wins") cannot guarantee
        /// this: a candidate that enters or leaves the window as chunkY
        /// shifts by one can always flip the choice right at the boundary
        /// between two chunks, which is exactly the bug being fixed here.
        ///
        /// SIZED AT 4x ElevatedBridgeHalfLengthChunks (20 chunks), not
        /// 2x+1 (the minimum that fits one span at all): a block exactly
        /// the size of one span leaves the candidate centre only one legal
        /// position -- dead centre of the block -- which is the same
        /// "quantised into fixed, evenly-spaced buckets" approach
        /// ElevatedBridgeHalfLengthChunks's own doc comment already found
        /// (via a live ~10,000-candidate sweep) matches zero real valleys,
        /// since a real dip's actual lowest point almost never lands
        /// exactly on an arbitrary fixed grid line. 4x leaves 10 legal
        /// candidate positions per block (see TryFindBlockBridgeCentre),
        /// restoring the floating search that actually finds real terrain,
        /// while every one of those 10 positions' own required
        /// [centre-half, centre+half] reach is, by construction, still
        /// guaranteed to land entirely inside the same block -- so every
        /// chunk that a found bridge could possibly cover is guaranteed to
        /// resolve to that exact same block and therefore that exact same
        /// centre.
        /// </summary>
        private const int ElevatedBridgeBlockChunks = ElevatedBridgeHalfLengthChunks * 4;

        /// <summary>
        /// How much lower the terrain has to genuinely dip at a span's own
        /// centre, relative to BOTH its flanking ends, before that dip is
        /// even eligible to become a bridge -- tied to the REAL terrain
        /// (same "below is just an empty plot" philosophy AddSlopedStreetRamp
        /// already established) rather than an arbitrary override. 3m is
        /// still comfortably more than a person's own height (this rig
        /// stands ~2.2m), so a qualifying dip always has real headroom to
        /// walk under once bridged.
        ///
        /// NOT A WHOLE TerraceStep (6.4m): confirmed directly with the same
        /// diagnostic sweep ElevatedBridgeHalfLengthChunks's own doc
        /// comment describes -- at a half-length of 5 chunks, a 6.4m
        /// threshold still only cleared a double-digit number of real
        /// candidates out of ~12,000, most of which then also have to clear
        /// the independent ElevatedBridgeChance roll on top. 3m clears
        /// several hundred, a real population rather than one lucky
        /// outlier, while still being deliberately more than double a
        /// person's own height.
        /// </summary>
        public const double ElevatedBridgeMinDip = 3.0;

        /// <summary>
        /// Chance a genuinely qualifying dip actually becomes a bridge.
        ///
        /// RAISED FROM 0.05 -- EXPLICIT ASK: "start making bridges where
        /// you can walk under." At 5%, combined with real qualifying dips
        /// already being a real but scattered minority of candidates (see
        /// ElevatedBridgeMinDip's own doc comment -- several hundred out of
        /// ~12,000 in the diagnostic sweep), the EXPECTED count across that
        /// whole sweep was only in the dozens, spread thin enough that nothing
        /// showed up anywhere near where a player would actually be
        /// exploring. Still not "every qualifying dip becomes one" -- most
        /// still don't -- but enough that walking around for real actually
        /// finds one instead of needing a targeted diagnostic search to
        /// prove the feature exists at all.
        /// </summary>
        public const double ElevatedBridgeChance = 0.35;

        /// <summary>
        /// Minimum clear height, everywhere along a span, between the deck
        /// and the real street it passes over -- EXPLICIT BUG REPORT, with a
        /// screenshot: a street visibly rising up into the underside of its
        /// own bridge, which a real car obviously cannot drive under. The
        /// deck's own height used to be a straight line between just the two
        /// flanking chunks' CENTRES, 5 chunks either side of the actual dip
        /// -- nothing checked that the real terrain in between (which the
        /// shorter terrain octave can and does wiggle above a straight line
        /// drawn between two more-distant points) ever stayed below it.
        /// SafeDeckHeight clamps the deck to whichever is higher: the smooth
        /// line, or the REAL ground right below that exact point plus this
        /// clearance -- a hard geometric guarantee the deck cannot sit below
        /// the street it spans, not a statistical one. 5m clears this rig's
        /// own ~2.2m height and a car with room to spare.
        /// </summary>
        public const double ElevatedBridgeMinClearance = 5.0;

        /// <summary>Square cross-section of the single support pillar each span chunk gets.</summary>
        public const float ElevatedBridgePillarSize = 0.6f;

        // --- highway system, EXPLICIT ASK: "generate a highway system with
        // exits. utilize the bridges. as well as the curved and sloped
        // roads... look at american cities with lots of highways." Reuses
        // the elevated-bridge deck's own general shape (a real tilted quad
        // plus rails, visible from both sides, a support pillar down to the
        // real ground) but PERMANENTLY, not only over a real terrain dip --
        // see CityLayout.IsHighwayChunk's own doc comment for why that is
        // safe (a highway is always a sparse SUBSET of the existing arterial
        // grid, never a line of its own, so nothing about lot placement or
        // per-chunk independence changes). Genuinely curves (a real gentle
        // sweep, unlike the ordinary street's own now-dormant wobble -- a
        // highway curving for real design reasons is exactly the "reason to
        // bend" that wobble was missing) and genuinely slopes (follows real
        // terrain at a constant clearance above it, easing down to grade at
        // real exits).

        /// <summary>
        /// How high above the real ground a highway deck sits, away from any
        /// exit -- taller than ElevatedBridgeMinClearance's own 5m so a
        /// highway reads as a real elevated freeway rather than just another
        /// bridge, comfortably clearing this world's tallest ordinary street-
        /// front structures (balconies, ribs, guard rails).
        /// </summary>
        public const float HighwayClearance = 14.0f;

        /// <summary>
        /// The taper toward a real exit is NEVER allowed to bring the deck
        /// below THIS fraction of full clearance -- not even at the exit's
        /// own centre. EXPLICIT BUG REPORT, TWICE: the first pass still let
        /// the deck reach a true ground-level touchdown in a short final
        /// stretch confined tightly around the exit -- which turned out to
        /// always be wrong, since every real exit sits on a genuine
        /// cross-arterial by construction (HighwayExitChunks is a whole
        /// multiple of DistrictChunks -- see CityLayout.HighwayClearance
        /// TaperAt's own doc comment for the full reasoning and the
        /// diagnostic that confirmed it). A literal at-grade touchdown was
        /// therefore ALWAYS a solid slab on a live street's own driving
        /// lanes, not an occasional bad case a tighter radius could dodge.
        /// This system has no real interchange geometry to route between
        /// two roads meeting at grade -- a genuinely bigger, separate task
        /// -- so the floor now holds unconditionally: an "exit" reads as
        /// the highway dipping to a comfortable minimum height near an
        /// interchange, never an actual ground-level merge. Reuses
        /// ElevatedBridgeMinClearance's own already-vetted 5m floor rather
        /// than inventing a separate number -- the invariant this creates
        /// ("a highway is never less generous than an ordinary bridge's
        /// own worst case") is a real, deliberate one.
        /// </summary>
        public const float HighwayMinimumClearanceFraction = (float)(ElevatedBridgeMinClearance / HighwayClearance);

        /// <summary>
        /// How far a highway deck's own centreline sweeps sideways at the
        /// peak of its curve -- a real, deliberate sweep, not a wobble.
        ///
        /// CAPPED SO THE SWEPT DECK CAN NEVER LEAVE ITS OWN CHUNK --
        /// EXPLICIT BUG REPORT: "make it so highways cant intersect into
        /// buildings." The original 8m amplitude plus HighwayDeckHalfWidth's
        /// own original 9m reached 17m from centreline at the sweep's peak,
        /// 5m past this chunk's own half-width (ChunkSize/2 = 12m) and into
        /// the NEIGHBOURING chunk's airspace -- which the ordinary grid is
        /// completely free to put a building on, at any height, since a
        /// highway chunk's own footprint is the only thing IsHighwayChunk
        /// keeps clear. This constant plus HighwayDeckHalfWidth now sum to
        /// comfortably under 12m (10m, a 2m safety margin) so the deck's
        /// own paved footprint can never leave the chunk it's built in,
        /// regardless of what the ordinary grid places one chunk over.
        /// </summary>
        public const double HighwayAmplitudeMetres = 3.0;

        /// <summary>Real metres of highway one full lateral sweep spans -- long enough to read as a real gentle curve at highway speed, not a slalom.</summary>
        public const double HighwayWavelengthMetres = 420.0;

        /// <summary>
        /// Half the deck's own paved width -- wider than an ordinary
        /// street's own carriageway, reading as a real multi-lane freeway.
        /// Reduced alongside HighwayAmplitudeMetres's own reduction (see
        /// that constant's doc comment) -- the two together must stay
        /// under ChunkSize/2 (12m) so the deck can never sweep out of its
        /// own chunk into a neighbouring building's airspace. 7m is still
        /// a genuinely wide, real 4-lane elevated freeway footprint.
        /// </summary>
        public const float HighwayDeckHalfWidth = 7.0f;

        /// <summary>Independent draw stream from a lane's own chunk-seed, so the highway's own sweep phase never collides with the ordinary street curve's phase (StreetCurveStream) even though both key off the same lane coordinate.</summary>
        private const ulong HighwayCurveStream = 0x484947485741594BUL; // "HIGHWAYK" (truncated to fit)

        /// <summary>
        /// EXPLICIT ASK: "the highway goes up and down pretty frequently
        /// make the up and downness of it less frequent like make it more
        /// stretched out." The deck's own vertical profile used to be the
        /// raw ContinuousHeightAt, which includes TerrainField's own two
        /// octaves (see TerrainField.OctaveWavelengths, 384m and 144m) --
        /// short enough that a highway travelling at highway scale crested
        /// and dipped every couple hundred metres, reading as choppy rather
        /// than the long, gentle grades a real elevated freeway is
        /// engineered with.
        ///
        /// A window merely comparable to the COARSER octave (tried 320m
        /// first) only partially damps that octave -- measured directly at
        /// just a ~40% cut in direction reversals along a real test line,
        /// not the strong, obviously-stretched-out profile the ask wants.
        ///
        /// NEITHER WINDOW SIZE NOR SAMPLE COUNT ALONE BEHAVED PREDICTABLY.
        /// TerrainField's octaves are value noise (an independently-random
        /// lattice, smoothly interpolated), not periodic sine waves -- there
        /// is no window size that exactly "nulls" a wavelength the way an
        /// integer multiple of a true sinusoid's period would, and measured
        /// directly on one real line, widening the window while keeping few
        /// samples made reversals WORSE, not better (14, then 20, against 24
        /// unsmoothed) purely from which specific points happened to land
        /// where on that one line -- not a real regression, just a single
        /// line's own idiosyncrasy (see TerrainFieldTests.SmoothedHeight
        /// ReversesDirectionFarLessOftenThanRawTerrain's own doc comment,
        /// which sums several separated lines instead of trusting one). A
        /// generously wide window (900m, well past both octaves) plus dense
        /// sampling (see HighwayVerticalSmoothingSamples) is what actually
        /// gave a real, robust cut once measured that way -- cheap to make
        /// dense since this only runs at mesh-build time, never per frame.
        ///
        /// REGRESSION, WITH SCREENSHOT, FOUND THE NEXT DAY: "the highway is
        /// going into the street underground make that impossible." A wide
        /// average has no lower bound relative to the REAL ground at any
        /// single point -- over a local rise the smoothed reference can sit
        /// well below the actual terrain right there, which let the deck's
        /// computed height drop below the real street surface it was
        /// supposed to be flying over. See DeckCornerAt's own doc comment
        /// for the fix: every use of this window now takes
        /// Max(SmoothedHeightAt(...), ContinuousHeightAt(...)) as the ground
        /// reference, never SmoothedHeightAt alone.
        /// </summary>
        private const double HighwayVerticalSmoothingWindowMetres = 900.0;

        /// <summary>
        /// 61 samples over the window above spaces them 15m apart -- about
        /// 9-10 samples per period of the finer 144m octave and ~25 per
        /// period of the coarser 384m one, dense enough to average both
        /// cleanly rather than under-resolve them.
        /// </summary>
        private const int HighwayVerticalSmoothingSamples = 61;

        /// <summary>
        /// Splits a chunk across three buffers, one per material, which become
        /// three submeshes on one object -- cheaper than three objects sharing
        /// a transform.
        ///
        /// The split is by SURFACE, not by what the thing is:
        ///   ground        road and bare terrain
        ///   flatConcrete  pavement, roofs and walkway decks
        ///   structures    building walls, which are the only thing that wants
        ///                 the window facade
        ///
        /// Roofs go with pavement rather than with their own building because
        /// they are the same material in the real world, and because putting
        /// the window texture on the top of every building is glaring the
        /// moment the camera gets above roof height.
        /// </summary>
        public static void BuildChunk(CityWorld world, SkybridgeLayout bridges, int chunkX, int chunkY,
            Vector3 origin, MeshBuffer ground, MeshBuffer flatConcrete, MeshBuffer structures,
            MeshBuffer litGlass = null, MeshBuffer darkGlass = null,
            MeshBuffer crackedGlass = null, GlassDamage damage = null,
            List<CrackedOpening> crackedOpenings = null, MeshBuffer markings = null,
            MeshBuffer guardRail = null, MeshBuffer laneDividers = null,
            MeshBuffer crackDecals = null, MeshBuffer greenery = null, MeshBuffer bark = null,
            bool includeSurface = true, bool far = false)
        {
            // includeSurface false builds the BUILDING alone -- no terrain, street,
            // sidewalk, markings or open-block surface. The title screen bakes a
            // row of real generated buildings that stand on a plane of its own.
            //
            // far builds the DISTANT version of the chunk: the same silhouette, ground and glass, but a building
            // that is only its shell (see BuildBuildingShell) and none of the small street furniture. About half
            // of a downtown chunk's triangles are rooms and stairs nobody can see from a few chunks away.
            if (includeSurface)
            {
                BuildGround(world, chunkX, chunkY, origin, ground, flatConcrete, guardRail, far);
                if (!far)
                {
                    AddRoadMarkings(world, chunkX, chunkY, origin, markings, laneDividers);
                }
                AddOpenBlockSurface(world, chunkX, chunkY, origin, flatConcrete, greenery, bark);
            }

            BuildingSite site = world.BuildingAt(chunkX, chunkY);
            if (!site.Exists || site.AnchorX != chunkX || site.AnchorY != chunkY)
            {
                // Either nothing here, or it belongs to a neighbouring anchor
                // and that chunk will build it. Building it twice would double
                // every multi-chunk tower.
                return;
            }

            if (far)
            {
                BuildBuildingShell(world, site, origin, structures, flatConcrete,
                    new Glazing(litGlass, darkGlass, null, damage, null, null,
                        WindowGrid.LitShareFor(CityChunkGenerator.ZoneOfRole(site.Role))));
                AddWing(world, site, origin, structures, flatConcrete);
                if (bridges != null)
                {
                    BuildBridges(world, bridges, site, origin, flatConcrete);
                }
                return;
            }

            BuildBuilding(world, site, origin, structures, flatConcrete,
                new Glazing(litGlass, darkGlass, crackedGlass, damage, crackedOpenings, crackDecals,
                    WindowGrid.LitShareFor(CityChunkGenerator.ZoneOfRole(site.Role))));
            AddWing(world, site, origin, structures, flatConcrete);
            AddBalconies(world, site, origin, flatConcrete);
            AddRibs(world, site, origin, structures);
            if (bridges != null)
            {
                BuildBridges(world, bridges, site, origin, flatConcrete);
            }
        }

        // --- open blocks: parks and plazas ---

        /// <summary>
        /// Whether open blocks get NATURE -- lawns and trees. OFF: EXPLICIT
        /// FEEDBACK on the first version, "remove the trees i didnt want any
        /// nature yet." The code is kept behind this switch rather than
        /// deleted, because "yet" means it is wanted later; flip it (and the
        /// CityStreamer grass/bark materials, already wired) to bring it back.
        /// Paved plazas are not nature and stay.
        /// </summary>
        public static bool DrawNature = false;

        /// <summary>How far a lawn or plaza floats above the bare pad, so it never z-fights it.</summary>
        public const float OpenSurfaceLift = 0.04f;

        /// <summary>Trees per open chunk: at least this many.</summary>
        public const int MinTreesPerChunk = 3;

        /// <summary>...and at most this many. Trees sit on a jittered 3x3 grid, so the cap is 9.</summary>
        public const int MaxTreesPerChunk = 7;

        /// <summary>Trees stay at least this far inside their own chunk, so a canopy never leans over a neighbour's wall.</summary>
        public const float TreeEdgeClearance = 3.0f;

        private const ulong TreeStream = 0x54524545UL;   // "TREE"

        /// <summary>
        /// True where an open block is a paved PLAZA rather than a park: the
        /// downtown blocks left open are civic squares, everywhere else they
        /// are lawn. (Industrial open ground is left as bare yard.)
        /// </summary>
        public static bool IsPlazaZone(ZoneKind zone)
        {
            return zone == ZoneKind.Core || zone == ZoneKind.Urban;
        }

        /// <summary>
        /// What a chunk of an OPEN BLOCK looks like. Until this, an open block
        /// was bare terrace -- a hole in the city with nothing in it, which is
        /// what the block system's own doc comment said the renderer would
        /// "eventually want to pave". A park is a lawn with blocky trees in the
        /// same low-poly idiom as everything else here; a downtown open block is
        /// a paved plaza with a few trees; an industrial one stays bare yard.
        /// All of it is decoration built into buffers that only exist when the
        /// caller wants them, and none of it is collidable -- the terrace under
        /// it already is, and a trunk is not worth cooking a collider for.
        ///
        /// Deterministic per chunk (seeded from the chunk seed) and confined to
        /// the chunk's own square, so chunks stay independent.
        /// </summary>
        private static void AddOpenBlockSurface(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            MeshBuffer pavement, MeshBuffer greenery, MeshBuffer bark)
        {
            if (greenery == null && pavement == null)
            {
                return;
            }
            if (world.RoleAt(chunkX, chunkY).Kind != LotKind.Open)
            {
                return;
            }
            if (!CityLayout.TryGetBlockRect(chunkX, chunkY, out int minX, out int minY, out int width, out int height))
            {
                return;
            }

            ZoneKind zone = CityBlocks.ZoneOfBlock(minX, minY, width, height);
            if (zone == ZoneKind.Industrial)
            {
                return;
            }
            bool plaza = IsPlazaZone(zone);
            if (!plaza && !DrawNature)
            {
                return;   // a park is all nature, so it stays bare terrace
            }

            float size = CityLayout.ChunkSize;
            float x0 = (float)(chunkX * (double)size - origin.x);
            float z0 = (float)(chunkY * (double)size - origin.z);
            float x1 = x0 + size;
            float z1 = z0 + size;
            float y = world.Terrain.PadHeightFor(chunkX, chunkY) - origin.y + OpenSurfaceLift;

            MeshBuffer surface = plaza ? pavement : greenery;
            if (surface != null)
            {
                surface.AddQuad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1),
                    new Vector3(x1, y, z0), Vector3.up,
                    new Vector2(0, 0), new Vector2(0, size), new Vector2(size, size), new Vector2(size, 0));
            }

            if (!DrawNature || greenery == null || bark == null)
            {
                return;
            }

            var rng = new DeterministicRandom(unchecked((ulong)(long)world.ChunkSeed(chunkX, chunkY)), TreeStream);
            int wanted = MinTreesPerChunk + rng.Below(MaxTreesPerChunk - MinTreesPerChunk + 1);
            if (plaza)
            {
                wanted = 1 + rng.Below(3);
            }

            // A jittered 3x3 grid: trees are spread out and never overlap, and
            // the cells to fill are picked by walking the grid from a random
            // start rather than rejection sampling.
            float cell = (size - 2.0f * TreeEdgeClearance) / 3.0f;
            int start = rng.Below(9);
            for (int placed = 0, step = 0; placed < wanted && step < 9; step++)
            {
                int index = (start + step * 4) % 9;   // 4 is coprime with 9, so this visits every cell once
                int column = index % 3;
                int row = index / 3;
                float cx = x0 + TreeEdgeClearance + (column + 0.5f) * cell + (float)rng.Range(-1.0, 1.0);
                float cz = z0 + TreeEdgeClearance + (row + 0.5f) * cell + (float)rng.Range(-1.0, 1.0);
                AddTree(greenery, bark, cx, cz, y, rng);
                placed++;
            }
        }

        /// <summary>One blocky tree standing on (cx, baseY, cz): a broadleaf, a stepped conifer or a tall poplar.</summary>
        private static void AddTree(MeshBuffer greenery, MeshBuffer bark, float cx, float cz, float baseY,
            DeterministicRandom rng)
        {
            float scale = (float)rng.Range(0.8, 1.3);
            int kind = rng.Below(3);
            float trunk = 0.18f * scale;

            switch (kind)
            {
                case 0:   // broadleaf
                {
                    float trunkHeight = 2.8f * scale;
                    bark.AddBox(new Vector3(cx - trunk, baseY, cz - trunk),
                        new Vector3(cx + trunk, baseY + trunkHeight, cz + trunk));
                    float half = 2.1f * scale;
                    greenery.AddBox(new Vector3(cx - half, baseY + trunkHeight - 0.4f, cz - half),
                        new Vector3(cx + half, baseY + trunkHeight + 3.0f * scale, cz + half), includeBottom: true);
                    break;
                }
                case 1:   // conifer: three stepped tiers
                {
                    float trunkHeight = 1.6f * scale;
                    bark.AddBox(new Vector3(cx - trunk, baseY, cz - trunk),
                        new Vector3(cx + trunk, baseY + trunkHeight, cz + trunk));
                    float y = baseY + trunkHeight - 0.2f;
                    float[] halves = { 1.9f, 1.3f, 0.75f };
                    foreach (float tierHalf in halves)
                    {
                        float half = tierHalf * scale;
                        float tierHeight = 1.9f * scale;
                        greenery.AddBox(new Vector3(cx - half, y, cz - half),
                            new Vector3(cx + half, y + tierHeight, cz + half), includeBottom: true);
                        y += tierHeight - 0.3f;
                    }
                    break;
                }
                default:  // tall poplar
                {
                    float trunkHeight = 3.0f * scale;
                    bark.AddBox(new Vector3(cx - trunk * 0.8f, baseY, cz - trunk * 0.8f),
                        new Vector3(cx + trunk * 0.8f, baseY + trunkHeight, cz + trunk * 0.8f));
                    float half = 0.95f * scale;
                    greenery.AddBox(new Vector3(cx - half, baseY + trunkHeight - 0.6f, cz - half),
                        new Vector3(cx + half, baseY + trunkHeight + 6.2f * scale, cz + half), includeBottom: true);
                    break;
                }
            }
        }

        private static void BuildGround(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            MeshBuffer ground, MeshBuffer pavement, MeshBuffer guardRail = null, bool far = false)
        {
            float pad = world.Terrain.PadHeightFor(chunkX, chunkY);
            float size = CityLayout.ChunkSize;

            float x0 = (float)(chunkX * (double)size - origin.x);
            float z0 = (float)(chunkY * (double)size - origin.z);
            float x1 = x0 + size;
            float z1 = z0 + size;
            float y = pad - origin.y;

            bool isStreet = CityLayout.IsStreetChunk(chunkX, chunkY);

            // HILLSIDE STREETS, NOT FLAT PLATEAUS -- EXPLICIT ASK, repeated
            // and clarified after the terrace-edge ramps below kept missing
            // the point: "sloped streets... at an angle like hills... there
            // is nothing like that, all hard corners." A terrace-edge ramp
            // only ever bridges the rare exact chunk where two streets
            // happen to cross a terrace line; every OTHER metre of every
            // street still sat dead flat on its own quantised pad, which is
            // what actually read as "all hard corners" from anywhere else.
            //
            // A street's own ground now follows ContinuousHeightAt -- the
            // SAME smooth field TerraceStep quantises FROM (see that
            // method's own doc comment: "a road ramp... wants the shape
            // without the steps") -- sampled at its own four corners rather
            // than sitting flat at one pad. Buildings are untouched and
            // still sit on the quantised, terraced pad: the Chongqing
            // massing this world is otherwise built around is a claim about
            // BUILDINGS stepping up a hillside on flat platforms, not about
            // the streets between them being flat too, and real Chongqing's
            // own streets are exactly this kind of continuously graded.
            //
            // SEAMS ACROSS CHUNK BOUNDARIES FOR FREE. Two neighbouring
            // street chunks share their corner world positions exactly, so
            // sampling the same smooth field at that shared corner from
            // either side always agrees -- no stitching, no separate pass
            // to reconcile them, the same reason a shared mesh vertex never
            // needs welding.
            float h00 = y, h10 = y, h11 = y, h01 = y;
            if (isStreet)
            {
                StreetCornerHeights(world, chunkX, chunkY, origin, out h00, out h10, out h11, out h01);

                // THE CARRIAGEWAY CURVES; everything else built from h00..h01
                // below (retaining walls, corner skirts, sidewalks, the
                // elevated-bridge deck) deliberately does not -- see this
                // pass's own plan for why: only the road surface + its own
                // paint bend within the existing straight corridor, nothing
                // building-adjacent is touched, so lots/retaining walls keep
                // exactly the guarantees they already have.
                AddCurvingStreetGround(world, chunkX, chunkY, origin, x0, z0, x1, z1, ground);

                // ADDITIVE, NOT A REPLACEMENT -- EXPLICIT ASK: "the street
                // generates so high that there is open space under the
                // street... essentially a second street under the street
                // like a bridge." The real, continuously-tilted street
                // surface just built above is left completely alone -- it
                // IS "the second street under the bridge," for free, since
                // nothing here removes it. This only ever adds a second,
                // higher deck on top wherever the real terrain already dips
                // enough to warrant one.
                // SKIPPED ON A HIGHWAY CHUNK -- the highway's own deck below
                // is permanently elevated well clear of any real dip, so the
                // dip-triggered bridge would be redundant at best (and, if
                // both fired, two overlapping decks at worst).
                if (guardRail != null && !CityLayout.IsHighwayChunk(chunkX, chunkY) &&
                    TryGetElevatedBridgeDeckHeight(world, chunkX, chunkY,
                        out float deckHeightAtZ0, out float deckHeightAtZ1))
                {
                    float deckY0 = deckHeightAtZ0 - origin.y;
                    float deckY1 = deckHeightAtZ1 - origin.y;
                    AddElevatedBridgeDeck(x0, z0, x1, z1, deckY0, deckY1, ground, guardRail);
                    float realGroundY = BilinearHeight(0.5f, 0.5f, h00, h10, h11, h01);
                    float deckYAtCentre = (deckY0 + deckY1) * 0.5f;
                    AddElevatedBridgePillar((x0 + x1) * 0.5f, (z0 + z1) * 0.5f, deckYAtCentre, realGroundY, ground);
                }

                // THE HIGHWAY ITSELF -- permanently elevated, unlike the
                // dip-triggered bridge just above. See this file's own
                // "highway system" constants block for the full reasoning.
                if (guardRail != null && CityLayout.IsHighwayChunk(chunkX, chunkY))
                {
                    AddHighwayDeck(world, chunkX, chunkY, origin, x0, z0, x1, z1, ground, guardRail);
                }

                // A REAL REASON FOR A STREET NOT TO GO STRAIGHT THROUGH, NOT
                // A DECORATIVE WOBBLE -- EXPLICIT FEEDBACK on the curve above:
                // "roads shouldnt be just wiggly for no reason... a turn or
                // dead end or 4 way intersection." Whichever arm
                // CityLayout.IsClosedJunctionArm rolled closed gets one of
                // several real treatments (CityLayout.TreatmentForClosedArm)
                // -- EXPLICIT ASK, after seeing the plain barrier: "instead
                // of ... colliding with each other you should have some that
                // have some sort of pedestrian walkable plaza, some that are
                // just dead ends and are rounded off ... 2% chance of there
                // being a hidden skatepark ... a wide variety of solutions
                // and make it so that they all can occur." See that method's
                // own doc comment for why this is built here, entirely
                // within the junction chunk's own footprint, rather than a
                // change to the street grid (which CityChunkGenerator's own
                // lot placement depends on staying exactly as regular as it
                // already is).
                if (guardRail != null && IsJunctionChunk(chunkX, chunkY))
                {
                    var junctionPatch = new BilinearPatch(x0, z0, size, h00, h10, h11, h01);
                    Vector3 JunctionTopAt(float atX, float atZ) =>
                        new Vector3(atX, junctionPatch.HeightAt(atX, atZ), atZ);

                    float half = size * 0.5f;
                    float crossing = size - SidewalkWidth * 2.0f;
                    float halfCrossing = crossing * 0.5f;
                    float centreX = x0 + half;
                    float centreZ = z0 + half;

                    AddClosedArmTreatment(chunkX, chunkY, 0, -1,
                        JunctionTopAt(centreX - halfCrossing, z0), JunctionTopAt(centreX + halfCrossing, z0),
                        pavement, ground, guardRail);
                    AddClosedArmTreatment(chunkX, chunkY, 0, 1,
                        JunctionTopAt(centreX - halfCrossing, z1), JunctionTopAt(centreX + halfCrossing, z1),
                        pavement, ground, guardRail);
                    AddClosedArmTreatment(chunkX, chunkY, -1, 0,
                        JunctionTopAt(x0, centreZ - halfCrossing), JunctionTopAt(x0, centreZ + halfCrossing),
                        pavement, ground, guardRail);
                    AddClosedArmTreatment(chunkX, chunkY, 1, 0,
                        JunctionTopAt(x1, centreZ - halfCrossing), JunctionTopAt(x1, centreZ + halfCrossing),
                        pavement, ground, guardRail);
                }
            }
            else
            {
                // TAKE THE BUILDING'S FOOTPRINT OUT OF THE GROUND. A building's own
                // lobby floor sits at BaseHeight, and for a lot on level ground
                // that is exactly this quad's own height -- two coplanar upward
                // faces, which is a z-fight rather than a floor. Cutting the
                // footprint leaves one surface, and the ground under a building was
                // never visible anyway.
                //
                // Every chunk cuts only its OWN share of the footprint, which is
                // its square intersected with the building, so a building covering
                // four chunks is handled by four separate chunks agreeing without
                // talking to each other -- the same rule the rest of this file
                // works by.
                BuildingSite standing = world.BuildingAt(chunkX, chunkY);
                Rect covered = Rect.zero;
                if (standing.Exists)
                {
                    covered = Rect.MinMaxRect(
                        Mathf.Max(x0, (float)(standing.MinX - origin.x)),
                        Mathf.Max(z0, (float)(standing.MinZ - origin.z)),
                        Mathf.Min(x1, (float)(standing.MaxX - origin.x)),
                        Mathf.Min(z1, (float)(standing.MaxZ - origin.z)));
                    if (covered.width <= 0.0f || covered.height <= 0.0f)
                    {
                        covered = Rect.zero;
                    }
                }
                AddSlabFace(ground, x0, x1, z0, z1, covered, y, Vector3.up);
            }

            // The same bilinear patch AddTiltedStreetGround built the ground
            // itself from -- for a BUILDING chunk all four corners equal
            // the same flat y, so this collapses to exactly the old flat
            // behaviour there with no branching needed at every call site
            // below. For a STREET chunk it means every wall, stair, ramp,
            // and corner post below springs from the real tilted edge the
            // ground actually has, not the flat quantised pad the ground
            // itself no longer sits at -- the gap ATerraceEdgeAlwaysGetsA
            // RetainingWall exists to catch.
            var edgePatch = new BilinearPatch(x0, z0, size, h00, h10, h11, h01);
            Vector3 TopAt(float atX, float atZ) => new Vector3(atX, edgePatch.HeightAt(atX, atZ), atZ);

            // Retaining walls, but only where the neighbour is actually lower
            // AND is not another street -- two neighbouring streets are now
            // both individually tilted to the same continuous field above,
            // so they already meet seamlessly with no wall or ramp needed;
            // building a skirt there too would draw a wall or a hillside
            // ramp bisecting an otherwise smooth slope. Skirting every edge
            // unconditionally would also bury a wall inside solid ground on
            // roughly three quarters of them regardless -- invisible, but
            // paid for on every chunk in the world.
            bool westToStreet = isStreet && CityLayout.IsStreetChunk(chunkX - 1, chunkY);
            bool eastToStreet = isStreet && CityLayout.IsStreetChunk(chunkX + 1, chunkY);
            bool southToStreet = isStreet && CityLayout.IsStreetChunk(chunkX, chunkY - 1);
            bool northToStreet = isStreet && CityLayout.IsStreetChunk(chunkX, chunkY + 1);

            // An edge shared with ANOTHER CHUNK OF THE SAME BUILDING runs
            // through the middle of the building, whose lobby and podium own
            // that ground. Its retaining wall below stays (it is what closes
            // the strips the building does not cover, beside the street), but
            // the STAIRS AND GUARDRAIL built at a terrace edge must not: they
            // land inside the footprint, their top tread coplanar with the
            // lobby floor, on any multi-chunk building whose chunks straddle a
            // terrace step. Latent for as long as multi-chunk lots have
            // existed; surfaced when re-laying the city put such a building
            // under the lobby-floor tests.
            bool westInternal = !isStreet && SharesABuilding(world, chunkX, chunkY, chunkX - 1, chunkY);
            bool eastInternal = !isStreet && SharesABuilding(world, chunkX, chunkY, chunkX + 1, chunkY);
            bool southInternal = !isStreet && SharesABuilding(world, chunkX, chunkY, chunkX, chunkY - 1);
            bool northInternal = !isStreet && SharesABuilding(world, chunkX, chunkY, chunkX, chunkY + 1);

            if (!westToStreet)
            {
                AddSkirtIfLower(world, chunkX - 1, chunkY,
                    TopAt(x0, z0), TopAt(x0, z1), Vector3.left, origin, ground);
            }
            if (!eastToStreet)
            {
                AddSkirtIfLower(world, chunkX + 1, chunkY,
                    TopAt(x1, z1), TopAt(x1, z0), Vector3.right, origin, ground);
            }
            if (!southToStreet)
            {
                AddSkirtIfLower(world, chunkX, chunkY - 1,
                    TopAt(x1, z0), TopAt(x0, z0), Vector3.back, origin, ground);
            }
            if (!northToStreet)
            {
                AddSkirtIfLower(world, chunkX, chunkY + 1,
                    TopAt(x0, z1), TopAt(x1, z1), Vector3.forward, origin, ground);
            }

            // A real staircase and a short guardrail wherever one of the four
            // skirts above just fired next to a street -- see
            // AddStreetEdgeFeatures's own doc comment for why this runs
            // alongside AddSkirtIfLower rather than inside it.
            //
            // NO SIDEWALK-EXISTENCE FLAG NEEDED ANY MORE. This USED to check
            // the PERPENDICULAR neighbours (e.g. south/north for the west
            // edge) because the old street-vs-street version of this system
            // needed a sidewalk to carve into and the WEST edge itself had
            // none of its own whenever the west neighbour was ALSO a street
            // (AddSidewalks builds no west sidewalk in that case) -- the
            // only sidewalk reaching that corner was whichever perpendicular
            // one happened to exist. Now that BuildGround's own
            // westToStreet/etc. gating means this is only ever called for a
            // street-vs-BUILDING edge (see AddStreetEdgeFeatures's own "only
            // ever called" comment), the edge's OWN sidewalk is guaranteed
            // to exist every time: AddSidewalks builds it under the exact
            // same "neighbour is not a street" condition that got us into
            // this call in the first place. Checking the perpendicular
            // neighbours instead of that made the gate ask about the wrong
            // sidewalk entirely -- one that FAILING to exist (a mid-block
            // street chunk with plain building neighbours to both its own
            // south and north) does not mean there is nowhere to carve a
            // stair, which is exactly what silently zeroed out every stair
            // in the game (caught by AStreetTerraceDropWithASidewalkGetsA
            // RealStaircaseOrASlopedRamp asserting stairCases > 0).
            bool chunkAlreadySloped = false;
            if (!far && !westToStreet && !westInternal)
            {
                AddStreetEdgeFeatures(world, chunkX, chunkY, chunkX - 1, chunkY,
                    TopAt(x0, z0), TopAt(x0, z1), Vector3.left,
                    TopAt(x0, z0 + SidewalkWidth * 0.5f), TopAt(x0, z1 - SidewalkWidth * 0.5f), origin,
                    pavement, guardRail, ground, ref chunkAlreadySloped, isDoorFacingEdge: false);
            }
            if (!far && !eastToStreet && !eastInternal)
            {
                AddStreetEdgeFeatures(world, chunkX, chunkY, chunkX + 1, chunkY,
                    TopAt(x1, z1), TopAt(x1, z0), Vector3.right,
                    TopAt(x1, z1 - SidewalkWidth * 0.5f), TopAt(x1, z0 + SidewalkWidth * 0.5f), origin,
                    pavement, guardRail, ground, ref chunkAlreadySloped, isDoorFacingEdge: false);
            }
            if (!far && !southToStreet && !southInternal)
            {
                AddStreetEdgeFeatures(world, chunkX, chunkY, chunkX, chunkY - 1,
                    TopAt(x1, z0), TopAt(x0, z0), Vector3.back,
                    TopAt(x1 - SidewalkWidth * 0.5f, z0), TopAt(x0 + SidewalkWidth * 0.5f, z0), origin,
                    pavement, guardRail, ground, ref chunkAlreadySloped, isDoorFacingEdge: false);
            }
            if (!far && !northToStreet && !northInternal)
            {
                AddStreetEdgeFeatures(world, chunkX, chunkY, chunkX, chunkY + 1,
                    TopAt(x0, z1), TopAt(x1, z1), Vector3.forward,
                    TopAt(x0 + SidewalkWidth * 0.5f, z1), TopAt(x1 - SidewalkWidth * 0.5f, z1), origin,
                    pavement, guardRail, ground, ref chunkAlreadySloped, isDoorFacingEdge: true);
            }

            // Corner posts, for a DIAGONAL neighbour that drops away even when
            // neither of the two flanking cardinal neighbours does. Same
            // street-to-street exclusion as the cardinal skirts above.
            //
            // The four calls above only ever compare a chunk against the one
            // directly N/S/E/W of it. A diagonal neighbour is never compared
            // against at all -- and terracing only bounds how much a CARDINAL
            // step can be (see TerrainField.MaxNeighbourTerraceSteps), not a
            // diagonal one, which is free to compound two independent cardinal
            // drops. When that happens at a corner where neither flanking edge
            // is itself stepped, no skirt gets built anywhere near it: a slot
            // of open air (and an unobstructed line to the sun) right at the
            // corner, small enough to miss in a screenshot from most angles but
            // large enough to stand next to.
            if (!(isStreet && CityLayout.IsStreetChunk(chunkX - 1, chunkY - 1)))
            {
                AddCornerSkirtIfLower(world, chunkX - 1, chunkY - 1, TopAt(x0, z0),
                    TopAt(x0 + CornerSkirtReach, z0), Vector3.back,
                    TopAt(x0, z0 + CornerSkirtReach), Vector3.left, origin, ground);
            }
            if (!(isStreet && CityLayout.IsStreetChunk(chunkX + 1, chunkY - 1)))
            {
                AddCornerSkirtIfLower(world, chunkX + 1, chunkY - 1, TopAt(x1, z0),
                    TopAt(x1 - CornerSkirtReach, z0), Vector3.back,
                    TopAt(x1, z0 + CornerSkirtReach), Vector3.right, origin, ground);
            }
            if (!(isStreet && CityLayout.IsStreetChunk(chunkX - 1, chunkY + 1)))
            {
                AddCornerSkirtIfLower(world, chunkX - 1, chunkY + 1, TopAt(x0, z1),
                    TopAt(x0 + CornerSkirtReach, z1), Vector3.forward,
                    TopAt(x0, z1 - CornerSkirtReach), Vector3.left, origin, ground);
            }
            if (!(isStreet && CityLayout.IsStreetChunk(chunkX + 1, chunkY + 1)))
            {
                AddCornerSkirtIfLower(world, chunkX + 1, chunkY + 1, TopAt(x1, z1),
                    TopAt(x1 - CornerSkirtReach, z1), Vector3.forward,
                    TopAt(x1, z1 - CornerSkirtReach), Vector3.right, origin, ground);
            }

            AddSidewalks(world, chunkX, chunkY, origin, x0, z0, x1, z1, TopAt, pavement);

            // NO PHYSICAL BARRIER HERE ANY MORE -- EXPLICIT BUG REPORT, with
            // screenshots: a bollard row planted across an otherwise
            // completely ordinary-looking street read as "the road is
            // broken," not "this is a pedestrian plaza" -- there is nothing
            // about the surrounding pavement, signage, or approach that
            // tells a player this one street segment is different before
            // they are already at the wall of posts. Explicit follow-up
            // confirmed this is not a readability problem paving could fix:
            // "it is clearly just blocking the street and needs to not
            // generate like this." IsPedestrianZoneChunk itself, and its
            // cosmetic effects (no lane paint, benches, books -- see
            // CityLayout.IsBookChunk), are unaffected; only the physical
            // bollard barrier is gone.
        }

        /// <summary>
        /// A street chunk's own tilted ground, from four independently
        /// sampled corner heights -- see BuildGround's own "hillside
        /// streets" doc comment. The four corners are not generally
        /// coplanar (the smooth field can curve a little even across one
        /// 24m chunk), so this picks one representative normal from the
        /// diagonal cross product rather than trying to average four -- at
        /// the shallow grades TerrainField.MaxSlope actually bounds this to
        /// (worst case ~23 degrees), the shading error from treating a
        /// gently bowed quad as flat is not something a player will ever
        /// notice.
        /// </summary>
        private static void AddTiltedStreetGround(MeshBuffer ground,
            Vector3 corner00, Vector3 corner10, Vector3 corner11, Vector3 corner01)
        {
            Vector3 normal = Vector3.Cross(corner10 - corner00, corner01 - corner00).normalized;
            if (Vector3.Dot(normal, Vector3.up) < 0.0f)
            {
                normal = -normal;
            }
            float width = Vector3.Distance(corner00, corner10);
            float depth = Vector3.Distance(corner00, corner01);
            ground.AddQuad(corner00, corner10, corner11, corner01, normal,
                new Vector2(0, 0), new Vector2(width, 0), new Vector2(width, depth), new Vector2(0, depth));
        }

        /// <summary>
        /// A street chunk's own ground as a real curving ribbon: many
        /// narrow AddTiltedStreetGround segments along the street's own
        /// travel axis, each shifted sideways by StreetCurveLateralOffsetAt
        /// and tilted to the real continuous terrain -- see
        /// StreetCurveSegmentLength's own doc comment for why so many
        /// segments this time. Replaces the single-quad AddTiltedStreet
        /// Ground call entirely; near a junction (or inside a tight local
        /// neighbourhood whose own street spacing leaves no room for the
        /// taper), the offset is already ~0 everywhere in this chunk and
        /// every segment ends up coplanar -- more triangles than a single
        /// quad needed for the same result, never wrong, and not enough of
        /// them anywhere in a real streamed radius to matter.
        ///
        /// SEAMS ACROSS CHUNK BOUNDARIES FOR FREE, the same reason
        /// AddTiltedStreetGround's own single-quad version already had
        /// them: the offset is a pure function of real world position, so
        /// two neighbouring chunks sampling the exact same shared boundary
        /// world coordinate always agree, with no stitching needed.
        /// </summary>
        private static void AddCurvingStreetGround(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            float x0, float z0, float x1, float z1, MeshBuffer ground)
        {
            bool runsAlongX = CityLayout.StreetRunsAlongX(chunkX, chunkY);
            int laneKey = runsAlongX ? chunkY : chunkX;
            double phase = StreetCurvePhaseFor(world, laneKey);
            float size = CityLayout.ChunkSize;

            double worldAlongStart = runsAlongX ? chunkX * (double)size : chunkY * (double)size;
            double worldAcrossNear = runsAlongX ? z0 + origin.z : x0 + origin.x;
            double worldAcrossFar = runsAlongX ? z1 + origin.z : x1 + origin.x;

            int segments = Mathf.Max(1, Mathf.RoundToInt(size / StreetCurveSegmentLength));
            double segLen = size / segments;

            Vector3 CornerAt(double worldAlong, double worldAcross)
            {
                float taper = StreetCurveTaperAt(laneKey, worldAlong, runsAlongX);
                double offset = StreetCurveOffsetAt(worldAlong, phase) * taper;
                double wx = runsAlongX ? worldAlong : worldAcross + offset;
                double wz = runsAlongX ? worldAcross + offset : worldAlong;
                float height = (float)world.Terrain.ContinuousHeightAt(wx, wz) - origin.y;
                return new Vector3((float)(wx - origin.x), height, (float)(wz - origin.z));
            }

            for (int i = 0; i < segments; i++)
            {
                double a0 = worldAlongStart + i * segLen;
                double a1 = worldAlongStart + (i + 1) * segLen;

                Vector3 near0 = CornerAt(a0, worldAcrossNear);
                Vector3 near1 = CornerAt(a1, worldAcrossNear);
                Vector3 far1 = CornerAt(a1, worldAcrossFar);
                Vector3 far0 = CornerAt(a0, worldAcrossFar);

                AddTiltedStreetGround(ground, near0, near1, far1, far0);
            }
        }

        /// <summary>
        /// The same four corner samples BuildGround takes to tilt a street
        /// chunk's ground -- factored out so paint (AddRoadMarkings) can
        /// bilinearly match the exact surface it is supposed to lie on
        /// instead of re-deriving its own approximation of it.
        /// </summary>
        private static void StreetCornerHeights(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            out float h00, out float h10, out float h11, out float h01)
        {
            float size = CityLayout.ChunkSize;
            double worldX0 = chunkX * (double)size;
            double worldX1 = worldX0 + size;
            double worldZ0 = chunkY * (double)size;
            double worldZ1 = worldZ0 + size;

            h00 = (float)world.Terrain.ContinuousHeightAt(worldX0, worldZ0) - origin.y;
            h10 = (float)world.Terrain.ContinuousHeightAt(worldX1, worldZ0) - origin.y;
            h11 = (float)world.Terrain.ContinuousHeightAt(worldX1, worldZ1) - origin.y;
            h01 = (float)world.Terrain.ContinuousHeightAt(worldX0, worldZ1) - origin.y;
        }

        /// <summary>
        /// Height of the same bilinear patch AddTiltedStreetGround spans,
        /// at an arbitrary point inside the chunk -- not just its corners.
        /// Paint needs this because a crossing stripe or a centre line does
        /// not sit at a corner, it sits wherever its own geometry falls
        /// across the tilted surface.
        /// </summary>
        private static float BilinearHeight(float u, float v, float h00, float h10, float h11, float h01)
        {
            float bottom = Mathf.Lerp(h00, h10, u);
            float top = Mathf.Lerp(h01, h11, u);
            return Mathf.Lerp(bottom, top, v);
        }

        /// <summary>
        /// One street chunk's tilted surface, queryable at any local (x, z)
        /// rather than only at the four corners BuildGround itself needs --
        /// what road paint (AddMarking/AddCrosswalkBand) samples so it lies
        /// flush with a graded street instead of one flat height per chunk.
        /// PointAt already adds MarkingLift, since every caller wants a
        /// point ON the paint's own surface, never the bare road height.
        /// </summary>
        private readonly struct BilinearPatch
        {
            private readonly float _x0;
            private readonly float _z0;
            private readonly float _invSize;
            private readonly float _h00, _h10, _h11, _h01;

            public BilinearPatch(float x0, float z0, float size, float h00, float h10, float h11, float h01)
            {
                _x0 = x0;
                _z0 = z0;
                _invSize = 1.0f / size;
                _h00 = h00;
                _h10 = h10;
                _h11 = h11;
                _h01 = h01;
            }

            public float HeightAt(float x, float z)
            {
                float u = (x - _x0) * _invSize;
                float v = (z - _z0) * _invSize;
                return BilinearHeight(u, v, _h00, _h10, _h11, _h01);
            }

            public Vector3 PointAt(float x, float z)
            {
                return new Vector3(x, HeightAt(x, z) + MarkingLift, z);
            }
        }

        /// <summary>
        /// Raised pavement along the edges of a street where it meets buildings.
        ///
        /// A junction gets none, and that falls out of the grid rather than
        /// needing a special case: a street chunk has x or y divisible by the
        /// street period, so a straight run has buildable neighbours on exactly
        /// two OPPOSITE sides and a junction has street on all four. The two
        /// strips can therefore never meet at a corner and overlap.
        ///
        /// TILTED, NOT FLAT -- EXPLICIT BUG REPORT: "make the sidewalks sloped
        /// with the roads" plus a staircase reported hovering in mid-air.
        /// This used to build a plain MeshBuffer.AddBox at one flat quantised
        /// y (the chunk's own PadHeightFor), while AddStreetEdgeFeatures' own
        /// stairs/guard-rails already sample BuildGround's real tilted TopAt
        /// patch correctly -- wherever the continuous field diverges from the
        /// quantised pad (routine, off a TerraceStep boundary), the flat
        /// sidewalk sat at the wrong height relative to the correctly-tilted
        /// geometry already sitting on top of it. Takes the same TopAt(x, z)
        /// closure BuildGround already built for exactly this "sample the
        /// real tilted surface at an arbitrary point" need (AddRoadMarkings
        /// already reuses it the same way).
        ///
        /// THE ROAD-FACING EDGE CURVES TOO -- SECOND EXPLICIT BUG REPORT, with
        /// screenshots: a raised diagonal ridge cutting across the pavement,
        /// and road paint appearing to sink half-buried below the surface.
        /// AddCurvingStreetGround (the road itself) rigidly translates its
        /// WHOLE width sideways by StreetCurveOffsetAt at a given point along
        /// the street -- both its near AND far edge shift by the same amount,
        /// which is exactly right for keeping the road's own width constant,
        /// but a sidewalk built as a fixed axis-aligned box (the previous
        /// version of this method) never moved. Wherever the road curved
        /// toward a sidewalk it plowed up through the sidewalk's fixed box
        /// (the ridge); wherever it curved away, a gap opened between them
        /// exposing the flat, uncurved TopAt ground the sidewalk's own
        /// underside was never meant to be seen past (paint, which DOES
        /// correctly follow the curve, then reads as sitting under/through
        /// whichever surface happened to be poking through at that point).
        /// Fixed the way the original curving-street plan always intended:
        /// the OUTER (building-facing) edge of a sidewalk that runs parallel
        /// to the street stays exactly where it always sat; only the INNER
        /// (road-facing) edge picks up the identical StreetCurveOffsetAt/
        /// StreetCurveTaperAt shift the road's own near/far edge gets at that
        /// same world position, so the two surfaces stay flush everywhere
        /// instead of only agreeing where the curve happens to be zero. A
        /// sidewalk that runs PERPENDICULAR to the street (the rare dead-end
        /// cap where the street itself terminates) does not border the curve
        /// at all and stays a plain straight box.
        /// </summary>
        private static void AddSidewalks(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            float x0, float z0, float x1, float z1, System.Func<float, float, Vector3> topAt,
            MeshBuffer pavement)
        {
            if (!CityLayout.IsStreetChunk(chunkX, chunkY))
            {
                return;
            }

            bool runsAlongX = CityLayout.StreetRunsAlongX(chunkX, chunkY);

            if (!CityLayout.IsStreetChunk(chunkX - 1, chunkY))
            {
                if (runsAlongX)
                {
                    AddTiltedSidewalkBox(pavement, topAt, x0, z0, x0 + SidewalkWidth, z1);
                }
                else
                {
                    AddCurvingSidewalk(world, origin, false, chunkX,
                        z0, z1, x0, x0 + SidewalkWidth, pavement);
                }
            }
            if (!CityLayout.IsStreetChunk(chunkX + 1, chunkY))
            {
                if (runsAlongX)
                {
                    AddTiltedSidewalkBox(pavement, topAt, x1 - SidewalkWidth, z0, x1, z1);
                }
                else
                {
                    AddCurvingSidewalk(world, origin, false, chunkX,
                        z0, z1, x1, x1 - SidewalkWidth, pavement);
                }
            }
            if (!CityLayout.IsStreetChunk(chunkX, chunkY - 1))
            {
                if (!runsAlongX)
                {
                    AddTiltedSidewalkBox(pavement, topAt, x0, z0, x1, z0 + SidewalkWidth);
                }
                else
                {
                    AddCurvingSidewalk(world, origin, true, chunkY,
                        x0, x1, z0, z0 + SidewalkWidth, pavement);
                }
            }
            if (!CityLayout.IsStreetChunk(chunkX, chunkY + 1))
            {
                if (!runsAlongX)
                {
                    AddTiltedSidewalkBox(pavement, topAt, x0, z1 - SidewalkWidth, x1, z1);
                }
                else
                {
                    AddCurvingSidewalk(world, origin, true, chunkY,
                        x0, x1, z1, z1 - SidewalkWidth, pavement);
                }
            }

            AddSidewalkCorners(chunkX, chunkY, x0, z0, x1, z1, topAt, pavement);
        }

        /// <summary>
        /// EXPLICIT BUG REPORT, with a screenshot: a bare square notch of
        /// road left uncovered right at a street corner/junction, between
        /// two sidewalk strips that should meet there.
        ///
        /// ROOT CAUSE: the four edge strips above are each built independently
        /// per chunk, one per side that borders a building, and each spans
        /// that side's FULL length -- correct and gapless along a straight
        /// run, where the strip's own far end abuts the next chunk's matching
        /// strip at their shared boundary. At a real turn (this chunk has
        /// TWO ADJACENT sides that are both street, not building -- an L-bend,
        /// a T-junction, or a 4-way crossing), neither of those two street-
        /// facing sides gets a strip of its own, so nothing here ever reaches
        /// into the small square where a sidewalk coming from one arm and a
        /// sidewalk coming from the OTHER arm would otherwise meet. That
        /// square belongs to THIS chunk (whichever chunk sits at the pivot),
        /// not to either arm, since it is bounded by the pivot's own corner.
        ///
        /// CONDITION: for one of this chunk's own four corners to need
        /// filling, BOTH cardinal neighbours meeting at that corner must be
        /// street (that is exactly what leaves neither of this chunk's own
        /// matching edges building-adjacent, so the ordinary strips above
        /// stay silent there) AND the DIAGONAL neighbour across that corner
        /// must NOT be street (if it were, that corner sits in the interior
        /// of a wide street/plaza area with no building edge to pave at all).
        /// A straight run never satisfies this (its two street-facing sides
        /// are OPPOSITE, never adjacent, so no shared corner has both
        /// cardinal neighbours as street) -- confirmed by
        /// SidewalkCornersOnlyFillAtRealTurnsNeverAlongAStraightRun.
        /// </summary>
        private static void AddSidewalkCorners(int chunkX, int chunkY, float x0, float z0, float x1, float z1,
            System.Func<float, float, Vector3> topAt, MeshBuffer pavement)
        {
            if (CityLayout.IsStreetChunk(chunkX - 1, chunkY) && CityLayout.IsStreetChunk(chunkX, chunkY - 1)
                && !CityLayout.IsStreetChunk(chunkX - 1, chunkY - 1))
            {
                AddTiltedSidewalkBox(pavement, topAt, x0, z0, x0 + SidewalkWidth, z0 + SidewalkWidth);
            }
            if (CityLayout.IsStreetChunk(chunkX + 1, chunkY) && CityLayout.IsStreetChunk(chunkX, chunkY - 1)
                && !CityLayout.IsStreetChunk(chunkX + 1, chunkY - 1))
            {
                AddTiltedSidewalkBox(pavement, topAt, x1 - SidewalkWidth, z0, x1, z0 + SidewalkWidth);
            }
            if (CityLayout.IsStreetChunk(chunkX - 1, chunkY) && CityLayout.IsStreetChunk(chunkX, chunkY + 1)
                && !CityLayout.IsStreetChunk(chunkX - 1, chunkY + 1))
            {
                AddTiltedSidewalkBox(pavement, topAt, x0, z1 - SidewalkWidth, x0 + SidewalkWidth, z1);
            }
            if (CityLayout.IsStreetChunk(chunkX + 1, chunkY) && CityLayout.IsStreetChunk(chunkX, chunkY + 1)
                && !CityLayout.IsStreetChunk(chunkX + 1, chunkY + 1))
            {
                AddTiltedSidewalkBox(pavement, topAt, x1 - SidewalkWidth, z1 - SidewalkWidth, x1, z1);
            }
        }

        /// <summary>
        /// One sidewalk strip, as a straight axis-aligned box: a tilted top
        /// walking surface plus four kerb side walls dropping from each top
        /// corner straight down to the real ground at that same corner. Only
        /// for the rare PERPENDICULAR (dead-end) sidewalk that does not run
        /// alongside the curving street -- see AddSidewalks' own doc comment.
        /// A thin wrapper over AddTiltedSidewalkQuad, which does the real work
        /// generically so AddCurvingSidewalk can share it.
        /// </summary>
        private static void AddTiltedSidewalkBox(MeshBuffer pavement, System.Func<float, float, Vector3> topAt,
            float xa, float za, float xb, float zb)
        {
            AddTiltedSidewalkQuad(pavement, topAt(xa, za), topAt(xb, za), topAt(xb, zb), topAt(xa, zb));
        }

        /// <summary>
        /// A sidewalk strip running PARALLEL to a curving street, in short
        /// segments the same "many short pieces along the travel axis" way
        /// AddCurvingStreetGround itself is built (see StreetCurveSegmentLength's
        /// own doc comment for why so many). The OUTER (building-facing) edge
        /// stays at its fixed across-axis coordinate the whole way; the INNER
        /// (road-facing) edge is offset per segment by the exact same
        /// StreetCurveOffsetAt * StreetCurveTaperAt this lane's own road
        /// surface uses at that world position -- see AddSidewalks' own doc
        /// comment for the ridge/gap bug this fixes by keeping the two
        /// surfaces flush instead of only agreeing where the curve is zero.
        ///
        /// SAMPLES ContinuousHeightAt DIRECTLY, NOT topAt -- EXPLICIT BUG
        /// REPORT, with a screenshot: a long diagonal ridge across the road,
        /// and a glimpse of road paint peeking out from under the street.
        /// topAt is only a bilinear approximation of the chunk's own four
        /// CORNERS -- correct there, blind to any real curvature the terrain
        /// has between them, exactly the class of bug AddRoadMarkings' own
        /// RealPointAt already fixed for paint (see its doc comment: "a
        /// single centre sample was off by as much as 3.5m... on a steep
        /// enough grade"). The road surface right next to this strip
        /// (AddCurvingStreetGround) already samples the real field directly
        /// per segment; this sidewalk was still using the coarse
        /// approximation for BOTH its edges, so wherever the real terrain
        /// bowed away from the chunk's own flat 4-corner fit, the sidewalk
        /// and the road it borders silently disagreed on height -- read as a
        /// ridge where the sidewalk rose above the true road surface, and as
        /// paint "sinking" wherever it dipped below it instead.
        /// </summary>
        private static void AddCurvingSidewalk(CityWorld world, Vector3 origin, bool runsAlongX, int laneKey,
            float alongLocalStart, float alongLocalEnd, float outerAcrossLocal, float innerAcrossLocal,
            MeshBuffer pavement)
        {
            double phase = StreetCurvePhaseFor(world, laneKey);
            float length = alongLocalEnd - alongLocalStart;
            int segments = Mathf.Max(1, Mathf.RoundToInt(length / StreetCurveSegmentLength));
            float segLen = length / segments;
            double originAlong = runsAlongX ? origin.x : origin.z;

            Vector3 RealPointAt(float localX, float localZ)
            {
                double worldX = localX + origin.x;
                double worldZ = localZ + origin.z;
                float height = (float)world.Terrain.ContinuousHeightAt(worldX, worldZ) - origin.y;
                return new Vector3(localX, height, localZ);
            }

            Vector3 OuterAt(float alongLocal)
            {
                return runsAlongX
                    ? RealPointAt(alongLocal, outerAcrossLocal)
                    : RealPointAt(outerAcrossLocal, alongLocal);
            }

            Vector3 InnerAt(float alongLocal)
            {
                double worldAlong = originAlong + alongLocal;
                float taper = StreetCurveTaperAt(laneKey, worldAlong, runsAlongX);
                float offset = (float)(StreetCurveOffsetAt(worldAlong, phase) * taper);
                float across = innerAcrossLocal + offset;
                return runsAlongX ? RealPointAt(alongLocal, across) : RealPointAt(across, alongLocal);
            }

            for (int i = 0; i < segments; i++)
            {
                float a0 = alongLocalStart + i * segLen;
                float a1 = alongLocalStart + (i + 1) * segLen;

                AddTiltedSidewalkQuad(pavement, OuterAt(a0), OuterAt(a1), InnerAt(a1), InnerAt(a0));
            }
        }

        /// <summary>
        /// One sidewalk quadrilateral from four explicit ground-level corners
        /// (00/10/11/01, the same loop order AddTiltedStreetGround uses): a
        /// tilted top walking surface (real normal from the 4 independently
        /// sampled corners, same as the road itself) plus four kerb side
        /// walls dropping straight down to the real ground at each corner. No
        /// bottom face -- the underside of a sidewalk is never visible.
        /// Generalizes what used to be an axis-aligned-only box so a curving
        /// inner edge (AddCurvingSidewalk) and a straight one
        /// (AddTiltedSidewalkBox) can share it; wall normals are derived from
        /// the actual corners rather than hardcoded cardinal directions so
        /// they stay correct regardless of which world axis is "along" this
        /// particular strip.
        /// </summary>
        private static void AddTiltedSidewalkQuad(MeshBuffer pavement,
            Vector3 g00, Vector3 g10, Vector3 g11, Vector3 g01)
        {
            Vector3 kerb = Vector3.up * KerbHeight;
            Vector3 t00 = g00 + kerb, t10 = g10 + kerb, t11 = g11 + kerb, t01 = g01 + kerb;

            AddTiltedStreetGround(pavement, t00, t10, t11, t01);

            // g00->g01 points from the outer edge to the inner edge; the
            // outer wall faces the opposite way, the inner wall faces along
            // it. g00->g10 runs along the strip's own travel axis; the two
            // end walls face along/against that.
            Vector3 acrossDir = (g01 - g00).normalized;
            AddFace(pavement, -acrossDir, g00, g10, t10, t00);
            AddFace(pavement, acrossDir, g11, g01, t01, t11);

            Vector3 alongDir = (g10 - g00).normalized;
            AddFace(pavement, -alongDir, g01, g00, t00, t01);
            AddFace(pavement, alongDir, g10, g11, t11, t10);
        }

        // --- road markings, ported from city_chunk.gd's own constants ---

        public const float LineWidth = 0.15f;
        public const float DoubleLineGap = 0.12f;
        public const float CrosswalkDepth = 4.0f;
        public const float CrosswalkStripeWidth = 1.0f;
        public const float CrosswalkStripeGap = 1.0f;

        /// <summary>
        /// FIRST STEP OF A REAL ROAD HIERARCHY -- EXPLICIT ASK: "improve upon
        /// the Hierarchical road network (highways -> main avenues -> side
        /// streets -> alleys -> pedestrian zones)." CityLayout.IsArterialChunk
        /// already exists (the wide street between districts) but until now
        /// rendered IDENTICALLY to an ordinary local street -- same width,
        /// same single double-yellow centreline. This is the first of those
        /// tiers actually reading differently: an arterial gets two
        /// additional dashed white lane-divider lines either side of the
        /// centreline, the same "two lanes each direction" cross-section a
        /// real main avenue has and a real side street does not.
        ///
        /// Distance from centre, not lane width -- an arterial's own paved
        /// width (CityLayout.ChunkSize less two CityMeshBuilder.SidewalkWidth
        /// pavements either side) is split into two lanes per direction, and
        /// this is the boundary between them.
        ///
        /// A full 5-tier hierarchy (a real highway tier, alleys carved
        /// through block interiors, car-free pedestrian zones) needs the lot/
        /// block-carving side of CityChunkGenerator changed too, not just
        /// paint -- deliberately out of scope for this pass, which only
        /// touches road markings.
        /// </summary>
        public const float ArterialLaneDividerOffset = 4.0f;

        /// <summary>Dash length for an arterial's own lane-divider lines -- roughly the 3m a real painted lane stripe uses at this world's scale.</summary>
        public const float ArterialDashLength = 2.5f;

        /// <summary>Gap between one arterial lane-divider dash and the next.</summary>
        public const float ArterialDashGap = 2.5f;

        /// <summary>
        /// How far paint sits above the road it is painted on. Enough to beat
        /// depth precision at the far edge of a streamed world, small enough
        /// that nothing catches on it -- and it never needs collision, because
        /// the road under it already has some.
        ///
        /// 0.012 -> 0.03: EXPLICIT BUG REPORT, with screenshots -- paint
        /// appearing to sink half-buried below the road surface. The
        /// dominant cause was the sidewalk/road curve mismatch AddSidewalks'
        /// own doc comment describes (now fixed), but 0.012m left honestly
        /// no headroom against ordinary single-precision depth error at the
        /// draw distances this world actually uses (streets visible past
        /// 100m+) -- raised for a real safety margin on top of that fix.
        /// </summary>
        public const float MarkingLift = 0.03f;

        /// <summary>
        /// Lane paint: a double yellow line down a straight street, zebra
        /// crossings at a junction.
        ///
        /// WHICH ONE A CHUNK GETS FALLS OUT OF THE GRID rather than being
        /// stored. A street chunk with street on all four sides is a junction
        /// and gets crossings; anything else is a straight run and gets a
        /// centre line down whichever axis made it a street in the first place.
        /// The same derivation the pavement already uses.
        ///
        /// NO CHUNK EVER GETS BOTH IN THE MAIN markings BUFFER, which is what
        /// lets one buffer/one colour carry either -- a junction has no
        /// centre line to draw through the middle of its own crossings.
        ///
        /// laneDividers IS A SEPARATE, WHITE-ONLY THIRD CHANNEL, for
        /// CityLayout.IsArterialChunk's own lane-divider dashes -- see
        /// ArterialLaneDividerOffset's own doc comment. Needed because a
        /// real main avenue's yellow centreline and white lane dividers
        /// coexist in the SAME straight-run chunk, which the yellow-or-white
        /// choice above has no way to express in one buffer; CityStreamer
        /// renders this one with CrosswalkMaterial (white) regardless of
        /// whether that particular chunk is a junction.
        /// </summary>
        private static void AddRoadMarkings(CityWorld world, int chunkX, int chunkY,
            Vector3 origin, MeshBuffer markings, MeshBuffer laneDividers = null)
        {
            if (markings == null || !CityLayout.IsStreetChunk(chunkX, chunkY))
            {
                return;
            }

            float size = CityLayout.ChunkSize;
            float half = size * 0.5f;
            float x0 = (float)(chunkX * (double)size - origin.x);
            float z0 = (float)(chunkY * (double)size - origin.z);
            float centreX = x0 + half;
            float centreZ = z0 + half;

            // ContinuousHeightAt, not PadHeightFor -- a street chunk's own
            // ground is now tilted to the same smooth field (see
            // BuildGround's own "hillside street" comment), so paint held
            // to one flat height would float or sink relative to the road
            // it is supposed to be lying on. Each marking below samples the
            // REAL continuous terrain at its OWN corners (RealPointAt), not
            // a single bilinear patch of the chunk's own 4 corners and not
            // once at the chunk centre -- a centre line spans the entire
            // chunk, and a single centre sample was off by as much as 3.5m
            // at one end of it on a steep enough grade.
            //
            // BilinearPatch -> ContinuousHeightAt, EXPLICIT BUG REPORT (with
            // a screenshot): "some decals and sidewalks are still not
            // sloping with the roads." The road surface at a junction
            // (AddCurvingStreetGround, unconditional for every street chunk)
            // already samples ContinuousHeightAt directly per small ribbon
            // segment, so it correctly follows any real local relief inside
            // the chunk. The OLD crosswalk paint sampled only a bilinear
            // interpolation of the chunk's own 4 CORNERS -- correct at the
            // corners, but blind to any curvature the real terrain has
            // between them, which is exactly where a real noise field
            // usually isn't perfectly flat. Matches the fix
            // AddCurvingCentreLine's own straight-run paint already got.
            Vector3 RealPointAt(float atX, float atZ) => new Vector3(atX,
                (float)world.Terrain.ContinuousHeightAt(atX + origin.x, atZ + origin.z) - origin.y + MarkingLift,
                atZ);

            bool junction = CityLayout.IsStreetChunk(chunkX - 1, chunkY) &&
                            CityLayout.IsStreetChunk(chunkX + 1, chunkY) &&
                            CityLayout.IsStreetChunk(chunkX, chunkY - 1) &&
                            CityLayout.IsStreetChunk(chunkX, chunkY + 1);

            // The width of the road being crossed -- the chunk less the
            // pavement on either side of a straight run, which is what the
            // crossings have to line up with even though a junction itself is
            // paved corner to corner here.
            float crossing = size - SidewalkWidth * 2.0f;

            if (junction)
            {
                // A closed arm (see CityLayout.IsClosedJunctionArm's own doc
                // comment) gets no crosswalk toward it -- nobody paints a
                // crossing leading into a barrier.
                float band = half - CrosswalkDepth * 0.5f;
                if (!CityLayout.IsClosedJunctionArm(chunkX, chunkY, 0, -1))
                {
                    AddCrosswalkBand(markings, RealPointAt, centreX, centreZ - band, crossing, false);
                }
                if (!CityLayout.IsClosedJunctionArm(chunkX, chunkY, 0, 1))
                {
                    AddCrosswalkBand(markings, RealPointAt, centreX, centreZ + band, crossing, false);
                }
                if (!CityLayout.IsClosedJunctionArm(chunkX, chunkY, -1, 0))
                {
                    AddCrosswalkBand(markings, RealPointAt, centreX - band, centreZ, crossing, true);
                }
                if (!CityLayout.IsClosedJunctionArm(chunkX, chunkY, 1, 0))
                {
                    AddCrosswalkBand(markings, RealPointAt, centreX + band, centreZ, crossing, true);
                }
                return;
            }

            // Exactly one axis is this street's own direction of travel; the
            // other is what picked it out as a street at all.
            //
            // CURVING, NOT ONE STRAIGHT STRIPE -- the road itself now bends
            // (AddCurvingStreetGround); a straight line painted over it
            // would float off to one side of the real curved surface. See
            // AddCurvingCentreLine's own doc comment for the reverted
            // 2026-09-05 attempt this replaces (its own jagged double-
            // yellow line was the ORIGINAL bug report for this feature).
            bool runsAlongX = CityLayout.StreetRunsAlongX(chunkX, chunkY);
            float lineOffset = (LineWidth + DoubleLineGap) * 0.5f;

            // NO LANE PAINT ON A CAR-FREE BLOCK -- EXPLICIT ASK: "pedestrian
            // areas." Nobody paints a driving lane down the middle of a
            // street nobody is allowed to drive on.
            if (!CityLayout.IsPedestrianZoneChunk(chunkX, chunkY))
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    AddCurvingCentreLine(world, chunkX, chunkY, origin, runsAlongX, side * lineOffset, markings);
                }
            }

            // MAIN AVENUE, NOT JUST A WIDE LOCAL STREET -- see
            // ArterialLaneDividerOffset's own doc comment. Dashed, unlike the
            // solid double-yellow centreline: a lane divider between two
            // lanes travelling the SAME direction is legal to cross, which a
            // real dashed white line signals and a solid one would not. Into
            // laneDividers, not markings -- white, not this chunk's own
            // yellow-or-white choice (see this method's own doc comment).
            if (laneDividers != null && CityLayout.IsArterialChunk(chunkX, chunkY))
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    AddCurvingCentreLine(world, chunkX, chunkY, origin, runsAlongX,
                        side * ArterialLaneDividerOffset, laneDividers, ArterialDashLength, ArterialDashGap);
                }
            }
        }

        /// <summary>
        /// One of the double-yellow line's two strokes, following the same
        /// curving ribbon the road itself builds (AddCurvingStreetGround) --
        /// segmented the same "many short pieces along the travel axis" way
        /// and for the same reason: EXPLICIT BUG REPORT, with a screenshot,
        /// from the reverted 2026-09-05 attempt: "the double-yellow
        /// centreline as a visibly jagged zigzag." A top-down diagnostic
        /// screenshot at the old ~4-6-segments-per-chunk density looked like
        /// a smooth curve; the real, close-up, eye-level view did not.
        ///
        /// dashLength/dashGap, new: 0 (the default) draws solid, exactly the
        /// original behaviour every existing caller still gets. A positive
        /// dashLength instead keeps or skips each segment based on a dash/gap
        /// test evaluated at that segment's own WORLD position -- a pure
        /// function of position, the same reason the curve itself needs no
        /// stitching across a chunk boundary, so a dash never resets or
        /// jumps at one either. Reused for ArterialLaneDividerOffset's own
        /// dashed lane dividers rather than writing a second near-duplicate
        /// of this whole method.
        /// </summary>
        private static void AddCurvingCentreLine(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            bool runsAlongX, float acrossOffsetFromCentre, MeshBuffer markings,
            float dashLength = 0.0f, float dashGap = 0.0f)
        {
            int laneKey = runsAlongX ? chunkY : chunkX;
            double phase = StreetCurvePhaseFor(world, laneKey);
            float size = CityLayout.ChunkSize;
            double worldAlongStart = runsAlongX ? chunkX * (double)size : chunkY * (double)size;
            double worldAcrossCentre = (runsAlongX ? chunkY : chunkX) * (double)size + size * 0.5;
            float halfWidth = LineWidth * 0.5f;
            double dashPitch = dashLength + dashGap;

            int segments = Mathf.Max(1, Mathf.RoundToInt(size / StreetCurveSegmentLength));
            double segLen = size / segments;

            Vector3 EdgeCorner(double worldAlong, float acrossSign)
            {
                float taper = StreetCurveTaperAt(laneKey, worldAlong, runsAlongX);
                double lateral = StreetCurveOffsetAt(worldAlong, phase) * taper + acrossOffsetFromCentre;
                double worldAcross = worldAcrossCentre + lateral + acrossSign * halfWidth;
                double wx = runsAlongX ? worldAlong : worldAcross;
                double wz = runsAlongX ? worldAcross : worldAlong;
                float height = (float)world.Terrain.ContinuousHeightAt(wx, wz) - origin.y + MarkingLift;
                return new Vector3((float)(wx - origin.x), height, (float)(wz - origin.z));
            }

            for (int i = 0; i < segments; i++)
            {
                double a0 = worldAlongStart + i * segLen;
                double a1 = worldAlongStart + (i + 1) * segLen;

                if (dashLength > 0.0f)
                {
                    double mid = (a0 + a1) * 0.5;
                    double alongMod = ((mid % dashPitch) + dashPitch) % dashPitch;
                    if (alongMod >= dashLength)
                    {
                        continue;
                    }
                }

                Vector3 near0 = EdgeCorner(a0, -1.0f);
                Vector3 near1 = EdgeCorner(a1, -1.0f);
                Vector3 far1 = EdgeCorner(a1, 1.0f);
                Vector3 far0 = EdgeCorner(a0, 1.0f);

                Vector3 normal = Vector3.Cross(near1 - near0, far0 - near0).normalized;
                if (Vector3.Dot(normal, Vector3.up) < 0.0f)
                {
                    normal = -normal;
                }
                AddFace(markings, normal, near0, near1, far1, far0);
            }
        }

        /// <summary>
        /// One crossing's bars.
        ///
        /// THE BARS RUN WITH THE TRAFFIC, not with the pedestrian. That is what
        /// a real zebra crossing looks like and it is easy to get backwards:
        /// the crossing on the north side is for walking north-south, so its
        /// bars lie along Z and repeat across X.
        /// </summary>
        private static void AddCrosswalkBand(MeshBuffer markings, System.Func<float, float, Vector3> pointAt,
            float centreX, float centreZ, float crossingWidth, bool barsAlongX)
        {
            float pitch = CrosswalkStripeWidth + CrosswalkStripeGap;
            int count = Mathf.FloorToInt(crossingWidth / pitch);
            float start = -crossingWidth * 0.5f + pitch * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float along = start + i * pitch;
                if (barsAlongX)
                {
                    AddMarking(markings, pointAt, centreX, centreZ + along,
                        CrosswalkDepth, CrosswalkStripeWidth);
                }
                else
                {
                    AddMarking(markings, pointAt, centreX + along, centreZ,
                        CrosswalkStripeWidth, CrosswalkDepth);
                }
            }
        }

        /// <summary>
        /// A paint quad laid flush against the real, continuously tilted
        /// street surface: each of its four corners samples pointAt (the
        /// real terrain, not a coarse per-chunk approximation -- see
        /// AddRoadMarkings' own RealPointAt doc comment) independently
        /// rather than sharing one flat y, so a stripe running across a
        /// graded street tilts with the road instead of floating or sinking
        /// into it partway along its own length.
        /// </summary>
        private static void AddMarking(MeshBuffer markings, System.Func<float, float, Vector3> pointAt,
            float centreX, float centreZ, float sizeX, float sizeZ)
        {
            float halfX = sizeX * 0.5f;
            float halfZ = sizeZ * 0.5f;
            Vector3 a = pointAt(centreX - halfX, centreZ - halfZ);
            Vector3 b = pointAt(centreX + halfX, centreZ - halfZ);
            Vector3 c = pointAt(centreX + halfX, centreZ + halfZ);
            Vector3 d = pointAt(centreX - halfX, centreZ + halfZ);

            Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
            if (Vector3.Dot(normal, Vector3.up) < 0.0f)
            {
                normal = -normal;
            }
            AddFace(markings, normal, a, b, c, d);
        }

        /// <summary>
        /// The real ground height at a world-space boundary point, on
        /// whichever side the NEIGHBOUR chunk owns -- PadHeightFor's own
        /// quantised value if the neighbour is a building (flat, so that is
        /// exactly right), or ContinuousHeightAt sampled at that exact point
        /// if the neighbour is a street (whose own ground BuildGround
        /// already tilts continuously -- see StreetCornerHeights' own doc
        /// comment for why sampling the same field from either side of a
        /// shared point always agrees, with no separate stitching pass).
        ///
        /// EXPLICIT BUG REPORT (with screenshots): "there is just empty
        /// space" between a tilted street and the building next to it.
        /// Comparing two QUANTISED pads let a street edge and a building
        /// edge agree ("same terrace, no wall needed") even while the
        /// street's own real, continuously-tilted surface sat a metre or
        /// more away from the building's flat one -- a gap invisible to the
        /// pad comparison because it never crossed a whole terrace step.
        /// </summary>
        /// <summary>
        /// True when two chunks are covered by the SAME building -- the edge
        /// between them runs through that building rather than along a
        /// terrace. See the "westInternal" comment in BuildGround.
        /// </summary>
        private static bool SharesABuilding(CityWorld world, int chunkX, int chunkY, int otherX, int otherY)
        {
            BuildingSite here = world.BuildingAt(chunkX, chunkY);
            if (!here.Exists)
            {
                return false;
            }
            BuildingSite other = world.BuildingAt(otherX, otherY);
            return other.Exists && other.AnchorX == here.AnchorX && other.AnchorY == here.AnchorY;
        }

        private static float NeighbourGroundHeightAt(CityWorld world, int neighbourX, int neighbourY,
            float worldX, float worldZ)
        {
            if (CityLayout.IsStreetChunk(neighbourX, neighbourY))
            {
                return (float)world.Terrain.ContinuousHeightAt(worldX, worldZ);
            }
            return world.Terrain.PadHeightFor(neighbourX, neighbourY);
        }

        private static void AddSkirtIfLower(CityWorld world, int neighbourX, int neighbourY,
            Vector3 top0, Vector3 top1, Vector3 normal, Vector3 origin, MeshBuffer ground)
        {
            // top0.y, not this chunk's own pad -- see NeighbourGroundHeightAt's
            // own doc comment: on a street chunk this wall's own top stands
            // on the tilted continuous surface (TopAt at the call site in
            // BuildGround), which can sit above OR below the chunk's
            // quantised pad, and the neighbour side needs the identical
            // real-height treatment now that it can be a street too.
            float neighbourHeight = NeighbourGroundHeightAt(
                world, neighbourX, neighbourY, top0.x + origin.x, top0.z + origin.z);
            if (neighbourHeight >= top0.y)
            {
                return;
            }

            // Overshoot past the neighbour's surface. Meeting it exactly leaves
            // a hairline of z-fighting along every terrace, which reads as the
            // world flickering when the camera moves.
            float drop = top0.y - neighbourHeight + 0.5f;
            float bottomY = top0.y - Mathf.Min(drop, SkirtDepth);

            Vector3 bottom0 = new Vector3(top0.x, bottomY, top0.z);
            Vector3 bottom1 = new Vector3(top1.x, bottomY, top1.z);
            float width = Vector3.Distance(top0, top1);
            float height = top0.y - bottomY;

            ground.AddQuad(bottom0, bottom1, top1, top0, normal,
                new Vector2(0, 0), new Vector2(width, 0), new Vector2(width, height), new Vector2(0, height));
        }

        /// <summary>
        /// Two short faces meeting at a chunk corner, dropped toward a
        /// DIAGONAL neighbour's pad. See the corner-post comment at the call
        /// site in <see cref="BuildGround"/> for why this exists alongside
        /// <see cref="AddSkirtIfLower"/> rather than being covered by it.
        /// </summary>
        private static void AddCornerSkirtIfLower(CityWorld world, int neighbourX, int neighbourY,
            Vector3 corner, Vector3 alongA, Vector3 normalA, Vector3 alongB, Vector3 normalB,
            Vector3 origin, MeshBuffer ground)
        {
            // corner.y and NeighbourGroundHeightAt, not two quantised pads --
            // see AddSkirtIfLower's own comment on the same substitution. A
            // diagonal neighbour can be a street chunk too (a cardinal
            // street-vs-street pair is excluded at the call site, but the
            // diagonal is not), so it needs the same real-height treatment.
            float neighbourHeight = NeighbourGroundHeightAt(
                world, neighbourX, neighbourY, corner.x + origin.x, corner.z + origin.z);
            if (neighbourHeight >= corner.y)
            {
                return;
            }

            float drop = corner.y - neighbourHeight + 0.5f;
            float bottomY = corner.y - Mathf.Min(drop, SkirtDepth);

            AddCornerFace(ground, corner, alongA, bottomY, normalA);
            AddCornerFace(ground, corner, alongB, bottomY, normalB);
        }

        private static void AddCornerFace(MeshBuffer ground, Vector3 top0, Vector3 top1, float bottomY,
            Vector3 normal)
        {
            Vector3 bottom0 = new Vector3(top0.x, bottomY, top0.z);
            Vector3 bottom1 = new Vector3(top1.x, bottomY, top1.z);
            float width = Vector3.Distance(top0, top1);
            float height = top0.y - bottomY;
            ground.AddQuad(bottom0, bottom1, top1, top0, normal,
                new Vector2(0, 0), new Vector2(width, 0), new Vector2(width, height), new Vector2(0, height));
        }

        /// <summary>
        /// Share of qualifying terrace edges that become a hillside ramp
        /// rather than the stairs-and-guardrail treatment -- EXPLICIT ASK:
        /// "make it like 90% slopes 10% hard cutoffs with fencing and
        /// stairs." Raised from an even 50/50 split, and no longer only
        /// rolled for street-vs-street drops -- see the roll's own call
        /// site in AddStreetEdgeFeatures for why widening eligibility
        /// mattered at least as much as this number did.
        /// </summary>
        public const double SlopedStreetChance = 0.9;

        /// <summary>
        /// A hillside street's steepest allowed grade, in degrees. Kept well
        /// above every rig's own CharacterController.slopeLimit (50 degrees
        /// -- Player/Civilian/OldGuy all agree, see PlayerRigBuilder.cs) so
        /// the ramp is never accidentally climbable no matter how the
        /// margin below is tuned later.
        /// </summary>
        public const float SlopedStreetMinAngleDeg = 58.0f;

        /// <summary>
        /// Steepest a hillside street is allowed to get -- purely a look
        /// bound, so "random levels of steepness" never rolls something
        /// that reads as a wall wearing a road texture. Widened along with
        /// the minimum -- EXPLICIT ASK: "varying levels of steepness and
        /// lengths to the curvature" -- since the ramp's own run length is
        /// derived from this angle (a shallower grade covers more ground
        /// for the same drop), a wider spread of angles is also a wider
        /// spread of how long a given ramp reads as.
        /// </summary>
        public const float SlopedStreetMaxAngleDeg = 85.0f;

        /// <summary>
        /// Grade of the short easing bevel at the very top and bottom of a
        /// hillside ramp -- EXPLICIT ASK: "there shouldnt be these hard
        /// cutoffs." Deliberately UNDER the CharacterController's own 50
        /// degree slopeLimit -- a player really can walk a little way onto
        /// one of these, which is what makes it read as a real road grade
        /// easing in rather than a wall with a lip. It is the STEEP middle
        /// grade (SlopedStreetMinAngleDeg and up) that still does all the
        /// actual work of making the whole ramp uncrossable -- these two
        /// small bevels are never more than SlopedStreetBevelMaxDrop of
        /// vertical rise each, nowhere near enough on their own to reach
        /// anywhere a bevel this shallow could matter.
        /// </summary>
        public const float SlopedStreetBevelMinAngleDeg = 15.0f;

        public const float SlopedStreetBevelMaxAngleDeg = 25.0f;

        /// <summary>
        /// Share of a ramp's total drop given to EACH bevel, before the
        /// absolute cap below -- small enough that even a two-terrace-step
        /// drop still spends most of itself on the genuinely steep middle
        /// section.
        /// </summary>
        public const float SlopedStreetBevelDropFraction = 0.15f;

        /// <summary>
        /// Absolute ceiling on one bevel's own vertical rise, regardless of
        /// how tall the terrace drop it sits on is -- the bevel is a lip,
        /// not a second ramp.
        /// </summary>
        public const float SlopedStreetBevelMaxDrop = 1.5f;

        /// <summary>
        /// How far BELOW the road surface a hillside ramp is kept, wherever it would otherwise rise
        /// to it. EXPLICIT BUG REPORT, with screenshots: "long sloped parts poking through some
        /// streets". The ramp is built off the street's height AT THE BORDER, but reaches back a few
        /// metres INTO the chunk -- and the road, tilting like a hill, is often lower there than at
        /// the border. So the ramp's upper grades stood up out of the road: wedges 0.3-1m tall and a
        /// whole chunk long, on some 5% of street chunks. The road quad covers the ramp's whole
        /// footprint, so a ramp that stays under it is invisible from above -- which is all it ever
        /// was where it was not poking -- and one that does not is the artifact. 0.6m covers the
        /// road's own sideways curve and the two surfaces being sampled a little differently (a
        /// 23 degree grade moves the road ~0.5m for a 1.2m sideways shift).
        /// </summary>
        public const float SlopedStreetRampBuriedMetres = 0.6f;

        /// <summary>
        /// Where a street keeps going but the road itself drops a terrace,
        /// this edge either gets a real staircase cut into whichever
        /// sidewalk exists, or -- EXPLICIT ASK: "make these cut off streets
        /// sloped like san francisco... at random levels of steepness but
        /// never so steep that you can walk or run up it. I still like the
        /// fences and stairs, make certain parts have those still" -- a
        /// hillside ramp instead, chosen at random per edge. Either way it
        /// runs a short guardrail the rest of the way across with a gap
        /// over the stairs, or none at all over a ramp -- a real hillside
        /// street has no fence blocking the road.
        ///
        /// ADDITIVE, NOT A REPLACEMENT. AddSkirtIfLower's own wall geometry
        /// is left exactly as it already was -- the stair's own solid steps,
        /// or the ramp's own sloped face, stand directly in front of the
        /// section of wall they cover and hide it completely. Simpler and
        /// safer than editing that function's geometry, and it means
        /// ATerraceEdgeAlwaysGetsARetainingWall's own "no gap" guarantee
        /// stays true of a function this code never touches.
        ///
        /// ON ANY CHUNK, NOT JUST A STREET ONE -- EXPLICIT ASK: "if there is
        /// a drop that would cause fall damage... make a fence around it and
        /// have a staircase." This used to bail immediately for a building
        /// chunk, so AddSkirtIfLower's bare retaining wall was the ONLY
        /// thing a building's own edge against a lower neighbour (another
        /// lot on a different terrace, or a lower street) ever got: a hard,
        /// unguarded drop with no fence and no way down, exactly the "hard
        /// corner that would cause injury" the ask called out. TopAt already
        /// makes top0/top1/slice0Centre/slice1Centre correct for either
        /// chunk type (flat for a building, tilted for a street), so nothing
        /// below this needed to change to make it work on both -- only the
        /// early bail did.
        ///
        /// STREET-VS-STREET STILL NEEDS NO WALL: two neighbouring streets'
        /// own independent continuous hillside tilt already meets seamlessly
        /// (see BuildGround's "hillside streets" comment), so BuildGround's
        /// own westToStreet/etc. gating still skips calling this for that
        /// one pair -- nothing here excludes it separately.
        /// </summary>
        private static void AddStreetEdgeFeatures(CityWorld world, int chunkX, int chunkY,
            int neighbourX, int neighbourY, Vector3 top0, Vector3 top1, Vector3 normal,
            Vector3 slice0Centre, Vector3 slice1Centre, Vector3 origin,
            MeshBuffer pavement, MeshBuffer guardRail, MeshBuffer ground, ref bool chunkAlreadySloped,
            bool isDoorFacingEdge)
        {
            // NeighbourGroundHeightAt, not a plain pad -- see its own doc
            // comment on AddSkirtIfLower. The NEIGHBOUR can now be a street
            // too (a building chunk's own edge reaches this function), whose
            // real ground is continuously tilted rather than flat, and
            // comparing against a quantised pad instead let a chunk and its
            // neighbour agree "same terrace, no drop" while their real,
            // rendered surfaces still sat apart by however much the tilt
            // had carried one of them -- the exact gap EXPLICIT BUG REPORT
            // screenshots showed.
            float neighbourHeight = NeighbourGroundHeightAt(
                world, neighbourX, neighbourY, top0.x + origin.x, top0.z + origin.z);
            if (neighbourHeight >= top0.y)
            {
                return;
            }

            // EXPLICIT ASK: "make it transitions from door to street more
            // smoothly lets do 60% have the terrain slope into the
            // entrance... the other 40%... hard edges and fencing and
            // staircases." isDoorFacingEdge is only ever true for the one
            // wall BuildBuilding actually puts a door on (max-Z, see its own
            // "the doorway, centred on the wall at maximum Z" comment) --
            // never for a plain STREET chunk's own north edge, which has no
            // door to approach at all, hence the CityLayout.IsStreetChunk
            // exclusion here rather than folding that check into the
            // parameter itself.
            //
            // NEIGHBOUR MUST ALSO BE A REAL STREET -- "door to STREET,"
            // literally. EXPLICIT BUG, found live: without this, a door
            // facing another LOT (CityChunkGenerator.PartyMargin, 0.4m,
            // rather than StreetMargin's 3.0m -- see LotRectFor's own
            // MarginTowards) still tried to carve a 3m-run ramp, which ran
            // straight through 2.6m of what should have been the
            // building's own solid lobby floor (caught by
            // ABasementIsARoomWithAHoleInTheLobbyAboveIt/
            // EveryStoreyGetsAFloorAndTheRoofGetsItsStairwell measuring
            // extra floor area at BaseHeight that was really this ramp).
            if (isDoorFacingEdge && !CityLayout.IsStreetChunk(chunkX, chunkY) &&
                CityLayout.IsStreetChunk(neighbourX, neighbourY))
            {
                BuildingSite site = world.BuildingAt(chunkX, chunkY);
                if (site.Exists &&
                    BuildingDecor.EntranceApproachIsSloped(world.ChunkSeed(site.AnchorX, site.AnchorY)))
                {
                    // MEASURED, NOT ASSUMED -- top0.z is this chunk's own
                    // boundary; site.MaxZ is the building's own real wall,
                    // wherever CityChunkGenerator.LotRectFor actually put it
                    // for THIS building (StreetMargin in the ordinary case
                    // this branch is now restricted to, but derived from the
                    // real footprint rather than assuming the constant, the
                    // same "measure it" reasoning AddSkirtIfLower's own
                    // NeighbourGroundHeightAt substitution already used).
                    float run = top0.z - (float)(site.MaxZ - origin.z);
                    if (run > 0.01f)
                    {
                        AddSlopedEntranceApproach(top0, top1, normal, neighbourHeight, run, pavement);
                        return;
                    }
                }
            }

            // Seeded off the NEIGHBOUR's own coordinate, not this chunk's --
            // a chunk with several terraced edges (several different
            // neighbours) then rolls each one independently, and it never
            // collides with the slice0/slice1 pick below, which is seeded
            // off this chunk instead.
            //
            // ANY LOWER NEIGHBOUR, NOT JUST ANOTHER STREET -- EXPLICIT ASK:
            // "there arent any sloped streets at all these are all hard
            // cutoffs... make it mostly sloped streets". Restricting this to
            // street-vs-street drops made the feature genuinely rare: most
            // of a street's own terraced edges border a BUILDING lot, not
            // another street, and those always kept the sheer wall no
            // matter what. The ramp itself never depended on what is on the
            // other side (see AddSlopedStreetRamp's own "below is just an
            // empty plot" reasoning).
            //
            // THIS FUNCTION IS NEVER EVEN CALLED FOR A STREET-VS-STREET EDGE
            // ANY MORE. BuildGround's own westToStreet/etc. gating routes
            // every such edge through the continuous hillside tilt instead
            // (see its "hillside streets" comment) -- two neighbouring
            // streets now always meet seamlessly, by construction, so there
            // is no drop left here for a stair to descend. The stair below
            // used to require a real sidewalk to exist first, checked
            // against the PERPENDICULAR neighbours (e.g. south/north for
            // this edge if it is the west one) -- back when this was reached
            // for a street-vs-street edge too, THIS edge's own sidewalk did
            // not exist yet (AddSidewalks builds none where the neighbour in
            // that same direction is itself a street), so the only sidewalk
            // actually reaching this corner was whichever perpendicular one
            // happened to. Checking the WRONG neighbour pair for that reason
            // made sense there -- but a STREET chunk reaching here always
            // has THIS edge's OWN sidewalk guaranteed (BuildGround only
            // calls this for a street's own edge when the neighbour on that
            // exact side is not another street, the same condition
            // AddSidewalks itself builds that sidewalk under), so the old
            // perpendicular check was left asking about the wrong sidewalk
            // entirely: one whose ABSENCE (an ordinary mid-block chunk with
            // plain buildings on its own north and south) does not mean
            // there is nowhere to carve a stair. That silently zeroed out
            // every stair in the game (caught by
            // AStreetTerraceDropWithASidewalkGetsARealStaircaseOrASlopedRamp
            // asserting stairCases > 0) -- slice0Centre/slice1Centre are
            // both always valid places to carve one now, so the flags this
            // function used to take alongside them are gone. A BUILDING
            // chunk reaching here has no sidewalk of its own at all (only
            // streets get one, see AddSidewalks) -- slice0Centre/
            // slice1Centre still land safely on its own flat pad either
            // way, 2m in from the raw edge, well inside the clearance
            // CityChunkGenerator.StreetMargin already keeps between a lot's
            // building footprint and its own chunk boundary.
            //
            // AT MOST ONE SLOPED EDGE PER CHUNK -- confirmed directly, live:
            // two adjacent edges (a corner chunk with a lower neighbour on
            // both the x and z side) can each roll sloped independently,
            // and their two ramps -- each inset from a DIFFERENT border,
            // both reaching back toward the shared corner -- physically
            // cut through each other there, reading as exactly the
            // "interlocking staircases that make no sense" bug report this
            // guards against. `chunkAlreadySloped` is shared across all
            // four of this chunk's own edge calls (see BuildGround), so
            // whichever edge is checked first wins the ramp and every
            // other edge falls back to its ordinary stairs/rail treatment
            // even if its own roll also said sloped.
            // STREET CHUNKS ONLY. AddSlopedStreetRamp builds new ground
            // spanning this chunk's FULL edge width and reaching back
            // toward this chunk's own interior (see its own "pulled IN
            // from the border... inside this chunk's own footprint" doc
            // comment) -- exactly right when "this chunk's own footprint"
            // is a street's own walkable surface, but wrong the moment
            // this function started being reached for a BUILDING chunk's
            // edge too (see this function's own "ON ANY CHUNK, NOT JUST A
            // STREET ONE" comment above): a building's ground already had
            // its own footprint cut OUT of it (BuildGround's own
            // AddSlabFace call), and a ramp built afterward fills part of
            // that hole straight back in with new sloped ground, directly
            // under where the building itself stands. A building's own
            // drop still gets the ramp's alternative -- stairs plus a
            // guardrail, below -- which correctly runs AWAY from this
            // chunk (in the normal direction, into the lower neighbour)
            // rather than back into it.
            if (!chunkAlreadySloped && CityLayout.IsStreetChunk(chunkX, chunkY))
            {
                var slopeRoll = new DeterministicRandom(world.ChunkSeed(neighbourX, neighbourY));
                if (ground != null && slopeRoll.NextDouble() < SlopedStreetChance)
                {
                    // The road's own surface at any point of this chunk, in this chunk's local space: the
                    // ramp is kept beneath it (see SlopedStreetRampBuriedMetres).
                    float RoadHeightAt(float localX, float localZ) =>
                        (float)world.Terrain.ContinuousHeightAt(localX + origin.x, localZ + origin.z) - origin.y;
                    AddSlopedStreetRamp(slopeRoll, neighbourHeight, top0, top1, normal, ground, RoadHeightAt);
                    chunkAlreadySloped = true;
                    return;
                }
            }

            Vector3 along = top1 - top0;
            float edgeLength = along.magnitude;
            if (edgeLength < 0.01f)
            {
                return;
            }
            Vector3 alongDir = along / edgeLength;

            bool hasStair = false;
            float stairFrom = 0.0f;
            float stairTo = 0.0f;

            {
                // Seeded rather than hard-coded to one side, so the same
                // edge's two flanks don't always put every stair on
                // identical sides city-wide -- UNLESS the preferred side
                // sits on a patch of tilted ground too close to (or below)
                // neighbourHeight to carve a sane descent into at all. Each
                // slice sits at an OPPOSITE end of this edge, so on the
                // bounded grade TerrainField.MaxSlope allows, they cannot
                // both be pathological at once -- the far end of a 24m
                // edge is never plunging as steeply as the exact corner is.
                // Falling back to the sane one instead of insisting on the
                // coin flip is what actually fixes a real flight rather
                // than papering over a bad one with a clamp.
                bool preferSlice0 = new DeterministicRandom(world.ChunkSeed(chunkX, chunkY)).Below(2) == 0;
                bool slice0Sane = slice0Centre.y - neighbourHeight >= BuildingInterior.StairStepHeight;
                bool slice1Sane = slice1Centre.y - neighbourHeight >= BuildingInterior.StairStepHeight;
                bool useSlice0 = slice0Sane && slice1Sane ? preferSlice0 : slice0Sane;
                Vector3 wallPoint = useSlice0 ? slice0Centre : slice1Centre;

                // wallPoint.y, not this chunk's own pad -- on a street chunk
                // this edge point sits on the tilted continuous surface (see
                // TopAt at the call site in BuildGround), which can differ
                // from the chunk's own quantised pad; on a building chunk it
                // is simply flat, same as the pad. Anchoring the rise to the
                // point the stair actually starts from either way is what
                // keeps floorY below landing exactly on the neighbour's real
                // height instead of floating or sinking by some difference.
                //
                // STILL CLAMPED, as a last resort. The slice picked above
                // is sane whenever either one is, but if BOTH ends of this
                // one edge happen to be pathological at once (a grade
                // steeper than the bound above actually allows for, or a
                // future change to that bound), this keeps the flight
                // pointed the right way rather than flipping upside down
                // (floorY ending up ABOVE wallPoint) -- the same "overshoot
                // rather than undershoot" idiom AddSkirtIfLower's own
                // +0.5f already uses for the identical reason.
                float rise = Mathf.Max(wallPoint.y - neighbourHeight, BuildingInterior.StairStepHeight);
                float run = BuildingInterior.StepsFor(rise) * StreetStairTreadDepth;
                float floorY = wallPoint.y - rise;

                // normal already points AWAY from this chunk, out over the
                // lower neighbour (see AddSkirtIfLower's own 4 call sites --
                // e.g. the x0 edge's neighbour is chunkX-1 and its normal is
                // Vector3.left, the same direction), which is exactly the
                // direction a flight has to run to climb down off the ledge.
                var well = new BuildingInterior.Stairwell(wallPoint, normal, alongDir, default);
                foreach (BuildingInterior.Step step in BuildingInterior.Steps(well, run, floorY, rise))
                {
                    Vector3 half = step.Size * 0.5f;
                    pavement.AddBox(step.Centre - half, step.Centre + half);
                }

                // BuildingInterior.Steps always builds BuildingInterior.StairWidth
                // wide (the same width every interior flight uses), centred on
                // wallPoint -- narrower than the 4m sidewalk it sits inside,
                // which is exactly why it fits with clearance either side.
                float centreOffset = Vector3.Dot(wallPoint - top0, alongDir);
                float halfWidth = BuildingInterior.StairWidth * 0.5f;
                stairFrom = centreOffset - halfWidth;
                stairTo = centreOffset + halfWidth;
                hasStair = true;
            }

            if (guardRail == null)
            {
                return;
            }
            if (hasStair)
            {
                AddGuardRailRun(top0, alongDir, 0.0f, Mathf.Max(stairFrom, 0.0f), guardRail);
                AddGuardRailRun(top0, alongDir, Mathf.Min(stairTo, edgeLength), edgeLength, guardRail);
            }
            else
            {
                AddGuardRailRun(top0, alongDir, 0.0f, edgeLength, guardRail);
            }
        }

        /// <summary>
        /// A building's own door-facing edge, reshaped into a walkable slope
        /// instead of AddStreetEdgeFeatures' own hard-edge stairs+guardrail
        /// path -- EXPLICIT ASK: "60% have the terrain slope into the
        /// entrance sometimes with some sort of staircase that could be
        /// built into the terrain if needed but if its flat enough then no
        /// staircase."
        ///
        /// SPANS THE FULL EDGE WIDTH, always, same "a whole frontage tilts"
        /// reasoning AddSlopedStreetRamp's own doc comment gives, not a
        /// narrow ramp tucked in one corner. NO GUARDRAIL: once the whole
        /// width is graded ground, nobody can fall off it -- same "a real
        /// hillside street has no fence" reasoning AddSlopedStreetRamp
        /// already established.
        ///
        /// RUN IS MEASURED, NOT ASSUMED -- passed in by the caller as the
        /// real distance between top0/top1 (this chunk's own boundary) and
        /// the building's own actual wall, wherever CityChunkGenerator.
        /// LotRectFor really put it for THIS building (StreetMargin in the
        /// ordinary case, but never hardcoded here -- see this function's
        /// own call site for why guessing that constant was a real bug).
        /// Never the 4m sidewalk beyond the boundary either way: the
        /// sidewalk on the far side of top0/top1 belongs to the NEIGHBOUR
        /// chunk, already independently built by the time this runs.
        /// Reaching past the boundary to use it would be building into
        /// ground this function does not own, the exact thing
        /// AddSlopedStreetRamp's own "pulled IN from the border... nothing
        /// hangs out over the neighbour's already-independently-built
        /// ground" comment already warns against.
        /// </summary>
        private static void AddSlopedEntranceApproach(Vector3 top0, Vector3 top1, Vector3 normal,
            float neighbourHeight, float run, MeshBuffer pavement)
        {
            Vector3 inward = -normal;
            Vector3 low0 = new Vector3(top0.x, neighbourHeight, top0.z);
            Vector3 low1 = new Vector3(top1.x, neighbourHeight, top1.z);
            Vector3 high0 = top0 + inward * run;
            Vector3 high1 = top1 + inward * run;

            // One continuous slope across the whole frontage, always --
            // present even where a staircase gets embedded into it below,
            // so the stair reads as CUT INTO a graded hillside rather than
            // standing free in front of a flat wall.
            AddSlopeSegment(pavement, high0, high1, low0, low1);

            float rise0 = top0.y - neighbourHeight;
            float rise1 = top1.y - neighbourHeight;
            float worstAngleDeg = Mathf.Max(
                Mathf.Atan2(Mathf.Max(rise0, 0.0f), run) * Mathf.Rad2Deg,
                Mathf.Atan2(Mathf.Max(rise1, 0.0f), run) * Mathf.Rad2Deg);
            if (worstAngleDeg <= EntranceRampMaxAngleDeg)
            {
                // Flat enough -- EXPLICIT ASK's own "if its flat enough then
                // no staircase." Nothing more to build.
                return;
            }

            // Too steep to read as an easy walk-up incline -- EXPLICIT ASK's
            // own "some sort of staircase that could be built into the
            // terrain if needed." A narrow flight, centred on the edge, the
            // exact same BuildingInterior.Steps machinery the hard-edge case
            // above uses -- but its OWN run is the real physical margin
            // available, not an unconstrained one derived from the rise the
            // way the hard-edge flight's is. That constraint is what makes
            // this read as a steep, narrow hillside stair cut into the
            // slope rather than a gentle civic stairway standing apart from
            // it.
            Vector3 along = top1 - top0;
            float edgeLength = along.magnitude;
            if (edgeLength < 0.01f)
            {
                return;
            }
            Vector3 alongDir = along / edgeLength;
            Vector3 centre = Vector3.Lerp(top0, top1, 0.5f);
            float rise = Mathf.Max(centre.y - neighbourHeight, BuildingInterior.StairStepHeight);
            var well = new BuildingInterior.Stairwell(centre, normal, alongDir, default);
            foreach (BuildingInterior.Step step in
                     BuildingInterior.Steps(well, run, neighbourHeight, rise))
            {
                Vector3 half = step.Size * 0.5f;
                pavement.AddBox(step.Centre - half, step.Centre + half);
            }
        }

        /// <summary>
        /// Whether this street chunk belongs to an elevated bridge span,
        /// and if so, the WORLD height its own slice of the deck sits at.
        /// See ElevatedBridgeHalfLengthChunks's own doc comment for the ask
        /// this answers.
        ///
        /// V1 SCOPE: north-south columns only (StreetRunsAlongX false) --
        /// half the possible orientations, kept simple for a first pass at
        /// a genuinely new mechanic with no existing precedent to build on
        /// (confirmed directly: nothing else in this codebase reasons about
        /// a street with open space underneath it).
        ///
        /// SEARCHES NEARBY FOR A CENTRE, rather than assuming one on a
        /// fixed grid -- EXPLICIT BUG, found live: an earlier version
        /// quantised the column into fixed, evenly-spaced buckets and
        /// tested only each bucket's own rigid midpoint as the candidate
        /// valley bottom. A real valley's actual lowest point almost never
        /// lands exactly on an arbitrary fixed grid line, and a sweep of
        /// roughly 10,000 candidate spans confirmed it: zero ever
        /// qualified. Instead, every chunk searches every possible centre
        /// within ElevatedBridgeHalfLengthChunks of itself and asks IsSpan
        /// Centre directly, so a chunk near a real dip actually finds it
        /// wherever it really sits.
        ///
        /// RETURNS THE HEIGHT AT EACH OF THIS CHUNK'S OWN TWO Z EDGES, not
        /// one flat height for the whole chunk -- EXPLICIT BUG REPORT, with
        /// screenshots: "gaps in it where it doesnt tilt properly and
        /// larger gap." The earlier version returned a single interpolated
        /// deckHeight per CHUNK INDEX, which made every chunk's own slice
        /// of the deck perfectly flat internally but land at a different
        /// flat height than its neighbour -- a visible vertical step at
        /// every chunk boundary along the span, exactly the "gaps" and
        /// "doesnt tilt properly" reported (the real street right below it
        /// has never had this problem, because AddTiltedStreetGround
        /// already samples each of ITS OWN corners at the real continuous
        /// height there -- two neighbouring chunks share a corner's exact
        /// world position, so they always agree; the deck needs the same
        /// treatment). Interpolating by the REAL WORLD Z position of each
        /// edge (via DeckHeightAtWorldZ) rather than by chunk index means
        /// two neighbouring chunks, sharing the exact same world Z at their
        /// common edge, always compute the identical height there -- no
        /// stitching needed, the same reason the real street below never
        /// needed any either.
        /// </summary>
        private static bool TryGetElevatedBridgeDeckHeight(CityWorld world, int chunkX, int chunkY,
            out float deckHeightAtZ0, out float deckHeightAtZ1)
        {
            deckHeightAtZ0 = 0.0f;
            deckHeightAtZ1 = 0.0f;
            if (!CityLayout.IsStreetChunk(chunkX, chunkY) || CityLayout.StreetRunsAlongX(chunkX, chunkY))
            {
                return false;
            }

            if (!TryFindBlockBridgeCentre(world, chunkX, chunkY, out int centreY))
            {
                return false;
            }

            const int half = ElevatedBridgeHalfLengthChunks;
            if (chunkY < centreY - half || chunkY > centreY + half)
            {
                return false;
            }

            double chunkSize = CityLayout.ChunkSize;
            double columnX = CityLayout.ChunkCentreX(chunkX);

            // THE DECK STARTS AND ENDS AT REAL GROUND LEVEL -- sampled at
            // the span's own true boundary edges, not the flanking chunks'
            // centres (IsElevatedBridgeSpanCentre still uses ITS OWN centre
            // samples for the separate "is this dip worth bridging at all"
            // decision, unrelated to this). EXPLICIT BUG REPORT, with a
            // screenshot: a visible gap in the deck. Evaluating the old
            // centre-to-centre line at each chunk's own EDGE meant the very
            // first and last sampled heights were a small EXTRAPOLATION past
            // where either endpoint was actually measured (real terrain over
            // that distance is not perfectly linear) -- sampling right at
            // the true edges instead makes the deck's own first and last
            // height provably equal to the real ground there (t=0/t=1
            // exactly), which is also what makes it read as a real on-ramp/
            // off-ramp rather than a structure that begins already
            // elevated with nothing connecting it down to the street.
            double beforeZ = (centreY - half) * chunkSize;
            double afterZ = (centreY + half + 1) * chunkSize;
            double heightBefore = world.Terrain.ContinuousHeightAt(columnX, beforeZ);
            double heightAfter = world.Terrain.ContinuousHeightAt(columnX, afterZ);

            double chunkZ0 = chunkY * chunkSize;
            double chunkZ1 = chunkZ0 + chunkSize;

            deckHeightAtZ0 = SafeDeckHeight(world, columnX, chunkZ0, beforeZ, afterZ, heightBefore, heightAfter);
            deckHeightAtZ1 = SafeDeckHeight(world, columnX, chunkZ1, beforeZ, afterZ, heightBefore, heightAfter);
            return true;
        }

        /// <summary>
        /// The one bridge centre (if any) that chunkY's own fixed block
        /// owns -- see ElevatedBridgeBlockChunks's own doc comment for why
        /// this has to be keyed on the block rather than on chunkY's own
        /// sliding window. Deterministic per block: always scans the same
        /// candidate range in the same ascending order, so every chunkY in
        /// the block gets the exact same answer.
        /// </summary>
        private static bool TryFindBlockBridgeCentre(CityWorld world, int chunkX, int chunkY,
            out int centreY)
        {
            const int half = ElevatedBridgeHalfLengthChunks;
            const int block = ElevatedBridgeBlockChunks;
            int blockStart = FloorDiv(chunkY, block) * block;

            for (int candidate = blockStart + half; candidate <= blockStart + block - 1 - half; candidate++)
            {
                if (IsElevatedBridgeSpanCentre(world, chunkX, candidate))
                {
                    centreY = candidate;
                    return true;
                }
            }

            centreY = 0;
            return false;
        }

        /// <summary>
        /// The deck's own height at one real world Z within the span: the
        /// straight-line interpolation between the two true span-boundary
        /// heights, but never lower than the REAL ground right below that
        /// same point plus ElevatedBridgeMinClearance -- see that constant's
        /// own doc comment for the crash-through-the-deck bug this closes.
        /// </summary>
        private static float SafeDeckHeight(CityWorld world, double columnX, double worldZ,
            double beforeZ, double afterZ, double heightBefore, double heightAfter)
        {
            double linear = DeckHeightAtWorldZ(worldZ, beforeZ, afterZ, heightBefore, heightAfter);
            double realGround = world.Terrain.ContinuousHeightAt(columnX, worldZ);
            return (float)System.Math.Max(linear, realGround + ElevatedBridgeMinClearance);
        }

        /// <summary>Floor division -- integer / rounds toward zero, not toward negative infinity, and chunkY can be negative.</summary>
        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if (a % b != 0 && (a < 0) != (b < 0))
            {
                q--;
            }
            return q;
        }

        /// <summary>
        /// The bridge deck's own height at an arbitrary real world Z within
        /// the span, by straight-line interpolation between the two real
        /// flanking heights -- see TryGetElevatedBridgeDeckHeight's own
        /// "RETURNS THE HEIGHT AT EACH OF THIS CHUNK'S OWN TWO Z EDGES" doc
        /// comment for why this is keyed on the real coordinate rather than
        /// a chunk index.
        /// </summary>
        private static float DeckHeightAtWorldZ(double worldZ, double beforeZ, double afterZ,
            double heightBefore, double heightAfter)
        {
            double t = (worldZ - beforeZ) / (afterZ - beforeZ);
            return (float)(heightBefore + (heightAfter - heightBefore) * t);
        }

        /// <summary>
        /// True if centreY, on this street column, is a genuine local
        /// valley bottom deep enough to bridge, AND rolls the (rare) chance
        /// to actually become one. "Local" means strictly lower than every
        /// OTHER point within ElevatedBridgeHalfLengthChunks either side --
        /// the thing that makes this a real, single, well-defined centre
        /// rather than several neighbouring chunks all claiming the same
        /// broad dip.
        ///
        /// ONLY THE TWO FLANKING ENDPOINTS ARE CHECKED, not every sample in
        /// between -- EXPLICIT BUG, found live: an earlier version required
        /// EVERY intermediate sample to also be higher than the centre (a
        /// true strict local minimum). TerrainField's own shorter octave
        /// has a 144m wavelength, close enough to this span's own width
        /// that its natural wiggle routinely dips back down somewhere
        /// between the centre and an endpoint even while the endpoints
        /// themselves are genuinely, meaningfully higher -- a real sweep of
        /// over 70,000 candidates found zero that passed the stricter
        /// version. What actually matters for "is there a bridge-worthy
        /// valley here" is only the NET drop from each real endpoint to the
        /// centre, not whether the ground between them is perfectly
        /// monotonic.
        /// </summary>
        private static bool IsElevatedBridgeSpanCentre(CityWorld world, int chunkX, int centreY)
        {
            const int half = ElevatedBridgeHalfLengthChunks;
            double columnX = CityLayout.ChunkCentreX(chunkX);
            double heightCentre = world.Terrain.ContinuousHeightAt(columnX, CityLayout.ChunkCentreZ(centreY));
            double heightBefore = world.Terrain.ContinuousHeightAt(columnX, CityLayout.ChunkCentreZ(centreY - half));
            double heightAfter = world.Terrain.ContinuousHeightAt(columnX, CityLayout.ChunkCentreZ(centreY + half));

            // A REAL valley, not an arbitrary override -- both flanking
            // ends have to sit meaningfully higher than the centre, or
            // there is nothing here worth bridging over (see
            // ElevatedBridgeMinDip's own doc comment).
            if (heightBefore - heightCentre < ElevatedBridgeMinDip ||
                heightAfter - heightCentre < ElevatedBridgeMinDip)
            {
                return false;
            }

            // Plain default-stream constructor, matching AddStreetEdge
            // Features' own identical "new DeterministicRandom(world.
            // ChunkSeed(...))" idiom for the sibling sloped-street roll --
            // no custom salt needed since nothing else in the codebase
            // seeds off this exact (column, candidate-centre) pair.
            var roll = new DeterministicRandom(world.ChunkSeed(chunkX, centreY));
            return roll.NextDouble() < ElevatedBridgeChance;
        }

        /// <summary>
        /// One chunk's own slice of the elevated deck -- a real TILTED quad
        /// (deckY0 at z0, deckY1 at z1, matching AddTiltedStreetGround's own
        /// per-edge sampling so neighbouring chunks always agree at their
        /// shared edge, no gap), plus a short guardrail down both long
        /// edges (this deck has open air on both sides, not more walkable
        /// ground the way a hillside ramp does, so unlike
        /// AddSlopedStreetRamp it always needs one).
        ///
        /// TWO QUADS, ONE EACH WAY -- EXPLICIT BUG REPORT: "also is
        /// invisible from the bottom." A single upward-facing quad is
        /// exactly what URP's default back-face culling makes invisible
        /// from underneath -- the same "back to back, so neither is ever
        /// seen from the wrong side" pattern AddWindowedWall's own inward
        /// pass already uses for windows, applied here instead of touching
        /// the shared ground material's own culling mode (which would make
        /// every OTHER street's underside render too, not just this deck).
        /// </summary>
        private static void AddElevatedBridgeDeck(float x0, float z0, float x1, float z1,
            float deckY0, float deckY1, MeshBuffer ground, MeshBuffer guardRail)
        {
            Vector3 corner00 = new Vector3(x0, deckY0, z0);
            Vector3 corner10 = new Vector3(x1, deckY0, z0);
            Vector3 corner11 = new Vector3(x1, deckY1, z1);
            Vector3 corner01 = new Vector3(x0, deckY1, z1);

            Vector3 normal = Vector3.Cross(corner10 - corner00, corner01 - corner00).normalized;
            if (Vector3.Dot(normal, Vector3.up) < 0.0f)
            {
                normal = -normal;
            }

            ground.AddQuad(corner00, corner10, corner11, corner01, normal,
                new Vector2(0.0f, 0.0f), new Vector2(x1 - x0, 0.0f),
                new Vector2(x1 - x0, z1 - z0), new Vector2(0.0f, z1 - z0));

            // The underside -- same four corners, reversed winding and
            // normal.
            ground.AddQuad(corner01, corner11, corner10, corner00, -normal,
                new Vector2(0.0f, z1 - z0), new Vector2(x1 - x0, z1 - z0),
                new Vector2(x1 - x0, 0.0f), new Vector2(0.0f, 0.0f));

            // Following the REAL tilted edge (corner01-corner00), not a
            // flat Vector3.forward -- the earlier flat-deck version could
            // get away with an assumed horizontal direction; a genuinely
            // tilted deck needs its own two edges' real direction and
            // length, or the posts land at the wrong height relative to
            // the rail they are supposed to hold up.
            Vector3 leftEdge = corner01 - corner00;
            Vector3 rightEdge = corner11 - corner10;
            AddGuardRailRun(corner00, leftEdge.normalized, 0.0f, leftEdge.magnitude, guardRail);
            AddGuardRailRun(corner10, rightEdge.normalized, 0.0f, rightEdge.magnitude, guardRail);
        }

        /// <summary>
        /// A single support column under the centre of this chunk's own
        /// slice of the deck, reaching all the way down to the REAL ground
        /// already built there (the same bilinear patch the ordinary tilted
        /// street surface itself uses) -- not a resampled or assumed
        /// height, so the pillar never floats above or buries into the
        /// genuine lower street it stands on.
        /// </summary>
        private static void AddElevatedBridgePillar(float centreX, float centreZ, float topY, float bottomY,
            MeshBuffer ground)
        {
            float half = ElevatedBridgePillarSize * 0.5f;
            ground.AddBox(
                new Vector3(centreX - half, bottomY, centreZ - half),
                new Vector3(centreX + half, topY, centreZ + half));
        }

        /// <summary>
        /// One lane's own fixed phase for the highway curve -- same "seeded
        /// once per lane, off that lane's own chunk-seed" idiom
        /// StreetCurvePhaseFor already establishes, on an independent draw
        /// stream (HighwayCurveStream) so the two features' phases never
        /// collide even though both key off the same lane coordinate.
        /// </summary>
        private static double HighwayCurvePhaseFor(CityWorld world, int laneKey)
        {
            var roll = new DeterministicRandom(
                unchecked((ulong)(long)world.ChunkSeed(laneKey, 0)), HighwayCurveStream);
            return roll.NextDouble() * HighwayWavelengthMetres;
        }

        /// <summary>The highway's own real, deliberate lateral sweep -- a sine of world position, same shape as StreetCurveOffsetAt, different amplitude/wavelength.</summary>
        private static double HighwayLateralOffsetAt(double worldPosAlong, double phase)
        {
            double t = (worldPosAlong + phase) / HighwayWavelengthMetres * (2.0 * System.Math.PI);
            return HighwayAmplitudeMetres * System.Math.Sin(t);
        }

        /// <summary>
        /// One chunk's own slice of a highway: a permanently elevated,
        /// genuinely curving and sloping deck (see this file's own "highway
        /// system" constants block), built as a ribbon of short segments the
        /// same "many short pieces along the travel axis" way
        /// AddCurvingStreetGround already is, plus one support pillar.
        /// Generalised to BOTH travel axes from the start (unlike the
        /// dip-triggered bridge, which stayed north-south-only as an
        /// explicit V1 scope decision) -- the same runsAlongX-mapping idiom
        /// AddCurvingSidewalk/AddCurvingCentreLine already established this
        /// session, reused here for the same reason: a highway is exactly as
        /// likely to run east-west as north-south.
        ///
        /// EASES DOWN TO REAL GROUND AT A REAL EXIT (CityLayout.
        /// HighwayClearanceTaperAt) -- the deck's own clearance above the
        /// real terrain is multiplied by that taper at every sampled point,
        /// so approaching an exit chunk the whole deck genuinely descends
        /// and merges into the street grid below rather than simply
        /// stopping in mid-air.
        /// </summary>
        private static void AddHighwayDeck(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            float x0, float z0, float x1, float z1, MeshBuffer ground, MeshBuffer guardRail)
        {
            // NEVER CityLayout.StreetRunsAlongX -- EXPLICIT BUG REPORT: "make
            // it so the highway cant have gaps in it." That method decides
            // orientation purely from chunkY's own ordinary-grid row pattern
            // (a district-boundary row, or a district's own local street
            // period), with no idea a highway line even exists. A highway
            // chunk that ALSO happens to sit on one of those horizontal
            // arterial rows (i.e. a junction where the highway crosses an
            // ordinary cross-street) got `runsAlongX == true` from that
            // heuristic even though the highway itself, since IsHighwayChunk
            // fires on a single fixed X value for its ENTIRE line, always
            // runs along Y -- so that one chunk's deck segment built
            // perpendicular to every neighbour on either side of it, reading
            // as a real hole in an otherwise continuous elevated structure
            // (confirmed directly: every junction row along a real line
            // logged runsAlongX=true, every non-junction row logged false).
            // A highway's own orientation is a structural fact of the
            // layout, not a per-chunk street-pattern guess -- hardcode it.
            const bool runsAlongX = false;
            int laneKey = runsAlongX ? chunkY : chunkX;
            double phase = HighwayCurvePhaseFor(world, laneKey);
            float size = CityLayout.ChunkSize;

            double worldAlongStart = runsAlongX ? chunkX * (double)size : chunkY * (double)size;
            double worldAcrossCentre = (runsAlongX ? chunkY : chunkX) * (double)size + size * 0.5;

            int segments = Mathf.Max(1, Mathf.RoundToInt(size / StreetCurveSegmentLength));
            double segLen = size / segments;

            Vector3 DeckCornerAt(double worldAlong, float acrossSign)
            {
                double sweep = HighwayLateralOffsetAt(worldAlong, phase);
                double worldAcross = worldAcrossCentre + sweep + acrossSign * HighwayDeckHalfWidth;
                double wx = runsAlongX ? worldAlong : worldAcross;
                double wz = runsAlongX ? worldAcross : worldAlong;
                float clearanceTaper = CityLayout.HighwayClearanceTaperAt(
                    laneKey, worldAlong, HighwayMinimumClearanceFraction);
                // MAX of smoothed and real, not smoothed alone -- EXPLICIT BUG
                // REPORT, WITH SCREENSHOT: "the highway is going into the
                // street underground make that impossible." A 900m-wide
                // average has no lower bound relative to the REAL ground at
                // any one point -- over a local rise the smoothed reference
                // can sit well below the actual terrain there, which used to
                // let deckHeight compute to something below the real street
                // surface it was supposed to be flying over. Taking the max
                // keeps every one of this file's own already-hard-won
                // guarantees (see CityLayout.HighwayClearanceTaperAt's own
                // "no true touchdown anywhere, ever" fix, and this comment's
                // git history above it) intact -- the deck is now provably at
                // least `clearanceTaper * HighwayClearance` above the REAL
                // local ground at every single point, exactly as before
                // smoothing existed. The smoothing still does its job over an
                // ordinary dip (where the smoothed trend sits ABOVE the local
                // low point, so the max picks smoothed and the deck sails
                // over it on a gentle grade) -- it only stops mattering
                // exactly where mattering would be unsafe: a local rise.
                double groundHeight = System.Math.Max(
                    world.Terrain.SmoothedHeightAt(wx, wz, runsAlongX,
                        HighwayVerticalSmoothingWindowMetres, HighwayVerticalSmoothingSamples),
                    world.Terrain.ContinuousHeightAt(wx, wz));
                double deckHeight = groundHeight + HighwayClearance * clearanceTaper;
                return new Vector3((float)(wx - origin.x), (float)(deckHeight - origin.y), (float)(wz - origin.z));
            }

            for (int i = 0; i < segments; i++)
            {
                double a0 = worldAlongStart + i * segLen;
                double a1 = worldAlongStart + (i + 1) * segLen;

                Vector3 near0 = DeckCornerAt(a0, -1.0f);
                Vector3 near1 = DeckCornerAt(a1, -1.0f);
                Vector3 far1 = DeckCornerAt(a1, 1.0f);
                Vector3 far0 = DeckCornerAt(a0, 1.0f);

                AddHighwayDeckSegment(near0, near1, far1, far0, ground, guardRail);
            }

            // One pillar per chunk, at its own along-centre -- but pinned to
            // the FIXED, unswept cross-axis centreline (worldAcrossCentre,
            // no sweep term), not wherever the deck's own lateral sweep
            // happens to have moved it. EXPLICIT BUG REPORT: "make it so
            // the support beams dont block the lanes on the street." The
            // street below has no real median -- AddRoadMarkings paints its
            // double-yellow centreline right at this same fixed centre,
            // with a driving lane starting almost immediately on either
            // side of it -- so a pillar that followed the sweep could land
            // squarely inside a travel lane depending on where along the
            // 420m wavelength this particular chunk happened to fall.
            // Fixing it to the true centreline instead means it always
            // lands in the same place a real centre-median column would,
            // directly under the double-yellow line, never inside a lane.
            // Still genuinely supports the deck: HighwayAmplitudeMetres's
            // own reduced cap keeps the sweep well inside HighwayDeckHalfWidth,
            // so the fixed centreline point is always within the swept
            // deck's own footprint even at the peak of a curve.
            double pillarAlong = worldAlongStart + size * 0.5;
            double pillarWx = runsAlongX ? pillarAlong : worldAcrossCentre;
            double pillarWz = runsAlongX ? worldAcrossCentre : pillarAlong;
            float clearanceTaper = CityLayout.HighwayClearanceTaperAt(
                laneKey, pillarAlong, HighwayMinimumClearanceFraction);
            // Deck-height term uses the SAME max(smoothed, real) profile the
            // deck surface itself is built from (see DeckCornerAt's own
            // doc comment for why plain smoothing alone let the deck sink
            // below real ground), so the pillar's own top lands exactly on
            // the deck above it. The pillar's BASE (realGroundHeight, below)
            // stays the real, unsmoothed ground -- it is a physical support
            // planted into the actual terrain, not a point on the travelled
            // surface, so it must not float or clip through it.
            double realGroundHeight = world.Terrain.ContinuousHeightAt(pillarWx, pillarWz);
            double deckGroundHeight = System.Math.Max(
                world.Terrain.SmoothedHeightAt(pillarWx, pillarWz, runsAlongX,
                    HighwayVerticalSmoothingWindowMetres, HighwayVerticalSmoothingSamples),
                realGroundHeight);
            double deckHeightAtPillar = deckGroundHeight + HighwayClearance * clearanceTaper;
            float pillarCentreX = (float)(pillarWx - origin.x);
            float pillarCentreZ = (float)(pillarWz - origin.z);
            float pillarTopY = (float)(deckHeightAtPillar - origin.y);
            float realGroundY = (float)(realGroundHeight - origin.y);
            AddElevatedBridgePillar(pillarCentreX, pillarCentreZ, pillarTopY, realGroundY, ground);
        }

        /// <summary>
        /// One highway deck segment from four explicit corners (the same
        /// 00/10/11/01 loop order AddTiltedStreetGround uses): a real tilted
        /// quad, visible from both above and below (AddElevatedBridgeDeck's
        /// own "two quads, one each way" fix for the exact same problem),
        /// plus a short guard-rail run along each of its own two long edges.
        /// </summary>
        private static void AddHighwayDeckSegment(Vector3 near0, Vector3 near1, Vector3 far1, Vector3 far0,
            MeshBuffer ground, MeshBuffer guardRail)
        {
            Vector3 normal = Vector3.Cross(near1 - near0, far0 - near0).normalized;
            if (Vector3.Dot(normal, Vector3.up) < 0.0f)
            {
                normal = -normal;
            }

            float width = Vector3.Distance(near0, near1);
            float depth = Vector3.Distance(near0, far0);
            ground.AddQuad(near0, near1, far1, far0, normal,
                new Vector2(0.0f, 0.0f), new Vector2(width, 0.0f),
                new Vector2(width, depth), new Vector2(0.0f, depth));
            ground.AddQuad(far0, far1, near1, near0, -normal,
                new Vector2(0.0f, depth), new Vector2(width, depth),
                new Vector2(width, 0.0f), new Vector2(0.0f, 0.0f));

            AddGuardRailRun(near0, (near1 - near0).normalized, 0.0f, Vector3.Distance(near0, near1), guardRail);
            AddGuardRailRun(far0, (far1 - far0).normalized, 0.0f, Vector3.Distance(far0, far1), guardRail);
        }

        /// <summary>
        /// A hillside street covering AddSkirtIfLower's wall along the whole
        /// edge, tilting down from this chunk's own street height to
        /// roughly the lower neighbour's. THREE GRADES, NOT ONE -- a short
        /// easing bevel, the real random-steepness grade that does the
        /// actual work of making the drop uncrossable (see
        /// SlopedStreetMinAngleDeg), then a matching bevel back out at the
        /// bottom. EXPLICIT ASK: "there shouldnt be these hard cutoffs" --
        /// one quad running straight from dead flat into a 60-80 degree
        /// grade is a single sharp crease, which is what read as a cutoff.
        ///
        /// SPANS THE FULL EDGE WIDTH, not a narrow sidewalk slice the way a
        /// stair does -- the reference is a whole street tilting, the way a
        /// San Francisco block does, not a ramp tucked in one corner.
        ///
        /// WHERE IT SITS: pulled IN from the border by the combined run of
        /// all three grades, ending flush with the border at the lower
        /// height. That keeps the whole ramp inside this chunk's own
        /// footprint -- nothing hangs out over the neighbour's already-
        /// independently-built ground, so there is nothing to line up with
        /// over there. Below the ramp, whatever the neighbour chunk
        /// generates on its own is left completely alone: "make it so below
        /// is just an empty plot we can work on later."
        /// </summary>
        private static void AddSlopedStreetRamp(DeterministicRandom rng, float neighbourHeight,
            Vector3 top0, Vector3 top1, Vector3 normal, MeshBuffer ground,
            System.Func<float, float, float> roadHeightAt = null)
        {
            // top0.y, not pad -- see AddSkirtIfLower's own comment on the
            // same substitution. The ramp starts flush with top0 either
            // way; what matters here is sizing its total drop off where it
            // actually starts, not the chunk's flat quantised approximation
            // of that, so it reliably reaches back down near neighbourHeight.
            float drop = Mathf.Min(top0.y - neighbourHeight + 0.5f, SkirtDepth);

            // EXPLICIT ASK: "there shouldnt be these hard cutoffs" -- a
            // single quad from dead flat straight into a 60-80 degree grade
            // is one sharp crease, which is exactly what read as a cutoff
            // rather than a hillside. A short, GENTLE, still-randomised
            // bevel at each end -- capped small so it can never itself carry
            // a player anywhere near the top -- breaks that one crease into
            // two softer ones, the way a real road's own vertical curve
            // eases into and out of a steep grade instead of kinking.
            float topBevelDrop = Mathf.Min(SlopedStreetBevelMaxDrop, drop * SlopedStreetBevelDropFraction);
            float bottomBevelDrop = Mathf.Min(SlopedStreetBevelMaxDrop, drop * SlopedStreetBevelDropFraction);
            float steepDrop = drop - topBevelDrop - bottomBevelDrop;

            float topBevelAngleDeg = (float)rng.Range(SlopedStreetBevelMinAngleDeg, SlopedStreetBevelMaxAngleDeg);
            float steepAngleDeg = (float)rng.Range(SlopedStreetMinAngleDeg, SlopedStreetMaxAngleDeg);
            float bottomBevelAngleDeg = (float)rng.Range(SlopedStreetBevelMinAngleDeg, SlopedStreetBevelMaxAngleDeg);

            float topBevelRun = topBevelDrop / Mathf.Tan(topBevelAngleDeg * Mathf.Deg2Rad);
            float steepRun = steepDrop / Mathf.Tan(steepAngleDeg * Mathf.Deg2Rad);
            float bottomBevelRun = bottomBevelDrop / Mathf.Tan(bottomBevelAngleDeg * Mathf.Deg2Rad);

            Vector3 inward = -normal;
            Vector3 PointAt(Vector3 edge, float insetDistance, float dropAmount)
            {
                Vector3 point = edge + inward * insetDistance;
                point.y -= dropAmount;
                if (roadHeightAt != null)
                {
                    // Never above the road at this spot. Only ever LOWERS a point, and every grade
                    // shares its corner points, so the three stay joined.
                    point.y = Mathf.Min(point.y, roadHeightAt(point.x, point.z) - SlopedStreetRampBuriedMetres);
                }
                return point;
            }

            float fullRun = topBevelRun + steepRun + bottomBevelRun;
            Vector3 top0High = PointAt(top0, fullRun, 0.0f);
            Vector3 top1High = PointAt(top1, fullRun, 0.0f);
            Vector3 top0UpperMid = PointAt(top0, steepRun + bottomBevelRun, topBevelDrop);
            Vector3 top1UpperMid = PointAt(top1, steepRun + bottomBevelRun, topBevelDrop);
            Vector3 top0LowerMid = PointAt(top0, bottomBevelRun, topBevelDrop + steepDrop);
            Vector3 top1LowerMid = PointAt(top1, bottomBevelRun, topBevelDrop + steepDrop);
            Vector3 top0Low = PointAt(top0, 0.0f, drop);
            Vector3 top1Low = PointAt(top1, 0.0f, drop);

            AddSlopeSegment(ground, top0High, top1High, top0UpperMid, top1UpperMid);
            AddSlopeSegment(ground, top0UpperMid, top1UpperMid, top0LowerMid, top1LowerMid);
            AddSlopeSegment(ground, top0LowerMid, top1LowerMid, top0Low, top1Low);
        }

        /// <summary>
        /// One sloped quad from the upper edge (top0,top1) down to the lower
        /// edge (bottom0,bottom1) -- shared by all three grades a hillside
        /// street ramp is built from (see AddSlopedStreetRamp).
        /// </summary>
        private static void AddSlopeSegment(MeshBuffer ground,
            Vector3 top0, Vector3 top1, Vector3 bottom0, Vector3 bottom1)
        {
            Vector3 slopeNormal = Vector3.Cross(bottom1 - bottom0, top0 - bottom0).normalized;
            if (Vector3.Dot(slopeNormal, Vector3.up) < 0.0f)
            {
                // Semantic correction only -- a slope's surface should shade
                // as pointing up-and-outward regardless of which way the
                // raw cross product happened to land. AddQuad itself now
                // winds each of its two triangles to independently match
                // whatever normal is passed in (see its own doc comment), so
                // nothing here needs to also reorder bottom0/bottom1/top0/
                // top1 to match -- that used to be necessary when AddQuad
                // only checked and fixed its FIRST triangle and assumed the
                // second would follow, which a skewed quad like a bevel
                // segment on a tilted street edge does not reliably do.
                slopeNormal = -slopeNormal;
            }

            float width = Vector3.Distance(bottom0, bottom1);
            float length = Vector3.Distance(bottom0, top0);
            ground.AddQuad(bottom0, bottom1, top1, top0, slopeNormal,
                new Vector2(0, 0), new Vector2(width, 0), new Vector2(width, length), new Vector2(0, length));
        }

        /// <summary>
        /// One of several real treatments for a junction's own closed arm
        /// (see CityLayout.ClosedArmTreatment's own doc comment for the ask
        /// this answers), `from` one side of that arm's mouth `to` the
        /// other, if CityLayout.IsClosedJunctionArm says this particular
        /// (chunkX, chunkY, dx, dy) arm is one of the (usually zero,
        /// occasionally one) closed ones. A no-op for an ordinary open arm,
        /// which is most of them.
        /// </summary>
        private static void AddClosedArmTreatment(int chunkX, int chunkY, int dx, int dy,
            Vector3 from, Vector3 to, MeshBuffer pavement, MeshBuffer ground, MeshBuffer guardRail)
        {
            if (!CityLayout.IsClosedJunctionArm(chunkX, chunkY, dx, dy))
            {
                return;
            }

            Vector3 inward = new Vector3(-dx, 0.0f, -dy).normalized;
            switch (CityLayout.TreatmentForClosedArm(chunkX, chunkY))
            {
                case CityLayout.ClosedArmTreatment.Plaza:
                    AddClosedArmPlaza(from, to, inward, pavement);
                    break;
                case CityLayout.ClosedArmTreatment.RoundedCurb:
                    AddClosedArmRoundedCurb(from, to, inward, guardRail);
                    break;
                case CityLayout.ClosedArmTreatment.Skatepark:
                    AddClosedArmSkatepark(from, to, inward, pavement, ground);
                    break;
                default:
                    AddGuardRailRun(from, (to - from).normalized, 0.0f, Vector3.Distance(from, to), guardRail);
                    break;
            }
        }

        /// <summary>
        /// A raised, walkable paved plaza filling the arm's own mouth --
        /// reuses AddTiltedSidewalkQuad (the same tilted-top-plus-kerb-walls
        /// shape a curving sidewalk already builds) for a small pedestrian
        /// pad instead of a bare rail, extending ClosedArmPlazaDepth into
        /// the junction's own footprint (see that constant's own doc
        /// comment for why this can never reach the neighbouring chunk
        /// beyond the closed arm).
        /// </summary>
        private static void AddClosedArmPlaza(Vector3 from, Vector3 to, Vector3 inward, MeshBuffer pavement)
        {
            Vector3 innerFrom = from + inward * ClosedArmPlazaDepth;
            Vector3 innerTo = to + inward * ClosedArmPlazaDepth;
            AddTiltedSidewalkQuad(pavement, from, to, innerTo, innerFrom);
        }

        /// <summary>
        /// A curved rail bulging INTO the arm's own mouth (not straight
        /// across it, like Barrier) -- several short AddGuardRailRun
        /// segments along a quadratic-Bezier arc from `from` through an
        /// apex to `to`, reading as a real cul-de-sac a car could turn
        /// around in rather than a flat dead stop.
        /// </summary>
        private static void AddClosedArmRoundedCurb(Vector3 from, Vector3 to, Vector3 inward, MeshBuffer guardRail)
        {
            Vector3 mid = (from + to) * 0.5f;
            float mouthWidth = Vector3.Distance(from, to);
            Vector3 apex = mid + inward * (mouthWidth * ClosedArmCurbBulgeFactor);

            Vector3 previous = from;
            for (int i = 1; i <= ClosedArmCurbSegments; i++)
            {
                float t = (float)i / ClosedArmCurbSegments;
                Vector3 point = Vector3.Lerp(Vector3.Lerp(from, apex, t), Vector3.Lerp(apex, to, t), t);
                AddGuardRailRun(previous, (point - previous).normalized, 0.0f,
                    Vector3.Distance(previous, point), guardRail);
                previous = point;
            }
        }

        /// <summary>
        /// Rare (CityLayout.SkateparkChance): a small hidden skatepark --
        /// the same raised plaza pad as AddClosedArmPlaza, plus one sloped
        /// ramp (a single tilted quad rising from the pad's own surface)
        /// and a low grind ledge sitting on it.
        /// </summary>
        private static void AddClosedArmSkatepark(Vector3 from, Vector3 to, Vector3 inward,
            MeshBuffer pavement, MeshBuffer ground)
        {
            Vector3 innerFrom = from + inward * ClosedArmPlazaDepth;
            Vector3 innerTo = to + inward * ClosedArmPlazaDepth;
            AddTiltedSidewalkQuad(pavement, from, to, innerTo, innerFrom);

            Vector3 along = (to - from).normalized;
            Vector3 padCentre = (from + to + innerFrom + innerTo) * 0.25f + Vector3.up * KerbHeight;

            // The ramp, rising toward the pad's own inward side.
            Vector3 rampNear0 = padCentre - along * (SkateparkRampWidth * 0.5f);
            Vector3 rampNear1 = padCentre + along * (SkateparkRampWidth * 0.5f);
            Vector3 rampFar0 = rampNear0 + inward * SkateparkRampRun + Vector3.up * SkateparkRampHeight;
            Vector3 rampFar1 = rampNear1 + inward * SkateparkRampRun + Vector3.up * SkateparkRampHeight;
            AddTiltedStreetGround(ground, rampNear0, rampNear1, rampFar1, rampFar0);

            // A low grind ledge off to one side -- a simple raised box, not
            // a rail on posts, so it needs no per-orientation long axis the
            // way a thin box aligned to an arbitrary cardinal direction
            // would.
            Vector3 ledgeCentre = padCentre - along * (ClosedArmPlazaDepth * 0.6f)
                + inward * (ClosedArmPlazaDepth * 0.5f);
            Vector3 ledgeHalf = new Vector3(
                ClosedArmPlazaDepth * 0.3f, SkateparkLedgeHeight * 0.5f, ClosedArmPlazaDepth * 0.3f);
            Vector3 ledgeBase = new Vector3(ledgeCentre.x, ledgeCentre.y + SkateparkLedgeHeight * 0.5f, ledgeCentre.z);
            ground.AddBox(ledgeBase - ledgeHalf, ledgeBase + ledgeHalf);
        }

        /// <summary>
        /// One straight run of thin posts plus a top rail, `from` to `to`
        /// metres along `alongDir` starting at `origin`. `alongDir` is a
        /// unit vector along the edge itself (origin to the OTHER end top0/
        /// top1 already passed in by the caller) -- on a street chunk that
        /// edge is tilted, so alongDir already carries whatever vertical
        /// component the slope needs. Every post and the rail itself follow
        /// `origin + alongDir * distance` for their height too, not a flat
        /// constant, which is what keeps the fence sitting on the actual
        /// ground instead of floating above or burying into a graded edge.
        /// </summary>
        private static void AddGuardRailRun(Vector3 origin, Vector3 alongDir, float from, float to,
            MeshBuffer guardRail)
        {
            float length = to - from;
            if (length <= 0.01f)
            {
                return;
            }

            // Posts at both ends as well as along the middle, so a run always
            // reads as a bounded fence rather than trailing off mid-air.
            int postCount = Mathf.Max(2, Mathf.RoundToInt(length / GuardRailPostSpacing) + 1);
            Vector3 postHalf = new Vector3(GuardRailPostSize, GuardRailHeight, GuardRailPostSize) * 0.5f;
            for (int i = 0; i < postCount; i++)
            {
                float t = postCount == 1 ? 0.0f : (float)i / (postCount - 1);
                Vector3 baseAt = origin + alongDir * (from + length * t);
                Vector3 centre = new Vector3(baseAt.x, baseAt.y + GuardRailHeight * 0.5f, baseAt.z);
                guardRail.AddBox(centre - postHalf, centre + postHalf);
            }

            // The rail. Along-axis overshoot of half a rail-width at each end
            // (from the uniform inset below) is a couple of centimetres,
            // there to stop the rail's own corner z-fighting against the end
            // post rather than being trimmed exactly to length.
            Vector3 edgeAt0 = origin + alongDir * from;
            Vector3 edgeAt1 = origin + alongDir * to;
            Vector3 p0 = new Vector3(edgeAt0.x, edgeAt0.y + GuardRailHeight, edgeAt0.z);
            Vector3 p1 = new Vector3(edgeAt1.x, edgeAt1.y + GuardRailHeight, edgeAt1.z);
            Vector3 railHalf = new Vector3(GuardRailRailSize, GuardRailRailSize, GuardRailRailSize) * 0.5f;
            guardRail.AddBox(Vector3.Min(p0, p1) - railHalf, Vector3.Max(p0, p1) + railHalf);
        }

        /// <summary>
        /// One building: a facade you can see from the street and a stack of
        /// floors you can walk up inside it.
        ///
        /// NOT A SOLID BOX ANY MORE. It used to be one AddBox, which is all a
        /// skyline needs -- but city_chunk.gd's buildings are enterable, with
        /// a door, floor slabs, a stairwell hole through each slab and a
        /// flight climbing to it, and the roof cap is holed so the top flight
        /// actually gets you out onto the roof. This is that, ported.
        ///
        /// WHAT GOES IN WHICH BUFFER. Only the outward facade goes to
        /// structures, because structures is the buffer wearing the window
        /// material. Everything inside -- interior walls, slabs, steps --
        /// goes to the concrete buffer, which is the closest two buffers get
        /// to city_chunk.gd's own separate INTERIOR_COLOR/INTERIOR_FLOOR_COLOR
        /// materials. Printing windows down the inside of a stairwell would
        /// be the same mistake as printing them across a roof.
        ///
        /// COLLISION COMES FREE AND IS NOT THE SAME AS GODOT'S. The chunk gets
        /// one MeshCollider over exactly these triangles, so there is no
        /// separate collision pass to keep in step -- which is the whole
        /// reason city_chunk.gd builds its interior wall by wall. It does mean
        /// the steps are collided individually rather than as Godot's single
        /// sloped ramp box; at a 0.25m riser against the controller's 0.4m
        /// step offset that walks, and it is a truer surface than the ramp.
        /// </summary>
        /// <summary>
        /// Where a building's windows go, and what has already been done to
        /// them.
        ///
        /// Bundled because these four always travel together and every wall
        /// needs all of them -- four more parameters on each of eight calls
        /// says nothing the name does not. An instance with no buffers is the
        /// INWARD copy of a wall: same openings, no glass, because the outward
        /// pass already hung it.
        /// </summary>
        private readonly struct Glazing
        {
            public readonly MeshBuffer Lit;
            public readonly MeshBuffer Dark;
            public readonly MeshBuffer Cracked;
            public readonly GlassDamage Damage;
            public readonly List<CrackedOpening> Openings;

            /// <summary>
            /// The real crack-texture decal quad a known impact point gets,
            /// on top of the base Cracked glass -- see AddCrackDecal. Optional
            /// (defaults null) so every existing caller that only cares about
            /// the state machine, not the visual, keeps compiling unchanged.
            /// </summary>
            public readonly MeshBuffer Decals;

            /// <summary>Fraction of glazed cells with a light on -- the building's zone decides it (WindowGrid.LitShareFor).</summary>
            public readonly double LitShare;

            public Glazing(MeshBuffer lit, MeshBuffer dark, MeshBuffer cracked, GlassDamage damage,
                List<CrackedOpening> openings, MeshBuffer decals = null,
                double litShare = WindowGrid.LitShare)
            {
                Lit = lit;
                Dark = dark;
                Cracked = cracked;
                Damage = damage;
                Openings = openings;
                Decals = decals;
                LitShare = litShare;
            }

            public bool HangsGlass => Lit != null;

            public static readonly Glazing OpeningsOnly = default;
        }

        /// <summary>
        /// A window somebody has already cracked, as a box you could walk
        /// through.
        ///
        /// HANDED OUT RATHER THAN KEPT, because a cracked pane has one more
        /// thing to do than be drawn: shoving a body through the opening
        /// finishes it off, which is city_chunk.gd's own second way of
        /// shattering a window. That needs a trigger at the pane, and the only
        /// place that knows exactly where a pane is, is the code that just put
        /// one there. Rederiving it in the streamer would be a second copy of
        /// the window grid, which is the mistake this whole file exists to
        /// avoid making twice.
        ///
        /// Positions are chunk-local, like every vertex the builder writes.
        /// </summary>
        public readonly struct CrackedOpening
        {
            public readonly PaneKey Key;
            public readonly Vector3 Centre;
            public readonly Vector3 Size;

            public CrackedOpening(PaneKey key, Vector3 centre, Vector3 size)
            {
                Key = key;
                Centre = centre;
                Size = size;
            }
        }

        /// <summary>
        /// How deep through the wall a cracked opening reaches for the purpose
        /// of noticing somebody in it. A pane is a flat sheet; a trigger with
        /// no thickness would be stepped over between two physics frames by
        /// anyone moving at a run.
        /// </summary>
        public const float CrackedOpeningDepth = 0.5f;

        private static void BuildBuilding(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer structures, MeshBuffer concrete, Glazing glazing)
        {
            // The stretch below BaseHeight is buried on the uphill side and
            // exposed downhill, which is what a hillside podium actually is.
            float x0 = (float)(site.MinX - origin.x);
            float x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z);
            float z1 = (float)(site.MaxZ - origin.z);
            float podiumY = site.PodiumBase - origin.y;
            float lobbyY = site.BaseHeight - origin.y;

            float height = site.Params.Height;
            float storey = BuildingInterior.StoreyHeight(height);
            int floors = BuildingInterior.FloorCountFor(height);
            float halfX = site.Params.Width * 0.5f;
            float halfZ = site.Params.Depth * 0.5f;
            float run = BuildingInterior.RunFor(halfX, halfZ);
            float centreX = (x0 + x1) * 0.5f;
            float centreZ = (z0 + z1) * 0.5f;
            float roofY = lobbyY + height;

            // The doorway, centred on the wall at maximum Z -- city_chunk.gd's
            // own door wall -- and standing on the lobby floor.
            float doorU0 = centreX - BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = centreX + BuildingInterior.DoorWidth * 0.5f;
            float doorTop = lobbyY + BuildingInterior.DoorHeight(height);

            // EXPLICIT ASK: "the windows/entrances should never be cut off
            // always have like a stairway entrance." site.BaseHeight (what
            // the door stands on) is the HIGHEST pad the WHOLE footprint
            // covers (see PadRangeForSpan's own doc comment: "put it at the
            // lowest and the uphill corner is buried"), but a multi-chunk
            // building's door sits on one specific ROW of that footprint --
            // the one at MaxZ -- which is routinely NOT the row that set
            // BaseHeight. Comparing against the door's own row specifically
            // (not the whole footprint's PodiumBase, which could be a
            // completely different, uninvolved corner) is what tells a door
            // that is actually already flush apart from one that is
            // genuinely left floating over the real drop underneath it.
            float doorRowPad = float.MinValue;
            for (int dx = 0; dx < site.Role.SpanX; dx++)
            {
                doorRowPad = Mathf.Max(doorRowPad,
                    world.Terrain.PadHeightFor(site.AnchorX + dx, site.AnchorY + site.Role.SpanY - 1));
            }
            BuildingDecor.EntranceStyle entrance =
                BuildingDecor.EntranceStyleFor(site.BaseHeight - doorRowPad);
            if (entrance.NeedsStairs)
            {
                AddEntranceStairs(concrete, new Vector3(centreX, lobbyY, z1), entrance.Rise);
            }

            var west = new Wall(true, x0, z0, z1);
            var east = new Wall(true, x1, z0, z1);
            var south = new Wall(false, z0, x0, x1);
            var north = new Wall(false, z1, x0, x1);

            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(height);

            // The podium, below the lobby floor. Solid: it is buried on the
            // uphill side and exposed downhill, so glazing it would put windows
            // in a retaining wall.
            AddWallPanel(structures, west, z0, z1, podiumY, lobbyY, Vector3.left);
            AddWallPanel(structures, east, z0, z1, podiumY, lobbyY, Vector3.right);
            AddWallPanel(structures, south, x0, x1, podiumY, lobbyY, Vector3.back);
            AddWallPanel(structures, north, x0, x1, podiumY, lobbyY, Vector3.forward);

            // Outward, for the street, with real openings in it.
            AddWindowedWall(structures, glazing, site, west, z0, z1, lobbyY, roofY,
                Vector3.left, plan, WindowGrid.Wall.West, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, east, z0, z1, lobbyY, roofY,
                Vector3.right, plan, WindowGrid.Wall.East, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, south, x0, x1, lobbyY, roofY,
                Vector3.back, plan, WindowGrid.Wall.South, rows, 0.0f, 0.0f, false);
            AddWindowedWall(structures, glazing, site, north, x0, x1, lobbyY, roofY,
                Vector3.forward, plan, WindowGrid.Wall.North, rows, doorU0, doorU1, true);

            // Inward, for the room. Same planes and the same openings, opposite
            // normals -- back to back, so neither is ever seen from the wrong
            // side, and a window is a hole through BOTH or it is not a hole.
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, west, z0, z1, lobbyY, roofY,
                Vector3.right, plan, WindowGrid.Wall.West, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, east, z0, z1, lobbyY, roofY,
                Vector3.left, plan, WindowGrid.Wall.East, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, south, x0, x1, lobbyY, roofY,
                Vector3.forward, plan, WindowGrid.Wall.South, rows, 0.0f, 0.0f, false);
            AddWindowedWall(concrete, Glazing.OpeningsOnly, site, north, x0, x1, lobbyY, roofY,
                Vector3.back, plan, WindowGrid.Wall.North, rows, doorU0, doorU1, true);

            // The lobby floor, and NOT a port: city_chunk.gd leans on the
            // chunk's own ground body for the ground floor, and here that
            // ground is cut away under the footprint (see BuildGround) so this
            // is the only thing holding anybody up. Solid, unless there is a
            // basement, in which case it is opened for the flight down.
            BuildingInterior.Stairwell basement = BuildingInterior.StairwellFor(
                BuildingInterior.BasementStairCorner, halfX, halfZ, run);
            Rect basementHole = site.Params.HasBasement
                ? new Rect(basement.Hole.x + centreX, basement.Hole.y + centreZ,
                    basement.Hole.width, basement.Hole.height)
                : Rect.zero;
            AddSlabFace(concrete, x0, x1, z0, z1, basementHole, lobbyY, Vector3.up);

            if (site.Params.HasBasement)
            {
                BuildBasement(site, concrete, x0, x1, z0, z1, lobbyY, storey,
                    centreX, centreZ, basement, basementHole, run);
            }

            for (int floor = 0; floor < floors; floor++)
            {
                float bottom = floor * storey;
                float top = BuildingInterior.FloorTop(floor, floors, height, storey);
                BuildingInterior.Stairwell well =
                    BuildingInterior.StairwellFor(floor, halfX, halfZ, run);
                Rect hole = new Rect(well.Hole.x + centreX, well.Hole.y + centreZ,
                    well.Hole.width, well.Hole.height);

                // This floor's ceiling. The last one is the roof itself, holed
                // exactly like the rest -- that opening is how the top flight
                // gets you outside.
                AddSlab(concrete, x0, x1, z0, z1, hole, lobbyY + top);

                AddRoomPartition(concrete, BuildingInterior.RoomFor(floor, halfX, halfZ),
                    centreX, centreZ, lobbyY + bottom, lobbyY + top, doorTop - lobbyY);

                if (floor == floors - 1)
                {
                    AddRoofDetail(concrete, site.Params.Roof, x0, x1, z0, z1, lobbyY + top);
                }

                foreach (BuildingInterior.Step step in
                         BuildingInterior.Steps(well, run, lobbyY + bottom, top - bottom))
                {
                    Vector3 half = step.Size * 0.5f;
                    Vector3 centre = new Vector3(
                        step.Centre.x + centreX, step.Centre.y, step.Centre.z + centreZ);
                    concrete.AddBox(centre - half, centre + half);
                }
            }
        }

        /// <summary>
        /// How far in front of the wall a distant window's glass floats. Big enough not to z-fight the wall at
        /// the distances it is seen from, small enough that no gap shows at a glancing angle.
        /// </summary>
        public const float ShellGlassLift = 0.06f;

        /// <summary>
        /// The DISTANT version of a building: its outside and nothing else.
        ///
        /// One panel per wall from the podium to the roof, the roof, and the glass laid over the wall as flat
        /// strips -- no openings cut through the wall, no back faces, no floors, stairs, rooms or inward copy of
        /// the walls. From beyond a few chunks the eye cannot tell, and about half of a downtown chunk's
        /// triangles were exactly those hidden rooms. It is still a closed solid, so the collider built from
        /// it keeps a fast flyer out of a building whose full-detail chunk has not streamed in yet.
        ///
        /// Neighbouring glazed cells that light the same way merge into ONE strip (the mullions between them
        /// are a pixel at this range), so a glass tower's row is a quad or two, not a quad per window.
        /// </summary>
        private static void BuildBuildingShell(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer structures, MeshBuffer concrete, Glazing glazing)
        {
            float x0 = (float)(site.MinX - origin.x);
            float x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z);
            float z1 = (float)(site.MaxZ - origin.z);
            float podiumY = site.PodiumBase - origin.y;
            float lobbyY = site.BaseHeight - origin.y;
            float height = site.Params.Height;
            float roofY = lobbyY + height;

            var west = new Wall(true, x0, z0, z1);
            var east = new Wall(true, x1, z0, z1);
            var south = new Wall(false, z0, x0, x1);
            var north = new Wall(false, z1, x0, x1);

            // The podium below the lobby floor, then the whole height of the wall in one panel each.
            AddWallPanel(structures, west, z0, z1, podiumY, roofY, Vector3.left);
            AddWallPanel(structures, east, z0, z1, podiumY, roofY, Vector3.right);
            AddWallPanel(structures, south, x0, x1, podiumY, roofY, Vector3.back);
            AddWallPanel(structures, north, x0, x1, podiumY, roofY, Vector3.forward);

            // The roof, and the antennas and parapets on it: they are the skyline.
            AddSlabFace(concrete, x0, x1, z0, z1, Rect.zero, roofY, Vector3.up);
            AddRoofDetail(concrete, site.Params.Roof, x0, x1, z0, z1, roofY);

            if (!glazing.HangsGlass)
            {
                return;
            }

            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(height);
            float centreX = (x0 + x1) * 0.5f;
            float doorU0 = centreX - BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = centreX + BuildingInterior.DoorWidth * 0.5f;

            AddShellGlass(glazing, site, west, z0, z1, lobbyY, Vector3.left, plan, WindowGrid.Wall.West, rows, 0.0f, 0.0f);
            AddShellGlass(glazing, site, east, z0, z1, lobbyY, Vector3.right, plan, WindowGrid.Wall.East, rows, 0.0f, 0.0f);
            AddShellGlass(glazing, site, south, x0, x1, lobbyY, Vector3.back, plan, WindowGrid.Wall.South, rows, 0.0f, 0.0f);
            AddShellGlass(glazing, site, north, x0, x1, lobbyY, Vector3.forward, plan, WindowGrid.Wall.North, rows, doorU0, doorU1);
        }

        /// <summary>One wall's glass for <see cref="BuildBuildingShell"/>: flat one-sided strips floating just off the wall.</summary>
        private static void AddShellGlass(in Glazing glazing, BuildingSite site, in Wall wall,
            float u0, float u1, float floorY, Vector3 normal, in WindowPlan plan, WindowGrid.Wall which,
            CellLayout rows, float doorU0, float doorU1)
        {
            CellLayout columns = plan.ColumnsFor(u1 - u0);
            float inset = WindowGrid.PaneInset;
            Vector3 lift = normal * ShellGlassLift;
            bool doorWall = doorU1 > doorU0;

            for (int row = 0; row < rows.Count; row++)
            {
                float y0 = floorY + row * rows.Size;
                float y1 = y0 + rows.Size;
                plan.Insets(WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height), out float insetBottom, out float insetTop);
                if (doorWall && row == 0)
                {
                    insetBottom = WindowGrid.PaneInsetVerticalClear;
                    insetTop = WindowGrid.PaneInsetVerticalClear;
                }
                float paneY0 = y0 + rows.Size * insetBottom;
                float paneY1 = y1 - rows.Size * insetTop;

                // The strip being grown: where it starts, where it ends, and which way its light is on.
                bool open = false;
                float runU0 = 0.0f;
                float runU1 = 0.0f;
                bool runLit = false;

                for (int column = 0; column < columns.Count; column++)
                {
                    float cellU0 = u0 + column * columns.Size;
                    float paneU0 = cellU0 + columns.Size * inset;
                    float paneU1 = cellU0 + columns.Size * (1.0f - inset);

                    bool glazed = plan.IsGlazed(which, column, row);
                    if (glazed && doorWall && row == 0 && cellU0 + columns.Size > doorU0 && cellU0 < doorU1)
                    {
                        glazed = false;   // the doorway
                    }
                    if (glazed)
                    {
                        var key = new PaneKey(site.AnchorX, site.AnchorY, which, column, row);
                        if (glazing.Damage != null && glazing.Damage.StateOf(key) == PaneState.Shattered)
                        {
                            glazed = false;
                        }
                    }

                    bool lit = glazed && plan.IsLit(which, column, row, glazing.LitShare);
                    if (glazed && open && lit == runLit)
                    {
                        runU1 = paneU1;   // the next window of the same kind: the strip just gets longer
                        continue;
                    }

                    if (open)
                    {
                        AddShellStrip(runLit ? glazing.Lit : glazing.Dark, wall, runU0, runU1, paneY0, paneY1, normal, lift);
                        open = false;
                    }
                    if (glazed)
                    {
                        open = true;
                        runU0 = paneU0;
                        runU1 = paneU1;
                        runLit = lit;
                    }
                }
                if (open)
                {
                    AddShellStrip(runLit ? glazing.Lit : glazing.Dark, wall, runU0, runU1, paneY0, paneY1, normal, lift);
                }
            }
        }

        private static void AddShellStrip(MeshBuffer glass, in Wall wall, float u0, float u1, float y0, float y1,
            Vector3 normal, Vector3 lift)
        {
            if (glass == null || u1 - u0 <= 0.0001f || y1 - y0 <= 0.0001f)
            {
                return;
            }
            AddFace(glass, normal, wall.At(u0, y0) + lift, wall.At(u1, y0) + lift, wall.At(u1, y1) + lift, wall.At(u0, y1) + lift);
        }

        /// <summary>
        /// A run of real exterior steps from the ground outside a building's
        /// door up to the door's own threshold -- see
        /// BuildingDecor.EntranceStyleFor's own doc comment for the bug this
        /// fixes. Reuses the exact same BuildingInterior.Steps machinery a
        /// street's own terrace stairs already build from (see
        /// AddStreetEdgeFeatures) -- <paramref name="doorThreshold"/> is the
        /// TOP of the flight (Vector3.forward is the door's own outward
        /// facing, Vector3.right its own width axis, matching
        /// BuildingDoor.Panel.Facing), climbing down and outward by
        /// <paramref name="rise"/> to the real ground below it.
        /// </summary>
        private static void AddEntranceStairs(MeshBuffer concrete, Vector3 doorThreshold, float rise)
        {
            float run = BuildingInterior.StepsFor(rise) * StreetStairTreadDepth;
            float floorY = doorThreshold.y - rise;
            var well = new BuildingInterior.Stairwell(doorThreshold, Vector3.forward, Vector3.right, default);
            foreach (BuildingInterior.Step step in BuildingInterior.Steps(well, run, floorY, rise))
            {
                Vector3 half = step.Size * 0.5f;
                concrete.AddBox(step.Centre - half, step.Centre + half);
            }
        }

        /// <summary>
        /// Balcony slabs and their railings, hung off the facade.
        ///
        /// Concrete rather than facade, like everything else that is not the
        /// windowed wall itself -- a balcony reading as a sheet of windows
        /// stuck to the side of a building is worse than no balcony.
        /// </summary>
        private static void AddBalconies(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer concrete)
        {
            List<BuildingDecor.Balcony> balconies = BuildingDecor.Balconies(
                world.ChunkSeed(site.AnchorX, site.AnchorY),
                site.Params.Width, site.Params.Depth, site.Params.Height);
            if (balconies.Count == 0)
            {
                return;
            }

            float centreX = (float)(site.CentreX - origin.x);
            float centreZ = (float)(site.CentreZ - origin.z);
            float baseY = site.BaseHeight - origin.y;

            foreach (BuildingDecor.Balcony balcony in balconies)
            {
                Vector3 centre = new Vector3(
                    balcony.Centre.x + centreX,
                    balcony.Centre.y + baseY,
                    balcony.Centre.z + centreZ);

                // Width runs one way and depth the other depending on which
                // wall it hangs off, which is what WidthAlongX settles.
                float sizeX = balcony.WidthAlongX
                    ? BuildingDecor.BalconyWidth : BuildingDecor.BalconyDepth;
                float sizeZ = balcony.WidthAlongX
                    ? BuildingDecor.BalconyDepth : BuildingDecor.BalconyWidth;

                AddCentredBox(concrete, centre.x, centre.z,
                    centre.y - BuildingDecor.BalconySlabThickness * 0.5f,
                    sizeX, BuildingDecor.BalconySlabThickness, sizeZ);

                // The rail along the open edge, set in by its own thickness so
                // it stands ON the slab rather than off the end of it.
                Vector3 outer = centre + balcony.Normal
                    * (BuildingDecor.BalconyDepth * 0.5f
                       - BuildingDecor.BalconyRailThickness * 0.5f);
                AddCentredBox(concrete, outer.x, outer.z, centre.y,
                    balcony.WidthAlongX
                        ? BuildingDecor.BalconyWidth : BuildingDecor.BalconyRailThickness,
                    BuildingDecor.BalconyRailHeight,
                    balcony.WidthAlongX
                        ? BuildingDecor.BalconyRailThickness : BuildingDecor.BalconyWidth);

                // And one down each side, so it reads as an enclosure rather
                // than a shelf with a fence on the far edge.
                Vector3 sideStep = balcony.WidthAlongX
                    ? new Vector3(BuildingDecor.BalconyWidth * 0.5f, 0.0f, 0.0f)
                    : new Vector3(0.0f, 0.0f, BuildingDecor.BalconyWidth * 0.5f);
                foreach (float sign in new[] { -1.0f, 1.0f })
                {
                    Vector3 side = centre + sideStep * sign;
                    AddCentredBox(concrete, side.x, side.z, centre.y,
                        balcony.WidthAlongX
                            ? BuildingDecor.BalconyRailThickness : BuildingDecor.BalconyDepth,
                        BuildingDecor.BalconyRailHeight,
                        balcony.WidthAlongX
                            ? BuildingDecor.BalconyDepth : BuildingDecor.BalconyRailThickness);
                }
            }
        }

        /// <summary>
        /// The attached secondary volume that turns a box into an L.
        ///
        /// A SOLID BLOCK, NOT A SECOND ENTERABLE BUILDING, which is
        /// city_chunk.gd's own choice: a wing has no doorway, no stairwell and
        /// no interior, so there is nothing to walk into and nothing to leave
        /// a hole open for. Four windowed faces and a roof cap.
        ///
        /// IT STARTS AT THE PODIUM, not at the lobby floor Godot uses, and that
        /// is terrain rather than drift. A Godot building stands on flat
        /// ground, so its floor and the ground are the same height; here a lot
        /// spans terraces and the main walls already reach down to the lowest
        /// pad they cover. A wing stopping at the lobby floor would hang in the
        /// air on the downhill side.
        /// </summary>
        private static void AddWing(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer facade, MeshBuffer roof)
        {
            foreach (Bounds box in WingBoxesFor(world, site))
            {
                facade.AddBox(box.min - origin, box.max - origin, roof);
            }
        }

        /// <summary>
        /// Where a building's wings stand, in world space -- zero, one, two or
        /// three of them (see BuildingDecor.WingsFor's own doc comment).
        ///
        /// PUBLIC BECAUSE A TEST HAS TO ASK. The clamp that stops a wing from
        /// growing through the building next door is the whole reason this
        /// differs from Godot, and the only honest way to check it is to take
        /// every wing in a sweep and hold it against every neighbouring
        /// footprint. Rederiving these six numbers inside the test would be
        /// testing a copy of the arithmetic rather than the arithmetic.
        /// </summary>
        public static List<Bounds> WingBoxesFor(CityWorld world, BuildingSite site)
        {
            var boxes = new List<Bounds>();

            // How much clear ground each of the three candidate walls has: the
            // slack the building leaves inside its own lot, plus whatever lies
            // beyond that lot edge.
            CityChunkGenerator.LotRect lot = CityChunkGenerator.LotRectFor(site.Role);
            double lotCentreX = CityLayout.SpanCentre(site.AnchorX, site.Role.SpanX) + lot.CentreOffsetX;
            double lotCentreZ = CityLayout.SpanCentre(site.AnchorY, site.Role.SpanY) + lot.CentreOffsetZ;

            float westRoom = (float)(site.MinX - (lotCentreX - lot.Width * 0.5))
                             + CityChunkGenerator.MarginBeyond(site.Role, -1, 0);
            float eastRoom = (float)(lotCentreX + lot.Width * 0.5 - site.MaxX)
                             + CityChunkGenerator.MarginBeyond(site.Role, 1, 0);
            float southRoom = (float)(site.MinZ - (lotCentreZ - lot.Depth * 0.5))
                              + CityChunkGenerator.MarginBeyond(site.Role, 0, -1);

            List<BuildingDecor.Wing> wings = BuildingDecor.WingsFor(
                world.ChunkSeed(site.AnchorX, site.AnchorY),
                site.Params.Width, site.Params.Depth, site.Params.Height,
                westRoom, eastRoom, southRoom);

            float centreX = (float)site.CentreX;
            float centreZ = (float)site.CentreZ;
            float halfX = site.Params.Width * 0.5f;
            float halfZ = site.Params.Depth * 0.5f;

            // Bite into the wall it grows from. Meeting it exactly leaves two
            // coincident faces, which z-fight along the whole join.
            const float Bite = 0.05f;

            foreach (BuildingDecor.Wing wing in wings)
            {
                float minX, maxX, minZ, maxZ;
                if (Mathf.Abs(wing.Normal.x) > 0.5f)
                {
                    float face = centreX + wing.Normal.x * halfX;
                    float outer = face + wing.Depth * wing.Normal.x;
                    float inner = face - Bite * wing.Normal.x;
                    minX = Mathf.Min(inner, outer);
                    maxX = Mathf.Max(inner, outer);
                    minZ = centreZ + wing.Along - wing.Width * 0.5f;
                    maxZ = centreZ + wing.Along + wing.Width * 0.5f;
                }
                else
                {
                    float face = centreZ + wing.Normal.z * halfZ;
                    float outer = face + wing.Depth * wing.Normal.z;
                    float inner = face - Bite * wing.Normal.z;
                    minZ = Mathf.Min(inner, outer);
                    maxZ = Mathf.Max(inner, outer);
                    minX = centreX + wing.Along - wing.Width * 0.5f;
                    maxX = centreX + wing.Along + wing.Width * 0.5f;
                }

                var box = new Bounds();
                box.SetMinMax(
                    new Vector3(minX, site.PodiumBase, minZ),
                    new Vector3(maxX, site.BaseHeight + wing.Height, maxZ));
                boxes.Add(box);
            }

            return boxes;
        }

        /// <summary>
        /// Vertical concrete fins standing proud of the facade.
        ///
        /// FACADE MATERIAL, NOT CONCRETE, which is city_chunk.gd's own choice
        /// and worth stating because the opposite looks more obviously right.
        /// A fin's own faces are UV'd in metres like every other surface here,
        /// so a fin picks up the same strip of wall texture as the wall
        /// directly behind it -- it reads as a piece of the facade pulled
        /// forward, which is what a cast fin is. Putting them in the concrete
        /// buffer would give a cleaner fin and a visible seam where it meets
        /// the wall it grew out of.
        ///
        /// NO COLLISION IS NOT A DECISION HERE, unlike in Godot where it was:
        /// the chunk's own MeshCollider takes whatever triangles it is given.
        /// A fin is a thin ledge on a wall that is already solid behind it, so
        /// nothing becomes passable or impassable either way.
        /// </summary>
        private static void AddRibs(CityWorld world, BuildingSite site, Vector3 origin,
            MeshBuffer facade)
        {
            float halfX = site.Params.Width * 0.5f;
            float halfZ = site.Params.Depth * 0.5f;

            BuildingDecor.RibbedFacade ribbed = BuildingDecor.Ribs(
                world.ChunkSeed(site.AnchorX, site.AnchorY),
                site.Params.Width, site.Params.Depth, site.Params.Height,
                -BuildingInterior.DoorWidth * 0.5f, BuildingInterior.DoorWidth * 0.5f);
            if (ribbed.Fins.Count == 0)
            {
                return;
            }

            float centreX = (float)(site.CentreX - origin.x);
            float centreZ = (float)(site.CentreZ - origin.z);
            float y0 = site.BaseHeight - origin.y + ribbed.BaseY;
            float y1 = site.BaseHeight - origin.y + site.Params.Height;

            foreach (BuildingDecor.Rib rib in ribbed.Fins)
            {
                bool runsInZ = Mathf.Abs(rib.Normal.x) > 0.5f;
                Wall wall = runsInZ
                    ? new Wall(true, centreX + rib.Normal.x * halfX, rib.U0, rib.U1)
                    : new Wall(false, centreZ + rib.Normal.z * halfZ, rib.U0, rib.U1);
                float u0 = rib.U0 + (runsInZ ? centreZ : centreX);
                float u1 = rib.U1 + (runsInZ ? centreZ : centreX);

                AddFin(facade, wall, u0, u1, y0, y1, rib.Normal, ribbed.Depth,
                    runsInZ ? Vector3.forward : Vector3.right);
            }
        }

        /// <summary>
        /// One fin: a rectangular prism standing proud of its wall plane.
        ///
        /// FOUR FACES, NOT SIX, and this is Godot's own reasoning carried over
        /// rather than a shortcut. The back face is flush against the wall it
        /// stands on and the underside is only visible from directly below a
        /// fin that starts one storey up. A densely ribbed tower is a hundred
        /// fins, and this file's cost is already what dominates streaming.
        /// </summary>
        private static void AddFin(MeshBuffer buffer, in Wall wall, float u0, float u1,
            float y0, float y1, Vector3 normal, float depth, Vector3 along)
        {
            Vector3 out_ = normal * depth;

            Vector3 baseLow0 = wall.At(u0, y0);
            Vector3 baseLow1 = wall.At(u1, y0);
            Vector3 baseHigh0 = wall.At(u0, y1);
            Vector3 baseHigh1 = wall.At(u1, y1);

            AddFace(buffer, normal,
                baseLow0 + out_, baseLow1 + out_, baseHigh1 + out_, baseHigh0 + out_);
            AddFace(buffer, -along, baseLow0, baseLow0 + out_, baseHigh0 + out_, baseHigh0);
            AddFace(buffer, along, baseLow1, baseLow1 + out_, baseHigh1 + out_, baseHigh1);
            AddFace(buffer, Vector3.up, baseHigh0, baseHigh1, baseHigh1 + out_, baseHigh0 + out_);
        }

        /// <summary>
        /// Whatever is standing on this building's roof.
        ///
        /// city_chunk.gd has rolled a roof type per building since the start
        /// and CityChunkGenerator ports it -- it is generated, it is tested,
        /// and until now nothing read it, so every rooftop in the city was a
        /// bare plane. That is most visible from exactly the two angles the
        /// art direction cares about: the skyline, and standing on a roof.
        ///
        /// Sizes are Godot's own and are deliberately SMALL against a 20m
        /// roof. These are props, not massing: a water tank a person could
        /// stand beside, not a second storey.
        /// </summary>
        private static void AddRoofDetail(MeshBuffer buffer, string roof,
            float x0, float x1, float z0, float z1, float roofY)
        {
            float centreX = (x0 + x1) * 0.5f;
            float centreZ = (z0 + z1) * 0.5f;

            switch (roof)
            {
                case "shed":
                    AddCentredBox(buffer, centreX, centreZ, roofY, 1.1f, 0.6f, 1.1f);
                    break;

                case "antenna":
                    AddMast(buffer, centreX, centreZ, roofY, 2.0f);
                    break;

                case "chimney":
                    AddCentredBox(buffer, centreX, centreZ, roofY, 0.9f, 1.2f, 0.9f);
                    AddMast(buffer, centreX + 0.3f, centreZ, roofY + 1.2f, 1.5f);
                    break;

                case "parapet":
                    // A rim wall round all four edges. Part of the roofline
                    // rather than a prop sitting on it, which is why it takes
                    // the building's own footprint rather than a fixed size.
                    AddCentredBox(buffer, centreX, z0 + ParapetThickness * 0.5f, roofY,
                        x1 - x0, ParapetHeight, ParapetThickness);
                    AddCentredBox(buffer, centreX, z1 - ParapetThickness * 0.5f, roofY,
                        x1 - x0, ParapetHeight, ParapetThickness);
                    AddCentredBox(buffer, x0 + ParapetThickness * 0.5f, centreZ, roofY,
                        ParapetThickness, ParapetHeight, z1 - z0);
                    AddCentredBox(buffer, x1 - ParapetThickness * 0.5f, centreZ, roofY,
                        ParapetThickness, ParapetHeight, z1 - z0);
                    break;

                case "water_tower":
                    // Four thin legs and a tank, which is the silhouette that
                    // reads as a rooftop from a long way off.
                    foreach (float dx in new[] { -0.55f, 0.55f })
                    {
                        foreach (float dz in new[] { -0.55f, 0.55f })
                        {
                            AddCentredBox(buffer, centreX + dx, centreZ + dz, roofY,
                                0.1f, TankLegHeight, 0.1f);
                        }
                    }
                    AddCentredBox(buffer, centreX, centreZ, roofY + TankLegHeight,
                        TankRadius * 2.0f, TankHeight, TankRadius * 2.0f);
                    break;
            }
        }

        private const float ParapetHeight = 0.5f;
        private const float ParapetThickness = 0.15f;
        private const float TankLegHeight = 1.4f;
        private const float TankRadius = 0.9f;
        private const float TankHeight = 1.3f;

        /// <summary>A box standing ON a surface, given its centre in X and Z.</summary>
        private static void AddCentredBox(MeshBuffer buffer, float centreX, float centreZ,
            float baseY, float width, float height, float depth)
        {
            buffer.AddBox(
                new Vector3(centreX - width * 0.5f, baseY, centreZ - depth * 0.5f),
                new Vector3(centreX + width * 0.5f, baseY + height, centreZ + depth * 0.5f));
        }

        /// <summary>
        /// An aerial. A square post rather than Godot's tapered cylinder --
        /// at 6cm across it is under a pixel wide from anywhere you would see
        /// a skyline from, and a cylinder would cost sixteen times the
        /// triangles to say the same thing.
        /// </summary>
        private static void AddMast(MeshBuffer buffer, float centreX, float centreZ,
            float baseY, float height)
        {
            AddCentredBox(buffer, centreX, centreZ, baseY, 0.06f, height, 0.06f);
        }

        /// <summary>
        /// One storey below ground, reached down the hole in the lobby floor.
        ///
        /// DELIBERATELY WINDOWLESS, which is city_chunk.gd's own choice: it is
        /// underground, so it skips the facade treatment every storey above it
        /// gets and is four solid walls. Its own floor is solid too -- nothing
        /// goes below this -- and its ceiling is the lobby slab, which the
        /// caller has already opened for the flight.
        ///
        /// The stairwell is a FIXED corner rather than the rotating one every
        /// floor above uses. There is only one floor down here and nothing
        /// below it to meet, so there is nothing for a rotation to line up
        /// with.
        /// </summary>
        private static void BuildBasement(BuildingSite site, MeshBuffer concrete,
            float x0, float x1, float z0, float z1, float lobbyY, float storey,
            float centreX, float centreZ, BuildingInterior.Stairwell well, Rect hole, float run)
        {
            float floorY = lobbyY - storey;

            var west = new Wall(true, x0, z0, z1);
            var east = new Wall(true, x1, z0, z1);
            var south = new Wall(false, z0, x0, x1);
            var north = new Wall(false, z1, x0, x1);
            AddWallPanel(concrete, west, z0, z1, floorY, lobbyY, Vector3.right);
            AddWallPanel(concrete, east, z0, z1, floorY, lobbyY, Vector3.left);
            AddWallPanel(concrete, south, x0, x1, floorY, lobbyY, Vector3.forward);
            AddWallPanel(concrete, north, x0, x1, floorY, lobbyY, Vector3.back);

            AddSlabFace(concrete, x0, x1, z0, z1, Rect.zero, floorY, Vector3.up);

            // The underside of the lobby slab, seen from down here, and the
            // collar closing the opening's own edges.
            AddSlabFace(concrete, x0, x1, z0, z1, hole,
                lobbyY - BuildingInterior.SlabThickness, Vector3.down);
            AddHoleCollar(concrete, hole, lobbyY - BuildingInterior.SlabThickness, lobbyY);

            // The same flight the player walks DOWN to get here.
            foreach (BuildingInterior.Step step in
                     BuildingInterior.Steps(well, run, floorY, storey))
            {
                Vector3 half = step.Size * 0.5f;
                Vector3 centre = new Vector3(
                    step.Centre.x + centreX, step.Centre.y, step.Centre.z + centreZ);
                concrete.AddBox(centre - half, centre + half);
            }
        }

        /// <summary>
        /// The two walls that cut one floor's back corner into a separate room.
        ///
        /// BOTH SIDES OF EACH ARE REAL. Every other interior wall in a building
        /// has open air or solid podium behind it, so one face is enough. These
        /// two have walkable floor on both sides, and a partition you can see
        /// through from one side of a room is worse than no partition.
        /// </summary>
        private static void AddRoomPartition(MeshBuffer buffer, in BuildingInterior.Room room,
            float centreX, float centreZ, float floorY, float ceilingY, float doorHeight)
        {
            if (!room.Exists)
            {
                return;
            }

            float x0 = room.Plan.xMin + centreX;
            float x1 = room.Plan.xMax + centreX;
            float z0 = room.Plan.yMin + centreZ;
            float z1 = room.Plan.yMax + centreZ;
            float doorX = room.DoorWallX + centreX;
            float doorZ0 = room.DoorZ0 + centreZ;
            float doorZ1 = room.DoorZ1 + centreZ;

            var doorWall = new Wall(true, doorX, z0, z1);
            foreach (Vector3 normal in new[] { Vector3.right, Vector3.left })
            {
                AddWallAround(buffer, doorWall, floorY, ceilingY, normal,
                    doorZ0, doorZ1, floorY, floorY + doorHeight);
            }

            // The room's other inner side, solid: one doorway is enough for a
            // corner room this size.
            var backWall = new Wall(false, z1, x0, x1);
            foreach (Vector3 normal in new[] { Vector3.forward, Vector3.back })
            {
                AddWallPanel(buffer, backWall, x0, x1, floorY, ceilingY, normal);
            }
        }

        /// <summary>
        /// One axis-aligned wall plane and the mapping from "along the wall"
        /// to world, which is city_chunk.gd's own to_world callable. Wall
        /// geometry is written once against this rather than four times
        /// against hand-derived signs, which is where that file's own
        /// comments say the sign errors came from.
        /// </summary>
        private readonly struct Wall
        {
            /// <summary>True if the wall stands at a fixed X and runs in Z.</summary>
            public readonly bool RunsInZ;

            /// <summary>The wall's own X, or its own Z.</summary>
            public readonly float Plane;

            public readonly float U0;
            public readonly float U1;

            public Wall(bool runsInZ, float plane, float u0, float u1)
            {
                RunsInZ = runsInZ;
                Plane = plane;
                U0 = u0;
                U1 = u1;
            }

            public Vector3 At(float u, float y)
            {
                return RunsInZ ? new Vector3(Plane, y, u) : new Vector3(u, y, Plane);
            }
        }

        private static void AddWallPanel(MeshBuffer buffer, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal)
        {
            // A panel squeezed to nothing is normal here, not an error: a
            // doorway reaching a wall's edge leaves no strip beside it.
            if (u1 - u0 <= 0.0001f || y1 - y0 <= 0.0001f)
            {
                return;
            }
            AddFace(buffer, normal,
                wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1));
        }

        /// <summary>
        /// AddWallPanel's own shape, but double-sided (see AddDoubleSidedFace's
        /// own doc comment) -- used ONLY for the glass frame strips
        /// AddPaneWithHole cuts around a bullet hole, so a cracked pane can
        /// still be finished off (shattered) by a second shot from inside
        /// the same way an intact one can now be cracked from inside.
        /// </summary>
        private static void AddDoubleSidedWallPanel(MeshBuffer buffer, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal)
        {
            if (u1 - u0 <= 0.0001f || y1 - y0 <= 0.0001f)
            {
                return;
            }
            AddDoubleSidedFace(buffer, normal,
                wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1));
        }

        /// <summary>
        /// A wall with real openings cut through it, one per glazed cell.
        ///
        /// THE DECOMPOSITION IS THE WHOLE COST STORY. The obvious way to build
        /// this is four frame pieces around every cell, and that is what
        /// city_chunk.gd does -- but city_chunk.gd glazes every cell, so it has
        /// no blank concrete to waste quads on. Here 58% of cells are blank,
        /// and cell-at-a-time would have paid four quads each for nothing.
        ///
        /// Instead each ROW is cut into three horizontal bands. Every opening
        /// in a row starts and ends at the same two heights -- same margin
        /// ratio, same row height -- so the sill band below them and the header
        /// band above them each run the full width of the wall as ONE quad,
        /// however many windows the row has. Only the middle band is broken up,
        /// and there the blank cells between two windows merge into a single
        /// run. A twelve-by-twelve wall comes out at about eight quads a row
        /// rather than sixty.
        ///
        /// A <paramref name="glazing"/> with no buffers is the INWARD copy of
        /// the wall: same openings, opposite normal, and no glass, because the
        /// outward pass already hung it. A window has to be a hole through both
        /// or it is not a hole -- AND THE OPENING IS CUT WHATEVER STATE THE
        /// GLASS IS IN, so a shattered window leaves a hole rather than
        /// reinstating the wall it was cut from.
        /// </summary>
        private static void AddWindowedWall(MeshBuffer wallBuffer, in Glazing glazing,
            BuildingSite site, in Wall wall, float u0, float u1, float floorY, float topY,
            Vector3 normal, in WindowPlan plan, WindowGrid.Wall which, CellLayout rows,
            float doorU0, float doorU1, bool hasDoor)
        {
            CellLayout columns = plan.ColumnsFor(u1 - u0);
            float inset = WindowGrid.PaneInset;

            for (int row = 0; row < rows.Count; row++)
            {
                float y0 = floorY + row * rows.Size;
                float y1 = y0 + rows.Size;

                if (hasDoor && row == 0)
                {
                    AddDoorRow(wallBuffer, glazing, site, wall, u0, y0, y1, normal,
                        plan, which, columns, doorU0, doorU1);
                    continue;
                }

                // A row with no window in it (a blank party wall, the solid band between two ribbons, a
                // warehouse below its clerestory) is ONE panel, not the sill, lintel and infill of three.
                bool anyWindow = false;
                for (int column = 0; column < columns.Count && !anyWindow; column++)
                {
                    anyWindow = plan.IsGlazed(which, column, row);
                }
                if (!anyWindow)
                {
                    AddWallPanel(wallBuffer, wall, u0, u1, y0, y1, normal);
                    continue;
                }

                // ASYMMETRIC ON PURPOSE: AddSlab hangs a floor slab downward
                // from its own boundary, so a slab only ever eats into the
                // TOP of the row directly below it -- a row's OWN bottom
                // edge never has one, whatever the row index, because that
                // material is entirely on the far side of the boundary
                // below. Only the top edge needs checking. See
                // WindowGrid.TopEdgeHasSlab's own comment for why that is a
                // real per-building check and not a row-parity guess.
                plan.Insets(WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height),
                    out float insetBottom, out float insetTop);
                float paneY0 = y0 + rows.Size * insetBottom;
                float paneY1 = y1 - rows.Size * insetTop;

                AddWallPanel(wallBuffer, wall, u0, u1, y0, paneY0, normal);
                AddWallPanel(wallBuffer, wall, u0, u1, paneY1, y1, normal);

                float runStart = u0;
                for (int column = 0; column < columns.Count; column++)
                {
                    if (!plan.IsGlazed(which, column, row))
                    {
                        continue;
                    }

                    float cellU0 = u0 + column * columns.Size;
                    float paneU0 = cellU0 + columns.Size * inset;
                    float paneU1 = cellU0 + columns.Size * (1.0f - inset);

                    AddWallPanel(wallBuffer, wall, runStart, paneU0, paneY0, paneY1, normal);
                    runStart = paneU1;

                    AddPane(glazing, site, wall, paneU0, paneU1, paneY0, paneY1, normal,
                        plan, which, column, row);
                }
                AddWallPanel(wallBuffer, wall, runStart, u1, paneY0, paneY1, normal);
            }
        }

        /// <summary>
        /// The entrance row: same grid, but a doorway the height of the whole
        /// row stands in it.
        ///
        /// NO BAND MERGING HERE, because the door reaches from the floor to the
        /// top of the row and cuts both the sill and the header band in two.
        /// One row per building pays cell-at-a-time; the eleven above it do not.
        ///
        /// A cell the door passes through keeps whatever is left of itself
        /// either side and gets no window, which is city_chunk.gd's own
        /// whole-cell exclusion. Narrowing the cells around the door instead --
        /// its earlier approach -- gives that row a different column grid from
        /// every row above it, and that shipped a visible seam there twice.
        /// </summary>
        private static void AddDoorRow(MeshBuffer wallBuffer, in Glazing glazing,
            BuildingSite site, in Wall wall, float u0, float y0, float y1, Vector3 normal,
            in WindowPlan plan, WindowGrid.Wall which, CellLayout columns, float doorU0, float doorU1)
        {
            float inset = WindowGrid.PaneInset;
            // The door row is always row 0, whose own top boundary (1) is
            // never a slab location (see WindowGrid.TopEdgeHasSlab) -- the
            // small clear margin is correct on both edges here, not just a
            // simplification.
            float paneY0 = y0 + (y1 - y0) * WindowGrid.PaneInsetVerticalClear;
            float paneY1 = y1 - (y1 - y0) * WindowGrid.PaneInsetVerticalClear;

            for (int column = 0; column < columns.Count; column++)
            {
                float cellU0 = u0 + column * columns.Size;
                float cellU1 = cellU0 + columns.Size;

                if (cellU1 > doorU0 && cellU0 < doorU1)
                {
                    // Whatever is left of this cell beside the opening.
                    AddWallPanel(wallBuffer, wall, cellU0, Mathf.Min(cellU1, doorU0), y0, y1, normal);
                    AddWallPanel(wallBuffer, wall, Mathf.Max(cellU0, doorU1), cellU1, y0, y1, normal);
                    continue;
                }

                if (!plan.IsGlazed(which, column, 0))
                {
                    AddWallPanel(wallBuffer, wall, cellU0, cellU1, y0, y1, normal);
                    continue;
                }

                float paneU0 = cellU0 + columns.Size * inset;
                float paneU1 = cellU1 - columns.Size * inset;
                AddWallPanel(wallBuffer, wall, cellU0, paneU0, y0, y1, normal);
                AddWallPanel(wallBuffer, wall, paneU1, cellU1, y0, y1, normal);
                AddWallPanel(wallBuffer, wall, paneU0, paneU1, y0, paneY0, normal);
                AddWallPanel(wallBuffer, wall, paneU0, paneU1, paneY1, y1, normal);

                AddPane(glazing, site, wall, paneU0, paneU1, paneY0, paneY1, normal,
                    plan, which, column, 0);
            }
        }

        /// <summary>
        /// One sheet of glass, or none if somebody has already put a fist
        /// through it.
        ///
        /// THREE BUFFERS RATHER THAN A COLOUR PER PANE. Whether a window is lit
        /// and whether it is cracked both have to reach the shader somehow, and
        /// MeshBuffer has no colour channel -- adding one would put four bytes
        /// on every vertex in the city to say something about a few hundred of
        /// them. Submeshes cost nothing per vertex, and they let DayNightCycle
        /// bring every lit window up at dusk by touching one material rather
        /// than walking geometry.
        ///
        /// CRACKED GLASS IS THE ONE THAT MATTERS STRUCTURALLY. It has to still
        /// be drawn and NOT be collided with, because the first hit is what
        /// makes a window walkable. The chunk's collider is the render mesh, so
        /// the only way to have one without the other is to put it in a buffer
        /// the collider is not built from.
        /// </summary>
        private static void AddPane(in Glazing glazing, BuildingSite site, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal,
            in WindowPlan plan, WindowGrid.Wall which, int column, int row)
        {
            if (!glazing.HangsGlass)
            {
                return;
            }

            var key = new PaneKey(site.AnchorX, site.AnchorY, which, column, row);
            PaneState state = glazing.Damage == null ? PaneState.Intact : glazing.Damage.StateOf(key);
            if (state == PaneState.Shattered)
            {
                return;
            }

            MeshBuffer glass = state == PaneState.Cracked
                ? glazing.Cracked
                : plan.IsLit(which, column, row, glazing.LitShare) ? glazing.Lit : glazing.Dark;
            if (glass == null)
            {
                return;
            }

            // A real hole where the blow that cracked it landed, EXPLICIT
            // ASK: "when you shoot the glass make it shatter but also have a
            // hole in it where you shot." Only cracked panes carry an
            // impact point at all (a fresh Intact pane was never hit), and
            // only cracked panes are still drawn (Shattered returned above),
            // so this is the only branch that ever needs one.
            if (state == PaneState.Cracked && glazing.Damage != null &&
                glazing.Damage.TryGetImpacts(key, out Vector2[] impacts))
            {
                // EVERY bullet's hole, each with its own crack pattern round it.
                var centres = new Vector2[impacts.Length];
                for (int i = 0; i < impacts.Length; i++)
                {
                    centres[i] = ClampedHoleCentre(u0, u1, y0, y1, impacts[i]);
                }
                AddPaneWithHoles(glass, wall, u0, u1, y0, y1, normal, centres);
                if (glazing.Decals != null)
                {
                    foreach (Vector2 centre in centres)
                    {
                        AddCrackDecal(glazing.Decals, wall, centre, normal);
                    }
                }
            }
            else
            {
                AddDoubleSidedFace(glass, normal,
                    wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1));
            }

            if (state == PaneState.Cracked && glazing.Openings != null)
            {
                Vector3 centre = (wall.At(u0, y0) + wall.At(u1, y1)) * 0.5f;
                Vector3 across = wall.At(u1, y0) - wall.At(u0, y0);
                var size = new Vector3(
                    Mathf.Max(Mathf.Abs(across.x), CrackedOpeningDepth),
                    y1 - y0,
                    Mathf.Max(Mathf.Abs(across.z), CrackedOpeningDepth));
                glazing.Openings.Add(new CrackedOpening(key, centre, size));
            }
        }

        /// <summary>
        /// The crack-texture decal's own size in metres -- EXPLICIT ASK:
        /// "shrink them to size," a real bullet-hole scale rather than the
        /// old 18cm square that read as oversized. Aspect matches
        /// BulletHoleCrack.png's own 1024x856 ratio so the image doesn't
        /// stretch.
        /// </summary>
        private const float BulletHoleDecalWidth = 0.34f;
        private const float BulletHoleDecalHeight = 0.28f;

        /// <summary>
        /// How far the decal sits proud of the base glass plane -- both are
        /// alpha-blended and coplanar otherwise, which is exactly the
        /// condition that z-fights.
        /// </summary>
        private const float BulletHoleDecalOffset = 0.003f;

        /// <summary>
        /// The REAL geometric opening at the decal's own centre, sized as a fraction of the decal itself.
        ///
        /// DELIBERATELY SMALLER THAN THE TEXTURE'S OWN TRANSPARENT CENTRE (a measured alpha=0 run 149px wide by
        /// 141px tall in a 1024x856 image; this is 60% of it) -- EXPLICIT BUG REPORT: "the bullet holes have a
        /// visible square around them due to the image i used." Sizing the real cut to match the transparent
        /// run EXACTLY left nothing standing between the hard-edged rectangular hole and whatever is really
        /// behind the glass (the building interior, at night far darker or far flatter than the frosted glass
        /// around it) -- there was no margin left for the decal's own dense crack shards, which start growing
        /// in only a little further out, to actually cover the cut's own straight edges and corners. Shrinking
        /// the real hole leaves a ring of ordinary frosted (Cracked) glass between the cut and the decal's own
        /// transparent centre -- itself still shown through the decal's transparency, so nothing new is hidden,
        /// but now it is glass being shown, not whatever is on the other side of it.
        /// </summary>
        private const float BulletHoleWidthFraction = 149.0f / 1024.0f * 0.6f;
        private const float BulletHoleHeightFraction = 141.0f / 856.0f * 0.6f;
        private const float BulletHoleHalfWidth = BulletHoleDecalWidth * BulletHoleWidthFraction * 0.5f;
        private const float BulletHoleHalfHeight = BulletHoleDecalHeight * BulletHoleHeightFraction * 0.5f;

        /// <summary>
        /// Where a hole actually lands within its own pane, clamped so it
        /// never pokes out past the pane's own edge -- shared between
        /// AddPaneWithHole (the real geometric opening) and AddCrackDecal
        /// (the crack texture sitting on top of it), so the two always
        /// agree on centre even when an impact near a pane's edge gets
        /// pulled inward.
        /// </summary>
        private static Vector2 ClampedHoleCentre(float u0, float u1, float y0, float y1, Vector2 impactUV)
        {
            float halfW = Mathf.Min(BulletHoleHalfWidth, (u1 - u0) * 0.4f);
            float halfH = Mathf.Min(BulletHoleHalfHeight, (y1 - y0) * 0.4f);
            float centreU = Mathf.Clamp(Mathf.Lerp(u0, u1, impactUV.x), u0 + halfW, u1 - halfW);
            float centreY = Mathf.Clamp(Mathf.Lerp(y0, y1, impactUV.y), y0 + halfH, y1 - halfH);
            return new Vector2(centreU, centreY);
        }

        /// <summary>
        /// A cracked pane with a real opening punched through it at
        /// `centre` -- four strips framing a hole, the exact same technique
        /// AddWallAround already uses to cut a window's own opening into
        /// the wall around it, just nested one level down into the pane
        /// itself.
        ///
        /// STILL THE SAME `glass` (Cracked) BUFFER for the frame, so the
        /// rest of the pane keeps reading as broken/frosted glass around a
        /// hole rather than clean glass with a hole in it. THE WALKTHROUGH
        /// TRIGGER STAYS WHOLE-PANE, deliberately -- shrinking it to just
        /// this hole is a bigger, separate change to CrackedOpening/
        /// CrackedPane that nothing here asked for.
        /// </summary>
        private static void AddPaneWithHole(MeshBuffer glass, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal, Vector2 centre)
        {
            AddPaneWithHoles(glass, wall, u0, u1, y0, y1, normal, new[] { centre });
        }

        /// <summary>
        /// A pane with a real opening at EACH of <paramref name="centres"/> -- a window somebody has been
        /// shooting at.
        ///
        /// The pane is cut into vertical strips at every hole's left and right edge, and each strip is filled
        /// EXCEPT where holes are: within a strip the holes that cross it are merged into runs and the glass is
        /// the gaps between them. That handles any number of holes, holes that overlap (a second bullet in the
        /// same spot) and holes side by side, with no quad emitted twice. One hole comes out as exactly the
        /// four strips it always was.
        /// </summary>
        private static void AddPaneWithHoles(MeshBuffer glass, in Wall wall,
            float u0, float u1, float y0, float y1, Vector3 normal, Vector2[] centres)
        {
            float halfW = Mathf.Min(BulletHoleHalfWidth, (u1 - u0) * 0.4f);
            float halfH = Mathf.Min(BulletHoleHalfHeight, (y1 - y0) * 0.4f);

            int count = centres.Length;
            var holes = new Rect[count];
            var edges = new List<float>(count * 2 + 2) { u0, u1 };
            for (int i = 0; i < count; i++)
            {
                holes[i] = Rect.MinMaxRect(centres[i].x - halfW, centres[i].y - halfH, centres[i].x + halfW, centres[i].y + halfH);
                edges.Add(holes[i].xMin);
                edges.Add(holes[i].xMax);
            }
            edges.Sort();

            var spans = new List<Vector2>(count);
            for (int e = 0; e + 1 < edges.Count; e++)
            {
                float left = edges[e];
                float right = edges[e + 1];
                if (right - left <= 0.0001f)
                {
                    continue;
                }
                float middle = (left + right) * 0.5f;

                // The vertical spans of this strip that are hole.
                spans.Clear();
                for (int i = 0; i < count; i++)
                {
                    if (middle > holes[i].xMin && middle < holes[i].xMax)
                    {
                        spans.Add(new Vector2(holes[i].yMin, holes[i].yMax));
                    }
                }
                spans.Sort((a, b) => a.x.CompareTo(b.x));

                // Glass in the gaps between them.
                float cursor = y0;
                foreach (Vector2 span in spans)
                {
                    if (span.x > cursor)
                    {
                        AddDoubleSidedWallPanel(glass, wall, left, right, cursor, span.x, normal);
                    }
                    cursor = Mathf.Max(cursor, span.y);
                }
                if (cursor < y1)
                {
                    AddDoubleSidedWallPanel(glass, wall, left, right, cursor, y1, normal);
                }
            }
        }

        /// <summary>
        /// The real crack-texture decal (BulletHoleCrack.png, via
        /// CrackDecalMaterial) sitting on top of the base glass, centred on
        /// the same `centre` AddPaneWithHole just cut its own real opening
        /// at -- EXPLICIT ASK: replacing the old plain square hole with the
        /// user's own reference image, "radial cracks with a real see-
        /// through hole in the middle" instead of a bare geometric notch.
        /// One quad, explicit 0..1 UVs (not MeshBuffer's usual metre-scale
        /// planar projection -- this has to be the WHOLE image across the
        /// WHOLE quad, not tiled), offset slightly along the wall's own
        /// normal so its alpha blending never z-fights the coplanar Cracked
        /// quad behind it.
        /// </summary>
        private static void AddCrackDecal(MeshBuffer decals, in Wall wall, Vector2 centre, Vector3 normal)
        {
            float halfW = BulletHoleDecalWidth * 0.5f;
            float halfH = BulletHoleDecalHeight * 0.5f;
            Vector3 offset = normal * BulletHoleDecalOffset;

            Vector3 a = wall.At(centre.x - halfW, centre.y - halfH) + offset;
            Vector3 b = wall.At(centre.x + halfW, centre.y - halfH) + offset;
            Vector3 c = wall.At(centre.x + halfW, centre.y + halfH) + offset;
            Vector3 d = wall.At(centre.x - halfW, centre.y + halfH) + offset;

            decals.AddQuad(a, b, c, d, normal,
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f),
                new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f));
        }

        /// <summary>A wall with a rectangular opening in it: strips each side, a sill and a header.</summary>
        private static void AddWallAround(MeshBuffer buffer, in Wall wall, float y0, float y1,
            Vector3 normal, float holeU0, float holeU1, float holeY0, float holeY1)
        {
            AddWallPanel(buffer, wall, wall.U0, holeU0, y0, y1, normal);
            AddWallPanel(buffer, wall, holeU1, wall.U1, y0, y1, normal);
            AddWallPanel(buffer, wall, holeU0, holeU1, y0, holeY0, normal);
            AddWallPanel(buffer, wall, holeU0, holeU1, holeY1, y1, normal);
        }

        /// <summary>
        /// A floor slab with real thickness and a stairwell hole through it.
        ///
        /// THE THICKNESS IS NOT DECORATION. Two coincident faces at one y let
        /// the staircase from the floor above bleed through what should read
        /// as a solid ceiling -- confirmed in a screenshot in the Godot build,
        /// not argued about -- so the underside sits a slab below the walkable
        /// top and a collar closes the gap around the opening.
        /// </summary>
        private static void AddSlab(MeshBuffer buffer, float x0, float x1, float z0, float z1,
            Rect hole, float topY)
        {
            float bottomY = topY - BuildingInterior.SlabThickness;
            AddSlabFace(buffer, x0, x1, z0, z1, hole, topY, Vector3.up);
            AddSlabFace(buffer, x0, x1, z0, z1, hole, bottomY, Vector3.down);
            AddHoleCollar(buffer, hole, bottomY, topY);
        }

        /// <summary>
        /// The vertical edges of a slab's opening, so it reads as a hole cut
        /// through solid material rather than a gap between two floating
        /// sheets. Each face points INTO the opening, which is the only side
        /// anyone ever sees it from -- the other faces solid slab.
        /// </summary>
        private static void AddHoleCollar(MeshBuffer buffer, Rect hole, float bottomY, float topY)
        {
            if (hole.width <= 0.0f || hole.height <= 0.0f)
            {
                return;
            }

            AddFace(buffer, Vector3.right,
                new Vector3(hole.xMin, bottomY, hole.yMin), new Vector3(hole.xMin, bottomY, hole.yMax),
                new Vector3(hole.xMin, topY, hole.yMax), new Vector3(hole.xMin, topY, hole.yMin));
            AddFace(buffer, Vector3.left,
                new Vector3(hole.xMax, bottomY, hole.yMin), new Vector3(hole.xMax, bottomY, hole.yMax),
                new Vector3(hole.xMax, topY, hole.yMax), new Vector3(hole.xMax, topY, hole.yMin));
            AddFace(buffer, Vector3.forward,
                new Vector3(hole.xMin, bottomY, hole.yMin), new Vector3(hole.xMax, bottomY, hole.yMin),
                new Vector3(hole.xMax, topY, hole.yMin), new Vector3(hole.xMin, topY, hole.yMin));
            AddFace(buffer, Vector3.back,
                new Vector3(hole.xMin, bottomY, hole.yMax), new Vector3(hole.xMax, bottomY, hole.yMax),
                new Vector3(hole.xMax, topY, hole.yMax), new Vector3(hole.xMin, topY, hole.yMax));
        }

        /// <summary>
        /// One face of a slab, as up to four strips around its opening. The
        /// Rect is in X and Z, so its own y is a z. Ported from
        /// _add_floor_with_hole: east and west take the slab's full depth and
        /// north and south fill only what is left between them, so no strip is
        /// ever emitted twice.
        /// </summary>
        private static void AddSlabFace(MeshBuffer buffer, float x0, float x1, float z0, float z1,
            Rect hole, float y, Vector3 normal)
        {
            float hx0 = Mathf.Clamp(hole.xMin, x0, x1);
            float hx1 = Mathf.Clamp(hole.xMax, x0, x1);
            float hz0 = Mathf.Clamp(hole.yMin, z0, z1);
            float hz1 = Mathf.Clamp(hole.yMax, z0, z1);

            if (hole.width <= 0.0f || hole.height <= 0.0f)
            {
                // No opening at all, so one quad rather than four strips.
                AddFace(buffer, normal, new Vector3(x0, y, z0), new Vector3(x1, y, z0),
                    new Vector3(x1, y, z1), new Vector3(x0, y, z1));
                return;
            }

            AddQuadIfReal(buffer, normal, x0, hx0, z0, z1, y);
            AddQuadIfReal(buffer, normal, hx1, x1, z0, z1, y);
            AddQuadIfReal(buffer, normal, hx0, hx1, z0, hz0, y);
            AddQuadIfReal(buffer, normal, hx0, hx1, hz1, z1, y);
        }

        private static void AddQuadIfReal(MeshBuffer buffer, Vector3 normal,
            float x0, float x1, float z0, float z1, float y)
        {
            if (x1 - x0 <= 0.0001f || z1 - z0 <= 0.0001f)
            {
                return;
            }
            AddFace(buffer, normal, new Vector3(x0, y, z0), new Vector3(x1, y, z0),
                new Vector3(x1, y, z1), new Vector3(x0, y, z1));
        }

        /// <summary>
        /// A quad UV'd in metres from the two world axes it actually lies in,
        /// so a floor slab and a stair riser read at the same scale as the
        /// street outside without anyone working out coordinates by hand.
        ///
        /// Corner order does not matter -- AddQuad winds to the normal.
        /// </summary>
        private static void AddFace(MeshBuffer buffer, Vector3 normal,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            buffer.AddQuad(a, b, c, d, normal,
                Uv(a, normal), Uv(b, normal), Uv(c, normal), Uv(d, normal));
        }

        /// <summary>
        /// The same quad, plus a second copy wound the other way so it also
        /// faces (and renders/collides) from behind -- EXPLICIT BUG REPORT:
        /// "when shooting windows from inside the building the are not
        /// breaking." Every other wall surface in the city is genuinely
        /// solid (the interior gets its own separate inward-facing pass, see
        /// BuildBuilding's own structures/darkGlass/litGlass split), but
        /// glass is a single, one-sided quad at the outward facade with
        /// nothing behind it -- confirmed directly: the whole city (ground,
        /// walls, glass) shares ONE non-convex MeshCollider
        /// (CityStreamer.cs), and this project's own DynamicsManager.asset
        /// has queriesHitBackfaces off (Unity's own default), so
        /// Physics.Raycast against a triangle's back face silently reports
        /// no hit at all. A shot from outside hits the pane's front face and
        /// works; the identical shot from inside hits its back face and is
        /// invisible to PhysX -- exactly the reported asymmetry.
        /// GlassDamage.TryPaneAt itself is already direction-agnostic (see
        /// its own doc comment), so making the RAYCAST able to land from
        /// either side is the whole fix; nothing downstream needs to change.
        /// Only used for glass -- ordinary walls already have their own
        /// separate interior pass and doubling THEIR panel strips too would
        /// just add redundant, potentially z-fighting geometry they don't
        /// need.
        /// </summary>
        private static void AddDoubleSidedFace(MeshBuffer buffer, Vector3 normal,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // Same corners both times, on purpose -- MeshBuffer.AddQuad's own
            // doc comment: winding is corrected FROM the normal, not trusted
            // from vertex order, so simply negating the normal (rather than
            // also reversing a/b/c/d) is already enough to wind the back
            // copy correctly.
            AddFace(buffer, normal, a, b, c, d);
            AddFace(buffer, -normal, a, b, c, d);
        }

        private static Vector2 Uv(Vector3 v, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f)
            {
                return new Vector2(v.x, v.z);
            }
            return Mathf.Abs(normal.x) > 0.5f ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y);
        }

        private static void BuildBridges(CityWorld world, SkybridgeLayout bridges, BuildingSite site,
            Vector3 origin, MeshBuffer decks)
        {
            // Only east and north. Every pair has exactly one building that
            // sees it as east-or-north, so each walkway is built once -- the
            // west and south lookups exist for queries, not for geometry.
            BuildSpan(bridges, site, SkybridgeLayout.EastNeighbour(world, site), true, origin, decks);
            BuildSpan(bridges, site, SkybridgeLayout.NorthNeighbour(world, site), false, origin, decks);
        }

        private static void BuildSpan(SkybridgeLayout bridges, BuildingSite from, BuildingSite to,
            bool alongX, Vector3 origin, MeshBuffer decks)
        {
            if (!to.Exists)
            {
                return;
            }

            List<Skybridge> spans = bridges.BridgesBetween(from, to);
            if (spans.Count == 0)
            {
                return;
            }

            foreach (Skybridge bridge in spans)
            {
                float y = bridge.Height - origin.y;
                Vector3 min;
                Vector3 max;

                if (alongX)
                {
                    // Centred on the two buildings' shared middle in Z, which
                    // keeps the deck on the overlap rather than hanging off a
                    // corner when the neighbour is offset.
                    float centreZ = (float)((from.CentreZ + to.CentreZ) * 0.5 - origin.z);
                    min = new Vector3((float)(from.MaxX - origin.x), y - BridgeThickness,
                        centreZ - BridgeWidth * 0.5f);
                    max = new Vector3((float)(to.MinX - origin.x), y, centreZ + BridgeWidth * 0.5f);
                }
                else
                {
                    float centreX = (float)((from.CentreX + to.CentreX) * 0.5 - origin.x);
                    min = new Vector3(centreX - BridgeWidth * 0.5f, y - BridgeThickness,
                        (float)(from.MaxZ - origin.z));
                    max = new Vector3(centreX + BridgeWidth * 0.5f, y, (float)(to.MinZ - origin.z));
                }

                // A neighbour whose footprint starts before this one ends would
                // give an inside-out box, which renders as a hole rather than
                // an error.
                if (max.x <= min.x || max.z <= min.z)
                {
                    continue;
                }

                decks.AddBox(min, max, decks, includeBottom: true);
            }
        }
    }
}

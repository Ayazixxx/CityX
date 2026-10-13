# Expressways, exits and the ring road (2026-10-12)

Asked: "make the city look much better ... im thinking highways with exits. would probably have study how real life cities utilize highways
and how their exits work", then "highways dont just connect cities they often times flow through them or around them. though highways do
have an end at some point. just research real life highways".

## What the research found

**How real cities use them.** An urban freeway does three things: it runs *through* the city on a structure (an elevated viaduct over a
surface street that becomes its frontage/service road: Tokyo's Shuto Expressway over the old canals and avenues, Bangkok's expressways, the
Gardiner in Toronto, the old Central Artery in Boston, Seattle's Alaskan Way viaduct, the FDR on Manhattan's east side), it runs *around* it as
an orbital/ring road (Beijing's 2nd-6th rings, the Périphérique round Paris, the M25, Berlin's A10, Houston's loops, Atlanta's I-285,
Moscow's MKAD), and it *ends*: a freeway's terminus is a surface junction where the carriageway descends to grade and the traffic is fed into
an arterial or an at-grade intersection (the "stub ends" of many US urban freeways, the end of the M11 in London, the Embarcadero Freeway's
former end in San Francisco). Ring roads sit a few kilometres out from the centre (the Périphérique ~4 km, Beijing's 2nd ring ~3 km, 3rd ring
~6 km); the through-routes radiate from the centre and cross the ring.

**Exits.** Urban interchanges are about one mile apart (the Caltrans Highway Design Manual and WSDOT Design Manual both give one mile as the
minimum urban interchange spacing; rural two miles). The standard, most compact form is the **diamond** (WSDOT: "the most generally
applicable and serviceable" configuration): one ramp in each quadrant, each leaving the freeway in the direction of travel, running alongside
it as a deceleration lane, then angling down to meet the cross street at a signalised or stop-controlled junction. Ramp grades are held to
about 6% (AASHTO's Green Book allows 3-5% desirable, up to 6-8% in constrained urban cases; most state manuals cap at 6%), the ramps are one
lane 12-16 ft wide plus shoulders, and the exit is signed by an overhead gantry ("guide sign") a few hundred metres ahead and at the gore.

**The structure.** Elevated urban viaducts: a deck 1.0-1.5 m deep on hammerhead piers (a single column on the median of the street below
carrying a cross-head the width of the deck) spaced 20-55 m apart, with a minimum vertical clearance of 4.9-5.5 m (16.5 ft US; 5.3 m UK; 4.5-5.5 m
Japan) over the road below; concrete parapets ~1 m high on each edge; the service road beneath carries local traffic and the piers stand on
its centreline so both halves stay open.

**Video-game cities.** GTA V's Los Santos has a full freeway loop with signed exits, Watch Dogs' Chicago keeps the Kennedy and the elevated
L, Cyberpunk's Night City stacks expressways over the street grid, Marvel's Spider-Man runs the FDR along the East River. All of them use
the elevated freeway as a *landmark and a route*: a way to see the city from above at speed, and a thing to run under.

**The lore.** Terraplex 9 was emptied in an instant by the rapture (`docs/GAME_DESIGN.md`, Lore). The expressways are where that shows most:
the last traffic jam is still on the decks, cars nose to tail where it stood and sparse between, a few skewed across the lanes or burnt out;
the ring road and the through-routes lead out of the district into the rest of the endless city.

## What was built

### The plan (`World/Plan`, pure and deterministic; harness `tools/cityplan`)
- **Decks** (`PlanGeometry`): every `RoadClass.Highway` edge is now an elevated expressway. The ground ribbon stays as its **service road**;
  a deck rides above it. `DeckTop[node]` = the highest ground under any of the node's expressway spans (sampled every `DeckSampleStep` 8 m) +
  `PlanConfig.DeckClearance` (7.0 m, the game's 1.3x) + `DeckThickness` (1.2 m); two decks meeting share the node's height; the straight run
  between two nodes therefore clears everything under it. `EdgeGeometry.Elevated/DeckA/DeckB/DeckTrimA/DeckTrimB/MaxGround/DeckTopAt`;
  `JunctionGeometry.DeckPlate/DeckTop` where expressways meet (ground junctions with side streets are not plated on the deck: the deck runs over them).
- **Ends**: a node with exactly one expressway edge is a terminus; its deck top is the ground + `RoadLift`, and the nodes within
  `DescentLength` (250 m x the district's scale) descend on a straight grade toward it (never under the ground + 0.3 m), so the expressway
  comes down to meet its last surface junction like a real one.
- **The ring road** (`RoadNetwork.SeedOrbital`): after the radials, an orbital is seeded round the origin at `PlanConfig.OrbitalRadius`
  (1100 m x scale^2), grown as ring items (`Item.Ring/RingCentre/RingRadius/RingTurn`) whose direction is the circle's tangent plus a drift
  correction (`RingDirection`), closing when it snaps onto a node it has already made. The radials cross it, so there are interchanges on it.
- **Interchanges** (`Interchanges.Place`): candidates are nodes where exactly two (non-bridge) expressway edges meet at least one other road, in
  built-up country (population >= 0.18), taken in a seed-hashed order, no two closer than `ExitSpacing` (700 m x sqrt(scale)). For each of the
  crossing's two expressway edges and each side: the ramp **starts** on the deck edge `RampAlong` (150 x scale) along the expressway (the walk
  continues through side-street junctions, stops at bends, expressway junctions and ends), runs alongside the deck (`RampTaper` 60 m, lengthened
  until the drop to the ground is within `MaxGrade` 6.5%), then angles down to the **foot** on the cross street's kerb `RampToStreet` (48 m) from the
  crossing. Rejected if it would cross a road, land near a junction (`ClearOfJunction`), cross water, or still be steeper than 1.15 x the grade.
  `RampPlan` = Start/Mid/Foot, `Leaving` (off- or on-ramp from right-hand traffic), `Exclusion` (a convex hull no lot may be cut over --
  `LotSubdivider.EmitLot` drops any lot `Interchanges.Touches`) and `Sliver` (the land between ramp, expressway and street: paved as a car park).
  Ramps are **geometry only**, not graph edges: they change no block, no lot, no pedestrian route. `Interchanges.Rejections` counts why candidates fail.
- `PlanWorld.TryFindSpawn` no longer spawns on an expressway or bridge node; `IndexAll` indexes decks, ramps and slivers by chunk (`RampsIn`).

### The mesh (`View/CitySurfaceBuilder`)
- `BuildDeck`: the carriageway (clean asphalt, `AsphaltLook(Highway)`) at `DeckHeight` (the straight run, never under the service road), the
  slab's underside and side faces, a parapet along each edge (`DeckParapet` 1.1 m, into the guard-rail collider buffer, near chunks only), a
  hammerhead pier every `PierSpacing` 32 m (built by the chunk containing its centre, skipped where the deck is down on the ground), lane paint
  with edge lines (`PaintLanes(..., edgeLines: true)`), and the jam (`AddDeckWrecks`: a `PatchNoise` field 170 m across decides where the jam
  stood -- 60% of lane slots there, 5% elsewhere -- cars face their lane's way, 8% skewed, `CityProps.BurntShare` 9% burnt).
- The service road under a deck is drawn and painted as an arterial (worn asphalt); the deck's own clean asphalt is above it.
- `BuildDeckPlate`: where expressways meet, the plate at the deck's height with its underside and edges.
- `BuildRamp`: two stretches (mitred at Mid), a sloped slab `RampSlab` 0.9 m deep; a **viaduct** on small piers every 15 m where it is more than
  `RampEmbankmentClearance` 1 m up, an **embankment** (sides straight down to the ground) where it is low; parapets, white edge lines, a car on
  about one piece in nine. `BuildInterchangeLand`: the sliver inset 1.5 m, laid in asphalt above the paving, stalls painted in rows along its
  longest side with an aisle between pairs, cars in 30% of them; the **exit gantry sign** (`CityProps.AddGantrySign`) over the deck where an off-ramp
  leaves it: two posts, a beam, a green panel lettered in white bars over the ramp's side.
- `AddStreetWrecks`: on ordinary streets the odd car at the kerb (5.5% of 10 m slots, one in ten of those crashed across the lanes) and now
  and then a row of jersey barriers across a street (3.5% of streets over 40 m): the evacuation's last cordons.
- `CityProps` (`View/CityProps.cs`): the shared prop library -- cars (sedan/hatch/van, 12-colour palette, burnt), barriers, rubble, benches,
  planters, bollards, dry fountains, kiosks, gantry and parking signs -- all boxes turned about the vertical (`AddTurnedBox`), chunk-local.

### Numbers (seed 74920318, the game's varied 10 km): 69 km of expressway, 28 interchanges (46 ramps, one per 2.5 km), 7 ends, 16 junctions
of expressways, the ring road at 2.2 km with 82 spans; steepest ramp 7.3%, least drop 4.0 m. At the classic 5 km scale the tests pin: 42 km,
21 interchanges (28 ramps), 9 ends, steepest 7.1%. The seed was rerolled to 74920318 for this pass (city-generation change).

Two rules found by the tests (2026-10-12): no exit where the deck is descending to an end (`ChainDistanceToEnd`: a ramp whose start is within
`DescentLength` of a terminus along the chain, either way, is dropped -- the first version put a ramp on a deck a metre off the ground), and
the grade is judged on the deck's real height (`DeckTopAt` mirrors `PlanGeometry.ComputeDeckTops`: over a dip the deck is well above the
clearance, and the taper is lengthened for the real drop) with a `MinDrop` of 3 m (on a hillside the uphill street can be level with the deck).

## Tests
`CityFeaturesTests` (EditMode): decks never dip under their service road and mostly clear the ground by the full clearance; an expressway end is
on the ground; the ring road has spans in all four quadrants; exits are spaced and ramps climb at <= 7.8% from the deck's height to the ground;
no lot touches a ramp; the spawn is off the expressway; the deck chunk's road surface reaches the deck's height with parapets, an underside and
a lighter far version; there are cars on the deck; an exit has its sign and its slope; `PlanGeometry.RoadLift == CitySurfaceBuilder.RoadLift`.
`CityFeaturesCapture` (PlayMode, Explicit, `-Graphics`) renders the deck, the exit and the ring road to `Logs/CityShots/city-*.png`.

## Not done / next
- The ramp's foot meets the cross street's kerb but the pavement is not cut for it yet (the ramp lies over it); a kerb cut-out where the foot lands.
- Loops and cloverleaf quadrants, stacked interchanges where two expressways cross (today they meet on one plate at one height).
- A route for cars (there are none yet): the decks are driveable by the player on foot and by jetpack.
- A sound: wind on the deck, the hum under it.

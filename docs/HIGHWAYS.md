# Expressways: corridors, cloverleafs and exits (rebuilt 2026-10-13)

Asked first (2026-10-12): "highways with exits ... study how real life cities utilize highways and how their exits work"; "highways dont
just connect cities they often times flow through them or around them".

Then, of that first version (2026-10-13): "a four way intersection on a bridge/highway section which is very glitched and doesnt work in
real life. it also goes downward and sinks into a street blocking the street below. it seems all the highways are above streets parallel to
them which doesnt happen that often in real life, and if it does then it is higher not typically right above ... the exits and entrances need
to curve and slope into the highway fully, the ones i came across were cut off"; "the highway exits and entrances should come from and go
lead to a street"; "research the highways on the saints row and gta maps"; "research how highways are efficiently placed throughout cities in
real life too"; "research highway exits and entrances too. they are sometimes elevated sometimes not sometimes curved sometimes straight".

And of the rebuild (2026-10-13, with screenshots): "the highway entrances/exits are not connecting to the highways properly. there is an
edge where it is clearly cut off instead of transitioning into the major freeway"; "it seems like the highway entrances/exits only connect
to other highways. you should mostly be entering the highway from a street and exiting onto one as well".

The first version laid a deck over the boulevard each district centre grew along, so every expressway hung right over a street of its own,
two met on one flat plate (the "four way intersection"), an end came down onto the road under it, and the ramps were two straight pieces.
Everything below replaced it.

## What the research found

**Where cities put them.** Urban freeways are laid in their own right-of-way, not on top of the street grid: along rivers, rail lines and
waterfronts, through cheaper fringe land and round the edge of downtown, crossing the streets at whatever angle that gives (Horwood, *The
Nature of Urban Freeway Systems*, HRB Bulletin 230, 1959). A city's system is radials plus an inner loop and an outer ring a few kilometres
apart. Elevated where the ground or the land below demands it, depressed or at grade where it can be (TxDOT Roadway Design Manual 8-4, urban
design elements). A viaduct right over a street does exist (Shanghai's Yan'an and North-South elevated roads over their boulevards, Tokyo's
Shuto over canals and avenues) but it is the exception, and stands high when it does.

**Games.** GTA V's Los Santos: a handful of freeways (Del Perro, Olympic, La Puerta, Elysian Fields, Los Santos/Palomino) cut through the city
in their own corridors, elevated downtown and on embankments outside, meeting each other only at big system interchanges, with exits every
kilometre or so (the GTA wiki's highway map). Saints Row: The Third's Steelport: one highway looping the islands along their shores plus a
cross link. Both compress a real system into a game map and keep its rules: no flat crossings, ramps that peel off and curve down to a street.

**Interchanges and exits.** "All connections to freeways are by traffic interchanges" (Caltrans Highway Design Manual 501.2): two freeways
never meet on the flat. Urban interchanges are about a mile apart, two miles from a freeway-to-freeway one (HDM 501.3). Local-street
interchanges come in a few families (HDM 502.2; WSDOT Design Manual 1360; TxDOT 15-7 ramp types; the kurumi.com interchange glossary):
- **Diamond** (WSDOT: "the most generally applicable and serviceable"): one ramp per quadrant, leaving in the direction of travel, running
  alongside as a deceleration lane, then angling to meet the cross street square at a junction. Mostly straight with a curve at the end.
  A **split diamond** (Caltrans L-3) lands its ramps on the next streets along in a tight grid.
- **Loop** ramps (a partial cloverleaf, parclo): a left turn made by turning right three quarters of a circle; the loop merges alongside the
  cross street. Curved all the way.
- **Slip** ramps onto a street running alongside the freeway (a Texas frontage road). Straight.
- Ramps stand on a viaduct where they are high and come down on fill where they are low; grades at most 8% (HDM 504.2), 5-7% usual.
- Where two freeways cross: a **cloverleaf** (four loops for the left turns, four outer connectors for the right turns) or a **stack** of
  flyovers (Practical Engineering on Texas's tall stacks). Every movement is served; the connectors weave round each other at levels.

**The lore.** Terraplex 9 was emptied by the rapture (`docs/GAME_DESIGN.md`): the decks are empty, the routes run out of the district into
the rest of the endless city and are closed there by barriers.

## What was built

### The plan (`World/Plan`, pure and deterministic; harness `tools/cityplan`)
The expressways are their own layer over the finished street network (`ExpresswayPlanner`, `.Ramps`, `.Furniture`; data in
`Expressways.cs`). The growth spines are now `RoadClass.Boulevard`, plain wide streets with no deck. Trig comes only from `DMath` (plain
arithmetic, the same on every machine) and every curve is a `DubinsPath` (`Dubins.cs`), so server and clients lay the same roads.

1. **Corridors.** A ring round the centre at `PlanConfig.RingRadius` (1100 m x scale^2, swelling with two hashed harmonics) and
   `PlanConfig.ThroughRoutes` (2) routes right across the region at spread-out angles, offset off the centre. Each control point picks one of
   9 positions sideways (Viterbi, `Choose`): a cost for water, steep ground, the very centre and population (`PlaceCost`), for jumping
   sideways, and three times the share of the stretch that would run ALONG a street (`AlongStreetShare`). A centripetal Catmull-Rom spline,
   resampled every 6 m, clipped to the region; the ends get barriers. Result: under 2% of main line runs along a street.
2. **Height.** Every sample needs: an embankment (0.9 m) in open country, a viaduct in the city (population >= 0.22), `StreetClearance`
   9.5 m under the slab over a street it crosses, `ParallelClearance` 12.5 m over one it runs along, a bridge over water, `LevelGap` 9 m over
   an earlier route; then `SmoothProfile` (a cone envelope at 4% and rounding), so it climbs gently and never dips into a street.
3. **Cloverleafs** where two routes cross (`SystemInterchanges`): the later passes over the earlier. The four loops first (radius 58-88 m,
   sized to the corner's angle: `TangentReach`), then the four outer connectors swinging round the loops through a point just beyond each
   (`RoundTheLoop`; a single curve on the same tangents would take a square kilometre). A connector that cannot be laid without a clash is left
   out. Each connector: a taper beside its main line, the curve, a taper beside the other; level with each line to its gore nose, sloping
   between. The land inside is the interchange's (`Infields`, a hull): no lots.
4. **Exits** (`Exits`, `BuildExit`): where a street passes under a route at 50 degrees or more in built-up country; best streets first; no two
   within `ExitSpacing` (450 m x road scale: a downtown freeway's ramps every few blocks) on a route, none within `SystemSpacing` (400) of a
   cloverleaf, and their ramps' tapers clear of the cloverleaf's loops (0.35 of that). So most ramps a player meets come from and lead to a
   street: in the game's city street ramps outnumber the connectors about two to one. For each direction an off-ramp and an on-ramp, in a
   hashed order of shapes, each falling back to the next:
   - **diamond**: the off-ramp leaves before the street, curves down and meets the street square at its kerb (`Landing` 14 m straight across
     the pavement); the on-ramp the mirror image after it. Split: if the exit's own street has no room, the next street along (within
     `SplitReach` 450 m).
   - **loop**: the off-ramp leaves after the street and turns right about 250 degrees round the corner beyond it to run back along the street
     toward the expressway, merging at 20 degrees; the on-ramp peels off the lanes heading away and loops up into the main line before the street.
   - **slip**: onto a street running alongside the expressway at 20 degrees, where there is one.
   Every ramp: a 45 m TAPER that grows out of the main line's edge from a tip -- next to nothing wide where it meets the main line, widening
   (eased) to a whole lane where it curves away, its inner edge on the main line's edge throughout (`TaperSamples`; the first version stood
   a full-width square end against the main line) -- then the gore paved until 3 m wide, the main line's parapet opened where the lane is
   wide enough to drive; a Dubins curve; the landing. Level with the main line to the nose, then a smooth slope (no grade at either end, at
   most `RampGrade` 8%, Caltrans's limit).
   Where that even slope would pass low over another street, or through a deck laid before it at about its height, the ramp stays up over
   it and drops after it (`LiftOverRoads`: cones at 90% of the ramp grade round the clearance needed, rounded; a flyover where connectors
   cross). `Clear` then turns down anything that leaves the region, runs low over a road, comes down in a river, passes within
   `RampClearance` 5.3 m of another deck (US 16.5 ft is 5.0 m), or is too steep over any 12 m. Kerb cuts where ramps land.
5. **Land.** A lot beside a deck is cut back to `LotMargin` (5 m) from its edge rather than lost (`ExpresswayNetwork.CutBack`: a cut along each
   segment that reaches in, the lot keeping its own side; the cut edge is not a street front; a lot left with no street front is dropped); a lot
   reaching into an interchange's land is cut along that land's nearest side. So the city lines the motorway, as it does along Shanghai's and
   Tokyo's elevated roads, and only the land inside the interchanges stays open.
6. **Furniture** (`.Furniture`): piers every 36 m (24 on ramps), shifted up to 12 m to stand clear of roads and lower decks, twin columns under
   a wide deck; a gantry sign before every diverge; barriers where a route leaves the region.

### The mesh (`View/CitySurfaceBuilder.Expressways.cs`)
Every deck kind is drawn by the same code: per segment two planar triangles (`PlaneThrough`, so a curve's pieces share their edges), the
slab's underside and sides (straight down to the ground where it is low: an embankment), parapets (`DeckParapet` 1.1 m, off where a ramp
abuts), a median barrier on main lines, lane paint (dashes by distance along; a ramp's outer edge line follows its taper in to the tip, its
inner line only where it has left the main line), piers built by the chunk holding their centre (none under a taper's narrow tip), gantry
signs and barriers. Kerb cuts subtract from the pavement and kerb faces.

### Numbers (seed 74102958, the game's varied 10 km)
Seed 70485213 after the 2026-10-15 rework (routes aimed at the outlying centres, loops one exit in eight): 32 km of main line (ring 13.7, a route through the city 11.5, a bypass 7.1), 2 cloverleafs, 24 exits with 66 diamond ramps, 21 loop ramps and 1 slip; the earlier hashed routes gave 5 cloverleafs and 20 exits on this seed. The 2026-10-13 numbers follow.

34 km of main line (ring 14 km, routes 10 and 10 km), 35% of it over a road, 0.8% along one; 5 cloverleafs (all 20 loops and all 20 outer
connectors); 25 exits with 80 ramps to and from streets (38 diamond, 35 loop, 7 slip) against those 40 connectors; steepest ramp 7.8%;
2770 piers. The tests' classic city (seed 61839025), five cloverleafs crowded inside a small ring: 13 exits. The world seed was rerolled to
72604913 for the rebuild and to 74102958 for the tapers and the denser exits.

## Tests and pictures
`CityFeaturesTests` (EditMode, classic seed 61839025): a ring and routes across; no flat crossing and every crossing a cloverleaf; main decks
clear every road (higher over one they run along) and climb gently; every ramp meets a street at its kerb and its main line beside it, level;
ramps curve and slope smoothly, come in at least two shapes, loops turn most of the way round; every ramp and connector grows out of its
main line from a taper tip; no deck runs low over a street or through another; exits spaced and clear of the cloverleafs; in the game's own
city street ramps outnumber connectors 1.6 to 1 or more; no lot under a deck or in an interchange's land, and many cut back beside one; no pier in a
road; the spawn in the open; mesh checks for the deck chunk, a pier and an exit. `ExpresswayGeometryTests`: DMath, Dubins words, splines,
profiles, and `CutBack` on a straight deck and an infield.
`ExpresswayCapture` (PlayMode, Explicit, `-Graphics`) renders `Logs/CityShots/xw-*.png` in the game's own varied city: on the deck, under
it, every ramp shape from the air, the deck, the street and close to its taper tip, a cloverleaf and where its connectors leave and join, the
ring and a closed end. Map views: `dotnet run -c Release
--project tools/cityplan -- <seed> 0 <px> out.png <x> <z> <half>` (0 = the game's region), `interchanges <seed>` lists them,
`CITYX_XW_DEBUG=1` prints why connectors were lost.

## Not done / next
- Cars and traffic (there are none: the rapture).
- Stacks and direct connectors where two expressways cross at a tight angle (a 35-degree crossing gets its four loops but few of its outer
  connectors, the others finding no way past the long loops). Collector-distributor lanes.
- The land inside interchanges is the city's plain paving; grass or wasteland would read better.
- Sound: wind on the deck, the hum under it.

## Sources
- Caltrans Highway Design Manual, chapter 500 (traffic interchanges): https://dot.ca.gov/-/media/dot-media/programs/design/documents/chp0500-032020.pdf
- TxDOT Roadway Design Manual 8-4 (urban freeway design elements): https://www.txdot.gov/manuals/des/rdw/chapter-8--freeways--4r-/8-4-urban-design-elements.html
- TxDOT 15-7 ramps and direct connectors, types of ramps: https://www.txdot.gov/manuals/des/rdw/chapter-15-grade-separations-and-interchanges-/15-7-ramps---direct-connectors-/15-7-1-types-of-ramps.html
- WSDOT Design Manual 1360 (interchanges): https://wsdot.wa.gov/publications/manuals/fulltext/M22-01/1360.pdf
- Horwood, The Nature of Urban Freeway Systems (HRB Bulletin 230): https://onlinepubs.trb.org/Onlinepubs/hrbbulletin/230/230-003.pdf
- Practical Engineering, why Texas interchanges are so tall: https://practical.engineering/blog/2024/8/19/why-are-texas-interchanges-texas-so-tall
- Interchange glossary: https://www.kurumi.com/roads/interchanges/gloss.html
- Elevated highways: https://en.wikipedia.org/wiki/Elevated_highway ; https://en.wikipedia.org/wiki/Yan%27an_Elevated_Road ; https://en.wikipedia.org/wiki/Expressways_of_Shanghai
- GTA V: https://gta.fandom.com/wiki/Numbered_Highways_in_GTA_V ; https://gta.fandom.com/wiki/Los_Santos_Freeway_(HD_Universe)

## How highways flow through cities (research, 2026-10-15)

Owner, of the plan maps: "whats up with these highway clusters where they just spin around eachother and go nowhere is this common in real
life? ... this is why i want you to research how humans utilize highways and when they are used ... also research how highways flow
throughout cities."

**What the circles were.** Two things: cloverleafs (a loop for each of the four left turns) where two expressways crossed, and loop exit
ramps (a partial cloverleaf's). Both are real. The cloverleaf is the 1930s-70s American answer to a freeway crossing; its loops put merging
and leaving traffic in the same lane (weaving), so cities stopped building them and rebuild old ones as stacks and turbines (flyovers), or
"cloverstacks" (two loops kept, two replaced by flyovers), or raindrop diamonds. Exits are mostly diamonds: about six in ten of a state's
interchanges (Alabama's inventory, NCHRP), partial cloverleafs one in six, and a city of ten kilometres has two or three freeway-to-freeway
interchanges, two miles apart (Caltrans HDM 501.3). Our first version had nine of them and loops on half the exits: too many of both.

**How a system is shaped.** Radials bring the intercity roads to the city; an inner loop (a distributor) rings the downtown and the
radials end on it or pass through it; an outer belt a few kilometres out carries through traffic round the city and ties the radials
together, each radial passing a suburb or a satellite town with an exit about every mile (the 1944 US interregional plan's belt lines;
Horwood 1959; the TRB's radial/circumferential/distributor studies). Paris: the autoroutes end at the Périphérique's gates; Tokyo's Shuto:
the C1 loop round the centre and radials out to the C2 and the outer ring; Houston: the I-610 loop, Beltway 8 outside it and the radials
through both; Beijing: rings two to six and the radials.

**How a freeway ends.** On another freeway with a trumpet (one loop and three direct ramps), a directional T (all direct, the "New England
Y") or a triangle; or by coming down to grade and becoming an arterial. One that just stops is a cancelled freeway.

**Changed today:** the routes across are aimed at the outlying centres (two on opposite sides share one route; a lone one well outside the
ring gets a bypass along its tangent; nearer in, none), so the crossings with the ring are few; exits beside those centres come first; loops
lead on one exit in eight. **Next:** a radial that ENDS on the ring with a trumpet or a directional T instead of crossing it, and flyovers
(a stack) in place of the loops at the busiest crossing.

Sources: Interchange and Cloverleaf interchange (Wikipedia); kurumi.com's cloverleaf and trumpet pages; TxDOT RDM 15-3 (types of
interchanges); IDOT's I-57/IL-16 raindrop diamond; TRB Circular 430 and NAP 27198 chapter 6 (Alabama's 63 % diamonds); roads.org.uk on
triangle junctions; the FHWA's interregional-highways history; Horwood, HRB Bulletin 230.

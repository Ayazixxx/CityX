# The layered city (Chongqing) and the railway: a plan (2026-10-15)

**Nothing in here is built yet.** The owner asked for it on 2026-10-15: *"I want to make our city more chongqing like with layers to stand out
from the other mass amount of procedurally generated city games being made right now with claude code. research chongqing city architecture
and how they utilized the layered city approach. also start planning for the railway/train system."* And the rule that governs all of it:
*"make everything walkable and functionable as a pedestrian not just look good."*

Seven reference images came with it: four of a dark, foggy concrete city seen from below (stairs climbing between towers, footbridges at
several heights, a catwalk along a slab, cables across the gap); a photo of a footbridge between two apartment blocks at mid height landing
on a stair tower, a playground in the courtyard under it; and two of Liziba, the monorail on its viaduct threading between hillside towers
and through one of them. The earlier direction (`docs/` history, 2026-08-10) said of the dark concept art: *"the architecture not the
lighting"*; this plan keeps to the architecture and asks about the fog at the end.

## 1. What Chongqing actually does

Chongqing stands on ridges between two rivers; the ground drops hundreds of metres within a few hundred metres sideways, so there is no single
ground level and the grid never worked there. What came of that, from the research:

- **A building has front doors on several floors**, one per road it touches. The plaza at Kuixing Lou looks like ground and is the roof of a
  22-storey building, with the other road 22 floors below ("the plaza that doesn't know which floor it is on").
- **Hongya Dong** is an 11-storey stilt complex down a river cliff: you walk in from the upper road, down through it, and out onto the lower
  road 75 m below; a 12 m steel stair joins it to the bridge beside it.
- **Stair streets.** Shibati (the Eighteen Steps) and its lanes drop several storeys in 400 m: stairs that are streets, with shops on them,
  and walkways that pass through buildings.
- **Footbridges between apartment blocks** at the third or fourth floor, landing on a stair tower or on a block's open walkway (the owner's
  photo): the bridge is the street for those floors; the ground under it is a courtyard.
- **Escalators as streets.** The Huangguan escalator is 112 m long and climbs 53 m, from the railway station up to the metro; the Wushan
  network stacks 21 escalators, 8 lifts, 4 moving walkways and footbridges up a 242 m hillside.
- **The monorail (Line 2):** 30 km, 25 stations, 2.4 km of it underground under the downtown, the rest on a viaduct along the Jialing river's
  cliff and between the hillside towers. **Liziba** station is inside floors six to eight of a 19-storey block (built round the line, with
  sound-absorbing walls); the Niujiaotuo interchange is an open-sided corridor from one line to the other above it. Four- to eight-car
  straddle-type trains. Line 3 crosses it, elevated too.
- **Green on every slope.** Retaining walls and embankments are overgrown (the owner left nature out of the World Gacha pass, see section 5).

Sources: Hongya Cave (Wikipedia), parametric-architecture.com on the "8D city", Liziba station (Wikipedia; the Chongqing municipal site),
Line 2 (Wikipedia; urbanrail.net), Huangguan Escalator (Wikipedia), elevatorworld.com on the Wushan escalators, the Shibati guides.

## 2. What we already have, and what is missing

Have: hillside terrain with streets that tilt and terrace (`docs/HILLSIDE`, `CITY_SCALE`), stairs in every building up to a roof hatch,
climbing and mantling onto any ledge, window vaults, apartment blocks with open walkways along one side and balconies on the other, the
expressways on viaducts with piers (`ExpresswayPlanner`: a pure layer over the street plan, which is exactly the shape a railway needs), a
streamer that draws near chunks in full and far ones as shells.

Missing, in Chongqing's terms: a second ground level anywhere; stairs that are streets; footbridges between buildings; outside stairs on
buildings; any rail.

## 3. The plan, in phases (each one walkable, each with a test that walks it)

Every phase is pure plan data first (a function of the seed, so every machine lays the same thing), then mesh and collider, then a PlayMode
test that puts the 2.6 m body on it and walks it end to end, then a capture to look at.

**L1. Two grounds (the terrace street).** Where the ground is steep enough (a drop of 8 m or more across a block), the plan lays an UPPER
street and a LOWER street on either side of a retaining wall instead of one tilted street. The buildings between them get a door on both: the
lower street's door at their ground floor, the upper street's at whatever floor meets it (a short building's roof becomes the upper street's
pavement: Kuixing Lou). Between the two streets every 120-180 m, a **stair street** 4-5 m wide with landings every 3 m of rise, handrails,
shops' doors onto its landings (Shibati). This is the signature; it is also the hardest: the lot planner, the door planner and the stairs
inside the buildings all have to agree about which floor meets which street.

**L2. Footbridges.** Between two blocks facing each other across a street or a courtyard, at a floor both have (an apartment block's walkway
floor, an office floor's corridor), a concrete bridge 2.6-3 m wide with 1.2 m parapets, 6-10 m up; at each end either a door into the
building's floor or a stair tower down to the pavement, so a bridge is never a dead end. Pure: `FootbridgePlan.For(siteA, siteB)` from the
two seeds, only where both sides have a floor at that height, never across an expressway's line. Several heights in one view where the
blocks are tall (the concept art's "three or four levels of bridges"). Rail-free rooftops joined by short bridges where two roofs are within
a metre of each other.

**L3. Outside stairs.** Zigzag switchback stairs on the blank side walls of slabs (the concept art): street to roof, a landing at every
floor with a door into that floor's corridor or walkway. The same stair kit as inside (`BuildingStairs`), outdoors, with railings.

**L4. The railway.** An elevated line in its own corridor, laid like the expressways (ring and radials, in the plan, pure): a twin-track
viaduct 10-14 m up on piers (Line 2's viaduct), with a 1.2 m maintenance walkway and railing along it so the whole line can be walked
(the owner's rule; a bare monorail beam cannot be walked, so this is a metro-style deck carrying the beam). **Stations** every 700-900 m:
a 4 m platform each side, a roof, stairs down and a long escalator (walkable as a stair, moving later) to the street, a footbridge from the
platform into the nearest tall block's floor (every station is also an L2 bridge). **One Liziba every few kilometres:** where the line and a
tower of 12 floors or more share a chunk, the line passes through the tower at its 3rd-4th floor, the station inside it, the floors above and
below untouched. Trains come later: a timetable that is a pure function of the world clock (so every client sees the same train in the same
place without sending it), 4-car straddle trains you can board, which is the game's fast travel. First the line, the stations and the walk.

**L5. Green slopes.** Retaining walls and embankments overgrown; the owner left nature out of the World Gacha pass, so this waits for a yes.

## 4. Order and cost

L2 first (a few days: it is additive, cheap to generate, and reads as Chongqing at once), then L4's line and stations (the expressway code is
most of it), then L1 (the big one; a week or more, it changes the lot planner), L3 alongside L1, L5 last. Each is a world-seed reroll.
Performance: a footbridge is a few hundred triangles; the rail deck about the cost of an expressway (there are already 35 km of those);
stations a building's worth each.

## 5. Questions for the owner

1. **The fog.** The four concept images are dark and foggy; the earlier instruction was "the architecture not the lighting". Keep the game's
   current light and air, or go darker and foggier in the layered districts?
2. **Monorail or metro?** A straddle monorail's beam is not walkable; the plan above puts the monorail's beam on a walkable deck. Or a plain
   elevated metro with two tracks and a walkway, which is what you can walk along.
3. **How much of the city is layered?** Only the steep districts (Chongqing is layered where it is steep, flat where it is flat), or
   everywhere?
4. **Green slopes** (section 3, L5): yes or no.

## Built so far

**L2 footbridges (2026-10-15, `World/Footbridges.cs`, `View/CityMeshBuilder.Footbridges.cs`).** Between two ordinary blocks of three floors or more
facing each other across a street 5-34 m wide (not round towers, sealed blocks, wings or set pieces; never on a wall of balconies or a walkway,
a party wall, or where an expressway runs): a 2.8 m concrete deck with 1.25 m solid parapets and a handrail, from a doorway in a floor of one
into a doorway in the matching floor of the other (the floors within 1.6 m of each other, the deck sloping between at no more than 1 in 10;
floor 1 at the lowest; its underside at least 5.2 m over the street). The doorway takes the window's place and is a pair of glazed doors (or one
door where the column is narrow) that opens like a balcony door, onto a floor clear of the stairs. Blocks of eight floors or more get a second
bridge three or more floors higher 40 % of the time. Shares of facing pairs: downtown 55 %, busy streets 50 %, homes 40 %, old town 35 %.
The lower-numbered block of a pair decides and draws the deck; the other finds it among its neighbours, so both agree. Near the spawn on seed
91736402: 193 bridges (71 downtown, 109 on the busy streets). Tests: `FootbridgeTests` (placement, floors, clearance, both ends agreeing, the
doorways, the deck drawn), `FootbridgeWalkPlayTests` (a body walks a deck end to end), `FootbridgeCapture`.

**Bridge doors fixed (2026-10-16, owner: "doesnt let me open sliding glass doors bridges connecting buildings", and the door "being blocked by the
buildings decals/architecture make those go away around the glass door").** The door interactor (`View/BalconyDoorInteractor.FindNear`) only asked
buildings whose BACK wall was near for their doors (the flats' balcony doors are all there), so a bridge's door on a front or side wall was never
found: neither E nor walking into it did anything. A building with a bridge is now asked whenever any of its walls is within reach. And a bridge's
LANDING on its wall -- the deck's width (or the door's, if wider) and 0.35 m either side, from under the deck to 0.35 m over the door's head
(`CityMeshBuilder.BridgeLanding`) -- is kept clear of everything the facade hangs out from the wall: the piers (`FacadeOrnament`) and the cast fins
(`BuildingDecor` ribs) stop under the deck and start again over the door (`NextBridgeLanding`), and the lit blade
signs, placed by their own dice, are left off where they would stand in it (a blade sign stood beside one door; the random single balconies, whose rail
stood across another, were removed altogether on 2026-10-16).
Tests: `FootbridgeDoorwayTests` (197 bridge doors near the spawn looked at from their decks, 115 on facades with piers or fins: rays across each
doorway from 1.5 m out reach the wall), `FootbridgeDoorPlayTests` (E opens and shuts a bridge door on a front or side wall; walking down the
deck opens it and the body walks into the building), `FootbridgeCapture` (`bridge-door-shut` and `bridge-door-open`, from the deck).

**Footbridges, second pass (2026-10-16): shapes, flights, rails, higher, and stairs down to the street.** Owner, with four reference images (dark
concrete towers, catwalks with post-and-rail railings crossing on the slant, zig-zag stairs climbing between buildings, a curved catwalk): *"ive
noticed the sky bridges are often times very low, make these have a lower chance of appearing closer to the surface and a higher chance of
appear on taller buildings. I also want to make the sky bridges more intricate ... more variety in angles/curves/turns, make them have different
types of guard rails ... make more staircases throughout the city overall, and have some of them connect to the sky bridges."*

- **Higher.** A pair's chance is the district's share times `TallFactor` of the shorter block's floors (rising as the floors over two to the
  power 1.5 since 2026-10-16: three or four floors a twelfth of it, eight two thirds, ten the whole, twelve and up 1.4 times; it rose in a
  straight line before, and on seed 45678679 fewer than half the bridges near the spawn joined blocks of eight floors; now 52 of 82); a bridge's floor is drawn weighted by its deck's clearance over the street (`HeightWeight`: 8 m clear a
  fifth as likely as 30 m and up). Pairs of eight floors or more get a second bridge half the time, of sixteen a third a third of the time,
  each three or more floors above the last. Near the spawn: mean clearance 30.5 m, 8 % of bridges under 12 m.
- **Shapes** (`BridgeShape`, by district): STRAIGHT across; ANGLED to a doorway along the other wall, 10-40 degrees off square, the deck's ends
  cut along the walls; DOG-LEG out square onto a landing by each wall and across between them on the slant (20-70 degrees), mitred at the
  turns, a PIER under each landing where the ground there is not a road; S-CURVE square out of one doorway and square into the other (a cubic
  sampled every 1.6 m). A straight or curved span over 24 m gets a pier in its middle the same way. Any shape that will not fit falls back to
  straight. The path (`BridgePath`: points along the walking surface, per stretch deck or a flight) is what is drawn, walked and tested.
- **Flights.** Two floors more than 1.6 m apart (up to 9 m, about two storeys) are joined by steps in the bridge's straight part (the dog-leg's
  middle): risers at most 0.22 m, treads 0.36 m, a landing every 18 steps (4 m; the building code's 12 ft between landings is 4.75 m at the
  game's 1.3x), level deck either side. 36 of the 108 bridges near the spawn have them.
- **Rails** (`BridgeRail`, by district): a concrete PARAPET with a handrail; a catwalk's RAILS (posts every 1.6 m, top and middle rail, toe
  board); PICKETS (balusters every 0.14 m between a top and a bottom rail); MESH panels between posts; industrial PIPE rails (three pipes,
  posts every 2 m). Far off the open kinds are a plain panel. **Decks** (`BridgeDeck`): a concrete slab, or a STEEL catwalk plate on two beams
  (often downtown with open rails, most of the time in the industrial districts).
- **Street stairs** (`StreetStair`): the pair's lowest bridge, where it leaves its wall square, often gets a stair from the deck's side down
  along that wall to the paving: a STRAIGHT RUN of flights hugging the wall where the deck is 16 m or less over the paving and the wall has
  room; else a ZIG-ZAG TOWER against the wall (flights doubling back on two lanes, a landing across both at each end, rails between the lanes,
  along the outside and round the landings' ends, posts to the ground), up to 45 m. Never over a road or into another building, never into the
  ground on the way down, clear of a wall's corners; on a front only where the street storey is plain (no shop front, no entrance stairs) and a
  good stride clear of the door. The bridge's rail on that side is open where the stair leaves it. 8 near the spawn, 7 of them towers.
- **The facade keeps clear** of each bridge's landing on its wall (as wide as the deck meets the wall: wider on the slant) and of each street
  stair's run (`Footbridges.ClearingsOn`; the piers, fins and blade signs stop and start again round them).
- Tests: `FootbridgeShapeTests` (every shape, rail and deck built near the spawn; every path walkable in plan: ends on the walls, decks no
  steeper than 1 in 10, steps no higher than a riser and a tread deep, turns only at landings, clear of every other building and of the
  street, piers off the roads; the bridges high; the street stairs along their walls onto the paving, off the roads, the facade clear of
  them), `FootbridgePathWalkPlayTests` (a body walks a dog-leg, a curve, a slanted bridge and a bridge's flight door to door, and climbs a
  straight street stair and a 19.6 m tower from the paving onto the deck), `FootbridgeTests` and `FootbridgeDoorwayTests` updated (stairs may
  join floors further apart; the doorway is looked at along the deck), `FootbridgeVarietyCapture` (bridges-*.png: each shape, each rail, the
  flights, a street stair and a tower).

**Stairways, stepped sidewalks and hills (2026-10-16): the stairs that are streets, a first part of L1.** Owner, the same message: *"make
more staircases throughout the city overall ... san francisco has a lot of randoms staircases that go up hills and stuff in intricate ways
study how they do it, maybe make the terrain more hilly overall."* What San Francisco and Pittsburgh do (Wikipedia, "Step street"; KQED's
Bay Curious on the city's 600-900 public stairways; the Pittsburgh city steps guides, some 700 sets, about 450 on structures and 350 set
into sidewalks): where a street would be too steep, or a block too long to walk round, a stair takes its place -- a flight of its own
between two streets at different levels, straight up the slope with landings where it eases (the Lyon Street Steps, 332 steps in paired
flights with clipped hedges; the Filbert and Greenwich Steps through gardens; the mosaic 16th Avenue steps), or the pavement itself built
as steps on a steep street (22nd Street at Vicksburg, a 31.5 % block). Concrete or stone, a handrail on posts down the middle.

- **Hills** (`PlanFields.Hillock`, SUBVERSION_CITY.md): the game's own city was almost flat (its rolling ground stretched with the varied
  scale's footprint: half the streets under 1 %); it now has San Francisco's hills, 90 m high about 900 m apart in the hilly districts.
  The world seed was rerolled to **45678679**, chosen among 3,000 (`tools/cityplan -- hillseeds`) for hills round the spawn: 37 % of the land
  within 1.5 km of it on a hill.
- **Stairways through steep blocks** (`World/Plan/StairLanes`, chosen after the malls and plazas, before any lot is cut): a block whose paving
  climbs 5 m or more straight across it, at 8-48 %, gets (three times in four, none within 90 m times the square root of the district's scale
  of another) a stairway 4.4 m wide straight across from the lot line on its low side to the lot line on its high side: the steepest square
  crossing from points along each side, the far side within about 12 degrees of square, the whole corridor and its lots' clearance inside the
  block, clear of the expressways. The block's land either side is cut into lots on its own (`LotSubdivider.PushBesideStairway`: the lot line
  clipped 0.6 m beyond each edge, the cut facing the stairway), so it runs between the buildings from one street's pavement to the other's.
  Its steps follow the ground (`StairLanes.Profile`): a step up of 0.2 m where the ground at a tread's far edge has climbed half a riser, a
  step down where it has fallen as much, a level 0.36 m tread where it is flat (0.28 m where the flight has fallen a riser and a half behind
  a steep slope), a 1.8 m landing after every 18 steps up, a level pad at each end; the pads climb nothing, so what the flights end short of
  the top is made up from the top down, a level tread turned into a step at a time without making a flight longer. Drawn
  (`CitySurfaceBuilder.StairLanes.cs`): concrete treads, each a block down past the ground, the ground and the block's paving cut away under
  the corridor; a retaining face up to the paving either side where the stairs sink into the slope; a handrail on posts every 2.4 m down the
  middle; a concrete planter of bare earth either side of each landing (the planting waits for the owner's yes on green); a parapet 0.9 m
  over the paving along a side where the paving stands more than 0.6 m over the stairs (cut into a slope across it). The sidewalk's bins,
  hydrants and cabinets keep out of its ends. A stairway whose treads would stray more than 1 m from the paving along its middle (too steep
  for its flights: one sank 5 m into a trench) is not taken. On seed 45678679: 177 in the game's region (18 within 1 km of the spawn, mean
  118 m long and 19 m up, the middle at most 0.3 m under the paving), 118 in the classic test region.
- **Stepped sidewalks** (`World/SteppedSidewalks`): a block's sidewalk along a street whose kerb climbs 14 % or more over 6 m or more, not along
  a pedestrian street and not where a garage's drive or a ramp's kerb cut crosses it, is steps: the strip from the kerb to the lot line (and
  5 cm out over the road, so the kerb is not drawn along it) cut from the paving and laid as treads following the slope, each 0.2 m from the
  last, long where the street is gentle and short where it is steep; each tread's face to the road is the kerb there; a retaining face up to
  the paving beyond the lot line where that is higher. Worked out once per plan (60-80 ms) so every chunk agrees. From afar (the far chunks) a stepped sidewalk is
  its sloping paving. 723 in the game's region,
  796 (of 809 steep ones) in the classic test region, the steepest 56 %.
- **Step streets** (`World/StepStreets`): an ordinary street (not an arterial, a boulevard or a bridge) climbing 21 % or more from junction to
  junction over 15 m or more -- where the car stalls, and steeper than San Francisco built streets to be driven -- is steps from the junction
  plate at one end to the plate at the other, the Vallejo, Filbert and Lyon Street way: the carriageway's width of concrete treads on the
  stairways' profile (pads of 1 m at the ends), a row of bollards across each end's pad closing it to cars, and on a wide street a terraced
  concrete planter of bare earth down the middle splitting it into two flights, each with a handrail (`CitySurfaceBuilder.StepStreets.cs`);
  no asphalt, paint or barricades; its sidewalks, as steep, are stepped sidewalks. Not a street an expressway ramp or exit lands on, and not
  one steeper than any flight climbs (its stair must meet the road at both junctions and keep within 0.75 m of it all the way: a street
  down a river bank sank 13 m under its own road; such streets stay roads). A steep street is no
  pedestrian street (tables and trees on a slope); no garage or car park is put on a lot whose front is a steep street
  (`PlanBuildings.FrontsSteepStreet`). 95 in the game's region (10.1 km; 4 within 1 km of the spawn), 144 in the classic test region.
- Tests: `HillTests` (31 % of the land within 2.5 km of the origin on a hill at the game's scale, 17 % of it 14 % or steeper; no hill by the
  water; the ground never near the fall-through rescue), `StairLaneTests` (every stairway on its block's lot line at both ends, inside its
  block, steep enough; its treads contiguous, a riser at most apart, no flight over 18 steps, within 1 m of the paving; no lot or building on one; the treads built
  and nothing of the ground over them), `SteppedSidewalkTests` (every steep sidewalk is steps unless a drive or ramp crosses it, and no
  other; the treads a riser apart within half a riser of the paving; built, nothing over them), `StepStreetTests` (every step street steep,
  none a pedestrian street; its treads plate to plate, a riser apart, within 0.75 m of the road and meeting it at both ends; built as steps with bollards and no
  asphalt; no garage or car park opening onto one), `StairLaneWalkPlayTests` (the real player controller climbs a 56 m stairway 7.3 m up and
  walks back down, climbs a 68 m step street 16.4 m up between its bollards, and a 60 m stepped sidewalk 10.8 m up), `StairLaneCapture`
  (stairs-*.png: three stairways from below, above and their tops, a stepped sidewalk, a step street from its foot and its top, the steepest
  street near the spawn that is still a road).

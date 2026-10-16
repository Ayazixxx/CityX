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

- **Higher.** A pair's chance is the district's share times `TallFactor` of the shorter block's floors (three floors a sixth of it, ten the
  whole, sixteen and up 1.3 times); a bridge's floor is drawn weighted by its deck's clearance over the street (`HeightWeight`: 8 m clear a
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

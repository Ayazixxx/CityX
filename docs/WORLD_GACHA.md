# Building variety from World Gacha (2026-10-15)

The owner pointed at **World Gacha** (tools.oyasumi-gamedev.com/worldgacha, by @sharp_maker): a procedural town generator in the
browser whose towns read as real places. Asks: *"I love their variety in building types and details, i want you to make my city as
detailed, you can leave out the nature and vehicles"*, *"dont fully break ours just make all the building types and details possible in
their world possible in ours too. and use their cities finer details to improve upon ours as well"*, *"but if you think they do a system
better than implement their way"*.

Their page says the towns use no outside data, only random numbers and formulas (no real-city map data).

## What their generator does (read from its own source notes, in Japanese)

- **A building is given a USE, and the use picks its facade type** (`窓の並び(用途ごと)`): 1 house (sparse windows), 2 apartment block
  (balconies), 3 office or shop building (window bands and floor lines), 4 factory or warehouse (high horizontal strip), 5 shop on the
  ground floor (glass) with windows above, 6 curtain wall. An Italian region adds 9 palazzo or apartment (tall windows, shutters, floor
  bands, eave cornice, rusticated ground floor), 10 church (tall arched windows), 11 bell tower (bell openings at the top), 12 small house.
- **Each use carries its own numbers:** ground-floor height, window module (bay) width, window width, height and sill, corner-pier
  width, floor band.
- **Per floor and per side** the type can change: ground floor = shopfront, entrance or shutter; floors above = windows, curtain wall,
  **balcony** or **open access walkway**.
- **The apartment block:** balconies on the south face, an open walkway (外廊下) on the north; each with its floor slab, a parapet wall,
  partitions between flats, and floor-length windows (掃き出し窓) or front doors behind.
- **Details:** a band at every floor line, corner piers, window sills, small canopies over windows (庇), sign bands, roof parapets with a
  coping, roof plant (stair heads, water tanks, air-conditioner units). A window is a hole + glass + inner mullion + sill.
- **Roofs:** flat, gable (切妻), hip (寄棟), and on special buildings domes and cones.
- **Special buildings:** churches (Romanesque, Gothic and Renaissance fronts with rose windows), bell towers, palazzos with a top-floor
  loggia, arcaded streets (portici: Bologna 85 %, Padua and Modena 60 %), Japanese castles (curved stone bases, white plaster or black
  board walls, gatehouses), temples and shrines.
- **Level of detail:** near the camera the detailed building (window recesses, balconies, shopfronts and signs, roof plant); further away
  the windows are drawn by the shader.

## How it maps onto CityX

| Theirs | Ours before | Plan |
|---|---|---|
| apartment block (balconies + walkway) | a few separate balconies | **done, phase 1** (below) |
| house with a pitched roof | none (every roof flat) | phase 2: gable / hip / skillion roofs, houses in the homes zones |
| office (window bands) | `FacadeStyle.Ribbon` | have |
| factory / warehouse | `HighBand`, sheds | have |
| shop under windows | storefront ground floor | have; **done, phase 3**: roller shutters and the band of shop signs |
| curtain wall | `Curtain` | have |
| window canopies (庇), mullions | sills, frames | **done, phase 3**: canopies (mullions: the frames already have them) |
| floor bands, corner piers, coping, roof plant | `FacadeOrnament`, `AddRoof` | have |
| church, bell tower, temple, shrine, castle | malls, garages, hospital, bank, police, supermarket | phase 5 |
| arcaded street (portici) | none | phase 5 |

Left out on purpose (the owner's call): trees and nature, vehicles.

Not in their list, added alongside it (my suggestion, in the spirit of their Japanese streets): **rooftop billboards** (屋上看板), phase 4 (below).

## Phase 1: the apartment block (`World/ApartmentFront.cs`, `View/CityMeshBuilder.Apartment.cs`)

- **Which buildings.** An ordinary building people live in: not a special one, a sealed or roof-top block, a wing or a round tower; not a
  glass skin, a shed or a blank box; at least three window rows, each at least 2.95 m (the first slab then clears a 2.6 m body). Of those,
  per zone: downtown 25 %, urban 45 %, residential 70 %, old town 40 %, outskirts 50 %, industrial none. Measured on seed 39271846:
  30 of the 295 buildings round the spawn (downtown), 72 % of the residential ones further out.
- **Balconies on the back wall** (South, away from the street door) on every row line from the first floor up: the slab (its top level
  with the floor inside), a solid parapet or a steel railing (30 %), a partition between flats every 2 or 3 window columns (on a column
  line, so never across glass), a solid side wall closing each end, and an eave over the top floor. 1.5 to 2.1 m deep.
- **A walkway on the street wall** (55 % of blocks without a shopfront): the same slab, a solid parapet, no partitions. Under it the
  entrance has no canopy and no blade sign (the walkway is the shelter).
- **Lived-in:** a laundry pole in 45 % of flats (near the wall, 2.7 m up: clear of a 2.6 m head, and leaving the strip behind the
  parapet free for a climber pulling up), washing on half of them (1 to 4 pieces, mostly whites and blues), a futon airing over 12 % of
  solid parapets. Air conditioners under the windows stand on the balcony floor instead of hanging through the slab.
  **2026-10-15 (later): the laundry is OFF** (`ApartmentFront.LaundryOn = false`; owner: "removed folded laundry from balconies we can add
  stuff like that later right now it just gets in the way"), and **a balcony sits on every FLOOR, not every window row** (owner, with two
  screenshots: "these balconies make no sense the doors are like stacked on top of eachother in one room"): a storey is two rows, so the
  balconies and their doors are on the even rows only, one per flat per floor, each slab at its floor's height.
- **Parapet finish:** the building's own wall (45 %), white (30 %) or bare concrete (25 %).
- **Not twice:** the vertical fins skip a wall with a band. (The old random single balconies were removed on 2026-10-16, owner: "get rid of these random pointless mini balconies"; balconies are the apartment blocks' alone now.)
- **Far away** only the slabs, parapets (or top rail) and side walls, so a block keeps its stripes as it comes into full detail.
- **Cost** (`ApartmentFrontTests.MeasureTheCost`, 81 chunks): near downtown +0.7 % wall triangles, residential +6.2 %; far +21 to 36 % of a
  small number; build time unchanged (4.1 ms a downtown chunk).
- **Climbing.** A balcony's outer face is a stack of 1.55 m parapets with gaps between, which `WallProbe` does not read as a wall (it
  wants one flat face from knee to head), and the slab is an overhang no climb gets past; so the balcony side is not climbable from the
  ground (like a shop's awning). A climb up it ends cleanly, never inside a slab (`ApartmentClimbPlayTests`). The other walls climb as
  before. Climbing from balcony to balcony would need a new move (grab a slab's edge from below, hop up parapet to parapet).
- **Before/after renders:** `ApartmentFrontCapture` (Explicit, `-Graphics`), with `CityMeshBuilder.ApartmentFrontsOn` as the switch.

Not yet (their apartment has it): front doors and small windows along the walkway, an outside staircase at a walkway's end. (The doors
onto the balconies came on 2026-10-15: below.)

### The doors onto the balconies (2026-10-15, `World/BalconyDoors.cs`, `View/CityMeshBuilder.BalconyDoors.cs`, `View/BalconyDoorInteractor.cs`)

Owner: "make it so all balconies have a door leading between the inside of the building and the balcony ... most will be sliding glass doors
which is the most common balcony door in real life ... double doors ... along with the single doors we built already".

- **One per flat per balcony**, in the middle of the flat's stretch of the back wall, in place of the window there (two window columns, one
  where a column is 2.6 m or wider): a doorway from the balcony floor up to the slab above (2.9 m at most; 2.75 m on the lowest rows that get
  balconies), cut through both faces of the wall. The cells it takes have no window (`GlassDamage.IsDoorCell`), and no pier stands in front
  of it. A flat of one column at the end of a wall is folded into its neighbour now (a 1.25 m flat had no room for a door).
- **The kind is the building's:** sliding 60 % (2.0-3.0 m: a fixed panel on the outer track and one sliding behind it on the inner, the open
  half at least 0.89 m wide), a pair of glazed doors 25 % (1.5-2.2 m), a single door 15 % (1.3 m: a solid wooden leaf with a lever handle each
  side, the street doors' look). A flat with no room for its building's kind gets the next narrower one. The aluminium frames are the
  building's window frame colour.
- **Which open.** (Until 2026-10-15 the balconies were on every window row, so the door on every other one was half way up a room and stayed
  shut and rattling; now there is one balcony per floor and no such door.) A door onto a floor opens unless the floor just inside (1.4 m) has the
  stairs, the opening they come up
  through or a partition in it, or a body could not walk from it to the stairs (a flood over the stairs' own walking grid). On seed 52817364:
  3564 of 11751 doors near the spawn open, and from every one a body walks to the stairs; 1230 of the 4794 doors onto a floor stay shut
  for the stairs or a wall just inside. (Found on the way, and fixed the same day: some floors could not be reached from their own stairs
  at all; docs/BUILDING_INTERIORS.md, "every floor can be walked into from its stairs".)
- **Opening one.** Walk at it (1.6 m out, 1.2 m/s toward it: it opens before you reach it) or press E looking at it (2.5 m); E again shuts
  it, but not on someone standing in it. Hinged leaves always open into the flat (the balcony is narrow and has the laundry pole and the
  washing on it). A door stays as the player left it. Sounds: a sliding door's rumble, the street doors' latch and thud, a locked door's
  rattle, all made in code.
- **Drawn in the chunk, not an object each.** A block of flats has fifty doors; each is drawn shut or open in the chunk's own mesh and
  collider, and opening one rebuilds its chunk in the background, the way a broken window does (`CityStreamer.OnBalconyDoorChanged`). So the
  door pops open rather than swinging: a short moment while the chunk is rebuilt. The laid-out doors are kept per building
  (`BalconyDoors.For`, which plans the stairs to see which open); a shot at a window asks only whether its cell is a doorway
  (`BalconyDoors.IsDoorCell(site, plan, column, row)`, worked out on the spot), so a bullet never waits on the stairs.
- **Every player sees it:** `Character/BalconyDoorsNetwork` (FishNet broadcasts like the light switches; the server takes only a door that
  exists and opens, 8 a second per player; a joiner is told every open door).
- **Research.** The Level Design Book: a sliding door is the simplest door, it recedes into its own frame and nothing blocks it; a Sims
  mod's sliding glass doors clipped furniture where they slid past their own footprint (ours stack inside the frame); double doors in games
  (Neverwinter Nights scripts, Minecraft's paired-door packs) open as one; in real flats a slider is the norm where space is short (Japan),
  French doors the traditional European choice, and a slider opens only half its width (hence the 2 m minimum).
- **Tests:** `BalconyDoorsTests` (every balcony has one; the kinds' shares; a 2.66 m, 0.58 m wide body fits through every one; which open,
  checked against `BuildingStairs.WalkPath`; no window in a door's cells; an open door leaves a body-sized way through the wall's mesh and a
  shut one does not; the state, the network check, the bump and E), `BalconyDoorPlayTests` (a body walks off a balcony through a real
  sliding door into the flat; a locked door holds it; E shuts and opens), `BalconyDoorCapture` (Explicit, `-Graphics`).

## Phase 2: pitched roofs (`World/RoofShape.cs`, `View/CityMeshBuilder.Roofs.cs`)

- **Which buildings.** Low buildings (LowRise, no more than 26 m across the ridge and 48 m along it) in the homes zones and some downtown --
  per zone 10 % downtown, 25 % urban, 60 % residential, 75 % old town, 70 % outskirts -- and 45 % of warehouses (a low metal gable). Never a
  special building, a block of a shaped building, or one something stands on. A building something stands against (a terrace's neighbours,
  the leg of an L) may have them on two opposite walls only: those are the gable's ends, flush, with no verge -- the roof of a row house.
  Measured on seed 61928374: 196 of the 1601 buildings within 40 chunks of the spawn (101 gables, 57 hips, 38 skillions; 6 sheds).
- **Kinds.** Gable (ridge along the long side, the wall carried up as a triangle at each end), hip (four slopes, a pyramid on a square house),
  skillion (one shallow slope, the high wall and the two side triangles carried up). Pitch 22-32 degrees (hip 20-28, skillion 6-12, sheds
  6-10), lowered if need be so no roof rises more than 5.5 m. Eaves 0.45-0.6 m out (sheds 0.35), verges 0.3 m.
- **Covering.** Tiles (the facade's Tile material at three times its size: 300 mm tiles; silver-grey or blue-black kawara, terracotta, brown)
  or standing-seam metal (dark grey, navy, green, red-brown, silver, teal), the seams running down the slope. A skillion is always metal.
  Ridge cap, fascia along the eaves, a pale soffit under them. A "chimney" roof puts a brick chimney through the slope near the ridge, an
  "antenna" one a television aerial on the ridge.
- **What changes with it.** No parapet and no roof plant (`AddRoof` builds the pitched roof instead), no cornice (`FacadeOrnament.For`), the
  corner piers stop at the eaves, no stair up to the roof (`BuildingStairs`: the attic is shut), and the building is never an apartment block
  (`ApartmentFront.For`). Far away the same roof less the ridge cap and aerial.
- **Climbing.** A climb up a house used to hang under its eaves for good: the ledge check (`Character/LedgeProbe`) wanted a level top. It
  now has a third kind besides the shelf and the parapet: the edge of a walkable slope (no steeper than 33 degrees) rising away from the
  wall, its eaves up to 0.9 m out. The mantle rises to the stand spot on the slope before stepping in (`RoofClimbPlayTests`: three climbs up a
  real house, all on its roof).
- **Before/after renders:** `RoofShapeCapture` (Explicit, `-Graphics`), with `RoofShape.PitchedRoofsOn` as the switch.

## Phase 2b: shed, saltbox and flat-with-eaves roofs; the tops of the towers (2026-10-15)

Owner, with three roof icons (a shed roof, a flat roof with overhanging eaves, a saltbox): "add all of these types of roof as types that can occur
in both the city district and the residential district. research how often these roofs occur in real world environments and make the chance they
occur ingame similar. also figure out what types of buildings they are typically utilized for"; and with six tower icons (an antenna mast on a
stepped top, an art-deco stepped crown with a spire, a needle spire, the Chrysler crown, the Empire State's mast, a slanted glass spire): "I want it
to have a chance that these types of tips generate only on really tall towers ... implement a few different types ... research the ratio that
these generally appear in comparison to flat roofs in real life cities and make them generate at a similar rate."

### What was found

- **How often, roofs.** OpenStreetMap's `roof:shape` over 9.75 million tagged buildings (taginfo, 2026-10): gabled 56.2 %, flat 21.2 %, hipped
  10.0 %, pyramidal 3.0 %, skillion 2.9 %, half-hipped 1.4 %, gambrel 0.46 %, mansard 0.37 %, saltbox 0.35 % (plus the double and quadruple
  saltboxes, 0.45 %), lean-to 0.15 %. The mapping is European and residential heavy; the roofing trade's guides (GAF, Roof Crafters) only rank:
  gable the commonest house roof of North America, hip second, flat the least common on homes and the rule on commercial and industrial
  buildings (low-slope roofing is about 55 % of all the roofing sold in the western US).
- **What for.** A SHED (skillion) roof: the modern house, additions, garages, the small workshop or retail box, the industrial shed (a monopitch).
  A SALTBOX: 17th-18th century New England, a gable whose back slope runs on down over a one-storey lean-to (two storeys in front, one behind),
  kept today on garages, sheds and the odd house. A FLAT ROOF WITH OVERHANGING EAVES: the mid-century house (the ranch, the Eichler), the
  1950s-80s school, motel, dealership and strip shop, the modern pavilion: a thin slab with a fascia projecting past the walls, no parapet.
- **Tower tops.** CTBUH counts a spire in a building's height and an antenna not, and dropped its ranking by roof height in 2009 because
  flat-topped skyscrapers "are not as common in the modern era"; its 2013 "vanity height" study found 61 % of the then 72 supertalls would be
  under 300 m without their non-occupiable top. No census of the KINDS of top exists, so they were counted off the tallest-building lists: New
  York, 68 towers over 230 m: 5 spires, 1 mast, 11 crowns or stepped tops, 51 flat (a quarter topped; 7 of the 19 over 300 m); Chicago, 57 over
  150 m: 2 spires, 1 mast, 13 crowns, 41 flat (6 of the 7 over 300 m, 8 of 32 at 200-300 m, 2 of 18 at 150-200 m). Hong Kong's list notes none.

### What was built

- **Roof kinds (`World/RoofShape.cs`):** `Saltbox` and `FlatEaves` beside gable, hip and skillion. Shares of the PITCHED house roofs by zone
  (gable / hip / skillion / saltbox): the homes 58 / 32 / 8 / 2, old town 57 / 35 / 4 / 4 (the saltbox belongs there), downtown 49 / 30 / 20 / 1
  (the houses between towers are the modern ones); downtown's pitched-house share raised from 10 to 16 % (Core) and 25 to 30 % (Urban). Of the
  FLAT house roofs, slabs with eaves instead of parapets: the homes 30 %, downtown 25 %, old town 10 %, industrial 15 %; of the mid-rise blocks
  8 % downtown, 6 % in the homes, 3 % old town (a 1960s office or walk-up with an overhang). Warehouses: 45 % pitched as before, now 60 % the low
  gable, 30 % a monopitch shed (4-7 degrees), 10 % a shed with a lean-to (a low saltbox); of the flat ones 25 % get the overhang and gutter. Never
  on a slab, a tower or a supertall. Measured on seed 46283197: of the 984 pitched houses within 80 chunks of the spawn 15 are saltboxes (1.5 %) and,
  in the homes, 49 of 757 are skillions (6.5 %); 212 of the 967 flat house roofs in the homes have eaves instead of a parapet (22 %), 149 of 3161
  downtown mid-rise volumes (4.7 %: the stacked and sealed ones never do); of the sheds 55 are pitched (18 monopitch, 6 lean-to) and 18 of the 79 flat
  ones have the overhang (23 %).
- **The saltbox** (`View/CityMeshBuilder.Roofs.cs`): both slopes at one pitch (26-32 degrees), the front wall (the street door's) carried up an
  attic storey (0.45 of the height, 3.6 m at most: two storeys in front, one behind), so the ridge sits forward (the attic's height over twice the
  pitch's tangent, never nearer the
  front than a fifth of the span) and the back slope runs long and low to the back wall's top; the end walls close in the saltbox's own profile;
  the ridge kept under 5.5 m like every roof; the chimney on the front slope. On a house with the ridge along its width the long slope is the back
  (south) one; along its depth, either side.
- **The flat roof with eaves:** the deck's slab carried 0.7-0.9 m out past every wall (no further than `ClimbJut.MaxLean`, so a climb leans round
  it as round a cornice), a fascia in white, cream, dark brown, charcoal or pale grey standing 10 cm over the deck as the gravel stop, a pale
  soffit under the overhang. No parapet, no coping, no cornice (`FacadeOrnament`), no billboard (`RoofBillboards`), the corner piers stop at the
  soffit, a vent or two instead of the full roof plant; the stair still comes up through the deck (`RoofShape.IsFlat`). `RoofShape.Pitched` now
  means sloped (nobody stands on it) and `HasParapet` the plain flat roof: what wants a parapet (cornice, billboard, piers) asks for that.
- **Tower tops (`World/TowerTop.cs`, `View/CityMeshBuilder.TowerTops.cs`).** The topmost volume of a tower that reaches 100 m (a plain tower, the
  tower on its podium, the top stage of a stepped one; a box or a round one; not the leg of an L, not a set piece: `BuildingSite.Reach` is the whole
  tower's height, since a stage carries the building's mass but only its own height) gets one on its flat parapet roof by its
  seed: a Tower (110-240 m) 22 % (mast 5, spire 5, stepped crown 5, fins 7), a Supertall (240 m up) 55 % (spire 20, stepped crown 12, fins 9,
  mast 8, slanted spire 6), matching the lists above (a fifth of the towers, more than half of the supertalls, crowns the commonest).
  MAST: a platform, a lattice of four legs tapering in over the lower two thirds with a brace ring every six metres, a pole, two cross bars and a
  beacon whose red glow (the billboards' floodlit trick in the lit glass) shows at night on a powered building; 12-20 % of the tower's reach, 18 m at
  least. SPIRE: a drum and a needle of eight tapering boxes with a collar of bars; 14-26 %. STEPPED CROWN: three setback tiers in the wall's own
  look with a band of trim on each, then the needle; 18-24 %. SLANTED SPIRE: at a corner, leaning eight degrees in over the roof; 16-24 %.
  FINS: a crown of 2.4 m fins every six metres round the roof's edge inside the parapet, a pair meeting at each corner, closed by a ring; 5-7 %.
  Placed in the roof's middle (the slanted one at a corner; a round roof's square inside its parapet, and no fins there), slid along the roof's longer
  side or shrunk (a crown's first tier down to 6 m, else
  it becomes a plain spire) to keep 1.2 m off the stair's hatch as a billboard does, and 1.3 m inside the parapet; the roof plant keeps off its
  base, and an "antenna" roof type's own 6-30 m mast is left out beside it. Drawn near and far alike (less the small bars), and the SKYLINE TIER
  carries a stub of it on its box (a pole, a few tapering boxes, four slabs), so the far towers keep their silhouettes (docs/RENDER_DISTANCE.md).
  Measured on seed 46283197: 68 of the 303 towers within 80 chunks carry a top (22 %) and so does the one supertall: 24 crowns of fins, 16 masts, 16
  spires, 13 stepped crowns (no slanted spire: only a supertall rolls one).
- **Tests:** `RoofShapeTests` (the shares above over the region; a saltbox built near and far: a storey taller in front, its ridge forward, its
  long slope down to the back wall, no stair into its attic; a flat roof with eaves: its lip at one height 0.4 m out past every wall, no cornice,
  no billboard, the stair still up to it; the old guards widened to the new kinds), `TowerTopTests` (only free-standing towers with parapet roofs,
  the share by mass, heights in range, inside the roof's edges and off the hatch, the mesh reaching the top's height near and far and the skyline
  stub more than half of it), `SkylineTests` (a box may now rise to its top's reach), `RoofKindsCapture` (Explicit, `-Graphics`: each new roof and
  each top alone, from the street, from above, and the tops close up). New world seed 46283197.

## Phase 3: window canopies, roller shutters, shop signs (2026-10-15)

- **Window canopies (庇, `FacadeOrnament.CanopyDepth`).** A thin concrete hood over every window above the street storey, a little wider than
  the window, 0.36-0.55 m deep (`MinCanopyDepth`..`MaxCanopyDepth`, 8 cm thick, 5 cm over the window's head). Punched and scattered facades
  only (not glass skins, ribbon offices or sheds), by district: residential 45 %, old town 50 %, outskirts 40 %, urban 22 %, downtown 8 %
  (measured: 48 %, 52 %, 19 %, 6 %). A building has canopies or stone lintels, never both. Not on a wall with balconies or a walkway (they are
  the shelter there). Shallower than `ClimbJut.MaxLean`, so a climb leans round them as it does round a cornice.
- **Roller shutters (シャッター, `World/Shopfronts.cs`).** Three shopfronts in ten have shutters down on some of their bays (each bay 60 %):
  a slatted metal curtain from the pavement to the top of the shop window, in a 6 cm recess, with the box it rolls into over it. The slats
  are the metal cladding texture turned on its side and drawn four times as close (one every 12.5 cm), so they cost one quad. No window is
  cut behind a shutter (the shop's wall is whole inside) and there is no pane to break (`GlassDamage.TryPaneAt`). Mostly galvanised steel,
  now and then painted.
- **The sign band.** Over the shopfronts, either side of the door, one sign per shop (about one per 4 m) instead of one board a side: each
  its own colour from a brighter palette than the walls' (white and cream most, then the reds, yellows, blues and greens of a shopping
  street), its own depth and drop. 45 % are lit from behind: a panel of lit glass inside a 10 cm rim of the board, dark by day, glowing at
  night (pink, blue, amber, the whites) -- on a building with power (one in ten, `PowerGrid`), like the blade signs; a dead building's are
  dark glass.
- **Tests:** `StreetDetailTests` (canopy shares and rules; a canopy over every upper window of a canopied wall and none over the street storey;
  shutters on about three shopfronts in ten, no pane behind one, the shop's inside wall whole; lit signs on most shopfronts),
  `StreetDetailCapture` (Explicit, `-Graphics`: a home's canopies, a shopping street by day and at night, a shutter close up).
- **Awnings clear the body.** A shop's awning hung its front flap 0.46 m under the top of the street storey: on a short storey (about 3 m)
  that was at head height, and on seed 61537940 two runs at shop windows stopped against it. Its lowest edge now stays 2.8 m over the shop
  floor (`CityMeshBuilder.AwningClearance`): on a short storey it hangs less, or is left off.

## Phase 4: billboards on the roofs (2026-10-15)

- **Where (`World/RoofBillboards.cs`).** The big painted board on steel legs that a Japanese street's mid-rise buildings carry over their
  street walls. On a flat roof of a building 9-70 m tall (not a pitched roof, a sealed block, a round or many-sided tower, or a roof another
  block stands on), its face 1 m in from the street wall (behind the parapet), facing the street. 5-14 m wide and 1.5 m in from each end of
  the roof, 3-5.2 m tall, its bottom 3.3 m over the roof: the catwalk under its front, 0.3 m lower, clears a 2.6 m body walking along the
  roof. Of the buildings that could carry one: downtown 30 %, the busy streets 25 %, the old town 15 %, industrial 10 %, the homes 6 %,
  elsewhere 5 % (measured round the spawn: 29 %, 23 %, 7 %, 5 %). Pure, from the building's seed: every machine has the same boards.
- **Never on the stair's hatch.** Where it would stand within 1.2 m of the hatch the stair comes up through, it slides to one end of the
  roof or the other, then a board 65 % as wide is tried, then there is none. The roof plant keeps off its ground (legs to catwalk).
- **The ad (`View/CityMeshBuilder.Billboards.cs`).** A grey galvanised panel whose face is a skin of paint in one of nine strong colours, an
  accent in another (a band across, a block at one end, or a border round a block) and lettering -- a headline and a line or two in white or
  black, whichever stands out from what it is on -- laid round the accent. No textures: thin boxes a centimetre or two proud of the face.
- **The steel.** Two or three legs behind it (three past 9 m wide) with struts to its back and two rails; a catwalk with a railing in front;
  seven boards in ten have floodlights on the railing, turned on the board.
- **Lit at night.** On a building with power (one in ten, `PowerGrid`) a floodlit board's face takes its lamps' light at night in its own
  colours: a pool over each lamp, evening out and fading going up. It is a FLOODLIT face in the lit glass (`SurfaceLook.PackFloodlit`: a light
  colour past the glass shader's eight real ones marks it, and the paint's colour rides in the bytes the glass would not use), so it costs no
  draw call of its own: the glass shader adds the paint's light and nothing else (nothing by day), each colour's light just in front of its
  paint and behind whatever stands prouder, which hides it. The far shells carry it too: a lit board is seen from across the city.
- **Tests:** `RoofBillboardTests` (shares by district, on the roof, clear of the hatch, the catwalk over a body's head; drawn where it is
  said to stand, facing the street; a floodlit face only on a powered building and only over its own board, near and from afar),
  `RoofBillboardCapture` (Explicit, `-Graphics`: from the street by day, at dusk and at night, close by day and at night, from behind on the
  roof). New world seed 70485213.

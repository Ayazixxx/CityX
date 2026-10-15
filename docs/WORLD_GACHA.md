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
- **Parapet finish:** the building's own wall (45 %), white (30 %) or bare concrete (25 %).
- **Not twice:** the old single balconies and the vertical fins skip a wall with a band.
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
- **Which open.** A storey is two window rows and the balconies are on every row, so the door on every other balcony is half way up a room:
  it stays shut and rattles. A door onto a floor opens unless the floor just inside (1.4 m) has the stairs, the opening they come up
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

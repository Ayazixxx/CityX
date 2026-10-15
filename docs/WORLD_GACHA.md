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
| shop under windows | storefront ground floor | have; phase 3 adds roller shutters |
| curtain wall | `Curtain` | have |
| window canopies (庇), mullions | sills, frames | phase 3 |
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

Not yet (their apartment has it): floor-length windows to the balcony, front doors and small windows along the walkway, an outside
staircase at a walkway's end.

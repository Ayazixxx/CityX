# Parking, malls and plazas (2026-10-12)

Asked: "research parking lots and parking garages in cities and decide how often they should spawn dont make it too often. make malls a thing,
massive buildings with "stores" within them and a cafeteria etc. make them very rare though. and make it unable to spawn nearby another they
should be as far away from each other as malls usually are away from each other in real life. design pedestrian plazas and place them
throughout the cities as well. research various video game cities and real life cities". Expressways: `docs/HIGHWAYS.md`.

## What the research found

**Parking.** In US downtowns parking covers a startling share of the land: the Parking Reform Network's downtown parking lot maps put the median
at ~26% of the central area (Arlington TX 42%, Houston ~26%, New York 1%); European and Asian centres are far lower (under 5%), with the parking in
structures. A multi-storey garage: floor-to-floor 3.0-3.3 m (10-11 ft), a spandrel (the solid band on each level) ~1 m high with open air above
it, columns on a bay of about 8-9 m x 16-18 m so a row of stalls and an aisle fit between them, stalls 2.5-2.7 x 5.0-5.5 m (8.5-9 x 18 ft), aisles
~7.3 m (24 ft) for 90-degree parking, a ramp of 12-16% between decks (often the parking deck itself sloped), 4-7 levels. A surface lot: the same
stalls in rows with an aisle between each pair, a kerb wall or railing, a booth and barrier arm at the entrance, a "P" sign.

**Malls.** A regional mall is 400,000-800,000 sq ft (37-74k m2) of gross leasable area on 2-3 levels (ICSC's shopping-centre definitions), drawing
on a trade area of 5-15 miles (8-24 km): neighbouring regional malls are typically 6-10 km apart, and a metropolis of millions has a handful.
The plan is Victor Gruen's "dumbbell": a **concourse** down the middle with the **inline stores** either side (20-40 ft wide, 40-60 ft deep,
each with a shopfront, a sign band and a roller shutter), an **anchor** (a department store, two to three levels, taking the whole end) at each
end, the upper levels as galleries round a void open to the ground floor, **escalators** in the void, skylights over the concourse, and the
**food court** at the point of convergence (its counters round the walls, its seating in the open), plus the car park round the outside. A
dead mall (the thing the lore makes of every one here) has most of its shutters down, over-turned shelving, a dry fountain, rubble in the concourse.

**Plazas.** William H. Whyte, *The Social Life of Small Urban Spaces* (1980; the Street Life Project): the plazas people use are **small**, close to
the street (not sunken, not raised), have **sittable edges** (a linear foot of seating per 30 sq ft of plaza; ledges, steps, movable chairs),
sun, water you can touch, trees, and food (a kiosk, a cart); the catchment of a plaza is about three blocks. Jan Gehl adds edge effect (people sit
with their backs to something) and the 100 m "social field of vision". Video-game cities put plazas at the foot of landmarks (Spider-Man's
Manhattan, Watch Dogs' Chicago riverwalk, Yakuza's Kamurocho with its Millennium Tower square) and use them as the open rooms of a dense city.

## What was built

### The plan (`World/Plan/Amenities.cs`)
- **`LotUse`** {Building, ParkingLot, ParkingGarage, Mall} on `PlanLot.Use`; `BuildingParams.SpecialType` carries it into the world as
  `SpecialTypes.ParkingGarage/ParkingLot/Mall` (`BuildingSite.IsParkingGarage/IsOpenGround/IsMall`); such buildings are one box each: no massing,
  no podium volumes, no basement; `WindowGrid.PlanFor` gives them a blank window plan (the mall a storefront ground floor).
- **Parking** (`Amenities.ParkingPlanner.Decide`, run from `PlanBuildings.Resolve` in lot order): downtown (Core/Urban zones) a lot at least
  `GarageMinSide` 26 m x sqrt(scale) each way rolls a **garage** with `GarageChance` 0.35, elsewhere (not OldTown) a lot of `LotMinSide` 18 m rolls
  an **open lot** with `LotChance` 0.30; never two in one block; never within `GarageSpacing` 180 m / `LotSpacing` 140 m (x scale) of another. The
  share lands at ~2-3% of the lots (the "not too often" the owner asked for; a US downtown would be 20-30%). Garages are 4-7 levels of
  `GarageFloorHeight` 4.0 m; an open lot is `ParkingLotHeight` 0.4 m (a wall round it).
- **Malls** (`Amenities.ChooseMalls`): a whole block of `MallMinArea`..`MallMaxArea` (24,000-110,000 m2 x scale) in population 0.30-0.78 (the
  edge of downtown, where real ones stand), whose largest fitting rectangle is at least `MallMinWidth` 150 x `MallMinDepth` 115 m (room for the
  complex and a car park round it), with no river channel through it, no two within `MallSeparation` **6 km**, at most `MaxMalls` 4 in the region,
  not touching a ramp. The block becomes one lot (`LotSubdivider.EmitMallLot`), and its parking is rolled (`PlanBlock.MallParking`): every mall has
  the open-air lot; a garage beside it as well with `MallGarageChanceAt` 12% at the edge of town rising to 30% near the core, about one in five
  (no national count was found; planning documents describe the regional mall as surrounded by surface parking, structured parking as the mark of
  urban and mixed-use centres).
- **Mall shapes** (`World/Plan/MallLayout.cs`, owner 2026-10-12 with photos and a real mall's floor plan: "L shaped or some type of trapazoid",
  "wacky with a few large stores and a ton of smaller ones"): a mall is a NETWORK of boxes in its lot. A **spine** corridor (`SegmentDepth` 56 m
  across: 14 m concourse, 20 m shops each side; `MinSpine`..`MaxSpine` 90-220 m long x the district's scale) across the back of the lot, sometimes
  set forward to leave room behind it; **anchors** (`AnchorWidth` 50 m) at its ends (80% each), pushed out front or back; then 1-3 **branches** off
  its sides, front or back: an anchor straight on its flank, a side corridor ending in an anchor or an entrance, or a side corridor that turns into a
  **wing** leading to a big box (taller, a cinema in spirit), the way the real plan's east wing runs off to the AMC. Pieces that join share a wall;
  the rest stand `Clearance` 12 m apart. Then the **garage** (when rolled) beside a piece of the mall across a drive, sides and back first; and 1-3
  **pads** (22 x 16 m single-storey restaurants and banks, ordinary buildings) out by the road. Cross-sections stay human-sized in every district;
  only lengths and the front car park grow with its scale. Pieces are `MassVolume`s (side corridors quarter-turned) resolved like any building's:
  the corridors and anchors are one building (`Wing`), sharing one floor; the garage and pads stand on their own floors. `PlanWorld.MallSites`
  lists a mall's buildings by block. `tools/cityplan -- mallshapes` tallies the shapes over 400 seeds.
- **Plazas** (`Amenities.ChoosePlazas`): small blocks (`PlazaMinArea` 700 - `PlazaMaxArea` 9,000 m2 x scale^2) in dense country (population >=
  `PlazaMinPopulation` 0.40), `PlazaShare` 40% of the candidates by seed hash, no two within `PlazaSpacing` 260 m (x scale). `PlanBlock.Plaza`: no
  lot is cut; `CityPlanData.PlazaBlocks/MallBlocks` list them.

### The mesh
- **Garage** (`CityMeshBuilder.Parking.cs`, `BuildGarage`): the podium, decks on a column grid (`GarageColumnBayX` 8 x `GarageColumnBayZ` 16 m,
  `GarageColumnSize` 0.6), each with a `GarageSpandrelHeight` 1.4 m spandrel and open air above it, two entrance openings on the front with lintels
  and a `P` sign, a zig-zag parked-on ramp along the back wall rising deck to deck with the arrival cut out of the slab above, a parapet round the
  roof, stalls painted in rows (`StallWidth` 3.2 x `StallDepth` 6.2, `AisleWidth` 8), rubble. Far: the
  outside only, the openings as dark bands.
- **Car park** (`BuildParkingLot`): asphalt over the lot that follows the ground like the block paving round it (sampled on `LotCell` 4 m cells;
  the first version stood on a building's podium and read as a raised platform on any slope), a 0.6 m wall stepping down the slope with the gap at
  the front, a booth with its barrier arm and `P` sign, stall rows, a barrier and some rubble. (No cars: owner, 2026-10-12.)
- **Mall** (`CityMeshBuilder.Mall.cs`): every piece is built with the passages to the pieces it touches -- at the middle of each shared stretch
  (`WallContact`), the same point seen from both sides, `MallPassageWidth` 12 x `MallPassageHeight` 5.6 m -- and where a side corridor or an anchor
  meets a corridor's flank, the shops there make way for it on the ground floor. **Corridors** (`BuildMall`): blank light walls with a base band on
  the solid stretches, a door under a canopy at each open end and on the front (or back) where nothing stands against it; the spine carries the
  mall's name; a corridor 130 m or longer keeps an anchor of its own at an open end. Inside (near chunks only) -- the `MallConcourseWidth` 14 m
  concourse; store units 10-14 m wide and `MallStoreDepth` 20 m deep both sides on every level, party walls, a lettered sign band,
  `MallShutteredShare` 55% shuttered, the rest with a stall riser, double-sided shopfront glass, a doorway, a counter and shelving (30% knocked
  over); `MallGalleryWidth` 3.5 m galleries with a balustrade round the void; dead escalators (two on the spine either side of its food court, one
  or two on another corridor, never in front of a passage); the spine's **food court** (counters with hoods, tables and stools, some over-turned)
  and dry fountain; planters with benches; rubble. **Anchors** (`BuildMallAnchor`): one big hall with a passage into every piece it touches, its own
  door on the first free wall (north, west, east, south) under a canopy and a big brand sign, long shelving rows clear of the aisles from every
  opening (18% pushed over), counters by the door, a mezzanine over the half away from the door, rubble. Far: the outsides with a dark backing
  behind each door.
- **Mall car park** (`CitySurfaceBuilder.BuildMallGrounds`, owner: "generate a parking lot fitted to the available space around the mall"): the
  block is asphalt kerb to wall; **double rows** of stalls nose to nose, an aisle between each pair running toward the mall's doors (the grid lined
  up with the spine, anchored to it, so every chunk paints its part of the same rows); a **planted island** every `MallLotIslandEvery` 12 stalls,
  a 9 m **lamp post** on every other one (`CityProps.AddLampPost`); stalls only where both ends are on the block, `MallLotKerbClearance` 4 m from the
  kerb and `MallLotBuildingClearance` 9 m from every building, so the rows stop where the space does and a drive runs round everything.
- **Plaza** (`CitySurfaceBuilder.BuildPlazaFurniture`, `BlockPavingLook`): the block paved in **setts** in a warm tint, and Whyte's furniture --
  benches every 7.5 m along every edge facing in, bollards along the kerb every 3 m, planters at the corners, in the middle a dry fountain (with or
  without its needle) or a big planter with four benches facing it, a shuttered kiosk on the longest side, two heaps of rubble.
- `CityStreamer.AddInteriorVolume` skips open car parks (nothing to walk inside).

### Numbers (seed 74920318, the game's varied 10 km, first pass): parking 226 garages + 116 open lots = 2.0% of the street-level sites, 3 malls
(the nearest two 6.1 km apart), 84 plazas (mean 570 m to the nearest other). Mall complexes (seed 38461927, rerolled for them): three malls 6.4 km
or more apart -- a 21.9 ha one with 3 corridors, 3 anchors, a pad and a garage, two smaller ones with 2 corridors and an anchor each.

## Tests
`CityFeaturesTests` (EditMode): parking is 0.5-8% of the lots and never twice in a block, garages are 4-7 levels and car parks flat; malls are
1-4 and far apart, each with one main corridor of two or more levels; every corridor and anchor that is a wing joins another piece; over 300 seeds
the layout makes at least six kinds of mall, side corridors in a quarter of them and more than one anchor in half, nothing overlapping or outside
its lot; a garage is the rare extra (the chance holds at 12-30%) and is placed when rolled; the mall's block has hundreds of parking stripes and a
lamp post, none within 2 m of a building; plazas (>= 10) have no lots and no building inside them; a plaza chunk is paved in setts with a bench; a
garage, a car park, a mall corridor and an anchor build near and far, wound and finite. `CityFeaturesCapture` (PlayMode, Explicit, `-Graphics`)
renders them to `Logs/CityShots/city-*.png`.

## Not done / next
- Store **names** (there is no font in the city: signs are lettered in white bars) and a real loot/shop system in the mall units.
- Angled wings (the real plan's Von Maur and AMC sit at an angle): pieces meet square here, since an angled box would leave a wedge of gap or overlap.
- Upper-floor passages between corridors (they join on the ground floor only), and a bridge from a mall's garage into the mall.
- Interior lighting and room volumes for the mall and the garage (`CityStreamer` interior volumes, `RoomLights`), and the garage's ramp as a
  thing the player can walk up (it is geometry; the collider is the chunk's).
- Plaza trees (there is no vegetation yet: the planters hold dry earth, as the lore has it).
- A kerb cut and a dropped kerb at a car park's entrance; crossings painted from the mall's doors across the car park.

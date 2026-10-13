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
  edge of downtown, where real ones stand), whose largest fitting rectangle is at least `MallMinWidth` 110 x `MallMinDepth` 70 m, no two within
  `MallSeparation` **6 km**, at most `MaxMalls` 4 in the region, not touching a ramp. The block becomes one lot (`LotSubdivider.EmitMallLot`): the
  building is capped at `MallMaxWidth` 280 x `MallMaxDepth` 170 m x sqrt(scale) and 2-3 levels of `MallLevelHeight` 6.5 m, the rest of the block is
  its car park (the block paving). `MallFront` picks its longest road edge.
- **Plazas** (`Amenities.ChoosePlazas`): small blocks (`PlazaMinArea` 700 - `PlazaMaxArea` 9,000 m2 x scale^2) in dense country (population >=
  `PlazaMinPopulation` 0.40), `PlazaShare` 40% of the candidates by seed hash, no two within `PlazaSpacing` 260 m (x scale). `PlanBlock.Plaza`: no
  lot is cut; `CityPlanData.PlazaBlocks/MallBlocks` list them.

### The mesh
- **Garage** (`CityMeshBuilder.Parking.cs`, `BuildGarage`): the podium, decks on a column grid (`GarageColumnBayX` 8 x `GarageColumnBayZ` 16 m,
  `GarageColumnSize` 0.6), each with a `GarageSpandrelHeight` 1.4 m spandrel and open air above it, two entrance openings on the front with lintels
  and a `P` sign, a zig-zag parked-on ramp along the back wall rising deck to deck with the arrival cut out of the slab above, a parapet round the
  roof, stalls painted in rows (`StallWidth` 3.2 x `StallDepth` 6.2, `AisleWidth` 8), `StallOccupancy` 28% of them with a car, rubble. Far: the
  outside only, the openings as dark bands.
- **Car park** (`BuildParkingLot`): asphalt over the lot that follows the ground like the block paving round it (sampled on `LotCell` 4 m cells;
  the first version stood on a building's podium and read as a raised platform on any slope), a 0.6 m wall stepping down the slope with the gap at
  the front, a booth with its barrier arm and `P` sign, stall rows with cars, a barrier and some rubble.
- **Mall** (`CityMeshBuilder.Mall.cs`, `BuildMall`): outside -- blank walls with a base band, a glazed entrance under a canopy in the middle of the
  front and of each end, two anchor signs and the mall's own sign over the entrance, a parapet, skylights over the concourse, roof plant. Inside
  (near chunks only) -- the `MallConcourseWidth` 14 m concourse along x; anchors `~20%` of the length at each end, open to the concourse on every
  level, shelving rows inside; store units 10-14 m wide and `MallStoreDepth` 20 m deep both sides on every level, party walls, a lettered sign
  band, `MallShutteredShare` 55% with the shutter down, the rest with a stall riser, double-sided shopfront glass, a doorway, a counter and shelving
  (30% knocked over); upper levels as `MallGalleryWidth` 3.5 m galleries with a balustrade round the void; dead escalators (30-degree slabs with
  balustrades and landings) between the levels, alternating direction; the **food court** on the ground floor in the middle (counters with hoods
  along both sides, tables and stools in the open, some over-turned, some dragged away); a dry fountain, planters with benches back to back,
  rubble. The far version is the outside with a dark backing behind each entrance.
- **Plaza** (`CitySurfaceBuilder.BuildPlazaFurniture`, `BlockPavingLook`): the block paved in **setts** in a warm tint, and Whyte's furniture --
  benches every 7.5 m along every edge facing in, bollards along the kerb every 3 m, planters at the corners, in the middle a dry fountain (with or
  without its needle) or a big planter with four benches facing it, a shuttered kiosk on the longest side, two heaps of rubble.
- `CityStreamer.AddInteriorVolume` skips open car parks (nothing to walk inside).

### Numbers (seed 74920318, the game's varied 10 km): parking 226 garages + 116 open lots = 2.0% of the street-level sites, 3 malls (the nearest
two 6.1 km apart), 84 plazas (mean 570 m to the nearest other). At the classic 5 km scale the tests pin: 1.5%, 1 mall, 64 plazas.

## Tests
`CityFeaturesTests` (EditMode): parking is 0.5-8% of the lots and never twice in a block, garages are 4-7 levels and car parks flat; malls are
1-4, far apart, big and two or more levels; plazas (>= 10) have no lots and no building inside them; a plaza chunk is paved in setts with a
bench; a garage, a car park and a mall build near and far, wound and finite, the mall with hundreds of vertices inside it and the garage with
decks at several heights. `CityFeaturesCapture` (PlayMode, Explicit, `-Graphics`) renders them to `Logs/CityShots/city-*.png`.

## Not done / next
- Store **names** (there is no font in the city: signs are lettered in white bars) and a real loot/shop system in the mall units.
- Interior lighting and room volumes for the mall and the garage (`CityStreamer` interior volumes, `RoomLights`), and the garage's ramp as a
  thing the player can walk up (it is geometry; the collider is the chunk's).
- Plaza trees (there is no vegetation yet: the planters hold dry earth, as the lore has it).
- A kerb cut and a dropped kerb at a car park's entrance; the car parks of malls as painted stalls on the block paving.

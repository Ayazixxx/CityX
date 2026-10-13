# The Subversion-style city (2026-10-01)

Asked: copy the city generator from Introversion's cancelled *Subversion* (demo at `C:\Games\CityGeneratorDemo`, YouTube "Subversion City
Generator Introversion Software") so the city has varied shapes and a realistic layout instead of a square grid, and "rework the buildings
and everything else to fit the new building and road sizes". **The game now runs on it.** The old lattice city is gone from the build.

## What the research found
The demo is a closed binary (`main.dat` is a packed archive); nothing was decompiled or copied from it. The method is public: Chris Delay's
blog series "It's all in your head" (Parts 3, 6, 7; forums.introversion.co.uk) names Parish & Mueller, *Procedural Modelling of Cities*
(2001) as its starting point. Part 6, in his words: city centres are the high points of a population map; highways follow the height map,
population and centres ("rigid near the centres, go around tall hills, coast roads along rivers, promoted to bridges where density is
high"); streets are short local roads to the nearest highway ("grid patterns in city centres"); then every enclosed "block" is found by
searching the street map; blocks are meant to be divided into allotments, one building each.

## The plan generator -- `CityX/Assets/CityX/World/Plan/` (pure C#, deterministic, links into `tools/cityplan`)
- `PlanMath.cs` -- `Vec2` (double), segment/polygon maths, mitre inset, pseudo-angle, rotation tables.
- `PlanFields.cs` -- `PlanConfig` (every tunable), `PlanFields` (smooth hills, river/lake "water", population, centres), `PlanNoise`.
- `RoadNetwork.cs` -- priority-queue growth: highways from centres toward population / off slopes / square near the core; streets branching
  at right angles; intersection, junction-snap, T-snap, sliver rejection; bridges over water.
- `CityPlan.cs` -- `CityPlan.Generate`; `BlockExtractor` (faces of the planar graph, spur pruning); `LotSubdivider` (recursive cuts, back-to-back
  deep blocks, courtyards dropped); each block keeps its kerb (`Inner`) and pavement-edge (`Outer`) outlines.
- `PlanGeometry.cs` -- each road's ribbon (trimmed back at both ends) and a plate at every junction, from the exact corner where one road's kerb
  meets the next. The pavement ring (between `Inner` and `Outer`) shares those corners, so road and pavement meet with no gap or overlap.
- `PlanWorld.cs` -- the city as the game asks about it: the plan, its geometry, every building (`PlanBuildings`: the largest rectangle that fits a
  lot with a side on its street frontage, turned to face the street), indexes by 24 m chunk, `BuildingAt`, `GroundHeight`, `TryFindSpawn`.
  One `PlanWorld` per seed, cached, read-only -> safe to read from the chunk worker threads.
- Render: `dotnet run -c Release --project tools/cityplan -- [seed] [halfMetres] [px] [out.png] [cx cz viewHalf]`. Seed 61839025, 5 km region:
  ~2,600 nodes, ~4,600 roads, ~1,250 blocks, ~12,100 buildings, generated in ~0.6 s. EditMode: `PlanWorldTests`, `PlanChunkMeshTests`.

Determinism (server and clients must grow the same city): double + - * / sqrt only (angles come from literal tables, no sin/cos/pow), integer-hash
randomness keyed by stable ids, priority-queue ties broken by creation order. Fallback if a platform's JIT still diverges: the plan is a few
thousand edges, so a server can send it.

## How the game uses it
- **A building is built in its own frame, then turned into place.** `BuildingSite` is a rectangle (width x depth) around its centre with a world
  `Axis` (local +X); local **+Z points out of the front, at the street, and the door is on that wall**. `MinX..MaxZ` are that rectangle *as if
  unturned*, so the whole existing building generator (walls, windows, glass, doors, stairs, rooms, roofs, balconies, ribs) runs unchanged.
  `MeshBuffer.SetFrame(offset, axisX, axisZ)` turns and moves everything written afterwards; `CityMeshBuilder.BuildSite` sets it on all the
  buffers, builds, and clears it. `LocalToWorld` / `WorldToLocal` / `ToBuilderFrame` convert for everything that needs to find something.
- **Chunks are still 24 m streaming tiles** (`CityStreamer`, the map, rain, `ExploredChunks`) but no longer decide what stands there: a chunk
  builds the part of the ground inside its square (`CitySurfaceBuilder`: terrain, road ribbons, junction plates, pavement + kerb, lane paint,
  bridges -- all clipped to the chunk, heights from one function, so neighbours meet exactly) plus every building whose centre is in it.
  `PaneKey` is now (building, wall, column, row); glass, doors, interior volumes and cracked-pane triggers all follow the building's turn.
- **Rivers and lakes are dry concrete trenches** (user, 2026-10-01: no water yet). They stay in the plan as channels that steer the roads and
  force the bridges. Add a water surface later.
- **Terrain** is the plan's smooth hills (30 m amplitude); a building's floor is the highest ground under it, with a podium down to the lowest.
- **Pedestrians**: `StreetNetwork` now walks the road graph (nearest road, a side of it, round junctions) via `CurrentWorld`.
- Spawn: the junction nearest the origin with no water within 90 m and plenty of buildings around it.
- The generated region is finite (5 km square) and the terrain carries on beyond it as bare ground.

- **Street walls** (2026-10-09, "don't be afraid to alter building size and the layout"): every building used to stand 2.2 m back from the pavement
  and 1.2 m in from each neighbour's lot line (+0.3 m of fitting margin), so the whole city -- downtown included -- was detached blocks with 3 m gaps,
  which read as a suburb of towers. Now a lot whose population is over `LotSubdivider.StreetWallPopulation` (0.55) is a **street wall**
  (`PlanLot.StreetWall`): no setback to its neighbours, 1.0 m to the pavement, 0.08 m fitting margin, so neighbours stand ~0.16 m apart, shoulder to
  shoulder. `PlanBuildings.AddPartyWalls` then gives both sides of every such joint a `WallContact` (parallel or quarter-turned neighbours in one
  block, facing walls within 0.6 m), which blanks the windows (`WindowPlan` takes any number of contacts now), drops the ornament and corner piers,
  and stops cornices wrapping round into the neighbour (`CityMeshBuilder.AgainstCorner`). About 60% of street-level buildings now share a wall.
  Measure with `dotnet run -c Release --project tools/cityplan -- partywalls` and `-- overlaps` / `-- fitcheck`.
- **Fit fix found by the street walls**: `FitRectangle` sampled the lot's chords 0.15 m shallower than the rectangle it returned; the old 0.3 m margin hid
  it. The chords now cover the whole depth (`fitcheck`: 0 rectangles outside their lot).
- **Shapes** (2026-10-08): a lot's rectangle is no longer always one box or a stack; `Massing.Compose` also makes L/T/U blocks, staggered pairs, twisted,
  wonky, cantilevered and round towers. See `docs/BUILDING_SHAPES.md`.

## Set aside, not deleted (user: "just set them aside for now")
The old grid world lives in `World/LegacyGrid~/` (CityBlocks, CityChunkGenerator, CityZones, SkybridgeLayout, CityPlanSvg) and
`Tests/LegacyGrid~/` (their tests, plus the grid-only mesh/layout/terrain tests). Unity ignores folders ending in `~`; everything is also in git
(last pre-port commit `67e1e68`). `Tests/ToMigrate~/` holds the superseded original of one ported test. Grid-era editor diagnostics
(`StreetAnomalyScan`, `CityMassingCapture`, three methods in `GrapplingHookBuilder`) are kept behind `#if CITYX_LEGACY_GRID_DIAGNOSTICS`.
`PlanCityCapture` (menu "CityX/Capture Plan City") replaces the capture tool.

## Not done / next
- Skybridges and the books of the old city are not in the new one (they were lattice features). Elevated highways with exits and a ring road
  came 2026-10-12 (`docs/HIGHWAYS.md`); pedestrian plazas with benches, parking and malls the same day (`docs/CITY_LIFE.md`). Seed rerolled
  to 74920318 with them, and to 38461927 for the mall complexes.
- Crosswalks, more street furniture, a smarter yard/courtyard fill, rounded kerb corners, a second row of buildings in deep lots.
- Endless world: the land fields are seamless noise already; only the roads need region borders (portals at hashed positions). Today the city is one
  5 km region.
- Chunk/streaming cost needs a measured pass (see the perf notes in this file once taken).

## Update: blocks paved kerb to wall (2026-10-09)

Reported with screenshots: between the pavement and the buildings the ground was a zigzag patchwork (4 m terrain squares, each one tarmac,
setts, paving or earth by its centre, with staircase edges, 23 cm below the pavement). Now every block (except one with an island inside it) is
paved as ONE surface from the kerb to the walls (`CitySurfaceBuilder.BuildBlockPaving`): its kerb outline cut into triangles once in the plan
(`PlanBlock.InnerTriangles`, `PlanMath.Triangulate`, ear clipping), each triangle clipped to the terrain grid so it follows the ground, at the
pavement's height and in one paving look per block. The old pavement ring is drawn only for unpaved blocks; terrain squares wholly under paving
are skipped. Ground floors were raised to stand on the paving (`PlanBuildings.PavingRise` 0.23 m: floor = ceil(highest ground + 0.23) to the
decimetre; the door's ground is the paving 2 m out). Seed rerolled to 61839025. Tests: `BlockPavingTests`.

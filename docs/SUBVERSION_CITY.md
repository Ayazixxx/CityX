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
- `RoadNetwork.cs` -- priority-queue growth: the spines (`RoadClass.Boulevard`, once called highways) from centres toward population / off
  slopes / square near the core; streets branching
  at right angles; intersection, junction-snap, T-snap, sliver rejection; bridges over water.
- `CityPlan.cs` -- `CityPlan.Generate`; `BlockExtractor` (faces of the planar graph, spur pruning); `LotSubdivider` (recursive cuts, back-to-back
  deep blocks, courtyards dropped); each block keeps its kerb (`Inner`) and pavement-edge (`Outer`) outlines.
- `ExpresswayPlanner.cs` (+ `.Ramps`, `.Furniture`), `Expressways.cs`, `Dubins.cs`, `DMath.cs` -- the expressways, a layer of their own over the
  finished streets: corridors, cloverleafs, exits (`docs/HIGHWAYS.md`).
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
- **Lane paint lies ON the asphalt** (2026-10-17, owner: "the street decals are often loading in glitching into the terrain. i want them to
  be apart of the texture of the street not hovering above or glitching into the terrain"). A stripe was a flat quad from the road's height
  at its own corners, 3 cm up, while the road under it lies on its own 5 m pieces, each flat: wherever the road bent under a stripe, part of
  it sank in and part floated (round the spawn of seed 19077151, 14 % of the paint was sunk and 27 % stood more than 8 cm over the road). Now
  `CitySurfaceBuilder.LayPaintOnAsphalt` cuts every stripe along the asphalt's own triangles under it (roads, junction plates, expressway
  decks: the `ground` buffer, its upward faces) and sets each piece on that triangle's plane, `PaintOnAsphaltLift` 1 cm above it, so it bends
  exactly where the road bends; where two surfaces overlap each gets its piece and the upper one shows. Paint over no asphalt (a mall's lot)
  is left as it was; car parks lay theirs on their own cells (docs/CITY_LIFE.md). `RoadPaintTests` measures it as the eye sees it, the top
  paint against the top surface at every corner and middle of every stripe piece: 99.8 % within 3 mm of the lift, none sunk, none floating,
  0.2 % hidden under a kerb. `RoadPaintCapture` (Explicit, `-Graphics`) films it low along the streets where the road bends most under it.
- **Rivers and lakes are dry concrete trenches** (user, 2026-10-01: no water yet). They stay in the plan as channels that steer the roads and
  force the bridges. Add a water surface later.
- **Terrain** is the plan's smooth rolling ground (30 m amplitude times the road scale), and since 2026-10-16 **San Francisco's hills** over it
  (`PlanFields.Hillock`; owner: "maybe make the terrain more hilly overall"): rounded hills up to 64 m high about 640 m apart at the classic
  scale (90 m and 900 m at the game's varied scale, both times the road scale), rising out of level ground where their noise is above zero
  and rounding off at the top, only in the hilly districts (a 1840 m noise; the rest keeps the rolling ground), never within four river
  bands of a river or by a shore, so no river cuts a gorge. Before them the rolling ground, stretched with the footprint, left the game's
  city almost flat: half its streets under 1 %, 27 sidewalks in the region at 14 % or more. With them (seed 63428175, varied): the steepest
  1 % of streets 26 %, the steepest 51 %, 1.8 % of the street length over 21 % (where the car stalls), 465 sidewalks at 14 % or more, the
  ground from -5 to 123 m, and a building's floor more than 6 m over the lowest ground round it for 14 % of them (was 2 %). A height costs
  1.7 times what it did (200 ns), the plan no more. The steep blocks get stairways and the streets too steep to drive (21 % or
  more) are steps from junction to junction (LAYERED_CITY_PLAN.md; the sidewalks laid as steps beside steep roads were taken out the same
  day, owner: "look like shit and serve no purpose").
  `tools/cityplan -- hills [seed] [amplitude] [wavelength] [hill height] [hill spacing] [district]` measures any setting; `hillseeds` ranks
  seeds by the hills round the spawn. A building's floor is the highest ground under it, with a podium down to the lowest.
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
  it. The chords now cover the whole depth (`fitcheck`: 0 rectangles outside their lot). They are also measured at each of the lot's corners
  over that depth, and a millimetre either side (2026-10-15, seed 70485213: eight even steps let a corner between two of them poke a street
  wall's back corner 5 cm into the next lot; `-- fitlot <id>` prints one lot and its rectangle).
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
  came 2026-10-12 and were rebuilt as their own layer 2026-10-13 (`docs/HIGHWAYS.md`); pedestrian plazas with benches, parking and malls
  came 2026-10-12 (`docs/CITY_LIFE.md`). Seed rerolled to 74920318 with them, to 38461927 for the mall complexes, to 61470385 for the
  building sizes, to 72604913 for the expressway rebuild, and to 74102958 for its ramp tapers and denser exits.
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

## Update 2026-10-15: door steps measured where you walk, wing floors over their own ground

- **A door's ground** is now the LOWEST paving in front of it -- at the wall's foot and 2 m out, under the door's middle and both edges
  (`PlanWorld`, `DoorGroundOutMetres`, `DoorGroundAlongMetres`). One point 2 m out read a door as flush over a dip at the wall's foot that left a
  0.77 m step and no stairs (`RealCityDoorwayPlayTests`); now that door gets its stoop.
- **The shared ground floor clears the ground at the foot of every block at street level** (`HighestAtFoot`: corners and side middles), not only
  the lot's 5 m sample grid: a wing's corner fell between the samples and the paving came up 8 cm through its floor (`BlockPavingTests`, building
  1195). `tools/cityplan -- floorcheck <seed>` lists any floor with paving over a corner (0 at both city scales).
- Seed rerolled 58316274 -> 74102958. What the new city showed, all fixed: the two above; the player's step lift meeting a shop canopy's front
  edge (docs/CLIMBING.md); and tests that assumed a clear window (some facades have vertical sun fins every metre, 0.22 to 0.37 m deep, one through
  the middle of a window), a clear walk from the spawn, or a plan sill where a shop front has a stall riser.

## Update 2026-10-15 (night): a kerb line folded flat (seed 58203917, block 520 at the classic scale)

- **The fold.** `PlanMath.InsetPolygon` moves every edge of a block's road outline inward by its road's half width and joins the moved edges at
  their mitres. A block with a 30-degree corner and a 25 m road stub on it (a boulevard bending at a junction right next to a sharp corner with a
  street) moved the stub's two ends past each other -- a sharp corner's mitre slides the vertex a long way along the edge, further than the edge
  is long -- so the inset edge came out BACKWARDS, and because the stub was collinear with the next edge the fold lay flat along it: no edge
  crossed another, the area was still smaller than the road outline, and both checks passed. The paving's triangles then overlapped by a tenth
  (`BlockPavingTests.EveryPavedBlockIsCoveredKerbToKerb`: 932.9 m2 of triangles for an 852.2 m2 outline).
- **The fix.** After the mitres, any edge whose direction is against its road edge's is COLLAPSED: both its ends go to where the edges either side
  of it meet, a zero-length edge (passes repeat until nothing folds). The vertex count stays one per road corner, which the pavement ring relies
  on (`CitySurfaceBuilder.BuildPavement` pairs kerb, lot line and road class by index); `PlanMath.Triangulate` skips a vertex on top of the one
  before it so the duplicate blocks no ear. A block whose neighbours are parallel at the collapse, or whose collapse point is thrown too far, is
  refused as before. Blocks that were "paved" from a folded lot line (a 700 m2 triangle between three roads at the classic scale, a 2000 m2
  sliver at the varied) are now dropped like other fold-overs: unpaved, no lots -- the open-space pass (docs/OPEN_SPACE.md) will pave such
  islands instead.
- **Checks in seconds:** `tools/cityplan -- pavingcheck [seed]` (every paved block's triangles against its outline, with the corners of a bad
  one) and `insetfail [seed]` (every block without a kerb line and which check refused it, the inset replayed step by step). Remember
  `CITYX_CITY_SCALE=classic`: the Unity tests run at the classic scale (`Tests/*/ClassicCityScaleFixture.cs`), the tool defaults to the game's
  varied one.

## Update 2026-10-16: the ground no longer shows through the roads; dead-end roads under buildings retired

- **Poke-through.** Owner, with a night screenshot of a sloping street: "on areas where the terrain slopes there are often times different
  colored textures poking through". A road ribbon is laid in 5 m pieces, each straight across the road's whole width (two triangles from corners
  at the ground + `RoadLift`); the terrain under it is sampled every 4 m (every 8 m from afar). On a curving hillside the terrain bulged up
  through the asphalt, by as much as 0.74 m, and where the two lay within a few centimetres they fought. `SurfacePokeSurvey` (EditMode,
  Explicit: every half metre of the chunks round the spawn and of the 160 hilliest road chunks within 40 of it, near and far, the surface on top
  and what it covers) found 3,636 points of earth over a road's asphalt and about 1,500 more where they fought within 3 cm, near; the spawn's
  own ground is flat and showed none, which is why nothing caught it before.
- **The fix.** The terrain is cut away under every road ribbon piece and junction plate (`CitySurfaceBuilder.RoadCuts`, not under a bridge),
  as it already was under basements, and each road and plate edge gets a SKIRT (`RoadSkirt`): a face from the road's edge to the terrain mesh's
  own height beside it (`TerrainMeshHeight`) and 0.15 m past it, facing the road where a bank stands over it and away where the road stands
  over the ground; set back 2 cm so it hides behind a kerb's face rather than fighting it. A road graded straight across, the way real ones are.
  The survey after: no earth over or fighting a road's asphalt near or far; a few dozen scattered points off the roads. Cost: about 0.1 ms a
  chunk (`ChunkCostBench`: median 0.8 -> 0.9 ms full, 0.4 -> 0.5 ms far). Guard: `RoadGroundTests` (the 40 hilliest road chunks near the
  spawn at the game's scale, near and far: on every carriageway point, the earth 3 cm or more under the asphalt). `PlanChunkMeshTests`' seam test
  now accepts a corner on one chunk's border that lies on the neighbour's straight border edge (the cut lines can put one there: a T-junction,
  no gap).
- **Dead ends under buildings.** `BlockExtractor` prunes dead-end streets (and roads with the same block on both sides) before it traces the
  blocks, so the lots were cut across them, but they stayed alive and were drawn on under the buildings (owner: "the road leads into a wall";
  47 roads under 129 buildings on seed 63428175). `BlockExtractor.RetireRoadsInsideBlocks` retires each such road inside a block; one an
  expressway ramp or exit lands on stays, with the lots kept clear of it (`ExpresswayNetwork.HeldStreets`). `tools/cityplan -- spurs`,
  `underroads` and `garages`; `GarageEntranceTests.NoLiveRoadRunsUnderABuilding`. The parking garages' drives: docs/CITY_LIFE.md.

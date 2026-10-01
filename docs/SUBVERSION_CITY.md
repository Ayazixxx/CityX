# Subversion-style city plan (started 2026-10-01)

Asked: copy the city generator from Introversion's cancelled *Subversion* (demo at `C:\Games\CityGeneratorDemo`,
YouTube "Subversion City Generator Introversion Software") so the game's city has varied shapes and a realistic
layout instead of a square grid. "Don't be afraid to break the city generation we have built."

## What the research found
The demo is a closed binary (`main.dat` is a packed archive); nothing was decompiled or copied from it. The method is
public: Chris Delay's blog series "It's all in your head" (Parts 3, 6, 7; forums.introversion.co.uk) names Parish & Müller,
*Procedural Modelling of Cities* (2001) as the starting point. Part 6, in his words: city centres are the high points of
a population map; highways follow the height map, population and centres ("rigid near the centres, go around tall hills,
coast roads along rivers, promoted to bridges where density is high"); streets are short local roads to the nearest
highway ("grid patterns in city centres"); then every enclosed "block" is found by searching the street map; blocks are
meant to be divided into allotments, one building each. ~2 km² in about two minutes (2007 hardware).

## What exists now (stage A, done, NOT yet in the game)
`CityX/Assets/CityX/World/Plan/` (pure C#, no Unity, links into the `tools/cityplan` harness):
- `PlanMath.cs` -- Vec2 (double), segment/polygon maths, polygon inset (mitre), pseudo-angle, rotation tables.
- `PlanFields.cs` -- `PlanConfig` (all tunables), `PlanFields` (height, river/lake "water", population, centres), `PlanNoise`.
- `RoadNetwork.cs` -- priority-queue growth: highways from centres toward population / off slopes / square near the core,
  streets branching at right angles, intersection / junction-snap / T-snap / sliver rejection / bridges over water.
- `CityPlan.cs` -- `CityPlan.Generate(seed, cfg)`; `BlockExtractor` (planar-graph faces, spur pruning); `LotSubdivider`
  (recursive cuts, back-to-back deep blocks, courtyards dropped, footprint inset, height from population).
- Render: `dotnet run -c Release --project tools/cityplan -- sub [seed] [halfMetres] [px] [out.png]`
  e.g. seed 1234, 3 km region: 6 centres, 971 nodes, 1756 edges, 474 blocks, 5004 lots, 136 ms. The picture reads like the
  Subversion screenshot (curving highways, districts at different angles, rivers with bridges).

Determinism (server and clients must grow the same city): double + - * / sqrt only, no sin/cos/pow (literal angle tables),
integer-hash randomness keyed by stable ids, queue ties broken by creation order. Fallback if a platform's JIT still
diverges: the plan is a few thousand edges, so a server can just send it.

## Decisions
- **Water (user, 2026-10-01): no water yet.** Rivers/lakes stay in the plan as channels (they steer roads and force
  bridges) but are built as DRY OPEN TRENCHES, or flat ground -- no water surface. Add water later.
- Nature stays off (see memory: no trees/lawn yet).

## What is NOT done -- the integration (stage B), and why it is large
The game is a chunk lattice all the way down: `TerrainField` pads per 24 m chunk, `CityLayout/CityBlocks` roles,
`BuildingSite` = axis-aligned rectangle with compass walls (`WindowGrid.Wall.North...`), `GlassDamage.TryPaneAt`,
interiors, doors, pedestrian `StreetNetwork`, highways/skybridges, `CityMeshBuilder` (4,800 lines). A free-form plan breaks
every one. Previous session (2026-09-25) deliberately did NOT do free-form streets for this reason; this is the reversal.
Suggested order: (1) `PlanWorld` runtime facade (tile queries over the plan, finite region first; endless later by tiling
regions with hashed border portals -- the land fields are already seamless noise); (2) a plan chunk builder: smooth
terrain, road ribbons + sidewalks + markings, trench where the plan has water, lots as extruded footprint polygons
(walls = polygon edges; generalise window/door/glass addressing from compass walls to edge indices); (3) nav (pedestrians
on plan roads), spawn on a plan road; (4) retire/replace the lattice code; reroll world seed; rebuild both scenes.

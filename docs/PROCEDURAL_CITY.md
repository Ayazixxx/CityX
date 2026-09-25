# Procedural city: research and what was built

Requested 2026-09-25: "research procedurally generated city environment and
implement what you learn ... dont be afraid to rewrite stuff if your confident
it will be an improvement."

## What the research says

Sources read (all on the public web):

- Parish and Muller, *Procedural Modeling of Cities* (SIGGRAPH 2001), via
  [phiresky/procedural-cities](https://github.com/phiresky/procedural-cities/blob/master/paper.md)
  - Roads are generated hierarchically: a sparse skeleton of highways between
    population centres first, then streets fill in by pattern.
  - Everything is steered by global goals (a *population density map*) and
    local constraints; density is highest at a few centres and falls away.
  - Blocks are subdivided recursively along their longest side into lots;
    a zone map and a height map decide what stands on each lot.
- Chen, Esch, Wonka, Muller, Zhang, [*Interactive Procedural Street
  Modeling*](https://www.sci.utah.edu/~chengu/street_sig08/street_project.htm)
  (SIGGRAPH 2008) - street networks from tensor fields (grid and radial basis
  fields blended by distance); major roads follow one eigenvector field and
  minor roads the other. Also
  [Martin Devans' write-up](https://martindevans.me/game-development/2015/12/11/Procedural-Generation-For-Dummies-Roads/).
- Marian Kleineberg, [*Infinite procedurally generated city with the Wave
  Function Collapse algorithm*](https://marian42.de/article/wfc/) - the same
  constraint this codebase already lives by: chunks are independent and
  deterministic, borders are agreed by construction, and a failed local solve
  degrades gracefully rather than backtracking.
- Terrain-aware road work (weighted anisotropic shortest paths, cut and fill
  along a road) - already covered in this codebase by the sloped-street and
  highway systems.

## What was already true here (audited before changing anything)

`CityLayout` was a strictly periodic grid: an arterial every 24th chunk in
both directions, forever, so from above the world was one 576m square district
repeated. Districts rolled a street period at random. Buildings were rolled
per block independently, so towers and low-rise were scattered salt-and-pepper
with no downtown, no edge and no neighbourhood character. Roads and buildings
were both fine as *pieces*; what was missing was the macro structure the
research puts in charge of them.

The plan renders in `CityX/Logs/CityPlans` (or a `dotnet` harness over the same
pure sources) made this obvious in one look, which is why a Unity-free plan
renderer was the first thing built.

## What was built

1. **`CityZones`** (new, `World/CityZones.cs`) - the density map. A jittered
   lattice of urban centres (one per 96x96-chunk cell, found by hash), each
   with a radius and strength; density is the strongest falloff plus a base
   fabric plus domain-warped noise so a core is a lobed blotch, not a disc. A
   second noise gives *character* (historic quarter vs industrial). Together
   they classify every chunk as Core, Urban, Residential, OldTown, Industrial
   or Outskirts. Chunk (0,0), where every new game spawns, is pinned to the
   middle of a core.
2. **`CityLayout`** (rewritten) - the road hierarchy is now irregular and
   density-driven:
   - arterial lines are a jittered lattice (14 to 34 chunks apart) instead of
     every 24th chunk, so districts are a patchwork of different rectangles;
   - a district's local street period follows its zone (tight in an old town,
     medium downtown, wide superblocks at the edge) and streets are spread
     *evenly* across whatever width the jittered arterials left it, so no
     block is ever a sliver and nothing has to divide anything;
   - highways are every 4th X arterial *by line index* and exits are every 3rd
     Y arterial, so a highway is always a real arterial and an exit is always
     on a real cross street;
   - `IsStreetChunk` stays a pure function of the chunk, cached per district in
     a thread-static direct-mapped table (no locks, no ordering).
3. **`CityBlocks`** (rewritten) - blocks are 2x2 to 9x9 chunks and are split
   by recursive subdivision (cut along the longer side anywhere in the middle
   30-70%, until every piece is at most 2x2 chunks), then each piece either
   stays one big lot or breaks into smaller ones with a probability set by the
   block's zone. Every chunk re-runs the same subdivision from the block seed,
   so nothing is communicated between chunks. Open blocks (parks, plazas) are
   far more common at the edge than downtown.
4. **`CityChunkGenerator`** - building mass, height, roofs and landmarks follow
   the zone. Heights blend one random draw with a density bias, so the
   skyline *climbs* toward a core. Supertalls only stand downtown; banks
   cluster downtown; supermarkets and hospitals serve the neighbourhoods;
   industrial zones grow wide low `Warehouse` sheds with chimneys and water
   towers. The dice are drawn in the same order as before, so only what a
   number means changed.
5. **Open blocks** (`CityMeshBuilder.AddOpenBlockSurface`, `CityStreamer.AddGreenery`):
   a Core/Urban open block is a paved plaza; a park stays bare terrace and an
   industrial one stays bare yard. The NATURE treatment (a lawn with 3-7 blocky
   trees in a park, 1-3 trees in a plaza) is built but SWITCHED OFF
   (`CityMeshBuilder.DrawNature = false`) after the feedback "remove the trees i
   didnt want any nature yet"; flip it to bring it back. No colliders;
   deterministic per chunk.
6. **Windows light up by zone** (`WindowGrid.LitShareFor`): downtown 46% of
   windows lit, industrial 10%, so the city has a night-time silhouette of
   where things happen. The same hash decides which cells light, so retuning a
   share never reshuffles a building.
7. **A latent mesh bug the re-laid city exposed:** an edge shared by two
   chunks of the *same* multi-chunk building was being treated as a terrace
   edge and got stairs and guardrails *inside* the building footprint (the top
   tread landing coplanar with the lobby floor). `BuildGround` now skips
   edges, and diagonal corners, between chunks of one building.

## Streaming cost

A chunk's geometry cost grows with building height (measured about 0.5ms under
40m, 2ms at 100m, 4.5ms at 200m, 10ms at 300m). Zoning moved the tall
buildings into cores instead of scattering them, so the city-wide average is
unchanged (~1.06ms per building chunk) but the spawn core costs roughly 2.5x
what the old spawn area did. `ChunkBuildCostTests` pins both numbers. Core
tower and supertall shares were trimmed for this reason.

Because a chunk *count* per frame is no longer a fair budget when chunks cost
0.5ms to 10ms, `CityStreamer` also has `ChunkBuildTimeBudgetMs` (6ms): the
per-frame streaming stops at whichever of the count (6 chunks) or the time is
reached first, and always builds at least one chunk so it can never stall. The
synchronous spawn pre-stream and the capture tools pass no time limit.

## Iterating on layout without Unity

Every layout rule is a pure function, so `tools/cityplan` (a small `dotnet`
console project that links the very same `World/*.cs` files the game uses)
renders the plan, a height heat-map and the zone map to PNG and prints the
mass/zone statistics in a couple of seconds. That is how the numbers above were
tuned.

```bash
dotnet run -c Release --project tools/cityplan -- -96 95 6 plan.png
```

Arguments: `<minChunk> <maxChunk> <pixelsPerChunk> <out.png> [worldSeed]`; it
also writes `<out>_height.png` and `<out>_zones.png`. `-- bench` (instead of
the numbers) times the layout queries.

## Deliberately not done

- **Tensor-field / free-form streets.** They would give organic, non-axis
  streets, but every consumer of the street grid (lot placement, pedestrian
  navigation, the highway deck, elevated bridges, skybridges, doors) is built
  on the chunk lattice, and chunk independence is what lets a client and server
  agree on the city without sending geometry. Irregular arterials and
  density-driven street fineness capture most of the visible variety at a
  fraction of the risk. If organic streets are wanted later, this is the seam
  to cut along.
- **Wave Function Collapse for buildings.** The block subdivision plays the
  same role (deterministic, local, no backtracking) with far less machinery.

## Tuning knobs (all in `World/`)

`CityZones`: `CentreCellChunks`, radii and strengths in `Centre`, `BaseDensity`,
`LobeAmplitude`, `WarpChunks`, the zone thresholds. `CityLayout`:
`ArterialJitterChunks`, `MinStreetGap`/`MaxStreetGap`, the per-zone period
tables, `LayoutSeed`. `CityBlocks`: `OpenBlockChanceFor`, `TowerChanceFor`,
`SlabChanceFor`. `CityChunkGenerator`: `HeightBiasFor`, `SupertallShareFor`,
`LowRiseShareFor`, the roof tables and `SpecialWeight`.

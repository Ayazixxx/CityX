# Frame rate: what was slow, what fixed it

Measured in a real built player (`CityX/Builds/Bench`, see below), RTX 3070 Laptop + Ryzen 7 5800H, 2560x1600
windowed, the city at its normal 24-chunk view distance, a player sprinting down the streets.

| | sprinting fps |
|---|---|
| Start (D3D11) | 25 |
| Start (D3D12, the default API) | 18 |
| + empty submeshes not drawn | 50 (D3D11) |
| + far version of distant chunks | 58-67 (D3D11) |
| same build, D3D12 | **85-95** |

## What the numbers said

- **Not resolution, MSAA, render scale or shadow distance.** 1080p and 1600p were the same; MSAA off, 60% render
  scale and a 100 m shadow distance changed nothing.
- **It followed how much city was loaded.** Radius 6 / 10 / 16 / 24 chunks = 207 / 102 / 47 / 26 fps.
- **A census of the scene** (`PlayerFrameBenchmark.Census`): ~6,000 mesh renderers, 10M triangles loaded, ~5,600
  draws (1,750 transparent) and 3.6M triangles inside the camera frustum.
- **Draws, not triangles.** The far version cut loaded triangles 10.5M -> 1.8M and GPU time did not move; the
  cost was per draw call (~8 us each on D3D11).

## The three fixes

1. **Empty submeshes are real draw calls.** Every chunk mesh had five submeshes (ground, pavement, walls, dark
   glass, lit glass) and a street chunk fills two of them. `CityStreamer.AssembleChunk` now gives a mesh only the
   submeshes that have something in them. This alone took 26 -> 50 fps.
2. **Far version of distant chunks** (`CityMeshBuilder.BuildChunk(far: true)`, `BuildBuildingShell`). Beyond
   `CityStreamer.FullDetailRadiusChunks` (8 chunks) a building is its outside only: one panel per wall, the roof,
   glass laid over the wall as strips that merge across neighbouring windows. No rooms, stairs, window openings,
   back faces, markings, doors, benches. About half of a downtown chunk's triangles were rooms. Far chunks keep a
   collider (the shell), so nobody flies through a building whose full version has not streamed in. Swapping is
   both ways with hysteresis (`DetailHysteresisChunks`).
3. **Chunk generation on worker threads** (`GeneratorThreads`, `AssembleTimeBudgetMs`). The main thread only
   makes the objects. Standing p99 65 -> 44 ms, main-thread CPU 22.8 -> 18.9 ms before the other two fixes.

## D3D12 is now the faster API

With thousands fewer draws D3D12 (Unity's default here) beats D3D11 (85-95 vs 58-67 fps). Its earlier problem
was draw-call volume. Nothing was changed in the project's graphics API settings.

## Measuring

```
Unity.exe -batchmode -quit -projectPath CityX -executeMethod CityX.ViewEditor.PerfBuild.BuildBenchmarkPlayer
CityX/Builds/Bench/CityX.exe -cityxbench -screen-width 2560 -screen-height 1600 -screen-fullscreen 0
```

Flags: `-force-d3d11`, `-bench-msaa N`, `-bench-shadow M`, `-bench-shadowres P`, `-bench-cascades N`,
`-bench-scale S`, `-bench-radius CHUNKS`, `-bench-fullradius CHUNKS` (0 = no far version), `-bench-threads N`,
`-bench-standing S`, `-bench-running S`. Results go to `bench-result.txt`; the census (renderers, draws,
triangles, in-frustum) is in it.

## Still on the table

- Merge far chunks into blocks (4x4 chunks per mesh): draws in view ~2,500 -> a few hundred.
- Frame pacing: ~20% of sprinting frames land at 17-20 ms even though CPU and GPU are each ~6 ms busy (windowed
  presentation); not chased.
- Far glass is still the transparent material; an opaque far material would leave the transparent pass empty.

## Update: shooting windows no longer rebuilds the chunk (2026-10-09)

Reported: frame drops spamming pistol shots at a window. Every bullet hole fired `GlassDamage.Changed`, and `CityStreamer.OnPaneChanged` destroyed
and rebuilt the whole chunk -- its big mesh, tangents and MeshCollider cook -- synchronously, once per shot. Now another hole in an already cracked
pane swaps only the chunk's `CrackedGlass` and `CrackDecals` children (`ReplaceGlass`); and with worker threads every refresh is generated in the
background while the old chunk stays on screen, a burst of hits on one chunk coalescing into one rebuild (`_refreshing`). Test:
`WindowHitRefreshPlayTests`.

## Update: render distance (2026-10-09)

The streamer reached 24 chunks (576 m) but the clear-weather fog ended at 380 m, hiding a third of it. Now `CityMassingSceneBuilder.ViewRadiusChunks`
= 30 (720 m) with the fog at 460-690 m: you see about 690 m instead of 380. More far-detail chunks are loaded (61x61 instead of 49x49); watch the
frame time on big views.

## Update: buildings popping in up close (2026-10-12)

Reported: "I keep witnessing buildings loading in in real time up close." Measured first (`StreamingPopInBench`, PlayMode, Explicit: the real
scene, the game's own varied city, a viewer moved at the player's real speeds with the main thread held to 60 fps; writes
`Logs/streaming-bench.txt`): holes within ~100 m of the viewer in every frame even standing still, and in every frame of a boosted jetpack
flight, as close as 1 m. `ChunkCostBench` (EditMode, Explicit) found why: the median chunk built in 0.8 ms, but five round the spawn took
**23-55 seconds** -- each holding a big building whose **stairs** took 11-27 s to plan (`BuildingStairs.PlanFor`), asked for four or five
times per build (the builder, the room lights, the roof hatch, the interior volume). The walking check (`BuildingStairs.WalkPath`) laid a grid
over the whole floor with at most 40,000 cells, so a 120 x 39 m storey got 34 cm cells -- wider than the 30 cm a doorway leaves a 0.45 m-radius
body -- every door read as shut, nearly every stair placement "failed", and the planner tried them all.

Fixed: `WalkPath` keeps a fixed 25 cm grid but only over the box round the two ends and the solids (2 m out, clipped to the floor), and stamps
the solids onto it instead of testing every cell against every solid; `PlanFor` remembers its plans (a pure function of its inputs; bounded
cache). Chunk build: mean 400 ms -> 3.3 ms full, 199 -> 0.4 ms far; worst 55 s -> 0.12 s. Streaming bench after: no holes within 100 m while
sprinting, jetpacking or flying at 45 m/s; standing after a world rebuild, filled in 0.37 s. The streamer now uses as many worker threads as
the machine can spare (`GeneratorThreads` -1 = half the logical cores less one, 3 to 6; at least two chunks in flight per thread) for the
detailed versions round a very fast flyer. Guarded by `StreamingCostTests` (a doorway is passable on a big floor; a 120 x 39 x 171 m building
plans its stairs in well under a second; no chunk round the spawn takes a second in the game's own city).

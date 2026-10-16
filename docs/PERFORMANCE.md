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
`-bench-standing S`, `-bench-running S`, `-bench-flying S`, `-bench-flyspeed M/S` (default 22, a boosted jetpack),
`-bench-exact S` (0 = off), `-bench-gcslice MS`. Results go to `bench-result.txt`; the census (renderers, draws,
triangles, in-frustum) is in it. Three phases: standing, sprinting, and flying 40 m over the ground (the body carried along a
straight line). Each reports its garbage collections, the managed heap, how much was allocated, the chunk mesh arrays made new and
held (`ChunkArrayPool`), and how long the game stood still: a watchdog thread does nothing but read the clock, and a collection
suspends every thread, so a gap in its readings is a stop (matched against the frames a collection finished in). After each phase
the same motion goes on for `-bench-exact` seconds with the collector switched off, which gives what was allocated exactly. The
city scene runs alone in this build, with no network manager: the animator sync and the room-light switches' relay are switched off.

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

## Update: spraying an AK-47 at windows (2026-10-14)

Reported: "serious frame drops when rapid firing the ak at windows". Measured (`WindowSprayFramePlayTests`: the real AK, real windows, background
streaming, 29 shots across a row of panes): a bullet itself costs ~0.3 ms, but every few frames a whole rebuilt chunk was swapped in on the main
thread for **15-20 ms** (95th-percentile frame 17.5 ms): ~5 ms `Mesh.RecalculateTangents`, ~9-10 ms cooking the chunk's MeshCollider. Three causes:

- **A bookkeeping bug**: a hit landing while a refresh was on its way was folded into THAT refresh's kind and the follow-up reused it, so once a
  pane cracked (a whole-chunk refresh), every further hole -- glass only -- became a whole-chunk refresh too, for as long as the shooting went
  on. Now the changes that arrive during a refresh decide the next one's kind on their own (`_pendingRefresh`). Test:
  `MoreHolesInACrackedWindowReplaceOnlyItsGlassNotTheWholeBlock` (fails with the old logic: the block swapped twice for one crack).
- **Tangents** are worked out on the worker with the rest of the chunk (`MeshTangents`, the classic per-triangle method; within 1 degree of
  Unity's own on flat faces, same handedness: `MeshTangentsTests`). This takes ~5 ms off EVERY chunk that streams in, not only rebuilt ones.
- **A window hit while its chunk was still streaming in** (a block being upgraded from its distant version) made the chunk be generated
  AGAIN on the main thread when it landed (20-30 ms; seen when the spray started before the city had settled). Now that version goes in
  as it is and the change follows as a background refresh.
- **The collider of a rebuilt chunk is cooked on a worker** (`Physics.BakeMesh` in a job, `BeginSwap`/`CompleteSwaps`): the new chunk waits
  switched off, the old one stays drawn and solid, and the swap happens when the cooking is done (a frame or two later), costing ~1 ms.

Also preloaded at start: the glass sounds and the shard burst's twelve convex colliders (the first window to shatter cost ~10 ms once), and the
gun's tracer material and effects when it is drawn. After: 95th-percentile frame **2.4 ms**, worst **~4-5 ms** across the spray.

A chunk that STREAMED IN still cooked its collider on the main thread; since the same day it does not either (next section).

## Update: chunks streaming in no longer cook their collider on the main thread (2026-10-14)

Reported: hitches walking, sprinting and flying the jetpack. Measured first (`CityStreamerFramePlayTests`, PlayMode: a real streamer with its
worker threads, in the game's own districts -- the tests otherwise pin the classic city, whose small buildings cook in a few ms and hid the
problem -- the far version out to 8 chunks and the full one within 4, flown through at 22 m/s for six seconds with the main thread held to
60 fps). Every chunk had its collider cooked on the main thread in the frame it landed: ~10 ms for an ordinary chunk, **77-107 ms** for a
block of towers (250-300k vertices), on top of 36-53 ms building its mesh. Worst streaming frame **150 ms**, 99th percentile 91 ms, 11-13
frames over 8 ms, 755-857 ms of main-thread time in the six seconds.

Now everything the background streaming makes (new chunks, far/full detail swaps, window rebuilds) is built switched off, its collider
cooked on a worker (`CityStreamer.BeginSwap`, `Physics.BakeMesh`, which is thread-safe and runs on the calling thread), and switched on
when that is done (`CompleteSwaps`; a MeshCollider given the mesh with the same cooking options takes the cooked data as it is). The
version on screen, if any, stays drawn and solid meanwhile: nothing falls through, nothing blinks. A chunk waiting for its cook stays
"in flight" (nothing else starts for it; window hits on it follow once it is on screen); one the viewer has left behind meanwhile, or
whose version on screen has been dropped, is thrown away. `Rebuild()` and switching the streamer off wait for a cook under way (a mesh
must never be destroyed under one) and cancel the rest. The synchronous path (spawn pre-stream, captures, tests) still cooks at once.

**On the streamer's own threads, not Unity's job system** (where window rebuilds cooked at first): a job is never interrupted, so a
100 ms cook holds one of the engine's own workers all that time, and a main thread waiting on some other job may run a waiting cook
itself. Up to half as many cooking threads as generator threads, taken from the thread pool only while there is something to cook. A
probe (`MeshCookContentionProbe`, Explicit) shows a cook on another thread does not hold up the main thread's own collider work: during
a 131 ms cook, switching colliders on and off, a raycast and a small cook all stayed under 0.3 ms.

After, same bench (three runs): worst streaming frame **25-35 ms**, 99th percentile 11-16 ms, 5-7 frames over 8 ms, **145-155 ms** of
main-thread time in the six seconds; switching cooked chunks on costs at most ~1.4 ms in a frame. Tests (`CityStreamerFramePlayTests`): chunks do wait
for their collider, and switching them on stays under 5 ms a frame (frames with a garbage collection are reported, not judged); after the
flight the ring is whole, one object per chunk, every one switched on and solid; a chunk that leaves range while it cooks, and a rebuild
or a switch-off while chunks cook, leave nothing behind and leak no mesh (the last two fail with the old code).

Still on the main thread (measured, not done):
- **Building a big chunk's mesh** (`AssembleChunk`): DONE the same day, 20-46 ms -> 2-3 ms for a block of towers (next section).
- **Room lights**: a powered block's lit rooms cost 0.5-0.8 ms each to build (10-16 ms for 20-30 rooms). Done the same day: 0.06-0.2 ms a
  block now (below).
- **The first powered block of a session**: its entrance lamp (`AddEntranceLamps`, `EntranceLamp.Build`) 3.0 ms and its room lights
  4.4 ms, code running for the first time (12 ms for the lamp once). The next powered block costs under 1 ms in all, and every other
  chunk's small parts under 0.3 ms. A one-time cost; not looked into.
- **Garbage collection**: a big chunk's buffers are tens of MB of garbage; collections of 30-270 ms were seen in the editor bench runs.
  Measured in the player and done the same day: 343-348 collections in a 30 s flight -> 56 (below).

## Update: a streamed chunk's mesh is copied in, not built, on the main thread (2026-10-14)

Next after the colliders: making a chunk's main mesh in the frame it lands. Measured first (`CityStreamerFramePlayTests`, the same flight,
temporary timers inside `AssembleChunk`, two runs): a block of towers (250-300k vertices) took **20-46 ms** of one frame with the classic
Mesh API -- `SetVertices`/`SetNormals`/`SetUVs`/`SetColors` 10-25 ms and `SetTangents` + `RecalculateBounds` 9-19 ms (each attribute
added repacks the whole vertex buffer; the bounds read every vertex again), `SetTriangles` 1-3 ms. Those were the worst streaming frames:
37-47 ms, 99th percentile 21-24 ms, 203-214 ms of main-thread time in the six seconds.

Now the worker lays the mesh out the way a Mesh keeps it (`ChunkMeshData`): positions, normals and tangents in a stream each, colour and
UV sharing the fourth (a mesh has at most four), every submesh's indices in one array (16-bit up to 65,000 vertices, 32-bit above, as
before), and the submeshes' vertex ranges and the bounds worked out. The main thread gives the layout once (`SetVertexBufferParams`),
copies each stream in one block (`SetVertexBufferData`) and the indices in one (`SetIndexBufferData`), and sets the submeshes and the
bounds as they are, with nothing checked or recalculated (`MeshUpdateFlags`). It is the same mesh: `ChunkMeshDataTests` builds real
chunks both ways (a block of towers, its far version, a street) and compares every vertex, index, submesh and bound; `ChunkMeshPlayTests`
checks each submesh's material and that every chunk is solid, made at once and streamed in.

After (same bench, five runs, the last three with the same day's room-lights rework in): the same blocks of towers take **2.0-2.8 ms**
(copying the vertex streams 1.7-2.2 ms, everything else under 0.5 ms); on their own (`ChunkMeshDataTests`) 2.0 ms against 14-19 ms the
classic way for 243k vertices. Worst streaming frame **8.3-8.8 ms** (one run also had a 15.6 ms frame, a garbage collection), 99th
percentile 2.9-3.3 ms, one or two frames over 4 ms, **56-72 ms** of main-thread time in the six seconds. The worst frame left is the first
powered block of the run landing (20 lit rooms): its entrance lamp 3.0 ms and its room lights 4.4 ms, both code running for the first
time (the next powered block, 30 rooms, costs under 1 ms in all), its mesh 0.5 ms. Not in these numbers: the mesh's upload to the
graphics card when it is first drawn, which only the real player does (the bench runs without graphics). The look is unchanged: the
fourteen views of `CityFeaturesCapture` and the eight of `LightingCapture` (morning to night) render the same as before, pixel for pixel
but for a few on the player standing in two of them.

## Update: streaming no longer throws its buffers away, garbage collections 6x rarer (2026-10-14)

The last item of the list above. Measured first in the real built player, not the editor (whose heap is many times the game's, so its
30-270 ms collections said nothing about the game), with the benchmark's new flying phase (Measuring, above): 30 s at a boosted jetpack's
22 m/s, 40 m over the ground, ~3,200 chunks put on screen.

Before (two runs; another session's Unity editor was running alongside, so the frame times are noisy, the collections are not):
incremental collection is on (3 ms slices) and the managed heap is small -- 50-160 MB used, ~300 MB reserved -- so there were no long
pauses; but the collector never stopped. **343-348 collections in the 30 s flight**, one every 0.09 s, ~**60 MB of garbage a second**;
each stopped every thread -- the main one, the generator workers, the collider cooks -- for 2-3 ms, mostly twice, up to 5-12 ms. The game
stood still **1.7-2.0 s of the 30** (6%). Sprinting: 44-45 collections, 8-10 MB/s. Why so often with so small a heap: the collector
(Boehm, under Mono) starts a cycle every time a few percent of its heap has been allocated -- about every 5 MB here -- and chunk
streaming threw away ~170 bytes a vertex: every chunk was generated into fresh buffers whose lists grew from empty (each growth throws the
last list away), then copied into fresh arrays, tangents worked out in more: tens of MB for a block of towers (250-380k vertices).

Now that storage is used again, chunk after chunk:
- **The five buffers a chunk's main mesh is built in** (`CityStreamer.ChunkScratch`: ground, pavement, walls, dark and lit glass): one set
  per chunk generated at once (a worker each), emptied (`MeshBuffer.Reset`: exactly what a new buffer is, but with the room its lists grew
  to) and handed to the next chunk.
- **The main mesh's arrays** (`ChunkMeshData`) come from `ChunkArrayPool` and go back (`Release`) once the main thread has copied them into
  the Mesh. Lengths come in half-octave steps (at most 41% longer than needed; the counts say how much is used). At most 192 MB is held:
  when more is given back, the sizes rented least lately go first (`ChunkArrayPool.Hold`), so what the chunks streaming now need stays.
  The tangents (`MeshTangents`) sum each corner's U direction in the tangent array itself and read the UVs from the colour-and-UV stream:
  one working array left of three.
- **The payload** that carries a chunk to the main thread, with its small buffers (markings, guard rail, cracked glass), is given back
  (`Recycle`) on every path -- made, thrown away (out of range, a rebuilt world, already there), a window refresh, a failure -- and
  `PayloadsOut` counts what is out.
A chunk is still a pure function of its coordinates: everything is emptied or overwritten before it is used. `ChunkStorageReuseTests`
builds a block of towers, a street and others in storage that has held dozens of chunks and compares them bit for bit with fresh
storage, and checks that streaming the block in again leaves little garbage; `ChunkStorageReusePlayTests` streams, shoots a window
(whole and glass-only rebuilds, some while one is on its way), moves on and rebuilds the world, then checks every payload came back (it
fails if one path forgets); the flight test checks it too; `ChunkArrayPoolTests` the pool (limit, least-lately-used first, threads).

After (same player benchmark, quiet runs, which also have the same day's mesh and room-light changes): **56 collections in the 30 s
flight** (one every 0.5 s), the game standing still **0.33 s in all**, longest stop 5.9 ms; worst frame **18.5 ms**, 99th percentile
13.9 ms, no frame over 33 ms (18-37 before). Sprinting: 9 collections in 30 s, 5 MB/s. Only 12 MB of new mesh arrays were made in the
flight. A block of towers (311k vertices) streamed in again leaves **0.03 MB** of garbage (its mesh's arrays alone are 17 MB, and its
buffers grew through tens of MB more, every time, before); a chunk on average ~0.24 MB in the editor (`ChunkGarbageBench`), of which
generating it is ~57 KB and making its objects on the main thread 0.2 KB.

The price is memory: the managed heap holds ~190 MB of pooled arrays and ~120 MB of build buffers (six sets, each grown to the biggest
chunk its worker made), ~360-400 MB used and 460 MB reserved against ~300 MB reserved before. A tighter pool costs collections:
without a tight limit it held 350 MB and gave 44-48 collections; with fewer arrays held the heap is smaller and the collector, which
starts a cycle every few percent of the heap, runs a little more often.

Still allocating (measured, not done): with the collector switched off for 3 s of flight, **~36 MB/s, ~0.38 MB a chunk**. Not the mesh
storage (12 MB of new arrays in the whole flight), and little of it a chunk's own generation where the city round it has been worked
out already (57 KB). Ground seen for the first time costs more: in the editor a chunk's first build leaves ~0.16 MB more than its second
(the city plan worked out for it, `World/Plan`: garbage and caches). The rest is not broken down yet; the next place to look, with a
profile of where the flight allocates. Standing still with the city loaded allocates nothing (1 MB in 3 s).

## Update: render distance (2026-10-09)

The streamer reached 24 chunks (576 m) but the clear-weather fog ended at 380 m, hiding a third of it. Now `CityMassingSceneBuilder.ViewRadiusChunks`
= 30 (720 m) with the fog at 460-690 m: you see about 690 m instead of 380. More far-detail chunks are loaded (61x61 instead of 49x49); watch the
frame time on big views. **2026-10-15:** a third tier past the ring, the skyline (`SkylineStreamer`: boxes to 2.2 km, about 2,500 vertices a
384 m group, no colliders), and the fog out to 2200 m: `docs/RENDER_DISTANCE.md`.

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

## Update: a powered block's room lights cost nothing until a switch is flipped (2026-10-14)

The room lights were the next main-thread cost of a chunk streaming in. Measured with a temporary timer in `CityStreamer.AddRoomLights` (the
streamer flown through the game's own districts, as in `CityStreamerFramePlayTests`): a block of 20 powered rooms took **24.4 ms** of one frame
(294 spot lights and 40 meshes made, all switched off), one of 30 rooms **14.1 ms**. A spot light alone costs 25-40 us to make. Nearly all of it
was for nothing: a room lights only when somebody flips its switch.

Now a powered room is a plain object on its chunk (`RoomLightGroup`, one `ChunkRoomLights` component per chunk), its glow and windows are drawn
only once it is lit (one mesh per light colour per chunk), and its real lights are lent by `RoomLightDirector` from a pool (at most 220, made at
most 32 a frame) while it is lit and in reach. Same blocks after: **0.16 ms** and **0.06 ms** (the first powered block of a session 4.5-6 ms
once in the editor: its code running for the first time); twenty rooms of a block switched on at once cost 1.25 ms. The look is the same to the
pixel (old and new code rendered). Details and tests: ROOM_LIGHTS.md, "nothing is made for a dark room".

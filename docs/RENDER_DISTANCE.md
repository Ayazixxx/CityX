# Render distance: the skyline tier (2026-10-15)

*Later the same day:* a tall tower's mast, spire or crown (`World/TowerTop`, docs/WORLD_GACHA.md phase 2b) is drawn on its skyline box as a
stub (`CityMeshBuilder.AddSkylineTop`: a pole, a few tapering boxes, four slabs in pale metal), so the far towers keep their silhouettes;
`SkylineTests` lets a box rise to its top's reach.

Asked (owner, 2026-10-15, of @_chuckyc's OpenMapTiles earth game): "the render distance on his looks waaay better than ours i want mine like
that". Before: `CityStreamer` built the city in full detail to 8 chunks (192 m), as far shells to 30 chunks (720 m), and the air (`CityHaze`,
driven by the fog distances 460-690 m) hid the end of it: from a roof or a jetpack the city stopped at a wall of haze a few blocks out
(`Logs/CityShots/before/horizon-*.png`).

## What was built

- **`View/SkylineStreamer.cs`** -- a third tier past the streamed ring: every building of 9 m or more as ONE BOX in its own wall colour (a round
  tower as the prism of its outline), and the ground as a plain floor following the land (water in water's colour), one mesh per group of 16 x 16
  chunks (384 m square), out to `RadiusMetres` 2200 m (127 groups round the spawn; about 2,500 vertices a group near the centre, nothing else).
  No colliders, no shadows, nothing small. Generated on the thread pool from the plan alone (`CityMeshBuilder.BuildSkyline`), assembled one group a
  frame; the game's start builds them all on the spot (`BuildAllAtStart`) so the horizon is there from the first frame.
- **Keeping out of the real city's way.** A box stands `SkylineInset` 0.35 m inside its building's walls and ends 0.35 m under its roof, and the
  floor lies `SkylineFloorDrop` 2 m under the ground, so a FAR chunk (an opaque shell with its glass floating in front) covers them. A FULL-DETAIL
  chunk has real windows and rooms a box would fill, so the chunks within `ExcludeRadiusChunks` 14 (the full-detail radius 8, the hysteresis 2,
  and a step of 4) of the viewer's chunk get no boxes and no floor; a group near the viewer is built again once the viewer has moved
  `RebuildStepChunks` 4 chunks from the chunk it was built for (`NearViewer`: only the groups the exclusion can touch; far groups never rebuild).
- **The air.** `CityMassingSceneBuilder.ClearFogStartMetres` 460 -> 600 and `ClearFogEndMetres` 690 -> 2200 (= `SkylineStreamer.RadiusMetres`):
  the haze's edge fade (`CityHaze.EdgeFadeFraction`, from half way between the two) now runs from 1400 m to 2200 m, where the skyline ends in the
  sky's own colour; the camera's far plane 2000 -> 3000 m. The near city is as clear as before (the exponential haze is unchanged: 0.0015 a
  metre at street level, thinning with height).

## Pictures (`HorizonCapture`, Explicit, `-Graphics`; `Logs/CityShots/horizon-*.png`, the old ones in `before/`)
From 400 m over the spawn by day: the city runs on to the horizon, the far districts as coloured blocks melting into the haze, where it used to
end at a wall 700 m out. From a jetpack at sunset: the far towers stand as silhouettes in the haze. From the street: unchanged (the longest
street view is 107 m).

## Tests
`SkylineTests` (EditMode): a group far from the viewer has a floor quad a chunk and a box inside every tall building (every vertex inside its
building's footprint and under its roof), the floor at the ground less the drop at every corner, the chunks round the viewer get nothing, the
group maths (GroupOf, ChunkOf, Distance, NearViewer). `SkylinePlayTests`: groups come within the radius and go when the viewer leaves (6 km),
`StreamAround` builds everything at once with a material on every submesh, a group near the viewer is rebuilt for the viewer's new chunk after a
five-chunk step while a far one is not.

## Not done / next
- The far ground is one grey: at the edge of the streamed ring (720 m) the real streets and blocks hand over to a plain floor. Fine in the
  haze from a roof; from 400 m up it is a flat grey sea. A next step is the main roads as darker strips in the floor (the plan knows them).
- Boxes have no roofs of their own colour (the wall colour tops them); far roofs are mostly the parapet colour anyway.
- Night: the skyline has no lit windows. A sprinkle of lit faces in the box colours would carry the night city to the horizon (the far shells
  have them to 720 m).

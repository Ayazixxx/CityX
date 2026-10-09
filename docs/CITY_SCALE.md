# City scale: bigger buildings, same city (2026-10-02)

> **Update, same day: the default is now VARIED districts, not uniformly huge.** Asked: "buildings as small as before this change and as large as they can be now, a wide variety, like districts so larger buildings tend to be around larger ones, but not exclusively." See "Varied districts" below; the uniform presets are still there.

Asked: "make the buildings in the city orders of magnitude larger, both significantly wider and significantly taller. I want to see how this would
look but still remember how the city as we have it now is configured."

## How to remember the old city
- **Git tag `city-classic-2026-10-02`** is the last commit before any of this (stairs and paint fixes included).
- **`CityScale.Classic` (footprint 1, height 1) is the old city, building for building** (`PlanConfig.For(Classic)` equals the old default; a test pins it).
  Every test runs pinned to classic (`ClassicCityScaleFixture` in both test assemblies); the game starts at `CityScale.Default` (Varied).

## How to switch
- **Editor:** menu **CityX / City Scale /** Classic, Large, Huge, Colossal (kept in EditorPrefs; press Play to rebuild the world).
- **Anywhere:** environment variable `CITYX_CITY_SCALE` = `classic` | `large` | `huge` | `colossal` | `footprint,height` (for example `6,4`). It beats the menu.
- **Built player:** `CityScale.Default` in `World/Plan/CityScale.cs`, or the variable.

| preset | footprint | height | typical building | tallest |
|---|---|---|---|---|
| classic | x1 | x1 | 31 m long side, 34 m tall | about 320 m |
| large | x2.5 | x2 | | |
| huge | x4 | x3 | 133 m long side, 100 m tall (median) | about 720 m |
| colossal | x8 | x5 | | |
| **varied (default)** | x1 to x4 by district | x1 to x3 | 44 m long side, 52 m tall (median); 21 m to 124 m (5th to 95th percentile) | about 700 m |

## What scales
FOOTPRINT multiplies every length in the plan (`PlanConfig.For`): road growth, snap distances, bridge spans, hills, rivers, population, the downtown radius, the
region (10 km half width at huge, 20 km across), block and lot sizes, setbacks, massing. The result is the classic plan drawn bigger (same topology; the huge
city has 17,218 buildings, the classic 13,640). Roads widen only by its square root (a street twice as wide at x4), hills by the same.
HEIGHT multiplies building heights and the thresholds that make a tower or a supertall (judged relative to the classic metres), and the podium's storeys.
Windows are wider by the square root of the footprint (3.2 m instead of 1.6 at huge), floors are still 3.2 m a row and a storey is still two rows.

## What had to change to make it playable (measured, RTX 3070 Laptop, 2560x1600 release player)
First try: 143 fps standing, 119 sprinting, a 1 s hitch when a tower streamed in. A tower's chunk was up to 2 million vertices (100 MB of mesh), and
tangents and the collider are built on the main thread. Fixes:
1. **A building sealed above its last fitted-out floor (taller than 11 storeys, about 70 m) is only detailed that far.** Above it, the walls are the distant
   version: one panel per wall and flat glass strips (`BuildBuilding`, `AddShellGlass(firstRow)`). Nobody sees into a sealed void. Applies to the classic city too.
   Result: 1.2M triangles in view instead of 4.9M, **195 fps sprinting, 199 standing, worst frame 78 ms**, biggest chunk 450k vertices (about 200 ms to assemble).
2. **Plan indexing**: blocks were registered chunk by chunk (hundreds of chunks each; 2.7 s). Now a coarse grid filtered by bounding box, and road edges register
   along the segment instead of its bounding box. Huge city plan: 0.8 s (classic 0.55 s).
3. **Landmarks**: a building 150 m or taller keeps its distant chunk loaded out to 2.5x the stream radius (`CityStreamer.LandmarkRadiusChunks`), so a 700 m tower
   does not vanish at 576 m. (Beyond the ring there is no ground, only the towers; fog hides the join.)

## Not done / to look at
- Everything sized in metres that is not the building: roof plant, signs, AC units, doors (3.2 m), awnings, props. At x4 they are small against the facade.
- Streets are narrower relative to the buildings (x2 for x4): canyons. Easy to change (`PlanConfig.For`: the road widths).
- The player, the jetpack and the camera are unchanged: the city is simply 4x as big to cross. Camera far plane is 2000 m.
- Interiors scale with the footprint (a carved "room" is 40% of the plan: 50 m wide at huge).
- Tangents and the collider of a large chunk are still built on the main thread (200 ms for the biggest); worker-thread baking would remove that hitch.
- The chunk of a very large building (colossal) can be far from its own door; full detail is chosen by the centre's distance.

## Varied districts (the default)
`CityScale.Varied` = footprint x1 to x4, height x1 to x3. A scale is now a range (`MinFootprint`/`Footprint`, `MinHeight`/`Height`):
- **Districts** (`PlanFields.DistrictAt`): smooth noise with a 1.5 km wavelength, spread so a district is clearly small or clearly large, nudged toward larger
  downtown. `ScaleAt(x, z)` / `HeightScaleAt(x, z)` blend the range by it (linearly: no pow, the plan must be identical on every machine).
- **The road network grows at the local scale** (`RoadNetwork.Local`): segment lengths, probes, snap distances, bridge steps. So a small district gets short blocks
  and a large one long blocks, and lots follow the blocks. (The uniform presets are the same code with a constant.)
- **Not exclusively**: each block's size is its district pushed up to +-0.2 of the range by a hash of the block (neighbours are not all alike), a lot carries the
  scale it was cut at (`PlanLot.Scale`, `HeightScale`) and a big district is taller. Roads widen by sqrt of the geometric middle of the range (x1.4 street widths).
- Window columns follow the building, not the city (`WindowGrid.ColumnScaleFor`: 1.6 m up to 3.8 m on a 200 m facade); classic stays exactly 1.6.
- The region is 10 km across (`Half = 2500 * RoadWidth^2`), 3,300 road nodes, 21,000 buildings (classic 13,600, huge 17,200); the plan takes about 1.7 s (classic 1.0 s).

Measured (`CityScaleTests`, seed 53190847), long side of a building: 5th percentile classic 17 m / varied 21 m, 25th 25 / 32, median 31 / 44, 75th 37 / 69, 95th 47 / 124.
A building's size is much more like its nearest neighbour's than a random building's (mean log-size difference 0.22 against 0.61, ratio 0.35), and not identical (a test pins
the ratio between 0.12 and 0.7).
Player build, 2560x1600: 176 fps standing, 170 sprinting, worst frame 93 ms (the uniform huge city: 199 / 195 / 78).

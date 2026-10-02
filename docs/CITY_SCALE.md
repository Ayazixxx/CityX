# City scale: bigger buildings, same city (2026-10-02)

Asked: "make the buildings in the city orders of magnitude larger, both significantly wider and significantly taller. I want to see how this would
look but still remember how the city as we have it now is configured."

## How to remember the old city
- **Git tag `city-classic-2026-10-02`** is the last commit before any of this (stairs and paint fixes included).
- **`CityScale.Classic` (footprint 1, height 1) is the old city, building for building** (`PlanConfig.For(Classic)` equals the old default; a test pins it).
  Every test runs pinned to classic (`ClassicCityScaleFixture` in both test assemblies); the game starts at `CityScale.Default` (Huge).

## How to switch
- **Editor:** menu **CityX / City Scale /** Classic, Large, Huge, Colossal (kept in EditorPrefs; press Play to rebuild the world).
- **Anywhere:** environment variable `CITYX_CITY_SCALE` = `classic` | `large` | `huge` | `colossal` | `footprint,height` (for example `6,4`). It beats the menu.
- **Built player:** `CityScale.Default` in `World/Plan/CityScale.cs`, or the variable.

| preset | footprint | height | typical building | tallest |
|---|---|---|---|---|
| classic | x1 | x1 | 31 m long side, 34 m tall | about 320 m |
| large | x2.5 | x2 | | |
| **huge (default)** | x4 | x3 | 133 m long side, 100 m tall (median) | about 720 m |
| colossal | x8 | x5 | | |

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

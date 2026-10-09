# Lighting

Researched 2026-10-05 (Unity 6 URP 17). Findings that drove the choices:
- **Hardware ray tracing is HDRP-only.** URP has none, and its GI answer (Adaptive Probe Volumes) needs baked data, which a streamed procedural city cannot have.
- So the honest wins inside URP are sky-driven ambient and reflections, better shadow resolution where it is seen, and AO.

Done:
- **Trilight ambient** (DayNightCycle.Apply): more, bluer light from the sky above, the base colour at the horizon, a darker warm bounce from the street
  below, instead of one flat colour. Same average brightness; roofs, undersides and the two sides of a street now differ.
- **Reflections from the real sky**: default reflection = skybox, re-captured with DynamicGI.UpdateEnvironment every 3 s, so glass and wet streets reflect
  the actual sky at the actual time of day.
- **4 shadow cascades** (was 2), splits 4% / 12% / 35% of the 260 m distance: crisp shadows near the player.
- Already in place: soft shadows, 4096 map, SSAO, Neutral tonemapping, bloom.

## The city's air (2026-10-09, "implement all the lighting stuff that makes the other cities look better")

The reference cities (Megacity, the Unreal city demos) read as big because of **aerial perspective**: the street is sharp, towers a few blocks off are
paler, the far skyline melts into the sky. Ours had a flat linear fog wall at 150-380 m that only hid the end of the streamed city.

- **Haze pass** (`View/Shaders/CityXHaze.shader`, `View/CityHaze.cs`): exponential height fog as a URP Full Screen Pass renderer feature, before
  post-processing, reading depth. Closed-form optical depth (Wenzel 2006 / Unreal's exponential height fog), no ray marching. Density 0.0015/m at street
  level, thinning by e every 150 m up; a storm makes it 3.5x thicker.
- **The haze is the colour of the sky in the direction you look** (the dome's own gradient, from `_CityXHazeSkyTop` and the horizon), brightened toward
  the sun. A far tower fades into exactly the sky behind it instead of into a pale wall.
- **Sky pixels** get haze only at and below the horizon (a 10 degree band), which closes the "angle problem" the old fog could not: from a roof or a
  jetpack you no longer see the dome's ground half past the edge of the streamed city.
- **The streamed-city edge** still comes from `RenderSettings.fogStartDistance/fogEndDistance` (the storm and the intro's cloud fall drive those): the haze
  fades to all-sky from halfway between them to the end. The built-in fog is switched **off** while the haze draws (`DayNightCycle.HazeDrawn`).
- `HazeSetup.EnsureFeature` (run by the scene builder) puts the pass on both the PC and Mobile renderers. Globals are cleared in `DayNightCycle.OnDisable`
  so the title screen never inherits the city's air.
- **Grade**: split toning (shade a touch cool, sunlight a touch warm), stronger bloom (threshold 0.95, intensity 0.5) so lit windows glow at night.
  **ACES was tried and reverted**: it turned every shaded street canyon dark navy and drained the facade colours. Neutral stays.
- Capture gotcha: the capture tests load the city scene additively, so the *test* scene's render settings apply. `BuildingShapeCapture.UseTheScenesAir`
  sets the city's fog distances first, or the haze edge lands at Unity's default 0-300 m and everything looks fogged.

Next candidates: tune SSAO radius/intensity against the new ambient, a screen-space reflection renderer feature for wet streets, volumetric light shafts
(a custom renderer feature), light-layer split for the sun vs window lights, emissive window glow with bloom thresholds. `LightingCapture` (Explicit, -Graphics)
renders four times of day to Logs/CityShots/light-*.png.

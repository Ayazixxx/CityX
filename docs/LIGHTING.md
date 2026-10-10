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

## Update: clear air round the player (2026-10-09)

"I don't want to be walking around the whole game in fog." The haze now has a clear bubble round the camera: none at all within
`CityHaze.ClearRadius` (25 m), fading in smoothly to full thickness at `CityHaze.FadeInEnd` (120 m). The street you stand in is crisp; the haze
only shows on what is genuinely far. The streamed-city edge fade (and the storm and intro-fall fog distances) are unchanged.

## The night (2026-10-09 lighting pass)

"The lighting really doesn't look good ... looking from outdoors during nighttime. Research ... city lighting at night time and implement what you
learn." Research (game night-lighting write-ups, the RenderHub lighting guide the owner sent, real night photography): night should **feel dark
without being dark** -- a dim, cool fill and a moon strong enough to be the key light that gives the city its shape and shadows; **warm practical
lights against the cool dark** are the contrast a night scene is made of; scant lights (a lit doorway, a lit room) say more than many; fog pushes
the background back. What changed:

- **Moon** (`SkyCycle.MoonIntensityPeak` 0.13 -> 0.3, colour a paler blue 0.70/0.78/1.0): it lights and shadows the streets now; the night is no
  longer one flat navy.
- **Stars** (`CityXProceduralSky.shader`, `_StarEnergy` driven by DayNightCycle: the square of the dark, hidden by overcast): hashed cells of sky,
  most stars faint and a few bright, a little twinkle, faded towards the horizon. With nine buildings in ten dark, the sky over this city is starry.
- **Night grade** (`CityNightGrade.asset`, a second global Volume whose weight is 1 - daylight): +0.7 stops of exposure over the day grade (eyes
  adapt), saturation -12, contrast +12, the darkest shadows a little blue and highlights warm (balance +25: a first try with strong blue split
  toning turned every lit room cold blue). The day grade is untouched.
- **Road paint is lit** (URP Lit, was Unlit): crossings no longer glow white in a dark street.
- **Lamps over powered doors**, **clear glass on real rooms**, **lit rooms that cannot shine through walls**: see ROOM_LIGHTS.md.
- Not done: no street lamps (removed 2026-09-04 on request: "they look bad and are clipping into the ground"); lit rooms beyond the full-detail
  radius (8 chunks) show nothing (far chunks have no rooms) -- a far lit window could be added to the far shell if it is missed.

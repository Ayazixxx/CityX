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

- **Haze pass** (`View/Shaders/CityXHaze.shader`, `View/Lighting/CityHaze.cs`): exponential height fog as a URP Full Screen Pass renderer feature, before
  post-processing, reading depth. Closed-form optical depth (Wenzel 2006 / Unreal's exponential height fog), no ray marching. Density 0.0015/m at street
  level, thinning by e every 150 m up; a storm makes it 3.5x thicker.
- **The haze is the colour of the sky in the direction you look** (the dome's own gradient, from `_CityXHazeSkyTop` and the horizon), brightened toward
  the sun. A far tower fades into exactly the sky behind it instead of into a pale wall.
- **Sky pixels** get haze only at and below the horizon (a 10 degree band), which closes the "angle problem" the old fog could not: from a roof or a
  jetpack you no longer see the dome's ground half past the edge of the streamed city.
- **The streamed-city edge** still comes from `RenderSettings.fogStartDistance/fogEndDistance` (the storm and the intro's cloud fall drive those): the haze
  fades to all-sky from halfway between them to the end. The built-in fog is switched **off** while the haze draws (`DayNightCycle.HazeDrawn`).
  **2026-10-15:** the distances are 600-2200 m now, the end at the edge of the skyline tier (`docs/RENDER_DISTANCE.md`): the far city melts into the sky
  over its last 800 m instead of stopping at 690.
- `HazeSetup.EnsureFeature` (run by the scene builder) puts the pass on both the PC and Mobile renderers. Globals are cleared in `DayNightCycle.OnDisable`
  so the title screen never inherits the city's air.
- **Grade**: split toning (shade a touch cool, sunlight a touch warm), stronger bloom (threshold 0.95, intensity 0.5) so lit windows glow at night.
  **ACES was tried and reverted**: it turned every shaded street canyon dark navy and drained the facade colours. Neutral stays.
- Capture gotcha: the capture tests load the city scene additively, so the *test* scene's render settings apply. `BuildingShapeCapture.UseTheScenesAir`
  sets the city's fog distances first, or the haze edge lands at Unity's default 0-300 m and everything looks fogged.

**Cel shading** (2026-10-24): every light goes through a cel-shaded version of itself and black lines are drawn round everything; see
`docs/CEL_SHADING.md`.

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

## Update: night is Minecraft-dark (2026-10-09, same day)

Owner, on the moonlit night above: "at nighttime it needs to be darker ... get rid of this greyish hue that appears when you distance yourself
from light", "it should be almost pitch black at night like minecraft", then "I don't want it any darker than minecraft". So:
- **The grey glow was mostly reflections.** The sky's reflection map is captured once (in daylight), so every window and wet street reflected a
  daytime sky at midnight. `RenderSettings.reflectionIntensity` now follows the daylight down to `DayNightCycle.NightReflection` (0.03).
- Moon 0.3 -> 0.06 (its disc kept bright with `MoonDiscGain` 20), night sky energy 0.15 -> 0.05 (the haze takes its colour, so the far city no
  longer fades to grey), dark rooms 0.92 indoor shade, night grade without the exposure lift, desaturation, contrast or toning.
- **Ambient colours are gamma**: Unity squares them on the way into the lighting (a 0.04 colour lights at 0.003). `AmbientNightEnergy` 0.65 is
  what leaves a faint navy glow on every wall, so streets and buildings stay just readable, as in Minecraft.
- Character skin and hair are matte (`CharacterSkin.MakeMatte`: gloss 0, no specular highlights): their sheen caught every lamp at night.
- Tests: `NightLightingTests.AwayFromItsLampsTheNightIsAlmostPitchBlack`, `TheCharacterAndItsHairHaveNoShine`.

## Ray tracing: why not, and what instead (researched 2026-10-09)

- Unity's built-in ray tracing (RT shadows, reflections, GI, path tracing) exists only in **HDRP**. This game is on **URP**, which has none.
  The low-level ray tracing API (RayTracingShader on DX12) could be driven from a URP renderer feature, but every effect would be ours to
  write, denoise and keep fast, and players without an RTX-class GPU would need a fallback anyway.
- **HDRP is not the way**: Unity's [Render Pipelines strategy for 2026](https://unity.com/topics/render-pipelines-strategy-for-2026) puts HDRP
  in maintenance (no new features) and names URP as where the investment goes, including "new beautiful 3D lighting runtime capabilities for
  dynamic and procedural worlds". Porting would also mean rewriting every custom shader (facade, glass, haze, sky, window light) and retuning
  the look, for a heavier renderer.
- **Alternatives that work in URP today**: screen-space global illumination (open source
  [UnitySSGIURP](https://discussions.unity.com/t/open-source-screen-space-global-illumination-for-urp/952599), Unity 6; commercial Radiant GI),
  screen-space reflections from the same author, and what this pass already does by hand (bounce lights, per-surface sky occlusion). Screen-space
  GI only bounces light from what is on screen, so it suits lit rooms and lamp pools; it cannot see round corners. A candidate for the next pass.

## Light shafts (2026-11-06/07)

Owner, 2026-11-06, of the graphics ideas offered: "lets try 1 2 3 and 5 leave 4 for later" (1 bounced light, 2 wet reflections, 3 light
shafts, 4 neon signs, 5 contact shadows). Contact shadows were built first and taken out again: the sun's shadows are already sharp near
things (4096 px, four cascades), so a screen-space contact shadow pass changed a few hundred pixels of a frame.

The light shafts (`LightShaftsFeature`, `Shaders/CityXLightShafts.shader`, set up by `LightShaftsSetup`) trace the air along each pixel's line of
sight in 24 steps, each asking the sun's shadow map whether the sun reaches it: the air in the sun glows more, the air in a tower's shadow less
(only where there is haze). Kept subtle on purpose: the first version washed the city out, the second turned it blue-black (it took away
more than the haze had added). Now the darkening is capped at a quarter of the picture.

Cost (docs/GRAPHICS_SETTINGS.md): traced at full size through URP's soft shadow filter they took 2.5 ms of a frame at 2560x1600; one hardware
shadow comparison a step did not help by day (the 4 million pixels' scattered shadow-map reads were the cost) but stopped them running by
moonlight; traced at half the width and height and laid over the picture, 1.2 ms by day. With the sun under the horizon the feature adds no
pass at all. Settings > GRAPHICS > LIGHT SHAFTS switches them off.

Pictures: `TrafficCapture.RenderTheLightEffects` (the real city, morning and afternoon, down a street toward the sun and from 60 m up) and
`RenderTheLightEffectsInTheSun` (a low sun behind a row of pillars), each `gfx-*-off.png` and `-on.png`.

## Wet reflections (2026-11-10)

Idea 2 of that list: docs/WET_STREETS.md.

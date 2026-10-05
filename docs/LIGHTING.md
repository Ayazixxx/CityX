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

Next candidates: tune SSAO radius/intensity against the new ambient, a screen-space reflection renderer feature for wet streets, volumetric light shafts
(a custom renderer feature), light-layer split for the sun vs window lights, emissive window glow with bloom thresholds. `LightingCapture` (Explicit, -Graphics)
renders four times of day to Logs/CityShots/light-*.png.

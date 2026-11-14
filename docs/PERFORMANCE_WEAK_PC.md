# Running on a weaker PC

Owner, 2026-11-13, after a friend played the shared build: "look into optimizing the game so it runs better. My friend played the game on
a weaker machine and it was experiencing performance issues which it shouldn't". The friend's machine is not known yet (graphics card,
screen, whether it was a low frame rate or freezes).

This page is what a read of the project found the same day; each item says how to measure it. **What has been done since is at the
end** (Done and measured, 2026-11-13).

## What a fresh install starts on

- **The HIGH preset, on every machine** (`Core/GraphicsOptions.cs`): the sun's shadows at 4096 px in 4 cascades, anti-aliasing 4x,
  ambient occlusion, light shafts and reflections on, resolution scale 100%. Nothing looks at the machine (`SystemInfo` is read only for
  the streamer's worker count and the benchmark's labels). The friend ran the owner's own settings.
- **Fullscreen at the screen's own resolution** (borderless, `fullscreenMode: 1`), frame rate uncapped (`MaxFps 0`, vSync off). The
  title's settings apply a chosen resolution only when not fullscreen, so on a 1440p or 4K laptop screen the resolution scale is the
  only control of how many pixels are drawn, and no preset touches it.
- The presets (LOW / MEDIUM / HIGH) switch only the sun's shadow map (1024 x 2, 2048 x 2, 4096 x 4), anti-aliasing (off, 2x, 4x), ambient
  occlusion, light shafts and reflections.

## What no setting turns down

(`Assets/Settings/PC_RPAsset.asset`, `PC_Renderer.asset`, `CityMassingSceneBuilder`)

- Shadows out to 260 m, soft, with a 2048 atlas for lamp shadows; HDR; the opaque-texture copy; Forward+.
- SMAA High on the main camera on top of the MSAA: "anti-aliasing off" leaves it on.
- Bloom, vignette, chromatic aberration, film grain, the haze.
- The outlines ask for depth and normals (`CityXOutline`, `requirements: 3`), so every opaque thing is drawn once more for them even
  with ambient occlusion off.
- The view: 30 chunks (720 m, 3,721 chunks loaded) and the skyline to 2.2 km.

## The measurement there is

`CityX/Builds/Bench/bench-result.txt` (RTX 3070 Laptop, 2560 x 1600, day): the graphics card takes **8.4 ms a frame with everything on
and 2.3 ms with everything off**. LOW's switches are worth about 3.3 ms of that (shafts 1.0, ambient occlusion 0.8, anti-aliasing 0.7,
shadows 0.75). Always on: post-processing 0.85, outlines 0.47, haze 0.42. Heavy rain at night costs about 3 ms more and has no switch.
The processor's side is 4.3 to 4.9 ms (main thread) and 3.6 ms (render thread), so at these defaults a weak machine is most likely held
back by its graphics card.

## What to do, biggest likely gain first

**Better starting settings**

1. **Choose the preset and the resolution scale on the first launch**, when there is no `settings.json` (`GameSettings.Load`,
   `GraphicsQuality.ApplySaved`): from the graphics memory and the processor count, or from a two-second timed look at the title.
   Measure: the benchmark with `-bench-msaa 1 -bench-scale 0.7`.
2. **Put the resolution scale in the presets** (LOW at 70%), or have fullscreen honour the chosen resolution.
3. **Cap the frame rate by default** at 60 or the screen's rate (`DisplaySettings.DefaultMaxFps`): it stops a laptop running flat out and
   throttling. A saved setting is not touched.

**Cheaper always-on costs**

4. A tier below LOW: SMAA off or Low (`UniversalAdditionalCameraData.antialiasing` in `GraphicsQuality.Apply`), grain and chromatic
   aberration off, a cheaper bloom. Ask the owner first: the bloom is part of the look.
5. Depth-only outlines on LOW (`requirements: 1`): no second pass over every opaque thing when ambient occlusion is off. Needs a render
   and a look (creases between faces at the same depth lose their line).
6. Shadow distance by preset (LOW 100 to 120 m, `GraphicsQuality.ShadowsOf`).
7. A cheaper ambient occlusion for MEDIUM (it is full size with the high blur).
8. The rain: find what its 3 ms is, and give LOW a lighter one.

**Loading and freezes**

9. A view-distance setting (`CityStreamer.RadiusChunks` 30, 20, 14, the fog pulled in with it): 3,721, 1,681 or 841 chunks. The owner
   asked for a long view, so this is a setting, not a new default. Measure: `-bench-radius 20`.
10. Fewer worker threads on few-core processors: today at least 3 generator threads and a cook thread even on a 4-thread CPU, fighting
    the game's own two. Measure: `-bench-threads`.
11. `Maximum Allowed Timestep` 0.333 lets one slow frame run 16 physics steps; 0.1 would not.

**Real engineering**

12. An IL2CPP build (the shared build is Mono): usually 1.5 to 3 times faster game code, which is the city's generation and the posing.
    Needs a full test pass.
13. Shader warm-up: there is none, so a fresh install compiles each effect the first time it is seen (the owner's own machine has them
    cached, which hides it).
14. Lighter bodies are **not** a gain here: `docs/CROWD_TEST.md` found 86k triangles against 6.5k made no difference; a body costs its
    ten draws, and a 1v1 has two bodies.

## Done and measured, 2026-11-13

- **1, the first launch picks its picture** (`HardwareDefaults`, `GameSettings.FirstLaunch`): with no settings saved yet, a graphics
  card built into the processor (Intel HD, UHD, Iris; AMD Radeon Graphics, Vega) or one under 3 GB starts on LOW at 70% resolution
  (50% on a screen bigger than 1080p); a 4 GB card on MEDIUM (fewer pixels on a bigger screen); a 6 GB card on HIGH at 1080p and
  MEDIUM above; 7 GB and up on HIGH at full resolution, as before. The owner's laptop starts as it always did. Anything saved in
  the settings is never overridden. Not measurable on the owner's machine: it rests on the benchmark's costs above.
- **10, generator threads**: a processor of four threads gets two (three before), of two threads one (`CityStreamer.SpareThreads`);
  six threads and up as before.
- **11, `Maximum Allowed Timestep`** is 0.1 s (0.333): one long frame is followed by five physics steps at the most, not sixteen.
- **4 (the cameras' SMAA off with anti-aliasing off) and 6 (the sun's shadows drawn less far on LOW): measured, no gain, not
  kept.** With LOW's shadows, 260 m against 120 m: 143 against 144 fps. With anti-aliasing off, SMAA on against off: 117 against
  116 fps (`-bench-smaa 0`). On this graphics card neither is worth changing the picture for.
- **3, a frame cap by default: not done.** A fresh install starting uncapped is an earlier decision with tests of its own
  (`MaxFpsSceneWiringPlayTests`); it is the owner's to change.
- Not started: 2, 5, 7, 8, 9, 12, 13.

Besides these, the city streaming in leaves 60% less garbage (docs/PERFORMANCE.md, 2026-11-13), which is what a slow processor's
stops while moving follow.

## To learn from the friend's machine

Its graphics card, processor, memory and screen resolution; whether it was a low frame rate, freezes, or the network stutter fixed the
same day (docs/MULTIPLAYER_MOVEMENT.md). Running the `Builds/Bench` folder there (`CityX.exe -cityxbench`, then with `-bench-effects 2`,
and with `-force-d3d11`) would say where its time goes.

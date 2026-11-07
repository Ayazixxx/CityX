# Graphics settings

Owner, 2026-11-07: "i hope this doesnt make the game too hard to run. there should be setting to turn all the extra high quality stuff
off in the settings. research how other games do that part", then "or just let me know if it is effecting performance at all, all the
graphics and lighting changes we have made that is". So: what each effect costs was measured, and the costly ones each got a switch.

## How other games do it

- **Presets plus Custom.** Nearly every PC game offers Low / Medium / High / Ultra, each a bundle of the settings below it, and shows
  Custom once one setting is changed by hand. Some add an Auto that picks a preset from the hardware on the first start
  ([Digital Trends](https://digitaltrends.com/computing/graphics-settings-principles), [Bugnet on graceful degradation](https://bugnet.io/blog/how-to-implement-graceful-degradation-mode-for-low-end-devices)).
- **Every costly effect gets its own switch**, and changes take effect at once without a restart (Assassin's Creed Shadows,
  [PC Gamer](https://pcgamer.com/hardware/assassins-creed-shadows-pc-performance-analysis-ray-tracing-upscaling-and-frame-generation-are-all-optional-but-only-two-of-them-are-worth-using-though-not-the-ones-you-might-think)).
  Accessibility guidance asks for the same: toggles for each visual effect, and settings remembered ([GamerBraves](https://gamerbraves.com/?p=112400), [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/?p=21)).
- **What usually costs most**: sun shadow quality, ambient occlusion and screen-space GI, screen-space reflections, volumetric fog and
  light; and above all the resolution, which is why a resolution scale (render fewer pixels, upscale) is the biggest single saving
  ([Digital Foundry via OpenCritic](https://opencritic.com/news/22194/these-two-battlefield-6-settings-are-killing-your-performance), [Godot docs on GI](https://docs.godotengine.org/en/4.5/tutorials/3d/global_illumination/introduction_to_global_illumination.html), [Epic's Lumen guide](https://dev.epicgames.com/documentation/en-us/unreal-engine/lumen-performance-guide-for-unreal-engine)).
- **Saying what a setting costs** is rare in the menu itself: Resident Evil 4's VRAM bar is the known example
  ([TechPowerUp](https://techpowerup.com/review/resident-evil-4-benchmark-test-performance-analysis/3.html)); most leave it to guides. A short line
  saying what the setting does and roughly what it costs is cheap to give, so CityX gives it.
- **Consoles** offer two or three modes (performance, quality) instead of a page of switches; that is for later.

## What CityX has

Settings > **GRAPHICS**, on the title screen and in the pause menu alike (`GraphicsSettingsPanel`, built by `GraphicsPanelBuilder` for
both). Each row steps with its arrows, takes effect at once and is saved (`GameSettingsData.Graphics`, a `GraphicsOptions`). A line under the
rows says what the row last pointed at does and what it costs. Escape on the title screen steps back to the settings.

| Row | Choices | What it changes |
|---|---|---|
| QUALITY PRESET | LOW, MEDIUM, HIGH (CUSTOM shown when the rows match none) | every row below but the resolution scale |
| RESOLUTION SCALE | 100%, 85%, 70%, 50% | the pipeline's render scale, upscaled with AMD FSR 1 below 100% |
| SHADOWS | LOW, MEDIUM, HIGH | the sun's shadow map: 1024 px x2 cascades, 2048 x2, the project's own (4096 x4) |
| ANTI-ALIASING | OFF, 2X, 4X | MSAA |
| AMBIENT OCCLUSION | OFF, ON | URP's SSAO renderer feature |
| LIGHT SHAFTS | OFF, ON | the `CityXLightShafts` renderer feature |

| Preset | Shadows | Anti-aliasing | Ambient occlusion | Light shafts |
|---|---|---|---|---|
| LOW | LOW | OFF | OFF | OFF |
| MEDIUM | MEDIUM | 2X | ON | OFF |
| HIGH (a new game) | HIGH | 4X | ON | ON |

A settings file from before this loads as HIGH, the game as it was. The black outlines, the haze and the bloom stay on in every preset: they
are the game's look, and cost about 0.2 ms each.

`GraphicsQuality` puts the options into force: renderer features switched with `SetActive`, the rest set on the pipeline asset. Those are
project assets, so in the editor everything is put back when play stops; tests call `GraphicsQuality.Restore()`. The saved choice is put in
after the first scene loads, except in batch mode, where tests and builders see the project as it is made.

## What it costs (measured)

`PlayerFrameBenchmark -bench-effects SECONDS` (docs/PERFORMANCE.md): the built game standing still in the city, by day and by night, each effect
timed on and then straight after off, its cost the pair's difference (a laptop card slows as it warms; against one reading at the start,
that drift reached 1.7 ms and swamped the small ones).

2026-11-07, the owner's laptop (RTX 3070 Laptop, 2560x1600, D3D12), with World of Warcraft closed. Everything on: about 106-130 fps standing
in the city (7.4-9.3 ms of graphics card time a frame), everything in the table off: about 205 fps. Runs differ by a few tenths of a ms.

| Effect (on against off) | Day | Night |
|---|---|---|
| light shafts: full size, URP's soft shadow filter | 2.5 ms | 3.0 ms (they ran by moonlight) |
| light shafts: half size (now) | 1.2 | none (no pass with the sun down) |
| anti-aliasing (MSAA 4x against off) | 0.8-1.1 | 1.2-1.3 |
| ambient occlusion | 0.8-1.1 | 1.0-1.1 |
| bloom and colour grade | 0.7-0.8 | 0.75 |
| black outlines | 0.4-0.55 | 0.6-0.8 |
| haze | 0.3-0.45 | 0.2-0.4 |
| sun shadows 4096 px x4 against 2048 px x2 | 0.35-1.0 | 0.3 or less |
| soft against hard shadows; lamp and room light shadows | none measurable | none measurable |

So all the effects together are a little under half of a frame's graphics work; the light shafts were the one costly surprise. The black
outlines, the haze and the bloom are the game's look and stay on; the rest are on the graphics page.

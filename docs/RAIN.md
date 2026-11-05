# Rain: research and what was built

Requested 2026-09-25: "research good rain mechanics in video games like mine
and implement what you learn."

The city already had rain (drops, a rain sound, a dimmer sun, wet-looking
ground, a lightning flash). What it lacked is what the research says makes
game rain feel real: rain that knows where the roofs are, water that
accumulates and dries on its own clock, and consequences for the player.

## What the research says

- Sebastien Lagarde,
  [*Water drop 2b - Dynamic rain and its effects*](https://seblagarde.wordpress.com/2013/01/03/water-drop-2b-dynamic-rain-and-its-effects/)
  (Remember Me / Dontnod). Rain is only drawn where it can reach: a top-down
  depth map of the world says what is under cover. Splashes and ripples belong
  where a drop really lands, and ripples form only where enough water has
  gathered. Wetness ramps up quickly and dries slowly.
- Lagarde,
  [*Water drop 3b - Physically based wet surfaces*](https://seblagarde.wordpress.com/2013/04/14/water-drop-3b-physically-based-wet-surfaces/).
  A wet surface is DARKER first (water fills the pores and the diffuse colour
  drops), and glossier (the surface smoothness rises); as it dries the gloss
  goes before the darkness does.
- fxguide, [*Game environments part B*](https://www.fxguide.com/fxfeatured/game-environments-partb/)
  on the same Remember Me system: layered rain, occlusion from the top-down
  map, splashes at real landing points.
- Rain mechanics people actually notice in open-world games: *Breath of the
  Wild* makes wet surfaces slippery to climb and rain puts out flames
  ([Zelda Dungeon forum thread on climbing](https://www.zeldadungeon.net/forum/threads/can-we-improve-botws-climbing.62466/),
  [Screen Rant on its weather mechanics](https://screenrant.com/zelda-botw-weather-mechanics-difficulty-op-ed/)).
  Pedestrians hurrying for a roof is this project's own choice, not something the sources prescribe.
- Character wetness in games: the Skyrim "Soaking Wet" mods
  ([mod thread](https://schaken-mods.com/forums/topic/26851-soaking-wet-character-wetness-effect/)) soak a
  character over time in the rain, darken and gloss the skin and clothes, and
  dry them again under cover, and *Don't Starve* / *Project Zomboid* treat
  wetness as a value that rises in rain and falls under shelter
  ([Don't Starve wiki](https://dontstarve.fandom.com/wiki/Wetness#Wet_Items),
  [PZwiki](https://pzwiki.net/wiki/Wet)).
- Thunder is delayed by the speed of sound, roughly 343 m/s
  ([Wikipedia: Thunder](https://en.wikipedia.org/wiki/Thunder)): a strike a
  kilometre off is heard about three seconds after it is seen, and it is duller
  and quieter the further it is.

## What was built

| Idea | Where |
|---|---|
| Rain only falls where there is open sky: a cover map | `View/Weather/RainCover.cs` |
| Drops end at the first surface (roofs, decks), splashes at real landings | `View/Weather/RainEffect.cs` (`EmitOneDrop`, `ProcessLandings`) |
| Ripples only on wet enough ground, denser in heavier rain | `Weather.RippleChance`, `RainEffect.RippleDensity` |
| Wetness rises fast, falls slowly; the gloss goes before the darkness | `Weather.StepWetness`, `SheenFactor` |
| Intensity levels: light / moderate / heavy, eased in and out | `Weather.LightRain`..`HeavyRain`, `RainEffect.SetRain` |
| Rain sound muffled and quieter under cover | `Weather.ShelteredCutoffHz`, `ShelteredVolume` |
| The outdoor rain's sound: a seamless loop (2026-10-26, owner: "take the audio from this and replace our outdoor rain noise with this. make it a clean loop") | `UI/Audio/rain_outdoor.ogg` (`RainEffect.OutdoorRainSoundPath`), cut from the owner's clip by `SourceArt/Rain/rain_loop.py` (Blender's audio library): 6.55 s, stereo, crossfaded 0.75 s at its seam, levelled to the old sound's loudness (+6 dB), Ogg Vorbis (an mp3 leaves a gap at the loop; the old `rain_noises.mp3` also faded in from near silence, a dip every minute; the title screen still plays it). `OutdoorRainSoundTests`: wired, no click or dip at the seam, as loud as the old |
| Thunder delayed by 343 m/s, duller and quieter with distance | `Weather.ThunderDelaySeconds`, `View/Weather/ThunderSynth.cs` |
| Lightning: jagged bolt to a tall building, glow, whole sky flashes | `View/Weather/LightningBolt.cs`, `RainEffect.StrikeLightning`, `DayNightCycle.Flash` |
| A storm greys the sky, fog and background TOGETHER | `SkyCycle.Weathered`, `DayNightCycle.Overcast` |
| Wet ground: less traction | `Character/Movement/PlayerLocomotion.cs` (`Weather.GroundTraction`) |
| Wet walls and ladders: more stamina, and a slow slide | `Character/Movement/WallStick.cs`, `Character/Movement/PlayerClimbing.cs` |
| Rain weakens flames (reach and damage) | `Weapons/Firearms/Flamethrower.cs` (`Weather.FlameStrength`) |
| Pedestrians hurry, and seek a roof | `Character/NPC/CivilianBrain.cs` (`IRainSense`) |
| The player soaks, darkens and dries: back to dry in a minute out of the rain, 1.5x faster standing in the sun (the sun out, nothing between the chest and it; owner 2026-10-27) | `Character/Player/PlayerWetness.cs`, `Weather.StepBodyWetness`, `World/CurrentSky.cs` (published by `DayNightCycle`) |
| Dev console | `/weather rain [light\|moderate\|heavy\|0.1-1]`, `clear`, `dry`, `lightning [metres]`, `status` |

### Design rules worth keeping

- **Pure rules live in `World/Weather.cs`.** Everything with a number in it
  (ramp times, traction, drain, speed of sound) is a pure function there with
  unit tests; the View and Character code only asks. `CurrentWeather` is the
  static bridge, so Character and Weapons read weather without referencing View.
- **Nothing shared is edited for the player's wetness.** Skin and clothes are
  wetted with per-renderer property blocks, never by changing the shared
  material (which would soak everyone, and re-save the asset in the editor).
- **The sky, fog and camera background are one colour.** The dome's horizon
  end IS the fog colour, which is what hides the streamed world's edge; a storm
  greys all three from `SkyCycle.Weathered` so they cannot drift apart.
- **Clear weather is exactly the un-weathered sky**: `Weathered(c, 0, 0) == c`,
  and the last frame of a storm re-applies the sky fresh so nothing sticks.

### Tuning it took (looked at, not just measured)

The first photographs showed the rain was there in the numbers and invisible
on screen: drops 2 cm wide with a peaked texture were sub-pixel, the sky stayed
clear blue in a storm, splashes were 15-35 cm glowing balls, and ripples were
buried by the street's own slope. Fixed by: wider, flatter-profile streaks with a
minimum on-screen size; sky/fog overcast; 5-11 cm fading splashes; ripples lifted
9 cm and made a smaller scatter. Ring/splash sizes are constants in `RainEffect`.

## Tests

- EditMode: `WeatherTests`, `CurrentWeatherTests`, `RainCoverTests`,
  `ThunderSynthTests`, `LightningBoltTests`, `SkyWeatherTests`,
  `CivilianBrainRainTests`, `RainGameplayTests` (flames, wet walls, ladders).
- PlayMode: `RainShelterPlayTests` (real physics roof: no drops under it, sound
  muffles, wetness dynamics, ripples, thunder timing, cleanup),
  `RainPlayerPlayTests` (traction, body wetness, shared materials untouched),
  `RainRealCityPlayTests` (the real scene: no rain falls under the highway deck,
  rain does beside it, rings form on soaked streets; with `-Graphics` it
  photographs to `CityX/Logs/CityShots/rain-*.png`).

### A test-harness trap

Under `-Graphics`, the PlayMode runner stops ticking the simulation after the
first `yield return new WaitForEndOfFrame()` in a test: `Time.frameCount` and
the clock freeze and every later yield returns at once. "Rain for another
second" silently becomes no time at all, and a photograph shows a soaked street
with no ripples. `RainRealCityPlayTests` therefore uses no end-of-frame waits;
`Camera.Render()` does not need one.

## The same for every player (2026-11-05)

`/weather` now goes through the server, so a change of rain or snow reaches every machine, and a joining player is told how it is. See docs/SNOW.md.

## Not built

- Rain masking footsteps / stealth noise (no stealth system to hook into yet).
- Camera lens droplets and rain on the screen.
- Real audio for thunder: it is synthesized (`ThunderSynth`), and has not been
  heard by a person yet.
- Drizzle never thunders; only intensity >= 0.6 has lightning.

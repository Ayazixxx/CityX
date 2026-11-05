# Snow

Owner, 2026-11-05: "add snowy weather mechanics, research how video games do snow weather and make it a weather option".

`/weather snow [light|moderate|heavy|0.1-1]`, `/weather clear` (it stops and slowly melts), `/weather dry` (gone at once), `/weather deepsnow` (a blizzard over streets already buried, to see it without waiting two minutes).

## What games do (researched 2026-11-05)

- [Red Dead Redemption 2](https://rockstar-files.pages.dev/reports/07_gameplay/0682_Weather_Effects.html) (a fan site, a weak source): fresh snow keeps footprints that lawmen can follow, storms cut sight to a few metres, breath shows, bodies shiver.
- [Days Gone](https://www.pushsquare.com/news/2019/04/days_gone_has_some_seriously_impressive_snow) (a light source): snow muffles footsteps and changes enemies' behaviour.
- [Batman: Arkham Origins](https://gdcvault.com/play/1020379/Deformable-Snow-Rendering-in-Batman) (GDC 2014): a camera under the ground sees what presses into the snow, blurred into a height map that slowly fills back in. Forum writers note it needs a [texture per surface](https://polycount.com/discussion/comment/2574874), so it suits small areas, not a whole world. That is why a print here is a mark on the ground.
- Whole-world snow: a surface [whitens by how much it faces up](https://www.patreon.com/posts/28227883), so it stays on top however a thing is turned, and needs [a way to keep it out from under cover](https://assetstore-fallback.unity.com/packages/vfx/shaders/fullscreen-camera-effects/global-snow-2-248191).
- [Deep snow slows characters](https://forums.unrealengine.com/t/distant-sky-winter-weather-snow-deep-trails/2836592), as an option.

What they share: a movement cost, tracks that last, and less to see and hear. Keeping snow out from under cover is the same idea as [Remember Me's rain](RAIN.md).

## What it is (every number is my first guess)

| Part | What | Where |
|---|---|---|
| Falling | 14000 flakes in one draw, in a 40 x 24 x 40 m box that wraps round the camera, so a sprint or a jetpack never leaves the snow behind. Fall 1.1-2.2 m/s, wandering half a metre, on a wind of 0.4-5 m/s. Lit by the sky, so blue and dim by night | `CityXSnowfall.shader`, `FlakeField` |
| Under cover | none falls or lies where a surface stands over it: a map of what is overhead, 72 m across at 1.5 m a cell. Indoors the walls and floors already know they are indoors. Past the map the sky is taken to be open. The map is worn back one cell (`RainCover.WearEdges`): straight from the rays, the street along every wall stayed bare in steps | `RainCover` (finer here), `CityXSnow.hlsl` |
| Lying | whitens what faces up, in patches that grow together. A blizzard buries a clear city in 2 minutes; a flurry only dusts it. It melts in 5 minutes when it stops, twice as fast in the sun, four times in the rain | `SnowRules.StepCover`, `CityXSnow.hlsl` |
| On what | walls, roofs and ground (`CityX/Facade`), roads, pavement and the city's lit props (`_CITYX_SNOW` on their materials). Not on bodies: every character is on `CityX/Lit` too, and white shoulders that vanish under a roof looked wrong in my head; try it if you want it | `CityMassingSceneBuilder` |
| Walking | full cover takes 15% off a body's speed and 35% of its grip (soaked ground takes 50%). A dusting does not slow it. Under a roof, nothing. For every body, enemies too | `PlayerLocomotion`, `SnowRules.Pace`, `SnowRules.Traction` |
| Tracks | a shoe-shaped mark where a foot lands, lasting 45 s, up to 1000 at once, with a crunch within 12 m. From what each machine sees of each body, so other players' tracks show with nothing sent | `SnowTracks`, `FootprintStepper` |
| Breath | a puff every 2.4 s from every body within 16 m | `SnowTracks` |
| Air | the grey lid of cloud, the sun at 55% in a blizzard, and fog closing to 35-120 m | `SnowEffect.LateUpdate`, `SnowRules` |
| Sound | a wind loop made in code, up with the snow, duller and quieter under cover | `SnowSounds` |
| Fire | snow weakens flames as half as much rain would; it does not soak bodies | `CurrentWeather.QuenchingOn` |

## Everyone has the same weather

Before this, `/weather` changed only the machine it was typed on, and a friend had no rain at all, since rain hung off the baked player, which is switched off on their machine. Now an order goes to the server, which keeps it and tells everyone; a joining player asks how it is, how much lies too (`NetworkedWeather`, `CurrentWeather.Order`). Alone it goes straight to the weather. Rain moves to the body a friend claims (`SnowEffect.OnBodyClaimed`). Lightning is still only local.

## Not built

- Prints are marks, not dents; nothing deforms.
- Snow on bodies; enemies following tracks (Red Dead's); snow muffling noise for stealth (no stealth system yet).
- Night snow lit by lamps as anything but the surfaces they light.
- Whether a level or the time of year brings snow by itself: it is only the console's for now.

## Tests

`SnowRulesTests` (EditMode: the words, burying, dusting, melting, walking, fog, the order's route, foot landings, the mesh, the map), `SnowPlayTests` (eases in, lies, melts; none under a roof; slower through it; prints; orders; rain follows a friend's body), `NetworkedWeatherPlayTests` (real sockets: one order changes all machines; a latecomer is told; forged orders refused). Pictures: `SnowCapture` (`Logs/CityShots/snow-*.png`).

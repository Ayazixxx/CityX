# Water: research and the recommended approach

Asked 2026-10-26 (research only, nothing built yet): "the game is going to need some basic water mechanics for the sewer biome as well as for
water attacks. i dont want there to be large bodies of water that generate but i want there to be a great flood god tier attack"; "the sewer
biome is just meant to have like water up to your ankles or knees"; "only place i can imagine swimming right now is if you get caught up in
something like the great flood in which it should be very basic hold space to go up type stuff"; "utilize the latest strategies that game devs
are using"; "i dont want it to be too detailed and make the game run poorly find a balance that will work for our games water usage (water
spells, sewer, rain)"; "obviously i still want it to look good ... dont tank performance"; "i dont know if water spells and the sewer water on
the ground will even be the same 'water mechanics' ... just do some research and let me know how we should approach this".

## The short answer

**They are not one system.** Games split "water" into three things that share a small core:

| | What it really is | Shares |
|---|---|---|
| **Water on the ground** (sewer floors, puddles, the flood) | a surface in the world at a height: you stand in it, wade, splash; it is drawn | the core below |
| **Water spells** | effects (a mesh, particles, a shader, like the fireball) that deal damage and make the target **Wet** | Wet; some spells leave a short-lived puddle, which is ground water |
| **Rain** (exists) | weather that makes the world and people wet | Wet; puddles if wanted |

**The shared core is small:** a **Wet** status on bodies (rain already darkens and glosses a soaked player, `PlayerWetness`), and, for ground
water, **one list of water areas** the game can ask "how deep is the water here" plus **one water shader** and **splash effects**.

## What games do now (the research)

1. **Gameplay asks a cheap question: "what is the water height (and flow) here?"** Crest (Unity's best-known water system) and Unreal's
   Fluid Flux both answer exactly that for their gameplay, at a point; where the water is not simulated, the answer is a plane's height.
   ([Crest](https://crest.readthedocs.io/), [Fluid Flux](https://imaginaryblend.com/2021/09/26/fluid-flux/))
2. **The look is one surface shader**: colour that deepens with depth (from the depth texture), a foam line where the water meets walls and
   legs, scrolling noise or a flow map for movement, a little refraction. The standard stylised-water recipe in Unity's URP.
   ([80.lv: stylized water in Unity](https://80.lv/articles/tutorial-making-a-stylized-water-shader-in-unity), [80.lv: Windwaker/RiME-style water in Unity](https://80.lv/articles/an-amazing-stylized-water-shader-made-in-unity))
3. **Flowing shallow water is a flow map, not a simulation.** Valve's Left 4 Dead 2 swamps and Portal 2 pools: an artist-drawn direction
   texture bends the ripples along the flow. In Left 4 Dead 2 it was a gameplay fix: flowing water showed the way, and testers took **17%
   fewer wrong turns** in the swamp maze. A sewer is a maze too. ([Vlachos, Water Flow in Portal 2, SIGGRAPH 2010](https://www.advances.realtimerendering.com/s2010/Vlachos-Waterflow(SIGGRAPH%202010%20Advanced%20RealTime%20Rendering%20Course).pdf), [Polycount: a sewer with Valve's flow maps](https://polycount.com/discussion/comment/1565435))
4. **Splashes and ripples round a character are local**: either a small ripple simulation that follows the player (Crest's dynamic waves)
   or, cheaper, rings and droplets spawned at the legs. RDR2's admired ripples are local disturbance, not a simulated lake.
   ([Game Developer: the evolution of water rendering](https://www.gamedeveloper.com/design/the-evolution-of-water-rendering-in-video-games), [Looper on RDR2](https://www.looper.com/177286/things-in-red-dead-redemption-gamers-take-for-granted/))
5. **Real flowing floods are a GPU shallow-water simulation** (a height field: Müller's method, "virtual pipes"): Fluid Flux, and
   Crimson Desert (GDC 2025) pairs one with an FFT ocean. It floods round buildings properly, but costs a GPU budget over a fixed area, and
   in multiplayer every machine would have to agree on it. ([80.lv: BlackSpace engine](https://80.lv/articles/pearl-abyss-demonstrated-crimson-desert-s-blackspace-engine-tech-advancements), [Dagenais et al., extended virtual pipes](https://profs.etsmtl.ca/epaquette/Research/Papers/Dagenais.2018.cg/))
6. **Multiplayer floods are a level that rises and falls on a schedule**: Fortnite Chapter 2 Season 3 flooded its island and let the water
   recede; Roblox's Flood Escape is a rising level used as a timer. The simulated floods (Hydrophobia, From Dust) were single-player.
   ([VGC: Fortnite's flooded map](https://videogameschronicle.com/news/fortnites-new-season-launches-with-a-flooded-battle-royale-map), [Hydrophobia](https://en.wikipedia.org/wiki/Hydrophobia_(video_game)), [GameSpot: From Dust](https://gamespot.com/articles/from-dust-first-impressions/1100-6273905/))
7. **Wading and treading are depth states, with a gap between entering and leaving** so the state does not flicker at the waterline.
   Valheim slows running from 0.4 m deep; Breath of the Wild swims on stamina and cannot dive.
   ([Endstar wiki: wading, treading, diving](https://endstar.wiki.gg/wiki/Swimming), [Catlike Coding: swimming](https://catlikecoding.com/unity/tutorials/movement/swimming/), [bugnet: waterline thrashing](https://bugnet.io/blog/bug-reporting-for-games-with-swimming-mechanics), [Zelda wiki: swimming](https://zeldawiki.wiki/wiki/Swimming))
8. **Slowing water must be predictable**: FromSoftware's swamps slow you, and players resent them most where the rule feels inconsistent.
   ([Blighttown](https://en.wikipedia.org/wiki/Blighttown), [TV Tropes: Mucking in the Mud](https://tvtropes.org/pmwiki/pmwiki.php/Main/MuckingInTheMud))
9. **Water as an element is a status, Wet, and its value is what it combines with**: Genshin Impact (Wet + Electro, Wet + Cryo freezes),
   Magicka (wet targets take double lightning, fire dries them, cold freezes them), Divinity: Original Sin 2 (lightning electrifies water).
   Rain applies it for free. ([Genshin wiki: Wet](https://genshin-impact.fandom.com/wiki/Wet_Status_to_Opponents), [Magicka statuses](https://magicka.fandom.com/wiki/Statuses), [Divinity forum](https://steamcommunity.com/app/435150/discussions/0/1630790506929643660))
10. **Water spells are effects, not water**: stylised water attacks are a mesh with a scrolling-noise shader plus particles for drops and
    spray, no fluid simulation. ([Stylized water spell VFX for Unity](https://flexunit.itch.io/water-spell-vfx), [RiME-style waterfall in Unity](https://discover.therookies.co/2019/11/05/stylized-vfx-in-unity-a-rime-inspired-waterfall/))

## The recommended approach (the balance)

Take the cheap layers (1, 2, 3, 4 in its cheap form, 6, 7, 9, 10); skip the simulated flood (5).

**A. Wet, first.** A gameplay status on any body, not only the player's look: rain wets you now, water spells and wading will. What it does
is the owner's call (below). Cheap: a number per body.

**B. Ground water (with the Sewers).**
- Water areas: a box (an area and a surface height) with an optional flow direction, in one list. The sewer's water comes with its tunnels,
  built from the world seed like the city, so every player has the same water without sending anything.
- Wading by depth, scaled to our 2.6 m body: ankle-deep barely slows you; knee-deep noticeably; waist-deep (only in a flood) slowly, with a
  weaker jump. Gaps at the boundaries so it never flickers.
- The look: one cel-shaded water shader (depth colour, a foam line at walls and legs, ripples moving along the flow, a little refraction of
  the floor under it), rings and droplets at the legs when moving, splashes on landing. No simulation.
- Flowing sewer water through the maze, Left 4 Dead 2 style, so the current hints at the way through.

**C. Water spells.** Built one at a time like the fireball and the ice shards (a mesh, a shader, particles), each applying Wet; a few can
leave a puddle that lasts a few seconds (a short-lived water area: B again).

**D. The Great Flood (god tier, later).** A water area over the city round the caster whose surface rises, holds and recedes on a timeline.
Every player's machine computes the same level from one start time, so the network sends one message, not the water. In deep water the body
floats: **hold Space to rise**, slow movement, no diving. The flow pushes bodies along the streets. One large plane and the same shader, plus
a foaming wave edge as it comes in. The roadmap cuts god-tier spells from year one, so it waits.

**Performance:** one transparent plane per water area and one shader; rings and splashes from a pool; a short list of areas checked per
body per frame; no simulation anywhere. A few water areas on screen should cost well under a millisecond. Measured when built, as always.

## Built: the groundwork (2026-10-27, overnight)

Owner: "ok implement what you learned in the water research", then "just lay the groundwork while i sleep no spells yet, just stuff we can
use later". Nothing in the city is water yet; `/water` makes some to try.

| Piece | Where |
|---|---|
| A body of water (a box in plan, a floor, a surface, a flow) and the list the game asks "how deep is it here" | `World/Water/WaterBody`, `CurrentWater` |
| Wading and floating as pure rules: slower in a straight line to 45% at the chest, a weaker jump; floating over the chest (starts at 0.64 of the body's height, stops at 0.56), sinking 0.22 heights a second, rising with Space held to float with the water at 0.74 of the body (head out), moving at 55% of a walk; the flow carries a floating body fully, a wader by depth; water at 0.4 of the body breaks a fall; water soaks a body (soaked by the waist) | `World/Water/Wading` |
| A flood's level over time (rise, hold, recede, eased), the same on every machine from one start time | `World/Water/FloodTimeline`, `View/Water/RisingWater` |
| The body in the water: speed, jump, floating, the flow, the Animator shown treading (its walk and idle, not the fall); Space held is `LocomotionInput.JumpHeld` | `Character/Movement/PlayerLocomotion.Water.cs` |
| A fall into deep water does not hurt | `PlayerInputReader` (`WaterBreaksFall` before the fall is measured) |
| Water soaks the body the rain's way (darker, glossier), far faster | `PlayerWetness` |
| The drawn water: a box (top and sides) in the one material | `View/Water/WaterSurface` |
| The look: one cel water shader -- what is under it seen through it and bent by ripples, tinted and hidden with depth, fine bright veins of light on the floor (only on what faces up), the sky in it at grazing angles, one hard glint of the sun or moon (none in shadow), a moving foam line where something breaks the surface, ripples running with the flow; lit by the light on it, so dark at night | `View/Shaders/CityXWater.shader`, `View/Materials/Water.mat` (reset to the shader's values every build: tune the shader) |
| Splashes for every humanoid body in water (this player, other players' copies, the city's people): a ring a stride from alternate feet, drops kicked up running past the ankles, a slow ring standing or floating still, a crown of drops and a wide ring coming down into it. Seen by everyone from where the bodies are: nothing is sent | `View/Water/WaterSplashes` (on the city scene's "Water" object, which also holds the materials), `View/Water/RippleRings` (shared with the rain's rings) |
| Try it | `/water [depth]` (a 16 m pool round you, 0.7 m by default), `/water flood [height]` (120 m across, rising 4 m over 15 s, holding 25 s, receding over 15 s, flowing 1 m/s the way you face), `/water off` -- this machine only, like `/weather` |

**Cost, measured** (`WaterCapture`, 1280x720, a pool covering the lower half of the view): a frame 6.60 ms dry, 6.72 ms with the water,
**+0.12 ms**. The splashes look for bodies once a second and do nothing in a dry world.

Tests: `WaterRulesTests` (depth, overlap, wading, floating's two depths and its climb, the flow, the flood's level), `WaterPlayTests` (the
real player body: knee-deep slows a walk by the rule; six metres floats it, it sinks let go and rises with Space to float with its head out;
a flow carries it; standing waist-deep soaks it; rings and drops answer a run through water), `WaterCapture`
(`Logs/CityShots/water-*.png`: wading by day, low along the water, by night, floating in a flood, a flooded street; and the cost).

Not built (the owner's calls below, then later): Wet doing anything in a fight, dirty water, the sewers themselves, water spells, the
Great Flood as a spell (its timeline and its rising water are ready; it needs a caster, the network message with its start time, and a
wave edge), a breath bar, swimming under water, a camera under water.

## Owner's decisions before building

1. **What does Wet do?** Suggested (from Genshin and Magicka): lightning hurts a wet target more (Self Smite gets better in the rain), fire
   dries you and does less to a wet target, cold slows or freezes a wet target. Or Wet stays only a look for now.
2. **Sewer water:** only slows, or is it also dirty (a little poison over time, or a smell/sickness status)?
3. **Does flowing sewer water push you along?** (Gentle, or only the flood.)
4. **The flood:** does it hurt, carry people along the streets, and can you drown (a breath bar), or is it only slow, dangerous ground?
5. **When:** the roadmap puts the Sewers at March 2027. Wet and the wading code can come any time; the shader and splashes are best built
   with the first sewer tunnel to look at.

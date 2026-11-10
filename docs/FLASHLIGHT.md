# Flashlight

## 2026-10-29: built to the owner's ask

> "oh and then make a basic flashlight item. research video game flashlights and use what you learn to make it"

`/equip flashlight` (or `light`). F or a left click switches it on and off.

## What games do (the research)

- **A spot light shaped by a cookie** (a gobo): a texture projected from the light gives the beam its shape -- a bright hot spot in a softer
  wide spill, and the rings and imperfections a real reflector throws. A plain spot reads as a stage light; the cookie is what makes it a
  torch (Valve's lighting docs list the flashlight among their preset cookies; Unity's manual suggests "dirty" cookies for horror; commercial
  cookie packs are rendered from real lens and reflector shapes). Real torches: a bright hot spot with a soft spill round it.
  **Taken out, 2026-11-09**: the reflector's ring and ripples, and the hot spot's edge. Owner: "it keeps reflecting as circles instead of
  just an area of light like most light sources in video games do". Each of them drew a circle on every wall and floor the beam fell on;
  the cookie is now one area of light that only ever fades going out.
- **Shadows**: a flashlight that casts shadows is most of the atmosphere (Doom 3, F.E.A.R.): walls and bodies cut the beam and throw long
  moving shadows. Start the shadows a little out from the lens so the hand holding it never shades it.
- **A beam in the air**: the volumetric shaft Alan Wake is known for; cheaply, a soft additive shape along the beam that faces the camera.
- **The light lags the aim a little**: held torches in third-person games swing onto where you look a moment late, which reads as a hand
  holding it rather than a light glued to the camera.
- **A glare on the lens** when the beam points at you: how other people's torches read from in front.
- **Warm or cold**: filament warm white in Alan Wake and Resident Evil, LED blue-white in modern settings.
- **Battery**: a design lever in horror games (F.E.A.R. treats light as a limited resource; others drain and recharge it). Not wanted for a
  basic torch: left for later.

Sources: Valve Developer Community, light cookies; Unity Manual, Cookies; Candlepower Forums, "Flashlights in games"; the F.E.A.R.
flashlight retrospective (stagingpoint.com); "Creating a Flashlight for Z4CH" (itch.io devlog); Volumetric Light Beam (Unity asset).

## What it is

- **The torch** (`Flashlight.prefab`, `FlashlightBuilder`): black anodised aluminium turned in code -- a ribbed tail cap, a straight grip,
  a head flaring out to a bezel round a pale domed lens -- a third of a metre long (the city is 1.3 times life size).
- **Held** in the right hand, its head out past the thumb (as a staff is held). Off, it is carried low at the side; lit, the arm holds it
  out before the shoulder and points it where you look, catching up a little late (`PlayerLocomotion.Torch`, TwoBoneIK after the Animator).
- **The beam** (`FlashlightItem`, numbers in `FlashlightRules`, drawn with `FlashlightLook`): a spot light, warm white (1, 0.92, 0.78), 90 bright,
  45 m, 55 degrees across with a 16 degree hot spot, soft shadows from 25 cm out, shaped by a cookie made in code (`FlashlightRules.CookieAt`:
  the hot spot full in its middle and melting into the spill, the spill falling smoothly to nothing at the edge, no rings); a faint
  shaft of light in the air along the first 10 m, as wide as the hot spot's cone and a little more (three soft wedges crossed about the beam,
  fading along it and to their sides, so it reads as a volume from the side; seen best in the dark; it fades out to an eye that looks
  along the beam from either end, `FlashlightRules.ShaftShown`: looked along, its wedges show as flat slabs, and the game's own camera
  stands behind the hand, so the one who carries a torch does not see its shaft and everybody beside them does); a glow on the lens that
  grows and brightens as the beam points at the camera.
- **A click** going on and off (`SwitchClick`, the room lights' switch sound, a little lower going off).

## 2026-11-09: under the ground

> "the sewers need to be darker more pitch black like night time in minecraft with barely any visibility and the flashlight/ other light
> sources need to light up the darkness"

In a tunnel the torch as it was lit a dim ring of brick some metres ahead and nothing else. Three things were wrong (docs/SEWERS.md has
the dark itself). The first is mended everywhere; the other two are only under the ground, and on the street and in a room the torch
falls off and is carried as it was.

- **The cookie was read far darker than it was painted.** Its texture is an sRGB one and a light decodes what it reads from it, so a
  spill painted a half lit as a fifth. `FlashlightRules.CookieTexelAt` now holds the beam encoded and the light gets `CookieAt` itself:
  the spill round the hot spot is as wide and as bright as it was meant, on the street too. It is the spill that lights the walls and
  the floor of a tunnel walked along (`SpillShare` 0.55); the hot spot lands far down it.
- **The beam does not fall off with the distance** (`CityLightLayers.Beam`, `CityXThrowBeam` in `CityXToonLighting.hlsl`). A tunnel is
  long and its walls lie almost along the beam. By the square of the distance a beam bright enough to show the far end of one burns the
  wall beside the hand white, and one that spares the wall is gone ten metres on. So on a surface under the ground (the tunnels' own,
  marked `SurfaceLook.PackUnderground`; bodies and props go by the eye, `_CityXUnderground`) a light on the beam layer lights what it
  falls on, near or far, as a bare light of its strength would 4 m off (`CITYX_BEAM_MATCH_METRES`), and only its range ends it (URP
  fades every light out over the last of its range: nine tenths left at 22 m, a quarter at 38 m of the 45). Under the sky it falls off
  as any light does: thrown so down a street it floods every house front thirty metres off.
- **A glow round it** (`FlashlightItem.Glow`): the beam only lights what is in front of it, so the walls beside whoever carries it, and
  the carrier, stayed black. A dim point light at the lens, 8 m (`GlowReach`), 9 % of the beam (`GlowShare`), on the beam layer too, no
  shadows: it stands for the beam's light come back off the walls round it, which has no one place to cast from. It is as strong as the
  torch's own place is out of the sky's light (`FlashlightRules.GlowIntensity` of `CurrentSewers.DarknessAt`): whole in a tunnel, coming
  on down a manhole's neck, nothing on the street, where there are no walls to throw the beam back and it was a pool of light at the
  carrier's feet, by day too. Known limit: unshadowed, it lights a little of what is behind a pillar or a body near the torch.

A light's marks only reach the shaders if their rendering layers have names in Tags and Layers (`Carried Beams` is the beam's): URP
strips a mark on a layer the project does not name, and says nothing. `LightMarksPlayTests` render the marks to prove they arrive.

## Other players (docs/MULTIPLAYER_STATE.md)

Their copy of you holds the same torch (`ICosmeticMountable`). `FlashlightSync` (an `IReplicatedAbility` on the body, "flashlight") tells
them when it goes on (with where it points) and off, and while it is on, where it points ten times a second when that moved 20 cm; their
copy's torch switches and points by that word (`ITorch.ShowLit`), the arm with it.

## Tests

`FlashlightRulesTests` (the cookie's shape, that it only fades going out, and that the light gets it as it is meant; the shaft shown
from the side and not looked along; the glow dim, short, and only as strong as the place is dark), `FlashlightPlayTests` (click on and
off, pointed where you look and a little late, a shadowed spot with its cookie, a glow in a tunnel and none under the sky, the sync and
a copy lit and pointed by it, glowing too), `LightMarksPlayTests` (rendered: under the ground a carried beam is as bright on a far wall
as on a near one, on a body and on a tunnel's wall; under the sky it falls off as any light does); pictures `FlashlightCapture`
(`flashlight-*.png`) and `SewerCapture` (`sewer-7-flashlight-on.png` and `-off.png`: a torch in a hand in a tunnel).

## Not yet

- A battery, a switch key that keeps the torch in the off hand with a gun in the other (Alan Wake, Resident Evil), lights on guns.

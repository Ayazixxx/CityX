# Flashlight

## 2026-10-29: built to the owner's ask

> "oh and then make a basic flashlight item. research video game flashlights and use what you learn to make it"

`/equip flashlight` (or `light`). F or a left click switches it on and off.

## What games do (the research)

- **A spot light shaped by a cookie** (a gobo): a texture projected from the light gives the beam its shape -- a bright hot spot in a softer
  wide spill, and the rings and imperfections a real reflector throws. A plain spot reads as a stage light; the cookie is what makes it a
  torch (Valve's lighting docs list the flashlight among their preset cookies; Unity's manual suggests "dirty" cookies for horror; commercial
  cookie packs are rendered from real lens and reflector shapes). Real torches: a bright hot spot with a soft spill round it.
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
  the full hot spot, a faint reflector's ring just past it, the spill falling smoothly to nothing at the edge, faint ripples); a faint
  shaft of light in the air along the first 10 m, as wide as the hot spot's cone and a little more (three soft wedges crossed about the beam,
  fading along it and to their sides, so it reads as a volume from every side; seen best in the dark; it fades out as the beam turns toward
  the camera, where looked at from inside it its wedges would show as flat slabs); a glow on the lens that grows and brightens as the beam
  points at the camera.
- **A click** going on and off (`SwitchClick`, the room lights' switch sound, a little lower going off).

## Other players (docs/MULTIPLAYER_STATE.md)

Their copy of you holds the same torch (`ICosmeticMountable`). `FlashlightSync` (an `IReplicatedAbility` on the body, "flashlight") tells
them when it goes on (with where it points) and off, and while it is on, where it points ten times a second when that moved 20 cm; their
copy's torch switches and points by that word (`ITorch.ShowLit`), the arm with it.

## Tests

`FlashlightRulesTests` (the cookie's shape), `FlashlightPlayTests` (click on and off, pointed where you look and a little late, a shadowed
spot with its cookie, the sync and a copy lit and pointed by it); pictures `FlashlightCapture` (`flashlight-*.png`).

## Not yet

- A battery, a switch key that keeps the torch in the off hand with a gun in the other (Alan Wake, Resident Evil), lights on guns.

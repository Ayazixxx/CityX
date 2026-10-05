# Climbing: on purpose, on real walls

## What was wrong

Wall climbing (`PlayerClimbing`) grabbed the instant Shift and W were held with ANY surface within a metre at
chest height, and the airborne sticky-feet ability (`WallStick`) did the same for Shift in the air. Shift+W is how
the player runs. So it fired on the wall at the top of a staircase, the frame beside a window you were jumping
through, and the jamb of a doorway. The grab also left the body hanging wherever it was when it grabbed (up to a
metre off the wall), facing whichever way it happened to be facing.

## What the research said

- **Assassin's Creed** starts a climb only while a dedicated parkour-up button is HELD. Ubisoft's own design
  note for Shadows: holding it "like a gas pedal" is not the way to play; crouch/stance are separate buttons so
  the wrong action cannot fire; only small vaults are automatic while sprinting.
- **Unity ledge/wall systems** validate in stages and reject at every stage: a horizontal check with several
  rays plus an overlap check, then vertical checks, then the surface angle (too steep or too shallow is
  discarded), so odd geometry never passes. (WeaverDev's ledge-grab write-up is the clearest.)

## What it does now

1. **Intent** (`Character/GrabIntent.cs`). Space held for 0.2 s is a grab; a tap is a jump. Needs W held toward
   the wall. One hold grabs once (release to grab again), and a 0.6 s cooldown after any grab ends, so a hop-off is
   not undone by the next frame. Not available with a jetpack or parachute on the back (Space means thrust or
   canopy there).
2. **A real wall** (`Character/WallProbe.cs`). Nine rays (three across, at knee, chest and head height) must ALL
   hit, on one flat face (hit distances within 0.3 m, normals within 20 degrees), near-vertical, within 45 degrees
   of where the player is facing, and not a person, a door or flying glass. A doorway, a window opening, a jamb, a
   stair, a low fence and an outside corner each have a gap or a second face in that grid. On a real streamed
   building: 69 of 71 plain-wall spots accepted, all 3 doorway spots refused.
3. **The grab itself**: the body is drawn onto the wall face (it used to hang off it) and turns to look at it.

Sticky feet (`WallStick`) uses the same intent and the same probe.

## Controls now

| | |
|---|---|
| Jump | tap Space |
| Climb a wall | hold Space + W facing it (a tall, wide wall) |
| Climb up / down | W / S |
| Hop off | tap Space again (after letting go) |
| Sticky feet | hold Space in the air + Shift, facing a wall |

## Tests

`WallGrabIntentTests` (fixtures: doorway, jamb, window, stairs, fence, corner, door, angle, intent),
`RealCityWallGrabPlayTests` (the real facade and doorway), `RealInputClimbingPlayTests` (real keyboard: Shift+W
never climbs, a tap jumps, hold-Space-and-W climbs).

## Update: ledges and the mantle (the "stuck in the wall" bug)

**What was wrong.** The top of a climb was "the wall stopped": one ray, 1.8 m above the hands, stopped hitting the wall. A window opening, recess or balcony at that
height looked like the top, so the body was moved up 2.2 m and 0.6 m INTO the wall (`-wallNormal * MantleForward` points into it) and stuck. At a real
edge the clip played, then the body teleported to a fixed offset.

**Research** (Unity ledge/parkour write-ups, Assassin's Creed's annotated ledges, the Dynamic Parkour System's feature list, Unity's MatchTarget):
validate a ledge in stages and refuse every look-alike; a mantle authored for one height looks wrong on any other, so use **motion warping** (drive
the body along a path computed from the real ledge) and put the **hands on the ledge** (IK).

**What it does now.**
- `LedgeProbe` finds a real ledge: the wall face at hand height, then a grid of top probes behind it (2 depths x 3 across) that must ALL find an upward-facing
  surface agreeing on height to 12 cm (flat, wide, deep: not a pole, a slope, a sill), the face just under it must be the same face (not a set-back shelf),
  and a capsule at the standing spot must be free (room to stand). Only then is it a ledge.
- Climbing up mantles only at a validated ledge within reach of the hands. With no ledge and no wall left at head height the body holds its grip rather than
  climbing into thin air. A **stuck guard** lets go if it is asked to climb but is not rising for 0.8 s.
- The **mantle** (`PlayerClimbing.MantlePath`, 1.2 s, the controller off while it runs): 60% pulls straight up the wall to the ledge's height (never toward the
  wall first), 40% steps over the lip onto the validated standing spot with a small lift; the clip is fitted to that length; then the controller is back on, the
  feet are snapped to the real surface and the animator is handed to Locomotion.
- **Hands on the ledge** (`PlayerLocomotion.SetLedgeHands`, two-bone IK after the Animator): both palms go on the lip as it comes within reach and stay through the
  pull-up.
- **Control drops** off the wall (no push); Space still hops off outward. (The Dynamic Parkour System uses C for drop and slide: moved to Control here. Slide
  is not built.)

Tests: `LedgeProbeTests` (a plain top, a tall wall, a window gap, a pole, a slope, a ceiling, a set-back shelf, the path never going toward the wall before it is up),
`LedgeMantlePlayTests` (real rig: mantles onto a real edge and stands in free space; a gap in the wall is never an edge and never leaves the body in it; Control
drops). `LedgeMantleCapture` (Explicit, -Graphics) renders it to `Logs/CityShots/mantle-*.png`.
Not done: shimmy along a ledge, ledge-to-ledge jumps, corner turns, drainpipes, a dedicated hang clip (the existing climb clip is reused), and the climbing
state is still not replicated to other players.


## Update: the vault

**Use it:** run (Shift + W) at a waist-to-chest-high obstacle with a flat, thin top (a fence, a low wall, a car, a stack of crates) and the player goes over it and
lands on the far side. Running past something or walking into it does nothing; holding Space at a wall is still the climb.

- `VaultProbe` validates in stages, like `LedgeProbe`: a face at waist height that the player is facing (not a graze); a flat top just behind it, inside the band
  0.30 to 0.68 of standing height (lower is a step, higher is a climb); the top must END within 1.1 m (a building is not vaulted); ground within 1.6 m below
  past the far edge with a standing capsule's room, and open space over the far edge (no rail just beyond).
- `PlayerVaulting` drives the body along `Path` (motion warping, 0.7 s, controller off while it runs): up to 12 cm over the top first, travel across, drop to the
  landing; the body leans 35 degrees into it about the hips and the hands reach for the top. Needs 3 m/s of running speed.
- Wired in `PlayerInputReader` after the Space-hold grab check, so a grab wins. There is no vault clip in the project, so the legs hang while the body leans:
  an authored vault clip would improve the look; the path and hands would not change.
- Multiplayer: the body's position is what other players see (owner-authoritative movement), so they see it go over; the hand IK and the lean are local.
- Tests: `VaultProbeTests` (a fence, a kerb, a head-high wall, a building-sized block, a boxed-in gap, facing away, a pole, feet always above the obstacle while over it),
  `VaultPlayTests` (real rig: over a fence to the far side with the controller on; no vault when walking, not moving forward or airborne), `VaultCapture` (Explicit, -Graphics).
- Not done: slide (the Dynamic Parkour System's C; would be Control), vault-on-jump-tap, kong/speed vault variants, a clip.

## Fix: floors of inner storeys read as ledges

Climbing a real facade mantled at the floor of an inner storey and carried the body through the outer wall (reproduced on the real city before the fix:
began the mantle at y=22.6 on a building 50 m tall). The top probes behind the wall face start inside the wall's thickness, so they found the interior floor slab.
`LedgeProbe` now also requires the way over the lip to be OPEN: level rays from just outside the lip to the standing spot, at four heights up the body, must
hit nothing (a real ledge has nothing above its lip; an inner floor has the wall above it). Tests: `LedgeProbeTests.TheFloorOfAStoreyInside...`,
`RealCityClimbTopOutPlayTests` (six real facade climbs: never a mantle below the roof, never inside geometry). A climb up a facade now ends at the first
window or recess instead (it lets go), it does not top out through the wall.

Also: running or diving into an intact window no longer breaks it (windows break only from a punch, a bullet, a weapon, a thrown bottle; a cracked pane gives way to a knock).

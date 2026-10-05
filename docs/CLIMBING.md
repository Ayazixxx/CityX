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

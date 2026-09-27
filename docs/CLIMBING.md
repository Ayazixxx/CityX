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

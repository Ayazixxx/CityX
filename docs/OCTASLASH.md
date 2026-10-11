# Octaslash (key 6, sword in hand): Sephiroth's held up special

Asked for (2026-10-11), with a GIF of Sephiroth's Octaslash from Smash Ultimate:

- a hotbar skill;
- a 3 s wind-up, then an eight-slash dash in whatever direction you are looking;
- a click sends it early;
- you must be holding a sword;
- an animation like the GIF.

The owner also said: "the percentage thing is just how smash bros does its damage", so only the shape of Smash's damage carries over.

## What Sephiroth's does (SmashWiki "Sephiroth (SSBU)/Up special")

- **Cuts.** Eight of them, on frames 36, 38, 40, 42, 44, 46, 48 and 50: seven at 2.3%, then a final one at 7%, so the last is about 3x the others.
- **Movement.** The dash carries opponents along, then the final cut launches them. It goes "slightly farther than Blade Dash".
- **Walls and endlag.** Remaining cuts happen in place if the dash is stopped. It has heavy endlag (106 frames in all).
- **What the page leaves out.** No travel distance or charge window is given.

## What the game does

All the numbers are in `Character/OctaslashMove.cs`.

| | |
|---|---|
| Press 6 | **Wind-up**, up to 3 s: the body is rooted (and hangs in the air if airborne), coiled forward, the blade drawn back and low behind |
| Left click during it | the dash goes at once (the click is not also a swing) |
| Dash | where the camera looks (up, down, anywhere), about 14 m in 16 frames (52 m/s), keys and gravity off (`PlayerLocomotion.ForceVelocity`), the body lunging along a level dash (`OctaslashMove.LeanUp`), upright straight up or down |
| Cuts | on Smash's frames (2 frames apart), all round the body to 3.2 m: seven at 0.35x a sword swing, the last at 1.05x, which throws whatever can be launched on along the dash and up |
| People | the dash goes through bodies and cuts them; only the world stops it (`PassThroughBodiesAhead`); stopped by a wall, the remaining cuts land where it stopped |
| After | 0.5 s held still, then 2 s before another |

- **The pose.** It reuses the sword spin's one-handed arm pose (`PlayerLocomotion.SetSwordSpinPose`):
  - wind-up: blade back 110° and down 35°;
  - dash: arm straight out.
- **The lean.** It goes on the visual root (the same transform the jetpack tilts).
- **The slashes.** Each of the eight cuts draws a crescent of white light round the body (`OctaslashSlash`, with the sword spin's streak material), each turned a golden angle about the dash from the last; the final one is bigger and level.
- **Damage.** Every cut goes through `MeleeAttacker.LandSwordCut`, so Strength and Bloodrage count, XP is given, blood is spilled, and other players are hit through their copies.

## Multiplayer

`OctaslashAbility` implements `IReplicatedAbility` with the id `octaslash`. It sends three events: charging, dashing (with the direction) and ended. A copy shows the pose, lean and slashes on the same timeline and hurts nothing; the body's movement arrives with its transform. A copy whose dash event is lost ends its wind-up by itself after 4 s. Both kinds of body get it through `PlayerAbilityWiring`.

## Tests

- `OctaslashMoveTests` (EditMode) covers the frames, the damage ratio, the timing and the lean.
- `OctaslashPlayTests` (real keys, real city) covers:
  - the wind-up holds still for 3 s, then the dash goes about 14 m where the camera looks, with all eight cuts, and hits a body it passes through;
  - a click sends it early;
  - with no sword, nothing happens;
  - another player's copy shows it and hurts nothing.
- The Explicit `OctaslashCapture` renders `Logs/CityShots/octaslash-windup.png` and `-dash.png`.

## Not done

- **Sound:** none yet.
- **Skill tree:** none yet; it is a test key.
- **Dragging:** Smash drags opponents along with the dash; here they are cut where they stand and launched at the end.
- **Wings:** there are no Sephiroth wings.

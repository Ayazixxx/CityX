# Movement: facing, turning, side-steps

Owner, 2026-11-01: "i just want dynamic movement that feels good improve upon our movement the best you can", "research how video games get
good looking movement", then on GTA-style facing: "i want to try out both lets do gta style first and if i dont like it il have you revert it
back".

## What good third-person movement is made of (research, 2026-11-01)

- **Motion matching** (For Honor, The Last of Us Part II): every few frames the game picks, from a large motion-capture library, the moment that
  best fits where the body is and where the stick points. Looks the most natural; needs a big library (Ubisoft captured weekly). An MIT Unity 6
  package exists (JLPM22/MotionMatching).
- **Start, loop, stop and pivot clips**: separate short animations for setting off, stopping and a plant-and-turn reversal. Where the weight comes from.
- **Orientation warping** (Unreal's Lyra): facing the aim while moving at an angle plays one walk and turns the hips and legs toward the travel,
  the chest staying on the aim, instead of blending a forward walk with a side-step (which tangles the legs).
- **Stride warping**: the steps stretched to the real speed (ours: PacedGait and the foot locks).
- **Physics reactions** (GTA's and Red Dead's Euphoria): ours are the ragdolls and the hit reactions.

## GTA-style facing (built 2026-11-01)

Out of an aim (right-click), a gun's fire, a cast (spell, staff, wand) or a lock-on (Tab), the body **turns toward where the keys point and goes
where it faces** (`MovementTurn`, `PlayerLocomotion.FaceMovement`). Before, it always faced the camera while moving, so A, D and S side-stepped and
back-stepped, and W+A blended a forward walk with a side-step.

| | |
|---|---|
| Turn rate | 900 deg/s from a standstill (an about-face in a fifth of a second), 600 at a walk, 300 at a sprint: a sprint curves round (a right angle in 0.3 s, an arc about 1.4 m across) |
| Sharp turns | a turn more than 60 deg off where the body faces brakes it, down to 35% of its speed at 160 deg and more: a reversal plants and turns instead of skating round |
| Sprint | any way the keys point (the body faces it); still forward only while facing the camera |
| Facing the camera | aiming, firing, casting, a staff or wand pointed, locked on: as before, side-steps and back-steps |
| Compare | `/movement classic` brings back the old camera-facing movement, `/movement gta` this one |

Other players see it as it is: the facing travels with the body's rotation and the walk with the animator's parameters.

## Side-steps and diagonals (built 2026-11-01)

- **The gun aimed and the two-handed sword side-step as the plain walk does.** Their own side-steps (the Mixamo pistol and great-sword strafes)
  squatted bow-legged (`StrafeCapture` rows 4 and 5, "you looks like your squatting"). The arms come from the aim and the sword carry, which
  sit on top, so only the legs changed. The one-handed sword's baked carry keeps its own (baked from the same plain side-steps).
- **Orientation warping** (`OrientationWarp`, `PlayerLocomotion.Warp.cs`): facing the camera and moving at an angle, the Animator is shown only
  the nearest of the four straight ways (forward, left, right, back), and after it the hips and legs turn the rest of the way toward the travel
  (60 deg at most) while the chest turns back onto the facing. A shown way holds until the travel is 55 deg off it, so a diagonal does not
  flicker between the walk and the side-step. Worked out from the shown way (the synced Animator parameters) and the body's measured motion,
  so other players' copies warp the same. Before, a diagonal blended the walk with the side-step and the legs tangled.

## The pistol: two hands only while aiming (2026-11-01)

Owner: "i only want him only the pistol with two hands like that when holding right click on scoping in, have normal walk leave the arm normal
just carrying it with 1 hand". The gun's walk (`GunLocomotion`, the Mixamo pistol walk, run and back-step with both hands on the gun) is entered
only with Aiming held, and left when the aim is let go; out of an aim the plain walk plays and the pistol rides in the right hand at the side
(`StrafeCapture`'s last row). The owner's Mixamo Pistol Handgun Locomotion Pack turned out to hold the same clips the game already had (the arm
poses match frame for frame), so nothing was taken from it; its skinless copies also imported sunk into the ground.

## Next

- The owner's Mixamo Locomotion Pack (2026-11-01: idle, walk, run, jump, walking and running side-steps, turns on the spot and 90 deg turns) for
  the side-steps and for turning on the spot.
- Weight: starts and stops (the pack has none; Mixamo has more).

## Tests

`MovementTurnTests` (the rates and the braking), `PlayerLocomotionPlayTests` (pushing left turns and walks left; a sprint curves round and runs
full speed any way; a reversal brakes; locked on still side-steps and backs up; classic mode side-steps). Render: `StrafeCapture` (every
side-step and the diagonal, from the front).

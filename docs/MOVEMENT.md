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

## Free movement, as Skyrim's (2026-11-04)

Owner, after playing GTA-style facing: "get rid of the gta style movement i dont think it works well i like our more free movement like minecraft", "the free movement like minecraft makes it feel a bit cheap and roblox like", "skyrims feels like a good example we should try to imitate", "i think its pretty similar to what we had before", "go ahead and make all of the improvements".

A player's body **faces the camera as it moves**; the keys walk it forward, sideways and back, whatever is in hand. Standing still it does not follow the camera, and a blow turns it to the camera (`SetFacingOverride`).

| | |
|---|---|
| Backward | 75% of a walk (`FreeMovement.BackwardSpeedShare`), less of a cut on a diagonal back. Before, the same speed as forward. The Animator is shown the full backward walk and the foot locks take up the difference: shown the real speed, the blend took in the idle and the feet slid at 2.6 m/s (`FootIkPlayTests`), and much slower than this the locks cannot hold. Skyrim's slower backward walk is from memory, not measured |
| Sideways | a full walk, with the hips and legs turned on a diagonal (below) |
| Sprint | forward only |
| Weight | light: a walk in about 0.2 s, a stop in about 0.15 s (`MovementWeight` at `FreeMovement.PlayerFootScale`). Before, a walk in a fifteenth of a second |
| Quick still | aiming, firing, casting, a lock-on, a blow, a dash, flight, the air |

Both numbers are a first go, for the owner to feel. Other players see it as it is: the facing travels with the body's rotation and the walk with the animator's parameters.

**A brain's body is different on purpose** (no `PlayerInputReader`): it turns toward where it steers and goes where it faces (`MovementTurn`: 900 deg/s at a stand, 600 at a walk, 300 at a sprint; a turn over 60 deg off brakes it, to 35% at 160 deg), with `MovementWeight`'s own numbers. Enemies have steered that way since 2026-11-01 and their brains are tuned to it. An eased turn left a Shoota running in a circle round his cover for ever, so it stays a steady rate.

**GTA-style facing for the player** (2026-11-01 to 11-04) is gone: the eased turn, `/movement`, and a sprint in any direction. It is in the history at `ce2b643c` if it is ever wanted.

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

## Weight (built 2026-11-02)

Owner: "sure do movement weight". On foot, speed builds and falls over a few steps (`MovementWeight`): 14 m/s per second speeding up (a walk in 0.3 s, a sprint from a stand in 0.55 s), 20 braking (a sprint stops in 0.38 s, about 1.4 m on); a player's are 1.3 times those since 2026-11-04. Before, every change took 60 m/s per second: a sprint in an eighth of a second, and a stop as sudden. Only speed along the way the body goes is slow: sideways drift is gripped away at 40, so a turn holds its line and does not slide as on ice. Wet ground scales all three down (`WetTraction`, as before), a giant's up, so it takes the same time.

Left quick, at 60, on purpose: aiming, firing, casting, a lock-on, a blow (a fight needs to answer the keys at once), flight (its own inertia) and the air. `BodyLean` needed no change: it follows the measured motion, so the push off and the brace on a stop come by themselves. There are no start or stop clips yet: the strides simply grow and settle. Other players see it as it is, the movement travels with the body. The numbers are a first go, for the owner to feel.

## Next

- The owner's Mixamo Locomotion Pack (2026-11-01: idle, walk, run, jump, walking and running side-steps, turns on the spot and 90 deg turns) for
  the side-steps and for turning on the spot.
- Start and stop clips (the pack has none; Mixamo has more).

## Tests

`FreeMovementTests` and `FreeMovementPlayTests` (the player: S backs up with anything in hand and slower, side-steps, sprint forward only, the light weight), `MovementTurnTests` (a brain's rates and braking), `MovementWeightTests` (the build-up, the stop, the grip), `PlayerLocomotionPlayTests` (a brain's body: steering left turns and walks left; a sprint curves round and runs
full speed any way; a reversal brakes; locked on still side-steps and backs up). Renders: `MovementWeightCapture` (a start, a stop, a reversal), `StrafeCapture` (every
side-step and the diagonal, from the front).

## The body against walls (2026-11-10)

Owner: "make it so body parts cant clip through walls and other objects, make the hitbox the exact size of the body and stop at contact
with the wall" (screenshots of an arm and a shoulder in the bricks, walking along a building). The moving capsule is 0.5 m round: the trunk,
head and legs are inside it, the arms reach past it, further as they swing.

- Once the body is posed each frame (`PlayerLocomotion.WallClearance`, after `PoseAfterTheAnimator`), the arms' and the head's own ragdoll
  colliders, kinematic on the bones, are asked how far they are into the world (`Ragdoll.WallContacts`: static colliders only, no bodies,
  nothing with a rigid body, floors and ceilings left out), and the body is moved through its capsule as far as takes them out.
- Only walls beside the body push it, and only sideways (`BesideTheBody`): pushed head-on too, the body could not run into a window opening
  narrower than its arms (1.16 m against 1.25 m), and an arm on the corner of an opening pushed it back out.
- A wall walked along is remembered (`WallStandoff`): the body is kept as far off as its widest swing needed and let back at 2 cm a second;
  kept off by each frame's arms alone it went in and out 13 cm a stride. Forgotten 1.2 s after the last touch, or once 0.3 m clear.
- A remembered wall is a plane with no end, so it is also forgotten once it is ahead of the body or behind it (only a wall beside the
  body is held, as only a contact beside it is taken) and once nothing of the world is left within the arms' reach on its side (an
  overlap at the arms' height, `WallStillBeside`). Without that (owner, 2026-11-12: "walking up this staircase is super glitchy and barely
  works"), a body sprinting up a roof stair beside the rail between its lanes turned at the landing to cross past the rail's end, was held
  there by the rail it had left, then pushed back down the flight at 15 m/s; it never reached the roof
  (`RoofStairWalkPlayTests`: now walked, sprinted and sidestepped up with no stall).
- Two walls pushing opposite ways (a gap narrower than the arms) push it neither way (`WallClearance.Combined`).
- The push is not the body's own going: the lean does not count it (counted, it swung the shoulders out further, 19 cm off between two
  walls that should not have moved it).
- Not seated, in a car, climbing, on a ladder or limp; a network copy follows its owner.

Head-on the capsule still stops the body some 24 cm short of a wall (it is rounder than the chest is deep); making that exact means a
smaller capsule, which touches stairs, climbing, crouching and doors.

Tests: `WallClearanceTests`, `BodyWallClearancePlayTests` (standing, walking along, squeezed), `RealCityDoorwayPlayTests`. Picture:
`BodyWallCapture`.

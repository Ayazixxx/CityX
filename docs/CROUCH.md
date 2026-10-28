# Crouching

Owner, 2026-10-27: "the crouch animation looks bad and i dont think it even shortens the hitbox. I kind of want the crouching to work like
it does in source games with the crouch jump as well. research that and make ours more like it with a more suitable animation."

## What it was

- A pose only: the controller stayed 2.6 m tall crouched, so nothing about the body's size changed (shots over a crouched head still hit;
  it fitted under nothing).
- No slowdown: crouched walking was the full 4 m/s walk.
- The held crouch was the first frame of the Mixamo crouch walk frozen (mid-stride), with fold/unfold clips in between; sprint beat crouch.

## What Source does (Source SDK 2013, `gamemovement.cpp`)

- Hulls: standing 72 units, crouched 36.
- On the ground a duck takes `TIME_TO_DUCK` (0.4 s), eased by `SimpleSpline`; the view comes down through it and the hull shrinks when it
  finishes (`FinishDuck`). Standing up takes `TIME_TO_UNDUCK` (0.2 s), and only if the standing hull fits (`CanUnduck`): under a low ceiling
  you stay down.
- In the air the duck is instant and the origin is lifted by the hull difference (`origin += hullSizeNormal - hullSizeCrouch`): the feet come
  up, the head stays. That is the crouch jump: duck at the top of a jump and clear a ledge the standing body could not. Letting go in the air
  drops the feet back down, when there is room.
- Ducked on the ground: a third of the speed (`HandleDuckingSpeedCrop`, `frac = 0.33333333`).

## What was built

| | |
|---|---|
| The rules | `Character/Movement/CrouchHull.cs` (pure, `CrouchHullTests`): 0.4 s down and 0.2 s up on the ground, eased; instant in the air with the feet lifted (`Shift.TuckFeet`) or dropped back (`DropFeet`); no standing without room; a third of the speed on the ground |
| On the body | `PlayerLocomotion.Crouch.cs`: the controller takes the crouched height while ducked (`CrouchHeightShare` 0.78: 2.03 m of 2.6), the body is lifted or lowered by the difference in the air, `StandingFits` is Source's `CanUnduck` (an overlap of the standing capsule, the body's own colliders ignored). Crouch now beats sprint, as in Source |
| The camera | `ThirdPersonCamera`: the pivot comes down by `CrouchDrop` (the eyes' drop, eased with the duck); a crouch in the air lifted the body by as much, so the view holds still |
| The look | `CrouchBaker` bakes the crouch from the game's own Godot idle, walk and side-steps (so it moves like everything else): the hips 0.38 m down and 0.12 m back, the whole upper body leaned 24 degrees forward on them (the torso is one piece on the hips), the neck turned back up, each leg folded under by a two-bone solve onto where the walk puts the foot, the knees a little out, a planted sole flat. Each clip's steps are drawn in or out so it covers the crouching speed (1.33 m/s) at its own pace (measured from its planted feet). Backward is the crouched walk reversed: the Godot walk backward is slow, and fitting it nearly doubled its steps. Numbers in `Character/Posing/CrouchPose.cs` |
| In the Animator | `PlayerRigBuilder.AddCrouchLayer`: a layer of its own, masked to the root (the hips' height and lean), the torso, the head and the legs, over the base and under the upper-body layers; one state, a directional blend of the crouch clips on MoveX/MoveZ at the crouching speed. Its weight is the duck's eased share each frame (`ShowCrouch`), so going down and up are Source's 0.4 s and 0.2 s with nothing to transition between, and the arms keep whatever the base and upper layers give them (fists, sword, gun). The hips follow the clip while crouched (the walk holds them upright otherwise) |
| Feet | `FootIK` takes the crouch tree's strides while crouched (ClipCadence paces it like every other tree) |
| Other players | The `Crouching` parameter is synced as before; a copy runs the same rules from it (no room checks, no lifting: its position comes from the owner), so its controller (what a shot at it hits) and its pose duck with the owner's (`NetworkedPlayerStateSyncPlayTests.ACrouchShowsOnAnotherPlayersCopyAndShrinksItsHitbox`) |

## Numbers on this body

- Crouched: 2.03 m tall (78% of standing). Source halves its hull; this body's legs are a third of its height (hips at 0.9 m of 2.6) and
  its head is big, so most of the drop comes from the lean.
- Crouch walk: 1.33 m/s (a third of the 4 m/s walk).
- Crouch jump: the feet come up 0.57 m. A plain jump's feet reach 1.03 m, and the controller climbs an edge 0.4 m above its feet even in
  the air, so a plain jump gets onto about 1.4 m; a crouch jump onto about 2.0 m (`PlayerLocomotionCrouchPlayTests` puts a ledge at 1.63 m).

## Tests and pictures

`CrouchHullTests` (the rules), `PlayerLocomotionCrouchPlayTests` (the controller shrinks and the crouched body fits inside it with its feet
on the ground; a third of the speed and no sprint; stays down under a slab and stands once out; a duck in the air lifts the feet and not
the head or the eyes; the crouch jump clears a ledge a plain jump cannot), the copy test above, `ClipCadenceTests` (the crouch tree is
paced). Pictures: `CrouchCapture` (Explicit, `-Graphics`): standing and crouched still from three sides, the crouched walk forward,
sideways and back, a crouch at the top of a jump.

## Not done

- Weapons held crouched use the arms of whatever plays below (the walk's, a sword's, a gun's): no crouched aiming pose of its own.
- The baker warns of a foot's twist jumping between two frames in the walks (the sole laid flat as it lifts); not visible in the frames
  looked at.

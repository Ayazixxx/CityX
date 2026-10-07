# Making the Unity character look and move like the Godot one

The owner kept saying the Godot character looked better than the Unity one. Side-by-side renders (same camera, light and poses: `tests/scratch_render_player_compare.gd` on the Godot side, `CharacterCompareCapture` on the Unity side, output in `CityX/Logs/CityShots`) found **five separate causes**, none of them the proportions that earlier sessions had checked.

## What was wrong, and what fixed it

1. **The arms were 15% short.** The body shape was built from `BodyRig.PositionOf`, whose "hand" is the *wrist* (85% along the arm). The arm capsule stopped there and the whole hand was missing. Fix: `BodyRig.ShapePositionOf`, where a hand is the end of the capsule (`HAND_X, HAND_Y`, fused_body.gd).
2. **The body was modelled in a T-pose.** Every clip then swung the shoulders through about 77 degrees, which shears a shoulder into a knob with a crease under it. The Godot body is modelled arms-down and its clips only bend it a little. Fix: the surface is extracted arms-down and bound arms-down (`BodyRig.RestBindMatrix`, `FusedBodyBuilder`), while the live skeleton stays a T-pose for Unity's humanoid retargeting. The garments were already bound this way.
3. **A humanoid-avatar setting tore the body at every sprint.** `upperLegTwist`, `lowerLegTwist` and the two arm equivalents were at Unity's default 0.5, which is for rigs that have twist bones. This body has none; with the default a bent leg or arm spun about its own length by up to 130 degrees for a few frames of every fast stride (the "pelvis tear" and flung arms in every sprint capture, from any clip). Now 0 in `BodyRigBuilder.BuildAvatar`.
4. **Ragdoll rigidbodies had interpolation on while animated.** The kinematic limb bodies rewrote each bone's transform every frame to smooth it between physics steps, a second writer on bones the Animator also writes: lower legs and arms flipped 110 to 150 degrees in one frame and back. A clip sampled directly was clean, the same clip through an Animator was not. Now `RigidbodyInterpolation.None` while kinematic, `Interpolate` only while limp (`Ragdoll.SetKinematic`, `RagdollBuilder.MakeBody`). Guarded by `LimbFlipPlayTests`.
5. **The locomotion clips were a different retarget of the same Mixamo files.** Both games start from the same Mixamo clips. The Godot ones (`character/animations/*.tres`) are already retargeted to this skeleton; Unity's humanoid retarget gave different motion, so an earlier session stretched the leg swing 1.8x, which made the extremes worse. Now `GodotMotionBaker` converts the Godot clips into Unity humanoid clips (`Character/Animations/Godot/godot_*.anim`): idle, walk, run, walk_backward, walk_left, walk_right. Measured after: sprint thigh swing 65.6 forward / -39.5 back and 126 degrees of knee bend against Godot's 66 / -40 / 127.

6. **The baked clips were a MIRROR IMAGE of Godot's** (found by the owner: pressing A played D's strafe). Godot is right-handed and Unity left-handed, and Godot's `_l` bones are the character's physical right (character.gd explains the crossing), so copying joint for joint mirrors the whole character. A walk or run looks the same mirrored; a strafe does not. The baker now reflects every track across the body's x = 0 plane: left and right bones swap, a rotation `(x, y, z, w)` becomes `(x, -y, -z, w)`, hips x is negated. After it the arm poses match Godot frame for frame (`compare-arms-closeup` renders). Guarded by `LocomotionClipDirectionPlayTests`: a walking foot slides opposite to the way the body moves, which tells every clip's direction without reading its name (checked: swapping the expected strafe directions makes it fail).
7. **Skin weights were far wider than Godot's, so the torso dragged and wrinkled at the armpit.** The first port blended every capsule within 0.75 of the thicker limb's radius (about 0.2 m at a shoulder), on the belief that Godot's rule was nearest-capsule-only and tore. Godot's rule is a softmin: each capsule contributes `exp(-(d - dmin) / BLEND_K)`, summed per bone, top four bones normalised (`BLEND_K` = 0.06, the same k as the surface blend). `FusedBodyShape.WeightsAt` is now exactly that. The top of a shoulder is mostly the spine's, as in Godot (test threshold 0.9, was 0.8).

Also: the boxing guard sits lower (`FistStrikes.GuardOffset`, y 0.28 to 0.10 arm lengths): the guard places the wrist, and the hand now reaches 0.16 m past it, so the old height put the fists over the eyes.

Also: while the plain walk/run blend plays, the spine now keeps the clip's forward lean (`PlayerLocomotion.LocomotionClipsOwnTheSpine`); the hips stay locked, as in Godot. Every other state still holds the spine upright.

## How the baker works (`Character/Editor/GodotMotionBaker.cs`)

A Godot track is one bone's rotation in its parent's frame from a rest where every rotation is the identity (arms hanging). The Unity skeleton is a T-pose, so the arm chain is turned by the shoulder's hang angle S (`BodyRig.ArmDownDegrees`, 77.3): shoulder `q*S`, forearm and hand `S^-1*q*S`. Skinned against the arms-down bind that is exactly Godot's deformation. Each frame is applied to a throwaway skeleton and read back as muscle values with a `HumanPoseHandler`. Curves are simplified to straight segments within a small tolerance (a 10 s idle was 6 MB dense). The reflection across x (see 6 above) is the only coordinate conversion: the joint numbers and facing (+Z) agree.

`CharacterRebuild.RebuildAll` and `PlayerRigBuilder.Build` bake first, then build the controller from the baked clips (the Mixamo clips are the fallback if baking fails). To move another state over, add its Godot clip name to `DefaultClips` and pick it in `PlayerRigBuilder` with `GodotOrMixamo`. Not moved yet: jump, fall, crouch, climb, car, dance, sword and pistol sets.

## Reproducing the comparison

- Godot: `"tools/Godot_v4.7.1-stable_win64.exe" --path . res://tests/scratch_render_player_compare.tscn` (poses sheet), `.../scratch_render_clip_strip.tscn` (one clip across its cycle), `.../scratch_dump_player_joints.tscn` (joint positions). `-s res://tests/scratch_dump_run_range.gd --headless` prints thigh swing and knee bend.
- Unity: `tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CharacterCompareCapture` (sheet, strips, the arm close-up against `scratch_render_arms_closeup.tscn`, and the same numbers in the log).

## Still different from Godot

- The head turns about its own centre rather than the neck's top (the Godot head bone pivots at `NECK_Y` with the sphere offset above it), about 4 to 6 cm of forward travel in idle.
- Skin colour is the player's chosen one; Godot's default renders white.
- Hand-held items were tuned against the old hand, which ended at the wrist; the hands now reach the fingertips, so check grips after this change.

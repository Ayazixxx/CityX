# Mixamo clips with a pistol and a two-handed sword: what was wrong, and the fix

Asked (2026-09-26): "research how to properly utilize mixamo animations for pistol and
two handed sword mechanics and implement what you learn because as they are now doesnt
cut it". This is what was found by **measuring the real rig with the real weapons in
real frames** (`WeaponHandsPlayTests`, pictures in `CityX/Logs/WeaponPoses/`), and what
was built for it.

## What the practice says (and what it meant here)

Mocap for a weapon is authored for *someone else's skeleton holding someone else's prop*.
Every practical guide to using it for a two-handed weapon lands on the same recipe
(Unity's Humanoid retargeting notes and general game-animation practice; nothing here
depends on a single source):

1. **The animation owns one hand; IK puts the other on the weapon.** Keep the mocap
   placement of the hand that holds the weapon, then reach the support hand onto it.
   A two-bone (shoulder-elbow-wrist) analytic IK is the standard tool; the elbow's side
   comes from a hint.
2. **The weapon's mount is derived from the clip, not typed in.** A grip offset tuned
   against one clip is wrong for the next one.
3. **Don't throw the torso away.** Weapon swings are driven by hips/spine rotation; an
   arms-only swing looks like flailing.
4. **Feel is not in the mocap.** The clip has no contact in it; a hit needs a few frames
   of hit-stop.
5. **Check the import** (loop seams, compression) before blaming it.

## What was actually wrong, measured

Player is ~2.6 m tall (arm 52 + 37 cm), so "20 cm apart on a grip" in a real person is
~30 cm here.

| | before | after |
|---|---|---|
| Sword, idle: distance of the hands from the handle | 46 cm and 43 cm | 1 cm and 0 cm |
| Sword, worst hand-to-handle distance in a light swing | **115 cm** | 6 cm (first frame, while it blends in); 4 cm after |
| Sword, blade vs the line between the hands | 87 deg (held across the chest, blade over the shoulder) | 25 deg; blade up, edge forward |
| Pistol, aiming: hands apart | **61 cm** (left arm dangling) | 15 cm (palms on the grip) |
| Pistol, support palm from its place on the gun | n/a | 0.5 - 1.7 cm at 0 / +40 / -40 pitch |
| Loop seams on all 11 weapon locomotion loops | not measured | 0.0 - 1.2 cm (fine) |

Three separate causes, found in this order:

1. **The sword mount was Godot's numbers.** `GripHandPosition/Rotation` were tuned for
   Godot's clips; against the Mixamo `2hsw_idle` it held the sword sideways. **Fix:** the
   mount is rebuilt from the animation: in the idle the two hands are 39 cm apart on one
   line (right on top), that line in the right hand's frame is the blade axis
   (`SwordWeapon.BladeAxisInHand`), and the handle passes through the palm. The roll
   (which way the edge faces) was chosen from four rendered candidates: 270.
2. **The rig has no Chest, UpperChest or Shoulder (collar) bones** (checked, not
   assumed: `GetBoneTransform` returns null for all three). Mixamo's swings bring both
   hands together with chest twist and collar-bone swing; Humanoid retargeting silently
   drops both, so the arms alone put the hands wherever they land - 39 cm to **125 cm**
   apart on a 37 cm handle (the heavy attack). **Fix:** the support hand is reached onto
   the weapon every frame (below). Torso twist cannot fix it: twisting the spine turns
   both shoulders and the weapon together, so hand-to-hand distance does not change.
3. **`PlayerLocomotion.LateUpdate` held hips and spine at identity every frame** (added
   long ago to stop a walk-cycle "butt wiggle"). That also discarded the torso lean and
   twist of every swing. **Fix:** during a committed sword attack or a guard the spine
   and hips follow the clip (blended in and out at 10/s); walking and standing keep the
   lock. Full follow bowed a light swing 64 deg (one spine bone is handed the clip's
   share for three), so it keeps 65% (`PlayerLocomotion.TorsoFollowStrength`).

## What was built

- `Character/TwoBoneIK.cs` - the analytic reach (law of cosines, elbow hint, blends by
  weight, clamps to 99.5% of the arm so it never locks straight). 6 unit tests
  (`TwoBoneIKTests`). It caught a real bug in its first version (the forearm was aimed
  from its pre-shoulder-turn rotation).
- `PlayerLocomotion.SetSupportHand / ClearSupportHand / ApplySupportHand` - runs at the
  end of `LateUpdate`, after the gun aim, aims the **palm** (wrist + 8 cm along the
  forearm) at a point in the weapon's own space. Eases in and out at 9/s. A gun passes
  `onlyWhenAiming` so the free hand swings free while the pistol is carried and comes up
  only as it is raised.
- `SwordWeapon` - one mount for every clip (the per-clip "heavy attack" grip is gone),
  left palm on the handle 20 cm below the right.
- `PistolWeapon.SupportGripLocal` - the free palm, one palm-width to the shooter's left of
  the shooting palm. This model is built on its side (+Z barrel, +Y the shooter's left,
  +X up) and scaled 2.5x, so 0.04 here is 10 cm. Follows the reload dip and the kick,
  since it hangs off the pistol's transform.
- `SoulsSwordCombat` hit-stop - a landed hit freezes the swing's clock and its clip for
  0.07 s (1.8x for a heavy), then lets both go together. Longest stop wins when several
  targets are hit in one frame. Not networked (other players see the swing unfrozen).
- **Adjusting the hold in the Unity editor** (`WeaponGripSettings`, `WeaponGrips.asset`, the Inspectors in
  `Weapons/Editor/WeaponGripEditors.cs`): see the next section.

## Adjusting how a weapon is held (in the Unity editor)

1. Press **Play**, then equip the weapon in the game (press `` ` `` and type `/equip sword` or `/equip pistol` --
   the only console step; a pistol's free hand only comes up while you aim, so hold right-click).
2. **CityX > Weapon Grips > Select the held sword** (or pistol). The held weapon is an ordinary object under the
   right hand and carries an orange **left-hand marker** (drawn in the Scene view).
3. Drag them with the Scene view gizmos: **Move/Rotate the sword** to change the right hand's hold and which way
   the edge faces; **Move the marker** to change where the left hand holds. The hands follow live, through swings.
4. On the weapon's (or the marker's) Inspector press **Save this hold**. It is written to
   `Assets/CityX/Character/Resources/WeaponGrips.asset`, outlives Play Mode, and every later equip uses it.
   **Go back to the derived hold** forgets it. The **Swing torso follow** slider (0 = stay upright, 1 = the clip's
   full lean) is on the same Inspector and on the asset (CityX > Weapon Grips > Open the grip settings).

Untouched, the sword uses the hold derived from the animation and the pistol's free hand a measured point, so a
fresh project needs nothing saved. The old `/grip` console command was removed in favour of this.

## Checked and left alone

- **Loop seams** (`WeaponClipImportTests`, evaluated through a PlayableGraph - note
  `AnimationClip.SampleAnimation` silently does nothing to a Humanoid clip in EditMode,
  which made a first version of this test pass vacuously): every weapon loop closes to
  within 1.2 cm. So "Loop Pose" would change nothing; not turned on.
- **Compression**: keyframe reduction at 0.5 deg is ~0.8 cm at the hand, and the support
  hand is now corrected by IK anyway. Left.

## Limits / what is next

- The missing chest and collar bones are the root of it. The real fix is to give the body
  skeleton those bones (and reweight the torso to them); the IK is the workaround that
  makes every clip usable without that.
- The blade sweep and the 2.2 m reach box are unchanged, so hit detection is as before;
  the blade path is now the true one, so it is worth a feel pass with `/sword`.
- The IK runs in `PlayerLocomotion.LateUpdate`, i.e. for the local player. Remote players
  show no held sword or pistol at all yet (nothing in the network layer spawns one), so
  there is nothing on them to pose; when that is built it needs the same support hand.

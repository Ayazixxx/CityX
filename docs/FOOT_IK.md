# Foot placement, hip drop, landing, lean and slope posture

Owner, 2026-10-13: "Add Unity Animation Rigging foot IK with ground detection, hip drop, and stride adjustment so the character walks/runs smoothly
on slopes and stairs without sliding feet ... right now there is a very apparent lean when moving around in third person that doesnt really look good,
please tone it down or remove it completely but make it work with the foot ground detection. please also research dynamic movement and implement more
of what you learn."

## What the research said

- Foot IK everywhere follows one pipeline (foot-placement write-ups, Opsive's character controller, a parkour developer's stairs notes): a ray down under
  each foot finds its ground; the **pelvis is lowered** so the leg that has to reach furthest down can; a **two-bone solve** puts each sole on its ground;
  the **sole is tilted** to the surface; and a foot that is **planted is locked** where it first touched and released smoothly when the clip lifts it or
  the body outruns it. The lock is what stops sliding, and it is the stride adjustment: each stride is as long as the ground the body covered.
- Lean into acceleration, small and smoothed; keep the gameplay capsule authoritative and let the presentation warp; slope posture (lean uphill) is
  one of the cheapest big wins; a landing should bend the knees.
- Unity's Animation Rigging package runs its constraints inside the animation graph, BEFORE `LateUpdate`, which is where every procedural pose in this
  project already runs (climb limbs, aim, fist strikes, the posture), and it would fight them. The same solver this project already owns
  (`TwoBoneIK`, after the Animator) does the identical job with no new dependency, so that is what is used. The result is what was asked for.

## What it does

`Character/FootPlanting.cs` (pure, tested without a scene) and `Character/PlayerLocomotion.FootIK.cs` (`ApplyFootIK`, run in `LateUpdate` right after
`ClampFeetAboveGround`, before the upper-body poses):

1. **Ground under each foot**: a ray straight down at the ankle's position (under its lock while it is held), from 0.55 m above the clip's floor,
   reaching 0.75 m below it; walkable only (normal within 60 degrees of up); not this body, not a person, not a trigger. Ground more than 0.45 m above
   the floor is a wall, not a step; more than 0.5 m below is a drop, and that foot is left to the clip (a cliff edge never drags the hips down).
2. **Hip drop**: the whole drawn body (the Animator's root, not the collider) is lowered by what the lowest foot needs to reach its ground, up to
   0.5 m, smoothed (0.06 s down, 0.12 s up). A planted leg is straight on this rig (hip 1.3 m, ankle 0.135 m, leg 1.165 m), so it can only reach lower
   if the hips come down. Ground that is higher just bends the knee.
3. **Leg solve**: `TwoBoneIK` puts each ankle on its target, keeping the clip's own knee direction; the foot keeps the clip's angle in the world
   (not turned with the shin) and is then tilted onto its ground's normal (up to 40 degrees), with the body lean taken back off it so a planted sole
   stays flat while the body tips.
4. **Locks (no sliding)**: a foot whose clip lift above the floor is under 3.5 cm is planted and held where it is; it is let go when the clip lifts it
   past 7 cm or the body outruns it by 25 cm, fading back to the clip over 0.1 s. A body turning on the spot (over 1.5 degrees a frame while moving under
   0.4 m/s) releases both, so feet held in the world cannot twist the legs. A teleport resets everything.
5. **Landing dip**: when the body touches down faster than 3 m/s the hips get a downward impulse into a critically damped spring (4 cm per m/s, at
   most 30 cm): the knees give and the body stands back up in about a quarter of a second.
6. **Slope posture**: the average ground normal under the feet goes to `BodyLean`: walking uphill leans into the hill by 0.35 of the slope's angle,
   downhill back by 0.2 of it.
7. **The lean is toned down** (`BodyLean`): 0.3 deg per m/s of speed (was 0.9), 0.5 deg per m/s² of acceleration (was 1.1), capped at 6 (was 13); a
   sprint tips the head a couple of degrees, a hard stop or turn about six.

Off in the air, on a wall, over a ledge, during a vault or mantle (the controller is off), in a seat, in a dance (the body may be on the floor) and during
a baked move; it fades in and out over 0.12 s. Everything is read from the body's own position and the ground, so **another player's copy places its
feet the same way with nothing sent** (like the lean). `PlayerLocomotion.FootIkEnabled` switches it off for a comparison.

## Tests and pictures

- `FootPlantingTests` (EditMode): the drop for a low foot and none for a high one; the cap and the cliff edge; a planted foot held while the clip slides
  it; let go and faded when lifted; let go when outrun; released on a turn; a landing dips and recovers; nothing in the air.
- `FootIkPlayTests` (PlayMode, real rig): standing across a 25 degree slope each sole sits on its own ground within 3 cm, tilted to it, and the hips drop;
  walking up 12 stairs the planted feet sit on the treads (within 4.5 cm) and never slide more than 1.5 cm in a frame; on flat ground the hips stay up and
  nothing changes; a 3 m fall bends the knees and the body stands back up.
- `FootIkCapture` (Explicit, `-Graphics`): `Logs/CityShots/footik-stairs.png` and `footik-slope.png`, the top row with the placement on, the bottom row
  with it off.

## Not done

- A proper tuck for the dive vault (the legs trail straight: see CLIMBING.md, the window vault).
- Head-stable lean (counter-rotating the head against the body lean); the lean is now small enough not to need it.
- Foot locking during attacks is on (the feet stay planted through a punch), which is right, but a sword clip that deliberately slides a foot will be
  held until it lifts the foot or outruns the lock.

## Cadence matching: why the feet slid in the first place (2026-10-13, same day)

Measured on the rig while fixing the locks: a planted foot in the walk clip moves back through the body at about 1.5 m/s and in the run at about 4,
under a body that walks at 4 m/s and sprints at 7.5. The clips are Mixamo-paced and the Godot game played them the same way (player.gd: SPEED 4,
SPRINT_SPEED 7.5, no speed scaling), so every step has always slid two to three metres a second on flat ground -- and a lock cannot hold a foot the
clip drags that fast (it reads as a swing within two frames). The research's answers are stride warping and cadence warping; on legs this short the
stride cannot stretch to 4 m a cycle, so the cadence does: `Character/Editor/ClipCadence.cs` measures each locomotion clip's ground speed on the rig
when the animator is built (the median speed of a foot while it is within 3 cm of the clip's lowest foot height, sampled 90 times round the loop)
and gives every blend child `timeScale = body speed / clip speed` (a 2-D child's position, a 1-D child's threshold, the parent's speed for a nested
diagonal mix; the idle and a held pose are left alone; clamped to 0.5 to 4). Plain, sword, gun and crouch trees all get it. `ClipCadenceTests`
checks the built animator; `FootIkPlayTests.OnFlatGround...` checks that planted feet keep up with the body within 25% walking and sprinting.
Side effect: the walk takes quick short steps at 4 m/s (its cycle is about 0.4 s rather than 1.03): the honest pace for a Mixamo walk asked to do a
jog's speed. A walk clip authored for this speed would look better than a sped-up one.

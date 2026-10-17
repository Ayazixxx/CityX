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

`Character/Movement/FootPlanting.cs` (pure, tested without a scene) and `Character/Movement/PlayerLocomotion.FootIK.cs` (`ApplyFootIK`, run in `LateUpdate` right after
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

## Update, the same day: the gaits, the lean from the head, and what the owner said

Owner, after trying it: the feet still "gliding / floating slightly off the ground", and "the walk and running animations feel too rapid and like
short steps compared to the speed the character is moving", then "the speed and animations of the leg movements for walk and sprint that we had
before was decent, I just wanted to add the IK foot connection to the ground", then "do what you think is best, research other games with dynamic
movement like Last of Us". And a forwarded paragraph of movement advice (lean from the head with a delayed foot centre that cannot catch up, the
lead foot angled when strafing, the feet returned to their relaxed rotation in swing, pelvis yaw following the feet).

**What the research says** (Epic's guide to adapting Lyra's animation, Naughty Dog's Last of Us Part II motion matching, a mocap studio's
locomotion design guides): pick the clip whose own pace is nearest the speed (walk, jog and run tiers; keyboard players mostly see the jog and run);
change its play rate by at most 15-20% and its stride by at most as much again ("values beyond that result in significant visual quality drops, and
it's often better to allow foot sliding"); let foot IK take the rest; large libraries so a clip exists for every speed.

**What it does now** (`Character/Editor/ClipCadence.cs`, `GaitStrides`, `FootPlanting`, `PlayerLocomotion.FootIK.cs`):
- At every straight-ahead speed the clip that needs the least re-pacing plays: **4 m/s is a jog** (the run clip slowed 11%), the sprint is the run sped
  up 20% with 20% longer strides (`GaitStrides`: the foot placement puts the feet that much further along the travel, the hips dipping to reach) and
  the foot locks holding the last 25% (stride limited to 12%: more dipped the hips at every heel strike). The walk clip keeps a sample at its own 1.4 m/s (a slow push on a stick). The sword, gun
  and crouch trees keep their own clips (a sword sprint clip carries the sword differently) re-paced within the same limits. A strafe or backward walk may be re-paced up to 50% and still slides some: there is no faster clip for them.
- A foot is **held** only while the clip's planted feet move through the body within 0.7 to 1.4 of the body's speed (`FootPlanting.LocksViable`),
  measured as it plays; otherwise the feet are placed on the ground but not held (holding one would drag it to the stretch limit every few frames).
- A planted foot is **pressed onto the ground** (no residual lift kept); a lifted one keeps the clip's lift over the clip's own floor. The foot's lowest
  point comes from its rotation (toe, heel or sole), so a toe-first run plants its toes.
- The grace over the controller's "not grounded" flicker holds only while ground is within 12 cm under a foot, up to half a second.
- The **lean turns about the head** (`BodyLean`, second version): the feet's centre chases the body's velocity times 45 ms through an under-damped
  spring, so a steady sprint holds the feet 34 cm behind the head (8.7 degrees), setting off overshoots, a hard stop swings the feet out in front, a
  tight turn leans into itself, and the head does not move (the drawn root is turned, then shifted to keep the head put). Capped at 10 degrees.
- Strafing, the **leading foot turns** toward the travel (60% of the way, up to 35 degrees).
- `/footik` in the console shows it all live (on/off, placing or not, hip drop, each foot held/on the floor/lifted, the clip's pace against the body's,
  the stride); `/footik off` switches it off to compare.
- Not done from the advice: pelvis yaw following the feet and pelvic tilt (this torso is one tube on the hips; turning the hips turns the chest), the
  feet relaxed in swing (the clips already turn them).

Sources: Epic, "Adapt the animations in the Lyra sample project into your game" (unrealengine.com tech blog); Naughty Dog, "Motion Matching in The
Last of Us Part II" (GDC 2021); MoCap Online, locomotion system design and walk/run/sprint blend guides.

## Update: the walk is the walk again, and the head looks ahead (2026-10-13)

Owner, after trying the jog: "the walking animation is the same as the running animation now just slower. he shouldn't be pumping his arms like that
when he is just walking", and "I don't like how the head looks side to side when running, it should just look forward".

- The gait swap is OFF (`ClipCadence.GaitSwapTree` is empty; set it to "Locomotion" to bring the jog back): 4 m/s plays the walk clip again, sped up
  20% with 12% longer strides (the research limits). Its feet are pressed onto the ground but not held (the walk clip's feet still move at about half
  the body's speed, too far apart to hold them without dragging). The sprint is unchanged. Removing the last of the walk's glide needs a walk clip
  with longer strides, or a slower walking speed.
- THE HEAD LOOKS STRAIGHT AHEAD in the walk/run/sword/gun/crouch blends (`PlayerLocomotion.ApplyHeadForward`, after the foot placement): the clip's
  side-to-side swing and the lean's roll in turns are taken out against the body's facing and the world's up; the up-down angle (clip, lean, posture)
  is kept, and the posture's own turn and tilt go back on. An aim still turns the head to the sights. Test: `HeadForwardPlayTests` (within 3 degrees of
  the body's facing, level within 3 degrees, walking and sprinting).

## Update: standing, the soles lie flat (2026-10-15)

Owner: "make the feet lay flat on the ground i always see the heels hovering". The idle clip stands on tiptoe; foot placement put the lowest
point of each foot (the toe) on the ground, so the heels floated up to 8.8 cm. Below `FlatFootBelowSpeed` (1 m/s, fully when still) each
foot is now measured as a flat sole (`SampleFoot`: its sole is `BodyRig.FootY` under the ankle, not the tiptoe's depth) so the ankle is set
down until the whole sole is on the ground, and turned so the sole's up is the ground's normal (`SolveLeg`, the foot's heading kept). Walking
and running are untouched. `FlatFeetPlayTests.StandingInTheIdleBothHeelsAndToesAreOnTheGround`: heel and toe now within 0.4 cm.

## Update: a sword fight's feet step (2026-10-16)

Owner: "make sure his feet dont lock up during combat animations while moving". Swinging on the move left the legs to the walk clip slowed to
40%, which shuffled the feet along the ground. While a sword attack is in progress (and in the ready stance or the guard below a guarded walk)
the ankles are now placed by `SwordFootwork` instead of the clip: a stance from the sword's frame, planted feet that never move, a 0.17 s
step when one is left too far from its place, within the leg's reach of its hip (`FootReach`, from the hips' height after the planting's and
the sword crouch's drop). In `ApplyFootIK` the stepper's ankles replace the clip's before the samples are taken, the planting's locks stand
aside while it has the feet, the soles lie flat, the toes and the knees point the stepper's way, and each sample's hip is taken where the
crouch will put it (`PoseHipDrop`) -- judged from standing height, a planted foot out in the stance looked out of reach and was pulled in
under the hip: dragged. A sword cut's hips are turned before all this (`ApplySwordHips`) so the legs are solved under them. See
docs/SOULSLIKE_COMBAT.md ("The feet step under the sword").

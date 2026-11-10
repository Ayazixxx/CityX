# The one-handed sword's three cuts as full-body clips

Owner, 2026-11-05, asking what full-body Blender animation is, against what the game does now: "our animations look alright right now could you just use those as referance".

## What is built

`CityX/SourceArt/Sword1HCombo/`: three cuts made in Blender on the real skeleton, written out as `Sword1HComboMotion.json`. **They are not in the game yet.**

- **Kept from today's moves** (`SwordStrikes`, ported into `sword_combo_pose.py`; keep the two in step): the hand's path, the blade's direction and edge, every phase's timing, the hips' and shoulders' turn, the lean and how far the hips sink. So a click lands when it lands today.
- **Added around them:**
  - **The feet are planted in the world.** The game's lunge carries the body past them, and a planted foot's place in the clip is its place on the ground less how far the body has lunged, so it stays still. (`SourceArt/planted_feet.py`, shared with the punches' clips since 2026-11-09, which are in the game: docs/FIST_COMBAT.md. What they needed there, and these clips will too: a stride drawn in when the body is stopped short, `PlayerLocomotion.ApplyFistStride`.)
  - **One real step a cut.** The left foot strides on the first cut, the right passes it on the second, and the left strides again on the finisher. The rear foot comes up after each.
  - **The weight moves.** The hips go back over the rear foot in the wind-up and forward over the front one for the blow.
  - **The rear heel comes up** as the body leaves that foot, and the foot turns out.
  - **The head stays on what is ahead** while the shoulders turn under it.

To build and check: `blender --background --factory-startup --python CityX/SourceArt/Sword1HCombo/sword_combo_build.py -- --render <folder> --export CityX/SourceArt/Sword1HCombo/Sword1HComboMotion.json`. It prints each cut's worst wrist bend, tightest knee and furthest ankle.

## What is left

1. Copy the JSON to `Character/Resources/` and play it: `SwordPoseDriver` samples its segment (`slash`, `cut`, `finisher`) on the swing's own clock and hands it to `PlayerLocomotion.SetBakedPose`, so hit-stop and Melee Speed carry over, and another player's copy plays it from the swing's name as it does today.
2. A console command to switch between the old and new moves, so the owner can compare them. Keep the old moves until the owner has chosen.
3. A rebound or guard stop drops back to today's pose (`SwordStrikes.StoppedOnGuard`, `Rebound`).
4. Slopes and stairs: the clip's feet assume flat ground, so foot IK should correct them.
5. Tests: the clip's frames, the hand staying on `SwordStrikes`' path, and a planted foot staying still in the world as the body lunges.

## Known weak spots

- **Chaining.** Each clip starts from the ready stance. A chain starts the next cut during the last one's recovery, so the feet will blend across there and may slide. Clips made for each join would cure it.
- **The finisher's stride is shorter than today's stance** (0.47 m between the feet at the blow, not 0.73 m): the 0.55 m lunge takes the body past what the legs can reach, so the back foot is pulled along behind on its toes.
- **The backhand's wind-up bends the wrist up to 108 degrees**, because the edge is kept leading as today's move does.

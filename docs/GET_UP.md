# Getting up off the ground

The ragdoll's get-up: the end of the fall from the sky (`IntroCutscene`) and every knockdown in play (Space once the body has lain still,
a finisher, a jetpack crash, the grapple's landing, civilians and enemies).

Owner, 2026-11-10, of the get-up borrowed from a Mixamo body: "the limbs bend all awkwardly clearly in an unintented way", "its like the
knees bend the wrong way", "i think it needs a better standing up animation"; and of the intro, "the feet are clipping through the
sidewalk".

## The clips (`CityX/SourceArt/GetUp`)

Authored for this body in Blender with Octaslash's pose solver, the way the punches were (`docs/FIST_COMBAT.md`), and exported to
`Character/Resources/GetUp/GetUpMotion.json` (`GetUpMotion`):

- `getup_back` (3.65 s): on its back, the head comes up, the left knee bends and its foot plants, the right arm goes out past the head, and
  the left foot and the reaching left arm roll the body over its right side onto its hands and knees.
- `getup_front` (3.15 s): face down, the hands come in under the chest, push it up, and the hips go back onto the knees.
- Both then step the right foot forward between the hands, lift the hands onto that knee, drive up over the foot, bring the left foot up
  beside it and settle standing as the idle stands (hips 0.85 m, on the root).

The body is long in the arm and short in the leg under a 1 m torso, so on hands and knees its shoulders sit well above its hips; every
contact is written at the height its limb's skin touches the ground (wrist 0.10, ankle 0.135, knee joint 0.135). Between the keys a knee
or elbow that would dip into the ground is turned out of it about its own limb's line (`keep_out_of_the_ground`). Lying on its side the
body rests on its shoulder, which this body carries 0.35 m out from the spine: the hips are kept 0.42 m up there.

Rebuild (headless): `blender --background --factory-startup --python CityX/SourceArt/GetUp/getup_build.py -- --export
<Character/Resources/GetUp/GetUpMotion.json>`. `--render <dir> [--close] [--every N] [--only getup_back] [--upto N]` writes check frames;
`--skin` measures the skinned body against the floor every other frame (its soles read about 5 cm deeper than the game's; the game's
`GetUpFloorPlayTests` is the measure); `--verbose` lists every frame a joint goes under the ground. The bake prints the worst knee hinge
(`knee_hinge`: above 0 is a knee bending forward; below, a thigh turned round with its knee pointing back).

## In the game (`Ragdoll.GetUp`)

- `BeginStandingUp` captures the pose the ragdoll lies in, picks the clip by which way the chest faces (`GetUpMotion.For`: up at all, the
  back), turns the root so the clip's first frame lies where the body lies, and rolls the body about its own length by whatever is left
  between the two (a quarter turn at most, from its side), every joint easing from the ragdoll's angle to the clip's over at least 0.4 s.
- `PoseGetUp` samples the clip in `LateUpdate`, after the Animator (which plays Locomotion underneath), and sets each bone by name, the hips
  in the body's own frame. Remote players see it through the ragdoll pose stream (`NetworkedRagdollSync` streams while getting up).
- `GetUpPace`: in play 2.2 times the clip's speed (control back 1.66 s from the back, 1.43 s from the front); the entrance 0.85 times
  (`IntroCutscene.GettingUpSpeed`).
- At the end the clip's last pose is eased into the Animator's over 0.25 s (`BlendFromSnapshot`), and the root goes onto the ground under
  the lower foot (`StandTheRootOnTheGround`): the root had been put on the ground under the lying hips, which across a kerb is the road, and
  the intro stood its look-around with its feet in the sidewalk.

### On the ground it really lies on (`HoldOnTheGround`)

The clips are made on a level floor at the root. While the clip lies, the whole pose is tipped to the slope under it; as it comes onto its
feet the hips go down to the lower foot's ground; and each hand and foot the clip has on its floor is put as high over the ground under it
as the clip has it over its floor (`FollowTheGround`, `TwoBoneIK`), its elbow or knee bowing the way the clip bows it. Before, a floor
clamp turned every joint near the ground to bow upward: on a kneeling leg that bent the knee backward. Reaching down a step can swing a
foot over the step above it, so the ground is looked at again where the foot went and it is lifted out of whatever it went into.

## Tests

`GetUpMotionTests` (every frame: no knee turned round or bent forward; each clip starts lying and ends standing on its root),
`GetUpPaceTests`, `GetUpOnTheGroundPlayTests` (each way up gets its own clip, eased in where it lies; stairs; a slope),
`GetUpFloorPlayTests` (the skin never more than 3 cm under the floor from the back or the front, the soles on the floor once up),
`RagdollStandUpPlayTests`. Pictures: `StandUpCapture` (Explicit: back, front, side on level ground, a slope, stairs) and
`IntroCutsceneCapture` (a picture every quarter second while the body gets up).

Not done: a get-up of its own from the side (it rolls to its back or front first).

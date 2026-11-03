# Going up and down stairs

Owner, 2026-11-02: "search for bugs try to make going up and down stairs look less jank research how video games do staircase animations". Then 2026-11-03, of the first pass's legs (the walk clip held onto the steps): "this looks all fucked up ... look at his legs. look up how to do good stairs animations with IK".

## What games do (research, 2026-11-02)

Nobody animates a body from the capsule on stairs. No talk from a big studio turned up, so this is general practice:

- **The capsule is physics, the body is drawn.** The capsule pops up each riser; the body and the camera are smoothed apart from it (the usual advice for Unreal and Unity alike).
- **The pelvis follows a blurred ground** ([a procedural walking patent](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/8243078)): its height comes from the terrain's height blurred, so it sees a slope and never a step. The same patent starts preparing for a step ahead of it.
- **The feet land on the real steps** ([Opsive's controller](https://opsive.com/support/documentation/ultimate-character-controller/inverse-kinematics/)), low-pass filtered ([a foot placement guide](https://docs.vulkan.org/tutorial/latest/Advanced_glTF/Procedural_Animation_IK/04_foot_placement.html)): applied directly each frame they snap.
- **The body leans into the climb** ([an animator's stair walkthrough](https://www.animationmentor.com/?p=89643)).
- **The feet are stepped, not warped** ([a working stair stepper's design](https://github.com/niktaninic/IK-Foot-System/pull/1), the most useful thing found): treads are found by scanning, the trailing foot goes next, edge-clearing arcs, the body on a ramp through the tread centres and never above what the feet can reach, an analytic two-bone solve with limits on how far a knee folds. Ubisoft's IK Rig talk makes a [stair walk from a base walk by rules on IK targets](https://80.lv/articles/how-ubisoft-revolutionises-animation-in-games) too (the article gives no detail).
- **Up and down differ** ([stair gait studies](https://johk.pl/?p=1864)): about 66% of a step on the ground going up and 60% going down, the knee bending more going up.

## What was wrong, measured

`StairWalkCapture` (Explicit, `-Graphics`) walks and sprints the real body up and down 22 steps of 0.36 m treads and 0.227 m risers, renders `Logs/CityShots/stairs-sheet.png` and logs what a picture cannot show.

| Over a flight | Walk up | Walk down | Sprint up | Sprint down |
|---|---|---|---|---|
| Frames off the ground, before | 0 of 88 | **73 of 88** | 0 of 47 | **46 of 47** |
| After | 0 | 0 | 0 | 0 |
| Head's worst jolt in a frame, cm, before | 10.9 | 13.4 | 13.1 | 20.2 |
| After | 2.9 | 1.6 | 3.2 | 2.5 |
| Head's wander off a steady line, cm, before | 5.6 | 7.5 | 6.4 | 20.4 |
| After | 1.7 | 2.1 | 2.9 | 2.4 |
| A foot's biggest jump in a frame, cm, before | 60.1 | 56.6 | 65.7 | 34.1 |
| After (now the speed of a step) | 14.3 | 13.5 | 21.0 | 20.8 |
| Tightest knee, degrees, after | 75 | 73 | 74 | 70 |
| A planted foot's worst distance from its place, cm, after | 5.2 | 0.0 | 8.0 | 0.0 |

- **Going down, the body was falling.** The controller only knows how to step up. Off each tread it flew until it landed on a lower one: hanging over the stairs at a sprint, unable to jump, its lean flicking 1 to 10 degrees between single frames (in the air the lean falls away at once).
- **The body and the camera were fastened to the capsule**, which goes up a riser in lurches of 10 cm in a frame.
- **A foot jumped when let go.** Its height came at once from the ground under where the clip wanted it, while its place along the ground faded back from its lock over 0.1 s. Over 0.3 m of stairs that is a jump of two risers.
- **A long stride left the range of the ground rays**, and the foot went to the clip, in and out of the steps.
- **The hips sat 40 to 46 cm low** the whole way, a third of the leg, so the foot furthest down the flight could reach its tread.
- **A foot is longer than a tread**, so one stood on what is under its ankle had its toes in the next riser.
- **The slope was invisible**: each tread is flat, so the lean saw level ground.
- Also found: the foot's ground ray counted broken glass as ground.

## What it does now

- **`StickToGround`** (`PlayerLocomotion.Stairs.cs`): having been on the ground and having left it this move, not by jumping, the body is put down on whatever is within 45 cm under it (`StickToGroundMetres`, a riser or two, a kerb). A taller drop is a fall. It can jump from a flight now.
- **`GroundGlide`** (pure): the ground's height at four points along the travel, ahead by what the smoothing lags, averaged and eased over 0.06 s, is where the body is drawn (at most 35 cm from the capsule); on the flat it is the capsule exactly. It gives the slope too. The camera's pivot goes with it. Points whose height is too far off the capsule's are a wall or a crate and are not counted. They are not spaced at a multiple of the 0.36 m tread, so they cross their risers one at a time.
- **The legs are all IK on a stair: `StairSteps`** (`Character/Posing`, pure, the way `SwordFootwork` is). Why: the legs are only 0.77 m hip to ankle and a riser is a third of that, and the clip swings a foot 0.6 m either way and kicks it up behind. Held to the steps, that folded the knee and curled the shin, whatever was smoothed (the first pass of this work, the picture the owner sent). So when `GroundGlide.IsStairs` (steep, with flats in it: not a ramp, not a kerb) and the body goes forward or back on the ground, the clip keeps only the arms:
  - **The trailing foot goes next**, when the hips are 15 cm past its ball going up and 12 cm going down, or at once if its leg can no longer reach it or its knee would fold shut (with the other foot still in the air then: at 4 m/s on these legs it is a jog).
  - **It lands on a real tread**, its ball 40 cm past where the hips will be going up and 25 going down, with the foot's uphill end clear of the riser: a foot is about as long as a tread, and that end is the toes going up and the heel going down. A landing that does not fit moves back toward the body.
  - **A planted foot never moves.** It rolls up onto its ball as the hips leave it, and going down stands on its toes ahead of them and lowers its heel as they come over it, so the leading leg is nearly straight.
  - **The arc** rises before it travels going up and travels before it falls going down, so it clears the edges. A foot in the air stays far enough under its hip for the knee to stay at about 75 degrees or more.
  - **The knee points forward**, not the way the clip bent it, and the hips come down only as far as the feet need (6 cm up, 10 down, 22 at most).
- **Off a stair the clip has the legs** as before, with `FootPlanting`: the ground is read where the foot really is (on its way back from a lock too), a step in it is followed over a few frames (0.035 s up, 0.05 s down), and going along a slope (`FitToSlope`) the hips drop less and a foot may stand further from the floor.
- **The torso bends at the spine** (`ApplyStairTorso`): 35% of the slope's angle going up, 15% back going down. Not with the whole-body lean, which turns about the head and swung the feet out behind it, off the steps they were placed on.

Other players' copies do the same with nothing sent: it is all read in `LateUpdate` from the body's own position and the ground.

Costs: four more rays a frame for a body that is moving, and a few at the start of each step. A single `StickToGround` sphere cast only on a frame it has just left the ground.

Tried and dropped, measured with `StairWalkCapture`: the walk clip held onto the steps with its feet locked (a planted foot skated at 2 to 3 m/s going up, and the legs curled: the owner's picture), strides shortened to 80% on a stair (locks held far less), the hips let down 20 cm going up (no better), and the ball of the foot on the higher tread under the clip's feet (`StairSteps` places them instead).

## Not done

- **The soft bend at the knee.** The body is one smooth surface, and a bent leg curves rather than folds: it shows most going down. That is the mesh, not the steps.
- **The speed.** A body still takes a flight at 4 m/s, about eleven treads a second, so the steps are quick and both feet leave the ground for a moment. Slowing on stairs would look calmer but change the feel: the owner's call.
- **The arms** are still the walk clip's. A stair clip (Mixamo has them) could give them theirs.
- Only tried on test stairs of the real size, in `StairWalkCapture` and `StairWalkPlayTests`; turning on a landing and short flights have no render yet.

## Tests

`StairStepsTests` (a planted foot never moves, every landing on a tread with its uphill end clear, the feet take turns, standing still), `GroundGlideTests` (a flight is stairs, a ramp and a kerb are not), `FootPlantingTests`, `StairWalkPlayTests` (all four ways over a flight on the real body: knees open and forward, no jolts; back to the clip at the top; a jump from a flight, a kerb and a drop, nothing on the flat, a copy too), `FootIkPlayTests` (the ball of the real foot stays where it was planted).

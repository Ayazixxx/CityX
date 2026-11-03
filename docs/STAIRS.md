# Going up and down stairs

Owner, 2026-11-02: "search for bugs try to make going up and down stairs look less jank research how video games do staircase animations".

## What games do (research, 2026-11-02)

Nobody animates a body from the capsule on stairs. No talk from a big studio turned up, so this is general practice:

- **The capsule is physics, the body is drawn.** The capsule pops up each riser; the body and the camera are smoothed apart from it (the usual advice for Unreal and Unity alike).
- **The pelvis follows a blurred ground** ([a procedural walking patent](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/8243078)): its height comes from the terrain's height blurred, so it sees a slope and never a step. The same patent starts preparing for a step ahead of it.
- **The feet land on the real steps** ([Opsive's controller](https://opsive.com/support/documentation/ultimate-character-controller/inverse-kinematics/)), low-pass filtered ([a foot placement guide](https://docs.vulkan.org/tutorial/latest/Advanced_glTF/Procedural_Animation_IK/04_foot_placement.html)): applied directly each frame they snap.
- **The body leans into the climb** ([an animator's stair walkthrough](https://www.animationmentor.com/?p=89643)).

## What was wrong, measured

`StairWalkCapture` (Explicit, `-Graphics`) walks and sprints the real body up and down 22 steps of 0.36 m treads and 0.227 m risers, renders `Logs/CityShots/stairs-sheet.png` and logs what a picture cannot show.

| Over a flight | Walk up | Walk down | Sprint up | Sprint down |
|---|---|---|---|---|
| Frames off the ground, before | 0 of 88 | **73 of 88** | 0 of 47 | **46 of 47** |
| After | 0 | 0 | 0 | 0 |
| Head's worst jolt in a frame, cm, before | 10.9 | 13.4 | 13.1 | 20.2 |
| After | 2.7 | 2.3 | 2.0 | 2.6 |
| Head's wander off a steady line, cm, before | 5.6 | 7.5 | 6.4 | 20.4 |
| After | 1.4 | 2.9 | 2.3 | 2.5 |
| A foot's biggest jump in a frame, cm, before | 60.1 | 56.6 | 65.7 | 34.1 |
| After | 20.1 | 17.3 | 11.7 | 23.7 |

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
- **The feet** (`FootPlanting`): the ground is read where the foot really is (on its way back from a lock too), a step in it is followed over a few frames (0.035 s up, 0.05 s down), and going along a slope (`FitToSlope`) the hips drop at most 12 cm up a stair and 20 cm down one, with a foot allowed further from the floor. The trailing foot leaves its tread sooner, as a real one does. On a stair the ball of the foot is what stands on the step, the heel over the edge.
- **The torso bends at the spine** (`ApplyStairTorso`): 35% of the slope's angle going up, 15% back going down. Not with the whole-body lean, which turns about the head and swung the feet out behind it, off the steps they were placed on.

Other players' copies do the same with nothing sent: it is all read in `LateUpdate` from the body's own position and the ground.

Costs: four more rays a frame for a body that is moving, two more on a stair. A single `StickToGround` sphere cast only on a frame it has just left the ground.

Tried and dropped, measured with `StairWalkCapture`: strides shortened to 80% on a stair (a foot on the floor was held far less, 9 frames against 23 up a flight at a walk and 24 against 58 down, and skated faster, 1.6 m/s against 0.7 going down); the hips let down 20 cm going up (skating hardly changed, 2.0 to 1.9 m/s, and toes went into the steps more).

## Not done

- **A stair clip of its own** (Mixamo has ascending and descending ones). The walk clip's arms and timing are still a walk's, and going up a planted foot still skates over its tread, about 2 m/s at a walk and 3 at a sprint (0.7 and 1.2 going down): the clip's feet do not move through the body as the stairs need, so they are hardly ever held. Next thing to try if it still looks off in play.
- Feet choosing their treads by shifting the walk's timing (the patent's way). A foot can still land near an edge.
- A body still walks 4 m/s up a flight, about eleven treads a second; a walk's strides cover several. Slowing on stairs would change the feel, so it is the owner's call.

## Tests

`GroundGlideTests`, `FootPlantingTests` (a step followed over frames, the limits on a slope), `StairWalkPlayTests` (all four ways over a flight, a jump from one, a kerb and a drop, nothing on the flat, a copy too).

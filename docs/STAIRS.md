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

## More research (2026-11-04)

Owner, sending [a clip of an indie game's stairs and foot IK](https://x.com/marwdev/status/1958204952817586192): "make the stairs feel more like this", then "just research how to make foot ik + stairs look good". The clip: about three unhurried steps a second, each foot flat on its own tread, the upper body gliding with almost no bob.

The best source found is Ubisoft's foot IK for Assassin's Creed, [GDC 2016, "Fitting the World"](https://media.gdcvault.com/gdc2016/Presentations/Roche_Clifford_Fitting%20the%20World.pdf) (slides read; the slides give rules and no numbers). Its aims are "Predictive is better than Reactive", "Preserve the original motion" and "Be less procedural-looking":

- **Prediction per foot:** contacts are marked on every clip automatically (toe speed and height, and whether the pose could stand on the ground). So each moving foot knows how far off and how long until it lands.
- **The foot's path:** "Foot forward comes from animations", "Height in anim is height above foot path", "Never let the foot go below foot path". The path is a line from where the foot left to where it will land. Paths are split at the opposite foot's contact.
- **Clearing steps:** a capsule cast along the path finds the surfaces, and a convex hull over them gives a "Ground Envelope" for the feet only, so a foot cannot go through a riser. Very tall rises are flagged as invalid.
- **The hips:** "Use slope from last to predicted position", "Hip height follows the slope". A support leg sets the height, which differs going up and down, and critical springs remove the bounce.
- **Locks are not absolute:** "Don't fully lock the foot!". Locked: position held, free to rotate. Sliding: on the ground, free to slide a bit, when the difference from the clip is small, matching the clip's silhouette. Unlocked: when the difference is large, "Prefer smooth motion".
- **Foot angle:** horizontal going up, parallel to the ground going down, limited because it pulls on the hips, and not at all when running. (Said of slopes.)
- **Turning:** the body turns about a point near the planted foot, not its centre, so the feet do not twist under it.

[Realtime Animation Tips, stepping stones](https://sites.google.com/view/realtime-animation-tips/tutorials/animation-stepping-stones): "IK alone isn't enough for stairs or steep hills": it recommends clips authored for a known slope, blended (up, down, two sideways), with foot IK on top, plus the same contact marking and hull. [Unreal's pose warping](https://dev.epicgames.com/documentation/unreal-engine/pose-warping-in-unreal-engine?lang=ja) changes the stride to match speed rather than the play rate.

No source was found for how Red Dead 2's or The Last of Us's stairs work. The [Red Dead 2 talk](https://gdconf.com/news/learn-secrets-red-dead-redemption-2s-player-locomotion-gdc) exists, but only its listing was read.

What this said about ours: `StairSteps` was the opposite of "preserve the original motion", since the legs were all procedural with no clip behind them, and a flight at 4 m/s was far quicker than the owner's reference. So: slow down on stairs, then recorded clips with these rules correcting them. Built, below.

## The recorded clips (2026-11-05)

Owner: "should we just use mixamo stairs animations". The four files are the owner's: Ascending Stairs, Running Up Stairs, Descending Stairs and Descending StairsRUN, on Mixamo's stock skeleton.

Measured frame by frame in Blender:

| | Up, walk | Up, run | Down, walk | Down, run |
|---|---|---|---|---|
| One loop, seconds | 1.37 | 0.60 | 0.93 | 0.40 |
| Steps a second | 1.46 | 3.3 | 2.15 | 5 |
| Slope the recording climbs | 43° | 42° | 48° | 46° |

- **The up clips are clean loops.** The foot has one shape in the walk and the run: it rises first with the heel up about 45°, goes over the tread's edge about 10 cm clear, and comes down flat. It stays flat until it lifts, with the leg nearly straight over it.
- **Both down clips are ruined below the floor line.** Mixamo holds the feet at floor height while the hips sink 0.49 m, so they end in a squat with the knees at 38°, and nothing below the floor survives. What is real in them is the upper body and the feet's travel along the ground. A time-reversed up clip is no fix: it walks backward down the stair.
- **No clip's feet can land on our treads.** They step 0.25 m on 0.90 m legs; ours step 0.36 m on 0.77 m. So the ankles stay `StairSteps`', and what comes from the recording is the rest:

What the game does now:

- **The pace: like Minecraft's stairs** (owner, 2026-11-05, after trying a stair pace of 27% of a walk: "the stairs feel horrendous now why is it so slow make it normal speed", then "just make it function like minecraft stairs no slowing down no weird movement, just minecraft stairs + foot IK"). A body keeps its own walk and sprint on a flight, the body glides over the steps, the legs are the walk and run clips', and the foot IK sets each foot on the step under it. At that pace no foot is held on a tread (`releaseLocks`), the feet keep the flat ground's reach, and going down a foot is raised at most a riser (`FootPlanting.MaxFootRaiseGoingDownQuickly`): held, or lifted to the treads behind, a sprinting leg folded up behind the body like a tail. The tread-by-tread stepping and the recorded stair clip play only up to `StairGait.MostSteppedSpeed` (2.2 m/s): standing on a stair, or slowed by snow or water. Tests: `StairWalkPlayTests.ABodyKeepsItsOwnPaceOnAStair` (the pace, no held foot, no knee folded shut).
- **The feet's timing and path** (`StairGait.Swing`) are the recording's, in shares of the way from where the foot left to where it lands. Going down the heights are rebuilt from what survives: the first frames of each fall, and the hips' drop.
- **The upper body** (`StairClipBaker`, `PlayerRigBuilder.AddStairLayers`): hips, spine and head on one layer and the arms on another, the arms only while nothing else needs them. The clip's time is `StairSteps.Phase`, not the clock, so the body keeps time with the feet at any speed, and a body backing along a stair plays its clip in reverse.
- **Why baked, not imported** (`SourceArt/StairClips/stair_clips.py`): imported as a humanoid FBX, Unity's retargeting raised the arms from about 70° below level to about 25°. So the turns are copied onto our skeleton in Blender and read back as muscles, as `GodotMotionBaker` does for the walks. The FBX files are not in the project.

Other players' copies do all of this with nothing sent: it is read from the body's own motion, like the feet.

To change the clips: `blender --background --factory-startup --python CityX/SourceArt/StairClips/stair_clips.py -- <folder with the four files>`, then rebuild.

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
- Only tried on test stairs of the real size, in `StairWalkCapture` and `StairWalkPlayTests`; turning on a landing and short flights have no render yet.

## Tests

`StairGaitTests` (the foot's path, a foot's cycle adding up), `StairStepsTests` (a planted foot never moves, every landing on a tread with its uphill end clear, one tread a step, the clip's phase in time with the feet, the feet take turns, standing still), `GroundGlideTests` (a flight is stairs, a ramp and a kerb are not), `FootPlantingTests`, `StairWalkPlayTests` (all four ways over a flight on the real body: knees open and forward, no jolts; back to the clip at the top; a jump from a flight, a kerb and a drop, nothing on the flat, a copy too), `FootIkPlayTests` (the ball of the real foot stays where it was planted).

# Octaslash (key 6, sword in hand): Sephiroth's held up special

Asked for (2026-10-11), with a GIF of Sephiroth's Octaslash from Smash Ultimate:

- a hotbar skill;
- a 3 s wind-up (shortened to 2.5 s on 2026-10-12), then an eight-slash dash in whatever direction you are looking;
- a click sends it early;
- you must be holding a sword;
- an animation like the GIF.

The owner also said: "the percentage thing is just how smash bros does its damage", so only the shape of Smash's damage carries over.

The animation was made in Blender on 2026-10-12. The owner's brief: "an animation that captures the same idea flying through the air swinging a sword doesn't have to be an exact replica". The two reference GIFs are `Downloads/450px-SephirothUSpecial2SSBU.gif` and `Downloads/Sephiroth_Up_B_2_SSBU-3588328863.gif`. A GIF pasted into chat arrives as one still frame, so they were split into every frame from the files (System.Drawing) and read frame by frame.

## What Sephiroth's does (SmashWiki "Sephiroth (SSBU)/Up special", and the two GIFs)

- **Cuts.** Eight of them, on frames 36, 38, 40, 42, 44, 46, 48 and 50: seven at 2.3%, then a final one at 7%, so the last is about 3x the others.
- **Movement.** The dash carries opponents along, then the final cut launches them. It goes "slightly farther than Blade Dash".
- **Walls and endlag.** Remaining cuts happen in place if the dash is stopped. It has heavy endlag (106 frames in all).
- **What the page leaves out.** No travel distance or charge window is given.
- **The body, from the hitbox GIF, one game frame per picture:**
  - frames 0-33, the coil: a low crouch with the blade at the hip, aimed where it will go;
  - frames 34-49, the dash: facing along it in a flying lunge (one knee driven forward, the other leg trailing). On every hitbox frame (35, 37 ... 49) the blade is straight ahead; between them the torso is twisted open (blade drawn back) or closed (blade swung across), so the sword whips from side to side in front;
  - frames 50 on, the follow-through: upright, the blade trailing low behind, then rising behind.
- **A misreading, corrected by the owner.** The first version read the open and closed frames, where his chest and then his back face the camera, as a barrel roll. The body flew flat and rolled four times. The owner: "you just made him spin around thats not the animation that sephiroth does at all. he does like a bunch of consecutive slashes facing forward with his knee bend and other leg going back showing forward motion".

## What the game does

All the numbers are in `Character/OctaslashMove.cs`.

| | |
|---|---|
| Press 6 | **Wind-up**, up to 2.5 s: the body is rooted (and hangs in the air if airborne). It turns to face where the camera looks and sinks into the coil: deep lunge, the sword hand drawn back to the hip, the blade aimed ahead, the free hand reaching out. The coil is then held, breathing, the blade trembling |
| Left click during it | the dash goes at once (the click is not also a swing) |
| Dash | where the camera looks (up, down, anywhere), about 14 m in 16 frames (52 m/s), keys and gravity off (`PlayerLocomotion.ForceVelocity`). The body faces along it in a flying lunge, left knee driven forward and right leg stretched back. The sword whips from side to side in front: drawn back on the right, straight ahead (a cut), swung across to the left, straight ahead again, at heights that vary cut to cut. After seven cuts it swings up overhead and the eighth comes down the middle into the follow-through |
| Cuts | on Smash's frames (2 frames apart), all round the body to 3.2 m: seven at 0.35x a sword swing, the last at 1.05x, which throws whatever can be launched on along the dash and up |
| People | the dash goes through bodies and cuts them; only the world stops it (`PassThroughBodiesAhead`); stopped by a wall, the remaining cuts land where it stopped |
| After | the follow-through (0.5 s, held still): the blade trailing behind, rising, then swung back into the guard; then 2 s before another |

- **The slashes trail from the real blade** (`BladeTrail`). The owner: "ideally the slashes would trail from the actual sword like the white slash trail".
  - Every frame, after the pose is on the arm, the blade's outer part is recorded, and a ribbon of the sword spin's white streak light is drawn through where it has been for the last 0.1 s.
  - It is wide and bright at the blade, narrowing onto the tip's path and fading at its tail.
  - It is drawn in the body's own space, so each cut stays a crescent round the body. In world space the 52 m/s dash smeared them into long ribbons down the street.
  - A cut moves the blade about 100° in a frame, so between frames the blade is turned, not slid (`BladeTrailPath.Between`); the tip draws the arc it swept.
  - It stops a few frames into the follow-through, once the last cut has carried through.
  - The earlier drawn crescents (`OctaslashSlash`) are gone.
- **Damage.** Every cut goes through `MeleeAttacker.LandSwordCut`, so Strength and Bloodrage count, XP is given, blood is spilled, and other players are hit through their copies.

## The animation (Blender)

It is played bone by bone after the Animator, over every other pose, not as a humanoid clip.

- **Why not a humanoid clip.**
  - A humanoid clip's muscles are clamped to the avatar's ranges.
  - The arm-snap guard holds any bone that turns faster than 1500° a second.
  - The dash whips the sword arm 100° a frame on purpose.
- **How it plays.**
  - `BakedMotion` reads the export.
  - `OctaslashAbility` samples it on its own phase clock (so a copy plays it on the same timeline) and hands each frame to `PlayerLocomotion.SetBakedPose`.
  - `ApplyBakedPose` sets each bone's local rotation and the hips' position.
  - While it holds the body, the support hand lets go of the grip, the running lean and the arm-snap guard stand aside, and the wrists keep the animation's.
- **Segments.** `coil_in` (22 frames, once), `hold` (48 frames, looped while the wind-up lasts), `dash` (16 frames, the move's own) and `recover` (30 frames, the move's 0.5 s).
  - It is eased in over 0.12 s and out over the follow-through's last 0.2 s.
  - The follow-through ends close to the sword's own stance, so handing the body back is short.
- **Direction.** The dash is authored level, along the body's forward. The body faces the dash (`SetFacingOverride`) and the whole pose is turned about `OctaslashMove.FlightPivot` (the lunging body's hips) to the dash's real pitch. That turn eases off over the last cut, which carries the body upright.

### Editing it

The files are in `CityX/SourceArt/Octaslash/`. The `.blend` there is a working file and is not committed; the scripts rebuild it exactly.

- **`octaslash_rig.py`** builds the body in an empty Blender scene:
  - the game's skeleton (`BodyRig`) at the arms-down rest the body is bound in;
  - the real body from `PlayerModel.obj`, skinned with `FusedBodyShape.WeightsAt`'s own rule;
  - a sword where `SwordWeapon`'s default hold puts it.
- **`octaslash_pose.py`** holds every pose and bakes one key a frame onto the rig:
  - poses are written as hips, body turn, spine, sword hand target and blade direction, free hand target, and feet (`DASH_SCHEDULE` is the flurry, frame by frame);
  - two-bone reaches place the arms and legs;
  - the sword hand is turned to lay the blade where asked, and the elbow sits where it does easily (down, a little out and back) unless the blade needs it elsewhere. Left to keep the wrist straight alone, the elbow flipped to point into the chest on alternate frames;
  - the blade's roll is left free so the wrist stays straight. It is kept near the last pose's roll so the sword does not spin in the hand between poses.
- **`octaslash_export.py`** writes `Character/Resources/Octaslash/OctaslashMotion.json`:
  - each bone's local rotation in the live skeleton's terms;
  - the conversion is Blender axes to Unity's, then the arm chain's hang put back.
  - Rebuilt on the game's T-pose skeleton, every joint lands within 0.03 mm of Blender's.

To change it, edit a pose in `octaslash_pose.py`, run it, then run `octaslash_export.py`, both from Blender's Scripting tab with `exec(open(r"<path>").read())`. Keys moved by hand in Blender export the same way.

Blender is driven from Claude through the "MCP for Blender" add-on, configured in `.mcp.json` at the repo root. Blender must be open with the add-on connected (port 9876).

## Multiplayer

`OctaslashAbility` implements `IReplicatedAbility` with the id `octaslash`. It sends three events: charging, dashing (with the direction) and ended.

- A copy plays the same animation and slashes on the same timeline, and hurts nothing.
- The body's movement and facing arrive with its transform.
- A copy whose dash event is lost ends its wind-up by itself after 4 s.
- Both kinds of body get it through `PlayerAbilityWiring`.

## Tests

- `OctaslashMoveTests` (EditMode) covers:
  - the frames, the damage ratio and the timing;
  - the animation: on BodyRig's bones; its dash and follow-through as long as the move's; its cut marks on Smash's frames; every frame whole;
  - every dash frame facing ahead, unrolled and leaning into it, the left knee forward and the right leg behind;
  - the blade straight ahead on every cut frame and out of the way between, raised for the last cut, which comes down ahead;
  - nothing snapping outside the dash, no bone turning near 180° in a frame inside it, and the hold looping on itself.
- `BakedMotionTests` (EditMode) covers sampling between frames, a loop wrapping to its own start (never into the next segment), and refusing a broken file.
- `BladeTrailTests` (EditMode) covers:
  - a fast cut's trail keeping to the arc the blade swung;
  - old positions forgotten;
  - the ribbon wide and bright at the blade, thin and faded at its tail.
- `OctaslashPlayTests` (real keys, real city) covers:
  - the wind-up holds still for 2.5 s, crouched in the animation and facing the dash;
  - the dash goes about 14 m where the camera looks, lunging along it and facing it, with all eight cuts, light trailing from the blade, and hits a body it passes through;
  - the animation lets go and the light fades after;
  - a click sends it early;
  - with no sword, nothing happens;
  - another player's copy shows it and hurts nothing.
- `AbilityPlaybackPlayTests.AnotherPlayersOctaslashPlaysItsAnimationOnTheirBodyThenLetsGo` uses a copy of a body holding a mounted sword: the coil crouches, the dash lunges, light trails from the blade, it stands back up and the light fades.
- The Explicit captures run with `-Graphics`:
  - `OctaslashCapture` renders `Logs/CityShots/octaslash-*.png`: the coil, dash frames 1, 2, 5, 9 and 15, and the follow-through, each from the side and from behind;
  - `OctaslashFilm` saves every frame from the held coil to the end of the follow-through, from the side and from behind, in `octaslash-film/`, to make a video from.

## Not done

- **Sound:** none yet.
- **Skill tree:** none yet; it is a test key.
- **Dragging:** Smash drags opponents along with the dash; here they are cut where they stand and launched at the end.
- **Wings:** there are no Sephiroth wings.
- **Charge effects:** none yet. Smash's purple sparks gathering during the wind-up are not made.

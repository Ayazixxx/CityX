# The intro: falling from the sky

A new character's entrance (`Character/Story/IntroCutscene.cs`, started by `IntroCutsceneStarter`). Rebuilt 2026-10-20 on the owner's
asks: "make it so you are ragdolling falling from the sky instead of an animation and make the fall impact as a ragdoll as well", "there
is a jump cut in the cutscene mid fall right before the impact", "it does like a rope climbing animation as he is standing up", "make
it so there is some fog but it fades as you get closer to the surface like you are falling through the clouds", "make it so your feet
are on the ground when you take over control", "make sure the hands arent clipping through the ground or any body parts".

## The beats

1. **Hanging** (6 frames, `PortraitCaptureDelayFrames`): the body waits 510 m up, standing (300 m until 2026-11-10; owner: "make the fall cutscene ... like 6 seconds longer in the sky"), so the HUD portrait is taken from an
   upright head.
2. **Falling**: `Ragdoll.Activate`. While falling, each body part gets air drag (`FallDrag` 0.28, about 35 m/s at most, about 16 s of
   fall) and continuous collision, and both are put back on landing. WASD or the left stick drift the body sideways, up to 10 m from
   the spawn. The body lies flat on its back with its arms and legs thrown about by the wind (below).
3. **Landing**: the torso loses more than half its speed in one frame while going faster than 8 m/s. The impact sound plays. A body
   that falls through the ground (its collider not built yet) is put back on it.
4. **Lying**: 2 s at rest, or 5 s after the landing at the most.
5. **Getting up**: `Ragdoll.BeginStandingUp(force, paceSeconds: 7.6)`, the ordinary recovery at the clip's own slow pace.
6. **Looking around** (the 12.7 s clip) and **hand-off** (1.5 s back to the play camera). At the end the feet are put on the ground
   under the player.

## No cuts

Nothing during the fall runs on a timer. The camera and the cloud follow the body's height over the ground:

- The camera follows a smoothed point: the torso while the body is limp, the player's own pivot once it rises. It swings from overhead
  (pitch 80, 10 m) to beside the body (pitch 15, 9 m) between 45 m and 4 m above the ground.
- **Where it lands** (owner, 2026-11-10: "make him fall far enough away from the buildings that the camera changes dont clip into the
  building"): not on the spawn itself (the driveway, 3.5 m from Old Guy's house) but on the most open ground within 40 m of it
  (`World/OpenGround`, from the plan: furthest from any building, Old Guy's house counted, out from under the expressways, off the water),
  that is also level all round as far as the cameras swing out (`IntroCutscene.LevelAround`, looking down at the built ground: no stair,
  raised plaza or drop; a kerb is fine). And the camera comes in short of anything solid rather than into it (`KeptOutOfWalls`). Tried:
  the most open ground on the roads only (it landed on a big stair the plan does not count as a building).
- The cloud is thick fog (4 to 30 m) for the first 50 m of the fall (above 460 m) and the scene's own fog below 290 m, eased in between: measured down from the start, so the six seconds added are clear sky over the city.
- The look-around camera glides through keys (`LookingAroundCamera`); the two old held shots were cut to.

`IntroCutsceneFallPlayTests` plays the whole intro on the real player and fails if the camera moves more than 1.5 m in a frame, if the
body never fell limp or landed, or if the feet are not on the ground at hand-off. `IntroCutsceneCapture` (Explicit, `-Graphics`) films
it in the city: `Logs/CityShots/intro-NN.png`.

## The stand-up

The "rope climb" was the get-up clip's height being thrown away on import: the body stayed at standing height while the legs pushed up
from the floor. `CharacterImportSetup` now keeps `getting_up`'s own height (`keepOriginalPositionY`, height baked into the pose).

The clip was made on a body with shorter arms and lower hips, so its hands, seat and soles went up to 25 cm under the floor.
`LimbFloorClamp` runs after the Animator (from `Ragdoll.LateUpdate`) all through the get-up:

- it lifts the hips clear;
- it puts each hand and foot back where the clip had it, no lower than its skin allows, with the elbow or knee bowing up off the floor
  (`TwoBoneIK`);
- it raises a hand whose fingertips still reach under the floor.

`GetUpFloorPlayTests` bakes the skin through the whole clip: no vertex more than 2 cm under the floor (about 1 cm at worst now).
`GetUpClipCapture` (Explicit) films it side on.

### From however it lies, on whatever it lies on (2026-11-10)

Owner: "make the standing up animation look good. the ragdoll to stand animation. use hand and foot ik to interact with the terrain it is
standing on properly. right now it looks glitchy. this is the same ragdoll to stand used at the end of the fall cutscene and when you
ragdoll ingame". Filmed first (`StandUpCapture`, Explicit: back, front and side on level ground, a slope and stairs), three faults:

- **The clip starts face down** (measured on the body: prone, a push-up to 2.0 s, knees to 3.0, feet under to 4.0, standing by 4.9; these
  notes said "on its back" and the fall lands it on its back for that). Every bone was turned straight from where it lay to the clip in
  0.4 s, so from its back the body went through itself. Now (`Ragdoll.GetUp`) it is rolled about its own length by however far it lies
  from face down (half a turn from its back, a quarter from its side, none from its front), the hips rising over the shoulder on the
  way, while each joint eases from the ragdoll's own angle to the clip's; the clip is held on its first pose until the roll is done. A
  half turn takes 0.4 s in play and 1.7 s in the entrance (`GetUpPace.HalfTurnSeconds`).
- **The rise to standing was a pop.** In play the clip was played evenly in 1.8 s, the rise in a fifth of a second. `GetUpPace` now
  places the clip itself (the Animator is held still): the quick get-up skips the clip's first second of shifting about and gives the
  push-up, the knees, the feet and the rise each a share, the rise over twice what it had. Control still comes back 1.18 s after the
  body is on its front. The entrance plays the clip as it is.
- **The ground was taken to be level** at the height of the root. Now (`Ragdoll.HoldOnTheGround`, each frame of the get-up, the ground
  looked for under each point, the body itself looked through): while the clip lies, the whole pose is tipped to the slope of the ground
  along and across the body; as it comes onto its feet the hips go down to the lower foot's ground; and `LimbFloorClamp` moves each hand
  and foot by as much as the ground under it is above or below the clip's floor while the clip has it on or near that floor
  (`LimbFloorClamp.Follows`), and keeps it and its elbow or knee out of the ground there.

Tests: `GetUpPaceTests`, `GetUpOnTheGroundPlayTests` (the roll from its back about its own length; on stairs hands on the steps above,
nothing in a step; lying along a slope), `RagdollStandUpPlayTests`, `GetUpFloorPlayTests`.

Not done: a get-up of its own from the back (sitting up) and from the side; the roll onto the front stands in for both.

## Flat on its back, flailing in the wind (2026-10-22)

Owner: "make it so you are laying flat horizontal during the fall cutscene at the beginning instead of vertical. and make some amount of
flailing in the air like there is wind, so he isnt just still during the whole fall". The limp body used to fall the way it was let go:
upright, limbs hanging.

- **On its back** (written when the get-up clip was taken to start so; it starts face down, and the get-up rolls the body over first: see the stand-up above) and with its face to the
  camera overhead. `FallFlail.Righting` is a damped spring (22 per radian, 6 per second) from the way the chest faces (`FallFlail.Front`:
  from the spine and the shoulders) round to the sky. It turns the WHOLE body as one about its middle, every part's spin and the speed that
  turn gives it where it is: turning the trunk alone fought the joints and the gusts on the limbs tumbled it (measured: the chest 96
  degrees off on average, never flat). It is still let go standing, for the HUD portrait, and tips over onto its back in under a second.
- **The wind** (`FallFlail.Wind`): on each forearm and shin (half as much on the upper arm and thigh) a lift and a gust that wanders every
  way (Perlin noise, 1.3 a second, each limb its own), growing with the fall's speed up to 25 m/s.
- Both are changes to the bodies' speeds made each frame, like the drift's, so they hold at any frame rate.

Measured over the fall (`IntroCutsceneFallPlayTests.TheBodyFallsFlatOnItsBackWithItsArmsAndLegsThrownAbout`): the chest within 30 degrees
of the sky for more than 80% of it, the hand and the foot moving well over 30 and 20 cm about the hips. `FallFlailTests` holds the maths.
Other players still see the body's transform, not its limp pose (docs/MULTIPLAYER_STATE.md, not synced yet).

## Out of the black (2026-11-10)

A new character is now made on a conveyor belt in the sky and dropped off its end (docs/BELT_INTRO.md); the screen goes black as it
falls, the world loads behind the black, and this fall comes up out of it over 1.2 s as the body is let go (`ScreenBlack.FadeTo`,
`IntroCutscene.FadeInSeconds`). A world entered with no entrance to play clears the black in half a second (`IntroCutsceneStarter`).

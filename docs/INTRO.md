# The intro: falling from the sky

A new character's entrance (`Character/Story/IntroCutscene.cs`, started by `IntroCutsceneStarter`). Rebuilt 2026-10-20 on the owner's
asks: "make it so you are ragdolling falling from the sky instead of an animation and make the fall impact as a ragdoll as well", "there
is a jump cut in the cutscene mid fall right before the impact", "it does like a rope climbing animation as he is standing up", "make
it so there is some fog but it fades as you get closer to the surface like you are falling through the clouds", "make it so your feet
are on the ground when you take over control", "make sure the hands arent clipping through the ground or any body parts".

## The beats

1. **Hanging** (6 frames, `PortraitCaptureDelayFrames`): the body waits 300 m up, standing, so the HUD portrait is taken from an
   upright head.
2. **Falling**: `Ragdoll.Activate`. While falling, each body part gets air drag (`FallDrag` 0.28, about 35 m/s at most, about 10 s of
   fall) and continuous collision, and both are put back on landing. WASD or the left stick drift the body sideways, up to 10 m from
   the spawn.
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
- The cloud is thick fog (4 to 30 m) above 250 m and the scene's own fog below 80 m, eased in between.
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

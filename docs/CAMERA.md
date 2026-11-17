# The follow camera

`Character/Player/ThirdPersonCamera.cs`: an orbit camera behind the player, with a spring arm that pulls in when a wall is between the camera
and the body. It started as a copy of player.gd's camera (spring length 4.0 m, pivot at 0.73 of the body's height, 0.5 m over the right
shoulder, a 75 degree field of view).

## 2026-10-17: framed like an action game

> "i feel like the camera pov needs to be adjusted so i can see more of whats going on in front of me, look at other third person
> action/fighting games and make the pov more like the common game"

**What was wrong.** The camera looked level at the pivot, the chest, from 4 m back. That put the player's own head and shoulders in the
middle of the screen, which is exactly where somebody standing in front of you is. In the renders (`CameraViewCapture`), a swordsman 6.5 m
down the street, and one in sword reach, were both hidden behind the player's head.

**What action games do.** They raise the camera above the head and let the body sit low in the frame, often a little to one side (the rule of
thirds). The view then reaches over the character to what is ahead:

- The Souls games and Assassin's Creed sit the camera above the character.
- Cinemachine's third-person rig has a "vertical arm" for this.
- Some games also add look-ahead, putting the focus point a few metres ahead of a running player.
- God of War (2018) went close over the shoulder instead, and its own combat designers had wanted it further back. The close camera was
  divisive for fights.

**What the camera does now.**

| | Before | Now |
|---|---|---|
| Camera height over the pivot (`FramingHeight`, in the view's own up) | 0 | **1.0 m** (1.3 at first; the owner chose 1.0 after playing it) |
| Distance back (`Distance`) | 4.0 m | **4.8 m** |
| Over the right shoulder (`ShoulderOffset`) | 0.5 m | 0.5 m |
| Field of view (vertical) | 75 | 75 |

- **Looking level,** the body now sits in the lower half of the screen. The top of the head is just below the middle and the feet are near
  the bottom. Whoever stands in front is seen over the head.
- **The height is applied in the view's own up,** so the body keeps the same place on screen at any pitch.
- **Aiming a gun is unchanged.** It eases from this framing down to its own over-the-shoulder place: `AimDistance` 2.3 m, `AimShoulderOffset`
  1.0 m and `AimPivotRaise` 0.2 m.
- **Grow and Shrink** scale every distance with the body.
- **Lock-on** still turns the camera toward the target from the pivot. Seen from above, the target sits a little below the middle and the
  player lower still.

Placement: `camera = pivot + R(pitch, yaw) * (shoulder, FramingHeight * (1 - aim), 0) - R * forward * distance`. The spring arm casts from
the pivot (inside the body's capsule) toward that point, so a low ceiling or a wall pulls the camera in along the same line. Indoors, under a
ceiling about a storey up, the raised camera meets the ceiling sooner than the old level one did and comes in closer.

**Tuning live:** `/camera` shows the numbers. Each of these sets one, for the session only:

- `/camera height <m>`
- `/camera distance <m>`
- `/camera shoulder <m>`
- `/camera fov <degrees>`

`/camera reset` goes back to the defaults. A setting that feels better should be reported, so it can become the default.

The scene's camera is generated (`CityMassingSceneBuilder`). A changed default needs the scene rebuilt. On 2026-10-17 the rebuilt scene
differed from the old one in that one component only: `FramingHeight` 1.3, `Distance` 4.8. The same day the owner set the height to 1.0
("set the camera height to 1"); the rebuilt scene changed in that value only.

## 2026-11-10: through the Wraith

Owner: "make the ingame camera move through the wraith as if it were air, not an object that would switch the camera angle if it blocks the
view". The camera comes in close in front of anything between it and the body, but not in front of something marked
`ICameraPassesThrough` (on its component; its colliders and its children's count as it). The Wraith is, its summoned kind too; any other
creature can be given the same in one line. Test: `ThirdPersonCameraPlayTests.TheCameraGoesThroughTheWraithButNotThroughAWall`.

## Tests and renders

- `ThirdPersonCameraPlayTests`:
  - `TheHeadSitsAtTheMiddleTheBodyFillsTheLowerHalfAndWhoeverIsAheadIsSeenOverIt` (the Saints Row framing since 2026-10-23; until then
    `LookingLevelTheBodySitsLowInTheFrame...`, the 2026-10-17 framing).
  - `AimingBringsTheCameraInCloseOverTheShoulder`, `AimingSnapsInAndEasesBackOut` and `SprintingWidensTheFieldOfViewATenthAndAimingOverridesIt`
    cover aiming and sprinting, plus the older shoulder, spring-arm, ragdoll and recoil tests.
- `DevConsoleViewTests.TheCameraCommandTunesTheFollowCameraAndResetPutsItBack` covers `/camera`.
- `CameraViewCapture` (Explicit, `-Graphics`) renders the game's own camera in the real city, squared up to a swordsman, with the 2026-10-17
  framing and the current one. It writes `Logs/CityShots/camera-compare-start.png`, `-level` and `-close`, each with the old view above the
  new, and `camera-sr3-aim.png`.

## Sources

- Game Developer, [The foundation of a third-person camera](https://www.gamedeveloper.com/programming/the-foundation-of-a-third-person-camera) (pivot offsets, pans, rule of thirds)
- Moonjump, [Third-person camera systems](https://moonjump.com/game-dev-mechanics-third-person-camera-systems-how-it-works/) (look-at above the target, pitch limits, look-ahead)
- Unity, [Cinemachine Third Person Follow](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineThirdPersonFollow.html) (shoulder offset and vertical arm)
- ActualidadGadget, [God of War's camera position](https://www.actualidadgadget.com/en/god-of-war-ps4-was-the-subject-of-discussions-development-the-position-the-camera/) and eXputer, [on the God of War camera](https://exputer.com/features/marvels-wolverine-god-of-war-camera-unfitting/)

## 2026-11-03: the pistol's own aim framing

Owner: "the pov when shooting the pistol doesnt show enough of the gun its covered by the body look at how other third person shooters handle this". A barrel
points where the camera looks, so its screen length comes only from how far it is off to the side for how far away it is: from 3.9 m the Glock showed its
back, 4% of the screen wide, 38% of it seen past the head. GTA V's third-person aim (gameplay footage) has the camera close, the head and shoulder cut off at the lower left
and the gun clear beside them. `ThirdPersonCamera.PistolAim` (set by `PlayerInputReader` from `PlayerLocomotion.HoldsPistol`): 2.4 m back, 1.4 m to the
side, pivot raised 0.3 m, field of view 40. **This undoes for the pistol the metre the owner added to the aim distance on 2026-10-24; every other gun keeps
3.9 m / 1.3 m / 30.** The barrel still points at the crosshair's point, on purpose: yawing it to show more of its side would part it from the tracer.

## 2026-10-23: Saints Row: The Third's camera, copied

> "our third person pov camera needs work research the saints row the third camera pov and zoom and just copy that"

**Where the numbers come from.** Saints Row's camera lives in `camera_free.xtbl`, one entry per situation ("exterior close" on foot, "sprint",
"fine aim", "crouch", "interior close"...), each with a look-at offset (where the camera orbits, metres above the feet), `z_dist` (how far
back), `x_shift` (how far to the side: the character sits left of the middle, the camera over the right shoulder), `y_dist`, `base_fov` and a
`blend_time`. The modding threads quote the stock values they changed: on foot `x_shift` 0.6, `y_dist` 0.05, `base_fov` 50, elevation limits
-75/55; interior `z_dist` 3, look-at 1.8; fine aim `z_dist` 2.0, `y_dist` 0.3, look-at 1.9, `base_fov` 30, `blend_time` 0.15 (0.5 going back
out). The first-person mod mirrors the game's sprint with a field of view 10% wider. Frames of the game (free roam on foot, hip fire, fine
aim) were measured to settle what the threads leave out: on foot the camera is about 3.5 m back, the character fills over half the screen's
height with the top of the head at the middle and the feet below the bottom edge, 10-15% left of the middle; in fine aim the camera is in
close over the right shoulder, the screen's middle at the shoulder, the head and torso on the left third, the view zoomed well in.

**Scaled to this body.** Saints Row's character is about 1.8 m; ours is 2.6 m, so every distance is 1.44x.

| | Saints Row (1.8 m body) | Here, before | **Here, now** |
|---|---|---|---|
| Field of view on foot (vertical) | 50 | 75 | **50** |
| Distance back | 3.5 | 4.8 | **6.0** (5.0 copied; the owner set 6 on 2026-10-24) |
| Height of the camera's line over the pivot (`FramingHeight`) | look-at at the top of the head | 1.0 (head below the middle) | **0.6** (the camera's line at the top of the head) |
| To the right of the spine (`ShoulderOffset`) | 0.6 | 0.5 | **0.87** |
| Fine aim: field of view | 30 | 50 | **30** |
| Fine aim: distance | 2.0 | 2.3 | **3.9** (2.9 copied; a metre further with the distance, 2026-10-24) |
| Fine aim: to the side | 0.6 (0.75 measured) | 1.0 | **1.3** (0.75 hid the Glock behind our bigger head; 1.3 keeps the angle past the head at 3.9 m back) |
| Fine aim: the camera's line | the shoulders (1.55) | the chest + 0.2 | **the chest + 0.35** |
| Into fine aim / back out | 0.15 s / 0.5 s | 0.25 s / 0.25 s | **0.15 s / 0.5 s** |
| Mouse-look while aiming | slower | 0.75x | **0.6x** (30/50, so a pixel moves the same on screen) |
| Sprinting | own entry | nothing | **field of view +10%** over 0.5 s |

**What it looks like.** The view is a lot less wide-angle (buildings stop splaying out at the edges, the far end of a street reads bigger),
the body is bigger on screen and further to the left, and fine aim is a real zoom: 1.7x, in close over the shoulder, snapping in and easing
back out. Sprinting widens the view a touch, as the game does. The spring arm, the lock-on and the ragdoll follow are unchanged; the look
limits stay at the full +/-90 the owner asked for. `/camera` tunes the same four numbers live; `/camera reset` puts these back.

Renders: `CameraViewCapture` now compares the 2026-10-17 framing (top) with this one (bottom) at the start pitch, level and in sword reach,
and adds `camera-sr3-aim.png` for fine aim. Tests: `ThirdPersonCameraPlayTests` (the head at the middle and the body filling the lower
half, aiming in over the shoulder with the zoom, in fast and out slower, sprinting widening the view).

## Indoors (2026-11-16)

Owner: "is there any way we can improve the indoor cam, how do third person games do it and can ours be improved upon", then "ok go ahead try to make it
better". The camera only had the spring arm: a wall between body and camera pulled it in within the frame and let it out the same way, so indoors it
popped in and out at every door frame, hung at the street's 6 m, and could end up in the next room looking back through a doorway.

`CameraRoom` (pure maths) and `ThirdPersonCamera`, as third-person games do it (Unreal's spring arm, Cinemachine's deoccluder, Sifu's and God of War's
close cameras):
- **In a room the shot tightens.** Each frame a ray straight up (a roof within 4.5 m of the pivot) and eight round it (a wall within 6 m) say how enclosed
  the body is: a roof and walls on 4 of 8 sides is a room, a roof with at most one is open air (a porch, under a footbridge), a street with no roof never
  is. In a room the camera comes in to `IndoorDistance` (3.6 m), lower (`IndoorFramingHeight` 0.3) and 6 degrees wider, eased in over half a second and
  out over most of a second. All on the camera's Inspector under Indoors.
- **The arm snaps in and eases out.** A wall still pulls the camera in within the frame; once it is gone the camera drifts back at
  `ArmOutMetresPerSecond` (5) instead of jumping. A longer wanted distance with nothing in the way (an aim let go, a giant growing) is followed as it is.
- Probes ignore the body, every damageable body and what the camera passes through. A teleport starts both again where they stand.

`IndoorCameraPlayTests` (Old Guy's den is a room, the camera in it is in the room, the open yard is not and the way out does not jump), `CameraRoomTests`;
`IndoorCameraCapture` (Explicit, -Graphics) renders the house before and after at five places. Not done yet: fading the body when the camera is pressed
close, and side probes that see a corner coming.

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
| Camera height over the pivot (`FramingHeight`, in the view's own up) | 0 | **1.3 m** |
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
differed from the old one in that one component only: `FramingHeight` 1.3, `Distance` 4.8.

## Tests and renders

- `ThirdPersonCameraPlayTests`:
  - `LookingLevelTheBodySitsLowInTheFrameAndWhoeverIsAheadIsSeenOverIt` fails with the old framing, where the head is above the middle of
    the screen.
  - `AimingBringsTheCameraDownFromOverTheHeadToItsOwnPlace` covers aiming, plus the older shoulder, spring-arm, ragdoll and recoil tests.
- `DevConsoleViewTests.TheCameraCommandTunesTheFollowCameraAndResetPutsItBack` covers `/camera`.
- `CameraViewCapture` (Explicit, `-Graphics`) renders the game's own camera in the real city, squared up to a swordsman, with the old framing
  and the new one. It writes `Logs/CityShots/camera-compare-start.png`, `-level` and `-close`, each with the old view above the new.

## Sources

- Game Developer, [The foundation of a third-person camera](https://www.gamedeveloper.com/programming/the-foundation-of-a-third-person-camera) (pivot offsets, pans, rule of thirds)
- Moonjump, [Third-person camera systems](https://moonjump.com/game-dev-mechanics-third-person-camera-systems-how-it-works/) (look-at above the target, pitch limits, look-ahead)
- Unity, [Cinemachine Third Person Follow](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineThirdPersonFollow.html) (shoulder offset and vertical arm)
- ActualidadGadget, [God of War's camera position](https://www.actualidadgadget.com/en/god-of-war-ps4-was-the-subject-of-discussions-development-the-position-the-camera/) and eXputer, [on the God of War camera](https://exputer.com/features/marvels-wolverine-god-of-war-camera-unfitting/)

# Climbing: on purpose, on real walls

## What was wrong

Wall climbing (`PlayerClimbing`) grabbed the instant Shift and W were held with ANY surface within a metre at
chest height, and the airborne sticky-feet ability (`WallStick`) did the same for Shift in the air. Shift+W is how
the player runs. So it fired on the wall at the top of a staircase, the frame beside a window you were jumping
through, and the jamb of a doorway. The grab also left the body hanging wherever it was when it grabbed (up to a
metre off the wall), facing whichever way it happened to be facing.

## What the research said

- **Assassin's Creed** starts a climb only while a dedicated parkour-up button is HELD. Ubisoft's own design
  note for Shadows: holding it "like a gas pedal" is not the way to play; crouch/stance are separate buttons so
  the wrong action cannot fire; only small vaults are automatic while sprinting.
- **Unity ledge/wall systems** validate in stages and reject at every stage: a horizontal check with several
  rays plus an overlap check, then vertical checks, then the surface angle (too steep or too shallow is
  discarded), so odd geometry never passes. (WeaverDev's ledge-grab write-up is the clearest.)

## What it does now

1. **Intent** (`Character/Interaction/GrabIntent.cs`). Space held for 0.2 s is a grab; a tap is a jump. Needs W held toward
   the wall. One hold grabs once (release to grab again), and a 0.6 s cooldown after any grab ends, so a hop-off is
   not undone by the next frame. Not available with a jetpack or parachute on the back (Space means thrust or
   canopy there).
2. **A real wall** (`Character/Movement/WallProbe.cs`). Nine rays (three across, at knee, chest and head height) must ALL
   hit, on one flat face (hit distances within 0.3 m, normals within 20 degrees), near-vertical, within 45 degrees
   of where the player is facing, and not a person, a door or flying glass. A doorway, a window opening, a jamb, a
   stair, a low fence and an outside corner each have a gap or a second face in that grid. On a real streamed
   building: 69 of 71 plain-wall spots accepted, all 3 doorway spots refused.
3. **The grab itself**: the body is drawn onto the wall face (it used to hang off it) and turns to look at it.

Sticky feet (`WallStick`) uses the same intent and the same probe.

## Controls now

| | |
|---|---|
| Jump | tap Space |
| Climb a wall | hold Space + W facing it (a tall, wide wall) |
| Climb up / down | W / S |
| Hop off | tap Space again (after letting go) |
| Sticky feet | hold Space in the air + Shift, facing a wall |

**Climbing is a skill since 2026-10-14** (the Freerunner tree's Climb, docs/SKILLS.md): without it no wall is grabbed, so no climbing, no pulling
up onto ledges (owner: "pulling yourself up onto a ledge naturally is apart of climb") and no climbing in through high windows. Sticky feet is
Wall Stick, above Climb. Vaulting is everyone's. A body with no character (the editor's play button, the tests) knows Climb and Wall Stick.

Ladders are their own thing (2026-11-09, `PlayerLadder`, docs/SEWERS.md): anyone climbs one, skill or not, with no stamina; walk into its
foot or press E. A ladder's top is climbed out of with the wall climb's own pull-up (`PlayerClimbing.MantleOnto`).

## Tests

`WallGrabIntentTests` (fixtures: doorway, jamb, window, stairs, fence, corner, door, angle, intent),
`RealCityWallGrabPlayTests` (the real facade and doorway), `RealInputClimbingPlayTests` (real keyboard: Shift+W
never climbs, a tap jumps, hold-Space-and-W climbs).

## Update: ledges and the mantle (the "stuck in the wall" bug)

**What was wrong.** The top of a climb was "the wall stopped": one ray, 1.8 m above the hands, stopped hitting the wall. A window opening, recess or balcony at that
height looked like the top, so the body was moved up 2.2 m and 0.6 m INTO the wall (`-wallNormal * MantleForward` points into it) and stuck. At a real
edge the clip played, then the body teleported to a fixed offset.

**Research** (Unity ledge/parkour write-ups, Assassin's Creed's annotated ledges, the Dynamic Parkour System's feature list, Unity's MatchTarget):
validate a ledge in stages and refuse every look-alike; a mantle authored for one height looks wrong on any other, so use **motion warping** (drive
the body along a path computed from the real ledge) and put the **hands on the ledge** (IK).

**What it does now.**
- `LedgeProbe` finds a real ledge: the wall face at hand height, then a grid of top probes behind it (2 depths x 3 across) that must ALL find an upward-facing
  surface agreeing on height to 12 cm (flat, wide, deep: not a pole, a slope, a sill), the face just under it must be the same face (not a set-back shelf),
  and a capsule at the standing spot must be free (room to stand). Only then is it a ledge.
- Climbing up mantles only at a validated ledge within reach of the hands. With no ledge and no wall left at head height the body holds its grip rather than
  climbing into thin air. A **stuck guard** lets go if it is asked to climb but is not rising for 0.8 s.
- The **mantle** (`PlayerClimbing.MantlePath`, 1.2 s, the controller off while it runs): 60% pulls straight up the wall to the ledge's height (never toward the
  wall first), 40% steps over the lip onto the validated standing spot with a small lift; the clip is fitted to that length; then the controller is back on, the
  feet are snapped to the real surface and the animator is handed to Locomotion.
- **Hands on the ledge** (`PlayerLocomotion.SetLedgeHands`, two-bone IK after the Animator): both palms go on the lip as it comes within reach and stay through the
  pull-up.
- **Control drops** off the wall (no push); Space still hops off outward. (The Dynamic Parkour System uses C for drop and slide: moved to Control here. Slide
  is not built.)

Tests: `LedgeProbeTests` (a plain top, a tall wall, a window gap, a pole, a slope, a ceiling, a set-back shelf, the path never going toward the wall before it is up),
`LedgeMantlePlayTests` (real rig: mantles onto a real edge and stands in free space; a gap in the wall is never an edge and never leaves the body in it; Control
drops). `LedgeMantleCapture` (Explicit, -Graphics) renders it to `Logs/CityShots/mantle-*.png`.
Not done: shimmy along a ledge, ledge-to-ledge jumps, corner turns, drainpipes, a dedicated hang clip (the existing climb clip is reused), and the climbing
state is still not replicated to other players.


## Update: the vault

**Use it:** run (Shift + W) at a waist-to-chest-high obstacle with a flat, thin top (a fence, a low wall, a car, a stack of crates) and the player goes over it and
lands on the far side. Running past something or walking into it does nothing; holding Space at a wall is still the climb.

- `VaultProbe` validates in stages, like `LedgeProbe`: a face at waist height that the player is facing (not a graze); a flat top just behind it, inside the band
  0.30 to 0.68 of standing height (lower is a step, higher is a climb); the top must END within 1.1 m (a building is not vaulted); ground within 1.6 m below
  past the far edge with a standing capsule's room, and open space over the far edge (no rail just beyond).
- `PlayerVaulting` drives the body along `Path` (motion warping, 0.7 s, controller off while it runs): up to 12 cm over the top first, travel across, drop to the
  landing; the body leans 35 degrees into it about the hips and the hands reach for the top. Needs 3 m/s of running speed.
- Wired in `PlayerInputReader` after the Space-hold grab check, so a grab wins. There is no vault clip in the project, so the legs hang while the body leans:
  an authored vault clip would improve the look; the path and hands would not change.
- Multiplayer: the body's position is what other players see (owner-authoritative movement), so they see it go over; the hand IK and the lean are local.
- Tests: `VaultProbeTests` (a fence, a kerb, a head-high wall, a building-sized block, a boxed-in gap, facing away, a pole, feet always above the obstacle while over it),
  `VaultPlayTests` (real rig: over a fence to the far side with the controller on; no vault when walking, not moving forward or airborne), `VaultCapture` (Explicit, -Graphics).
- Not done: slide (the Dynamic Parkour System's C; would be Control), vault-on-jump-tap, kong/speed vault variants, a clip.

## Fix: floors of inner storeys read as ledges

Climbing a real facade mantled at the floor of an inner storey and carried the body through the outer wall (reproduced on the real city before the fix:
began the mantle at y=22.6 on a building 50 m tall). The top probes behind the wall face start inside the wall's thickness, so they found the interior floor slab.
`LedgeProbe` now also requires the way over the lip to be OPEN: level rays from just outside the lip to the standing spot, at four heights up the body, must
hit nothing (a real ledge has nothing above its lip; an inner floor has the wall above it). Tests: `LedgeProbeTests.TheFloorOfAStoreyInside...`,
`RealCityClimbTopOutPlayTests` (six real facade climbs: never a mantle below the roof, never inside geometry). A climb up a facade now ends at the first
window or recess instead (it lets go), it does not top out through the wall.

Also: running or diving into an intact window no longer breaks it (windows break only from a punch, a bullet, a weapon, a thrown bottle; a cracked pane gives way to a knock).

## Update: roof parapets, limbs kept out of the wall, stamina only while moving

- **Roof edges climb again.** The open-path fix above refused real roof edges, which are PARAPETS (a thin top, with the roof lower behind it). `LedgeProbe` now also
  accepts a thin-topped ledge (a flat top at least a hand's width deep, up to 0.9 m, that ENDS, with standable ground behind it, up to 4.5 m lower) and a cornice
  that juts out up to 0.5 m past the wall (the body moves out clear of it, then rises). The mantle ends over the far edge; behind a tall parapet it stops 0.8 m
  below the top and the controller drops the rest. Hands reach overhead: mantle reach 1.2 x standing height. On the real city, facade climbs that end at a plain
  parapet now top out onto the roof. A climb that ends under a cornice that has a taller wall on top still just holds its grip (nothing to mantle onto).
- **Limbs can't go through the wall** (`PlayerLocomotion.ApplyClimbWallLimbs`): after the Animator, any hand, foot, elbow or knee that the climb clip puts behind the
  wall plane is moved out to just in front of it and its limb is solved to reach there (two-bone IK), so hands and feet touch the wall and never go through it.
  Before: a hand 0.37 m through the wall in the test; after: nothing behind it (`ClimbLimbsPlayTests`).
- **Stamina drains only while moving** up or down (or sliding on a wet wall). Hanging still costs nothing and does not regenerate either.

## Update: automatic vault and automatic climb (run into it)

**Vault.** Holding run (Shift) + W at a vaultable obstacle now just vaults it, with no speed requirement (standing against it counts). It used to work only "sometimes" because
(1) the face was looked for with ONE ray at waist height, so anything below the waist (a knee-high planter) had no face at that height, (2) the top had to be under 0.68 x
height, (3) it had to be square on, and (4) it needed 3 m/s of running speed, which a player pressed against the obstacle does not have. Now the face rays go at four heights
(knee to chest) in a 3-ray fan, the vaultable band is 0.22 to 0.8 x height (0.57 to 2.08 m on the 2.6 m player: it vaults a 1.9 m roadside box), up to 1.6 m thick, up to 60
degrees off square, landing up to 2.6 m below the top. A taller obstacle takes up to 0.3 s longer.

**Climb.** Holding run (Shift) + W into a tall wall now starts the climb by itself (holding Space at a wall still grabs from anywhere). It is a vault first, a climb second, and it
is guarded so ordinary running does not trigger it (`PlayerClimbing.TryStartAuto`):
- the wall must pass `WallProbe` (wide, tall, flat, no gaps: a doorway or window opening fails) and be faced squarely, within 30 degrees (brushing along it does nothing);
- it must be pressed into for 0.2 s;
- **stairs**: if the standing height has been rising faster than 0.8 m/s (a staircase or a steep ramp), no automatic climb for a second, AND the run keys must be let go and pressed
  again, so running up a flight and on into the wall at its top never turns into a climb;
- **doors**: none within 3 m of a door;
- grounded only, and after any climb ends the run keys must be released before the next automatic one.
Tests: `PlayerClimbingTests` (dwell, no run key, stairs, a door nearby, grazing, release after a drop), `RealInputClimbingPlayTests` (real keyboard: Shift+W climbs a tall wall;
not beside a door; not up a 14-step staircase into the wall at its top; Shift+W at a 1.9 m box vaults it), `VaultProbeTests` (a knee-high obstacle, a 1.9 m box, an angled approach).

## Update: the vault no longer depends on where you look

"It only vaults when I look right at the edge": the probe used thin rays along the BODY's facing, so a narrow obstacle was found only when aimed at, and the body's facing lags the camera. Now `PlayerInputReader` hands
the vault and the auto-climb the direction the player is trying to go (camera yaw + WASD), and `VaultProbe` finds the face with body-wide SPHERE casts in a 5-way fan (0, +-30, +-55 degrees) at four heights, and
looks for the top at the hit point and to either side of it (a corner clip hits the edge of a narrow box, not its middle). Tests: `ANarrowBoxIsVaultedEvenWhenRunningAtItsEdge` (0.8 m wide box from 0.45 m either side),
`AnObstacleBeingRunAtNotQuiteHeadOnIsStillVaulted` (+-50 degrees).

## Update: procedural climbing (hands and feet on the wall, stepping in pairs)

Reported: "the climbing animation looks like shit, the limbs keep bending weirdly; it has always looked bad: find a better climb animation like Breath of the Wild".
The climb was one clip (both arms waving over the head, the legs bent wherever it liked) with every limb then shoved off the wall by a clamp: bent the wrong way, touching nothing, and
the same every frame (no climbing motion at all).

No free climbing clip was fetched (a download needs permission; see the end of this note); what Breath of the Wild shows is not a clip but PLANTED LIMBS: the hands and feet hold the wall, the body moves
up past them, and a limb that has been left behind takes a quick step to a new hold, the diagonal pairs taking turns. That is built procedurally, so it works at any height and speed, down as well as up:

- `Character/Movement/ClimbLimbStepper.cs` (pure, tested): four anchors on the wall surface; a limb steps (0.13 s, a small lift off the wall) when it is more than 0.30 of its length behind its ideal hold; only
  one diagonal pair (left hand + right foot, or right hand + left foot) steps at a time, so the other carries the weight, and the two hands never let go together; holds are spread wider than the shoulders and
  hips, as a climber's are; each is snapped onto the real surface (a raycast) a few centimetres off it; no hold is ever inside the wall.
- `PlayerLocomotion.ApplyClimbLimbs` (LateUpdate, after the Animator): puts each hand and foot on its target with two-bone IK (elbows out and down, knees out and away from the wall), leans the chest into the
  wall and turns it a little toward whichever hand reaches. The old clamp (`ApplyClimbWallLimbs`) stays as a safety net and now does nothing in normal climbing.
- Other players' copies run the same stepper from the wall plane and `Climbing` flag they already receive, driven by the (smoothed) movement of the copy's body.
- Tests: `ClimbLimbStepperTests` (steps again and again up and down, nothing out of reach, every hold on the wall, one pair at a time, holds do not move while holding, the pairs alternate),
  `ClimbLimbsPlayTests` (no limb through the wall on the real rig), `ClimbCapture` (Explicit, -Graphics: `Logs/CityShots/climb-sheet*.png`; it sets `Time.captureFramerate = 60` because the limb
  steps run in real frame time).
- Still not BotW: it climbs only straight up and down (no sideways), the feet are not turned flat to the wall, and the pull-up over a ledge still uses the old clip.

## Update: leaning round courses and cornices (2026-10-09)

Facades got real relief (`World/FacadeOrnament.cs`: a base course over the shop fronts, string courses, cornices up to 0.85 m deep). The first full PlayMode run after
it showed every real-city facade climb letting go a metre up: the body rose into the underside of the base course, stopped rising, and the stuck guard released it.

- `Character/Movement/ClimbJut.cs`: one box sweep per frame, a column as wide as the body from just under the feet to 0.5 m over the head (down past the feet when climbing
  down), swept in toward the wall: the deepest thing jutting out past the plane the climb holds. `LeanFor`: lean out by all of it, up to 0.9 m (`MaxLean`, deeper than
  the deepest cornice); anything deeper (a balcony) is not leaned round. `PlayerClimbing.Tick` adds the lean to the hang point, so the body swings out ahead of the
  ledge and settles back once its feet are past. The head-height "is there wall left" check reaches as far again as the lean.
- The roof edge: the cornice now CAPS the parapet (its top flush with the coping, `CityMeshBuilder.AddWallOrnament`), so the edge is one flat top. A cornice a step below
  the coping was two tops at two heights and `LedgeProbe` refused it. `LedgeProbe.MaxOverhangMetres` is now `ClimbJut.MaxLean` (was 0.5), `MaxThinDepthMetres` 1.3
  (cornice + parapet + coping), and the thin-ledge face ray reaches past a leaning body.
- A deep cornice or a balcony partway up is a ledge like any other: the climb may pull up and stand on it.
- Tests: `ClimbJutTests`, `ClimbPastJutPlayTests` (a 0.24 m course climbed past with the body back on the wall after, a 0.85 m cornice climbed past or stood on, a
  1.4 m balcony not leaned round and never passed through), and `RealCityClimbTopOutPlayTests` again tops out on all six spots of a real ornamented facade.

## 2026-10-12: the probe looks past the player's own body

`WallProbe.TryFind` takes the probing body (`self`) and skips its colliders. Running, the swung-forward leg sat in front of the
knee-height ray; its ragdoll collider was read as "a person, not a wall", so Shift+W into a wall failed or worked depending on the
stride (`RealInputClimbingPlayTests` failed only in full runs). All three callers (grab, run-up climb, wall stick) pass their body.
`WallGrabIntentTests.ThePlayersOwnLegInFrontOfTheRayIsLookedPast` guards it.

## Update: the climb that looks like a climber, and sideways (2026-10-13)

Owner: "the climbing i want to keep like breath of the wild but update that animation because it looks awful, also allow sideways climbing with A & D
and make an animation that looks good for that." A render of the old climb (`ClimbCapture`) showed why: the hands were held 0.62 arm lengths over the
shoulders, which on this rig is a straight arm over the head, the feet hung half a leg straight down with the toes pointing at the ground, and the
body barely changed from frame to frame: a mannequin hung on a hook.

What Breath of the Wild does (New Frame Plus's breakdown of Link's climb, a developer's rebuild of it in Unreal): about eight directional loops (up,
down, left, right, the diagonals) blended by the way the stick points, IK keeping the hands and feet on the surface, a quick settle into an idle when
the stick is let go, the body following the wall's slope. The limbs here are already procedural, so the "loops" are the stepper's hold geometry:

- **Holds a climber could use** (`ClimbLimbStepper`): each limb has a REST hold (a hand 0.22 arm lengths above its shoulder, about chin height, and
  0.26 out; a foot 0.42 leg lengths below its hip and 0.30 out) plus a REACH along the direction of travel (a hand 0.42 arm lengths going up, 0.32
  sideways, 0.18 going down; a foot 0.26 going up or sideways, 0.34 going down). Going up, the hands reach for holds the arm can only get to bent and the feet step up beside the
  hips, a frog's push; going down the feet lead and the hands follow at shoulder height; sideways the leading hand and foot reach out along the wall and
  the trailing ones follow, and the hands never cross.
- **Steps along the travel**: a limb steps when its hold has fallen 0.36 (hands) / 0.34 (feet) limb lengths behind its ideal ALONG THE DIRECTION OF
  TRAVEL, whichever direction that is, so a change of direction re-plants the holds one diagonal pair at a time. Hanging still, nothing steps (BotW's
  idle).
- **Soles flat on the wall**, toes down: the ankle's hold is a foot's height (0.145 m) off the face and the foot bone is turned sole-to-wall after the
  leg solve (`PlayerLocomotion.ApplyClimbLimbs`).
- **The head looks where the climb is going** (up 14 degrees, down 10, along the wall 25 when shuffling), the chest leans into the wall as before and tips
  6 degrees toward the way it is shuffling.
- **Sideways** (`PlayerClimbing.Tick`, A and D): the hang point shuffles along the wall face at 1.8 m/s (climbing is 2.5) as long as the SAME face is
  found at chest and knee height 0.45 m past the body's edge on that side; at the wall's end or a corner the shuffle stops and the grip holds. Blocked
  (a pipe, another climber), the hang point comes back to where the body really is, so letting go of the key never leaves the body lurching. Diagonals
  (W + D) work. The clip is always told up or down and is frozen through `ClimbSpeedScale` when holding still (as character.gd's hold_climb did), played
  while shuffling.
- **Other players** see it: the travel (along, up) goes in the climbing motion message (`NetworkedAnimatorSync`) and the copy's stepper walks the same way.

Tests: `ClimbLimbStepperTests` (shuffling sideways every limb keeps stepping and nothing crosses; diagonals; the arms stay bent and the feet push from
beside the hips; climbing down the feet lead; a change of direction re-plants one pair at a time), `SidewaysClimbPlayTests` (real rig: D moves the body
1.2 m+ along the wall in a second at the same height, A brings it back; the shuffle stops at a 4 m wall's end; the limbs keep stepping and stay on the
wall), `ClimbCapture` now also renders `climb-sheet-sideways.png`.

Not done: a dedicated hang/idle sway; the hands' palms are not turned flat on the wall (the wrists stay straight, fingers along the forearm up the wall);
no clip is used for the limbs at all.

## Update: vaulting into a broken window (2026-10-13)

Owner: "right now you cant vault into a window which you should be able to if its been shattered or broken completely." The city's ground-floor windows
come in two kinds: the door wall's panes start 16 cm up (a step: a shattered one is simply walked into) and the others' sills are at 0.4 to 1.1 m
(Ribbon 0.38, Punched 0.9, HighBand 1.12 on a 3.2 m row). The sill is a fine vault, but `VaultProbe` asked for 0.9 of a standing body's worth of air over
the far edge, and a window's lintel is 1.4 to 1.9 m over its sill, so every window was refused.

- `VaultProbe` now measures the free height over the TOP (a body-wide sphere swept up from the middle of the top) as `Vault.Headroom`: the whole body
  over a fence, the opening up to the lintel over a sill. Anything under 0.9 m is refused; past the far edge it asks only for as much room as that.
- `PlayerVaulting` DIVES through a low opening: the flatter the lower it is (`DiveFor`): at 1.4 m of headroom or less the body is laid out at 82
  degrees, head first, its hips through the middle of the opening and its legs trailing, and stands up again on the far side; over 2.9 m of headroom it
  is the ordinary 35 degree vault; in between it blends. A 1.86 m punched window gets a 67 degree dive whose head just clears the lintel.
- A CRACKED pane in the way is knocked out as the vault starts (`CrackedPane.Knock`): its trigger only answers to a moving collider and the vaulting
  body's collider is off.
- Tests: `VaultProbeTests` (a shattered punched window is vaulted and the dive fits under its lintel; the whole pane is not; a 0.7 m letterbox is not;
  a fence is no dive), `RealCityWindowVaultPlayTests` (the real facades near spawn: a sill-high ground-floor window is refused whole and vaulted once
  shattered; a step-high one is walked into once shattered and not before), `VaultCapture` renders `vault-window-*.png`.
- The vault band now starts just over the controller's 0.4 m step (`VaultProbe.BandLowFraction` 0.16, was 0.22): a 0.4 to 0.57 m sill could be neither stepped nor vaulted.
- Not done (an upper-floor window reached by climbing is entered since 2026-10-14, see below; `LedgeProbe` still wants an open path a whole body tall over the
  lip); a tucked legs clip for the dive; vaulting a window when only walking at it (the vault is still run + forward).
- Walking in over a LOW sill (the door wall's 16 cm panes, a ribbon's 38 cm) on a real facade: the shattered pane no longer stops the body and it
  steps onto the sill, but on some buildings the lobby floor inside stands higher than the sill than the controller can step (`RealCityWindowVaultPlayTests`
  logs the difference). That is the interior's floor height, not the window; left as is.

## Update: most windows vault now, not one in seven (2026-10-14)

Owner: "the vaulting through window function isnt working on most windows". A survey of the real city near spawn
(`RealCityWindowVaultPlayTests.MostShatteredSillHighWindowsOnRealFacadesAreVaulted`: every ground-floor window on 30 buildings with its sill at
vault height over the street, shattered, run at from 18 approaches: the middle and a quarter of its width either side, a stride out and pressed
against the wall, straight on and 25 degrees off) found only 46 of 315 vaulted straight on, and 391 of 5670 approaches. `VaultProbe.TryFind` now
says which stage refused (`VaultProbe.Refusal`), and the survey logged it:

- **The room behind stands higher than the street** (most of them: NoTop). Past the sill the probe looks down for where the top ends; a room floor
  more than 20 cm over the street is inside the vault band, so it read as the top stepping DOWN part-way and the window was refused. Every window
  with its sill 1.2 m or more over the street failed, and nearly every tall ribbon window. A surface lower than the top now ends the top (it is past
  the far edge, and the landing check judges the drop); only a top that steps UP part-way is refused.
- **Off its middle, the way through was checked beside a jamb** (OpeningBlocked). The clear-opening box was centred wherever the body met the wall.
  Now rays along the sill find the jambs and the way through is slid to keep `JambClearanceMetres` (0.33 m) from each, or put midway in a narrow
  window; the landing follows. A fence or a box has no jambs and nothing moves.
- **The sill's stone ledge hid the wall under it** (NoFace). The ledge stands 8 cm proud of the wall, so the knee-height sweep met its underside
  first; an edge's normal points half down, and the sighting was thrown away. A sweep that catches an edge now looks for the face just under it
  with a ray. A sweep at 0.17 of the height was added for sills under 0.6 m (the lowest one passed over them into the room).
- **Every face found is tried, nearest first** (a plinth or a kerb in front of a window was the nearest face, a step with the sill rising behind
  it). The furthest stage any face reached is the refusal reported.
- A whole pane is still never vaulted: the clear-opening box now starts 6 cm in front of the near edge, so a pane in the very plane of the face
  found (when the face found IS the glass) is inside it. The survey asserts no whole window vaults from any approach.
- **A window too high to vault is climbed to and dived through.** 57 of the 532 ground-floor windows near spawn stand more than 2 m over the
  street (and every upper-floor window is out of a vault's reach). A climber under one was held there: a window is no ledge (`LedgeProbe` wants a
  whole body's worth of room over the lip) and there is no wall at head height to climb on. `PlayerClimbing.Tick` now asks `VaultProbe` while
  climbing up with no ledge ahead, as if the feet stood higher (`WindowReachLiftFraction`: a sill up to 1.2 body heights over the feet, the
  mantle's reach; from the feet the band ends below the head, where the climb stops at a window); a broken window in reach ends the climb and
  starts the same dive (`PlayerVaulting.Begin`). Only through an OPENING (`VaultProbe.IsThroughAnOpening`: something over the top, a
  lintel): a roof parapet under open sky is still mantled, as before (it was vaulted the first time round, `RealCityClimbTopOutPlayTests`). A
  whole pane refuses it and the climb goes on over the glass. It travels to other players like any vault (the path and lean with the transform).
- Result on the real city (the survey, open car parks and round towers now left out of it): 313 of 323 shattered sill-high windows vault
  straight on (was 46 of 315) and 5577 of 5814 approaches (was 391 of 5670); 64 of 73 windows too high to vault are dived into from a climb;
  no whole window either way. The rest are rooms with something right behind the window (no room to land) and one hillside building standing
  up to 11 m over the street.
- Tests: `VaultProbeTests` (a city-shaped window: hollow 0.3 m wall, ledge, jambs: into a raised room; from anywhere along it; under the ledge; a
  0.5 m ribbon sill; behind a plinth; whole from any approach; the refusal named), the survey (at least 90% straight on and 80% of approaches;
  80% of the windows too high to vault dived into from a climb; no whole pane either way), `VaultPlayTests` (climbing up to a broken window 3 m
  up dives in and stands on the room floor; a whole one is never dived through), `RealWindowVaultCapture` (Explicit, -Graphics) renders
  `vault-real-*.png`.

## Update: small obstacles are stepped onto, not run into (2026-10-15)

Owner (2026-10-14, a screenshot of the body held at the foot of a shattered shop window): "im getting stuck when running into small obstacles
like this when i should just be running right over this".

- **The gap.** The CharacterController steps 0.4 m on its own (in one frame); the vault starts at 0.42 m and only over something that ENDS (a
  fence, a sill with a lower room behind it). A raised floor does neither: a 0.44 m step stopped the body dead, walking or running. In the city
  the floor-length windows beside a front door have their sills 0.4 to 0.6 m over the pavement, and a survey of the real city near spawn ran
  into them shattered and got into none.
- **The step up** (`Character/Movement/StepUpProbe.cs`, `Character/Movement/PlayerLocomotion.StepUp.cs`). Anything in the way up to a quarter of the body's
  height (0.65 m, the share Unreal's character steps by default) is stepped onto in the stride, walking or running. The probe validates in
  stages like the vault's: a near-vertical face at shin height along the direction of travel (from the body's middle and either side of it); the
  face stands on the body's own ground, so a staircase (whose second riser stands on the first tread) is never one step and stays the
  controller's, a riser at a time; a flat top behind the face no higher than the limit; a whole standing body's worth of room on the top (a whole
  pane over a sill, or a broken window lower than the body, refuses it: the vault's dive is the way through those).
- **How it moves.** The body is lifted along a 45 degree line as it closes on the face (at its running speed, at least 3 m/s), so it is up as it
  gets there; it is carried level until its middle is over the top, then set down. Meanwhile the controller's own step is held at 1 cm, or it
  would pop the rest up in one frame. It counts as on the ground throughout: Space jumps, the footsteps go on, the animator's Grounded stays true
  (other players are sent a stride, not a fall). It lets go when the keys do, when the body turns away, at a ceiling, or after 0.6 s, and then
  waits 0.35 s before trying again (no bobbing at a step it cannot take).
- **Under a low ceiling the controller's step is cut to the room over the head** (`FitStepUnderCeiling`). Before every sideways move the
  CharacterController lifts the body by its whole step offset; with a ceiling closer than that over the head the lift met it and the body could
  not move at all, even on flat ground. That is what still held the body on a shop window's sill once it had stepped up: the top of the opening
  is 0.2 m over its head there. The step is now the free height over the head, measured over the whole patch the head can sweep this move (a
  flat square the body's width plus the move plus a margin, cast up from the top of the head): under a shop's canopy on seed 74102958 the lifted
  head met the canopy's front edge 0.3 m ahead and 2.75 m up, was pushed back, and the body shook on the spot (measured straight up, the room
  looked ample).
- **Taller than the step up** is the vault (run at it) or a jump. A shop's storefront (glass from a 0.5 m stall riser up to 2.8 m: 0.65 to 1 m
  over the pavement, and lower than the body) is still the vault's dive.
- Result on the real city near spawn (`RealCityDoorwayPlayTests`): every front door without steps built in front of it is walked and run into
  (20 of 20 on the first seed; the one left out stood 0.77 m over the pavement at its foot with no stairs, a city bug since fixed: the door's
  ground is now measured at the wall's foot, docs/SUBVERSION_CITY.md), and every shattered floor-length window whose sill a body steps and that has
  standing room in it and just inside is run straight into (8 of 8, sills 0.39 to 0.62 m over the pavement; none before). On seed 74102958 some
  are not: a shop front's stall riser leaves an opening lower than the body (the vault's dive), or a stair or a ceiling stands just inside.
- Tests: `SmallObstaclePlayTests` (raised floors 0.3 to 0.64 m walked and run up, smoothly (under 0.14 m a frame) and on the body's feet
  throughout; low bars stepped over; 0.8 m not; a whole pane over a sill and an opening lower than the body not stepped into; six risers taken one
  at a time; ceilings 0.12 and 0.25 m over the head; the city's floor-length window), `RealCityDoorwayPlayTests` (the doors and the shop windows
  of the real city), `NetworkedPlayerStateSyncPlayTests.AStepUpOnOnePlayersBodyShowsOnTheCopyAsAStrideNotAFall`. `StepUpCapture` (Explicit,
  -Graphics) renders `step-up-side-*.png` and `step-up-behind-*.png`: a run into a shattered shop window with a 0.55 m sill, lifted over four
  frames and set down on the shop floor.
- **Measured from what the body stands on**, not the bottom of its capsule (`StandingOnY`): perched on a stair's nosing the rounded bottom dips
  below the tread under it, and an ordinary 0.22 m riser read as a 0.3 m step (the stairs test failed in full runs only).

## Update: on the toes, knees out, palms on the wall (2026-10-30)

Owner: "improve upon our climbing it looks terrible, the bottom of the feet is pressed flat on the wall instead of it being like the tip of the toes", with a
video of a Unity ledge and free-climb system (shoseini's, shown by @Botan_cr on X, 2026-10-29) as the model: "use that video ... as inspirations or just copy
it exactly". Studied frame by frame: the feet point INTO the wall and only the toes touch it, heels out; the knees bend out to the sides (a frog); the hands
hold high, beside or over the head, the elbows out and down under them. A render of ours (`ClimbCapture`) showed the opposite: the soles flat on the wall,
the legs hanging nearly straight under a vertical body, and the fists held at the face with the forearms in front of it.

- **On the toes** (`Character/Movement/ClimbToeHold.cs`, pure): each foot points into the wall, turned out 22 degrees toward its own side, the heel 12 degrees
  below level; the stepper's foot hold is now where the TOE touches the wall (`ClimbLimbStepper.FootOffWall` 1 cm, was the ankle a foot's height off it),
  and the ankle is placed so the front of the body's own round toe (`FusedBodyShape`) lands on it, no other part of the foot nearer the wall.
- **The frog**: the feet hold higher (0.32 leg lengths under the hip, was 0.42) and wider (0.38, was 0.30), and the knees bow out to the sides, a little up
  and away from the wall (`ClimbKneeOffWall`, `ClimbKneeUp`).
- **The hands**: hold beside the head (0.50 arm lengths over the shoulder, was 0.22) and a little wider, reaching less further going up (0.20) and more down
  toward the shoulders climbing down (0.30); the PALM is turned flat on the wall, the fingers up it leaning 15 degrees out, the thumb inward
  (`PlayerLocomotion.PalmOnWall`), the wrist 5 cm off the face (the palm's thickness).
- **The safety net no longer fights the pose.** `ApplyClimbWallLimbs` (after the stepper) kept every hand 9 cm off the wall, more than the stepper's own
  3 cm hold, so it re-solved every arm, every frame, with its own elbow hint (toward the camera): that was the forearms in front of the face, and the
  left elbow 19 cm through the wall at the grab (`ClimbLimbsPlayTests`, failing every run since 2026-10-16). Its hand clearance is now 4 cm, under the
  stepper's hold: the elbow now stays 1.4 cm from the face at worst and the test passes.
- Other players see it: the copies run the same stepper and pose from the wall plane and travel they already receive.

Tests: `ClimbToeHoldTests` (the foot points into the wall and its sole does not face it; each foot turns out to its own side; the toe lands on its hold and
nothing else of the foot is nearer the wall, at two body sizes), `ClimbPalmTests`, `ClimbLimbsPlayTests.TheFeetClimbOnTheirToesWithTheKneesOut` (real rig: the
feet point into the wall, the toes 1 to 3 cm off it, the knees 21 cm wider than the hips at least), `ClimbLimbStepperTests` (unchanged rules, new holds).
`ClimbCapture` also renders `climb-toes.png` (the legs from the side and from behind, the whole body three-quarter).
Not copied from the video: its ledge hang and shimmy with the fingers curled over the lip, corner wrapping, and inclined ledges (our ledges are pulled up
onto, `LedgeProbe`).

## Update: onto a pitched roof (2026-10-15)

`LedgeProbe` has a third kind of ledge (`TryFindSlope`) for the eaves of a pitched roof (`World/RoofShape`): the top is a walkable slope (at most 33 degrees,
`LedgeProbe.MaxSlopeDegrees`) rising away from the wall, its edge up to `MaxOverhangMetres` out past the face. Rows of probes up the slope must each be level across and rise steadily;
the stand spot is on the slope, and the ledge's height is the stand spot's, so the mantle rises above the edge before it steps in and the feet pass over the slope.
A 35-degree slab on a wall top is still refused (`ASlopedTopIsNotALedge`). Tests: `LedgeProbeTests.APitchedRoofsEavesAreALedge...`, `RoofClimbPlayTests`.

## Fix: no accidental vault running up stairs (2026-11-21)

Owner, 2026-11-19: "i accidentally vault when running up stairs sometimes" (and on 2026-11-21, of everything else about vaulting: "i will have
fable to better vaulting animations and mechanics", so only this was changed). Reproduced in `VaultOffStairsPlayTests`: the real body sprinting
up a stair of the city's size vaulted the rail along its landing, and, on a flight with a short landing and a drop past it, the stair's own
upper steps from its second step. Two causes, two fixes:

- **The obstacle must stand up from the ground you are on** (`VaultProbe`, stage 2, `Refusal.NotAnObstacle`): the ground just in front of the
  face found within a step (0.4 m) of the feet, and the top at least 0.4 m over it. A stair's upper steps rise a step at a time from the tread
  before them; a rail on a landing stands on ground well above the flight. A vault from a climb (`TryFindFromAClimb`, into a broken window too
  high to vault from the street) is not asked this: its feet are where the body would stand, not ground.
- **A run carried on up a stair vaults nothing at first** (`PlayerVaulting.AfterStairsMetres`, 3 m): while Shift and W are held since the body
  was last on a stair, no vault starts until it has gone 3 m on the level. Held into a landing's rail it goes nowhere, so it never vaults it;
  let go and press again, and the rail vaults as any fence. The same rule the automatic climb has (`PlayerClimbing.MarkStairs`). "On a stair"
  is `StairRun`: the feet rising faster than 1.6 m/s over a short stretch (a sprint up the city's stairs rises 4.7, a walk 2.5; a kerb is
  over in a frame, a 20% street at a sprint is 1.5), or the walk's own stair sense (`GroundGlide.IsStairs`), which a sprint up a short flight
  outruns.

An earlier attempt (2026-11-19, never committed) also refused any vault with two risers in the ground round the run-up, a metre either side: it
refused 156 street windows beside entrance steps and did not catch the landings, and was dropped. The real-facade survey after the fix:
363 shattered sill-high windows vaulted straight on, 22 of 22 high ones dived into from a climb, none refused as `NotAnObstacle`.

# Unarmed (fist) combat

## What was wrong

Reported: "the no-weapons fists melee feels really horrible and looks awful: the feet freeze up and stop moving and the animations themselves just look awful."
Diagnosed in the code:

1. **The feet froze twice over.** The punches were two full-body Mixamo jab clips played on the animator's BASE layer (so the legs stopped walking for the clip), and
   `MeleeAttacker.SwingMoveFraction` was 0 for an unarmed swing (so the body could not move at all while it lasted). The sword had already been fixed the same way
   ("make the legs walk and not freeze up during sword use too"); the fists never were.
2. **The punch came late.** `PlayerInputReader` threw it when the mouse button was RELEASED (to tell a tap from a hold): 100 ms or more between the click and the fist.
3. **It punched where it used to face.** The body turned to the camera only while MOVING; standing still after turning the camera, the punch went the old way.
4. **Every punch was the same punch.** `punch_right`/`punch_left` x2 plus the haymaker, each one the same weak jab; on this rig (short arms on a 2.6 m body) the clip barely
   left the chest.
5. A press during a swing was simply lost, and each swing had to play out its whole tail before the next could start.

## What third-person fighting games do (the research)

Sifu, the Arkham and Yakuza games, Street Fighter's 3D cousins, Fortnite's melee, Souls-likes: (a) a **guard stance** the fists return to; (b) **anticipation, fast extension
that accelerates into the target, a brief hold on contact, slower recovery**; (c) **different punches on different paths** (jab, cross, hook, uppercut, haymaker);
(d) the **torso turns and leans into each blow**, with a small counter-turn in the wind-up; (e) the **fist reaches full extension at the moment it lands**;
(f) **hit-stop** on contact; (g) **punch magnetism** (a blow is thrown at the enemy near where the camera looks) and a **step into the blow**; (h) **input buffering**
and **cancelling a recovery into the next punch**; (i) **instant response**: attack on the press.

## What it does now

- `Character/FistStrikes.cs` (pure data and maths): five procedural strikes. Each is a path for the throwing fist (in arm lengths, relative to its shoulder), a torso twist
  and lean, a duration and a contact moment. `Evaluate(kind, t, guard)` gives the pose; `Guard()` the stance. Chain: **jab, cross, hook, uppercut, haymaker** (same
  damage/hitstun as before; the haymaker is still the finisher and the held punch).
- `Character/FistPoseDriver.cs`: plays a strike on any body (the owner's and every network copy's: `PlayerAnimationDriver.PlayState` hands it the move), holds the guard for 3 s
  after the last blow, and freezes the fist at full reach for 55 ms when a blow lands (hit-stop). Yields to anything else that owns the arms (a weapon, a gun, a climb, a vault, a ragdoll, a grapple).
- `PlayerLocomotion.ApplyFistPose` (LateUpdate, after the Animator): twists and leans the spine, puts each fist where the frame says with two-bone IK, the other held in the guard.
  `SetFacingOverride` turns the body.
- The legs keep walking: no clip is played over the body, and `MeleeAttacker.UnarmedMoveFraction` keeps 25-65% of walking pace (by punch) and `LungeFraction` steps INTO the blow
  while the fist is going out.
- `PlayerInputReader`: the punch goes out **on the press**; holding past 0.4 s and letting go follows with the haymaker. The body turns (1200 deg/s) to the nearest enemy within
  70 degrees of where the camera looks and within 4.5 m (`MeleeAttacker.TryFindMagnetYaw`), else to the camera.
- `MeleeAttacker`: each strike has its own length and contact moment (`FistStrikes`); a press during a swing is remembered for 0.25 s and thrown when the swing allows; the
  last 35% of a swing (after the contact) can be cancelled into the next punch.
- Multiplayer: the strike's name already travels (`PlayerAnimationDriver.StatePlayed`), and a copy plays the same procedural punch (`NetworkedActionSyncPlayTests`).

## Tests

`FistStrikesTests` (paths: guard at both ends, full reach at contact, five distinct paths, torso turns the right way, no NaN), `MeleeCombatPlayTests` (chain, buffering, cancel,
the body keeps its pace and lunges), `RealInputPunchPlayTests` (a real mouse and keyboard: the punch is on the press, the body walks and steps into it, it turns to the enemy near the
camera, hold-and-release is the haymaker), `NetworkedActionSyncPlayTests` (a copy punches). `FistCapture` (Explicit, -Graphics) renders a contact sheet of all five punches to
`Logs/CityShots/fist-sheet.png` (side) and `fist-sheet-front.png` (three-quarter).

## Animations: what was looked at

- **FiveMCombatOverhaul** (alberttheprince): a FiveM resource that replaces GTA V's own melee animations with Wolf Fire Modz's, "not exposed" as standalone files (the game's
  animation dictionaries): nothing to import, and they are Rockstar's/modder's assets, so not usable in this game.
- The project's existing clips (`boxing_left_jab`, `boxing_right_jab`, `heavy_punch`, `boxing_idle`, `body_block`) are no longer used for the punches (they remain in the folder).
- If better CLIPS are wanted they could be played on the arms on top of this (the strikes are procedural so the paths, the contact timing and the network replay do not depend on
  clips): see the end of the session summary for sources.

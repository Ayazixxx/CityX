# The parachute (2026-11-19)

Owner: "make a parachute with good parachuting mechanics, press space to open it when your falling in mid air. make so it doesnt just activate
off jumping and pressing space quickly even though you are technically in mid air. look at how gta and saints row do it, i think its like you
have to be falling in mid air for like 2 seconds before you can activate parachute. also i think we already made some basic shitty parachute
but it was terrible ... make a good parachute backpack that you equip on your back and when equipped you can use it by pressing space mid air.
make it look and feel really good research other games parachuting physics and implement what you learn", and "id like to design the backpack
myself later but just make a basic one that works for now".

`/equip parachute`. It is worn on the back (the jetpack's and the wings' slot), Uncommon, as before. The old one (Space opened and closed it
anywhere in the air, the fall clamped to 4 m/s, a sphere for a canopy, nothing shown to other players) is gone.

## What the research found

- **GTA V** (guides: gtaboom, gamepressure, alphr, sportskeeda): a fall long enough turns into a skydive you steer; you open the chute
  yourself; under it A/D (the left stick) steer, W/S pitch it forward (faster, down) and back (slower), and a "precise landing" brakes it for
  the touch-down; a bad landing knocks you down. No official number was found for how long the fall has to be: the owner's two seconds are
  used.
- **Saints Row**: nothing firm found on a fall-time rule.
- **Real skydiving** (USPA, dropzone.com, skydiving schools): belly down a body falls at about 55 m/s (120 mph), head down far faster; a ram-air
  canopy glides about 3 m forward for every 1 m it sinks, at 9-12 m/s; pulling a toggle turns it, the farther the faster, and **a turn always
  sinks it faster** (the low turn is how canopy pilots get hurt); both toggles down slow it; held deep the brakes sink more than you would
  think; the **flare** pulls both toggles quickly from a full glide and turns the speed into lift for a moment, which is how it is landed
  softly; flare too early and the lift is spent before the ground.

## How it plays

| | |
|---|---|
| Jump, fall a little | Nothing: a fall is timed, and Space does nothing (and climbing works as ever) until it has fallen **2 seconds** |
| Falling 2 seconds | The body spreads out face down, a skydiver's box. The air holds it near 50 m/s. **A/D** turn it, **W** dives (head low, arms back, up to 68 m/s and tracking forward at 16 m/s), **S** flattens out (40 m/s). The view widens and streaks of air go past, as with the jetpack |
| **Space** | The right hand goes back to the pilot chute's pouch and throws it; the lines pay out and the canopy fills with a crack, braking the fall hard over about a second; the body swings up under it, past hanging and back, and the hands go up to the toggles |
| Under the canopy | It flies the way it faces at 11 m/s, sinking 4 m/s. **A/D** pull a toggle and turn it (it banks, the body swings out, and it sinks faster); **W** pulls the front risers and dives (16 m/s, sinking 7.5); **S** pulls both toggles: slower and slower down. The camera stands 3 m further back so the canopy shows |
| **The flare** | S pulled from a full glide almost stops the sink for a second or two (pulled after a dive, it even lifts a little). Held on, it fades: held brakes alone sink 3.4 m/s |
| Landing | Flared (sinking under 3 m/s): soft. Gliding: a firm landing, no harm. Diving, turning low, or still opening (sinking over 6.5 m/s): **knocked down and hurt** (15, and 6 more per m/s past 6.5). The canopy carries on a little and collapses |
| **Space** under the canopy | Cuts it away (not in its first 0.6 s, so a double press on opening is not a cut-away): it tumbles off, the body falls, and two more seconds of falling open it again |
| A wall at speed | The canopy collapses, as cut away |

All the numbers are mine (`ParachuteFlight`), but the two seconds (the owner's).

## How it is built

- `Character/Movement/ParachuteFlight.cs`: the jump as a pure model (Packed, Freefall, Opening, Open), tested on its own.
- `Weapons/Gear/Parachute.cs`: the worn pack. `PlayerInputReader` ticks it every frame it is worn (`IParachute`); once it has the body it
  gives it its velocity (`PlayerLocomotion.ForceVelocity`) and its heading, and the walk gets no keys, the body no aim to turn to. Under the
  canopy the fall's height never hurts (`PlayerVitals.ResetFallTracking`); the landing does, by the model. A frame it is not ticked (the body
  on a wall, on a rope, knocked down) ends whatever was out.
- `Weapons/Gear/ParachuteRider.cs`: on every body (wired in `PlayerAbilityWiring`), it shows the jump: the poses, the canopy, the sounds.
- The poses are made in Blender (`CityX/SourceArt/Parachute/parachute_pose.py`, `parachute_build.py`, into
  `Character/Resources/Parachute/ParachuteMotion.json`): the box and the dive (made standing, laid face down by the game), the pull, hanging on
  the toggles, each toggle pulled, the flare, the front risers. The game blends between them as the hands move; the bank, the swing under the
  canopy and the body laid down are a turn of the whole pose (`BakedPose.Turn`).
- `Weapons/Gear/CanopyShape.cs`: the canopy, a ram-air wing 7 m across and 2.6 m deep (the body is 1.3 times a person), arched, nine cells
  that bulge between their ribs, orange and white, the mouths dark; 3.6 m over the shoulders. `ParachuteCanopy` places it and draws the
  lines: eight in all (owner: "the parachute has a lot of strings lower it to maybe 8 total"), three suspension lines down to each shoulder
  and a steering line from each tip's tail to each hand.
- `Weapons/Gear/ParachuteSounds.cs`: the crack and the flutter, made in code; the fall's rush is the jetpack's wind loop.
- The pack (`ParachuteBuilder`): a container, its top flap, the pilot chute's pouch and an orange handle. A stand-in for the owner's own.

## Other players

The body's `ParachuteRider` is an `IReplicatedAbility` ("parachute"): the wearer's says what state the jump is in and which of W, A, S, D are
held whenever either changes (a few words a second at most), and why it was put away (landed, or lost). A copy eases its hands to those keys
as the wearer's do, reads the bank off the way the body turns, and shows the same poses, canopy and sounds. The pack itself is shown as worn
gear always was (`NetworkedEquipmentSync`).

## Not done

- The owner's own backpack design.
- Shooting under the canopy: a gun can be fired, but the hands stay on the toggles (GTA lets you fire one-handed).
- A reserve canopy; wind; landing on a moving target.
- A hint on the screen that Space will open it (the body spreading out is the sign).

## Tests

`ParachuteTests` (EditMode: the two seconds, a jump never opening it, the terminal speeds, the opening's brake, the glide, the turns, the
flare and how it fades, the landings, the cut-away, a wall, the canopy's shape, a stranger's packet), `ParachuteFallPlayTests` (the real body:
a jump stays a jump; a long drop laid flat, opened, hanging upright under a canopy over its head, A turning it with the left toggle down; a
flared landing hurts no one; a dive into the ground knocks the body down; another player's copy), `ParachuteRealInputPlayTests` (the real
scene with a simulated keyboard), `ParachuteRealScenePlayTests` (equipping, the shared back slot). Pictures: `ParachuteCapture` (Explicit,
-Graphics).

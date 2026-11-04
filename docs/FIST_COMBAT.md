# Unarmed (fist) combat

## What it is now (2026-11-03: Souls-style)

Owner: "make the fists balanced right now they are too op and i dont like the animations either", "make the fists animations like dark souls unarmed
punching". The research and the numbers' reasoning are in `MELEE_COMBOS.md`.

### The moves

- **Lights: a right straight, then a left straight** (`ComboChain.Unarmed`, Souls' two-hit R1 string). The left closes the chain and its recovery
  cannot be cancelled: after it the fighter is open. The first's stun outlasts the gap to the second's contact, the second's does not outlast its
  own recovery: **two hits are guaranteed, the third is not** (`FistStrikesTests.TwoPunchesAreGuaranteedAndTheThirdIsNot`).
- **Heavy (hold left click, let go): the overhand** (`CombatTuning.HeavyPunch`): the fist wound back and high over the shoulder for a long moment
  (the tell), then brought down and across with the whole body. It **knocks a player down** (`ComboStage.KnocksDown`: the finisher ragdoll), no
  stun on top. It is outside the chain: a light after it starts the chain over.
- **Held again inside the overhand's window: the uppercut** (`CombatTuning.UppercutPunch`), from a dip, driven up under the chin. It stuns longest
  of the fists and opens nothing.

### The numbers (`ComboChain.cs`)

| | Right straight | Left straight | Overhand (hold) | Uppercut (hold again) |
|---|---|---|---|---|
| Damage (of a sword blow's 10) | 5 | 5 | 10 | 8 |
| Stamina | 8 | 8 | 16 | 12 |
| Hitstun | 0.33 s | 0.25 s | knockdown | 0.4 s |
| Length / contact | 0.5 s / 0.23 s | 0.5 s / 0.23 s | 0.8 s / 0.48 s | 0.6 s / 0.3 s |

- **Reach 1.4 m** in a **90 degree cone** (`MeleeAttacker.FistReachMetres`, `FistHalfArcDegrees`): the sword's 2.2 m keeps a puncher off. The old
  2.5 m / 120 degrees out-reached the sword. (`Range` stays for a held weapon's swing and for breaking a window.)
- **No stamina, no punch**, as with the sword (`PlayerVitals.TrySpendCombatStamina`: allowed while any is left, then the bar bottoms out; recovery
  pauses 0.6 s).
- A press during a punch is kept until the punch lets it out (as in Souls: the punches are long enough now that the old 0.25 s buffer lost presses
  made early in one); the last 35% of a non-closing punch can be cancelled into the next, as before.

### The animation (`Character/Posing/FistStrikes.cs`, all procedural)

Not a boxer's: a brawler's, from Souls' bare-handed moveset.

- **No guard.** Between blows the arms hang **down at the sides** in the READY stance (`FistStrikes.Ready`): fists a little forward of the hips,
  elbows slightly bent, hips sunk 7 cm, feet planted square. Held 1.5 s after the last blow (`FistPoseDriver.ReadyLingerSeconds`), then the body
  stands as it always does.
- **A straight**: the fist drawn back to the hip (low, behind the shoulder) with the torso wound against it, then driven straight out to full reach
  at chest height with the torso turned 38 degrees into it and leaning 16 degrees forward, the hips sinking 11 cm; a hold at the contact (hit-stop
  on a landed blow, as before); then a slow pull back down to the side. The left straight is the right one mirrored.
- **The overhand**: wound up high over the shoulder for 45% of its length, then brought down and across (elbow out), the body leaning 24 degrees.
- **The uppercut**: the hips dip 17 cm and the fist drops low, then it rises under the chin while the body straightens and leans back.
- **The feet** stand in the fighting stance and step to it with `SwordFootwork` (the sword's stepper): each blow puts the OTHER foot forward
  (`FistFrame.StanceLead`, half a leg length apart), so the hips turn into the blow over the front foot; the ready stance is square. The hips' drop
  (`FistFrame.HipDrop`) goes through `PlayerLocomotion.PoseHipDrop` like the sword's, which is what lets the feet stand apart (the foot reach).
- The other hand stays down at the side while one throws.

### Where it lives

- `FistStrikes` (pure paths and timings, tested on paper), `FistPoseDriver` (plays a blow on any body: the owner's and every network copy's, holds the
  ready stance, hit-stop), `PlayerLocomotion.Fists.cs` (`ApplyFistPose`: spine twist and lean, two-bone IK on each arm; `SetFacingOverride` turns the
  body to the target), `PlayerLocomotion.SwordPose.cs` (`StanceWanted`: the footwork takes the sword's stance while a sword is in hand, else the fists').
- `MeleeAttacker`: the chain, the heavy and its held follow-up (`_heavyFollowUpRemaining`), the stamina, the reach, the knockdown (`KnocksDown`).
- `PlayerInputReader.Combat.cs`: the punch goes out on the press; held past 0.4 s and let go it is the overhand; the body turns (1200 deg/s) to the
  nearest enemy within 70 degrees of where the camera looks and within 4.5 m (`MeleeAttacker.TryFindMagnetYaw`), else to the camera.
- Multiplayer: the strike's name travels as before (`PlayerAnimationDriver.StatePlayed`); a copy plays the same procedural punch, stance and all.

## Tests

`FistStrikesTests` (ready stance at both ends and arms down, full reach at contact, the straights mirror and the heavies differ, the straight is drawn
back to the hip, the overhand comes down from high and the uppercut rises, torso turns the right way, the other foot forward, two hits guaranteed and
not three, weaker than a sword and costs stamina), `CombatTuningTests` and `BlockAndHeavyTests` (the chain's shape, the knockdown's zero stun, the
overhand above every straight, the uppercut after it), `MeleeCombatPlayTests` (reach, buffering, cancel, the closing punch recovered in full and the
chain restarting, overhand then uppercut, stamina), `RealInputPunchPlayTests` (a real mouse and keyboard: the punch is on the press, the body walks
and steps into it, it turns to the enemy near the camera, hold-and-release is the overhand), `NetworkedActionSyncPlayTests` (a copy punches),
`PlayerHudPortraitPlayTests` (a portrait is not taken with the fist up). `FistCapture` (Explicit, -Graphics) renders a contact sheet of the four
punches to `Logs/CityShots/fist-sheet.png` (side) and `fist-sheet-front.png` (three-quarter).

## History

- First pass (2026-10): the two Mixamo jab clips froze the feet, punched on the release and all looked alike; replaced by five procedural boxing
  punches (jab, cross, hook, uppercut, haymaker) from a guard at the face, with punch magnetism, hit-stop, buffering and the body kept walking.
  Those are gone: the owner wanted Souls' brawler, and the fists (10 a hit at 2.5 m for nothing) were "too op".
- Clips looked at then: FiveMCombatOverhaul (Rockstar's/a modder's assets, not usable); the project's `boxing_*` and `heavy_punch` clips are unused.

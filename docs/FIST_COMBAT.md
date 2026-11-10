# Unarmed (fist) combat

## What it is now (2026-11-03: Souls-style; 2026-11-09: full-body clips)

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
| Stride (the body carried in) | 0.45 m | 0.45 m | 0.50 m | 0.30 m |

- **Reach 1.4 m** in a **90 degree cone** (`MeleeAttacker.FistReachMetres`, `FistHalfArcDegrees`): the sword's 2.2 m keeps a puncher off. The old
  2.5 m / 120 degrees out-reached the sword. (`Range` stays for a held weapon's swing and for breaking a window.)
- **No stamina, no punch**, as with the sword (`PlayerVitals.TrySpendCombatStamina`: allowed while any is left, then the bar bottoms out; recovery
  pauses 0.6 s).
- A press during a punch is kept until the punch lets it out (as in Souls: the punches are long enough now that the old 0.25 s buffer lost presses
  made early in one); the last 35% of a non-closing punch can be cancelled into the next, as before.
- **A punch roots the body** (2026-11-09) and carries it into the blow by its own stride, from the lead foot leaving the ground to the blow
  landing (`FistStrikes.Carried`, `MeleeAttacker.CarryIntoPunch`), through the character controller, so a wall or a body stops it. Before,
  the body kept half its walking pace under a punch and was pushed forward at nine tenths of it while the fist went out (about 0.7 m): the
  legs walked on the spot under an arm. Each punch now goes 0.45 m, two in a row 0.9 m.

### The animation (2026-11-09: full-body clips made in Blender)

Owner, 2026-11-09: "improve upon the fist fighting (mostly the animations)". Of the punches posed in code on the arms alone, 2026-11-03:
"there needs to be some type of full body animation/movement just the arms doesnt look right". Until now a punch was two-bone IK on the
throwing arm, a turn and a lean of the spine, and the walk cycle's legs under it.

Not a boxer's: a brawler's, from Souls' bare-handed moveset. Each blow is a clip on the game's own skeleton
(`CityX/SourceArt/FistCombo/fist_pose.py`, built headless by `fist_build.py`, exported to `Character/Resources/Fists/FistMotion.json`,
played by `BakedMotion` as Octaslash is: docs/OCTASLASH.md). The lengths and the moment each lands are the game's (`FistStrikes.Seconds`,
`ContactFraction`: keep the two in step), so the fights are timed as they were. What the clips add:

- **A real step.** The lead foot (the one across from the fist) strides as the body is carried forward; the rear foot stays planted on
  the ground where it stood, rolls up onto its ball as the hips leave it, and steps up after the blow. Every punch ends standing as it
  began, feet square, so the next can follow it. (The feet are written on the ground, `SourceArt/planted_feet.py`, shared with the sword's
  clips: a planted foot's place in the clip is its place on the ground less how far the body has been carried by then.)
- **The weight.** Back over the rear foot as the blow is wound up, forward over the lead foot as it lands.
- **The turn from the ground up.** The hips first, the shoulders after them, the fist last and fastest, at full reach on the frame the
  blow lands.
- **The other arm works against the blow**: out in front as it is wound up, snatched back to the ribs as it lands (the overhand throws
  it out behind). On their way back down both hands come up loosely before the belly: drawn straight back to the sides they pass through
  hands on the hips, elbows out (the owner's "what the fuck is this attack position", 2026-11-03).
- **The head stays on what is ahead** while the shoulders turn under it.
- **The straights**: the fist drawn back to the ribs, a stride, the arm driven straight out from the turning hips, the body leaning
  17 degrees into it over a bent lead knee, the rear leg stretched out behind on its toes. The left is the right one mirrored.
- **The overhand**: for nearly half its length the body leans back with the fist high behind the head, the other arm out in front and
  the lead knee lifted, like a pitcher: the tell. Then it comes over the top and down across, the body bending 24 degrees after it.
- **The uppercut**: a dip (the hips 24 cm down, the fist dropped beside the hip), then the legs drive it up the middle to chin height,
  the elbow under the fist, the body leaning back.
- **The fists are closed** (`PlayerLocomotion.ApplyFingerGrips`, while the stance is held): the hands were open, and an open hand thrown
  at somebody is a slap.
- **Between blows: no guard, and no placed hands.** The arms are the walk cycle's own (`FistStrikes.Ready` poses neither). Hips sunk
  7 cm, feet planted square, fists closed, for 1.5 s after the last blow (`FistPoseDriver.ReadyLingerSeconds`), then the body stands as it
  always does.

How it is played (`FistPoseDriver`, on the owner's body and every copy's alike):

- The clip is sampled on the blow's own clock, so hit-stop (the clock stops for 0.055 s on a landed blow) and Melee Speed carry over, and
  put on the body after the Animator (`PlayerLocomotion.SetBakedPose`), eased in over 0.09 s and out over 0.2 s once the blow is done.
- A punch taking over from one still recovering crossfades from the pose shown over 0.1 s (`BakedPose.MixBodyFrom`).
- **The stride is as long as the body really went.** The clip's feet are planted for the full stride; a body stopped short by whoever it
  is hitting (most second punches of a string) has gone less far, and the planted foot would slide forward under a body standing still.
  The driver measures how far the body has gone since the blow began against how far it was meant to (`StrideShare`), and
  `PlayerLocomotion.ApplyFistStride` draws each foot that share of its distance ahead of or behind the body (two-bone IK, the foot's
  height and turn kept). It works the same on a copy, from the copy's own movement.
- Not done: the feet on a slope or a stair. The clips stand on flat ground.

### Where it lives

- `FistStrikes` (the blows' names, lengths, contact moments and strides; the ready stance), `FistPoseDriver` (plays a blow on any body,
  holds the ready stance, hit-stop, the stride's share), `PlayerLocomotion.Fists.cs` (`ApplyFistStride`; `SetFacingOverride` turns the body
  to the target), `PlayerLocomotion.SwordPose.cs` (`StanceWanted`: the footwork takes the sword's stance while a sword is in hand, else the
  fists').
- `MeleeAttacker`: the chain, the heavy and its held follow-up (`_heavyFollowUpRemaining`), the stamina, the reach, the knockdown
  (`KnocksDown`), the stride (`CarryIntoPunch`).
- `PlayerInputReader.Combat.cs`: the punch goes out on the press; held past 0.4 s and let go it is the overhand; the body turns (1200 deg/s) to the
  nearest enemy within 70 degrees of where the camera looks and within 4.5 m (`MeleeAttacker.TryFindMagnetYaw`), else to the camera.
- Multiplayer: the strike's name travels as before (`PlayerAnimationDriver.StatePlayed`); a copy plays the same clip, stance and all, and
  its body's movement arrives with its transform.

### Changing a punch

Edit its keys in `fist_pose.py` (a key is one moment: where each wrist is, which way each elbow points, the hips' and shoulders' turn, the
lean, how far the hips sink and where they are over the feet; the feet are steps on the ground), then:

`"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/FistCombo/fist_build.py -- --render <folder> --export CityX/Assets/CityX/Character/Resources/Fists/FistMotion.json`

It prints how far each arm reaches, the tightest knee and the furthest a leg is asked to reach, and renders every punch's moments from
the side, the front and the game's camera. A blow's length, landing or stride changed there must be changed in `FistStrikes` too.

## Tests

`FistStrikesTests` (the clips read on the skeleton: on the game's bones and as long as their blows; whole poses, nothing snapping; every
blow starts and ends standing ready; the fist at full reach ahead of the body on the frame it lands; the straight drawn back first, the
overhand down from high, the uppercut up from a dip; the body turned into the blow from the hips up with the head kept ahead; the left
straight the right one mirrored; the lead foot strides and the rear foot stays where it stood while the body is carried past it; the
weight back, then forward; the stride done when the blow lands and no further; two hits guaranteed and not three; weaker than a sword and
costs stamina), `FistStancePlayTests` (on the real body: the hands in the stance are where they hang standing plain; no snap as a punch
takes over), `CombatTuningTests` and `BlockAndHeavyTests` (the chain's shape, the knockdown's zero stun, the overhand above every
straight, the uppercut after it), `MeleeCombatPlayTests` (reach, buffering, cancel, the closing punch recovered in full and the chain
restarting, overhand then uppercut, stamina, the body rooted and carried by the stride), `RealInputPunchPlayTests` (a real mouse and
keyboard: the punch is on the press, the body steps into it, it turns to the enemy near the camera, hold-and-release is the overhand),
`NetworkedActionSyncPlayTests` (a copy punches), `PlayerHudPortraitPlayTests` (a portrait is not taken with the fist up). `FistCapture`
(Explicit, -Graphics) renders a contact sheet of the four punches to `Logs/CityShots/fist-sheet.png` (side) and `fist-sheet-front.png`
(three-quarter), and the stance beside the plain body to `fist-stance.png`.

## History

- First pass (2026-10): the two Mixamo jab clips froze the feet, punched on the release and all looked alike; replaced by five procedural boxing
  punches (jab, cross, hook, uppercut, haymaker) from a guard at the face, with punch magnetism, hit-stop, buffering and the body kept walking.
  Those are gone: the owner wanted Souls' brawler, and the fists (10 a hit at 2.5 m for nothing) were "too op".
- 2026-11-03: Souls' four blows, posed in code (`FistStrikes.Evaluate`: the throwing arm by two-bone IK along a typed-in path, a twist and a
  lean of the spine, the walk cycle's legs with a stance stepped by `SwordFootwork`). The owner: "the melee combat just feels like shit all
  around there needs to be some type of full body animation/movement just the arms doesnt look right". Replaced on 2026-11-09 by the clips.
- Clips looked at then: FiveMCombatOverhaul (Rockstar's/a modder's assets, not usable); the project's `boxing_*` and `heavy_punch` clips are unused.

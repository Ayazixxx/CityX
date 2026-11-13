# The sword's attacks with the whole body

Owner, 2026-11-13, after the first online duel: "the sword combat animations are stilted and awkward. they only use the arms so It looks
odd. try to refine the combat animations so that they are telegraphed, have directionality, and the animation involves the whole body
akin to dark souls combat."

Asked how far to go, the owner chose both:

- **A swing is a commitment.** The body does not walk under it; the attack's own step carries it, and it is aimed by turning before it
  lands. (It kept 65% of walking pace under a swing until now, the legs walking on under an arm.)
- **Slower, readable wind-ups.** A light lands about 0.4 s after the click (0.28 before), a two-handed one about 0.47 s (0.34), and the
  heavies are held at the top for a third of a second or more before they come down. The damage went up with the cycle, so a sword does
  a second what it did.

## Who moves what

Until now every attack was a handful of keys of the sword arm, with the torso turned a little and the walk's legs stepped under it
(`SwordStrikes`, docs/SOULSLIKE_COMBAT.md). Now each attack has two halves, on one clock:

- **The legs, the hips, the torso and the head are a clip** made in Blender (`Resources/Swords/SwordMotion.json`): the stride, the coil
  against the cut, the weight going back and then through, the fold after a blow from above.
- **The sword arm, the blade, the free arm and the hand on the handle are still `SwordStrikes`' keys**, posed by the game every frame
  (`PlayerLocomotion.ApplySwordPose`, the support hand's IK, `KeepArmsOutOfTorso`), solved from the shoulders the clip has turned. They
  suit the game's hold (the handle across the palm) on whichever sword is held, and they keep the arms out of the body.

A first draft made the arms in Blender too. In the game's hold the forearm runs along the blade's edge, not along the blade, and arms
keyed without that bent the wrists double and went through the chest (owner, of a render: "why are his wrists all twisted and poking
through his torso"). The arms were taken out of the clips for that: `SwordArmsClearPlayTests` now measures, on every frame of every
attack with every kind of sword, that no arm is in the torso or the head and that the sword wrist is not wrung.

An attack with no clip (the knife's thrust), a rebound, a guard and the ready stance are the keys alone, as before.

## The attacks

Each is one clear direction, wound up with the whole body against it:

| Attack | The wind-up (the tell) | The blow | Lands at | Step |
|---|---|---|---|---|
| `sword1h_slash` (light 1) | blade drawn back out to the right behind the turned shoulders, weight on the back foot, free hand out ahead | level, right to left at chest height, a stride onto the left foot | 0.40 s | 0.45 m |
| `sword1h_cut` (light 2) | hand carried up past the left shoulder, blade behind the back, shoulders turned away left | down from the left shoulder to the right hip, the right foot striding through | 0.37 s | 0.50 m |
| `sword1h_finisher` (light 3) | blade high behind the right shoulder, the body arched back, the free arm reaching at the target, the left knee up | down to the lower left with the whole body driving after it | 0.55 s | 0.55 m |
| `sword1h_overhead` (heavy) | sword over and behind the head, held 0.3 s, the free hand out at the target | straight down the middle to the ground, the body folding after it | 0.77 s | 0.50 m |
| `sword2h_sweep` (light 1) | both hands at the right side, the blade back, the shoulders right round after it | level, right to left, a stride | 0.47 s | 0.45 m |
| `sword2h_reverse` (light 2) | the blade carried round to the left side | level, left to right, the right foot through | 0.44 s | 0.50 m |
| `sword2h_cut` (light 3) | blade high behind the right shoulder, the body coiled and sunk | down to the lower left, a long stride | 0.55 s | 0.55 m |
| `sword2h_smash` (a hammer's light 3) | both hands over the head | straight down | 0.49 s | 0.40 m |
| `sword2h_slam` (heavy) | sword over and behind the head, held 0.38 s, the body arched back under it | everything into the ground | 0.96 s | 0.50 m |

In every one: the lead foot strides and is planted before the blow; the hips turn first and the shoulders after them, and the game's
blade comes last and fastest; the weight goes back over the rear foot in the wind-up and forward over the front one for the blow; the
rear heel comes up and that foot steps up after; the head stays on what is ahead. No leg is ever locked straight: driving off the rear
foot the hips outrun it, and past nine tenths of the leg's length the ankle gives toward the hip (the heel rising, the toes dragging)
rather than the knee snapping straight. The heavies go up by way of their light's own wind-up, because a held click turns the light
already winding up into the heavy from the pose it has reached.

## How the clips are made

`CityX/SourceArt/SwordMoves/`, on the game's own skeleton in Blender, headless:

`"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/SwordMoves/sword_moves_build.py -- --render <folder> --export CityX/Assets/CityX/Character/Resources/Swords/SwordMotion.json`

- `sword_moves_pose.py`: each attack's keys (a key is one moment: the hips' and the shoulders' turn, the lean, the tilt, how far the hips
  sink and where they are over the feet; the feet are steps on the ground, `SourceArt/planted_feet.py`). The phases' seconds and the step
  are the game's (`SwordStrikes.TimingOf`, `StepMetres`): change one, change the other. The build prints each attack's tightest knee and
  how much of the leg's length it uses.
- **Only ten bones are exported** (hips, spine, neck, head, both legs): `octaslash_export.py` takes `EXPORT_BONES`. The arms seen in the
  build's renders are stand-ins carried before the chest.
- `sword_hold.py`: the stand-in sword in the game's own hold.
- The renders show each attack's moments from the side, the front and the game's camera. What the game shows is rendered by
  `SwordStrikeCapture.RenderTheSwordAttacks` (`-Graphics`): every attack with every kind of sword, from the side, the front and behind.

## How they are played

`SwordPoseDriver`, on the owner's body and every copy alike (an attack travels by name, as before):

- The clip is sampled on the attack's own clock (`PlayerLocomotion.SetBakedPose`). A hit-stop, Melee Speed and an enemy's tell hold it
  as they hold the arm's pose.
- **A clip with no arm bones is laid on before the arms are posed** (`BakedPose.LeavesTheArms`, `PlayerLocomotion.ApplyBakedTrunk`): the
  sword arm is then solved from the shoulder the clip has turned, the left hand reaches the handle from its own, and the elbows are
  turned out of the torso after both. The keys' own torso turn gives way to the clip's by the clip's weight. Every other baked move
  (the spin, the Octaslash, a reload, a punch) poses its own arms and is laid on after the arm poses, as before.
- **The arm turns with the shoulders** (`PlayerLocomotion.ArmAxesUnderTheClip`). A key puts the hand at an offset from the shoulder
  worked out for the shoulders turned by the key's own twist; a clip coils them further (70 degrees where the key has 45), and with the
  offset left in the body's axes the lower hand of a two-handed grip went 19 cm into the chest. So the arm's whole frame (hand, elbow,
  blade, free arm) is turned about the vertical by as far as the clip's shoulders are past the key's twist: the wind-ups draw the blade
  further back, the follow-throughs carry it further through, and at the contact, where the clips' twist is the keys' own, the blade
  lands where the keys aim it.
- From standing or walking, the clip comes in across the first three fifths of the wind-up, so the body goes straight into the coil;
  it is let go over 0.2 s. The next cut of a chain, or a light turned into the heavy, crossfades from the pose shown over 0.12 s.
- **The step** (`SoulsSwordCombat.ApplyLunge`, `SwordStrikes.StepShare`): from halfway through the startup to the end of the live window,
  which is how the clip's feet were planted. A body stopped short by whoever it is hitting has gone less far, and its stride is drawn in
  by that share (`PlayerLocomotion.SetClipStride`, shared with the punches).
- A rebound off a guard takes the body from the clip in 0.06 s; a guard raised in the recovery's tail, or a dash, in 0.2 s.
- The baked pose is one slot: if a spin, the Octaslash or a reload takes it, the attack's clip lets go.

## Tests

- `SwordMotionTests` (EditMode): the clips read bone by bone. As long as their attacks, whole poses, no bone turning more than 30
  degrees in a frame, standing ready at both ends, wound up away from the cut and turned through it, the weight back then forward, one
  foot striding and planted before the blow while the other stays put, no arm bone.
- `SwordChainFeetPlayTests`: both light chains thrown on the real body, the feet measured in the world.
- `SwordArmsClearPlayTests`: the arms out of the torso and the head on every frame of every attack with five kinds of sword. Measured
  2026-11-13 with the clips and without (the arms alone, as they were): elbows and arm bones 3 cm into the torso at the worst (4
  without), the head cleared by 4 cm or more (0 without), the lower hand's wrist 6 cm into the belly's round where the two-handed
  ready stance already rests it (5 without).
- `SoulsCombatRhythmTests`, `SoulsSwordRealInputPlayTests`: the commitment, the timings, the damage a second.

## Not done

- **The knife's thrust** has no clip (it is posed from its keys); its two cuts are the sword's, drawn in to its shorter step.
- **Slopes and stairs**: the clips stand on flat ground.
- **Joins**: a chain's next cut starts from standing ready and is crossfaded into from the last one's recovery; clips made for each join
  would be cleaner.
- **The arms' keys** are the ones the owner found stilted on a still body. Under a body that now coils and strides they read
  differently, but they have not been re-keyed.
- **The sword wrist bends hard at the ends of the cuts**: 160 degrees at a backhand's follow-through, 120 as a heavy lands (the same
  with the clips and without). The hold has the blade square to the forearm and the keys aim the blade and its edge with no regard for
  the forearm, so the wrist takes up whatever is left. To be dealt with together with the fingers' grip.

# The stab dash (Sword tree): the Mii Swordfighter's Power Thrust

Asked for (2026-10-18), with SmashWiki's hitbox GIF of the Mii Swordfighter's grounded Power Thrust
(`Downloads/MiiSwordfighterPowerThrustGroundedSSBU-2199574555.gif`): "make a hotbar skill for the swordsman called stab dash where it pretty
much goes the same distance as a dash but he does a stab like the gif attached". "The swordsman" is read as the Sword tree; the swordsman NPC
does not use it.

## What Power Thrust is (Smash Ultimate, 60 fps)

Source: UltimateFrameData (Mii Swordfighter, Power Thrust, grounded).
- First hit on frame 12; hits through frame 30 in three bands: 15% (frames 12-14), 13% (15-22), 10% (23-30).
- Interruptible on frame 59. No cooldown: the lag is the cost.

**The GIF, frame by frame** (34 frames, one per game frame: GIF frame g is game frame g + 1). The GIF ends on frame 34, before the recovery.
- Frames 1-4: from the sword stance into a wide, deep squat, side-on to where it will go, the sword hand drawn back past the shoulder with the
  blade pointing ahead and down beside the body, the free fist out ahead at chest height.
- Frames 4-9: the squat deepening, the weight going back (the head moves back by a quarter of the body's height).
- Frames 10-12: the release: the body unwinds to face ahead, the sword comes through at the hip, then the arm is thrust out straight (frame 12,
  the first and strongest hit).
- Frames 12-31: a very low lunge, held while the body slides: the torso pitched far forward, the sword arm and blade dead level, the back leg
  stretched out behind, the free arm cocked up behind the shoulders.
- The slide, measured on the GIF (the back foot's place, frame by frame): moving off on frame 11, fastest on frame 12, slowing all the way,
  still by frame 35. Per frame, in its pixels: 40, 90, 84, 74, 64, 60, 56, 50, 42, 34, 28, 28, 26, 24, 22, 20, 18, 16, 14, 12, 10, 8, 4, 2.

## What the game does

All the numbers are in [StabDashMove.cs](../CityX/Assets/CityX/Character/Abilities/StabDashMove.cs), as frame counts at 60 fps
(`StabDashMoveTests`).

| | |
|---|---|
| Key | its slot on the bar, with a sword in hand (the Sword tree's fourth star, after Sword Spin) |
| Wind-up | frames 0-11: the squat and the sword drawn back; the body turns to where the camera looks |
| Slide | frames 11-35: two dashes, 9.6 m (owner 2026-10-18: "increase the dash stab distance by 2x"; it was a dash's 4.8 m) where the camera looked as it left, level, shaped as the GIF's: off at about 62 m/s, slowing to a stop |
| Hits | frames 12-30, each target once, 1.5x a light swing (frames 12-14), 1.3x (15-22), 1.0x (23-30): Smash's percent at the spin's 1% = 0.1. A clean hit (12-14) throws what can be thrown on along the stab and up |
| Reach | 3 m ahead of the chest (the arm and the 2.1 m blade), 35 degrees either side (a swing's is 60) |
| Holds the body | 59 frames, from the key: nothing else is read (no walking, jumping, dashing or swinging out of it) |
| Walls | a wall, or a body it runs into, stops the slide there; the thrust still hurts until frame 30 |
| After | 0.5 s before another (Cooldown Reduction shortens it) |
| A stagger | during the wind-up it cancels the move; once the lunge has left, it goes on |

- The slide moves the body's CharacterController itself by what the curve still owes each frame (as a sword swing's lunge does), so a long
  frame cannot carry it further, and gravity still holds it to a hill.
- Bloodrage and Strength scale the thrust as any sword blow (`MeleeAttacker.LandSwordCut`, which since this takes a narrower arc and a set of
  targets already hit).
- No sound of its own yet: the hits play the impact sounds.

### The look

The move is ANIMATED IN BLENDER from the GIF and played bone by bone after the Animator, as the sword spin and Octaslash are (`BakedMotion`,
`PlayerLocomotion.SetBakedPose`). The clip's frame f is the move's frame f. The game moves the body along the slide and turns it to face the
stab; the clip only sinks, lunges and rises within the body. The lunge is centred over the body's capsule (its hips a little behind it), so
a stab stopped by a wall does not bury the head in it.

The recovery (frames 35-59) is not in the GIF: the lunge held a little, then the back leg drawn in, the body rising and the blade brought back
up into the sword stance, which the clip ends in exactly (it is handed back over its last 8 frames).

**Fixed 2026-10-18, the owner's two reports** ("the left arm is being bent the wrong way like its broken, and the blade isnt even pointed
straight horizontally", with a screenshot holding the combat sword):
- **The free arm.** The GIF's fist cocked up behind the head, on this body pitched far forward, hung the upper arm down the chest and folded
  the forearm back up past it: from in front, an elbow bent backwards. It is now a fencer's rear arm: the upper arm out to the side at the
  shoulder's height, the forearm up. The release (frame 10) is solved halfway between the drawn-back pose and the next, from their settings
  rather than by turning the bones between them, which had swung the blade straight up and flipped the free arm round. Checked from in
  front on the free arm's side as well as from the side and behind (`stab_render.py` view `frontleft`).
- **The blade, and the grip** (then: "make him grip the sword properly too as well as pointing it horizontally"). The move had been posed
  with the two-handed sword's hold, and the one-handed combat sword is held straight out past the thumb, so its blade pointed down at the
  street. Holding it the two-handed way instead levelled the blade but laid its long handle (47 cm) along the forearm, the fingers closing on
  nothing. Now the move is posed with, and the game holds every sword in, a THRUST GRIP (`SwordWeapon.ThrustTiltDegrees`, `FistPoint`,
  `ThrustGripRotation`; `stab_grip.py` puts the Blender rig's sword in the same): the handle in the fist where the one-handed hold has it,
  turned within the fist so the blade comes out between the thumb and the index finger leaning 50 degrees toward the fingertips, the guard at
  the knuckles (the combat sword keeps its own tuned place along the handle), the pommel under the wrist. While the stab holds the body the
  sword is in it (`BakedPose.ThrustGrip`, `ISwordBlade.HoldForThrust`), and back in its own hold as the move lets go.
  - The sword arm reaches a little down (20 degrees) through the lunge, so the blade carries on level from a closed fist with the handle's tail
    under the forearm rather than along it; the wind-up is a spear's chamber, the sword hand drawn back beside the chest with the blade aimed
    ahead and a little down (drawn far back behind the head, as the GIF has it, this grip bent the wrist right back).
  - The pose solver now also counts the hand's twist round its forearm when it picks how the blade rolls in the hand (`SOLVER_TWIST_WEIGHT`
    in `octaslash_pose.py`, 0 unless a move asks, so Octaslash and the spin solve as before): picked for the least wrist bend alone, the
    thrust grip wrung the forearm into a ribbon at the wrist.
  - The spin and Octaslash are posed with the two-handed hold and keep the sword in its own hold, as before.

`StabDashCapture` (an Explicit PlayMode test, needs `-Graphics`) films it in the city from the side (Smash's view), from behind (the game's
camera) and from in front on the free arm's side, following the body, the sword hand close up from its side and from in front, and from one
point beside the slide to show its length, with the two-handed sword (`Logs/CityShots/stabdash-*.png`) and the one-handed combat sword
(`stabdash-1h-*.png`).

### Editing the animation

The files are in `CityX/SourceArt/StabDash/`:
- `stab_pose.py` holds the poses, written in the character's own frame (Unity axes) like the spin's, and the keys between them.
- `stab_build_all.py` does everything headless:
  `blender --background --python stab_build_all.py -- [--no-export] [--render <dir> [--frames 0,4,9,12,20]]`. It builds the rig
  (`../Octaslash/octaslash_rig.py`), loads Octaslash's pose solver, bakes, exports `Character/Resources/StabDash/StabDashMotion.json`
  (`../Octaslash/octaslash_export.py`) and renders check frames (`stab_render.py`: side, behind, front).

### Multiplayer

`StabDashAbility` implements `IReplicatedAbility` with the id `stab-dash` and sends two events: begun (with where the camera looked) and
ended. A copy plays the same animation on the same timeline from the start, hurts nothing, and lets go when the end arrives early (a wind-up
cut short by a stagger). The slide and the facing are ordinary movement and arrive with the body's own transform. Both kinds of body get it
through `PlayerAbilityWiring`.

### Tests

- `StabDashMoveTests` (EditMode): the hit frames and the three damage bands, the clean hit, frame 59; the slide two dashes long from frame 11 to
  35, fastest off the mark and slowing all the way; the animation on BodyRig's bones and as long as the move, the squat with the sword drawn
  back, the blade straight ahead on a straight arm on every hit frame, the low lunge with the back leg stretched out and the free arm raised,
  its elbow lifted out to the side (never hanging with the forearm folded back up), ending in the stance it started from, nothing snapping
  but the dip and the thrust.
- `StabDashPlayTests` (real keys, real city): the squat, then the lunge two dashes long where the camera looks, facing it, low, the arm out,
  running through a target beside the line once and throwing it, the blade straight ahead and level; walking and jumping do nothing
  meanwhile; a wall stops the slide short; without a sword nothing happens; with the one-handed combat sword the blade is straight and level
  too, the handle running through the fist and across it (not along the hand), and the sword is back in its own hold after.
- `AbilityPlaybackPlayTests.AnotherPlayersStabDashLungesOnTheirBodyThenLetsGoAndHurtsNothing`.
- `SkillTreeTests`: a star of the Sword tree, needing a sword, its id the ability's.

### Not done

- The aerial Power Thrust (it dives downward in Smash); in the air this one lunges level as on the ground, falling as it goes.
- No sound of its own, no light (a blade's light trail draws nothing for a thrust along the blade's own line).
- The swordsman NPC does not use it.

## Tuning

The timeline, the slide's shape, the damage, the reach and the cooldown are constants in `StabDashMove`; the poses are the animation's
(`stab_pose.py`).

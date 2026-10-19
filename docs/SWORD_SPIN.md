# The sword spin (key 2): Link's Up Special

Asked for: "research link's up special from smash bros and make the 2 key sword spin more like that ... make it so you can use it on
ground or if you jump and use it then it goes upward like smash bros ... research all the details about it like how long it lasts."

## What Link's Spin Attack is (Smash Ultimate, 60 fps)

Sources: SmashWiki "Link (SSBU) / Up special" and "Spin Attack", UltimateFrameData (Link), KuroganeHammer (Smash 4).

**Grounded.** One slow, undulating spin (the speed swells and dips as it turns).
- Hitboxes frames 7-39. Damage falls with how late it connects: clean (7-8) 14%, mid (9-13) 12%, late (14-23) 9%, latest (24-39) 7%.
- **Chargeable**, ground only (Brawl onward): hold for up to 60 frames; damage x (1 + 0.6 x charge/60), so up to 1.6x (22.4% clean).
- Animation 94 frames (91 uncharged); interruptible at frame 77 (74 uncharged). No cooldown: the lag is the cost.

**Aerial.** A fast multi-hit spin that climbs.
- Eight blows, alternating front and back: front 4% at frames 8, 16, 22, 31; back 2% at 12, 19, 26, 38; a last blow at 47-49 (4%) with high
  upward knockback.
- Rises: initial vertical speed 2.4 (Smash units) under 0.55 gravity, air speed x0.5 while it turns; "moderate" height.
- Animation 91 frames. **Landing before it ends costs 30 frames of landing lag.** Link can use it once per time in the air (it is a recovery).
- Smash 4 buffed the height and the momentum kept from before; Ultimate made every version harder to escape.

## How Smash animates it (2026-10-17)

Owner: "the animation looks really bad and feels slow. research how smash bros does their animation and try to replicate it", with
SmashWiki's hitbox GIFs of Ultimate's grounded and aerial Spin Attack (and Melee's grounded one). A GIF pasted into chat arrives as one
still frame, so the files were split into every frame (System.Drawing) and read one game frame at a time.

**How Smash makes a move.** Every move in Smash is keyframed by hand, no motion capture, in four stages Sakurai sets with poses of
action figures: stand-by, wind-up, strike, follow-through. The wind-up is short, the strike is the strongest pose (the game holds it on a
hit), and the follow-through is usually the longest part, the window where a miss is punished. Sakurai's rule for attacks is to push the
rig past what a body could do: "almost no such thing as too far". Sources: a GDC talk on Brawl's production
(https://www.gameanim.com/?p=208) and Sakurai's video on exaggeration
(https://www.gonintendo.com/contents/11218-masahiro-sakurai-s-latest-video-details-over-exaggeration-in-animation).

**The grounded spin, frame by frame** (Ultimate):
- Frames 1-4, the wind-up: a quick dip into a deep lunge, the body wound to the right, the sword cocked high over the right shoulder with
  the blade laid back. A held charge waits here (SmashWiki: charging falls on frames 3-4).
- Frames 5-7: the release, whipping round to the left. On frame 7, the first and strongest hit, the blade points straight ahead.
- Frames 7-39: four turns to the left, low and wide, both arms spread: the sword arm straight out with the blade carrying on its line, the
  shield arm out the other way. The first turn takes 8 frames, the next 11, then 12: fastest at the start. That is four turns in 0.7 s,
  where this game's old spin did two in 0.8 s.
- Frames 39-49: the last turn slows, the arm comes down, and the body ends facing ahead in a deep lunge, the blade low in front.
- Frames 49-69: that lunge, held. Frames 70-76: rising a little, the blade lifting ahead.

**The aerial spin** (Ultimate): it starts turned to the right with the sword out behind and the knees tucked (one high, one hanging),
whips round to the left by frame 8, then makes four and a half turns, faster than the ground one (a turn every 6-9 frames). Every front
blow lands with the blade straight ahead and every back blow with it straight behind. The spin is tipped: the blade passes low in front and
high behind. The last turn swings the blade up from low beside the body to straight overhead (the launching cut, frames 47-49). It is held
there, then lowered out behind as the turning dies away, and the guard comes back by frame 91.

## What the game does

All of the timeline is in [SwordSpinMove.cs](../CityX/Assets/CityX/Character/Abilities/SwordSpinMove.cs) as frame counts at 60 fps, so it can be
checked against the above (`SwordSpinMoveTests`).

| | Ground | Air |
|---|---|---|
| Press | **hold 2**: sword drawn back, charging up to 1 s; **release** to spin (a quick tap spins at once) | 2 spins at once |
| Turn | 4 turns to the left (frames 7-49), fastest first, ending in a held lunge facing ahead | 4.5 turns (frames 5-48), up to 60 degrees a frame, tipped 12 degrees forward |
| Blows | each target once, 14/12/9/7% of the sword's damage by when it connects (x charge) | 8 blows 4%/2%, then a 12% blow (Smash: 4%) |
| Movement | rooted, no jumping out of it | **climbs**: starts at 7 m/s under 0.55 gravity (about 4 m, gravity easing back over 0.4 s after the last blow); **keeps the momentum it had** (sprint, jump, spin: the body keeps flowing) until it lands, the keys only steer it (14 m/s^2, half strength during the spin) |
| Holds the body | 74 frames | 91 frames; landing before then = 30 frames of landing lag |
| Reach | 2.8 m from the chest (the 2.1 m blade and the arm holding it out) | same |
| After | 0.4 s before another spin | once per time off the ground |

- **The last aerial blow launches** whatever can be launched (`ILaunchable`: pedestrians so far) up at 9 m/s and 3 m/s away. Smash's last hit
  is about knockback, not damage; in this game the damage had to be worth something too, hence 12% not 4%.
- **Bloodrage and Strength** scale every blow as they scale any melee hit.
- The spin used to be 1.2 s of a fixed 720 deg/s turn with the ordinary swing clip restarted every 0.3 s, a 4 s cooldown, and no rise.

### The look

The move is ANIMATED IN BLENDER from the GIFs and played bone by bone after the Animator, the way Octaslash is (`BakedMotion`,
`PlayerLocomotion.SetBakedPose`; see docs/OCTASLASH.md for the pipeline).

- **The turn is inside the animation**, on the hips. The body's own facing never moves: the camera and what the body faces are left alone,
  and another player's copy shows the whole spin from the animation, whatever its network rate. The clip's frame f is the move's frame f,
  so the blows land where the blade is.
- **The charge** plays the wind-up's first four frames, then loops the cocked pose (`ground_hold`), breathing, the blade trembling. Let go,
  the spin carries on from that frame (`SwordSpinMove.StartSeconds`), so a charged spin's first blow lands three frames later.
- **The weight**: the animation takes the body over in 3 frames and hands it back over the last 8 before the move lets go. An air spin
  that lands early lets go over those 8 frames while the landing lag runs.
- **The light trails from the real blade** (`BladeTrail`, the one Octaslash uses) while it sweeps: until frame 46 on the ground (the last
  turn has slowed into the lunge) and frame 51 in the air (the launching cut is overhead). It is drawn in the body's own space, so each turn
  leaves a ring of light round the body. The old swoosh prefab (three flat arcs that turned with the body) is gone; its material stays as
  the slash material (`SwordSlashMaterialBuilder`).
- **The free hand lets go of the handle** for the whole move: a baked pose places the free arm itself.
- `SwordSpinCapture` (an Explicit PlayMode test, needs `-Graphics`) films the charge, the ground spin and the air spin on the frames that
  matter, from the side (Smash's view) and from behind (the game's camera), to `Logs/CityShots/spin-*.png`.

### Editing the animation

The files are in `CityX/SourceArt/SwordSpin/`:
- `spin_pose.py` holds the poses (written in the character's own frame, as Octaslash's are) and the turn curve (`GROUND_YAW`, `AIR_YAW`:
  degrees to the left by frame through the GIFs' landmarks, a monotone cubic so it never turns back). The turn is put on each solved
  frame afterwards, about the upright through the feet.
- `spin_build_all.py` does everything headless, no Blender window needed:
  `blender --background --python spin_build_all.py -- [--no-export] [--render <dir> [--frames ground:4,7 air:8]]`. It builds the rig
  (`../Octaslash/octaslash_rig.py`), loads Octaslash's pose solver, bakes, exports
  `Character/Resources/SwordSpin/SwordSpinMotion.json` (`../Octaslash/octaslash_export.py`, now shared) and renders check frames
  (`spin_render.py`: side, behind, top).

### Multiplayer

`MeleeAttacker` implements `IReplicatedAbility` with the id `sword-spin` (the spin's code is `MeleeAttacker.Spin.cs`). The owner sends
charging, turning (air or ground, and the charge held) and ended. A copy plays the same animation and light on the same timeline from
those, hurts nothing, and lets go over the blend out when the end arrives early (an air spin that landed). The rise is ordinary movement
and arrives with the body's transform.

### Tests

- `SwordSpinMoveTests` (EditMode): the timeline against Smash's frames, and the animation itself: on BodyRig's bones and as long as the
  move; the ground spin winds up to the right, then turns four times to the left, fastest first, never turning back, and ends facing ahead;
  the first blow cuts straight ahead and the sword arm stays out, level, low and wide through the spin; the charge is the cocked pose with
  the sword behind and loops on itself; the air blows land with the blade ahead or behind and the last one swings it up; the air spin is
  the faster and ends facing ahead; nothing snaps outside the whirl. `BakedMotionPoses` reads a baked move without a scene (shared with
  `OctaslashMoveTests`).
- `MeleeCombatPlayTests`: the blows, the charge, the commitment, and the body's own facing never turning.
- `SwordSpinRealInputPlayTests` (real keys, real city): charging and releasing, the sword arm out along the chest's right with the free arm
  the other way, the light trailing from the blade and fading, the climb, the air blows and launch, the momentum, once per airtime, the
  landing lag.
- `AbilityPlaybackPlayTests.AnotherPlayersSwordSpinPlaysItsAnimationAndLightOnTheirBodyThenLetsGo`: a copy with a mounted sword crouches
  and whirls (its hips face every way), its own facing untouched, light from its blade, then lets go.

### Not done

- Only `Civilian` is launchable. Crimson Claws and dummies take the damage and stay put.
- No spin sound of its own yet (the blows play the impact sounds), and no charge glow.

## Tuning

Public fields on `MeleeAttacker` ("Sword Spin Attack"): `SpinReachMetres`, `SpinAttackCooldownSeconds`, `SpinAttackDamageScale`,
`AirSpinRiseSpeed`, `AirSpinGravityScale`, `AirSpinMoveFraction`, `AirSpinLaunchUp`, `AirSpinLaunchAway`, `SpinSlashMaterial`. The
timeline's numbers (frames, damage table, blend times) are constants in `SwordSpinMove`; the turn and the poses are the animation's.

## Fixed 2026-10-18: the blade level, gripped in the fist, with every sword

Owner: "right now the sword spin has the sword angled downward i want it either horizontally or slightly upward ... make sure the hand grip on the sword
look right as well". The spin was posed in Blender with the two-handed sword's hold, and each sword kept its own hold while it played; the one-handed
combat sword is held straight out past the thumb, so its blade pointed almost straight down through the whole spin (the two-handed one was level).
- The spin is now posed with the stab dash's **thrust grip** (`spin_build_all.py` runs `../StabDash/stab_grip.py` and the solver's twist weight), and
  the game holds every sword in that grip while the spin's animation holds the body (`MeleeAttacker.Spin`: `BakedPose.ThrustGrip`): the handle in a
  closed fist, the blade carrying on along the arm.
- Through the turns the blade rides a little above the arm (`spin_pose.BLADE_RISE`, about 8 degrees): level or slightly up, never down. The wind-up
  (sword cocked over the shoulder) and the ending (the deep lunge with the blade low in front, as in Smash) are unchanged.
- Tests: `SwordSpinMoveTests` measures the blade in the thrust grip (between 3 degrees down and 25 up on every hit frame of the ground spin);
  `SwordSpinRealInputPlayTests` checks the blade along the arm and level or up in the real game with both swords. `SwordSpinCapture` films both swords
  (`spin-*.png`, `spin-1h-*.png`) with the sword hand close up on frames 13, 20 and 33.

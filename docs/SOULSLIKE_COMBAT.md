# Soulslike sword combat

The sword is fought the way Dark Souls fights it. Fists, the bottle and every
other weapon keep the old `MeleeAttacker` combat; equipping a sword adds
`SoulsSwordCombat` to the body (`SwordWeapon.OnEquip`), and unequipping drops it.

Two kinds of sword since 2026-10-13: the **two-handed** greatsword and spike sword
(`/equip sword`, `/equip spikesword`), and the **one-handed** combat sword
(`/equip combatsword`, the owner's COMBATSWORDX.glb, `SwordWeapon.OneHanded`), each
with its own four attacks and guard. The one-handed sword's first attack is a
horizontal slash (the owner's choice).

## Controls

| Input | What it does |
|---|---|
| Left-click | Light attack. Two-handed: chains light 1 -> 2 -> 3 -> back to 1. One-handed: the two slashes back and forth (no thrust, owner 2026-10-17). You keep 65% of walking speed while swinging (40% until 2026-10-17). |
| Middle-click | Heavy attack: slow wind-up, big damage, long stagger, hyper-armour. (F stays unbound: explicit ask.) |
| Right-click (hold) | Guard. Its **first 0.2 s parries** (tap to parry, hold to block). Slows you to half speed (legs keep walking). Costs stamina per hit absorbed. |
| Double-tap Shift | Roll (dodge). Legal out of idle or the tail of a swing, never out of a wind-up. 0.42 s of i-frames. |
| 2 | The old spin attack (unchanged). |
| **Tab** | Lock on (works with or without a sword). Keep pressing to rotate through the surrounding enemies; the press after the last one releases. |

Every swing turns to face where the camera looks during its wind-up (1080 degrees a
second, `SwingTurnDegreesPerSecond`) and faces it exactly by the time the blade is
live -- and while locked on the camera is steering at the enemy, so the swing goes at
the enemy. (Until 2026-10-16 it snapped round on the click: half a turn in one frame
was one of the "awkward" turns.)

## The attacks are posed, not played from clips (2026-10-13)

The Mixamo two-handed chops were stiff, kept the blade close to the chest, and
were the same arc for a light and a heavy; there were none for one hand at all.
Each attack is now a handful of **keyframes of the sword** in `SwordStrikes`
(pure data): where the sword hand is (shoulder-relative, body axes, arm lengths),
which way the **blade** points and which way its **edge** faces, how the torso
twists, leans and tilts, how far the hips drop. They are joined the way a good
sword animation is built:

* a wind-up (smoothstep: anticipation is never faster than the action) into a
  short **hold** at the top;
* a **strike** that accelerates all the way into the contact (fastest at the
  moment it lands) along an arc (a Bezier through a control point) with the blade
  swept through a `Via` direction so a flat cut stays flat instead of flipping
  through the vertical;
* a **follow-through** past the target that decelerates, then a slower recovery
  to the ready stance;
* the torso winds up against the cut and unwinds through it; the heavies sink the
  hips (`HipDrop`, handed to the foot IK so the feet stay planted);
* the **edge leads** the motion (never the flat of the blade).

**Second pass, 2026-10-16** (owner: "my character holds and swings the sword
awkwardly ... make the character is not turned in any awkward way and make sure his
feet dont lock up during combat animations while moving", with a reference video
whose "two handed horizontal slashes" they liked, and "use the spikesword as guide ...
the side with the spikes" should hit). Research: the hips drive a cut and the
shoulders follow (koshi-mawari; the zwerchhau turns hips and shoulders into it); a
swordsman steps into every cut; the blade carries on through the target; the off
hand tracks the grip by IK; feet are latched where they land and step only once left
too far. What changed:

* **Cut planes**: a slash's blade turns within one plane (`SwordStrikes.CutPlane`:
  the flat forehand and backhand, the two diagonals, the overhead), and its edge is
  the plane's tangent -- the cutting side (the spike sword's spikes, its local -X)
  leads by construction. `SwordStrikesTests.TheEdgeLeadsEveryCut` on paper,
  `SwordStancePlayTests.TheSpikedEdgeLeadsEveryCutOnTheRig` on the real spike sword.
* **No dead stop at the contact**: the strike and the follow-through sweep the blade
  at the same average rate, so its speed is continuous through the hit
  (`TheBladeCarriesItsSpeedThroughTheContact`).
* **The hips lead**: each key has a hip turn as well as the shoulders'; the hips are
  turned first (`PlayerLocomotion.ApplySwordHips`, before the foot IK, legs re-solved
  under them) and the spine turns the rest, so the waist is never wrung more than
  25 degrees (`TheHipsLeadTheShouldersIntoTheCut`).
* **No stoop**: a posed swing or guard no longer follows the sword idle clip's spine
  (`TorsoFollowsClip` is off while the pose is active). That clip is bent forward
  over the sword, and following it bowed the body about 35 degrees into every cut
  (`SwordStancePlayTests.TheBodyStaysUprightThroughTheSwingsAndTheGuard`).
* **A stance and steps**: each key says which foot leads and how far apart the feet
  are; the stance opens into each cut and `SwordFootwork` steps the feet to it (see
  "The feet step under the sword"). The knees are bent: the ready stance crouches
  8 cm, the cuts 10-14, the heavies 16-20.
* **Arms out**: the cuts reach out to 0.9 arm lengths at the contact instead of
  staying at the chest.
* The **two-handed chain** is the reference's: a flat forehand, a flat backhand
  (the right foot passing forward), then the diagonal finisher.

| Attack | 1H (combat sword) | 2H (greatsword, spike sword, bat) |
|---|---|---|
| Light 1 | `sword1h_slash`: horizontal, right to left, chest height, 0.75 s | `sword2h_sweep`: FLAT FOREHAND, right to left at chest height, hips leading, the front foot striding, 0.85 s |
| Light 2 | `sword1h_cut`: backhand from over the left shoulder down to the right, the right foot stepping through, 0.8 s | `sword2h_reverse`: FLAT BACKHAND, left to right at chest height, the right foot passing forward, 0.85 s |
| Light 3 | `sword1h_thrust`: hilt to the hip, driven straight out in a lunge onto the right foot, 0.7 s | `sword2h_cut`: the finisher, a wide diagonal from over the right shoulder down to the lower left, a long step, 1.0 s |
| Heavy | `sword1h_overhead`: from behind the head down the middle, hips sinking, 1.1 s | `sword2h_slam`: overhead with the longest hold, everything into the floor, 1.5 s |
| Guard | `sword1h_guard`: blade across the body, free hand low | `sword2h_guard`: blade up across the front, both hands on the grip |

## The hold (the carry), 2026-10-16

The sword used to be held by the sword idle clip: arms straight out at shoulder
height, the blade straight up in front of the face, whether walking or standing. Now
`SwordPoseDriver` holds it whenever a sword is in the right hand and nothing else is
using the body (`SwordStrikes.Carry`): **low at the right side, the point forward
and down, the edge down**, both hands on the hilt; at a sprint the blade **trails
behind** instead (blended by the body's measured speed, 4.5 to 7 m/s). A one-handed
sword is carried by its own clips instead (see "the one-handed sword's own walk set"
below), with nothing posed over them. For 3 s after a
swing or guard the **ready stance** is held instead (a middle guard: the hands at the
belly, the point at the opponent's face, the left foot forward, the knees a little
bent), then it lowers into the carry over 0.45 s. The carry yields to anything that
owns the body (climbing, a ragdoll, a seat, the spin, a baked move, a dance, push-ups,
a spell's whole-body clip: `PlayerLocomotion.ClipOwnsTheBody`). Another player's copy
finds the same sword under the same hand and carries it the same way, with nothing
sent.

How it reaches the rig: `SwordPoseDriver` (added at runtime like `FistPoseDriver`)
runs the attack's clock -- a **hit-stop freezes it** (`SetSpeed 0`) -- and hands
each frame to `PlayerLocomotion.SwordPose.cs`, which after the Animator turns the
spine, solves the sword arm with the project's two-bone IK, turns the hand so the
blade points the way the frame says (`LookRotation(blade, edge) *
Inverse(LookRotation(bladeInHand, edgeInHand))`, from the mounted sword's own
hold), moves the roll into the **forearm** rather than the wrist
(`WristTwist.ShareWithForearm`, up to 100 degrees), keeps the **tip above the
ground** (`KeepTipAboveGround`), places the free hand (one-handed) or lets the
support hand follow the grip (two-handed). A new attack or a guard **takes over
from the pose the body is actually in** (the last swing's recovery) across a third
of its length, so a chain never snaps. The ready stance lingers 3 s after the last
swing, then lowers into the carry (see "The hold").

`SoulsAttack.FromStrike` derives each attack's Startup/Active/Recovery from the
strike's own `ActiveStart`/`ActiveEnd`, so the blade is live from just before the
contact into the follow-through. Pinned by
`SoulsSwordCombatPlayTests.TheActiveWindowSitsOnTheStrikesContact` and
`SwordStrikesTests` (fastest inside the live window, edge perpendicular, tip never
through the ground, two-handed poses within the left arm's reach, the slash flat and
right-to-left).

**Look at it:** `tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter
CityX.Tests.SwordStrikeCapture` writes `CityX/Logs/CityShots/strike-<spike|1h|crowbar|bat|2h>-<attack>.png`
(nine frames across each attack from the side, the front and behind; the spike
sword's spikes show which side leads), `strike-<sword>-carry.png` (the hold) and
`grip-<sword>.png` (the hands on the grips). `CityX.Tests.SwordWalkSwingCapture`
writes `swingwalk-<spike|1h>-<forward|sideways|standing>.png` (the light chain thrown
walking forward, sideways and standing, from the side and from behind) and logs the
feet: steps taken, how far a foot on the ground slid, how far it got from its hip.
The old clip-based menus (`CityX > Capture Souls Sword`, `Measure Sword Clips`) still
exist for the clips.

## The hands close round the grip

Every digit has two bones of its own now (`BodyRig`, `FusedBodyShape.Digits`,
not humanoid-mapped) and `HandGrip` folds them round whatever is mounted on the
hand: the sword hand always, the support hand once it is on the weapon. See
`docs/CHARACTER_LOOK.md` ("Finger bones and grips").

**Both hands on the grip, by IK (2026-10-16**, owner: "use inverse kinematic so the
hands reach the correct grip points on each weapon"). The right hand carries the
sword (it is mounted at the weapon's tuned grip); the left reaches for the weapon's
`LeftHandGrip` marker (two-bone IK, palm on the point). Now:

* wherever a cut takes the grip further from the left shoulder than that arm can
  reach, the whole sword is **drawn in toward it** (the right arm re-solved, the blade
  kept pointing the same way): the left palm stays within 1-2 cm of its grip point
  through every attack (it strayed up to 13 cm);
* the left hand is **turned onto the handle** (thumb up the handle toward the blade,
  the palm facing the side its forearm comes from) instead of keeping whatever wrist
  the walk clip gave it, faded out where the forearm runs along the handle (there the
  palm's side is undefined);
* the twist either wrist needs goes into its forearm **continuously**
  (`WristTwist.ShareWithForearm(..., ref last)`): a hand rolling through the half
  turn (a backhand's edge coming over) no longer flips the forearm 200 degrees in a
  frame.

## The feet step under the sword (SwordFootwork, 2026-10-16)

The attacks used to be posed over the base layer's walk cycle slowed to
`CommittedMoveSpeedFraction` (0.4): a blend of the idle and the walk that barely lifts
its feet, so swinging on the move slid each foot about 3.4 m along the ground in
three seconds with two real steps, and every lunge dragged them
(`SwordWalkSwingCapture` measured it). Now, while a sword attack is in progress (and
in the ready stance and the guard up to a guarded walk, 2.6 m/s), the feet are placed
by `SwordFootwork` instead of the clip:

* each foot has a **place** in the frame's stance (a little wider than the hips, the
  leading foot forward by half the stance depth, the back foot turned out 35
  degrees);
* a **planted foot never moves**; once it is left more than 0.18 leg lengths from
  its place (or as far as the body goes in 0.12 s, whichever is more), or turned 40
  degrees from it, it takes a 0.17 s **step** there, lifting about 10 cm, landing a
  little ahead of where its place is going;
* **one foot steps at a time** until the first is 60% through its step; the feet
  never cross;
* everything is kept within the **leg's reach** from its hip (`FootReach`: what the
  leg can reach along the ground with the hips as low as the crouch puts them), and a
  foot at the end of its reach steps at once -- a short-legged body standing tall
  reaches very little, which is also why the stance crouches;
* turning faster than 240 degrees a second (the swing's quick turn to the aim) the
  planted feet **pivot** with the body for the excess instead of staying while the
  hips spin over them;
* the lunge (`SoulsAttack.LungeMetres`) now surges from halfway through the wind-up to
  the end of the live window, eased, and the feet step with it;
* the foot IK puts the ankles where the stepper says (the planting's own locks stand
  aside), knees pointing the way the toes do.

Swinging while walking now takes about 17 steps in 3.4 s and the planted feet stay
put (`SwordStancePlayTests.SwingingWhileWalkingTheFeetStepAndDoNotSlide`); standing,
the feet step into the stance and with each lunge. The two-handed walk is still the
2hsw locomotion tree when not fighting (`LocomotionInput.SwordEquipped`). Pure rules
in `SwordFootworkTests`; the clip yardstick in `SoulsSwordRigPlayTests.TheLegsKeepWalking...`
(with the sparring dummy moved out of the way: walking into it, the body stopped and
the old clip marched on the spot, which those tests had counted as walking).
Another player's copy steps the same way from its own measured motion and the
attack's name (`SwordStancePlayTests.TheSwordIsCarriedLow...`).

## Lock-on

* **Tab** grabs the enemy nearest where you are looking (horizontal angle, max 65
  degrees, within 20m). Pressing again steps to the next enemy in a stable
  left-to-right sweep; after visiting them all the next press lets go.
* While locked the mouse is ignored and the camera swings to the target; the body
  faces it (moving or not), so **WASD strafes around the enemy** using the
  existing left/right/back walk clips.
* It lets go by itself when the target dies, goes out of range (>23m), is out of
  sight for more than 0.75s, or you die.
* **Pedestrians and story NPCs are not lockable** -- the ask was enemies. Anything
  else you can hit (crimson claws, dummies, other players) is.
* Code: `LockOn.cs` (pure rules), `ThirdPersonCamera.PressLockOn/TrackLock`,
  `LocomotionInput.LockedOn`; Tab is read in `PlayerInputReader`.

## The rules, and where they come from

Researched from how Dark Souls 3, Elden Ring and Sekiro actually behave (frame
data pages, the stamina/poise/i-frame tables, the animation guides on anticipation
and follow-through, and the PvP netcode write-ups). Numbers are theirs where they
have one, scaled to this game's 130-point stamina bar.

* **Commitment.** Every attack is `Startup -> Active -> Recovery`. You cannot walk
  out of it. The only exits are being staggered or rolling during Recovery.
* **Active frames only.** The blade hurts only during the Active window. Each
  target is hit at most once per swing.
* **Stamina.** Swings, rolls and blocked hits spend the one bar. Recovery pauses
  0.6s after spending, then refills at 45/s (DS3). You may act with any stamina
  left even if the cost is more than remains -- the bar bottoms out and you are
  exhausted until it recovers past 20%.
* **Input buffer.** A press is remembered for 0.45s -- or, pressed **during a
  swing**, until that swing's chain window has opened: a click in the wind-up is
  how a chain is asked for (`SecondsUntilChainWindow`). A press with nothing to
  chain to (mid-roll) expires after the 0.45s.
* **Heavy vs light.** The heavy has 2-3x the wind-up, more recovery, 2x the damage,
  a 0.9s stagger and hyper-armour. It is a decision, not an upgrade.
* **Roll i-frames.** 0.42s of invincibility (Elden Ring's medium roll: 13 frames
  at 30), 20 stamina.
* **Hit-stop, both sides.** A landed hit freezes the attacker's swing (its clock
  and its pose) for 0.07s (1.8x for a heavy), and the victim's recoil holds for the
  same instant. The freeze is sent to other players too (`StateSpeedChanged`), so a
  copy's swing freezes on contact.
* **Hit reactions.** Being hit shows now: `HitReactions` throws the torso and head
  away from the blow (four directions by name: `hit_front`...; a `stagger_*` when the
  blow took the controls away), played by `FlinchPoseDriver` and replicated like a
  swing. Nothing on the body reacted before.
* **Poise** (DS3's rule: it is only on inside attacks with hyper-armour). Every
  body has a 60-point pool that refills 20/s after 1.5s. A heavy's wind-up and
  active frames have **hyper-armour**: blows then only stagger once their poise
  damage has emptied the pool (lights take 25, a heavy 60 -- so a heavy breaks a
  heavy outright and it takes three lights to stop one). Outside hyper-armour every
  blow staggers, and a blow that carries no poise damage (a punch, a bullet, a claw)
  staggers through anything. `PlayerVitals.Poise/HyperArmour/TakePoise`.
* **The parry.** The guard's **first 0.2s** (`PerfectGuardSeconds`) is a perfect
  window: a blow caught then does no damage, costs no stamina, snaps the guard
  forward (`sword_parry`, replicated) and leaves the **attacker staggered for 0.9s**
  (`BlockedRecoilSeconds`: `HitResponse.Blocked`). The window closes on the blow it
  catches, and a tap of the button is all it takes.
* **Held guard.** Past its first instant the guard soaks: it costs stamina equal
  to the damage absorbed. A hit you cannot afford **breaks the guard**: 1s stagger,
  stamina to zero.
* **The guard rebound** (owner, 2026-10-17: "implement a stagger system for if you
  swing at a block"). A blow a held guard soaks (`HitResponse.Guarded`) now throws the
  attacker's blade back off it: stunned for `GuardReboundSeconds` (0.6s, shorter than
  a parry's 0.9s), the sword arm knocked up and back past the shoulder and the body
  leaning away (`sword1h_rebound` / `sword2h_rebound`, posed by
  `SwordPoseDriver.BeginRebound`, announced by name), then gathered into the ready
  stance. A parried attacker plays the same rebound for its longer stun. Every guarded
  or parried blow clashes: sparks and a ting (`PlayerVitals.GuardStruck` ->
  `BlockDeflectEffect`). Against another player the rebound comes back with the
  defender's verdict (`ApplyRemoteVerdict(Guarded)`). The swordsman NPC
  (docs/SWORDSMAN.md) punishes exactly this opening.

## Hit detection

Two volumes, both live only during Active:

1. **The blade**: a capsule swept along the real sword between last frame's pose
   and this frame's (no tunnelling through a fast swing).
2. **A reach box** in front of the wielder (`ReachMetres` 2.2, `ReachHalfWidth`
   0.8), kept from the clip days so a target at arm's length is never missed.
   `/sword ReachMetres 0` turns it off (blade only).

## Multiplayer (PvP)

* A swing, a guard, a parry and a hit reaction are each announced **by name**
  (`PlayerAnimationDriver.TimedStatePlayed` -> `NetworkedAnimatorSync`), so other
  players' copies run the same keyframes on their own rig. A hit-stop's freeze goes
  the same way (`StateSpeedChanged`).
* Damage is decided on the **attacker's** machine against the victim's copy and
  forwarded to the victim's owner (`NetworkedDamage`), with the blow's poise damage.
  **The verdict comes back**: the attacker hears "landed" at once (the hit-stop must
  not wait for a round trip), and when the victim's own body decided otherwise --
  parried, soaked, rolled through -- that answer is sent back with the hit's id and
  the attacker's `SoulsSwordCombat` takes it (`ApplyRemoteVerdict`): a parry or a
  soak by a held guard throws a remote attacker's blade back exactly as it does
  offline. The defender decides what
  happened to them (the research's rule for a dodge or a parry to feel fair).
* Still one-sided: the victim's flinch arrives with the network's delay, and a
  blow that reached a copy's hitbox on the attacker's screen is the blow that
  counts (favouring the attacker's view of contact, the usual choice).

## Tuning it live

`/sword` lists every number; `/sword <name> <value>` sets one:

```
/sword BufferSeconds 0.4
/sword Heavy.Startup 0.8
/sword OneHandLight1.PoiseDamage 30
/sword PerfectGuardSeconds 0.25
/sword ReachMetres 0
```

Fields per attack: `Startup Active Recovery ChainAt StaminaCost DamageScale
HitstunSeconds LungeMetres PoiseDamage` (and `HyperArmour`, `ClipSeconds ClipStart
ClipEnd` for a clip-backed one). Changes last until the sword is unequipped; bake
the winners into the defaults in `SoulsSwordCombat.cs`. The shape of an attack
itself is in `SwordStrikes.BuildKeys`: change a key, run the capture, look.

Where the hands sit on the hilt: the Inspector on the held sword (CityX > Weapon
Grips; a one-handed sword saves its own hold, `WeaponGripSettings.OneHand*`).

## Not built (deliberately)

* **Riposte / backstab** after a parry: the stagger is the opening for now.
* **Two-handing toggle, weapon arts, weapon weight -> roll speed.**
* **Hit-stop on the whole world** (the legs keep walking under a frozen swing).

## Known limitations

* The attacks are keyframed by hand from the research, not from reference
  animations: the owner may yet bring reference moves to recreate (the offer
  stands), and any attack is a few numbers to retune.
* Swinging on the move is a quick shuffle (about five steps a second at the swing's
  1.6 m/s): the legs are short for the speed. The steps' lengths and timing are a
  handful of constants at the top of `SwordFootwork`.
* The light strikes are fast (the greatsword's tip peaks around 80 m/s for a frame or
  two at the contact); there is no swing trail yet to carry the eye along the arc.
* Windows and numbers are from the research; *feel* (how long the wind-up should
  be, how big the heavy's stagger is, whether 0.2s is the right parry window for
  this game's ping) can only be judged by playing it, against another player.

## Update 2026-10-14: the head over a greatsword

Owner, with a screenshot of the greatsword held up and the face tipped to the sky: "why is he looking upward. make him looking straight like the
idle position ... make sure the head isnt too far forward or backward once rotating it ... use the idle head positioning as a guide". The sword's
standing clip (`2hsw_idle`, Mixamo) tipped the neck 23 degrees back (idle: 3 forward) and the face 56 degrees up (idle: 11, the rig's straight
ahead), carrying the head 13 cm behind the hips (idle: 2 cm ahead). `PlayerLocomotion.ApplySwordHeadCarriage` (after `ApplyHeadForward`, in the
sword gait) sets the neck and then the face to the idle's own carriage (`IdleNeckLeanDegrees`, `IdleFacePitchDegrees`), eased in and out; a cut
still turns the torso and the head with it. Measured after: face 11.0, neck 3.0, head 1.7 cm ahead. `SwordIdleHeadPlayTests` holds it within 5
degrees, 6 degrees and 6 cm of the idle (measured live, so a changed idle clip shows); `SwordIdleHeadCapture` (Explicit, -Graphics) renders both
side by side.

## 2026-10-14: the combat stats (docs/STATS.md)

- Melee Speed runs the swing clock (startup, active, recovery and the lunge) and the pose faster; other players see the faster swing.
- Block Chance: while the guard is up, everyone's 10% plus the stat to stop a blow outright -- no damage, no stamina, sparks and a ting (a bullet
  glances off). Armor Penetration cuts it.
- Stability: that chance to shrug off a staggering blow; a deeper poise pool for hyper-armoured swings; a cheaper guard (harder to break).
- Armor stops a share of blows and bullets, Magic Resistance of spells: rating/(rating + 100).

## Update 2026-10-15: the metal crowbar and the baseball bat

The owner's two models, fought with the sword combat (`SwordWeapon` on each): the **metal crowbar** is one-handed (the combat sword's
moves: a horizontal slash first) and the **baseball bat** two-handed (the greatsword's moves and hold). `/equip crowbar`, `/equip bat`.

- **Measured, not typed in** (`Weapons/Editor/HeldModelBaker.cs`): both glbs came in lying diagonally. The builder finds each model's long axis
  from its own vertices (the main eigenvector of their spread), which end is which (the crowbar's claw holds most of the vertices; the bat's
  barrel is the thicker end), turns it upright in the sword frame and bakes it into a plain mesh asset.
- **Crowbar** (`MetalCrowbarBuilder`, from `metalcrowbar.glb`, the owner's crowbar.glb renamed as asked): 1.0 m (a 0.77 m wrecking bar at the
  game's 1.3x), held 14% up from the straight end, the claw curling toward -X, the side a sword's edge leads with. Dark steel, metallic 0.9.
- **Bat** (`BaseballBatBuilder`, from `baseballbat.glb`): 1.12 m (an 86 cm bat), knob down, the bottom hand 8 cm over the knob so the
  greatsword's two-handed hold fits it. Unwrapped round its length for **the wood grain**, which `Weapons/WoodGrain.cs` draws in code the first
  time a bat appears (pale ash, wandering darker grain lines, varnish smoothness 0.35): no image file in the game.
- Seen by other players like every held item (`NetworkedPlayerWiring.ItemPrefabPaths`). Tests: `BluntWeaponTests`; `SwordStrikeCapture` renders
  `strike-crowbar-*`, `strike-bat-*`, `grip-crowbar.png`, `grip-bat.png`.
- Not done: blunt sounds (they use the sword's), and blunt hits drawing blood like a blade.

## Update 2026-10-17: the one-handed sword's own walk set

Owner, 2026-10-17: "the sword is like spinning around make you own sword running and walking animations and make them look good", then
"research how games do one handed sword running and walking animations and make ours work".

**What games do** (researched that day): the carry is a locomotion set of its own, apart from the combat stance; the sword arm hangs relaxed,
a little away from the body, and swings only a little, against the leg on its own side; the sword is held off to the side so it clears the
legs; the wrist stays near in line with the forearm (a hammer grip pointed down reads as a broken wrist); the arm trails a little to show the
weight; the parts overlap (upper arm, then forearm, then hand) rather than swinging as one piece. Sources: the Warbell hero-locomotion
write-up (sword arm phased against its own leg; carry apart from combat), animation critiques on polycount and student blogs (broken wrist,
tip clearance, trailing weight), Brian LeMay's arm-swing notes (overlap of no more than two drawings in sixteen).

**Why the blade spun before.** The sword sits across the fist, out past the thumb, so whatever pitches the forearm pitches the blade by the
same angle: the clips' forearm swinging through 80 degrees see-sawed the sword from the ground to straight ahead. Every runtime fix on top of
other clips (hand-driven carry, clip arm plus a limited wrist turn) still let the arm swing it.

**What it is now.** `SwordCarryBaker` (Character/Editor, run by PlayerRigBuilder before the controller is built, or CityX > Bake One-Handed
Sword Carry) bakes `Animations/Sword1H/sword1h_{idle,walk,run,walk_backward,walk_left,walk_right}.anim` from the game's own Godot clips
(GodotMotionBaker with a measure and an adjust hook): legs, hips, chest, head and free arm are the plain set's; the right arm is
`SwordCarryArm`'s, in real joint angles (shoulder swing and lift, upper-arm roll, elbow, forearm roll, wrist deviation and flex):

| | shoulder | lift | elbow | thumb turned out | wrist toward little finger |
|---|---|---|---|---|---|
| idle, walk, strafes, back | 12 deg back | 14 | 8 | 18 | 27 |
| run (sprint) | 22 deg back | 16 | 16 | 26 | 26 |

Its swing is read off the clip's own right arm (forward and back of its middle, in the chest's frame) and handed on in shares: the shoulder
0.3 of it walking, 0.2 running, 0.5 of the idle's sway standing; the elbow bends 0.15 more as it comes forward and the wrist leans the point
back down by 0.35 (0.3 running), both 0.06 of a cycle late (overlap). So the hand swings (27 cm walking against the free hand's 63, 38 against
103 sprinting) while the blade holds 19-28 degrees below level, 15-35 degrees out from the leg, turning at most 1-3 degrees a frame;
it clears the legs by at least 11 cm and the ground by 15 cm or more, with either hold (SwordCarryPlayTests). `Sword1HLocomotion` holds them at the plain tree's own
places, so the cadence, strides and foot placement are the plain walk's.

Nothing is posed over the carry: `SwordPoseDriver` lays `SwordStrikes.Carry` only on a two-handed sword now. An attack from the carry blends
in over its takeover time (30% of the attack) from the clips' arm; the ready stance after it lowers back into the clips over 0.6 s. The
one-handed default hold now runs the blade straight out past the thumb (`SwordWeapon.OneHandBladeAxisInHand`), as the owner holds it; tilted
15 degrees toward the fingers, as it was, the carried point dragged on the ground. The owner's six `1hsw_*` clips stay in
`Character/Animations` but are no longer played. `SwordCarryCapture` renders idle, walk, sprint, strafe and back from the side, from behind
and from the front-right (`carry-*.png`).

**The combat sword's edge is its +X.** Measured on the model: 2-7 mm thick on +X, 24-27 mm on -X, the whole length of the blade; every other
sword's edge is -X (the greatsword's profile, the spike sword's spikes). The code assumed -X for all of them, so the owner turned their saved
one-handed hold half round the blade to make the sword look right, and then every one-handed pose (the cuts, the guard, the ready stance)
rolled the hand half a turn to lead with the spine, and lowering the stance back into the carry snapped the hand round 87 degrees in a frame.
`SwordWeapon.EdgeOnPlusX` (set by CombatSwordBuilder) now says which side is sharp: `EdgeLocal`/`EdgeDirectionWorld` follow it, the sword
pose reads the edge from it, and the default one-handed hold turns that side to the knuckles (`DefaultOneHandGripRotationFor`), as the
owner's own hold does.

**The held one-handed stances bend at the elbow** (owner, same day, with a screenshot of the stance after a swing: "its like bent at the
wrong part of the arm"). The ready stance and the guard lay the forearm along the hand (`ForearmAlign`); with the real edge down in the ready
stance the knuckles face down, so a forearm straight along them came down from above and put the elbow up by the face. A fist holding a
sword up and forward cocks the wrist toward the little finger instead, so one-handed stances now bring the forearm in at
`PlayerLocomotion.HeldWristCockDegrees` (35) and let the hand give way (`HeldHandYield`) to where an arm with that wrist can put it: the
elbow hangs at the side (9 cm below the shoulder-to-hand line in the ready stance, 22 in the guard; it was 16 cm above), the wrist settles
at 35 degrees (53 for a moment while a swing hands over to the stance). Two-handed stances are unchanged. `SwordCarryCapture` renders
`carry-ready.png` and `carry-ready-walk.png` (a light attack, then the stance); `SwordCarryPlayTests.AfterASwingTheArmBendsDownAtTheElbow`.

**The free hand is relaxed** (owner, same day, with a screenshot: "i dont like the way left arm is in holding shield position after
swinging. make it relaxed or look at what most one handed sword animations do with the other hand when not holding anything"). It used to
be placed low in front of the stomach through every one-handed attack, the stance and the guard (`FreeHandReady`), which read as holding a
shield. With nothing in it, the empty hand is kept out of the way: the stance and the guard no longer place it at all
(`FreeHandWeight` 0, so the carry clips' arm hangs standing and swings walking), and through a cut it hangs by the hip
(`SwordStrikes.FreeHandRelaxed`, 96% of the arm's reach, a soft elbow) and swings against the sword for balance like a tennis player's
other arm (`FreeHandBalanceSwing`, forward while the sword is wound back, back as it crosses).

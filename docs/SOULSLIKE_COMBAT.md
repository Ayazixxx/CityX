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
| Left-click | Light attack. Chains light 1 -> 2 -> 3 -> back to 1. |
| Middle-click | Heavy attack: slow wind-up, big damage, long stagger, hyper-armour. (F stays unbound: explicit ask.) |
| Right-click (hold) | Guard. Its **first 0.2 s parries** (tap to parry, hold to block). Slows you to half speed (legs keep walking). Costs stamina per hit absorbed. |
| Double-tap Shift | Roll (dodge). Legal out of idle or the tail of a swing, never out of a wind-up. 0.42 s of i-frames. |
| 2 | The old spin attack (unchanged). |
| **Tab** | Lock on (works with or without a sword). Keep pressing to rotate through the surrounding enemies; the press after the last one releases. |

Every swing faces the camera the instant you click -- and while locked on the
camera is steering at the enemy, so the swing goes at the enemy.

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

| Attack | 1H (combat sword) | 2H (greatsword) |
|---|---|---|
| Light 1 | `sword1h_slash`: horizontal, right to left, chest height, 0.75 s | `sword2h_cut`: diagonal from over the right shoulder down to the left, 0.9 s |
| Light 2 | `sword1h_cut`: backhand from over the left shoulder down to the right, 0.8 s | `sword2h_reverse`: rising backhand from low left to upper right, 0.9 s |
| Light 3 | `sword1h_thrust`: hilt to the hip, driven straight out with a step, 0.7 s | `sword2h_sweep`: flat sweep right to left at waist height, 1.0 s |
| Heavy | `sword1h_overhead`: from behind the head down the middle, hips sinking, 1.1 s | `sword2h_slam`: overhead with the longest hold, everything into the floor, 1.5 s |
| Guard | `sword1h_guard`: blade across the body, free hand raised | `sword2h_guard`: blade up in front, both hands on the grip |

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
swing, then the arms go back to the walk cycle.

`SoulsAttack.FromStrike` derives each attack's Startup/Active/Recovery from the
strike's own `ActiveStart`/`ActiveEnd`, so the blade is live from just before the
contact into the follow-through. Pinned by
`SoulsSwordCombatPlayTests.TheActiveWindowSitsOnTheStrikesContact` and
`SwordStrikesTests` (fastest inside the live window, edge perpendicular, tip never
through the ground, two-handed poses within the left arm's reach, the slash flat and
right-to-left).

**Look at it:** `tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter
CityX.Tests.SwordStrikeCapture` writes `CityX/Logs/CityShots/strike-<1h|2h>-<attack>.png`
(nine frames across each attack from the side, the front and behind) and
`grip-<1h|2h>.png` (the hands on the grips). The old clip-based menus
(`CityX > Capture Souls Sword`, `Measure Sword Clips`) still exist for the clips.

## The hands close round the grip

Every digit has two bones of its own now (`BodyRig`, `FusedBodyShape.Digits`,
not humanoid-mapped) and `HandGrip` folds them round whatever is mounted on the
hand: the sword hand always, the support hand once it is on the weapon. See
`docs/CHARACTER_LOOK.md` ("Finger bones and grips").

## The legs keep walking under the sword

The attacks are posed over the **base layer's walk cycle**, so you can walk while
swinging, at `CommittedMoveSpeedFraction` (0.4) of walking speed, and at half speed
while guarding; the feet keep stepping (and the foot IK keeps them on the ground).
The two-handed carry is the 2hsw locomotion tree (`LocomotionInput.SwordEquipped`);
a one-handed sword is carried by the ordinary walk's own arm. The masked
`UpperBody` Animator layer is still there for anything that plays a clip; the
posed attacks do not use it. Verified by `SoulsSwordRigPlayTests.TheLegsKeepWalking...`.

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
  stamina to zero. A soaked hit does NOT punish the attacker (`HitResponse.Guarded`).

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
  the attacker's `SoulsSwordCombat` takes it (`ApplyRemoteVerdict`): a parry
  staggers a remote attacker exactly as it does offline. The defender decides what
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
* The two-handed sweep still takes the support hand a little off the grip at the
  very end of its follow-through (`SwordStrikeCapture` logs the worst distance).
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

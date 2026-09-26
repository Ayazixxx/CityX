# Soulslike sword combat

The sword is fought the way Dark Souls fights it. Fists, the bottle and every
other weapon keep the old `MeleeAttacker` combat; equipping a sword adds
`SoulsSwordCombat` to the body (`SwordWeapon.OnEquip`), and unequipping drops it.

## Controls

| Input | What it does |
|---|---|
| Left-click | Light attack. Chains light 1 -> 2 -> 3 -> back to 1. |
| *(no key yet)* | Heavy attack exists (slow wind-up, big damage, long stagger) but has no binding. F does nothing. |
| Right-click (hold) | Guard. Slows you to half speed (legs keep walking). Costs stamina per hit absorbed. |
| Double-tap Shift | Roll (dodge). Legal out of idle or the tail of a swing, never out of a wind-up. |
| 2 | The old spin attack (unchanged). |
| **Tab** | Lock on (works with or without a sword). Keep pressing to rotate through the surrounding enemies; the press after the last one releases. |

Every swing faces the camera the instant you click -- and while locked on the
camera is steering at the enemy, so the swing goes at the enemy.

## The legs keep walking under the sword

The Mixamo swing and guard clips are full-body; played on the base Animator
layer they overwrote the legs with a planted lunge, so the legs froze for the
whole swing. They now play on a masked **UpperBody layer** (spine, head, arms,
hands -- not hips or legs) over the top of the walk cycle, exactly as the pistol
got its own gun-walk. So:

* You can walk while swinging, at `CommittedMoveSpeedFraction` (0.4) of walking
  speed (`/sword CommittedMoveSpeedFraction 0` restores the old full root), and
  walk at half speed while guarding. The feet keep stepping.
* A swing's stretched playback speed is the layer's own `UpperSpeed` multiplier,
  **not** `Animator.speed`, so the legs are never sped up under a swing.
* Ending a swing or a guard fades to the layer's empty `UpperEmpty` state (0.15s),
  which hands the arms back to the walk cycle -- it no longer re-plays
  `SwordLocomotion`, which used to restart the leg cycle mid-stride.
* Other players' copies get the same behaviour: the driver routes any state that
  exists on the upper-body layer there, so the existing swing RPCs are unchanged.

Verified by `SoulsSwordRigPlayTests.TheLegsKeepWalkingWhileTheSwordSwings` /
`...WhileTheGuardIsUp`, which measure foot travel while swinging against plain
walking -- with a control test (`TheSameSwingOnTheBaseLayerFreezesTheLegsBy...`)
proving the same measurement DOES catch frozen legs on the old base-layer path.
`CityX > Capture Sword Walking Swing` renders it.

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
* History: this existed in the Unity port on **C**, and was removed 2026-08-17 only
  because it raced the player menu's own C binding. It was restored on Tab, and
  Tab is read in `PlayerInputReader` (behind its menu/console gating), not in the
  camera. Code: `LockOn.cs` (pure rules), `ThirdPersonCamera.PressLockOn/TrackLock`,
  `LocomotionInput.LockedOn`.

## The rules, and where they come from

Researched from how Dark Souls 3 actually behaves (frame data pages, the
community's stamina/poise/i-frame tables). Numbers are DS3's where DS3 has one
and are scaled to this game's 130-point stamina bar.

* **Commitment.** Every attack is `Startup -> Active -> Recovery`. You cannot walk
  out of it. The only exits are being hit (stagger) or rolling during Recovery.
* **Active frames only.** The blade hurts only during the Active window. Each
  target is hit at most once per swing.
* **Stamina.** Swings, rolls and blocked hits spend the one bar. Recovery pauses
  0.6s after spending, then refills at 45/s (DS3). You may act with any stamina
  left even if the cost is more than remains -- the bar bottoms out and you are
  exhausted until it recovers past 20%.
* **Input buffer.** An attack pressed early is remembered for 0.7s and fires the
  instant the chain window opens, instead of being dropped.
* **Heavy vs light.** The heavy has ~5x the wind-up, ~1.7x the recovery, 2x the
  damage and a 0.9s stagger. It is a decision, not an upgrade.
* **Roll i-frames.** 0.35s of invincibility (DS3: 12 frames = 0.4s), 20 stamina.
* **Held guard.** Costs stamina equal to the damage absorbed. A hit you cannot
  afford **breaks the guard**: 1s stagger, stamina to zero. A held guard does NOT
  punish the attacker (`HitResponse.Guarded`) -- only the old timed block does
  (`HitResponse.Blocked`), which is where a future parry belongs.
* **No poise.** Being hit mid-swing staggers you and cancels the swing.

## How the swings line up with the animation

The Mixamo clips are long and slow (1.27s, 1.83s and **4.8s** for the heavy) with
the actual strike buried in the middle. Each attack therefore plays only a
`ClipStart..ClipEnd` slice, stretched to `Startup+Active+Recovery`, and the
Active window sits on the strike. The strike positions were **measured**, not
guessed: `CityX > Measure Sword Clips` (`SoulsSwordCapture.MeasureClips`) logs the
blade tip's speed and position against clip time.

| Attack | Clip | Clip length | Strike (fraction of clip) | Slice played |
|---|---|---|---|---|
| Light 1, 3 | `sword_attack` | 1.27s | 52-62% | 36-85% |
| Light 2 | `sword_attack2` | 1.83s | 40-58% (double sweep) | 30-85% |
| Heavy | `sword_charged_attack` | 4.80s | 43-51% (overhead slam) | 10-66% |

`SoulsSwordCombatPlayTests.TheActiveWindowSitsOnTheClipsMeasuredStrike` pins that
alignment so a retune cannot slide the damage window off the visible swing.

`CityX > Capture Souls Sword` renders contact sheets of each swing and the guard
(`CityX/Logs/CityShots/souls-*.png`).

## Hit detection

Two volumes, both live only during Active:

1. **The blade**: a capsule swept along the real sword between last frame's pose
   and this frame's (no tunnelling through a fast swing).
2. **A reach box** in front of the wielder (`ReachMetres` 2.2, `ReachHalfWidth`
   0.8). The measured light-attack tip rarely gets more than ~1m in front of the
   body -- the mocap keeps the sword close -- so the blade alone would miss a
   target at arm's length. `/sword ReachMetres 0` turns it off (blade only).

## Multiplayer

A swing is announced with its state **and** the speed and clip offset it was
fitted to (`PlayerAnimationDriver.TimedStatePlayed` ->
`NetworkedAnimatorSync.SendTimedSwingToServer/BroadcastTimedSwing`), so other
players see the same paced swing rather than the raw 4.8s clip. Ending a swing
announces the sword idle at speed 1, which also restores their animator speed.
The remote copy clamps the speed to 0.1-4 and ignores unknown states.
Damage is still decided on the owner's machine, as with every other attack today.

## Tuning it live

`/sword` lists every number; `/sword <name> <value>` sets one:

```
/sword BufferSeconds 0.4
/sword Heavy.Startup 0.8
/sword Light1.Recovery 0.35
/sword ReachMetres 0
```

Fields per attack: `Startup Active Recovery ChainAt StaminaCost DamageScale
HitstunSeconds LungeMetres ClipSeconds ClipStart ClipEnd`. Changes last until the
sword is unequipped; bake the winners into the defaults in `SoulsSwordCombat.cs`.

**Hit-stop:** a landed hit freezes the swing's clock and its clip for `HitStopSeconds`
(0.07; `HeavyHitStopMultiplier` 1.8x for a heavy) so contact has weight. Several targets
in one frame do not add up. `/sword HitStopSeconds 0` turns it off. Where the hands sit
on the hilt: `/grip` (roll, primary, support, torso).

## Not built (deliberately)

* **Parry / riposte / backstab.** The `Guarded` vs `Blocked` split is the hook.
* **Player poise / hyper-armour.**
* **Two-handing toggle, weapon arts, weapon weight -> roll speed.**

## Known limitations

* The animation is the Mixamo clips, now with both hands held on the hilt (the free
  hand is reached onto the handle by IK), the mount rebuilt from the clip, the torso
  following the clip through a swing, and a hit-stop on contact -- see
  [MIXAMO_WEAPONS.md](MIXAMO_WEAPONS.md). The hunched *idle* is gone (the torso is held
  upright standing and walking); the light attacks still keep the blade close to the body.
* The guard clip is only 0.5s; a held guard loops its held stretch
  (`BlockHoldFrom/BlockHoldTo`), verified for continuity in state but the pose
  is only as steady as that stretch of the clip.
* Windows were fitted from measured clip data, but *feel* (how long the wind-up
  should be, how big the heavy's stagger is) can only be judged by playing it.

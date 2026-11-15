# Soulslike sword combat

The sword is fought the way Dark Souls fights it. Fists, the bottle and every
other weapon keep the old `MeleeAttacker` combat; equipping a sword adds
`SoulsSwordCombat` to the body (`SwordWeapon.OnEquip`), and unequipping drops it.

Two kinds of sword since 2026-10-13: the **two-handed** spike sword
(`/equip spikesword`), and the **one-handed** combat sword
(`/equip combatsword`, the owner's COMBATSWORDX.glb, `SwordWeapon.OneHanded`), each
with its own four attacks and guard. The one-handed sword's first attack is a
horizontal slash (the owner's choice). The two-handed moves are still called the
greatsword's after the first two-handed model, ported from Godot's `greatsword.gd`;
that model was removed on 2026-10-20 ("it looks terrible and we will make way
better"), and the spike sword keeps its frame and hold.

## Controls

| Input | What it does |
|---|---|
| Left-click | Light attack. Two-handed: chains light 1 -> 2 -> 3 -> back to 1. One-handed: the slash, the backhand, then the diagonal finisher, then back to the slash (owner, 2026-11-04; a knife goes back and forth between the first two). You keep 65% of walking speed while swinging (40% until 2026-10-17). |
| Left-click (hold) | Heavy attack (since 2026-10-24; it was the wheel's click): slow wind-up, big damage, long stagger, hyper-armour, and it **breaks a held guard**. The light goes out on the press; still held after 0.15 s it becomes the heavy from the pose the light has reached. (F stays unbound: explicit ask.) |
| Right-click (hold) | Guard. Its **first 0.2 s parries** (tap to parry, hold to block). Slows you to half speed (legs keep walking). Costs stamina per hit absorbed. |
| Double-tap Shift | Roll (dodge). Legal out of idle or the tail of a swing, never out of a wind-up. 0.42 s of i-frames. |
| 2 | The old spin attack (unchanged). |
| **Tab** | Lock on (works with or without a sword; not with a gun or the flamethrower in hand). Keep pressing to rotate through the surrounding enemies; the press after the last one releases. |

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
  **hold** at the top: a light's is a few frames (the click is answered at once),
  a heavy's a real pause, the telegraph, through which the hand creeps a few cm
  further back and the lean deepens (a moving hold, never a statue);
* a **strike** that accelerates all the way into the contact (a cubic: fastest at
  the moment it lands, three times its average rate, barely moving as it leaves
  the hold) along an arc (a Bezier through a control point) with the blade swept
  through a `Via` direction so a flat cut stays flat instead of flipping through
  the vertical;
* a **follow-through** past the target that decelerates (the same cubic
  reversed, so the blade carries its speed through the contact), then a short
  **settle** with the pose held (the animator's held frame after a blow: the
  weight arriving), then the recovery to the ready stance, which a heavy
  **overshoots** and settles back into like a weight coming to rest;
* all of it timed in seconds per attack in `SwordStrikes.TimingOf` (wind-up,
  hold, strike, follow-through, settle, recovery), from which the key times, the
  live window and the combat's own Startup/Active/Recovery derive (2026-10-23);
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
| Light 1 | `sword1h_slash`: (since 2026-11-13 the one-handed column is the owner's reference combo, `docs/SWORD_GIF_COMBO.md`: a steep cut into a lunge, a steep cut from the other shoulder (the full turn it was thrown out of was taken out 2026-11-14), a level slash right to left (left to right until 2026-11-15), and a heavy driven into the ground with sparks; the entries in this column are the older moves) horizontal, right to left, chest height, 0.62 s (lands at 0.28) | `sword2h_sweep`: FLAT FOREHAND, right to left at chest height, hips leading, the front foot striding, 0.72 s (lands at 0.34) |
| Light 2 | `sword1h_cut`: backhand from over the left shoulder down to the right, the right foot stepping through, 0.62 s (0.29) | `sword2h_reverse`: FLAT BACKHAND, left to right at chest height, the right foot passing forward, 0.70 s (0.33) |
| Light 3 | `sword1h_thrust`: hilt to the hip, driven straight out in a lunge onto the right foot, 0.56 s (0.26) | `sword2h_cut`: the finisher, a wide diagonal from over the right shoulder down to the lower left, a long step, 0.86 s (0.42) |
| Heavy | `sword1h_overhead`: from behind the head down the middle, hips sinking, 1.21 s (a 0.22 s pause at the top, lands at 0.65) | `sword2h_slam`: overhead with the longest hold (0.30 s), everything into the floor, 1.5 s (lands at 0.84) |
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
from the pose the body is actually in** (the last swing's recovery) across its own
wind-up and hold, so a chain never snaps and the body is wholly in the new pose the
instant the strike leaves. After the last swing the sword goes straight back into the carry, down at the
side (owner, 2026-11-14: "it looks much better when he holds the sword down and to
the side like normal. keep it like that"): the ready stance that used to linger 3 s
after a swing or a guard is gone. The ready pose is still where the attacks start from.

`SoulsAttack.FromStrike` derives each attack's Startup/Active/Recovery from the
strike's own `ActiveStart`/`ActiveEnd` (a quarter of the way into the strike to 70%
of the way through the follow-through, from `TimingOf`), so the blade is live from
just before the contact into the follow-through. Pinned by
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

## The two-handed hold is a hammer grip (2026-10-28)

Owner: "work on two handed sword combat he doesnt even grip the sword properly, use spikesword as guide". Rendered up close
(`SwordGripCapture`), the old default two-handed hold -- derived from the 2hsw mocap's hands -- ran the handle ALONG the fingers of the
right hand, so the fist closed on nothing and both hands sat up on the guard. Now (`SwordWeapon.DefaultGripRotation`, the one-handed
hold's own derivation from the hand's shape) the handle lies across the palm with the blade out past the thumb and the edge along the
knuckles, the right fist 10 cm under the guard and the left fist 30 cm under it, reached onto the handle by IK and turned onto it: the
hammer grip both hands take on a bat. Since the forearm runs into the hand along the knuckles, a two-handed key's EDGE is also where its
forearm comes from: the carry now holds the sword low before the belly (blade forward and down, spikes down, the elbows bent above the fists), the sprint trail
spikes-up, and the guard holds the blade near upright before the chest with the forearms coming in from the sides (the old guard's
"edge forward" would have bent the wrist double). The swings read the hold from the mounted sword every frame, so they took it as they were
(`strike-spike-*.png`). The bat shares the hold (its knob 8 cm under the bottom hand, `BluntWeaponTests`); the spike sword's handle is
0.45 m, room for both fists. Pictures: `SwordGripCapture` (Explicit, `-Graphics`) renders `Logs/CityShots/grip2h-{carry,guard}-*.png` from
the thumb's side, the back of the hand, above, the front and the whole body.

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
* **Not with a ranged weapon** (owner, 2026-10-24: "turn off tab lock on for ranged
  weapons"): with a gun or the flamethrower in hand Tab does nothing, and drawing one
  lets a lock go; the mouse aims (`PlayerInputReader.HoldsRangedWeapon`).
* Code: `LockOn.cs` (pure rules), `ThirdPersonCamera.PressLockOn/TrackLock`,
  `LocomotionInput.LockedOn`; Tab is read in `PlayerInputReader`.

## The rules, and where they come from

Researched from how Dark Souls 3, Elden Ring and Sekiro actually behave (frame
data pages, the stamina/poise/i-frame tables, the animation guides on anticipation
and follow-through, and the PvP netcode write-ups). Numbers are theirs where they
have one, scaled to this game's 130-point stamina bar.

* **Commitment.** Every attack is `Startup -> Active -> Recovery`. Until the
  recovery's **tail** (past its chain window, `InRecoveryTail`) you cannot walk out
  of it: the exits are being staggered or dashing during Recovery. In the tail the
  swing has done its hitting, and the body is free to move at full speed, raise a
  guard (which ends the swing), dash or chain while the arm finishes on its own
  (2026-10-23; before that a recovery held you to the end, which is what made
  Kingdoms of Amalur's combat read as sluggish).
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
* **Hit-stop, both sides.** A landed hit freezes the attacker's WHOLE BODY (the
  swing's clock and pose, the Animator, the footwork, the movement) for 0.09s (1.7x
  for a heavy), and the victim's whole body for its own instant (0.07s, 0.12s for a
  heavy). The freeze is sent to other players too (`StateSpeedChanged`), so a copy
  freezes on contact. See "Hits that land" below.
* **Hit reactions.** Being hit shows now: `HitReactions` throws the torso and head
  away from the blow (four directions by name: `hit_front`...; a `stagger_*` when the
  blow took the controls away), played by `FlinchPoseDriver` and replicated like a
  swing, out of the victim's instant of freeze. Nothing on the body reacted before.
* **A landed light earns its follow-up** (2026-10-17). The lights' hitstun is longer
  than the chain's gap to the next cut: one-handed 0.6s (the contacts are 0.64s
  apart), two-handed 0.65s then 0.75s (the finisher winds up for 0.44s). With the
  freezes counted the victim is free for 0.05-0.10s before the next cut lands: enough
  to raise a guard (whose parry window then catches the cut), never enough to swing
  first. The heavies stun 1.0s / 0.9s. `SoulsCombatRhythmTests` pins the margins.
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

## Hits that land (2026-10-17)

Owner: "the hits dont feel impactful at all. make it feel better and make the blood effects better too, research how to do both. we need
to work on the fluidity and the reactivity." Before this, a hit froze the attacker's sword arm for 0.07s while the legs walked on, tipped
the victim's torso 14 degrees, and sprayed fourteen slow dots back at the attacker; and a 0.35s stun let the victim get up and trade blow
for blow, so a fight was whoever clicked first.

**What the research says.** Games known for their hits fire several small things on the same frame rather than one big one: a hit-stop
on BOTH fighters (Street Fighter V holds 8/12/15 frames by weight; God of War on every hit; past about 0.12s a freeze reads as a dropped
frame), a displacement that shows the force (an impulse sized by the attack), a short camera shake scaled to the blow, particles whose
direction and size match the attack, a hit flash, layered sound. Sources: Jason de Heras on how 3rd-person melee games communicate hit
feel (https://www.jasondeheras.com/gamedesign/2021/4/23/how-do-3rd-person-melee-combat-games-communicate-game-and-hit-feel), the game-feel
guide on hit-stop and screen shake (https://swordarcade.xyz/guides/game-feel-hitstop-and-screen-shake/), a combat-feel devlog
(https://dev.to/hirodeath/improving-combat-feel-with-hit-stop-slash-effects-and-combo-animations-3dbo); for the blood, the Real Time
VFX critique of a sword-hit splatter (burst outward from the wound, not along the swing; most of what sells it is what stays on the
ground: https://realtimevfx.com/t/wip-blood-splatter-effect/11803).

**What a blow does now** (`HitImpactEffects`, on every body; `PlayerLocomotion.Impact.cs`):

1. **The freeze, both sides.** The attacker's hit-stop holds their whole body (Animator stopped, no movement, the sword's footwork
   waiting): 0.09s, 0.15s for a heavy. The victim is held for its own instant (0.07s, 0.12s for a heavy) with its pose at the moment of
   impact, and the recoil (`HitReactions`) is thrown out of the freeze. A freeze never outlasts 0.25s (`MaxImpactFreezeSeconds`), so a copy
   told to freeze and never told to stop lets go on its own; a copy that nobody Ticks counts it down in LateUpdate.
2. **The shove.** The victim is thrown back along the blow, 0.65m for each second of hitstun up to 1m (a light 0.39m, a heavy 0.65m, a
   punch 0.33m), over 0.2s, fast at first; through the controller, so walls stop it; after the freeze. A copy is moved by the network.
3. **The flash.** Every renderer on the body brightens by up to 90% and fades over 0.09s, through property blocks (no material touched).
4. **The blood** (`BloodBurst`): a burst out of the wound AWAY from the blow, tilted up -- 26 heavy drops that arc, streak as they fly
   (stretched billboards with a soft dot, no texture file) and stop where they land (world collision), plus 12 fast fine drops gone in a
   quarter second; a heavy throws 1.6x as much, a bullet 0.6x. Made in code once and copied. Then the splats (`BloodSplats`): on the floor
   under the wound, on the floor where the spray lands (further along the blow, stretched the way it flew), and on a wall within 1.8m
   behind the body; four procedural splat shapes (a ragged blob, droplets flung one way, a drip), alpha-blended quads a centimetre off
   the surface; they stay 45s and fade over the last 10; 48 at most, the oldest making room. The old `BloodSplatter` prefab (the Godot
   scene's fourteen dots) is gone; bodies without vitals of their own (a civilian, a dummy) get the burst from the striker's side.
5. **The thud**, at the wound: a low thump with a slap on the front of it, made in code; the attacker's own impact clip now varies its
   pitch a little (`MeleeAttacker.PlayImpact`).
6. **The camera** (`ThirdPersonCamera.AddShake`): when the player's own body is hit, the picture jolts 7cm (12cm for a heavy) and settles
   over 0.25s, a smooth wobble dying with the square of the time left; when the player's blade lands, 3.5cm for 0.15s, 9cm for a kill.
   Only where the picture is taken from: the aim, the crosshair and the hit traces never move, and it ends exactly where it began. A
   smaller shake never cuts a bigger one short.
7. **A killing cut flings the corpse** away from the striker (`PlayerVitals.DeathImpulse`, 3.6 m/s and 1.4 up, read by the ragdoll as it
   goes limp). A bullet, a spell or a fall still drops the body where it stood.

**Where the wound is.** The striker says where the blow met the body (`DamageContext.ContactPoint`: the blade sweep's closest point, a
bullet's hit) and the victim's own body shows the wound there, inside the blow's scope; a blow forwarded from another machine, or told to
a copy, is placed on the body's near side toward the blow.

**Multiplayer.** The victim's own body announces its wound by name (`wounded_<front|back|left|right>_<light|heavy>`, `AnnounceState`
like `block_deflect`), and every copy shows it: the freeze, the flash, the blood and the splats at the copy's own position (the shove
comes with the owner's movement). The attacker's hit-stop reaches copies as the swing's speed 0, which now freezes the copy's whole body
too, and the restored speed lets it go.

**Tests.** `HitImpactPlayTests` (a landed blow holds the body still then throws it back; a heavy holds longer and throws further; a guard
that held shows no wound; the blood lands on the floor and a wall behind; the splats never exceed their cap; the flash brightens then lets
go; a wound told from the network is shown on a copy whose freeze lets go without a Tick; the attacker's hit-stop holds the whole body;
a killing cut flings the corpse), `SoulsCombatRhythmTests` (the follow-up margins, the hit-stop's length),
`ThirdPersonCameraPlayTests` (the shake settles back exactly, never turns the camera, a smaller never cuts a bigger short).
`HitImpactCapture` (Explicit, `-Graphics`) films a light and a heavy landing on the swordsman from the side, the player's own view at the
contact, and the floor after a few cuts (`hit-impact-*.png`).

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
* **Hit-stop on the whole world.** Both fighters freeze (2026-10-17); the rest of the world goes on.

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
  greatsword's two-handed hold fits it. Unwrapped round its length for **the wood grain**, which `Weapons/Melee/WoodGrain.cs` draws in code the first
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

**The combat sword's paint (2026-10-19).** The owner sent Meshy's "White Sword": the same sword (same 1.9 m, same place), 50,698 triangles,
four 2048 maps (19 MB as files, about 22 MB of video memory per sword). The game keeps its own 3,802 triangles. `SourceArt/CombatSword/
combat_sword_bake.py` unwraps them, bakes Meshy's colour onto one 512 map (`Weapons/CombatSword.png`), and bakes metal and smoothness onto
one 256 map (`CombatSwordMetal.png`: metal in red, smoothness in alpha, URP's metallic map). It then writes `CombatSword.glb` back with its
UVs. White steel shines; the black wrapped grip does not. The normal map was left out: detail too fine to see on a swung sword, for twice the
size. In the game the two maps are about 0.45 MB of video memory.

## Update 2026-10-19: arms out of the body, the crowbar's hold, a bigger bat

Owner, with screenshots: "after you swing the crowbar it leave it in this weird arm position, and the bat's default position has the elbows
clipping through the body ... find out if it is for all two handed weapons", "adjust the size of the bat its two small", "its just a baseball
bat not meant to be a massive greatsword".

- **Every two-handed weapon** had it. The carry held the grip 0.22 m in front of the spine, but the torso is 0.26 m round it: the left hand
  (lower on the handle) sat inside the belly, the left forearm 21 cm in, and both elbows went through.
  - The carry now holds the grip 0.50 m out (`CarryKey2H`), and the running carry's 0.38 m to the side.
  - `ElbowClearance` (after the support hand, `PlayerLocomotion.KeepArmsOutOfTorso`) turns each arm holding a sword round its own
    shoulder-to-hand line until the elbow and both bones are out of the torso capsule, with the hands kept. Of the clear turns it takes the
    lowest elbow (the nearest swung it up into a chicken wing).
- **The crowbar after a swing**: the owner's saved one-handed hold was set on the combat sword (edge on +X), and the crowbar's edge is on -X.
  So it sat in the hand edge to the thumb, and every one-handed pose turned the hand half round to lead with it, which put the ready stance's
  arm up over the shoulder. `SwordWeapon.SavedOneHandHoldFor` turns the saved hold half round the handle for a sword whose edge is on the other
  side.
- **The bat** is 1.6 m (from 1.12 m), the bottom hand 11 cm above the knob.
- Tests: `SwordArmsClearPlayTests` holds each sword-combat weapon, standing and after a swing: no arm more than 3 cm into the torso, and a
  one-handed weapon's hand below the shoulder after a swing. `ElbowClearanceTests`, and
  `BluntWeaponTests.TheCrowbarsEdgeSitsInTheHandWhereTheCombatSwordsDoes`.

## Update 2026-10-23: the feel pass (anticipation -> acceleration -> impact -> recovery)

Owner: "research how to make melee combat feel smooth and satisfying. work on the fluidity and the reactivity", with ZumarDev's Unity
melee clip as the reference (fast, readable swings with a bright trail on the strike and sparks on the hit; its enemies shatter into bits on
death, which the owner does NOT want: the ragdoll stays), and "attacks should have clear anticipation -> acceleration -> impact ->
recovery. Light attacks feel snappy; heavy ones feel powerful. Avoid floaty or weightless swings."

Research (wayline.io on indie melee combat, slynyrd's pixelblog on melee attacks, Game Developer's "Combat Canceled" on God of War, the
DGMI wiki and Dustloop on input buffers, SSBWiki and the KOF XIII frame data on hit-stop, Aztez's "Anatomy of a Successful Attack" and
"The Amalur Problem", mocaponline's animation-polish guide): the player's own attacks must answer within a few frames (a long anticipation
reads as input lag; spend the frames on the follow-through instead), the impact frame matters most, hold the follow-through for weight,
overshoot-and-settle for heavy actions, hit-stop scaled by weight, trails only on the strike (never the wind-up), and let the recovery be
cancelled by movement or another action once the hit window is over (Amalur's locked recoveries are what made it sluggish).

What changed:

- **Explicit phases, in seconds** (`SwordStrikes.TimingOf`: wind-up, hold, strike, follow-through, settle, recovery). Every attack's key
  times, live window and `SoulsAttack` windows derive from it. Lights land in 0.26-0.34 s (the finisher 0.42); the heavies pause at the top
  0.22-0.30 s and land at 0.65-0.84 s. Totals: 1H 0.62/0.62/0.56/1.21 s, 2H 0.72/0.70/0.86/1.50 s.
- **Acceleration**: the strike and follow-through eases are cubic (`u^3`, `1-(1-u)^3`): the blade arrives at three times its average rate
  (two before) and barely moves as it leaves the hold. The follow-through's seconds keep the equal-average-rate rule, so the blade carries its
  speed through the contact.
- **Impact**: a short **settle hold** at the end of the follow-through (0.04-0.07 s lights, 0.14-0.18 s heavies). The hit-stop and the
  victim's freeze, shove, flash and blood are unchanged (2026-10-17).
- **Recovery**: a heavy's **overshoots** the ready stance by a tenth of the travel (easeOutBack, 2-4 cm of hand, the lean with it) and
  settles back; the blade's direction, the elbow and the feet never overshoot. A heavy's **hold creeps**: the hand draws 3 cm further back
  and the lean deepens 2 degrees through the pause.
- **The recovery's tail** (`SoulsSwordCombat.InRecoveryTail`, from the chain window on): movement goes back to full speed
  (`SlowsMovement`, used by `PlayerInputReader` and `SwordsmanBrain`), and a held right-click **raises the guard** there, ending the swing.
  Chaining and the dash already worked from the window.
- **Tempo**: ChainAt 0.45 for the lights (0.5 the finisher), 0.55 the heavies; light stuns 0.45 (1H), 0.50/0.58 (2H sweep/backhand) keep the
  rhythm rule (the next cut lands while the first still holds, `SoulsCombatRhythmTests`); the chain runs at about a cut every 0.57 s (1H) or
  0.64 s (2H), hit-stop included.
- **The cut is seen**: `SwordPoseDriver` lights the **blade trail** (`BladeTrail`, the spin's streak material) exactly while the blade is
  live, on the owner's body and every copy (the driver runs on both).
- **The cut is heard**: `SwingWhoosh`, a swish made in code (noise through a swept resonant filter, loudest and highest at the contact),
  played as the strike leaves the hold; lower, longer and louder for a heavy. Also on every copy.
- Tests: `SwordStrikesTests` (lights land fast / heavies pause and hold, the strike is a whip not a wave, the settle holds, the heavies
  overshoot and the lights do not, the heavy hold creeps), `SoulsCombatRhythmTests.ALightLandsInAThirdOfASecondAndTheChainRunsQuick`,
  `SoulsSwordCombatPlayTests` (the tail frees the body; a guard comes up in the tail), `SwingWhooshTests`, `SwordSwingShowPlayTests` (the
  trail only while live and the swish, on the owner and on a copy told the attack's name).

## Update 2026-10-24: hold the click for a guard-breaking heavy

Owner, on hearing the heavy was on the wheel's click: "i dont like that make it so holding click does a heavy guardbreaking attack".

- **Hold left click for the heavy.** The light still goes out on the press, so a click is answered at once (waiting for the release to tell
  a tap from a hold is what made the punch feel laggy). Held on for `SoulsSwordCombat.HeavyHoldSeconds` (0.15 s: longer than a click, shorter
  than every light's wind-up), `HoldIntoHeavy` turns the light still winding up into the heavy from the pose it has reached (the light's
  stamina counts towards the heavy's); a light remembered mid-swing is remembered as the heavy; a light already live is followed by the heavy.
  The wheel's click does nothing now.
- **The heavies break guards** (`SoulsAttack.BreaksGuard`). The blow is struck inside `DamageContext.GuardBreaking`; a held guard that
  catches it is broken open (`PlayerVitals.BreakGuard`: the reduced damage, staggered `GuardBreakStunSeconds`, `GuardBroken` raised, the
  blow answered as landed), whatever stamina is left, and block chance cannot turn it aside. Only a **parry** (the guard's first 0.2 s)
  still stops it, so the answer to a heavy is a timed guard or a dash. The flag crosses the network with the hit (`NetworkedDamage`), so
  another player's guard breaks on their own machine. The test swordsman's heavies break guards too.
- Tests: `SoulsSwordCombatPlayTests` (a guard-breaking blow breaks a held guard but not a parry; a held click turns the light into the
  heavy; a held press mid-swing asks for the heavy next), `SoulsSwordRealInputPlayTests.HoldingTheClickSwingsTheHeavyATapTheLight`,
  `NetworkedPlayerStateSyncPlayTests.AGuardBreakingHitOnACopyBreaksThatPlayersRealGuard`.

## Update 2026-10-24: an enemy's tell and committed swing

Owner, on fighting the swordsman: the dash is useless "because the enemies just auto lock onto you anyways", "the telegraph of the enemies
attack is either very short or non existent", and their guards go up "the second you swing" (docs/SWORDSMAN.md has the research and what
he does now). What the sword combat gained for it, all off for a player:

- **The tell** (`TelegraphLightSeconds`, `TelegraphChainedSeconds`, `TelegraphHeavySeconds`; 0 unless an enemy's brain sets them): once
  the wind-up reaches the top of its pose (`SoulsAttack.HoldAt`: the middle of the hold for a posed strike, 60% of the startup for a clip),
  the swing's clock and pose stop there, as a hit-stop stops them (`HoldClip`), for the tell's seconds, then go on. `Telegraphed` is
  raised as it begins (the swordsman shows `AttackGlint` at the blade's tip), `IsTelegraphing` while it lasts, and
  `SecondsUntilChainWindow` counts it in. A player's own swings answer the click as before.
- **A committed aim** (`AimSwing`): a brain that turns its body itself keeps a swing's aim where the body faces, so a wind-up follows the
  target only as fast as the brain turns (the swordsman: 90 degrees a second) and the blade goes where the body faces when it goes live.
- **No lock-on with a ranged weapon** (see "Lock-on" above).
- `AttackGlint`: four tapered rays round a bright core, flaring out and fading over 0.32 s, facing the camera, with a quiet ting. Its
  material and the clash sparks' (`BlockDeflectEffect.SparkMaterial`) are truly additive now (`SeeThroughMaterial`): they used to draw as
  solids, which the cel look's black lines traced until a thin spark was all line (docs/CEL_SHADING.md).
- Tests: `SwordsmanPlayTests.HisCutHoldsItsTellAndDoesNotFollowADashRoundHim`,
  `LockOnRealInputPlayTests.WithAGunInHandTabDoesNotLockOnAndDrawingOneLetsALockGo`, `DuelistMindTests`, `CrimsonClawTests`.

## Update 2026-11-04: the one-handed sword's three-hit combo

Owner: "lets make combos", a diagonal finisher, no knockback to start ("that should be stuff you have to upgrade to or unlock"), and "if the final hit gives a stagger then why wouldnt you just keep attacking you can just spam click". Then "make it feel really good AAA level".

- **The chain:** slash, backhand, then `SwordStrikeKind.Finisher1H` (`sword1h_finisher`), posed in `SwordStrikes` like the other two so it keeps the grip and blends out of the backhand. A knife keeps its two cuts (`LightChainLength`). The old thrust stays for the knife's heavy.
- **The swing:** the blade up over the right shoulder, the body sunk and coiled, the free arm reaching ahead (`FreeArm`: only this attack moves it this far); the left foot strides out through the wind-up, so it is planted before the blade lands (with the stance opening on the strike, the foot landed after the blow and the legs were bunched under it); around and down to the lower left with the body driving in; the free arm straight back against the blade (bent, it read as a hand on the hip, which the owner has rejected). `TheOneHandedFinisherIsADiagonalWithTheWholeBodyBehindIt`.
- **Why it cannot be looped:** `ChainAt` 1 (the recovery cannot be cancelled into a swing; a dash still can) and a stagger shorter than the recovery: target held 0.50 s + 0.12 s, attacker at least 0.83 s, then the next swing's 0.2 s to go live. `SoulsAttack.HitstunAt` shrinks the stagger with the attacker's Melee Speed, or a fast blade would be free first. The first two cuts are guaranteed, the finisher is not: about 0.2 s free after the backhand. `TheFinisherFreesTheTargetBeforeTheAttackerCanAct`, `ATargetHitByTheFinisherIsFreeBeforeTheNextSwingStarts`, `SpamClickingCannotCutTheFinishersRecoveryShort`, `TheOneHandedFinisherCanBeGotOutOfAfterTheSecondCut`.
- **Damage 2.1x (21 on a 10 blade), above the 1.6x heavy, on purpose:** its long recovery would otherwise drop the sword's damage a second below a knife's (`KnifeRulesTests`); the whole string does what the two cuts alone did. Stamina 16 + 16 + 24. It does not break guards.
- **The weight:** it lands as a heavy blow whatever its stun (`SoulsAttack.LandsHeavily`, `DamageContext.HeavyBlow`, forwarded to other players' bodies by `NetworkedDamage`): the heavy hit-stop, freeze, burst, thud, number, swing sound, and a bigger camera jolt for the attacker.
- **Other players** see it by name, like every swing.
- **Not built:** knockback or ragdoll (to be unlocked), and the two-handed sword's chain is unchanged.

## Update 2026-11-04: the whole body behind the first two cuts

Owner: "i dont hate the first two swings of the sword but am curious to see how you could improve them". Same shapes (the flat slash, the backhand diagonal), same timings and damage. The renders showed an arm waving a stiff body: upright, the free arm limp (`FreeHandBalanceSwing` moved it about 3 cm in practice), and the feet often in the air at the blow.

- **Staged strike (`SwordStrikes.Staged`):** everything arrives together at the contact, but the hips and the weight set off first, then the shoulders, then the arm, and the blade's cubic last. `TheFirstTwoCutsSetOffFromTheHipsAndTheBladeComesLast`.
- **The body:** coiled further in the wind-up (shoulders 55 and 48 degrees, leaning away), driving in at the contact (lean 16, hips 0.15 m lower), always less than the finisher. `TheFirstTwoCutsPutTheBodyBehindTheBlade`.
- **The free arm:** swings against the blade from the shoulder (`Slerp`: across the chord it folded on the way), long and out at its own side. In front of the belly it read as a hand on the crotch; bent, as a hand on the hip.
- **The backhand's wind-up:** the hand crosses past the left shoulder, not at the neck, so the arm is not all hidden behind the head.
- **One step a cut (`FootSteps`, `SwordStrikes.StepsAt`, all three cuts of the chain):** only the foot the cut strides on may step, and only while it can still land before the contact; nothing leaves the ground again until the blow's pose has been held, then the feet catch up. A foot the leg can no longer reach steps anyway, so walking while swinging does not lock the feet. The finisher's lunge carries the body past what the back leg reaches, so that foot trails off the ground at the blow; the front foot is planted. `SwordChainFeetPlayTests`, `EachCutOfTheChainStepsOneFootAndKeepsBothDownForTheBlow`.
- **One gameplay change:** the backhand's lunge is 0.40 m, was 0.20 m. With the right foot passing the left and the body staying back, the feet landed side by side. So the second cut reaches 20 cm further. The knife's stays 0.15 m.
- **Known:** a knife swings 1.3x faster but a step still takes 0.17 s, so its front foot can land just after the blow.
- **Other players** get all of it: the rule comes with the pose, which plays by name.
- **Not yet:** the staged strike and free arm's swing on the two-handed cuts and the heavies; the finisher has the free arm and the foot rule, not the staging.

## Update 2026-11-04: blades meeting guards

Owner: "I want the blocks to feel more impactful, have the noise a bit louder and really have the attackers weapon bounce off of the blocker. it should feel impactful hitting an opponent who is blocking and when you block an attack the opponents weapon should bounce off with a loud noise".

What was wrong: the clash was a 3D one-shot heard by the camera 5 m behind the player at about a fifth of its volume (with a body-hit sound over it on the attacker's side); the rebound began the next frame, so the blade never stopped on anything; the blocker's guard did not move.

- **Hits land when the blade arrives (every sword hit, not only blocks):** the forgiving reach volume (`ReachMetres`) used to catch a target on the first live frame, when the strike's whip has hardly moved the blade: it was still pointing behind the body, and the hit-stop froze it there. It now waits for `SoulsAttack.ContactAt`, the pose's own contact, about 0.08 s later. The blade's own sweep is unchanged. The gaps between hits, and so the chain's guarantees, stay as they were.
- **The beat (`BlockDeflectEffect.ClashFreezeSeconds`, 0.1 s):** both fighters held still. The attacker's blade stops on the guard (`SwordStrikes.StoppedOnGuard`, a pose: it rises to cross the blocker's blade, its point short of their body), inside the rebound's 0.6 s, so the stun is no longer.
- **The bounce:** the rebound's thrown pose is much bigger (hand above the shoulder, shoulders 42 degrees open, leaning back 16, free arm flung out) and the attacker is knocked back 0.25 m (`ReboundShoveMetres`), with a camera jolt harder than a landed blow's.
- **The blocker:** the guard is driven back and comes back (`SwordStrikes.GuardJolted`, 0.34 s), the body slides 0.15 m, 0.3 m from a finisher (`GuardShoveMetres`), with a camera jolt. The shoves are small so the two stay in reach to punish the opening. A parry stands firm: the same freeze and camera jolt, but the guard is not driven back and the body does not slide.
- **The sound:** the owner's recordings at full volume, with a low thunk under them (`ClashBody`), at full volume within 12 m (`ClashFullVolumeMetres`) and fading beyond it. The attacker's body-hit sound no longer plays on a guarded blow. `SwordClash1.mp3` peaks at 0.60, the other two at about 1.0, so it is the quiet one of the three.
- **The sight:** a flash (`AttackGlint.Flash`) and 26 sparks, was 9, bigger and longer, where the blades meet.
- **Also clashes now:** a blade stopped outright by Block Chance, and a guard broken open. A bullet glancing off stays the small ting.
- **Other players:** the clash and the rebound travel by name as before; a copy shows the freeze, the pose, the sparks and the sound, and its place stays the network's. `GuardClashPlayTests`.
- **Render:** `SwordStrikeCapture.RenderTheGuardClash`, `Logs/CityShots/guard-clash.png`.
- **Not changed:** bare fists hitting a guard do not bounce (they have no rebound).

## Update 2026-11-13: committed swings, slower wind-ups, the whole body

Owner: the swings are "stilted and awkward. they only use the arms so It looks odd ... telegraphed, have directionality, and the animation involves the whole body akin to dark souls combat". The whole of it is in `docs/SWORD_FULL_BODY.md`; what changes on this page:

- **A swing takes nothing from the player's movement** (owner, later the same day: "You should be able to move freely with zero slowdown"): the keys walk, sprint and steer through it, and the legs under it are the walk's or the run's (`docs/SWORD_MOMENTUM.md`). It slowed the walk to 0.65 until this day and rooted the body for a few hours of it; an enemy is still held to 0.65 by its own swing (`CommittedMoveSpeedFraction`). Standing, an attack steps the body by its clip's own stride (`SwordStrikes.StepMetres`, 0.40 to 0.60 m).
- **Every timing in the tables above is older than this.** `SwordStrikes.TimingOf` has today's: a one-handed light lands at 0.40 s, a two-handed one at 0.47 s, the heavies at 0.77 s and 0.96 s after a hold of 0.30 s and 0.38 s. The damage went up by the same share as the cycle grew, the knife's with it (`SoulsCombatRhythmTests`).
- **The swordsman's own tells are shorter by what the wind-ups grew** (`SwordsmanBrain.Telegraph*Seconds`): his cuts land as long after they start as they did.
- **The legs, hips, torso and head of each attack are a clip**; the sword arm, the blade and the hands are still this page's keys, solved from the clip's shoulders.
- **At a sprint the first light is a running attack** (the same day: "if you are sprinting it does a different attack"): the chain's diagonal cut brought down on the run. `docs/SWORD_MOMENTUM.md`.

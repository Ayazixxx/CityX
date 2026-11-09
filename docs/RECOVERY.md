# Recovery: flipping back onto your feet

Owner, 2026-11-08: "does the game have space to recover in mid air when ragdoll" (it did not: Space only stood a body up after it had lain
still for 2 s), then "add that as a skill in the movement tree called Recovery", "recovery should be one of the first 3 options in replacement
of superjump and superjump can go after it", and "make the recover skill work like the recovery in saints row 4".

## What Saints Row IV does

Saints Row IV's **Ninja Reflexes** (a Super Jump upgrade): knocked down, press Jump to get back up immediately. The Boss pops up with an aerial
flip and lands on their feet; Zin enemies have the same move. A later upgrade (Super Jump "Health": "Jump up when knocked down to get health
restore") also heals on the way up; that is not built here. Sources: the Saints Row wiki's Super Jump and Zin pages, SegmentNext's super powers
guide.

## The skill

`SkillCatalog.Recovery` ("recovery"), Freerunner, row one beside Dash and Climb (where Super Jump was), needing Vault. Super Jump moved straight
above it and needs it; the empty star above Super Jump needs Super Jump (one empty star fewer: the column is three high like the others). A
character who learnt Super Jump before keeps it (a learnt skill whose prerequisites are missing is kept: it was paid for). Movement: it keeps its
own key and never goes on the bar. Like Super Jump it is **not** in `SkillBook.AsBeforeTheTree` (a body with no character, the tests, a
pre-tree save): it came after the tree and changes what Space does to a body that is down.

## How it plays

Knocked down -- thrown by the Crimson Claw, flung by a finisher or a blast, crashed off the jetpack, lying on the ground, `/sleep` -- **press
Space** (`PlayerInputReader`'s ragdoll branch) and the body flips back onto its feet at once, from wherever it is:

- **In the air** it flips and flies on exactly as it was going, then lands on its feet. Its way across rides as momentum until it lands
  (`PlayerLocomotion.TakeOverFlight`, the grapple release's residual velocity), the keys steering on top as in a jump.
- **Lying on the ground** it springs up into the flip, high enough to finish it in the air and be upright a quarter second before landing
  (`RecoveryFlip.UprightBeforeLandingSeconds`). The same rule holds a body just above the ground: one that would land mid-flip is sent up enough.
- No waiting the 2 s a body without the skill must lie still, no cost, and as often as you are knocked down.
- Not while held in the Crimson Claw's fist (it throws you; flip once thrown), not dead, not hanging from the grapple rope (Space lets go of it).
- A blow or death mid-flip throws the body limp again from the pose it was flipping in.
- It ends a knockdown: the knockdown's timed get-up does not run after it, and it earns none of the get-up's moment of safety.
- Fall damage: the drop is counted from the highest point the body reached, as before, to where its feet land.

Without the skill, Space still stands a body up the old way (once it has lain still 2 s, with the get-up clip).

## The flip

`RecoveryFlip` (plain struct, tested alone) shapes it from the body as it lies or tumbles:

- **Facing**: the way the body lies (across its right side, from the thighs), level. It turns head over heels about its right.
- **How far**: from its pitch to upright. Spinning head over heels faster than 90 deg/s it carries on the way it spins (at least 150 deg, so a
  turn and a half at most); hardly spinning, it goes the long way round, between half a turn and a whole one, so it is always a real flip
  (upright: a whole forward flip; lying on its back: over backwards; diving: on over forwards).
- **How long**: 0.42 s for half a turn to 0.55 s for a whole one (Saints Row's is about half a second).
- **Speed** follows the tuck as a diver's does: slow while the body is open at either end, fast while it is curled (`TuckSpeedSwing`).

`Ragdoll.Recover` (Ragdoll.Recovery.cs) plays it. The bones stop flopping (kinematic) and are posed again: standing still (the Animator's
"Locomotion" at rest), curled into a **tuck** (`TuckPose`: back rounded, chin down, knees up to the chest, heels under them, hands on the shins;
each bone aimed in the body's frame, weighted in over the first quarter, held, opened out, legs down, from 58% to 90%), the whole body turned
about a point 0.2 m above its hips (the Animator's transform, as the jetpack's lean turns it). The pose it was flung in blends into the flip over
0.12 s (the get-up's pose snapshot). The standing capsule is put under the hips (never under the ground) and carries the body under the walker's
gravity; when the flip is round (or it lands early), the walker has it, in the air, arms at its sides.

Not the jump clip: it carries the body 1.25 m below where it stands (in a jump the walker's ground clamp lifts it back so the feet meet the
capsule's bottom) and holds the arms out in front; built on it, the flip hung half a metre under the capsule and, from lying down, went through
the ground (measured by `RecoveryCapture`'s log).

## Multiplayer

The flip is part of being down (`IsLimp` stays true through it, as through a get-up), so its pose goes to every other player with the rest of
the ragdoll (`NetworkedRagdollSync`, sent at the moving pace while it flips) and their copy flips as it does (`RagdollMirror`); the last word when
it is up hands the copy back to its Animator. Nothing new on the wire. Test: `NetworkedPlayerStateSyncPlayTests.ARecoveryFlipShowsOnAnotherPlayersCopy`.

## Also fixed with it

A knockdown's timed get-up (finisher, the Claw's throw) that ran out while the body was still flying stood it up in mid-air (the get-up clip
teleported it to the ground). It now waits until the body is down on something (`Ragdoll.IsOnGround`: the torso within 0.7 m of the surface
below), looking again every 0.05 s.

## Tests

- `RecoveryFlipTests` (EditMode): always ends upright after a real flip, the long way round when still, on the way it spins, fastest curled,
  faces the way the body lies, keeps momentum and pops up only when it would land first, how long a turn takes.
- `RecoveryPlayTests`: thrown, it flips head over heels and flies on, lands on its feet; lying down, it springs up without waiting and is
  upright before it lands; it starts from the flung pose; not from the Claw's fist; a blow or death mid-flip throws it limp; it ends the
  knockdown's timer; a knockdown thrown high gets up only once it has come down.
- `SkillLocksRealInputPlayTests.SpaceFlipsAThrownBodyBackOntoItsFeetOnlyOnceRecoveryIsLearnt` (real keys, real city).
- `SkillTreeTests`: Recovery in row one, Super Jump above it, a Super Jump learnt before kept.
- `RecoveryCapture` (Explicit, -Graphics): `Logs/CityShots/recovery-*.png`.

## Open

- Saints Row's healing upgrade (a rank 2 that restores some health on the way up) if the owner wants it.
- A whoosh on the flip (would need sending to the other players).

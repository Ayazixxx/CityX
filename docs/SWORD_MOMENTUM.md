# Sword attacks thrown on the move

Owner, 2026-11-13, of the committed swings of the same day (docs/SWORD_FULL_BODY.md): "The new sword animation is a little better but
doesn't work. We need to update the feel of the sword combat to feel more like dark souls where the movement of the character carries
over into the attack, for instance if you are sprinting it does a different attack, if you are walking forward it does a slightly
different attack if you are standing still, etc. I think the movement of the player has to factor in to the animation because right now
it feels like attacking forces you to stay still or slow down or something".

## What was wrong

A click took the keys away for the whole swing, and a body with no keys brakes at 20 m/s² (`MovementWeight`): a walk was stopped dead
0.2 s into the wind-up, a sprint in under 0.4 s. Then the attack's own half-metre step set off from that stop. Whatever the body was
doing, every attack was the standing one.

## What a click does now

The light that OPENS an attack is chosen by how fast the body is going **forward, toward where it will swing**, at the click
(`SwordStrikes.OpenerFor`, `SoulsSwordCombat.OpenerAt`):

| Going forward at | One-handed | Two-handed | Carried | Lands at |
|---|---|---|---|---|
| under 2 m/s (standing, backing off, strafing) | `sword1h_slash` | `sword2h_sweep` | 0.45 m, stepped from rest | 0.40 s / 0.47 s |
| 2 to 5.8 m/s (a walk is 4) | `sword1h_slash_walk` | `sword2h_sweep_walk` | 1.2 m / 1.3 m | 0.40 s / 0.47 s |
| 5.8 m/s and over (a sprint is 7.5) | `sword1h_run` | `sword2h_run` | 2.3 m | 0.43 s / 0.48 s |

- **Out of a walk**: the same cut as standing, on a body that walks into it. Two walking steps (the right foot passes, the left lands
  under the blow), the body leaning into its pace instead of rocking back to wind up. Same damage, stamina and stun as the first light.
- **Out of a sprint, the running attack**: the chain's last cut (the diagonal from over the right shoulder) brought down on the run. A
  running stride as the blade goes up, a long leaping stride, and the cut lands as the left foot does with the body sunk deep over it;
  the right leg trails and swings through into a skid. Harder than a first light (1.35 against 0.92 one-handed, 1.75 against 1.23
  two-handed), dearer in stamina, and its recovery is half a second: the runner is open after it.
- Both **chain on into the second light**, like the first light they stand in for.
- Chained cuts, the heavies and the knife are as they were: a chained cut starts from a body the last cut has already brought to rest.

The numbers are mine. The owner asked for the three cases; which cut the running attack is, how far each goes and what it costs were
not said.

## How the pace is carried

- At the click the attack **takes the body's speed along the swing's way off its feet** (`PlayerLocomotion.TakeMomentumAlong`), so
  nothing brakes it, and carries the body itself (`SoulsSwordCombat.ApplyLunge`).
- `SwordStrikes.Carried`: the body sets off at the speed it came in at and is at rest at the end of the blade's live window, the
  attack's whole carry behind it, with no stop between: one cubic with those two slopes. Given further to go than it would coast, it
  surges into the cut.
- The speed is whatever the body really had (a walk barely under way, a sprint), up to 1.3 times what the attack's clip was made for.
  The clip's feet are planted for 4 m/s or 7.5 m/s (`SwordStrikes.EntrySpeed`); at another pace, or stopped short by a wall or the
  target, the stride is drawn in or out by how far the body has really gone (`PlayerLocomotion.SetClipStride`).
- What the body had ACROSS the swing's way (strafing as it clicked) is left on its feet and brakes as it always did.
- A swing cut short while it is still carrying (a blow, a dash, a light turned into the heavy) gives what pace it has left back to the
  feet (`GiveMomentum`), which brake it from there.
- A swing is still a commitment: the keys do not steer it. It is aimed by where the camera looks at the click, as before.

## The clips

`CityX/SourceArt/SwordMoves/sword_moves_pose.py`, the four attacks under THROWN ON THE MOVE: legs, hips, torso and head, as every
attack's clip (the arms are the standing cut's own keys: the slash's, the sweep's, the finisher's and the two-handed cut's). `stepped`
there is `SwordStrikes.Carried`: keep them in step.

## Tests

- `SwordMotionTests`: the opener chosen by the pace; the carry (sets off at the pace it came in at, never stops or goes back, at rest
  at the end of the live window, at half the made-for pace and at 1.3 times it); the clips (nothing goes back, the body leans into
  its pace, the right foot passes first, the left is planted under the blow and does not slide).
- `SoulsSwordRealInputPlayTests`: W held then a click, and W + Shift then a click, with real keys in the real scene: the right opener
  is thrown, a tenth of a second in the body still has three quarters of its pace or more, it never all but stops before the blow, it
  is carried as far as the attack is made for, and the next click chains into the second light.
- `SwordArmsClearPlayTests`: the arms clear of the torso and the head through both, with every kind of sword.
- `SwordStrikeCapture.RenderTheSwordAttacks` renders `strike-<sword>-walk.png` and `strike-<sword>-run.png`.

## Not done

- **The knife** has the one opener.
- **Backwards and sideways**: backing off or strafing throws the standing cut (in Dark Souls too).
- **Out of a dash or a jump**: no rolling attack, no plunging attack.
- **A heavy on the move** is the standing heavy; the pace is braked as before.
- **Enemies** throw the same openers when they come in at a walk or a run.

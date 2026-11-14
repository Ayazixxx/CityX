# Moving freely while attacking with a sword

Owner, 2026-11-13, three times in one day, each after playing the last:

1. "the sword combat animations are stilted and awkward. they only use the arms ... akin to dark souls combat" (docs/SWORD_FULL_BODY.md:
   the whole body, and, of the two choices offered, committed swings: the body rooted, carried by the attack's own step).
2. "it feels like attacking forces you to stay still or slow down ... the movement of the character carries over into the attack, for
   instance if you are sprinting it does a different attack, if you are walking forward it does a slightly different attack".
3. "You need to have the sword movements conserve momentum and move freely while attacking. It feels bad right now because you are
   slowed down every time you attack. **You should be able to move freely with zero slowdown**".

The third is the rule, and it is the one in the game.

## The rule

**A sword swing takes nothing from the player's movement.** The keys walk, sprint and steer the body through every swing, wind-up,
blow and recovery alike, exactly as if no sword were swung. Measured with real keys in the real scene
(`SoulsSwordRealInputPlayTests`): walking at 4.00 m/s, never under 4.00 m/s through the first light and on into the second; sprinting
at 7.50 m/s, never under 7.50 m/s through the running attack and on into the second light; the ground covered is what that pace covers
in that time, no less and no more.

- `PlayerInputReader` no longer scales the keys or drops the sprint under a sword swing. (It slowed the walk to 0.65 and stopped the
  sprint until 2026-11-13, and rooted the body for a few hours of that day.) An unarmed punch still roots; a held guard still walks
  at half pace.
- An attack neither adds to the pace: its own step (`SoulsSwordCombat.ApplyLunge`, the half metre a standing cut steps) is given
  only to a body standing still, and fades out as the body's own speed goes from 0.4 to 1.6 m/s (`SwordStrikes.StandingShare`).
- An enemy's swing still holds it to 0.65 of its pace and still steps it into every cut, standing or walking
  (`SoulsSwordCombat.CommittedMoveSpeedFraction`, `SteppedOnTheMove`, set by `SwordsmanBrain`), as every swing did before: the owner's
  words are about the player, and the enemy's fighting was tuned on that (`SwordsmanPlayTests`: with the step gone on the move he
  stopped landing blows).

## The body under a moving swing

Each attack has a clip of the legs, hips, torso and head (docs/SWORD_FULL_BODY.md). Its feet are planted for a body that stands and
strides; on a body the keys are moving they would be nailed to ground that is going by. So:

- **Standing** (the body's own speed under 0.4 m/s): the whole clip, the stride and the attack's step, as made.
- **On the move** (over 1.6 m/s): the clip gives the hips and legs back to the walk or the run (`BakedPose.LegsWeight`,
  `SwordStrikes.ClipLegs`, `SwordPoseDriver`); the foot IK is on again under them; the clip keeps the torso's coil and the head, and
  the arms are the attack's own keys as always. Between the two speeds the legs are part each, and they change over in 0.15 s when
  the body moves off or comes to a stand mid-swing.
- The body's own speed is the Animator's (`PlayerLocomotion.OwnGroundSpeed`), which another player's copy is sent, so a copy's legs
  do the same.

## The running attack

"If you are sprinting it does a different attack": the light that opens an attack while the body is sprinting forward (5.8 m/s and
over toward where it will swing; `SwordStrikes.OpenerFor`, `SoulsSwordCombat.OpenerAt`) is the running attack: `sword1h_run`,
`sword2h_run`. It is the chain's last cut (the diagonal from over the right shoulder) brought down on the run, with a torso that
leans into the run all the way, over legs that are the sprint's own. Harder than a first light (1.35 against 0.92 one-handed, 1.75
against 1.23 two-handed), dearer in stamina, a longer recovery before the next attack can start; it chains on into the second light.
Which cut it is and what it costs are my numbers: the owner asked for a different attack at a sprint and said no more.

At a walk the opener is the chain's first cut, as standing: the "slightly different attack" is the same cut over walking legs and a
torso that goes with them. (For a few hours there were separate clips for it, with the body carried into the cut on a fixed path;
they went with the rule above.)

## Tests

- `SoulsSwordRealInputPlayTests`: standing, a click steps the body its stride; W held, and W + Shift held, a click swings (the
  running attack at a sprint), the pace is measured a tenth of a second at a time through the swing and into the chain's next cut and
  never drops under 95% of what it was (the measure's own grain), and the ground covered is that pace's.
- `SoulsSwordRigPlayTests.TheLegsKeepWalkingWhileTheSwordSwings`: the feet step as they step walking, the hand swings, the hips are
  not dragged about.
- `SwordMotionTests`: the legs' share by speed, nothing stepped on the move, the running attacks' torso, the opener by pace.
- `SwordArmsClearPlayTests`: the arms clear of the torso and the head standing, at a walk and at a sprint, with five kinds of sword,
  and the clip never holding the legs of a body on the move.
- `SwordStrikeCapture.RenderTheSwordAttacks` renders `strike-<sword>-walk.png` and `strike-<sword>-run.png` with the keys held.

## Not done

- **The knife** has no running attack.
- **A heavy** is the same heavy standing or moving (over moving legs when moving).
- **Out of a dash or a jump**: no attack of its own.
- **Enemies** throw the running attack when they come in at a sprint, and are still slowed by their own swings.
- **The test swordsman can be stun-locked by a player who walks after him swinging** (found by `SwordsmanPlayTests` the day the
  movement was freed): every cut lands, and he is stunned again before he can raise his guard or finish a wind-up. A player who lets go
  of the keys to swing gets the duel the test describes. His brain was tuned against swings that slowed or rooted the player; it has not
  been retuned.

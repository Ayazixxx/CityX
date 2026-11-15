# The one-handed sword's combo, from the owner's reference (2026-11-13)

Owner: "the one handed sword combat is feeling better but i want to try different animations, can you make the animation exactly
like this gif. this gif shows two sort of downward/diagonal slashes and then 1 horizontal slash, then the last one was a fourth
downward slash that hits the ground and has like a sparks effect from the ground. I want the first 3 to replace the 3 hit combo
animations we have now and then, the last one to replace our charged attack (hold click) animation." Then, of the third: "you can
reuse our horizontal slash animation for the third combo attack, make it swing in the same direction" and "match the direction of
the gif".

The reference is a 2D sprite seen from the side (40 frames, 0.06 s each; kept outside the repo, in the owner's Downloads). It was
split into its frames and read one by one. It is a reference for the MOTION only: the body, the sword and the effects are the game's.

## What the reference does, and what the game does

| | The reference | In the game |
|---|---|---|
| Light 1 (`sword1h_slash`) | Sword raised back over the shoulder; a steep diagonal cut down across the front; the body drops into a deep forward lunge; the blade carries on low to trail behind; the free arm is flung up behind. | The same, but for the free arm, which swings back long and low (the owner, 2026-10-17, wants that hand low, not held up). `SwordStrikes.SteepDown` (55 degrees off the level), the old finisher's coil and stride with a deeper lunge. Also the running attack's arm (`sword1h_run`). |
| Light 2 (`sword1h_cut`) | The body turns a full circle; out of it a steep cut from the other shoulder down across the front; the blade carries round. | No turn (owner, 2026-11-14): `SteepDownBackhand`, the first cut's mirror, from where the body stands. (Was: thrown out of a full turn of the whole body to the right that arrives as the blade lands (`SwordStrikes.SpinDegrees`). |
| Light 3 (`sword1h_finisher`) | A level slash at the height of the head: the blade crosses the front from the fighter's left to his right (read off frames 19 to 23), the body turning with it. | The game's own level slash (the chain's old first cut), right to left (`ForehandFlat`), at chest height, on the finisher's timing. It went left to right as the reference does until 2026-11-15 (owner: "switch the direction of the third swing make it a horizontal swing right to left", "you can reuse the old horizontal swing animation"): its keys and its clip are the old cut's again, the left foot striding. No full turn. |
| Heavy, the held click (`sword1h_overhead`) | Sword raised and drawn back over the head; slammed down in front into the ground in one frame; a burst of sparks at the ground; the body held in a deep crouch over the blade, free arm up behind, for 0.3 to 0.4 s; then it rises. | The same: the blade comes over the top into the ground ahead (`KeepTipAboveGround` rests its tip there), the body folds into a deep lunge over the front knee and stays down 0.38 s (`TimingOf`: the settle is 0.26 s, was 0.16), sparks fly from where the tip struck. |

Timing is the game's own (a light lands about 0.4 s after the click), not the reference's 0.48 s a hit: the owner had just said the
one-handed combat "is feeling better", and the reference has no wind-up an opponent could read.

## No turn, and straight lines (owner, 2026-11-14)

The second cut used to be thrown out of a full turn of the whole body, read off the reference. The owner: "i dont know why the one
handed sword combo does this gay little twirl on the second swing i never asked for the nor did the gif i provided show that". The
turn is gone: the second cut is the first's mirror, thrown from where the body stands.

And of the first: "the first slash changes direction mid slash ... just make it diagonal". The hand came forward over the top, then
across, then levelled off round behind the body. Now each of the two cuts is one straight diagonal: the hand goes down one line from
over the shoulder to past the opposite hip and the blade ends pointing down the cut (the first from upper right to lower left, the
second the mirror). Seen in `Logs/CityShots/strike-1h-light1.png` and `light2.png` (`SwordStrikeCapture`).
## Sparks from the ground

`SwordStrikes.StrikesTheGround` (the one-handed heavy). A frame after its contact `SwordPoseDriver.ThrowGroundSparks` asks the held
blade where its tip is and, if it is within `GroundStrikeReach` (0.35 m) of the ground the body stands on, calls
`BlockDeflectEffect.GroundStrike`: 44 sparks thrown up out of the ground (more, and longer in the air, than a clash's), a flash and the
blade's ring. It runs on every body that plays the blow, the owner's and each copy's, so every player sees it with nothing sent.

## Tests

`SwordStrikesTests` (`TheOneHandedChainIsTwoSteepCutsAndThenALevelSlashFromLeftToRight`, `TheSecondCutIsThrownOutOfAFullTurnThatArrivesWithTheBlade`,
`TheFirstCutDropsIntoALungeAndTheThirdIsSlowestToRecoverFrom`, `TheOneHandedHeavyGoesIntoTheGround`), `SwordMotionTests` (the clips), and
`SwordGifComboPlayTests` on the real body: the charged attack's tip reaches the ground and sparks fly once, in front, on the player and on
another player's copy; the second cut turns the shoulders once round standing and walking, the first does not.

## Pictures

`tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordStrikeCapture.RenderTheSwordAttacks` writes
`CityX/Logs/CityShots/strike-1h-{light1,light2,light3,heavy}.png`: ten moments of each attack from the side, the front and behind.

## Also changed

- A one-handed blade stopped on a guard stands steeper (`SwordStrikes.StoppedBlade1H`): the first cut now comes down out of a deeper
  lunge, nearer the blocker, and its point was 3 cm inside their head (`GuardClashPlayTests`).

## Not done

- The reference's third slash is at the height of the head and turns the body further; the owner asked for the game's own slash.

- The two-handed heavies also strike the ground and throw no sparks: the owner asked for the one-handed charged attack.

## The third swing's blade went through the body (owner, 2026-11-14)

"on the third swing, the sword goes through his body". Its wind-up drew the hand across the chest with the blade back behind it, and
since a wind-up's keys are in the frame of the shoulders as they are wound (55 degrees to the left here), the hand ended up beside and
behind the chest with the blade lying across the body, 12 to 21 cm inside it. The hand is now out past the left shoulder and in front,
the blade back beside the body. `SwordArmsClearPlayTests` now also holds the blade, along its length past the hand, out of the torso and
the head through every swing of every weapon (the least for the one-handed chain: 2 cm clear).

## The trails turned (owner, 2026-11-14)

"i dont like this change of direction mid swing it looks bad. fix the trail and trajectory of the swing", then, with a picture, "im still
seeing the trails switching direction". Measured from behind, as the game's camera sees it (`SwordGifComboPlayTests
.TheDiagonalCutsSweepTheTipOneWayAsTheCameraSeesIt`), the first cut's tip turned 127 degrees half way and the second's 89. Two causes:

- The planes the two cuts swing in (`SteepDown`, `SteepDownBackhand`) were tipped off the straight-ahead, so the arc's shape on the
  screen bent: the tip fell straight down, then went across. They now hold the straight-ahead, and seen from behind the tip draws one line.
- Both cuts were aimed through the floor: the tip was held off it (`PlayerLocomotion.KeepTipAboveGround`) and slid along it, sideways.
  They now finish higher and shorter (the first ends pointing forward and down to the left, not straight down), so the tip stays 20 to
  35 cm off the floor by itself. The first cut's follow-through is quicker to keep the blade's speed through the hit (the clip rebaked).

Now: 2 and 5 degrees. The third swing's blade, measured against the body's own shape at its real size (the old check used a thin tube 18%
too small), keeps 22 cm or more off it; `SwordArmsClearPlayTests` holds every weapon's blade 6 cm off the skin, the combo also at a walk.

## The second cut's arm (owner, 2026-11-14)

"work on the second swing make it more human like animation the arm bends strangly". Measured through each cut
(`SwordGifComboPlayTests.TheFirstTwoCutsMoveTheArmAsAnArmMoves`: the angle between the forearm and the knuckles, the elbow against
the line from shoulder to hand, the hand's distance from the head): the second cut's wrist was past 100 degrees through the whole hit and
folded 154 degrees back on itself at the end of the follow-through. The first cut's peaked at 81.

Why: the sword sits in the fist with its single edge where the knuckles face, and every pose keeps the edge leading the cut (owner,
2026-10-16). In a right-handed backhand that turns the knuckles the way the cut travels while the forearm reaches forward, and once the
blade swings on round after the hit the leading edge faces backwards and takes the hand round with it.

What changed:
- **The wrist gives** (`PlayerLocomotion.WithinAWristsBend`): a one-handed attack's hand rolls round the handle, the blade keeping its
  line, as little as brings the wrist within 55 degrees. While the blade can hit it rolls at most 40 degrees, so the edge still leads
  (`SwordStrikes.WristRollDegrees`); outside that, as far as it needs. A ground strike's wrist holds firm.
- **A backhand's elbow can come across the chest** (`SwordFrame.ElbowOut` below 0): the second cut meets its target with the hand on the
  right, the forearm level across the front and the elbow in front of the chest, and finishes with the arm carried out to the right.
- **Its wind-up** has the elbow out and the sword cocked back over the left shoulder, not the forearm jammed across the face; its blade
  turns less after the hit (the follow-through is quicker, the clip rebaked to match).

Now: the first cut stays at or under 55 degrees; the second sits about 50 through the wind-up, 105-116 for the tenth of a second the
sword whips through behind a hand already facing the cut, and about 85 after it. The test holds both under 120.

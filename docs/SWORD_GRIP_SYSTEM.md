# Holding a sword like a person: grip and wrist (2026-11-15)

Owner, 2026-11-15, with a screenshot of the one-handed second cut from behind: "the second swing is still off the way he holds it is
weird why is his wrist twisted like that. find out a system that makes it so we can generate animation with proper "grip" and wrist
positioning, look at other sword animations and research ways to eliminate this reoccuring issue of the weapons being held awkwardly
when swinging", then "research hand and wrist positioning in relevant games".

The issue has come back many times: 2026-10-16 ("angle the arm properly so theres no that weird bend"), 2026-10-17 ("bent at the wrong
part of the arm"), 2026-11-13 ("wrists all twisted and poking through his torso"), 2026-11-14 ("the arm bends strangly"), and this.

## What the research says

Studios rarely publish how their swings are built (no Mordhau, Chivalry 2 or FromSoftware write-ups turned up; For Honor's GDC 2016
talk is about motion matching on weekly motion capture). What does exist agrees on a few things:

- **The sword is a child of the hand.** The hand is posed and the blade goes where the hand points it; animators build a grip once per
  weapon (a hold bone or a pose-library grip) and reuse it (Sebastian Lague's Blender attack animations; the Cascadeur "OG Realistic
  Combat" write-up; Babylon/Unity attach-to-bone). IK is used to correct, mostly to put the off hand on a two-handed grip.
- **The arm and shoulder carry the arc, not the wrist.** Small wrist angles are magnified at the tip, so the wrist adds a snap at the
  peak and little else (Animation Mentor; MoCap Online's sword guide: "wrist-snap at swing peak").
- **The wrist stays close to straight.** Historical fencing guides: "if your wrist is bent at a sharp angle during a particular guard or
  cut, then chances are you're doing it wrong"; the wrists stay in line with the forearms and roll inward during cuts. Edge alignment
  comes from turning the forearm, not bending the wrist; reversed cuts change the grip (the thumb on the flat, crossed hands).
- **At the hit the sword continues the arm.** The arm extends and the blade lies along its line behind the edge.
- An Unreal developer described the same trap we fell into: "ideally the angle of the wrist would be determined by the IK using
  preferred/limit angles, but that becomes kind of a chicken-and-egg problem as rotating the wrist rotates the weapon."

## Why ours kept going wrong

Our swings are written the other way round from all of that: each pose says where the hand is, which way the BLADE points and which way
its edge faces, and the hand is turned to fit the blade. The elbow went wherever a fixed rule bowed it (down and out). Whatever did not
fit, the wrist took. With a single-edged sword whose edge faces the knuckles and must lead every cut (owner, 2026-10-16), a right-handed
backhand turned the knuckles one way while the forearm reached another: the second cut's wrist was past 100 degrees through the whole hit
and folded 154 degrees back on itself after it. Measured, not guessed: `SwordGifComboPlayTests.TheFirstTwoCutsMoveTheArmAsAnArmMoves`.

## The system

1. **The elbow finds the grip** (`SwordElbow`, `SwordFrame.ElbowFindsTheGrip`). For every frame of a one-handed attack, the elbow is
   tried all the way round the circle it can sit on (the hand where the pose puts it) and put where the forearm runs into the hand as a
   relaxed grip has it (the wrist tipped 35 degrees to the little finger, `HeldWristCockDegrees`), with an easy-hanging elbow and last
   frame's elbow as the tie-breaks. The Octaslash's Blender solver did this already; the swings never had it.
2. **The wrist gives, a little** (`WithinAWristsBend`, `SwordStrikes.WristRollDegrees`). If a hand would still bend past 55 degrees, it
   rolls round the handle, the blade keeping its line: up to 40 degrees while the blade can hit (the edge still leads), as far as needed
   outside that, never on a ground strike.
3. **The arm reaches along the blade at the hit** (`SwordStrikes.ArmAlongBlade`). A pose can put the hand on the line from the shoulder
   along the blade, so the arm and the sword are one line. The second cut's hit and follow-through use it: before, the hand stayed at
   shoulder height while the blade pointed steeply down, a sword hung from a wrist bent sideways (the owner's screenshot).
4. **Measured on every change.** The wrist is counted from the relaxed grip: from the hit on no more than a wrist goes (75 degrees), and
   through the strike's whip, when the sword lags a tenth of a second behind a hand already turned to the cut, no more than 95.

Now: the first cut 57 degrees from the hit on (61 in the whip), the second 73 (87). It was past 100 through the hit and 120 (folded)
after it, counted the same way.

## Not done, and the next step if it is still not right

- **The grip itself.** The game's one-handed hold has the handle square across the fist; a real hand holds it diagonally, pommel toward
  the heel of the hand, which is what lets the blade continue the forearm with a straight wrist. Turning every one-handed hold that way
  changes the carry, the guard and every attack, and the owner has tuned holds by hand (`WeaponGripSettings`), so it is a decision of its
  own.
- **Authoring the arms in Blender.** The full-body clips carry the legs, hips, torso and head; the arms are still the game's keys. The
  Octaslash and the stab dash show the other way: the arm solved in Blender with the elbow chosen for the wrist, baked, and played as it
  is. Moving the sword attacks' arms into the clips would let every swing be posed and checked by eye in Blender, frame by frame.
- Only the first two one-handed cuts are measured so far; the other attacks and the two-handed weapons (whose right hand is held by the
  grip of both hands) are not.

## Sources

- [Animation Mentor: animating sword fights](https://www.animationmentor.com/blog/tutorial-animate-sword-fights-battle-sequences/)
- [MoCap Online: sword melee animation guide](https://mocaponline.com/blogs/mocap-news/sword-melee-animation-guide)
- [Cascadeur: OG Realistic Combat System](https://cascadeur.com/blog/view/indie-project-spotlight-og-realistic-combat-system)
- [Sebastian Lague: RPG attack animations in Blender](https://rosetta.to/u/sebastianlague/rpg-graphics-e10-attack-animations-blender)
- [Unreal forum: two-handed melee, wrist IK and the weapon](https://forums.unrealengine.com/t/handling-left-hand-for-dynamic-two-handed-melee-animations/2695878)
- [swordfight.uk: gripping the sword](https://swordfight.uk/gripping-the-sword/), [the six cuts](https://swordfight.uk/the-six-cuts/), [reversed cuts](https://swordfight.uk/?p=1054)
- [Andrei Chirlesan: grips guide](https://docs.google.com/document/d/1hKTJhYnfgCwQ3o3FCpClOb9M9pROabNHYuNVAQFVI3A/export?format=pdf)
- [ARMA: what makes an effective sword cut](https://thearma.org/essays/howacutworks.htm)
- [Game Developer: motion matching talks of 2016 (For Honor)](https://gamedeveloper.com/programming/most-inspiring-game-animation-tech-talks-of-2016)
- [Roblox DevForum: how Mordhau and Chivalry 2 swing](https://devforum.roblox.com/t/how-do-games-like-mordhau-chivalry-2-and-criminality-achieve-their-procedural-weapon-animation-swings/3129339)

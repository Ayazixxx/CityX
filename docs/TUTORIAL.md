# The tutorial: from "show you the ropes" to the hub

Owner, 2026-11-16: "can you just complete the rest of the tutorial based off what we have talked about and make the dialogue and everything
explaining all the basics of the game, i will go and make it what it needs to be i just need something to start with ... for the savior
taking you to the hub just have the screen fade to black and unfade then you appear at the hub ... just wrap up the tutorial from the point
of after old guy says "i can teach you the ropes around here" also it should be "show you the ropes". make the dialogue and objectives and
everything after that needed to wrap up the tutorial. also make the swordsman upstairs only hostile once he lays eyes on you i shouldnt
hear him dashing around upstairs before i even go up there", then "the dialogue can be low effort im going to write it all myself again
later just make a basic guide of how you think it should be i can work with".

So this is a **first draft to rewrite**. Every word is in one file, `CityX/Assets/CityX/Character/Story/TutorialScript.cs`: the objectives,
their hints, and all of Old Guy's lines. Changing the words there changes nothing else.

The plan it follows: `GAME_DESIGN.md`, "The tutorial", and the house: `OLD_GUYS_HOUSE.md`.

## The steps

The objective shows in the top-right corner of the screen (a dark box with a gold rim: OBJECTIVE, what to do, a hint how). A step done by
doing passes as soon as it is done, even if it was done early (turning the power on before Old Guy asks skips that step). A talk step passes
when the conversation with Old Guy ends.

| Step | Objective (hint) | Done when | Old Guy says |
|---|---|---|---|
| (offer) | none | you say "Sure" | "Dude I just saw you fall from the sky", "I can show you the ropes around here if you'd like" [Sure / I'll figure things out myself] |
| GetIn | Get into Old Guy's house (Break the boards on the front door, or a window.) | you are inside the walls | It's his house; a guy with a sword boarded him out; bust the boards or a window; there's stuff to fight with; he'll wait outside |
| FindWeapon | Find something to fight with (Look around the house. E picks things up and opens drawers.) | you hold or carry the kitchen knife, butter knife, slingshot, magic wand or bat | (at the steps) "He's upstairs somewhere. Go on, I'll be right here." |
| Intruder | Get rid of the intruder upstairs (Left click attacks. Double tap Shift to dash.) | the swordsman is dead | same |
| ReportBack | Tell Old Guy the house is clear (He's in his den on the main floor. E talks.) | you hear him out | You did it; the power's out; fuse box in the workshop by the garage; **stay out of the boxes in the cellar** |
| Power | Turn the power back on (The fuse box is in the workshop, by the garage.) | the fuse box's lever is thrown | reminder of where the fuse box is |
| BackToOldGuy | Talk to Old Guy (He's in his den.) | you hear him out | (if you opened the cellar boxes: "Were you going through my boxes?...") Lights back on, you're getting stronger; stat points: C, Player, Stats; skill points: the skill tree, same menu, Skills |
| Stats | Spend your stat points (Press C, then Player, then Stats.) | no stat points left | reminder |
| Skill | Learn a skill (Press C, then Player, then Skills.) | no skill points left | reminder |
| LastTalk | Talk to Old Guy (He's in his den.) | you answer about the faction and hear him out | Now you know enough to get killed; spells go on the hotbar, number keys; **want to start a faction with me?** [Let's do it / Not really]; a room with a bed in it is yours (claiming); **"Alright, I think we're safe now."** |
| Ambush | none | the ending is over | nothing |

Between steps, by itself:
- **Old Guy moves into his den** once the swordsman is dead. He waits for a moment when you can see neither the steps outside nor the den
  (a wall or floor in between counts), so you never see him pop.
- **The level-up:** going from BackToOldGuy to Stats, if you have no stat points he gives you exactly enough XP for the next level (the
  swordsman gives 80 of the 100 the first level needs), so there are points to spend.

## The ending

Right after "Alright, I think we're safe now.":
1. About 2.4 seconds of gunfire from outside, through the den's windows. The windows shatter, and bullet trails fly into the den. Every
   third shot goes at you, every third at Old Guy, and the rest all round the room. The shots use the AK-47's sound.
2. You are shot down (the ragdoll, with no death screen), and Old Guy falls over a moment later.
3. After a second and a half on the floor, the screen fades to black.
4. In the black: you are brought back, and you stand in the middle of the sky hub's island, facing the side the house is on. Old Guy is
   gone, and the tutorial is saved as done for that character, so he is never by his house again.
5. The screen fades back, and "Welcome to the hub" shows in the big banner.

## Placeholders (to replace)

- **Old Guy's fall** is a stiff topple (he has no ragdoll). A real death animation or a ragdoll is still to come.
- **No rescuer and no Heaven or Hell**, as asked: the black screen stands in for them.
- **The shooters are never seen.** Who fired is still open.
- **The faction answer** is kept (yes or no) but does nothing yet; factions are not built.
- **The cellar boxes:** opening one after his warning is noticed. It changes one line of his and is kept as a flag for karma, which
  does not exist yet. The bat's crate is one of those boxes.
- **Claiming** is only mentioned ("a room with a bed in it"), since how it works is still yours to decide. The old `/claim` and Old Guy
  moving into your building (from the Godot version) are gone.
- **The swordsman's begging** (a karma choice in the design) is not built.

## The swordsman upstairs

He waits in Old Guy's bedroom and fights nobody until he lays eyes on someone. They have to be:
- in front of him (80 degrees to either side),
- with no wall, floor or glass between his eyes and their chest or head.

He still notices someone who walks right up to him (within 2.5 m) or hits him. He can still stroll round the bedroom; he just doesn't come
for you through the floor any more.

How it works: the house marks his spot when it puts him down (`WaitingEnemies`), and his mind claims the mark on the machine that runs him,
so it works online too. Every other swordsman notices as before.

## Trying one part

The dev console (backtick) has `/tutorial <step>`, which puts the tutorial straight onto a step. From `reportback` on, Old Guy is put in
his den. For example, `/tutorial ambush` plays the ending. On its own, `/tutorial` says which step you are on.

## Code

| Piece | File |
|---|---|
| The steps and what passes them | `Character/Story/TutorialProgress.cs` |
| Every word | `Character/Story/TutorialScript.cs` |
| Old Guy: what he says at each step, moving unseen, his fall | `Character/NPC/OldGuy.cs` |
| Watching the world, the den, the level-up, the boxes, the console | `UI/Story/TutorialDirector.cs` |
| The ending | `UI/Story/TutorialDirector.Ending.cs` |
| The objective box | `UI/Hud/ObjectiveView.cs` |
| The swordsman's eyes | `Weapons/NPC/SwordsmanBrain.cs` (`WaitsToSee`, `Notices`), `Character/NPC/WaitingEnemies.cs`, `Character/NPC/Sight.cs` |
| Done for good | `CharacterSave.TutorialDone`; `OldGuySpawner` skips him |

Tests: `TutorialProgressTests`, `OldGuyTests`, `OldGuyStagingTests`, `OldGuysHousePlanTests` (his den spot), `CharacterServiceTests`
(done stays done), `SwordsmanWaitsToSeePlayTests`, `TutorialEndingPlayTests`. `TutorialCapture` (Explicit, `-Graphics`) renders the
objective box in the house and the ending (`Logs/CityShots/tutorial-*.png`).

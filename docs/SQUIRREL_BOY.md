# Squirrel Boy (2026-11-25)

Owner, 2026-11-25: "make an npc called squirrel boy that uses my squirrel boy character i made and the outfit he is wearing now. the sb shirt
i designed, the squirrel mask, the brown cape and gloves, and khakis with bwsneakers. he is non hostile until struck and then he fights
unarmed with fists only for now. he is also able to crawl up walls and will run away and crawl up a wall when low health"

## How it plays

- **`/spawn squirrelboy`** puts him down in front of you. He is not in the world's own spawns (`docs/MOB_SPAWNING.md`): where he turns up is
  the owner's call.
- **Looks**: the owner's character "squirrel boy" (his save copied into `NpcLooks/SquirrelBoy.json`, so he never depends on one machine's
  saves): his skin and paint, no hair, and his outfit: the T-shirt with the owner's **SB** design printed on it, the Squirrel Mask, the brown
  cape, the brown fingerless gloves, khakis and black-and-white sneakers.
- **Calm** until somebody hurts him: he stands where he is, even beside you.
- **Struck**, he fights whoever hit him (`Opponents.WhoStruck`; once they are gone, whoever is nearest) with his fists, the player's own
  punches: he closes in (running from over 5 m), throws two to four punches, one in five of them heavy, then pauses about a second before
  the next combo.
- **His claws** (owner, 2026-11-25, with a picture of three curved glowing slashes: "give squirrel boy a claw attack with a trail that
  looks like the image attached. you can use the same swing used in the wraith punch. you decide how often he should do it"): about one
  combo in three ends with a claw swipe, never two within 5 s (my choice: `SquirrelBoyMind.ClawShare`, `ClawCooldownSeconds`). The swing
  is the Wraith's punch (SourceArt/WraithPunch: drawn back, over and down) on his upper body, his legs walking on; three streaks of light
  follow his claws, white in the middle glowing peach at their edges, pointed at both ends, drawn only while the claws cut fast and fading
  a third of a second after, a few sparks flying off (`ClawTrail`, `ClawSlashMaterialBuilder`). As the swing comes down it cuts whoever
  stands in front within reach of the middle claw: 15 damage (a fist's 10, the Wraith's punch 20; mine), half a second's stun, and it
  bleeds (an edged blow). Other players see the swipe and its streaks (announced by name, `PlayerAnimationDriver.ClawSwipeName`).
- **Below 30% of his health** he runs for the nearest wall he can climb (a wall standing up that goes on at least 6 m above the ground, one
  past you counting for less) and goes up it hand over hand with the player's own climb, and over the top. He never tires on a wall. With no
  wall within 25 m he just runs away from you.
- **At the top** he stays. Follow him up (within 4 m) and he runs again. Knocked off the wall, he goes back to it.
- Beaten, his body lies 25 s and is taken away. Worth 80 XP (the swordsman's: mine).

My choices, all easy to change: the 30% (`SquirrelBoyMind.FleeBelowHealth`), the combo sizes and pauses, 80 XP, standing still while calm (the
hobos wander), and the 6 m a wall must reach to be worth climbing.

## Online

He is a body enemy like the swordsman (`NetworkedSquirrelBoy.prefab`): the machine that brings him in runs his mind, everyone sees his walk,
punches, climb and health. Every machine dresses its copy from the prefab, the SB design included (handed to that machine's design library
from the prefab, never fetched).

## How it is built

| Piece | What |
|---|---|
| `Character/NPC/SquirrelBoyMind` | what he decides, pure: calm, fight, flee, climb, perched |
| `Weapons/NPC/SquirrelBoyBrain` | the body doing it: the player's fists (`MeleeAttacker`), walk and run (`PlayerLocomotion`), climb (`PlayerClimbing`); finds his foe and a wall; puts on his clothes and prints the shirt |
| `Character/Combat/ClawSwipe`, `ClawTrail` | the claw swipe (the Wraith's punch, the cut) and its three streaks; any body can carry them |
| `Weapons/Editor/SquirrelBoyBuilder` | `SquirrelBoy.prefab`: `EnemyBody.FromPlayer` in the owner's look (`DuelistBuilder`), the SB design (`NpcLooks/SquirrelBoyShirt.bytes`) |
| `NetworkedEnemyBuilder` | the networked copy, his mind run by the owner alone |

To change his outfit: dress the owner's character in the creator, copy its save over `NpcLooks/SquirrelBoy.json` and rebuild the scene. A new
SB design: copy its `.design` file over `NpcLooks/SquirrelBoyShirt.bytes`.

## Tests

`SquirrelBoyMindTests` (calm until struck, combos with pauses, the run for the wall and the climb, no wall, stunned), `SquirrelBoyTests` (his
outfit, the networked copy) and `SquirrelBoyPlayTests` (on the real body: the printed shirt, calm then punching the player, up a 20 m wall when
badly hurt).

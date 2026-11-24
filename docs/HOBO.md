# Hobos (2026-11-24)

Owner, 2026-11-24, before a playtest with a friend: "make an enemy called hobo and it spawns as either the character i made named hobo2 with
the saem outfit white shirt green parka khakis, blackgloves, blacksneakers. and then the other type that can spawn is just hobo. with his outfit
he has on blackjean and short and bwsneakers and black gloves. make it so they spawn with either a metal pole, broken glass bottle. or a chain,
or a machete as their weapon. its random which one and which skin they get. make them not hostile until hit they jus walk around. and make it so
they throw a bottle at you if you are distanced from them and they have a bottle in hand. if they spawn with the broken bottle make it so they
have 3 not broken but empty glass bottles in their inventory they can throw and are lootable upon death."

The first slice of the design in `docs/GAME_DESIGN.md` (Hobos). Not in it yet: other hobos joining in, running from guns and magic, karma.

## How it plays

- **`/spawn hobo`** puts one down: either look, any of the four weapons, at random. `/spawn hobopole`, `hobobottle`, `hobochain`,
  `hobomachete` pick the weapon (to try each).
- **In the streets**: hobos appear out of doors with the ghosts and the monsters (`docs/MOB_SPAWNING.md`): of the outdoor ones, a quarter
  monsters, 40% hobos (mine), the rest ghosts. The outdoor budget (4 alive round a player) is unchanged.
- **They wander** and leave everyone alone, even standing right beside them.
- **Hit one** and he fights back: whoever is nearest when he is hurt (the one who hit him, nearly always). He fights with the
  swordsman's mind (guards, dashes, punishes) and the weapon's own moves, as the player swings it.
- **With the broken bottle** he carries three whole empty ones. When the one he fights is 6 to 24 m off, in front of him and in sight,
  he stops and throws one (a whole bottle shows in his hand while the arm goes back), lobbed to land on them, every 2.5 s at most.
  Nearer, he fights with the broken one. Thrown, a bottle does what a player's does (10).
- **Beaten**: his weapon falls from his hand (the bottle still broken), and his body can be searched: his clothes and **the bottles he
  did not throw**.
- Worth 40 XP (the swordsman's 80: mine).

## Online

- Each look with each weapon is a prefab of its own (eight, `Character/Hobos/Hobo_1_Pole` ... `Hobo_2_Machete`, and a networked
  copy of each), so every machine spawns the same hobo without being told which. `"hobo"` is rolled where it is asked for.
- The machine that runs him decides; the others see his walk, swings, guard (as every enemy's), and the throw: the arm, the whole bottle
  in the hand, the bottle flying (`BottleThrower`, the player's own). Their copy of him carries one fewer with each throw, so his body
  lists the same bottles on every screen.
- Taking from his body: the server gives each bottle to the first to ask, as many times as he carried them (before, an item could be
  taken off a body only once).

## How it is built

| Piece | What |
|---|---|
| `Character/NPC/HoboKinds` | the eight makes, their names, the roll |
| `Weapons/Editor/HoboBuilder` | the prefabs: `DuelistBuilder` with the owner's looks (`NpcLooks/Hobo.json`, `Hobo2.json`), each weapon, calm until hurt, the broken bottle and three spares |
| `Weapons/NPC/SwordsmanBrain` | now takes anything swung the sword's way (`ISwordBlade`), its state at spawn, and `PeacefulUntilHurt` |
| `Weapons/NPC/SwordsmanBrain.Throwing` | the throw from a distance |
| `Weapons/NPC/BottleLob` | the arc that lands on the target |
| `Character/Interaction/CarriedLoot` | what a body carries besides what it wears; listed on the fallen body (`CorpseLoot`) |
| `BottleWeapon.ShowSpare`, `BottleThrower` | the whole bottle in the hand for a spare's throw, told to other screens |

## Tests

`HoboTests` (the makes and the roll, the name on a fallen one, the lob lands on its mark, hobos out of doors, every prefab's weapon,
spares, throw and networked copy, both outfits), `HoboPlayTests` (calm beside a player until hurt, then fights; each weapon in the hand,
the bottle broken; from 14 m he throws and hits, a copy carries one fewer and shows the whole bottle, the rest are on his body).

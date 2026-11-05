# The Wooden Boomerang

Owner, 2026-11-05: "make a boomerang weapon that functions exactly like the terraria boomerang but in 3d". `/equip boomerang`. Uncommon, as the rarity list in `GAME_DESIGN.md` has it.

## Terraria's rules, and ours

From the [Wooden Boomerang](https://terraria.wiki.gg/wiki/Wooden_Boomerang) and [Boomerangs](https://terraria.wiki.gg/wiki/Boomerangs) pages of the Terraria wiki. A Terraria player stands 3 tiles tall and ours 2.6 m, so a tile is about 0.87 m (`BoomerangRules`).

| Rule | Terraria | Here |
|---|---|---|
| Damage | 10, of a player's 100 | 10 of 100, plus Strength as a thrown bottle has it |
| Speed out | velocity 6.5 | 20 m/s, no gravity |
| How long out | 0.5 s, then it slows and turns back | the same |
| Range | about 13.6 tiles | 12 m |
| Speed back | up to 46 mph | up to 29 m/s, steering for wherever the thrower now is |
| Going out it meets something | turns straight back (not on the wiki: from memory of the game's code) | the same, a person or a wall |
| Coming back | pierces without limit, passes through blocks | the same; the same body again only after 0.33 s |
| Knockback | always away from the thrower, whichever way it flies | the hit's "from" is the thrower, so the stagger pushes that way |
| How many | "most boomerangs cannot be fired again before returning" | one out; clicks do nothing till it is caught |
| Use time | 20 ticks | a throw at most every 1/3 s |
| Lost | after a minute, or 187.5 tiles off | a minute, or 163 m |

3D's own additions, all mine: it is aimed at what the crosshair is on, since the camera looks past the shoulder. Going out, it cracks a window it hits, as a fist does, and turns back. Its disc is tipped, so its spin reads from behind, from the side and from above. It is 70 cm tip to tip, well over life size, so that it reads in the air at its range.

Terraria's melee speed bonus and its slowing in liquids are not in.

## The pieces (`Weapons/Thrown`)

| Piece | What |
|---|---|
| `BoomerangRules` | the numbers |
| `BoomerangFlight` | the path, pure |
| `ThrownBoomerang` | one in the air: the path, a sphere swept along it each frame for hits, the spin, the whirr |
| `BoomerangThrower` | on every body: whether it is out (it outlives a swap to another weapon), the throw, and word to other players |
| `BoomerangItem` | in the hand; hidden while it is out. A plain click throws it, as a grenade's does |
| `BoomerangBuilder` | the stand-in model, a carved V with red tips, drawn in code |
| `PlayerLocomotion.Throw.cs` | the arm's swing: code-posed over the Animator, a first go, not a Blender clip |

## Other players

`BoomerangThrower` is an `IReplicatedAbility`: the throw (their copy swings its arm and flies its own), what turned it back, and the catch. The copy hurts no one: the thrower's machine decides hits, and they reach other players' bodies as every blow does.

## Tests

`BoomerangFlightTests` (the path, pure), `BoomerangPlayTests` (on a real body: out and back; a hit going out turns it back and it can hit again coming home; a wall turns it back with nobody behind it hurt; home through a wall, hitting who it passes; pushed away from the thrower; one out at a time; a swap and back while it is out; another player's copy), `ItemCardTests`. Pictures: `BoomerangCapture` (`Logs/CityShots/boomerang-*.png`).

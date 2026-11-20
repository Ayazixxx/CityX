# The combat playtest bot (2026-11-18)

Owner: "can you play the game and fight swordsman and crazyguys using various weapons and make improvements".

`CombatPlaytest` (Explicit, PlayMode) plays the real game: the real scene, player, camera, HUD and keys (simulated keyboard and mouse),
against a swordsman and Crazy Guy with each weapon in turn. It plays as a fair player would: it walks up and swings in reach, guards
(55%) or dashes back (20%) when the enemy winds up close by, and with a gun, a staff or a boomerang keeps its distance, strafes and puts
the crosshair on the enemy's chest. It is healed back to full under 25 health and counts that as a life lost.

```
$env:CITYX_PLAYTEST = "fists,baseball_bat"   # only these loadouts (leave unset for all)
$env:CITYX_PLAYTEST_ROUNDS = "2"             # each fight this many times (one fight is one roll of the dice)
powershell -ExecutionPolicy Bypass -File tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter "CityX.Tests.CombatPlaytest" -TimeoutMinutes 60
```

It writes `CityX/Logs/Playtest/report.txt`: per fight, who won and how fast, damage and hits each way, lives lost, clicks, blocks,
parries, staggers, dashes, guards, seconds knocked down, far apart, close with nobody hitting, the enemy's attacks and any errors; then a
trace each second (positions, the enemy's state and timers, stun and shrug). With `-Graphics` it also saves a picture from the game's
camera every 3 s.

Lesson: the input test fixture takes one change per device a frame (two in one frame and the second overwrites the first). The bot
queues what it wants and sends one change a frame per device; before that a lost release left the right button down and the bot fought a
whole fist fight with its guard up.

## What the first runs found (2026-11-18, two rounds of every fight)

| Loadout | Seconds to win | Damage taken | Enemy swings |
|---|---|---|---|
| Fists | 10-45 | 11-63 | 5-23 |
| Combat sword | 5-10 | 0-35 | 2-5 |
| Kitchen knife | 13-18 | 36-57 | 7-9 |
| Bat, sledgehammer | 7-13 | 0-36 | 1-6 |
| Pistol, AK-47 | 0.4-6 | 0-18 | 0-3 |
| Fireball staff | 3-4 | 0 | 0-1 |
| Boomerang | 5-9 | 0 | 0-1 |

- Fixed: stun-lock (`StaggerChain`, docs/SOULSLIKE_COMBAT.md).
- Left to the owner (balance): a gun, the fireball staff and the boomerang beat a lone sword or knife enemy from range before he reaches
  you; every enemy dies in 4-12 blows.

## The new weapons (2026-11-19)

Owner: "do bug tests for all those new weapons we added. try fighting with them against swordsman and crazyguys. and test the riot shield
with pistol as well as blocking bullets with the combatsword vs shoota and check for bugs". The bot gained loadouts for the shotgun, the RPG,
the riot shield beside the pistol, the spear, the bo staff and the dual pistols (`Loadout.Extra`, `Loadout.AllSkills`: it learns every
skill first, so it runs last), and `GuardAgainstAShootaWithTheSwordAndTheRiotShield` (12 s against a Shoota 10 m off, five ways: the combat
sword's guard held, the riot shield raised, raised with a shot each second, the shield with hip fire, the pistol alone), into
`report-shoota.txt`. One round each, no errors from the game in any fight.

| Loadout | Swordsman | Crazy Guy | Damage taken |
|---|---|---|---|
| Shotgun | 0.2 s, one shot (108) | 1.9 s, two | 0-9 |
| RPG | 0.3 s, one rocket (175) | 0.3 s | 29-36: the bot fires at once at 8 m, inside the 7.5 m blast's edge, and is knocked down by its own rocket |
| Riot shield + pistol | 1.8 s, five shots | 2.3 s | 0 |
| Spear | 12 s, nine blows | 9.6 s | 18-36 |
| Bo staff | 11.6 s, nine blows | 11.1 s | 0-3; the enemy is down 5-8 s of each fight (the rising blow and the spin) |
| Dual pistols | 4.1 s | 2.3 s | 0 (the bot fires the right-hand gun only) |

Against the Shoota: the sword's guard held turned aside 2 of 30 bullets and let a quarter of the rest through (100 -> 26 health in 12 s): as
designed, a gamble (Block Chance 10%), the parry's instant being the sure one. The riot shield raised stopped all 27; with a shot back each
second the Shoota died after three shots at the player. With the crosshair on him the pistol kills a Shoota in five hits. (A first version
of the test aimed from the body, not the camera, and every shot passed 0.9 m to his right: the camera stands 0.87 m off the shoulder. Not
the game.)

## The rare guns raised (2026-11-19)

Owner: "the shotgun should do more damage close up the enemies are feeling like bullet sponges, 100 health is a low level enemy", "guns like
shotguns and ak47 are going to be hard to find they should kill low level enemies very quick". The shotgun's pellets 12 -> 20 (180 a whole
pattern), the AK-47's round 34 -> 50 (`FirearmProfile.ShotgunPelletDamage`, `AK47RoundDamage`). Three rounds each:

| Loadout | Swordsman | Crazy Guy |
|---|---|---|
| Shotgun | 0.2 s every time, one blast (120-180 landed) | 0.2 s every time, one blast (100-180) |
| AK-47 | 0.6-0.8 s, two rounds | 0.6-0.7 s, two rounds |

None of these enemies had his guard up. A swordsman holds it up close in about half his fights (`DuelistMind.GuardStanceChance` 0.45), and a
bullet through a held guard is a quarter of itself (the owner's rule of 2026-10-27, made for the player's sword): a whole blast into it is 45,
three blasts; an AK round 12.5, eight rounds.

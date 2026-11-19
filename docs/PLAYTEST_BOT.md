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

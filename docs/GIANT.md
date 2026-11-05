# The Giant

## 2026-10-29: built to the owner's ask

> "make an enemy that is a giant version of the playermodel like when you use the grow spell. take that size playermodel and make it a enemy"

`/spawn giant` puts one 34 m in front of you, facing you (inside the 40 m a session lets a player spawn at; `/spawn giant 3` spaces them out by half its size).

## What it is

- **The player's own body at the Grow spell's size** (`Giant.prefab`, built by `GiantBuilder` from `Player.prefab` through `EnemyBody`, as the
  swordsman and the Shoota now are): ten times over (`BodySize.GrowScale`), 26 m tall, in a weathered stone-grey skin so it is no player. The
  size is the prefab's root scale, so its capsule, ragdoll and colliders grow with it and its movement reads it as Grow's does
  (`PlayerLocomotion.BodyScale`: its strides cover ten times the ground, its jump goes ten times as high, its falls land as a giant's).
- **A giant's health**: 1200 (twelve players), from the Max Health stat (110 points over a body's 100). Killing it is worth **400 XP**.
- **A body as any body's**: it bleeds, ragdolls when thrown, dies limp and comes apart (`Dismemberer`); the corpse goes after 30 s.

## How it fights (`Weapons/NPC/GiantMind.cs`, pure)

| | What | When |
|---|---|---|
| 1 | **Approach**: strides straight at the nearest living player (`PlayerBodies.NearestAlive`, within 160 m), turning slowly (120 degrees a second); stands off at 6 m | it has a target and is not about to leap |
| 2 | **Crouch** (the tell, 1.1 s: long enough to see and run) | the player within 26 m (about its height), its leap ready and it on the ground |
| 3 | **Leap**: jumps, still walking at the player (60%) as it flies | the crouch is over |
| 4 | **Slam** where it comes down (`GiantSlam`): everyone within 18 m is hurt (40 at the middle, half at the rim) and thrown down and away; the camera of a player near it shakes (hardest at the middle, nothing past four times the slam's reach); a ring of dust races out over the ground, dust rolls up, a deep boom (the arcane strike's thump pitched far down) | it touches the ground again |
| 5 | **Stands** 1.4 s, then goes after the player again; 5 s from one slam to the next | after a slam |

- Never hurts itself, nor a packmate (`EnemyPack.AreAllies`: not thrown either).
- Thrown limp in the middle of its crouch or its leap, it stands its 1.4 s once back on its feet and crouches again before the next leap
  (`GiantMind.KnockedDown`): its slam never comes without the tell. Its mind used to stay in the leap while it lay there, and it
  slammed the moment it stood (2026-11-05).
- Its numbers are constants in `GiantRules`.

## Other players (docs/MULTIPLAYER_STATE.md)

`NetworkedGiant.prefab` (`NetworkedEnemyBuilder`), as the swordsman's: the machine that spawned it runs its brain; every other player's
machine shows a copy that walks, crouches and leaps as the owner's (its transform and animator). Its slam is announced by name
(`PlayerAnimationDriver.GroundSlamName`): every copy's ground heaves too, with the shake (`GiantSlam.Show`, no blows); the blows land on
the owner's machine and reach other players through their copies (`NetworkedDamage`), as every enemy's do.

## Tests

`GiantMindTests` (the phases, the tell's length, the cooldown, the slam's share), `GiantPlayTests` (the size and health, the stride, the leap
and the slam on a real player that spares the giant; knocked out of its leap, no slam until it has crouched again), `NetworkedPlayerStateSyncPlayTests.Enemies` (the copy is a giant and shows the slam);
pictures `GiantCapture` (`giant-*.png`).

## Not yet

- Its own look (a Meshy model when the owner has credits again): for now the stone-grey player.
- Grabbing, stomping a player under its foot, throwing cars.

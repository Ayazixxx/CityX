# The Shoota

## 2026-10-15: built to the owner's ask

> "create an npc called "shoota" and he is just a white playermodel and he wields the ak47 and make him a smart hostile npc so if you are
> nearby and in sight of him he starts shooting at you but also runs to cover if he is getting damaged and when he needs to reload and stuff
> like that."

`/spawn shoota` puts one 14 m in front of you, facing you.

## What he is

- **The player's own body** (`Shoota.prefab`, built by `ShootaBuilder` from `Player.prefab`): white skin, and without the parts only the
  player's body needs (the input reader, the saved character's look, the voice capture). His `ShootaBrain` decides each frame what a
  player would press and hands it to `PlayerLocomotion`, so he walks, runs, crouches and shoulders the rifle with the player's own animation
  and hands-on-the-gun posing.
- **The real AK-47** (`AK47.prefab`), mounted in his hands the way another player's copy holds one (`PistolWeapon.MountCosmetic`): the
  two-handed rifle hold and the AK's own reload motion.
- **The player's health and ragdoll** (`PlayerVitals`, `Ragdoll`): he can be hit by anything, knocked down (Self Smite's burst, finishers),
  and dies limp; the body goes after 25 s. Killing him is worth **60 XP** (`PlayerVitals.XpRewardOnDeath`; guns now pay kill XP too).

## How he fights (`Character/NPC/ShootaMind.cs`, pure)

| State | What he does | Leaves it when |
|---|---|---|
| Idle | stands | he sees you (45 m, 75 degrees either side of where he faces; all round once alerted) or is hit |
| Engage | faces you, rifle shouldered, fires in bursts of 3 to 7 rounds with 0.35-0.85 s pauses, after a 0.55 s beat to react | dry, hurt with cover near, or you out of sight 2.5 s |
| TakeCover | runs (sprints) for cover, round a corner if need be | there: reloads if short of rounds, else stays down 2 s and leans back out |
| Reload | changes the magazine (crouched in cover, or where he stands if there is none) | full again, and 2 s down in cover |
| Chase | walks to where you were last seen | sees you, or gives up after 15 s more |

- He will not run for cover again for 4 s after leaving it (no ducking at every hit); with no cover anywhere he fights on.
- **His aim**: hitscan from the muzzle like the player's; the spread grows with range, your speed and the length of the burst
  (`ShootaMind.SpreadDegrees`: about 1.7 degrees on a standing target at 15 m, up to 9). A round that meets a window cracks it; one
  that meets a body hurts it as a **bullet** (armour counts), at **35%** of the player's own AK (`DamageScale`: 12 a round), with blood.
  The tracer is drawn along the true path, with the flash, the crack, the brass and the bolt.
- **Never through a wall**: he only shoots what he can see.

## Cover (`Character/NPC/CoverPicker.cs`)

Rings of spots round him (2.5 to 20 m, 16 each): a spot is cover when the lines from your eye to a crouching body there (head and chest) both
hit something solid, it does not take him more than 1.5 m closer to you, and he can run to it in a straight line or by way of one turn
(round the corner of a building). Indoor spots (a ceiling over them) are not tried for a way round. Nearest wins, tucked in close behind what
hides it preferred. The search is spread over frames, 48 rays a frame (about 0.2 ms in the city; one search at once cost 4 ms), and the spot
is rechecked every half second against where you are now.

## Multiplayer

**Local for now**, as the Crimson Claw is: NPCs are not networked yet. A Shoota exists only on the machine that spawned it and only ever
targets that machine's own player, never another player's copy (who could not see who was shooting them).

## 2026-10-17: he had no animation

Found while building the swordsman: `Shoota.prefab` had no animator controller (the builder copies `Player.prefab` in the same run that rebuilds
the player rig, and the reference came out empty), so he stood in his bind pose with nothing playing. `ShootaBuilder` now sets the controller
by path; `EnemyPrefabAnimatorTests` guards it.

## Tests

`ShootaMindTests` (EditMode: the reaction beat and bursts, cover and reload, no ducking at every hit, chase and give up, spread, cover behind
a crate and round a block's corner), `ShootaPlayTests` (the real prefab: hits a player in sight, never through a wall, runs behind a crate
when hurt and reloads in cover). `ShootaCapture` (Explicit, `-Graphics`) renders him in the city.

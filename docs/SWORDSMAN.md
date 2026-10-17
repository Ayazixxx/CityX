# The Swordsman

## 2026-10-17: built to the owner's ask

> "make a swordsman npc that wields the one handed sword so i can duel with him. make him utilize dashes as well as blocking and make him take
> advantage of the stagger if i attack their block. implement a stagger system for if you swing at a block"
>
> "make the npc have all the movement animations that i do" / "i only want dashes"

`/spawn swordsman` puts one 8 m in front of you, facing you. Draw your sword (`/equip combatsword`) and fight him.

## What he is

- **The player's own body** (`Swordsman.prefab`, built by `SwordsmanBuilder` from `Player.prefab`): slate-blue skin, without the parts only
  the player's body needs (the input reader, the saved character's look, the voice capture). His `SwordsmanBrain` decides each frame what a
  player would press and hands it to the same `SoulsSwordCombat`, dash and `PlayerLocomotion` the player's keys reach.
- **The real combat sword**, equipped as the player equips it (`SwordWeapon.OnEquip`): the one-handed cuts, chain, heavy, guard and parry.
- **The player's movement animation**: the one-handed sword's own walk, run, side-steps and back-step (`Sword1HLocomotion`), the sprint, the
  dash, the guarded walk -- locked on to you (`LocomotionInput.LockedOn`), so he faces you and steps round you the way Tab makes the player.
- **The player's dash**: double-tap Shift with a sword out (`PlayerInputReader.TickDash`, which the sword combat calls its roll -- there is no
  rolling; it is the dash), with its stamina cost and its moment of invulnerability. He keeps a dash's worth of stamina in hand.
- **The player's health and ragdoll**: he can be hit by anything, knocked down, and dies limp; the body goes after 25 s. Beating him is worth
  **80 XP**.

## How he fights (`Character/DuelistMind.cs`, pure)

By priority, every frame:

| | What | When |
|---|---|---|
| 1 | **Punish**: straight in with cuts, dashing in if too far to walk in time | you are OPEN: your blade bounced off his guard, he parried you, or your guard broke (not a mere flinch from his own cut: pressing those was a stun-lock) |
| 2 | **Answer a blow**: after 0.14-0.28 s (a person's reaction) the guard (57%), a dash out of it (33%), else he swings first or takes it | you start a swing within 3.8 m |
| 3 | **Recover**: backs off to about 5.5 m and circles | his stamina under 25% (until it is back to 60%) |
| 4 | **Attack**: one or two cuts, or now and then the heavy (15%); then back out, with a dash 40% of the time | you in reach, not swinging, not dashing; 1-2.4 s between his attacks |
| 5 | **Approach** (running beyond 8 m, closing with a dash now and then from 5 m), then **circle** at about 3.4 m, switching direction every 1.2-2.8 s | otherwise |

- A raised guard he mostly does not swing into (his blade would bounce off it as yours does); only sometimes he drives a heavy into it.
- Hit, he dashes away 35% of the time.
- His numbers are constants at the top of `DuelistMind`; the sword's are the player's own (`/sword`).

## The stagger: the guard rebound

Swing into a held guard and the blade bounces off it: `SoulsSwordCombat.GuardReboundSeconds` (0.6 s) stunned, the sword arm thrown up and back
past the shoulder and the body leaning away (`SwordStrikes.Rebound1HName` / `Rebound2HName`, posed by `SwordPoseDriver.BeginRebound`), then
gathered back into the ready stance. A parry (the guard's first 0.2 s) does the same for longer (`BlockedRecoilSeconds`, 0.9 s). A blow the
guard cannot afford breaks the guard instead (the defender staggers, 1 s). The blades clash with sparks and a ting on every guarded or parried
blow (`PlayerVitals.GuardStruck` -> `BlockDeflectEffect`). It is the same for both sides: his blade bounces off your guard too, and that is your
opening. Details in docs/SOULSLIKE_COMBAT.md.

## Multiplayer

**He is local for now**, as the Shoota and the Crimson Claw are: NPCs are not networked yet (docs/GAME_DESIGN.md, "Enemy/NPC sync"). He exists
only on the machine that spawned him and only fights that machine's own player. What happens to a PLAYER in a fight -- the rebound, a hit
reaction, a parry -- is the player's own and reaches other players by name as everything else does (`sword1h_rebound` through
`NetworkedAnimatorSync`; a rebound decided on the defender's machine comes back as its verdict, `ApplyRemoteVerdict`).

## Found on the way

The Shoota's and the swordsman's prefabs had **no animator controller**: their builders copy `Player.prefab` in the same run that rebuilds the
player rig, and the controller reference came out empty, so the Shoota had been standing in his bind pose with nothing playing. Both builders
now set the controller by path (`EnemyPrefabAnimatorTests` guards it).

## Tests

`DuelistMindTests` (EditMode: comes in and runs when far, circles both ways, answers a blow only after a reaction with the guard and the dash in
the right shares, punishes at once and keeps cutting, dashes in on a far opening, attacks an open target, waits out a raised guard, recovers
stamina, presses nothing stunned, dashes away when hit, dashes back out after a chain, closes from far with a dash), `SwordsmanPlayTests` (the
real prefab against a player body that keeps swinging, 40 s: he blocks, the player's blade rebounds, he punishes it, he dashes, he hits back,
he faces the player and moves on the sword's walk, the rebound is announced by name; he can be beaten and pays XP), `EnemyPrefabAnimatorTests`.
`SwordsmanCapture` (Explicit, `-Graphics`) renders `duel-rebound.png`, `duel-dash.png` and `duel-circle.png` in `Logs/CityShots`.

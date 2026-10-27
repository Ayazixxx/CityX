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

## How he fights (`Character/NPC/DuelistMind.cs`, pure)

By priority, every frame:

| | What | When |
|---|---|---|
| 1 | **Punish**: straight in with cuts, dashing in if too far to walk in time | you are OPEN: your blade bounced off his guard, he parried you, or your guard broke (not a mere flinch from his own cut: pressing those was a stun-lock) |
| 2 | **Dash out of a slow blow**: after 0.3-0.45 s (a person's reaction; a light cut lands sooner) a dash out of it (35%) if it is still winding up, which only a heavy is by then. Never from behind his guard, and never the guard itself (2026-10-24, below) | you start a swing within 3.8 m |
| 3 | **Recover**: backs off to about 5.5 m and circles | his stamina under 25% (until it is back to 60%) |
| 4 | **Attack**: one or two cuts, or now and then the heavy (15%); then back out, with a dash 40% of the time | you in reach, not swinging, not dashing; 1-2.4 s between his attacks |
| 5 | **Approach** (running beyond 8 m, closing with a dash now and then from 5 m), then **circle** at about 3.4 m, switching direction every 1.2-2.8 s; 45% of those spells behind his raised guard (his guard stance) | otherwise |

- A raised guard he mostly does not swing into (his blade would bounce off it as yours does); only sometimes he drives a heavy into it.
- Hit, he dashes away 35% of the time.
- His numbers are constants at the top of `DuelistMind`; the sword's are the player's own (`/sword`).

## The stagger: the guard rebound

Swing into a held guard and the blade bounces off it: `SoulsSwordCombat.GuardReboundSeconds` (0.6 s) stunned, the sword arm thrown up and back
past the shoulder and the body leaning away (`SwordStrikes.Rebound1HName` / `Rebound2HName`, posed by `SwordPoseDriver.BeginRebound`), then
gathered back into the ready stance. A parry (the guard's first 0.2 s) does the same for longer (`BlockedRecoilSeconds`, 0.9 s). A blow the
guard cannot afford breaks the guard instead (the defender staggers, 1 s). The blades clash with sparks and one of the owner's three recorded clashes (picked at random) on every guarded or parried
blow (`PlayerVitals.GuardStruck` -> `BlockDeflectEffect`). It is the same for both sides: his blade bounces off your guard too, and that is your
opening. Details in docs/SOULSLIKE_COMBAT.md.

## 2026-10-24: tells, committed swings, a guard you can see

> "the dash feels pretty useless unless you are like backed into a corner or something, because the enemies just auto lock onto you anyways
> ... the attack is far to quick to use dash to get away from unless you prefire it and get lucky because the telegraph of the enemies
> attack is either very short or non existent, also the enemies get a lot of really bullshit blocks where the second you swing they just
> throw up guard and you get punished ... find another way" / "research games with similar combat im thinking dark souls"

**What Dark Souls does** (researched that day): an enemy's swing winds up and **holds at the top** before it comes (the tell; the fakeout
is a longer hold, never a missing one); it **tracks you while winding up and commits at the strike**, so a dash through the swing at the
right moment leaves it behind; shield enemies **hold their guard as a stance** you can see before you swing, and you break it with a heavy,
wait for it to drop, or go round it. An enemy that reads the button you just pressed and answers it at once ("input reading") is what
players call unfair. Sources: Wayline on telegraphs and fakeouts, Gamer Guides on Elden Ring's delayed wind-ups, Game Developer on
telegraphing attacks, Steam's Dark Souls threads on tracking and input reading, GamePro on Zullie the Witch's input-reading findings, the
Fextralife and Gamer Guides stamina and guard-break pages.

**What he does now:**

- **His tell.** A cut he opens with holds at the top of its wind-up for 0.4 s (a chained cut 0.2 s; the heavy 0.3 s on top of its own
  pause), the blade drawn back, and a **glint** flares on its tip with a soft ting (`AttackGlint`). A light cut now lands about 0.6 s after
  he starts it (0.26-0.34 s before). `SwordsmanBrain.TelegraphLightSeconds` and its two siblings, handed to his `SoulsSwordCombat`
  (`TelegraphLightSeconds`...): the player's own swings have none, so a duel between players is unchanged.
- **His swings commit.** Out of a swing he turns to face you at 540 degrees a second; winding up (the tell included) only 90; from the
  strike to the end of his recovery not at all (`SwordsmanBrain.Facing`, `SoulsSwordCombat.AimSwing`). Dash past him while the blade is
  held: the cut goes where he faced, and he is open while he recovers.
- **His guard is a stance.** Whether a spell of circling is spent behind his guard is decided as the spell begins (45%); it goes up once he
  is near and only while no blow of yours is on its way, and never comes down under one. He never throws it up against a swing already
  coming. You see it before you swing: break it with the heavy (hold left click: it breaks a held guard), wait for it to drop (he lowers it
  to attack, to dash, short of stamina, or when the spell ends), or go round it.
- **His dodge** is a person's: 0.3-0.45 s after your swing starts (a light cut has landed by then) he dashes out of it 35% of the time if it
  is still winding up, which only your heavy is.

**How to fight him:** watch for the glint and dash as it flashes, to the side or past him; or hit him while he circles with his guard
down; or hold left click for the heavy into a raised guard.

**The Crimson Claw too** (`CrimsonClaw.cs`): its claw draws back (0.18 s), then holds there glinting for 0.35 s (`TellSeconds`) before
the swipe; it turns toward you at 120 degrees a second while drawing back and holding, not at all while it swipes; and the swipe reaches
only the 70 degrees each side of where it faces (`ClawHitHalfAngleDegrees`; it hit all round before). It used to swipe 0.18 s after
starting, facing you every frame.

## What a hit does, both ways (2026-10-17)

A cut that lands -- his on you, yours on him -- holds both bodies still for a few frames, throws the one hit back, flashes it, bursts
blood out of the wound onto the street and the walls, and jolts your camera (docs/SOULSLIKE_COMBAT.md, "Hits that land"). A landed
light holds the other until the chain's next cut arrives, so whoever lands first gets the follow-up; the way out is a guard held through
the stun (the parry window catches the next cut) or a dash the moment it ends. He is stunned by the same rule, and dashes away 35% of the
time once he can. A killing cut flings the corpse.

## Beaten, he drops his sword (2026-10-17)

His combat sword falls from his hand, tumbles to the street and lies there for the player to pick up with E (docs/DROPPED_ITEMS.md). It is on
this machine alone, as he is.

## Multiplayer

**Shared in a session** (2026-10-27, docs/MULTIPLAYER_STATE.md "Enemies"): the machine that spawned him runs him; every other player sees
a copy that plays his swings, guard and tell (his glint is announced as `attack_glint`). He fights whichever player is nearest, another
player's copy too. Not yet: his reading of a REMOTE player's swing (a copy's sword combat is only posed, so he does not block or parry
what another machine's player throws at him as well as he does his own machine's). What happens to a PLAYER in a fight -- the rebound, a hit
reaction, a parry -- is the player's own and reaches other players by name as everything else does (`sword1h_rebound` through
`NetworkedAnimatorSync`; a rebound decided on the defender's machine comes back as its verdict, `ApplyRemoteVerdict`).

## Found on the way

The Shoota's and the swordsman's prefabs had **no animator controller**: their builders copy `Player.prefab` in the same run that rebuilds the
player rig, and the controller reference came out empty, so the Shoota had been standing in his bind pose with nothing playing. Both builders
now set the controller by path (`EnemyPrefabAnimatorTests` guards it).

## Tests

`DuelistMindTests` (EditMode: comes in and runs when far, circles both ways, never raises his guard against a blow already coming, holds
his guard through whole spells of circling, dashes out of a slow blow only after reacting and never out of a quick one, punishes at once and
keeps cutting, dashes in on a far opening, attacks an open target, waits out a raised guard, recovers stamina, presses nothing stunned,
dashes away when hit, dashes back out after a chain, closes from far with a dash), `SwordsmanPlayTests` (the real prefab against a player
body that keeps swinging, 40 s: he blocks, the player's blade rebounds, he punishes it, he dashes, he hits back, he faces the player and
moves on the sword's walk, the rebound is announced by name; he can be beaten and pays XP;
`HisCutHoldsItsTellAndDoesNotFollowADashRoundHim`: the tell is held with its glint, and a dash round him in the tell is not followed and not
hit), `EnemyPrefabAnimatorTests`. `SwordsmanCapture` (Explicit, `-Graphics`) renders `duel-rebound.png`, `duel-dash.png`,
`duel-circle.png` and `duel-tell.png` (his tell and glint, then the cut) in `Logs/CityShots`.

# Stats: the Stats page and what every stat does

## 2026-10-14: the owner's list, built

The owner: "I want to add all of these stats to the stats page ... and make them actually effect the character as the stat describes, atleast for
the game functions that exist right now"; then "might as well just add the stagger effect and stuff now too. if the stat describes something that
isnt in the game just add it"; Dodge Efficiency taken back out ("that doesnt work in our game"); Flight Speed and Vehicle Handling added, "dont
worry about implement the mechanics of those stats just put them on the list" (wings and the car rework come later).

**Points**: five stat points a level (Godot's `STAT_POINTS_PER_LEVEL`), a separate pool from the skill points. The Stats page (C, Player, Stats)
lists every stat under Vitals, Offense, Precision, Mobility and Defense, scrolling; "+" spends one, Shift and "+" five. What each point is worth
and every cap are mine, first values to tune (`Character/Stats/StatCatalog.cs`).

| Stat | A point | Cap | What it does in the game |
|---|---|---|---|
| Max Health | +10 health | - | the ceiling (the old Vitality) |
| Health Regen | +0.25 a second | - | **new**: health comes back on its own, 1 a second plus this, once 5 s have passed without a blow (`TickHealthRegen`) |
| Max Stamina | +10 stamina | - | the ceiling (the old Endurance) |
| Stamina Regen | +4% | - | the rate stamina comes back (the sword's own rate too) |
| Max Mana | +10 mana | - | the ceiling |
| Mana Regen | +5% | - | the rate mana comes back |
| Melee Damage | +2% | - | fists, blades, the bottle (and thrown), the sword's techniques (the old Strength) |
| Melee Speed | +1.5% | +60% | every swing runs faster -- wind-up, cut, recovery, the lunge and the pose, unarmed and sword -- and other players see the faster swing |
| Firearm Damage | +2% | - | guns and the flamethrower (they scaled with Strength, the melee stat, before) |
| Reload Speed | +2% | +75% | the reload motion, its sounds (pitched up with it) and its timer; other players see the faster reload |
| Fire Rate | +1.5% | +60% | the wait between shots |
| Spell Power | +2% | - | the fireball, ice shards, poison gas and acid rain (they scaled with Strength before); healing when spells heal |
| Cast Speed | +1.5% | +60% | the poison scream's wind-up and the ice volley; the fireball and acid rain go at once |
| Cooldown Reduction | +1% | 50% | every spell's and technique's cooldown (fireball, ice, poison, acid rain, sword wave, Octaslash, the sword spin) |
| Armor Penetration | +2% | 100% | ignores that share of the target's armour, magic resistance and block chance; travels with the blow to another player |
| Accuracy | -3% spread | 90% | narrows the gun's cone (below) |
| Recoil Control | -2% recoil | 80% | less camera climb, shake and gun kick, and the kick settles faster |
| Movement Speed | +1% | +40% | walking and running (capped: the city has to stream in ahead of you) |
| Flight Speed | +1% | +50% | **not in the game yet** (wings later; the jetpack keeps its own speeds for now) |
| Vehicle Handling | +2% | +100% | **not in the game yet** (the cars are to be redone) |
| Armor | +5 armour | - | **new**: stops rating/(rating + 100) of melee blows and bullets (100 armour: half), never all |
| Magic Resistance | +5 resistance | - | **new**: the same against spells, poison included |
| Stability | +3% | 75% | **new**: that chance to shrug off a blow that would stagger (no hitstun, nothing cut short); a deeper poise pool in a hyper-armoured swing; a cheaper guard to hold (harder to break) |
| Block Chance | +2% | 70% (80% in all) | **new**: while blocking, a chance to stop a blow outright -- no damage, no stamina, a bullet glances off with sparks and a ting. Everyone starts at 10% (the owner: "it starts at a lower chance and lets you upgrade it") |

## What was added for them

- **Damage kinds** (`Character/Combat/DamageContext.cs`): every blow says what it is -- Physical (fists, blades, thrown things, a claw, the sword's
  wave), Bullet (guns; armour stops it, a block can deflect it) or Magic (spells) -- and how much armour it ignores. `IDamageable` is unchanged:
  the attacker opens a scope around the hit and the target reads it, as poise damage was already said.
- **The stagger cuts things short** (`PlayerVitals.Staggered`): a blow that puts the body in hitstun stops the poison scream before the gas
  leaves, the rest of an ice volley, Octaslash's wind-up and a charging sword spin. A shrugged-off blow does not.
- **Bullet spread, CS:GO's way** (`PistolWeapon.SpreadDegrees`): a standing (or creeping) first shot is still exact on the crosshair -- the owner's
  CS:GO ask -- but running opens the cone up to 2.5 degrees, the air 4, and each shot of a held trigger after the first 0.4 more (up to 3). Aiming
  halves it; Accuracy takes its share off.
- **Health regeneration**: none existed (see Health Regen).
- **Deflection sparks and ting** (`Character/Combat/BlockDeflectEffect.cs`): drawn and voiced in code.

## Multiplayer

The stats are the owner's own; what they change travels as before. New on the wire: a hit relayed to another player carries its kind and the
attacker's penetration (that player's own armour answers it); a player's Max Health is shared with their health, so a copy's bar is right; a
deflection is announced by name (`block_deflect`) and the copy throws the same sparks; a faster swing or reload is sent with its speed and the
copy plays it at that pace. Tested over real sockets (`NetworkedPlayerStateSyncPlayTests`).

## Saving

`CharacterSave.Stats` keeps every stat by key. A save from before has only Vitality, Endurance and Strength, which become Max Health, Max
Stamina and Melee Damage (`CharacterStats.Load`); those three are still written for the character list (which now shows the three stats with
the most points) and older builds.

## Tests

`StatsTests` (the list and groups, a point's worth and the caps, the save and the old three, spending, health regen, stamina and mana regen,
armour and magic resistance and penetration, block chance, stability, the 2%-a-point agreement, the spread cone), `StatsPanelViewTests` (every
row, spending, Block Chance's whole chance), `StatEffectsPlayTests` (movement speed, melee speed, a stagger cutting a charging spin short and
Stability shrugging it off, a deflection's sparks here and from another player, spell power not melee damage on the fireball, cooldown
reduction), `GunFeelRealInputPlayTests.TheGunStatsMakeShotsHurtMoreComeFasterReloadSoonerAndKickLess` (firearm damage, fire rate, recoil
control, reload speed: 1.42 s to 0.95 s at +50%), `NetworkedPlayerStateSyncPlayTests.TheRealBodysStatsAnswerAHitThroughTheRelayAndItsDeflectionShowsOnTheCopy`.
`StatsPageCapture` (Explicit, -Graphics) renders `Logs/CityShots/stats-page-*.png`.

## Fixed on the way

The Stats page sat on its own hidden object, so the first time it opened it woke up and closed itself (and gave the controls back): it now
opens on the first click. The Loadout page has the same pattern and was left as it is.

## Not done

- Flight Speed and Vehicle Handling do nothing until wings and the new cars exist.
- Enemies have no armour, resistance or block chance of their own yet (Armor Penetration only matters against players).
- A stat respec; healing spells (Spell Power is ready for them).

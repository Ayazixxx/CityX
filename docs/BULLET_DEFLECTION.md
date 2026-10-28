# Bullets turned aside by the guard

Owner, 2026-10-27: "lets work on the blocking bullet system, I want it to work like in lego star wars when you hold block as a jedi it
reflects all bullets landed into another direction, in our game its a chance and its a stat you can upgrade ... (and then opposer can up
their bullet penetration stat ...)", and "it would feel really bullshit if your teammate or something reflected a bullet and it ricocheted
and it you ... maybe make it so it can fly in a random direction but not if its going to hit an ally?"

## The owner's decisions

- A bullet against a block rolls **Block Chance** (everyone 10%, upgraded with the stat; docs/STATS.md), less the shooter's **Armor
  Penetration** share. Both already existed: a won roll used to stop the bullet with sparks and a ting.
- A **failed roll on a held guard** lets a quarter of the bullet through and costs **no stamina** (a burst of fire must not leave a sword user
  unable to swing back). A melee blow on a held guard still costs stamina, as before.
- A turned-aside bullet **flies off in a random direction, never at an ally**: until parties and factions exist, it may hit enemies and
  whoever fired it, never another player.
- Open: whether the sword's guard can be held from the start or only after an early sword skill ("im not sure because I want you to stand a
  chance against gun users if you want to just main swords no shield. but that also kind of eliminates the need for a shield"). My
  suggestion: the sword's held guard is the gamble (Block Chance, the bullet flies off), a shield is the certainty (frontal bullets stop, no
  roll, nothing sent back). The owner asked for a basic round shield to test with: built, docs/SHIELD.md.

## What was built

| | |
|---|---|
| The roll | `PlayerVitals.ApplyHit`: a won roll raises `Deflected` (sparks, ting) and, for a bullet, `BulletTurnedAside` (where it came from, its damage). **A parry's instant** (the guard's first 0.2 s) turns a bullet aside for certain, no roll: the timing is the skill (my addition) |
| The ricochet | `Weapons/Firearms/BulletRicochet.cs` on every player body (`PlayerAbilityWiring`) and the swordsman: the bullet leaves the guard (62% of the body's height, 0.6 m toward the shooter) back the way it came, at random within 70 degrees either side and -10..+35 degrees of level (`Ricochet`), up to 50 m, and hits what it meets with the damage it carried (`DamageContext.Strike`, the defender as the striker) |
| Never at an ally | Each way is traced first (`Ricochet.TryPick`, `BulletRicochet.MayHit`): a way ending on this body, a player other than the one who fired it (a body within 1.5 m of where the bullet came from), or anyone who is no one's enemy (a civilian) is passed over, up to 10 tries; with no safe way the bullet only glances off. Enemies are marked `IHostile` (the Wraith, the Crimson Claw, the swordsman, the Shoota). An enemy's guard sends bullets at players instead |
| Other players | Decided on the machine whose body blocked (where the roll is made); a hit on another machine's body or enemy goes through its copy as every blow does. The streak is an `IReplicatedAbility` event (`bullet-ricochet`): every other player sees it (`NetworkedPlayerStateSyncPlayTests.ABulletAPlayersGuardTurnsAsideIsSeenFlyingOffOnTheirCopy`); the networked enemies now carry `NetworkedAbilitySync` for the swordsman's |

## Tests

`RicochetTests` (random within the cone, a refused way passed over, none safe: nowhere), `BulletRicochetPlayTests` (it hits an enemy with
the bullet's damage, never a bystander and only glances off when that is all there is, another player is never a target but the shooter is,
a failed roll on a held guard: a quarter and no stamina, a parry turns it aside for certain), the copy test above, `StatEffectsPlayTests`
(the deflection's sparks, unchanged).

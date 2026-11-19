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

## The guard knocked to a new angle by each bullet (2026-11-19)

Owner, 2026-11-19: "in lego star wars when holding block the lighsaber deflects bullets. i like the mechanic and animations it does when
it blocks the bullet. he change his angles sword when the bullet hits the blade and he stays in that new position until another bullet
get deflected by the blade then it moves to a new angle", and "this is for swords".

What the games do: in the LEGO Star Wars games a Jedi holding block (or attack) turns blaster bolts aside, and a block started as the bolt
arrives sends it back at whoever fired; in Star Wars Jedi: Fallen Order and Survivor a held block deflects bolts and a timed parry sends
them back. Neither's poses are written down anywhere found (fan wikis, guides, the GDC talk on Fallen Order's physical animation), so the
owner's description is the spec: the blade jumps to a new angle on each bolt and stays there.

| | |
|---|---|
| The angles | `SwordStrikes.Deflect.cs`: seven per hold, every one with the blade across the front of the head or the body, between it and the shooter (owner, on a first draft with the sword stood up by the right shoulder: "make it so all the blocking positions have either the sword blocking the head or body otherwise it will just feel like bullshit"). One hand: the guard's own (from the right of the chest up across the face), level across the face, from the right shoulder down across the chest, level across the hips, upright before the face, rising from the right hip across the chest, and hung point down before the body. Two hands: the guard's own (upright before the body), up and left across the chest and face, up and right across them, level across the chest, from the left of the belly down to the right, across the belly down to the left, and level across the chin. Two-handed grips stay near the middle and the chest's height (the long blade does the reaching): higher or out on the right, the left arm reaching across for the handle went through the face or the chest. Each angle turns the shoulders, the hips and the lean a little with it |
| Which angle | `SwordStrikes.PickDeflect`: never the one the blade is at; for a shot from more than 20 degrees above, one that covers the head (or the chest), from below one that covers the hips (or the chest); at random among those |
| The snap | `SwordPoseDriver.Deflect`: 0.1 s from wherever the blade is, there in a third of that, carried about a tenth past and settled back (`SwordStrikes.DeflectSnap`); then held until the next bullet. Letting go lowers the sword as before; the guard raised again starts at its own angle |
| What moves it | A bullet the held guard turns aside: the Block Chance roll won, or the parry's instant. A bullet that gets through the guard (a quarter of it), a blade meeting the guard (its knock-back plays on top, as before) and a shield's stop leave the angle alone |
| What is seen | The sparks and the ting on the blade where the bullet met it (they were in front of the chest), then the blade jumps (`BlockDeflectEffect.KnockTheGuard`). The jump is the body's whole reaction: the hit flinch (14 degrees back for 0.3 s) and, for a bullet in the parry's instant, the parry's thrust no longer play on top of it; both rocked the new angle about for a quarter of a second |
| Other players | The angle travels by name, `sword_deflect_<n>`, like the plain deflection: the copy's guard snaps to the same angle with the same sparks (`PlayerAnimationDriver.RemoteGuardDeflected`) |
| Who | Every weapon held with the sword's guard (swords, knives, the bat, the crowbar, the sledgehammer) and the swordsman enemy; not the spear, whose guard stays as it is |

My choices, for the owner to change: the seven angles and their shapes; the height rule (a pure random pick was the alternative; a first
draft also sent the blade to the side the shot came from, dropped with the angles that held it out to the side); 0.1 s for the snap with
a small overshoot; only bullets that are turned aside move the blade (so the jump is the sign that the roll was won); the sparks on the
blade where it was hit; no flinch or parry thrust on top of the jump; the angle back to the guard's own each time the guard is raised;
blunt weapons doing it too (one line to limit it to blades). The bullet still flies off from in front of the chest (`BulletRicochet`), not
from the blade.

Tests: `SwordDeflectTests` (the names, the pick's height rule, every angle used, at every angle at least 40 cm of the blade across the
front of the head or the body, every angle apart from the rest, in reach and off the ground, the snap's timing and overshoot, no garbage),
`SwordDeflectGuardPlayTests` (on the real body: each turned-aside bullet moves the blade within the snap and it holds, a parried one too;
a bullet that gets through leaves it; letting go and raising again; at every angle with six weapons the blade across the front of the
body, the arms out of the torso and the head and the blade out of both),
`NetworkedPlayerStateSyncPlayTests.ABulletKnockingAPlayersSwordGuardToANewAngleIsSeenOnTheirCopy`, and the pictures `SwordDeflectCapture`
(each angle from behind and the front, and a bullet's snap frame by frame).

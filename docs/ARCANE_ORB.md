# Arcane Orb

Owner, 2026-10-27: "lets make some arcane spells. we'll start with "arcane orb" its like fireball but its a magenta ball of aura and upon landing,
it functions like shadow flare from sephiroth's move set in smash bros, it leaves a cursed orb floating around you and that inflicts damage a few
seconds later, see how long it should take from researching the shadow flare ability. for the hotbar spell version, if you just unlocked arcane orb
... its just 1 orb that stays floating around and does damage but you can upgrade it and level it up to get stronger and eventually go up to 3 and
do more damage and stuff, (maybe more as a high level skill tree unlockable), but the staff version, how that one works is if you click it shots a
ball that does 1 cursed orb upon landing, but you can holding down with the staff to do a charged shot that goes up from 1 to 2 to 3 orbs depending
how long you charge ... and remember you dont get infinite shots with the staff it costs mana still, it just lets you do it more efficiently then
pressing a key with a cooldown." Asked mid-way: "i recall that shadow flare goes up to 3 orbs but does it go up to more?" (one shot places up to 3;
re-hitting the same target stacks up to 5).

## Shadow Flare, as researched (SmashWiki, Ultimate Frame Data)

| | Shadow Flare | Arcane Orb |
|---|---|---|
| Charge | uncharged 1 sphere; 17 frames held, 2; 26 to 34 frames, 3 | the Arcane Staff: 2 orbs at 0.28 s held, 3 at 0.43 s |
| After the release | the surge appears 8 frames later | the staff's ball leaves 0.13 s after the click is let go |
| The strike | the spheres strike after at least 4 seconds | 4 s |
| Re-hit | adds spheres, up to 5 on one opponent; pushes the strike back 0.5 to 3 s by charge, never past 7.5 s from the first hit | the same |
| While striking | nothing more can be added | the same |
| Damage | the surge 1.5% a sphere it carries, 6.5% each explosion | the ball 3 an orb, each orb 15 (before Spell Power) |
| Quickest use | 38 frames uncharged | the staff shoots again 0.63 s after a release |

Not taken: Shadow Flare's limit of 5 spheres per Sephiroth across all opponents (it would fight a later "more orbs" unlock), the spheres hurting
bystanders they pass, and the reflected surge doubling them.

## How it works (`Weapons/Spells/`)

| | |
|---|---|
| The ball | `ArcaneOrbProjectile`: a ball of magenta aura drawn in code (soft glows turned to the camera, a trail, sparks, a magenta light), 20 m/s, gone after 2.5 s (50 m: inside the 60 m round a caster that other machines believe news from). It grows a fifth for each orb it carries. On someone: its small hit and their curse; on anything else: a burst only |
| The curse | `ArcaneCurse` (the numbers, a plain class) and `CursedOrbs` (what is seen): dark orbs in a magenta glow fly out from the landing to circle the target's chest, bobbing and trailing light, at 38% of their height out. For the last 0.6 s they spin 2.5 times faster, draw in and pulse: a fair warning (a guard or a dash answers them). Then they dive in 0.12 s apart, each a blow from where it was, with a burst. If the target dies first they fade away |
| From the bar | `ArcaneOrbCaster.TryCast`: 20 mana and a 5 s cooldown. Its rank's orbs: 1 at rank 1, 2 at rank 2, 3 from rank 3 on; every rank past the first adds a tenth to its damage (both the ball's and the orbs') |
| From the staff | the Arcane Staff (docs/STAFFS.md): a click places 1 orb, a hold charges to 2 and 3 (little orbs circle the staff's head, one for each, with a rising chime). 8 mana an orb, no cooldown; the charge stops growing at what the mana pays for. The staff ignores the spell's rank (the spell need not be learnt) |
| Ranks | the skill map's first ranked skill (docs/SKILLS.md: ranks): learning it is rank 1, each rank after 1 skill point, without end |
| Numbers | `ArcaneOrbRules`. The Shadow Flare timings are research; the damage, mana and pace are mine |

## Other players (`IReplicatedAbility`)

The caster's machine decides every blow. Other machines are told: the ball leaving (`BallFired`: where, which way, how many orbs, from the bar or a
staff), the curse after each hit (`OrbsPlaced`: where the target stood, how many orbs in all, how long they have left; each machine shows them on
its own copy of whoever stood there, within 2.5 m), and a staff's charge starting and stopping. A copy's orbs are cosmetic: they look, sound and
strike the same and hurt nothing; the real blows reach a player through their copy as every hit does (`PlayerVitals.Relay`).

## Not yet

- More than three orbs a shot ("maybe more as a high level skill tree unlockable"): an empty Arcanist star waits for it.
- What the cost of a rank should grow to (GAME_DESIGN.md, Infinite upgrades): one point each for now.
- Its icon: the letter A, until the owner draws one.

## Tests

`ArcaneOrbTests` (EditMode: the charge's orbs, ranks, the curse's clock and caps), `ArcaneOrbPlayTests` (a hit curses the target and the orbs strike
four seconds later for their damage; ranks give more orbs; re-hits stack to five and push the strike back; a wall gets none; mana and cooldown; a
copy's orbs hurt nothing), `StaffPlayTests` (the staffs), `SkillRankTests` (ranks). `ArcaneOrbCapture` (Explicit, -Graphics) renders
`Logs/CityShots/arcane-*.png`.

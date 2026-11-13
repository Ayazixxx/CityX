# Frost: the Frost Staff and freezing

Owner, 2026-10-29: "make a frost staff, it basically works like mei's freeze gun in overwatch, you hold click to spray a cloud of frost and it
does a low amount of consecutive damage and slows them down then eventually freezes them. later i will have it so players have to spam space
bar or something to get out but just make it something i can test with enemies".

## What was looked at

Mei's Endothermic Blaster (Overwatch; the numbers move between patches): the primary fire is a short-range stream of frost, about 10 m; it
slows whoever it is held on, more the longer it is held (up to 50%, cut to 40% in June 2023), and held on long enough (about 1.5 to 2 s in
Overwatch 1) it freezes them solid for a moment; 55 then 70 damage a second (2023). Sources: Blizzard's hero page and patch notes (June 2023),
the Overwatch wiki, game guides.

## The rules (`FrostRules`, pure)

| | |
|---|---|
| The spray | held, a cone of frost from the staff's head along the crosshair: 10 m, 12 degrees either side (and a little more up close, for a body's width) |
| Damage | 3 magic damage every 0.25 s, 12 a second before Spell Power ("a low amount"), with no blood |
| Chill | each tick on a body makes it an eighth colder (a full chill in 2 s held on it); out of the spray it wears off, all of it in 2 s |
| Slow | the colder, the slower: down to half its pace with a full chill |
| Freeze | a full chill freezes the body for 1.5 s; it then thaws, its chill gone, and cannot freeze again for 1.5 s |
| Mana | 7 a second while held |

## Freezing a body (`Chill`, on every body: Player.prefab and so the enemies, the civilians)

Every frost blow that lands adds its chill (`DamageContext.Chilling`, a scope like the blade's edge; read where the blow is taken, on the
body's own machine: a blow on another player's copy carries its chill to the owner with the rest of it, `NetworkedDamage`, in eighths of a
full chill since 2026-11-12: the spray's tick is one, Glacier Spikes' blow four, docs/GLACIER_SPIKES.md). Chilled, the
body moves at `SpeedShare` of its pace (`PlayerLocomotion.SlowFactor`). Frozen: its animation stops where it is, it cannot move, an enemy's mind
(whatever makes it an enemy, `IHostile`: the swordsman's and the Shoota's brains, the Crimson Claw, the Wraith) stops, and the body itself
takes a light blue tinge (`FrozenLook`: a see-through layer of pale ice blue, 35%, drawn over every part on its own surface, one more
material on each renderer, in 0.2 s and out again in 0.35 s on the thaw; a layer because a painted body's colour is its texture, and black
stays black under any colour it is multiplied by). Other players are told it froze (and for how long) and thawed (`IReplicatedAbility`), so their
copy shows it.

The first look was a see-through ice block round the body; the owner (2026-10-29): "instead of it being frozen in like a cylinder, make it
so its just his body freezes in the position its in and has a slightly light blue tinge to it".

For now a frozen player thaws by themselves after 1.5 s, as an enemy does; breaking out by mashing a key is for later (the owner's).

## The look (`FrostSpray`)

Drawn in code: white-blue puffs of frost thrown along the cone at 14 m/s, widening and slowing as they go, gone by the end of its reach, and
ice crystals glittering through them; the water staff's hiss, played higher. The model (`StaffBuilder.BuildFrost`): dark blue wood, white
metal, a pale cyan orb. Other players see the spray on the wearer's copy of the staff, pointed where they point it (ten times a second).

## Tests

`FrostRulesTests` (the cone; colder is slower; two seconds to freeze; a little damage), `FrostStaffPlayTests` (a swordsman held in the spray
is chilled and slowed after a second, frozen after two with his mind and animation stopped, thaws, and cannot freeze again straight away;
one beside the cone untouched; a little damage; the spray stopping when let go; a copy showing the spray and a freeze it is told of).
Pictures: `FrostCapture` (`Logs/CityShots/frost-*.png`).

## Not yet

Breaking out of the ice by mashing a key (the owner's, for players); shattering a frozen body with a heavy blow; frost on the ground and on
walls; frozen water.

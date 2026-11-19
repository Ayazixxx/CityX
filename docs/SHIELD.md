# The round shield

Owner, 2026-10-27: "why dont you add a basic round, 1 handed shield il do a better model later just do something that functions for testing
now". Earlier the same day, on bullets and the guard: "the blocking with the sword is meant to be something you have to time, the shields im
going to add later are the ones you can hold down".

## The idea (my suggestion, the owner asked for a shield to test it with)

The sword's held guard is the **gamble**: a bullet rolls Block Chance and, won, flies off somewhere (docs/BULLET_DEFLECTION.md). A raised
shield is the **certainty**: what comes at its front stops, no roll, nothing sent back. Still open with the owner: whether the sword's guard
can be held from the start or only after an early sword skill.

## What was built

| | |
|---|---|
| The item | `round_shield` ("Round Shield", its own `shield` slot beside the two weapon slots, so a sword, a bat or fists stay in the right hand). `/equip shield` (or `/spawn shield` to drop one to pick up), `/unequip shield`. The inventory and the loadout show a SHIELD slot |
| The model | `RoundShieldBuilder`, drawn in code: a wooden disc ~1 m across (a real 75 cm one on this 2.6 m body), its face a shallow dome, an iron band round the rim and an iron boss. 800 triangles, two plain materials. A placeholder for the owner's own model |
| How it is held | By a centre grip in the left hand (`RoundShield.StrapOn` parents it to the hand bone, so the fingers close on it; the wrist held straight so the fist lines up with the forearm). Lowered, it hangs at the side, face out, swinging with the arm. Raised, the left arm brings it up before the chest (`PlayerLocomotion.RaisedHand`: two-bone IK, elbow out to the side) and it faces straight ahead, the fist hidden behind the boss -- in 0.12 s. `PlayerLocomotion.Shield.cs` places it every frame after the other arm poses |
| Raising it | Right-click held, when the right hand has no gun or spray can (those aim with right-click). It replaces the sword's and the fists' own block while it is in hand. The body walks at half speed with it up |
| What it stops | Anything coming from within 75 degrees either side of straight ahead (`PlayerVitals.ShieldCovers`), while raised. A **bullet** stops dead: no damage, no roll, nothing flies off, just sparks and a ting. A **blow** (a sword, a fist, a spell) is a held guard's: a quarter through, stamina spent, the Block Chance roll may still turn it fully aside, a guard-breaking heavy still breaks it. From the side or behind, or lowered, it stops nothing |
| Other players | Seen on their copies in the left hand (`ICosmeticMountable`), raised and lowered with its owner (the `ShieldRaised` animator parameter, synced by `NetworkedAnimatorSync`). The hit rules run where the hit is decided, from the same synced state |

## Not yet

No parry with the shield, no shield bash, no shield-specific stat, no durability. Nothing stops a shield user from swinging with it raised.

## Tests

`ShieldPlayTests` (in the left hand, the fingers closed on it, raised it comes up before the chest facing forward; half speed raised; a bullet
from the front stops dead with a roll that would have failed and nothing ricochets, one from behind hurts, lowered it stops nothing; a blow
from the front is a held guard's), `NetworkedPlayerStateSyncPlayTests.AShieldShowsOnAnotherPlayersCopyAndIsRaisedWithItsOwners`,
`EquipSlotTests` (the slot is in both menus). Pictures: `ShieldCapture` (Explicit, `-Graphics`) writes `Logs/CityShots/shield-*.png`,
lowered and raised, bare-handed and with the combat sword.

# The riot shield (2026-11-18)

Owner: "make a riot shield make it so you can equip 1 handed weapons with it. so you can equip shield and pistol or shield and 1 handed melee",
wands too but not staffs ("I think wands only"). On bullets: "there is no aim in with the pistol with riot shield in other hand, instead when
you hold right click, he brings the shield forward and blocks with it and if you want to shoot you have to left click with no aim in and you
have to move the riot shield from its guarded state if you want to go from guard to shoot"; as Call of Duty, "your feet are visible if you are
not crouching"; and "there should be no guard meter on the riot shield it should last forever like call of duty". On its size: "look at the og
mw2 riot shields".

`riot_shield` ("Riot Shield", the shield slot, Uncommon by my placement). `/equip riotshield`.

| | |
|---|---|
| The rules | `ShieldTraits.Riot` (`Character/Combat/ShieldTraits.cs`); the round shield's are `ShieldTraits.Round`, unchanged. A shield tells the body its kind as it is taken up (`HeldShield`, the base of `RoundShield` and `RiotShield`) |
| Beside what | `ShieldTraits.GoesBeside(HeldKind)`: bare hands, a one-handed melee weapon (the combat sword, a knife, a bat...), the pistol, a wand. Not a rifle, a bow, a staff, a two-handed sword or hammer, the flamethrower, the grapple or the Giant Boomerang: with those in hand it cannot be raised and right-click is the weapon's own again (it still hangs in the left hand: see Not yet) |
| Raising it | Right-click held. With the pistol nothing aims in: the shot is from the hip. Up in 0.12 s, as the round one |
| Attacking | A click (held or pressed) takes it out of its guard for `OpenToAttackSeconds` (0.5 s, mine) after the click ends, even with right-click held: it cannot cover and attack at once |
| What it stops | As the round shield (75 degrees either side of straight ahead; a bullet stops dead, a blow is a held guard's), but a hit that lands within `UncoveredBelow` (0.55 m) of the ground on a standing body is not covered: the feet and shins show. Crouched behind it, everything from the front is. Where a blow does not say where it lands, it is covered. No meter, no durability |
| Moving | Not slowed with it up (`MoveSpeedShare` 1; owner, 2026-11-06: "no annoying penalties"). The round shield still halves the walk |
| The model | `RiotShieldBuilder`, drawn in code, a stand-in: a dark slab 0.95 m by 1.95 m bowed round the bearer, a paler window before the face (opaque for now), a grip bar on the back. 204 triangles |
| How it is held | Carried upright at the left side with the arm bent (`CarriedBent`, `CarriedHand`: hung from a straight arm it would drag in the ground); raised, before the body from the shins to over the head |
| Other players | As the round shield: seen in the left hand, raised with its owner (`ShieldRaised`); hits are decided on the bearer's machine, which knows whether it crouches |

For later (owner, 2026-11-18: "maybe there are skills where you can increase your bullet penetration ... maybe stronger guns pierce through, we
will figure out later"): the bullet's penetration is already at hand where the shield stops it (`PlayerVitals.ApplyHit`, `DamageContext.Penetration`).

## Not yet

- With a two-handed weapon in hand it still hangs in the left hand; it should go on the back (`BackCarry`).
- The window is not see-through. No bash. Explosions ignore every shield, as before.
- Enemies do not use it.

Tests: `ShieldTraitsTests`, `RiotShieldPlayTests` (the pose; a bullet at the chest, at the feet standing and crouched, from behind; not slowed; from
the real mouse in the real scene: beside the pistol right-click raises it and nothing aims, a shot opens it for a moment; not raised beside the
AK-47, a staff or the two-handed sword, raised beside the wand and the combat sword),
`NetworkedPlayerStateSyncPlayTests.AShieldShowsOnAnotherPlayersCopyAndIsRaisedWithItsOwners` (both shields). Pictures:
`ShieldCapture.RenderTheRiotShield` (`riotshield-*.png`).

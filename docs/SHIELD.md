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

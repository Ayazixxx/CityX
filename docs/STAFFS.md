# Magic staffs

The owner's idea (GAME_DESIGN.md, Staffs, magic weapons and enchants): a hotbar spell is one cast; a staff's attack is the spell on the click,
every shot paying mana. Built 2026-10-27, three of them, as the owner asked one after another:

- **Arcane Staff**: "if you click it shots a ball that does 1 cursed orb upon landing, but you can holding down with the staff to do a charged shot
  that goes up from 1 to 2 to 3 orbs ... remember you dont get infinite shots with the staff it costs mana still" (docs/ARCANE_ORB.md).
- **Fireball Staff**: "when you hold click with that one it does a charged shot the explodes into a bigger ball of fire the longer you charge it,
  come up with a reasonable maximum the how long you can charge it for."
- **Ice Shard Staff**: "when you click it shoots 1 iceshard but when you hold click it just shoots consecutively like an automatic gun", and
  "dont make it too OP".
- The models: "just have it be a basic model for now il get to it when i can make more meshy models."

## What every staff shares

| | |
|---|---|
| The item | `StaffItem` (Weapons/Spells), a primary-slot weapon: `/equip staff`, `/equip firestaff`, `/equip icestaff` |
| The hold | in the right hand, its shaft through the fist past the thumb (where a one-handed sword's handle runs), the fingers closed on it. Carried upright at the side, the forearm forward; while it charges or shoots, the arm thrusts it out before the chest, its head leaning 25 degrees forward, and the body turns to the crosshair as with a gun (`PlayerLocomotion.Staff.cs`, two-bone IK and the hand turned to stand the shaft) |
| The click | left-click is the staff's (right-click stays the shield's). What a click and a hold do is the spell's, on the body (`IStaffCaster`: `ArcaneOrbCaster`, `FireballCaster`, `IceShardCaster`), so every shot reaches other players as that spell's does. A charge (`StaffCharge`) starts on the press, grows while held, and the shot leaves 8 frames (0.13 s) after the release, as Shadow Flare's does, the staff up by then |
| The charge's look | a glow at the staff's head swelling with the charge, and a light (`StaffChargeGlow`), on every player's screen |
| Mana | every shot pays; a charge stops growing at what the mana pays for, and one cannot start without enough for the smallest shot. Tab lock-on is off with a staff, as with a gun |
| The models | `StaffBuilder`, drawn in code (turned shapes): a 2.4 m shaft with a ferrule, a leather wrap at the grip, a cup at the head holding the crystal, glowing in the spell's colour. Arcane: dark purple wood, brass, a magenta orb. Fireball: dark red wood, an iron brazier, an orange ember. Ice Shard: pale wood, silver, a faceted shard of ice |

## Each staff

| | Click | Hold | Mana | Pace |
|---|---|---|---|---|
| Arcane | a ball that leaves 1 cursed orb | 2 orbs from 0.28 s, 3 from 0.43 s (Shadow Flare's frames) | 8 an orb | 0.63 s between shots |
| Fireball | a plain fireball (25) | grows for up to 1.5 s (about a Mega Man charged shot): up to 2.5 times the ball and its damage (62), its burst burning everyone within up to 4 m (all of the hit's damage at the middle, 30% at the edge) | 10, up to 30 full | 0.5 s (the bar's fireball waits 1.5 s) |
| Ice Shard | one shard | one every 0.25 s for as long as it is held, wandering up to 1.5 degrees | 4 a shard | about 32 damage a second, eight seconds from a full bar: well under a gun's |

All the numbers but Shadow Flare's frames are mine. The staffs ignore the hotbar spells' ranks (the spell need not be learnt).

## Other players

Each staff's model is held by a copy (`ICosmeticMountable`, via `NetworkedEquipmentSync`). Its shots, charge and thrust are its spell's
events (`IReplicatedAbility`): `ArcaneOrbCaster` (`BallFired` with B 1, `ChargeStarted`, `ChargeStopped`), `FireballCaster` (kind 0 with its size and
N 1, `StaffChargeStarted`, `StaffChargeStopped`), `IceShardCaster` (`StaffShardFired`). A copy holds the staff out while it shows them.

## Not yet

- The owner's own models (Meshy, after 2026-10-31).
- Staffs boosting their element's spells, general staffs and wands (GAME_DESIGN.md: the owner's ideas, not built).
- A staff's melee (a bonk) or right-click.

## Tests

`StaffTests` (EditMode: the charge's timing; the fireball's charge, mana and size), `StaffPlayTests` (each staff equipped in the right hand and seen
on a copy; the arcane click 1 orb and the hold 2 and 3; the fireball's charge grows the ball and its blast; the ice staff's hold keeps shooting at its
pace; mana paid and refused), the `EquipmentReplicationGuardTests`. `StaffCapture` (Explicit, -Graphics) renders `Logs/CityShots/staff-*.png`.

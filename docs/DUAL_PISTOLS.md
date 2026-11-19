# Dual pistols (2026-11-19)

Owner: "make dual wield pistols a thing. it will be a skill you have to unlock on the tree". Asked how it should play, the owner chose
**"Each click fires a gun"** (left-click the right hand's gun, right-click the left hand's, hold both to fire both, no aiming down the
sights), **"Two pistols in inventory"**, and for the tree: "Call the guns skill tree "Gunslinger" for now but i think we can come up with a
better name. i was thinking Marksman but that feels more focused on sniping or something". Then: "make it so you can equip two pistols in the
primary or secondary slot once dual wield has been unlocked ... so technically you could carry 4 pistols and use all of them if you dual
wield in your primary slot and secondary slot".

## The skill

**Dual Pistols**, the root of a new tree, **Gunslinger** (the owner's name for now), next to Sword on the skill map. One point. A new kind of
star, `SkillKind.Passive`: nothing to press, never on the bar. The three empty stars above it are my layout. A body with no character (the
editor's play button, the tests) does not have it: learn it on the tree, or `/skills all`.

## Carrying them

- With the skill, each weapon slot has a second, left-hand place that takes a pistol beside a pistol (`EquipSlots.PrimaryOffHand`,
  `SecondaryOffHand`; the rule is `PistolPairs.SlotFor`). A pistol being equipped goes, in order: to an empty primary; beside the primary's
  pistol; to an empty secondary; beside the secondary's pistol; else in place of the weapon in hand (the old rule). Four pistols: a pair in
  each slot. Anything but a pistol is never paired, and a pistol never goes beside anything but a pistol.
- **Scrolling** puts one pair away and draws the other, both guns together.
- **Taking a pair apart**: a menu click on the slot gives back the left-hand pistol first. Putting the right-hand one away (`/unequip
  hand_r`) hands its place to the left-hand one, which moves into the right hand. A weapon that cannot be paired taking the slot sends the
  left-hand pistol to storage.
- **A shield** goes on the left arm: putting one on sends every left-hand pistol to storage, and nothing pairs while it is worn (my rule: the
  left hand holds one or the other).
- **Forgetting the skill** (a respec) sends the left-hand pistols to storage.
- **Dying** drops both guns in hand.
- The menus show a pair as "Pistol + Pistol". Saves still keep only the primary's weapon (as before: the rest of the inventory is not saved
  yet), so a pair comes back as one pistol.
- Picking up a pistol with E follows the old rule (into empty hands, else into storage); from storage, a click equips it beside its pair.

## Firing

| | |
|---|---|
| Left-click | Fires the right hand's gun (once a click: the Glock is semi-automatic) |
| Right-click | Fires the left hand's gun. Nothing aims down the sights with a pair |
| Both held | Both fire |
| R | Reloads the pair (either gun's dry click starts it too) |

Every shot still lands where the crosshair is (the hitscan runs from the camera), each from its own magazine, each with its own kick, flash,
brass and tracer from its own muzzle. A shot from either gun raises **both** arms for the half second a pistol's shot always raised the one:
the chest square on, each gun out before its own shoulder (`DualPistolHold.RightGunInChest`, mirrored), both barrels on the crosshair's point.
Carried, each hangs in its hand where the walk puts it. The ammo counter shows both, the left hand's on the left.

Damage, fire rate and recoil are each gun's own; two guns simply fire twice as often when both are used. Balance by play is open.

## How the left hand holds its gun

As the right hand holds its own, seen in a mirror (`DualPistolHold.Mirror`): the two hand bones are mirror images (+x runs to the fingertips
of the right hand and back up the arm of the left, +y the back of either hand, +z the thumb's side), so the hold's position is flipped in x and
its turn reflected across x. The gun itself is the same gun (its port still on its right). The fingers grip it as the right hand's grip its
own, the trigger finger through the guard. Neither hand reaches for the other gun (the support hand is off while a pair is held).

## The reload

There is no free hand, so each gun is reloaded against the body, the one-handed way: both magazines drop together (muzzles up, the thumbs on
the releases), both guns go down to the hips, where a fresh magazine stands in a pouch on each side of the belt, and each well is pushed
straight down onto its magazine (the seat: the ammo is in). On an empty pair each slide is then hooked on the front of the belt and the gun
pushed down, which racks it. Then both come back up into the stance. Made in Blender like the other reloads:
`CityX/SourceArt/Glock/glock_pair_reload.py` (the keys) through `glock_pair_build_all.py` (which builds a copy of the Glock for the left
hand, its parts named SlideL, MagazineL, TriggerL), into `Resources/Glock/GlockPairReload.json`; 1.5 s, empty 1.87 s. `firearm_pose.py` and
`firearm_export.py` learnt a second gun for it (`gun_l`, `props_l`); the single guns' reloads are untouched.

In the game the right hand's gun leads (`PistolWeapon.TryReload`): it plays the pair's motion (`FirearmProfile.PairMotionResource`) and takes
its partner along (`FollowReload`); each gun drops its own magazine and is filled from its own reserve at the seat. A shot once the
magazines are in cuts the rest short, as with one gun.

## Other players

A left-hand pistol is announced to the server marked (`Inventory.PairMark`, "pair:pistol"), so another player's copy mounts it in the left
hand (`IOffHandItem.MountCosmeticOffHand`); the two copies find each other on the body and lend it the pair's reload. A shot from the left gun
is announced under its own name (`GunReload.FireLeftName`), so the copy's left gun flashes, cracks and throws its brass, not the right one.

## The pieces

| Piece | What |
|---|---|
| `SkillCatalog.DualPistols`, the `gunslinger` tree | the skill |
| `PistolPairs` | where a weapon goes, pure |
| `Inventory` (the pair's half) | the left-hand places, drawing and putting away a pair, taking one apart |
| `IOffHandItem` | an item that can go in the left hand |
| `PistolWeapon.Pair.cs` | the gun in the left hand, finding its partner, following the reload |
| `DualPistolHold` | the mirrored hold, the stance |
| `PlayerLocomotion.PistolPair.cs` | both arms raised onto their guns |
| `PlayerInputReader` (the pair branch of `TickCombat`) | the clicks |
| `GunFeel.AttachLeft` | the left gun's kick and hit ticks |

## Not done

- Only the Glock pairs (`ItemRegistry.IsHandgun`); other pistols join when they exist.
- The rest of the Gunslinger tree, a better name for it, and balance by play.

## Tests

`DualPistolsTests` (the skill, where a pistol goes, the mirror, the counter), `DualPistolsInventoryTests` (four pistols, scrolling pairs,
taking a pair apart, the shield, the respec, what a copy is told, dying), `DualPistolsPlayTests` (on the real body: held mirrored; a shot
from either raises both arms with both barrels on the crosshair's point; R reloads both from their own reserves with the pair's motion;
another player's copy holds the pair and flashes the gun that fired; in the game, left-click fires the right hand's gun and right-click the
left's, R reloads both). Pictures: `FirearmCapture.RenderTheDualPistols` (Explicit, -Graphics): `Logs/CityShots/firearm-dual-*.png`.

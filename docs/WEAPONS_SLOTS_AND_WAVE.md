# Primary/secondary weapons and the sword wave (2026-10-04)

**Primary / secondary** (`Character/Player/Inventory.cs`): the old right/left hand slots (`hand_r`, `hand_l`) are now Primary and Secondary. The first weapon equipped is
primary, the next secondary (holstered), a third replaces the one in hand. Only the weapon in hand exists as an object; the other is remembered (its ammo/durability)
and rebuilt on draw through the normal OnEquip/OnUnequip. **Scroll wheel** swaps (`PlayerInputReader.TickCombat`, 0.25 s cooldown; not while carrying or aiming).
The inventory panel shows PRIMARY / SECONDARY and a `>` on the one in hand.
A gun's rounds are part of what is remembered since 2026-11-05 (`ItemInstanceData.HasAmmo`, `Magazine`, `Reserve`; `PistolWeapon.GetInstanceData`
and `OnEquip`): before, only a bottle's cracks were, and a gun drawn again came with a full magazine and reserve, so scrolling away and back
was a free, instant reload. A dropped gun carries its rounds too, between machines as well (`ItemDropMessage`). A gun nobody has held yet
comes loaded. Not saved with the character: a new session starts with full guns. `GunAmmoKeptPlayTests`, `DroppedItemsRulesTests`.

**Picked up straight into the hand** (2026-10-21, owner: "if you dont have any item in hand and you press E on weapon then it equips it automatically, starting
with primary slot and if thats full then it goes in secondary"). Every pickup of a hand item goes through `Inventory.PickUp`: this covers a weapon's own E pickup
(sword, pistol and AK, flamethrower, bottle, grappling hook) and a dropped item (`DroppedItems.Take`). With the hand empty and a weapon slot free, the item is
equipped (primary first, else secondary) and drawn. Otherwise it goes into storage as before. Clothing always goes into storage. Tests: `InventoryTests.PickingUp*`,
`DroppedItemPlayTests`.

**Sword wave, key 7** with the sword in hand (`Character/ISwordWaveCaster`, `Weapons/Spells/SwordWaveCaster`, `SwordWaveProjectile`, `Editor/SwordWaveBuilder`): the sword does a
light-attack swing and 0.18 s later a white swoosh (three layered streaky ribbons, additive, a small light) leaves the blade down the crosshair like the fireball
(`ConvergedDirection`). It flies 34 m/s for 1.6 s, cuts through what it hurts (40 damage, each target once) and stops on the world. Colour: `SwordWaveBuilder.WaveColour`
(white for now). `CityX/Capture Sword Wave` renders it to Logs/CityShots. Not done: a dedicated slash animation (it reuses the light attack), impact effect, sound.

**Spawn:** the scene bakes a spawn for the scale it was built at, so `CityStreamer.PlaceAtSpawn` re-places the player (and the Old Guy) at the spawn of the world actually made.

## Bloodrage, key 8 (2026-10-04)

`Character/Abilities/BloodrageAbility.cs` (innate, like the fireball). For 7.5 s: movement speed x1.25 on foot (`PlayerLocomotion`, not a jetpack's speed), melee damage x1.25
(`MeleeAttacker.BloodrageFactor`, read by fists, the weapon swing, the spin attack and `SoulsSwordCombat`), the walk, run and jump animations replaced by the user's
`bloodrage_walking/run/jump.fbx` (walk and run replaced on 2026-10-04 by the user's Mutant Walking and Mutant Run; the jump is still the original Bloodrage jump) (`Character/Animations`, looped and stride-widened like the ordinary clips by `PlayerRigBuilder`), and a red aura (`BloodrageAuraBuilder`: two
particle layers rising off the body in world space plus a red light) that builds up in 0.35 s and fades over 1.4 s after the effect ends. The swap is an `AnimatorOverrideController`
made over the player's own at Awake and changed with one `ApplyOverrides` call, so the animator keeps its state; the sword's, gun's and other states are untouched. It cannot be
restarted mid-rage. `CityX/Capture Bloodrage Aura` renders the aura. Not done: a cooldown, a sound, the animation clip speed matching the faster body, a sword-equipped walk/run (those
are the sword's own clips).

**Bloodrage run head (2026-10-04):** the Mutant Run's neck nod averages 1.11 (the sprint's is 0.07) with peaks of 1.32, past the muscle limit of 1, which Mecanim plays beyond the limit: the head stared upward.
`BloodrageHeadFit` (run by `PlayerRigBuilder.Build` after the prefab is made) poses the real player body in the sprint and in the run through a playable graph, measures the head's average
pitch, and fits the run's neck nod (a shift search; clamped to the limit) so it matches the sprint's: -19.2 against -19.1 degrees, from -13.5. `CityX/Capture Bloodrage Run Pose` renders both for comparison.
(`AnimationClip.SampleAnimation` leaves a humanoid body in its rest pose; use a playable graph.)

**Bloodrage run head, second fix (2026-10-04):** matching the head's pitch to the horizon (the first fix) left it level but cranked back on a torso bent far more than the sprint's (46 degrees of lean
against 28), which still read as "tilted backwards". `BloodrageHeadFit` now matches the head's tilt RELATIVE TO THE TORSO (the sprint's is -9.3 degrees, the run now -9.3: neck nod shifted by -0.6),
so the head hangs forward off the hunched back the way the sprint's does.

**Joining players' Bloodrage walk and run (2026-10-17):** `NetworkedPlayer.prefab` had its first two `BloodrageClips` (the walk and the run)
saved blank, so a joining player, and every copy of one, kept the ordinary walk and run in Bloodrage (only the jump changed). `PlayerRigBuilder`
deleted and re-made the two stride-widened clips on every build, and a body saved while it still held the deleted ones stored nothing. The
clips are now written into the ones already there (`ClipAsset.Store`, as the Godot clips always were), and
`NetworkedPlayerRigTests.EveryBodyMadeFromThePlayersKeepsItsBloodrageClips` checks the joining player, the swordsman and the Shoota.

**Sword wave hitbox (2026-10-04):** the WHOLE crescent is the hitbox for enemies (a trigger box round all of it, on the tilted visual; each target is hurt once and the wave passes through).
Only a 0.3 m sphere at its CENTRE meets the world, swept along each fixed step and checked where it stands (`SwordWaveProjectile.WorldCheckRadius`), so the lower tip scraping the ground no longer
ends it. `SwordWaveProjectilePlayTests` pins both (ground under the lower edge does not stop it; centre in the ground does; a wall does; middle, top tip and bottom tip all hurt).

## Equipment slots (2026-10-15)

One list of slot keys, `Items/EquipSlots` (helmet, mask, top, gloves, legs, shoes, back, hand_r, hand_l), which items register against (`ItemRegistry`) and both
panels show: the inventory's equipped row (I) reads HELMET, MASK, TORSO, GLOVES, LEGS, SHOES, BACK, PRIMARY, SECONDARY, the loadout the same less the mask. The owner asked
for shoes, gloves and back slots in the inventory; its row had kept Godot's own keys (hat, hands, feet), which nothing is registered under, so a hat or a jetpack
worn showed nowhere in it. Shoes, gloves and the mask have no item yet. `EquipSlotTests` checks every wearable item has a slot on both panels;
`InventoryPanelCapture` renders the panel.

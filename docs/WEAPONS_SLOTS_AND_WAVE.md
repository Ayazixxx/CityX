# Primary/secondary weapons and the sword wave (2026-10-04)

**Primary / secondary** (`Character/Inventory.cs`): the old right/left hand slots (`hand_r`, `hand_l`) are now Primary and Secondary. The first weapon equipped is
primary, the next secondary (holstered), a third replaces the one in hand. Only the weapon in hand exists as an object; the other is remembered (its ammo/durability)
and rebuilt on draw through the normal OnEquip/OnUnequip. **Scroll wheel** swaps (`PlayerInputReader.TickCombat`, 0.25 s cooldown; not while carrying or aiming).
The inventory panel shows PRIMARY / SECONDARY and a `>` on the one in hand.

**Sword wave, key 7** with the sword in hand (`Character/ISwordWaveCaster`, `Weapons/SwordWaveCaster`, `SwordWaveProjectile`, `Editor/SwordWaveBuilder`): the sword does a
light-attack swing and 0.18 s later a white swoosh (three layered streaky ribbons, additive, a small light) leaves the blade down the crosshair like the fireball
(`ConvergedDirection`). It flies 34 m/s for 1.6 s, cuts through what it hurts (40 damage, each target once) and stops on the world. Colour: `SwordWaveBuilder.WaveColour`
(white for now). `CityX/Capture Sword Wave` renders it to Logs/CityShots. Not done: a dedicated slash animation (it reuses the light attack), impact effect, sound.

**Spawn:** the scene bakes a spawn for the scale it was built at, so `CityStreamer.PlaceAtSpawn` re-places the player (and the Old Guy) at the spawn of the world actually made.

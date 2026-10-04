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

## Bloodrage, key 8 (2026-10-04)

`Character/BloodrageAbility.cs` (innate, like the fireball). For 7.5 s: movement speed x1.25 on foot (`PlayerLocomotion`, not a jetpack's speed), melee damage x1.25
(`MeleeAttacker.BloodrageFactor`, read by fists, the weapon swing, the spin attack and `SoulsSwordCombat`), the walk, run and jump animations replaced by the user's
`bloodrage_walking/run/jump.fbx` (walk and run replaced on 2026-10-04 by the user's Mutant Walking and Mutant Run; the jump is still the original Bloodrage jump) (`Character/Animations`, looped and stride-widened like the ordinary clips by `PlayerRigBuilder`), and a red aura (`BloodrageAuraBuilder`: two
particle layers rising off the body in world space plus a red light) that builds up in 0.35 s and fades over 1.4 s after the effect ends. The swap is an `AnimatorOverrideController`
made over the player's own at Awake and changed with one `ApplyOverrides` call, so the animator keeps its state; the sword's, gun's and other states are untouched. It cannot be
restarted mid-rage. `CityX/Capture Bloodrage Aura` renders the aura. Not done: a cooldown, a sound, the animation clip speed matching the faster body, a sword-equipped walk/run (those
are the sword's own clips).

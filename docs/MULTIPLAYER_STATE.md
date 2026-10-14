# What other players see: movement, items, hair, damage

Everything below is owner-authoritative (the player's own machine decides, the server relays) and is tested over real sockets by `NetworkedPlayerStateSyncPlayTests`
(a server and two clients). The standing rule: a new thing a player can do or have is not finished until other players can see it (abilities: `docs/ABILITIES_MULTIPLAYER.md`).

## Synced

| What | How | Where |
|---|---|---|
| Position, rotation | `NetworkTransform`, interpolated over 3 ticks, position and rotation sent UNPACKED (no quantisation steps) | `NetworkedPlayerWiring.TuneTransform` |
| Walk/run/aim/crouch animation | Animator parameters 20 times a second; a copy glides toward them (0.09 s damped follow) instead of snapping | `NetworkedAnimatorSync` |
| Jump, dash, punches, sword swings, block | one reliable event each. A sword attack or guard is a NAME (`SwordStrikes`: `sword1h_slash`, `sword2h_guard`, ...): the owner's `PlayerAnimationDriver.PlayLocalState` announces it, the copy's `PlayRemoteState` hands it to its own `SwordPoseDriver`, so both bodies are posed by the same keyframes; a hit-stop's speed change is replicated the same way (`StateSpeedChanged`). | `NetworkedAnimatorSync` |
| **Gun reloads** (2026-10-14) | a NAME like a swing (`gun_reload`, `gun_reload_empty`): the copy's `GunReloadDriver` plays the same Blender motion upper-body only, the copy's gun lends it its parts (slide/bolt, magazine, trigger) in `MountCosmetic`, so the magazine drops (and lies on the copy's floor) and goes back in | `GunReloadDriver`, `PistolWeapon.HookReload` |
| **Gun shots** (2026-10-14, pistol and AK-47) | one reliable event per shot (`gun_fire`, `PlayerAnimationDriver.AnnounceState`, never played on the shooter's own body): the copy's gun flashes, plays the crack in 3D at the gun, draws a tracer down its barrel, throws brass and cycles its slide or bolt; a shot also ends a reload the copy is still showing. Damage is separate (`NetworkedDamage`). | `PistolWeapon.OnRemoteShot`, `FirearmEffects` |
| **Rifle hold** | a copy holding a rifle holds it as one (`PlayerLocomotion.SetRifle` from `MountCosmetic`): two hands, low ready, shouldered when the synced Aiming parameter is on. Its aim pitch is not synced yet: a copy's rifle aims level. | `PlayerLocomotion.RifleAim.cs` |
| **Climbing** | the Animator `Climbing`/`ClimbDirection`/`ClimbSpeedScale`, plus the wall plane the limbs are kept off and the ledge the hands reach for, so a copy runs the same IK in its own LateUpdate | `NetworkedAnimatorSync.SendMotionIfNeeded` / `ApplyRemoteMotion` |
| **Mantle** (the pull-up over a ledge) | one reliable event (`PlayerClimbing.MantleStarted`); the copy plays `ClimbTopOut` fitted to the mantle's length. The body moves with the transform. | `NetworkedAnimatorSync` |
| **Vault** | the body's path and lean travel with the transform; the hands on the obstacle go with the ledge-hands packet | same |
| **Held items and back items** (sword, pistol, AK-47, bottle, flamethrower, grappling hook, jetpack, parachute) | the owner sends the ids of the items that exist as objects, whenever they change (`Inventory.EquipmentChanged`); a copy builds a cosmetic model of each (`ICosmeticMountable.MountCosmetic`: the right bone and grip, the sword's two-handed hold or the combat sword's one-handed one) and switches every behaviour off. The copy's fingers close round the mounted model exactly as the owner's do (`HandGrip`, driven by what hangs off the hand, not by input). The jetpack's flame follows the thrust parameter. BufferLast: a late joiner is told. | `NetworkedEquipmentSync`, `Inventory`, `Items/ICosmeticMountable` |
| **Hairstyle and colour, skin, paint, name** | once when the body appears (BufferLast). The joining player's body had NO hair component before this, so no hairstyle could show on it: added to `NetworkedPlayer.prefab` by the scene builder. | `NetworkedPlayerIdentity`, `CityMassingSceneBuilder` |
| **Damage between players** | every hit (punch, sword, bullet, fireball, ice shard, sword wave, flame, thrown bottle, poison) is worked out on the attacker's machine against the target's COPY; the copy forwards it (`PlayerVitals.Relay`) to the server, which checks it (sender is not the victim, a sane amount, the attacker's body within 90 m, at most 40 hits a second) and sends it to the real body's owner, whose own vitals take it: block, parry, poise, stun, health and reaction as for any hit. The blow's poise damage travels with it. **The verdict comes back** (2026-10-13): when the owner's body parried, soaked or rolled through the blow, that answer returns to the attacker with the hit's id (`NetworkedDamage.Verdict`), and a parry staggers the remote attacker (`SoulsSwordCombat.ApplyRemoteVerdict`). The hit reaction itself is announced by name like a swing (`HitReactions`). | `NetworkedDamage`, `IRemoteDamageRelay` |
| **Health** | the owner shares their health 4 times a second, so a copy shows it (and a copy that has died stops being a target) | `NetworkedDamage` |
| Abilities (1, 2, 3, 4, 7, 8) | see `docs/ABILITIES_MULTIPLAYER.md` | |
| **Light switches** (world state, not a body) | FishNet broadcasts: flip -> server (sane room, 12/s) -> every client; a joiner is sent every light that is on. Not RPCs on a player body: those stop at the 100 m view distance. | `RoomLightsNetwork`, `docs/ROOM_LIGHTS.md` |

A joining player's body now has an `Inventory` with the item prefabs (it had none), so it can hold things at all.

## Not synced yet

- **Clothing** (jackets, jeans, coats, hats): they are equipment, but a garment merges into the body's skinned mesh and is not shown on copies. `EquipmentReplicationGuardTests` exempts `Wearables`.
- **Ragdoll / knockdown / stand-up**: a hit's knockdown plays on the victim's machine only; other players see the body's transform but not the limp pose.
- **Parachute canopy open/closed**, **grapple rope**, **bottle throw projectile**, **vehicles**, **sitting on benches**, **doors**.
- Death is shown as health 0 only.

## Adding a new held item

Implement `ICosmeticMountable` on its component (copy `SwordWeapon`), and add its prefab to `NetworkedPlayerWiring.ItemPrefabPaths`.
`EquipmentReplicationGuardTests` fails otherwise.

# CityX Multiplayer Architecture

Networking is built on **FishNet** (Unity). This document describes how it works
*today*, including the parts that are not finished. Where the code and the
original design intent disagree, that is called out explicitly (see
[Gaps and caveats](#gaps-and-caveats)).

## 1. Governing rule

There is exactly one simulation and singleplayer is just a server with one local
client. `GameMode` (`Core/GameMode.cs`) is a **topology selector, not a feature
switch**: nothing may branch on it to decide what the game *does*. Systems ask
FishNet whether they are server/client (`IsServerInitialized`, `IsOwner`, ...),
never "which mode am I".

## 2. Topologies

| `GameMode` | Runs server | Runs client | Binds | Intended size |
|---|---|---|---|---|
| `Singleplayer` | yes | yes (local) | `127.0.0.1` only | 1 |
| `HostedWorld` | yes | yes (local) | `0.0.0.0` | ~2-8 |
| `DedicatedShard` | yes | no (headless) | `0.0.0.0` | dozens |
| `RemoteClient` | no | yes | - (dials out) | - |

`GameModeInfo` exposes the facts as orthogonal booleans (`RunsServer`,
`RunsClient`, `AcceptsRemoteConnections`, `IsHost`). Singleplayer is **not**
transport-less: FishNet has no offline transport, so it is a host whose server
binds loopback and whose client connects to it.

### `NetworkBootstrap` (`Net/NetworkBootstrap.cs`)
The **only** place a `GameMode` becomes running sockets. `Begin(mode, args)`:

1. sets the transport port;
2. if `RunsServer`: binds loopback (Singleplayer) or the public address (all others) and starts `ServerManager`;
3. if `RunsClient`: starts `ClientManager`, connecting to `127.0.0.1` when this process is also the server, otherwise to `args.Address`;
4. returns whether every socket actually came up (bind failures are synchronous).

`Start()` auto-begins from the command line unless `AutoStartOnPlay` is off (the
title screen and tests turn it off). A bad `LaunchArgs` refuses to start rather
than coming up half-configured.

### `LaunchArgs` (`Core/LaunchArgs.cs`)
Pure, testable argv parser: `-mode singleplayer|host|dedicated|client`,
`-address`, `-port` (default 7770), `-seed`. Unknown engine flags are ignored;
malformed values of ours are hard errors. `LaunchArgs.For(...)` builds one
directly for UI callers.

```
CityX.exe -mode dedicated -port 7770 -seed 12345
CityX.exe -mode client -address 203.0.113.7 -port 7770
CityX.exe                                   # singleplayer
```

## 3. Connecting (title screen)

`UI/TitleMultiplayerView` drives it:
- **Host** -> `Bootstrap.Begin(HostedWorld, LaunchArgs.For(HostedWorld, loopback, port))`; success/failure is known synchronously.
- **Join** -> `Bootstrap.Begin(RemoteClient, ...)`; there is no synchronous answer, so it subscribes to `ClientManager.OnClientConnectionState` and reacts to the real result.

## 4. How every peer agrees on the world

The world is **deterministic from a seed**, and nothing about it is streamed
over the network. `CityMassingSceneBuilder.WorldSeed` (currently `29481073`) is
baked into the scene, and `CityStreamer` builds chunks purely from that seed and
each chunk's own coordinates. Every peer that loads the same scene builds the
same city. (`LaunchArgs.WorldSeed` is parsed and logged but not currently fed
into the streamer - see gaps.)

## 5. Player spawning

Two different prefabs exist on purpose:

- **`Player.prefab`** - plain, no networking components. About 80 PlayMode tests
  `Instantiate` it directly with no NetworkManager; FishNet's
  `NetworkObject.Awake()` logs an error for any instance that is neither a
  scene object nor pool-spawned, which would fail all of them.
- **`NetworkedPlayer.prefab`** (built by `NetworkedPlayerBuilder`) - derived from
  `Player.prefab`, adds `NetworkObject`, `NetworkTransform`,
  `NetworkedPlayerIdentity`, `NetworkedAnimatorSync`, and the view-distance
  `NetworkObserver` (section 8). Used only for spawned clones.

`NetworkedPlayerSpawner` (in the scene) runs only when the mode runs a server and
is not Singleplayer:

1. Waits until the scene's baked player is `IsServerInitialized` (FishNet defers scene objects by a frame; calling `GiveOwnership` earlier throws).
2. For every already-connected client **and** every future connection:
   - the **host's own connection** gets `LocalPlayer.GiveOwnership(...)` - the scene-baked player becomes the host's body (it already has camera, HUD, etc. wired);
   - **every other connection** gets a clone via `NetworkManager.GetPooledInstantiated(PlayerPrefab, ...)` + `ServerManager.Spawn(nob, connection)`, placed on a 2 m circle around the spawn point offset by client id.

The baked scene player is a real scene object: its `NetworkObject` gets a genuine
`SceneId` from `NetworkedPlayerReserialize.ReserializeSceneObject` at scene-build
time (a plain `AddComponent` in batch mode never runs `OnValidate`, so this is
forced explicitly). Clones use the per-object variant and must *not* get a
SceneId.

`NetworkedPlayerIdentity.Awake` calls `SetIsNetworked(false)` when no session is
active, so the baked player keeps working as the plain singleplayer body instead
of being deactivated by FishNet.

## 6. Authority model (as implemented)

For real players this is **owner-authoritative**, not server-authoritative:

- The owner's `PlayerInputReader` is the *only* thing that ticks `PlayerLocomotion` (movement, jetpack, ragdoll, climbing, every Animator write). `NetworkedPlayerIdentity.OnStartClient` disables `PlayerInputReader` on every non-owner copy - a complete, zero-touch authority gate.
- Position/rotation for remote copies comes from FishNet's `NetworkTransform`, client-authoritative (its default), so the owner drives it.
- For an owned spawned clone, `OnStartClient` re-targets the scene's single `ThirdPersonCamera` at that body (the scene-baked player is skipped - it already has its camera).

## 7. What is replicated

| Data | Mechanism | Notes |
|---|---|---|
| Position / rotation | `NetworkTransform` | client-authoritative |
| Locomotion animation | `NetworkedAnimatorSync` | Owner reads its own Animator params (Speed, MoveX/Z, Grounded, Crouching, Aiming, SwordEquipped, GunEquipped, JetpackThrusting, Dancing, Breakdancing) every 0.08 s -> `ServerRpc` -> `ObserversRpc(ExcludeOwner)` -> applied to remote Animators. Parameter names must stay in lockstep with `PlayerLocomotion`. |
| Appearance (name, skin colour, hair, painted atlas) | `NetworkedPlayerIdentity` | Sent once on start (`ServerRpc` -> `ObserversRpc(BufferLast = true)`); `BufferLast` replays it to late joiners / newly-in-range observers. Deliberately does not touch level/XP/vitals. |
| One-shot actions: jump, dash | `NetworkedAnimatorSync` | `PlayerLocomotion.AnimatorTriggerFired` on the owner -> reliable `ServerRpc` -> `ObserversRpc(ExcludeOwner)` -> `Animator.SetTrigger` on every other copy. Only the `Jump`/`Dash` triggers are accepted off the wire. |
| One-shot actions: every punch / sword swing / heavy / spin / block | `NetworkedAnimatorSync` + `PlayerAnimationDriver` | `PlayerAnimationDriver.StatePlayed(stateName)` on the owner -> same RPC path -> `PlayerAnimationDriver.PlayRemoteState`, which ignores names the Animator doesn't have and does not re-announce (no echo). Visual only: remote copies never run `MeleeAttacker` hit/damage logic. |
| One-shot actions: soulslike sword swings and guard (`SoulsSwordCombat`) | `NetworkedAnimatorSync` + `PlayerAnimationDriver` | Same shape, but `PlayerAnimationDriver.TimedStatePlayed(state, speed, startSeconds)` -> `SendTimedSwingToServer` -> `BroadcastTimedSwing` -> `PlayRemoteState(state, speed, start)`. The speed and clip offset ride along because the sword plays a stretched slice of a much longer clip; ending a swing announces the sword idle at speed 1, which restores remote animator speed. Remote copies clamp speed to 0.1-4 and ignore NaN / unknown states. See `docs/SOULSLIKE_COMBAT.md`. |
| Car enter/exit, climbing | **not replicated** | vehicles aren't networked; the `Climbing` bool isn't synced either |

## 8. Interest management (scaling)

Without it every client observes every player. Now each player object carries a
FishNet `NetworkObserver` with a `DistanceCondition`
(`Character/PlayerViewDistance.asset`, max distance
`NetworkedPlayerIdentity.ViewRadius = 100 m` - past pistol range (60 m), inside
the 380 m fog distance).

- Applied in both places a real player's network components are added, via `NetworkedPlayerBuilder.AddViewDistanceObserver`: the clone prefab and the scene-baked host body, so the two can't drift apart.
- FishNet only sends spawn/RPC/transform traffic to a NetworkObject's *observers*, so distant players are neither instantiated on your machine (no Animator, no garments) nor synced to you.
- `DistanceCondition` is timed (re-evaluated on an interval, not per frame) and measures from the objects a connection *owns*. A player always observes their own body (tested at 500 m from origin).
- `_observerConditions` is an internal FishNet field, so it is written via `SerializedObject`/`SerializedProperty` in editor builders, and the condition must be a persisted asset (see the shared-prefab-collection gotcha in section 10).

## 9. The `CityX.Spike` sandbox

R&D for scale and feel, kept separate from the real player:

- `SpikeAgent` (bare server-simulated capsule), `SpikePlayer` (a connection's presence; also carries the distance condition now), `SpikePredictedPlayer` (client-side prediction + server reconciliation using FishNet `Replicate`/`Reconcile`, no `NetworkTransform`), `SpikeWorld` (spawner), `SpikeMetrics`.
- `SpikeAssetBuilder` generates prefabs and the `DistanceCondition` asset, plus `SpikeNetworkManager.prefab` (AllowMultiple persistence, shared `SpawnablePrefabs`).
- Tests measured: server holds tick rate with 30 real clients + 200 entities; per-client bandwidth stays flat as entity count grows (`FieldRadiusFor` keeps density constant); observed set turns over when a player moves; prediction feels responsive at simulated 100 ms latency; player-to-player culling (`InterestManagementPlayerToPlayerPlayTests`).
- Prediction is proven **only here**; the real player does not use it.

## 10. Testing multiplayer

- Real multi-client tests run several `NetworkManager`s in one process over loopback. Use the generated `SpikeNetworkManager` prefab for **all** of them: it shares one asset-backed `SpawnablePrefabs`. Two independently `ScriptableObject.CreateInstance`'d collections resolve `PrefabId`s differently and fail with `PrefabId N is out of range`, even if pre-registered by hand.
- `NetworkManager` construction logs a benign "SpawnablePrefabs is null" error; wrap setup in `LogAssert.ignoreFailingMessages`.
- With a dedicated server + N clients, look up a client's server-side connection by **sorted `ClientId`**. In `HostedWorld` the host's own local connection is also in `ServerManager.Clients`, so "first entry" can be the host itself.
- Physics-dependent unit tests need `Physics.SyncTransforms()` after moving a fresh collider.

## Gaps and caveats

- **Real players are owner-authoritative**, which conflicts with the "server-authoritative" rule stated in `GameMode`. Cheating/rollback protection does not exist yet; prediction is only in the Spike.
- **`DedicatedShard` has no player spawn path.** `NetworkedPlayerSpawner` assumes a baked host body; a headless shard would need every player to be a spawned clone and the scene to stop baking one.
- **`-seed` is not wired to the world.** The seed comes from the scene bake; `LaunchArgs.WorldSeed` is only logged.
- **Not networked:** NPCs, `/spawn`-ed world props, inventory/equipment, car enter/exit and climbing animations, and (per the last audit) doors, glass, ragdoll death, carrying and emotes.
- **History worth knowing:** until the one-shot sync work, `NetworkedPlayer.prefab` was saved with an explicit `m_Controller: {fileID: 0}` override, so every spawned clone had an Animator playing nothing (no synced animation of any kind could show). `NetworkedPlayerBuilder` now restores the controller and `NetworkedPlayerRigTests` guards it. Health/damage authority was also not synced at the last audit.
- **Scale is proven by extrapolation, not measured at 1000.** 30 real sockets is the practical ceiling of the test harness. Server CPU cost per simulated player body, and GPU instancing for many nearby players' garments, are untested/undone.
- The 100 m view radius is a tuning choice, not a measured optimum.

## Key files

| Area | Path |
|---|---|
| Modes / args | `CityX/Assets/CityX/Core/GameMode.cs`, `Core/LaunchArgs.cs` |
| Startup | `Net/NetworkBootstrap.cs`, `UI/TitleMultiplayerView.cs` |
| Player spawn | `Character/Network/NetworkedPlayerSpawner.cs`, `Character/Network/NetworkedPlayerIdentity.cs`, `Character/Network/NetworkedAnimatorSync.cs` |
| Prefab / scene wiring | `Character/Editor/NetworkedPlayerBuilder.cs`, `View/Editor/CityMassingSceneBuilder.cs` |
| Interest management | `NetworkedPlayerBuilder.AddViewDistanceObserver`, `Character/PlayerViewDistance.asset` |
| Scale / prediction R&D | `Spike/` |
| Tests | `Tests/PlayMode/NetworkBootstrapPlayTests`, `NetworkedPlayerSpawnPlayTests`, `InterestManagementPlayTests`, `InterestManagementPlayerToPlayerPlayTests`, `ScaleMeasurementPlayTests`, `ThirtyClientLoadPlayTests`, `PredictionLatencyPlayTests` |

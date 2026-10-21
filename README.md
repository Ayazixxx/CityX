# CityX

A multiplayer open-world game set in a procedurally generated megacity, built in Unity: third-person melee and gun combat, parkour and
climbing, spells, a jetpack and a character creator. It started as a Godot prototype and is being ported to Unity for multiplayer scale.

## Repository layout

| Path | What it is |
|---|---|
| `CityX/` | **The Unity project** (open this folder in Unity). |
| `CityX/Assets/CityX/` | All game code and assets, one assembly per area (below). |
| `CityX/SourceArt/` | Blender scripts that build models and animations headlessly and export them into `Assets` (the `.blend` files are not committed). |
| `docs/` | Design notes, one per feature (`CLIMBING.md`, `SOULSLIKE_COMBAT.md`, ...). `GAME_DESIGN.md` is the whole-game plan, `CODE_STYLE.md` the coding conventions. |
| `tools/` | `unity_tests.ps1` (headless test runner), `model_qa.py` (checks imported models), small dotnet helpers. |
| everything else at the root | The original **Godot** prototype (`project.godot`, `player/`, `city/`, ...), kept as the reference the port reads from. It is not built. |

### Code areas (`CityX/Assets/CityX/`)

Each folder is its own assembly (`.asmdef`) with one namespace, so dependencies stay one-way.

| Folder | Assembly | Responsibility |
|---|---|---|
| `Core` | `CityX.Core` | Game-wide basics: saved characters, settings, launch arguments, the play session, small shared helpers. |
| `World` | `CityX.World` | The city as pure data: a chunk is a function of the world seed and its coordinates, so every machine builds the same city without sending geometry. Also the deterministic random it is built with. |
| `View` | `CityX.View` | Turns the world data into meshes, streams chunks around the player, lighting, weather. |
| `Items` | `CityX.Items` | The item registry and equip slots (pure data). |
| `Character` | `CityX.Character` | The player and NPCs: movement, posing and IK, combat, abilities, stats, body and networking. Runtime scripts sit in topic folders (`Movement`, `Combat`, ...). |
| `Weapons`, `Wearables`, `Vehicles` | `CityX.Weapons`, ... | Items that are held, worn or driven. |
| `UI` | `CityX.UI` | HUD, menus, the dev console, the character creator. |
| `Net` | `CityX.Net` | Session start-up on FishNet. |
| `MapImport/Editor` | `CityX.MapImport.Editor` | Imports Source-engine `.bsp` maps, with procedurally generated textures. |
| `NPC`, `Radio`, `Scenes` | (assets only) | Models, audio and the two scenes (`Title`, `CityMassing`). |
| `*/Editor/` | `CityX.*.Editor` | Editor-only **builders** that generate the scenes, prefabs and meshes (see below). |
| `Tests/EditMode`, `Tests/PlayMode` | test assemblies | NUnit tests; the `*Capture` tests render images to `CityX/Logs/CityShots/` for visual checks. |

Folders ending in `~` (`World/LegacyGrid~`, ...) are an older city generator set aside on purpose; Unity ignores them.
`Spike/` is a networking sandbox, not part of the game.

## Getting started

1. Install **Unity 6000.3.21f1** (Unity Hub) with Windows build support.
2. Open the `CityX/` folder. The first import takes a while; packages, FishNet included, resolve from `Packages/manifest.json`.
3. Open `Assets/CityX/Scenes/Title.unity` and press Play. `CityMassing.unity` is the city itself.

In play mode, the backtick key opens the dev console (`/help` lists the commands; `/equip spikesword`, `/spawn swordsman`,
`/weather rain`, `/time night`, ...).

## How the project is built

- **Generated content.** `CityMassing.unity`, `Player.prefab`, `NetworkedPlayer.prefab` and most item prefabs are written by editor
  builders (`CityMassingSceneBuilder`, `PlayerRigBuilder`, `NetworkedPlayerBuilder`, one builder per item). Change the builder and rebuild;
  do not edit the generated files by hand. Rebuild everything from the menu **CityX** in the editor, or headlessly:

  ```
  Unity.exe -batchmode -quit -projectPath CityX -executeMethod CityX.ViewEditor.CityMassingSceneBuilder.Build -logFile Logs/build.log
  ```

- **Logic in plain classes.** The maths (ledge probes, strike keyframes, IK, the city plan) lives in plain C# classes that are unit-tested
  without a scene; MonoBehaviours only wire them in.
- **Multiplayer first.** Anything a player does or holds is replicated to the other players (FishNet, owner-authoritative bodies), and guard
  tests check every ability and item for it. See `docs/MULTIPLAYER_STATE.md`.

## Running the tests

With the Unity editor closed:

```
powershell -ExecutionPolicy Bypass -File tools/unity_tests.ps1 -Platform EditMode
powershell -ExecutionPolicy Bypass -File tools/unity_tests.ps1 -Platform PlayMode -TimeoutMinutes 75
```

Add `-Filter CityX.Tests.SomeTests` to run one class, and `-Graphics` for the `*Capture` render tests. Known failures are listed in
`CLAUDE.md` under "Hard rules".

## Conventions

`docs/CODE_STYLE.md` holds the conventions (shared helpers to reuse, one topic per file, no leftovers, zero compiler warnings).
`CLAUDE.md` is the working brief for the AI assistant that wrote much of the code; it doubles as a summary of the project's rules and
non-obvious facts.

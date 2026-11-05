# CityX: project context for Claude

Unity port of a Godot game: a procedurally generated multiplayer megacity with third-person combat, parkour, spells and a jetpack.
**The owner is not a programmer.** Explain results in plain language: what changed, how it looks or feels, what to try in the game. Decide technical details yourself.

## Stack
- Unity **6000.3.21f1**, URP **17.3.0**, Input System **1.20.0** (new handler only; never use `UnityEngine.Input`), uGUI 2.0.0, Test Framework 1.6.0.
- Networking: **FishNet** (git package), owner-authoritative player bodies, server relays.
- Target: **Windows PC** (D3D12 player, 60 fps budget). Consoles later: don't hard-depend on mouse and keyboard.

## Layout
- Repo root = the **original Godot project** (`project.godot`, `main.gd`, `player/`, `city/`...). **Read-only reference** for porting: read the `.gd`, reuse its assets. Never edit it.
- `CityX/` = the Unity project. Code is in `CityX/Assets/CityX/<Area>/`, one assembly each: `Core`, `Items`, `World` (pure city/plan logic), `Character`, `Weapons`, `Wearables`, `Vehicles`, `View` (mesh building, chunk streaming), `UI`, `Net`. `Spike/` is a networking sandbox: ignore it.
- Editor-only builders live in `*/Editor/`. Tests: `Assets/CityX/Tests/EditMode` and `PlayMode`.
- `Character`'s runtime scripts sit in topic folders: `Player`, `Movement`, `Posing`, `Combat`, `Abilities`, `Stats`, `Body`, `NPC`, `Network`, `Interaction`, `Story`. `View`, `UI` and `Weapons` likewise (`View/Glass`, `UI/Hud`, `Weapons/Spells`, ...; the table is in `docs/CODE_STYLE.md`). Big classes are split into partial files by topic (`PlayerLocomotion.Climbing.cs`). Namespaces stay one per assembly.
- `CityX/SourceArt/<Move>/` = moves animated in Blender (driven through the Blender MCP, `.mcp.json`). Its scripts rebuild the rig, bake the poses and export JSON to `Resources`, which `BakedMotion` plays bone by bone after the Animator. The `.blend` is not committed. Example: `docs/OCTASLASH.md`.
- `docs/*.md` hold the design notes per feature (CLIMBING, FIST_COMBAT, MULTIPLAYER_STATE, ...). Read the relevant one before changing a feature; update it after.
- `docs/GAME_DESIGN.md` is the whole-game plan (progression, world layers, death, karma, factions, roadmap, open decisions). Read it before starting any new system; build in its roadmap order.
- `tools/unity_tests.ps1` runs tests headless.
- `tools/model_qa.py` checks every model that comes in (Meshy zip, owner's fit, bake) before it goes into the game; show the owner its labelled picture and record the findings they say are meant with `--keep` (`docs/MODEL_QA.md`).

## Commands (PowerShell; the Unity editor must be closed)
- `powershell -ExecutionPolicy Bypass -File tools/unity_tests.ps1 -Platform EditMode|PlayMode [-Graphics] [-Filter CityX.Tests.SomeTests]`
- Rebuild the generated scene and prefabs: `"C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe" -batchmode -quit -projectPath CityX -executeMethod CityX.ViewEditor.CityMassingSceneBuilder.Build -logFile Logs/x.log`
- A body mesh change (`FusedBodyBuilder`) also needs `CityX.CharacterEditor.CharacterRebuild.RebuildAll` (civilians) and `CityX.UIEditor.TitleScreenBuilder.Build` (the character creator): each saves its own bone list, and a stale one scatters the body's parts (`SharedBodyBindingTests` guards it).
- `CityMassing.unity`, `Player.prefab` and `NetworkedPlayer.prefab` are **generated** by the builders (`CityMassingSceneBuilder`, `PlayerRigBuilder`, `NetworkedPlayerBuilder`). Change the builder, rebuild, never hand-edit the output. New serialized fields or components need a rebuild.

## Architecture conventions
- **Pure logic in plain classes, thin MonoBehaviours around it** (e.g. `LedgeProbe`, `FistStrikes`, `ClimbLimbStepper`): the maths is unit-tested without a scene, the component only wires it in.
- The city is a **pure function of the world seed and coordinates** (`new CityWorld(seed)`): a chunk is built from its own coordinates alone, so server and clients agree without sending geometry.
- Procedural poses (IK, torso twist, climb limbs) run in `PlayerLocomotion.LateUpdate` **after** the Animator, with `TwoBoneIK`. The animation driver is told states **by name**; no parallel animator-parameter state machines.
- Tuning data goes in ScriptableObjects or serialized fields, with named constants, not scattered numbers.
- No joint-physics swing for the grapple (failed six times).
- **Code style: `docs/CODE_STYLE.md`** (read it before writing code). Reuse the shared helpers it lists (`TransformSearch`, `UiKit`, `CreatorKit`, `StillPose`...) and never paste a helper into a second file. Keep one topic per file. Commit no diagnostics or dead code. Keep comments to the why, and put the history in commits and docs. Keep the build at zero compiler warnings.

## Hard rules
- **Multiplayer-first**: anything a player does or has (ability, held item, movement) must be visible to other players in the same commit: `IReplicatedAbility` or `ICosmeticMountable`, wired in `PlayerAbilityWiring` / `NetworkedPlayerWiring`, with a test. Guard tests enforce it. Spec: `docs/MULTIPLAYER_STATE.md`.
- Every change gets tests. Run EditMode and PlayMode before committing. **A full run is green** (since 2026-10-31: the corpse-fling test used to depend on test order, and the straight-street-stair test reported Inconclusive on seeds without one; now it tries earlier worlds' seeds too). A FAIL is yours.
- **Verify visuals by rendering** (the Explicit `*Capture` tests with `-Graphics`) and looking at the images. Green tests do not mean it looks right.
- Keep assets small: no embedded textures, bake and trim meshes.
- **City-generation change = reroll the world seed.** It is hardcoded in several places (currently `95998845`): grep for the old number and change them all together.
- If an open Unity editor blocks a headless run, **kill `Unity.exe` yourself without asking**. Never edit `.cs` files while a test run is in progress (it causes false failures).
- **Commit each finished task** after its tests pass, with the `Co-Authored-By` line. **Never push** unless asked.

## Non-obvious facts
- The player is **2.6 m tall** (`BodyRig.StandingHeight`): real-world sizes are about 1.3x (a door is a whole 3.2 m window row).
- FishNet: `IsOwner`/`IsSpawned` throw on a body that never joined a session (singleplayer, the baked player). Guard with `NetworkObject != null` first (see `IsLiveOwner`). `NetworkObject` goes only on the baked scene player and `NetworkedPlayer.prefab`, never on `Player.prefab`.
- In batch captures frames are about 0.4 ms: set `Time.captureFramerate = 60` before rendering anything timed (IK steps, animators). A cube seen edge-on along its face is invisible.
- `InputTestFixture`: one key event per frame, and a Mouse device is required. Runtime instance IDs are not ordered.
- `StreamAround` never re-details a chunk loaded far; captures must `Rebuild()`.
- **Every test runs on the classic city** (`ClassicCityScaleFixture`), but the game plays the varied one (`CityScale.Default`): other roads, other node ids, wider streets. `tools/cityplan` measures the game's city; a capture that should show what is played sets `CityScale.Active = CityScale.Default` first (`TrafficCapture`).
- **The body must look like Godot's** (`docs/CHARACTER_LOOK.md`): shaped and bound arms-down (`BodyRig.ShapePositionOf`/`RestBindMatrix`); walk/run/idle clips are **baked from the Godot clips** (`GodotMotionBaker`); the humanoid avatar's twist distribution stays **0**; ragdoll rigidbodies must have **interpolation off while kinematic**. Each of these, undone, tears or distorts limbs in sprints (`LimbFlipPlayTests` guards it).
- A GIF pasted into chat reaches you as ONE still frame. To study a reference move, split the GIF file on disk into every frame (System.Drawing from PowerShell, `Image.SelectActiveFrame`) and read the frames.
- Shell quirks (Windows, Git Bash): heredocs containing apostrophes fail, and multi-line `sed -i 'a\'` collapses onto one line; use the Write/Edit tools for multi-line code.
- Persistent memory notes live outside the repo (the Claude memory folder), not here.

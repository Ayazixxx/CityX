# Code style: keeping CityX tidy

## Why this exists

The owner asked on 2026-10-17: "ive heard ai tends to write very sloppy code ... research what that means and try to improve upon and
organize the code we've made so far". Studies of AI-assisted code keep finding the same problems:

- Copy-paste instead of reuse. GitClear's 2026 report counts 81% more duplicated blocks than in 2023, and copied code now outnumbers
  refactored code about 5 to 1 (it was 1 to 2 in 2022). New code also calls existing functions less often (35% fewer calls per changed line).
- Leftovers: debugging code, half-used helpers, dead branches.
- Very large files and functions that do many jobs.
- About 1.7 times as many problems as human-written code (CodeRabbit, cited by Shiplight), readability problems most of all.

The 2026-10-17 review found all four in this project, removed what could be removed without changing the game (see "The 2026-10-17
review" below), and wrote down these rules so new code stays clean. Unity's own guidance boils down to the same thing: write the project's
conventions down and apply them every time.

## The rules

1. **Reuse before you write.** Before adding a helper, search the code for one that already does the job. If you are about to paste a
   function into a second file, move it to a shared place and call it from both. Shared helpers so far:
   - `Core/TransformSearch.FindDeep`: a part of a model by name, anywhere under a root (inactive parts too).
   - `UI/UiKit`: `EnsureEventSystem`, `Stretch`, `MakeText` for panels built in code, by an editor builder or at runtime.
   - `UI/Buttons/ArrowButtons`: the new button style (owner, 2026-11-10): `Straight` and `Curled` arrows drawn with no texture (`ArrowGraphic`),
     black-edged white italics, a yellow band and a little growth when pointed at or selected (`ArrowButtonLook`). Every button is to move to it.
   - `UI/Creator/CreatorKit`: the character creator's tool column, prompts and stage lights. The title screen's creator and ReDraw are both built
     from it, so they can no longer drift apart. They had drifted, which is how ReDraw's tools came to do nothing (fixed in fa8cefc).
   - `Character/Posing/StillPose`: fixed poses for a body shown without its Animator.
   - `Core/LitShader`: the shader every lit material is made with (CityX/Lit, cel shaded); never ask for URP's Lit by name.
   - `Character/Combat/SeeThroughMaterial.Apply`: a particle material made truly see-through (alpha or additive), so the cel lines skip it.
   - `Items/InertModel`: an item's model with nothing of the item acting (another player's copy holds one; a dead body drops one).
   - `Character/Interaction/InteractionReach.LookSource`, `CanReach`, `LocalPlayerBody` and `IsLeaving`: where a player looks from, whether a
     pickup is in reach, and which body entering or leaving its trigger is this machine's player.
   - `Wearables/GroundWearable`: a rigid wearable lying on the ground, picked up with E (headwear and shoes derive from it).
   - `Wearables/Editor/ModelParts.Add`: one named part of an imported model, baked with its own material under a rigid wearable's prefab (the
     3D Glasses, the Squirrel Mask).
   - `Wearables/WornBodyCover.Hide`/`Show`: hide the body under whatever is worn (garments, shoes), several at once.
   - `Wearables/Editor/LongCoatBody`: what a long open-fronted coat hides of the body and how it lies on the ground (the Midnight Coat, the
     Fur Hooded Parka). In Blender, `SourceArt/garment_rig.py` holds the skinning steps every garment's build script shares.
   - `Tests/PlayMode/BuildingShapeCapture.Shoot` and `ShotsFolder`: render a camera to `Logs/CityShots/<name>.png` (20 captures use it).
   - `Tests/PlayMode/OpenRun.Face`: turn the player to look along an open, level run of street (the tests of moves that travel).
   - `Character/Editor/ClipAsset.Store`: save a generated animation clip into the one already at its path. Never delete and re-create an
     asset that prefabs or controllers hold: what still held the deleted one saves a blank.
   - `Character/NPC/KeyedMotion.Along`: one smooth curve through timed keys (the Crimson Claw's swipe and grab).
   - `Character/NPC/Sight.SolidBetween`: whether a wall, floor or glass stands between two points, for an enemy's eyes (the Shoota, the
     swordsman, Old Guy unseen).
   - `Character/Body/MaterialBlocks`: each material's own property block, for a copy of a renderer to look like it (a dye lives there).
   - `Character/Network/SessionRelay`: the base of everything kept the same between machines by broadcasts (light switches, balcony doors,
     glass, graffiti, dropped items, the weather, the enemies). It finds the session, hooks and unhooks what the relay hears as this
     machine becomes the server or a client (`HearAsServer`, `HearAsClient`, `Hear<T>`), and says when a joiner may ask for what it missed
     (`OnLetIn`). A new shared piece of the world derives from it.
   - `Character/Interaction/GroundPickup`: something on the ground picked up with E: who has walked into its trigger, whether they tapped
     E in range, and `Take` into the inventory. Every item and wearable uses it; a pickup that also needs to be looked at asks
     `InteractionReach.CanReach`.
   - `Character/Player/GameControls.Now`: every control the player uses, from the keyboard and mouse or a controller (docs/CONTROLLER.md).
     Ask it ("Jump.Pressed", "Move", "Slot(3)"), never `Keyboard.current` or `Mouse.current`: a key read straight from the keyboard
     is a thing the controller cannot do. `Key(...)` is for what only a keyboard has (typing, the console).
   - `UI/MenuFocus` and `FreedCursor`: what a page that takes over the screen does to the game (the pointer freed, the player's input held,
     the orbit camera resting) and gives back on closing. Thirteen views use one or the other.
   - `UI/Menus/SettingsPage`: the settings a player can change, shared by the pause menu and the title screen.
   - `UI/IconGeometry`: inside-a-polygon and distance-to-a-stroke for the three icon painters.
   - `World/CitySeed.Default`: the game's world seed, the one place it is written.
   - `Character/Posing/SwordElbow.Bow`: where a sword arm's elbow goes so the forearm runs into the hand as the grip wants it (the wrist
     stays a wrist; docs/SWORD_GRIP_SYSTEM.md).
   - Already shared before: `TwoBoneIK`, `DeterministicRandom`, `CaptureGuard` (tests), the `*Strikes` keyframe tables and others.
2. **One topic per file.** A file past about a thousand lines usually holds several topics. Split it into partial files by topic, named
   `Class.Topic.cs` (see `PlayerLocomotion.*.cs`), and where a topic can stand on its own, make it a class of its own. Pure logic in plain
   classes, thin MonoBehaviours around it (CLAUDE.md).
3. **Folders by topic.** Each area (`Character`, `View`, `UI`, ...) is one assembly. Inside `Character`, the runtime scripts sit in topic
   folders; put a new script in the folder of its topic:

   | Folder | What is in it |
   |---|---|
   | `Player` | the player's own body: input, camera, identity, inventory |
   | `Movement` | moving the body: locomotion (and its partial files), climbing, vaulting, wall stick, grapple swing, lean and posture |
   | `Posing` | poses on top of the Animator: the animation driver, baked moves, the fist and sword pose drivers, IK, hand grips |
   | `Combat` | hits and weapons: melee, the soulslike sword, damage, blocking, lock-on, gun feel |
   | `Abilities` | skills, the hotbar and every spell or special move |
   | `Stats` | vitals, stat points, the stat catalog |
   | `Body` | the body's mesh, shape, paint, hair and ragdoll |
   | `NPC` | civilians, enemies' minds, story NPCs, the sparring dummy |
   | `Network` | everything that sends state to other players |
   | `Voice` | proximity voice: the codec, the hearing, playback (sending it is `Network`'s `NetworkedVoice`) |
   | `Interaction` | picking things up and graffiti |
   | `Story` | the intro cutscene, the tutorial's steps and words, and claiming buildings |

   `View`, `UI` and `Weapons` are sorted the same way (since 2026-10-20):

   | Area | Folders |
   |---|---|
   | `View` | `Streaming` (chunk streaming and mesh buffers), `Mobs` (the world's own enemies round the player: `MobSpawner`, `MobSpawnRules`), `Skyline`, `Buildings` (`CityMeshBuilder` and its partials, facades), `Streets` (`CitySurfaceBuilder`, props), `Glass`, `Interactables` (doors, switches, benches, books, the radio), `Lighting` (day and night, room lights, haze), `Weather` (rain, lightning, thunder), `Water` (water surfaces, splashes, rising water), `Tools` (the fly camera, the frame benchmark) |
   | `UI` | `Hud`, `Menus`, `Map`, `Title`, `Creator` (the character creator and ReDraw), `DevConsole`, `Story` (the tutorial's director); `UiKit` and `ButtonAction` at the root |
   | `Weapons` | `Melee`, `Drinks` (the PHIZZ can), `Firearms`, `Spells`, `Gear` (jetpack, parachute, grapple, spray can, flashlight), `Archery`, `Explosives` (the grenade and its explosion), `Thrown` (the boomerang), `NPC` (the enemies' minds); `GripMarker` at the root |

   Move a script together with its `.meta` file (`git mv` both, or move it in the Unity editor). The `.meta` holds the GUID that prefabs
   and scenes use to find the script; lose it and every reference to the script breaks. Namespaces stay one per assembly
   (`CityX.Character`), not one per folder: a namespace change would break saved data that names the type.
4. **No leftovers.** One-off diagnostics, "temporary" renders and commented-out code are not committed. A check worth keeping becomes a
   test. Dead code is deleted; git keeps it. (The old grid city in the `~` folders is set aside on purpose, at the owner's request, and stays.)
5. **Comments say why, briefly.** A comment explains why the code is the way it is now, in a few lines. The story of how it got there
   (the bug report, the owner's words, the dates, what was tried first) goes in the commit message and in `docs/<FEATURE>.md`.
6. **No compiler warnings.** A warning is either a bug or noise that hides the next bug. The project compiled with 31 warnings before
   the review and with none after; keep it at none.
7. **Tests and multiplayer as in CLAUDE.md.** Run EditMode and PlayMode before every commit.
8. **No garbage in a hot path.** Anything that runs every frame, or once per chunk, group, door or enemy every frame, must allocate nothing:
   no `new` collections or arrays, no closures, no LINQ, no string building, no `foreach` over an interface-typed collection, and no
   `Mathf.Max`/`Mathf.Min` with more than two arguments (the three-argument form is the `params` overload and allocates an array per call: it
   was three quarters of all garbage while sprinting until 2026-10-31, docs/PERFORMANCE.md). The profile report's "Top by GARBAGE" table
   (`ProfileReport`) says where the garbage is made; a test can hold a pure helper to zero bytes with `GC.GetAllocatedBytesForCurrentThread`
   (`SkylineTests.MeasuringAGroupsDistanceMakesNoGarbage`).
9. **No Vec2 operators in the plan's hottest loops.** The game runs on Mono, which turns each Vec2 `+`, `-`, `*` into a call that copies
   structs: `PlanMath.DistanceToSegment` was seven times slower than written out in doubles (docs/PERFORMANCE.md, 2026-11-26). A helper
   the plan asks millions of times is written out, doing the Vec2 form's sums in the same order (the city must stay the same to the last
   bit), with a test holding the two equal (`PlanMathFastPathTests`). Measure in Mono, not CoreCLR: `tools/plan_time_mono.ps1`.

## The 2026-10-17 review

What was changed. None of it changes what the game does. The full EditMode and PlayMode suites gave the same result for every test before
and after each step. The scenes and prefabs regenerated by the changed builders came out identical, compared object by object.

- **Removed about 2,000 lines of leftovers:** fourteen one-off "DIAGNOSTIC, NOT A PERMANENT TOOL" methods parked in
  `GrapplingHookBuilder` (from the grapple saga; most had nothing to do with the grapple), a "TEMPORARY DIAGNOSTIC" render, helpers nothing
  called any more, the lit tower crowns that went with the lit windows (2026-10-09), and constants nothing read.
- **Merged copy-pasted helpers** into the shared ones listed above. Before the review:
  - `FindDeep` had 12 copies, one of them under another name.
  - `EnsureEventSystem` had 15 copies.
  - `MakeText` had 7 copies and `Stretch` 5.
  - The arms-down pose had 3 copies.
  - The title screen's creator and ReDraw's had about 500 lines in common, copied by hand.
- **Fixed all 31 compiler warnings:**
  - 25 tests whose `[UnityTearDown]` method was named `TearDown`, which hid the base class's own method of that name.
  - 5 calls to an obsolete physics call (`Physics.BakeMesh` by instance ID; now by `EntityId`).
  - 1 unused local function.
- **Split the two biggest scripts into topic files:**
  - `PlayerLocomotion` went from 3,488 to about 1,560 lines, with ten new topic files: support hand, sword spin, climbing, fists, baked
    pose, arm-snap guard, seated, casting, gun aim, lean.
  - `PlayerInputReader` went from 2,142 to about 1,230 lines, with five topic files: spells, emotes, seats, combat, jump and dash.
- **Sorted `Character`'s 147 flat runtime scripts** (162 with the new topic files) into the eleven folders above.
- **The body-shape preview tool (`tools/bodyshape`) builds again.** Its stand-in for Unity's bone list had no finger bones since the hands
  got fingers.

## The 2026-10-20 review

Before the code went to GitHub for another programmer to read (owner: "clean up the code if needed, fix any amateur mistakes ... and
organize the code professionally ... and dont mess anything up"). Proven the same way as the first review: full suites before and after,
the regenerated scene and prefabs compared object by object.

- **Retired items removed** at the owner's request: the default sword, the farmer hat, the Midnight Patchwork Coat, the Black Puffer Jacket,
  with everything that served only them (`GeometryRig`, `BlackPufferDiagnostic`, `/jacket`).
- **The dev console's twelve copy-pasted item spawn blocks** and their prefab fields replaced by the generic `SpawnItem`; `DevConsoleView`
  (1,630 lines) split into partial files by topic: the console itself, `World`, `Player`, `Spawning`, `Tuning`.
- **`View`, `UI` and `Weapons` sorted into topic folders** (145 scripts moved with their `.meta` files; see rule 3).
- **23 stranded doc comments** (one member's comment left above another's, or above nothing after its member was deleted) moved to their
  members, merged or removed.
- **Unity's template leftovers** (`Readme.asset`, `TutorialInfo/`, an empty `SampleScene` that was still in the build) removed; a README
  added; the Blender scripts find the repository from their own place instead of one machine's folder.

Looked for and not found: logging in game code (two calls, both meant), silently swallowed exceptions, scene-wide searches every frame
(the few per-frame lookups are `Camera.main`, which Unity caches, or are throttled and cached).

## The 2026-11-14 review

Owner: "do a general overview of all existing code and refine it, simplify it, make everything run better ... remove redundancies ...
clean up anything that is poorly made or haphazardly put together that can create problems in the future". Proven as the others were:
the full PlayMode suite before (1,357 passed, none failed) and after, EditMode after each step (2,494 passed).

How it was looked for, since 220,000 lines cannot be read through in a sitting: a script that finds every block of seven or more lines
repeated in more than one place; one that finds members nothing else names; one that finds fields only ever written; one that lists what
each per-frame method (`Update`, `LateUpdate`, `FixedUpdate` and what they call) allocates or searches for; and the profiler, recorded in
the real city with the world's enemies alive (`MobCostBench`, `ProfileReport`).

- **Repeated code was under 2% of the game's code** (1,725 of 103,000 lines in blocks of seven or more), in a handful of clusters. Merged:
  - the eight network relays' session hook-up, into `SessionRelay`;
  - the nine pickups' E-to-take code, into `GroundPickup` (two bugs in it had each been fixed copy by copy);
  - thirteen views' pointer, input and camera hand-over, into `MenuFocus` / `FreedCursor`;
  - the pause menu's settings and the title screen's, two hand-kept copies of one page, into `SettingsPage` (733 lines to 502);
  - the three icon painters' geometry, into `IconGeometry`.
- **Leftovers removed:** 71 public members nothing in the project named, not even a test (373 lines: accessors, constants, whole
  methods); five fields that were written and never read; four doc comments stranded above the wrong member; stale claims in comments
  (the pause menu's "voice chat does not exist yet", the mob spawner's "the monster is not put into a session").
- **The world seed is one constant** (`CitySeed.Default`). It was written out in 117 files, to be changed together by hand at each reroll.
- **Less work a frame**, measured (docs/PERFORMANCE.md, 2026-11-14): the idle monster's every-frame scene search, the sword-combat lookup
  every swordless body made two or three times a frame, the ragdoll's fifteen arrays a physics step, the flashlight's search of the whole
  body every frame, the camera's array a frame. Ten enemies cost 2.95 ms of a frame before and 1.34 ms after.

Net: about 1,150 lines fewer in 230 files, with six small shared files added.

Looked at and left, each for a reason:

- **The old grid city** (`World/LegacyGrid~`, `Tests/LegacyGrid~`, and the editor diagnostics switched off behind
  `CITYX_LEGACY_GRID_DIAGNOSTICS`: `CityMassingCapture`, `StreetAnomalyScan`, a third of `GrapplingHookBuilder`), about 11,500 lines
  that are never compiled. Set aside at the owner's request ("just set them aside for now"); deleting it is theirs to say.
- **The editor builders' copy-paste** (below): their output is the scene and the prefabs, so merging them means regenerating and
  comparing those object by object. Not done in a pass that also changed runtime code.
- **The tests' own repetition** (2,675 of 82,000 lines; mostly the first ten lines of each `*Capture`).
- **Doors asleep when nobody is near** (67 to 83 `BuildingDoor.Update` calls a frame, 0.03 ms): a door can be opened by more than the
  player at its sill, so every way in would have to wake it. Not worth a door that fails to swing.
- **Posing skipped for bodies nobody can see**: the largest cost left in an enemy is its posing (0.08 ms a body), and most of the
  world's enemies are behind walls. It would change what is simulated, and a headless test sees nothing at all; a design decision.

## Known debts (not fixed yet)

These are bigger than a tidy-up, so each would go in as a careful change of its own, with tests.

- `PlayerInputReader.Update` is one method of about 900 lines, and `TickCombat` is about 250. They should be broken into steps. The two
  main files are still over a thousand lines (`PlayerLocomotion.cs` about 1,560, `PlayerInputReader.cs` about 1,230).
- `PlayerLocomotion` is still one class with many jobs, now spread over its topic files. Some topics (the support hand, the arm-snap guard,
  the lean) could become components or plain classes of their own.
- Copy-paste remains between:
  - some panel builders (`LoadoutPanelBuilder` and `StatsPanelBuilder`, `PauseMenuBuilder` and `TitleScreenBuilder`,
    `BookPanelBuilder` and `PlaceholderPanelBuilder`), and the first-open guard (`_opening`) in the five views that sit on their own
    hidden root (Stats, Loadout, Book, Placeholder, the Player fly-out)
  - the three icon painters (`MenuIcons`, `ToolIcons`, `SkillIcons`): each still has its own `Shape` with its own kinds; only the
    geometry under them is shared (`IconGeometry`)
  - the item models' "into the right hand" on equip (four items), and the creator's list of what a preview body is stripped of (three places)
  - the render tests (`*Capture`), which each carry their own copy of the shoot-and-save code
- Older files carry long history comments: bug reports, dates and the owner's words inside the code (their shouted "EXPLICIT ASK" /
  "EXPLICIT BUG REPORT" labels became plain "Requested:" / "Bug report:" on 2026-10-20). Rule 5 applies to new code; old comments get
  trimmed when the code around them is touched.
- Large files remain where splitting needs more than moving whole methods: `CityMeshBuilder.cs` (about 2,500 lines, already with 14
  partial topic files), `CityMassingSceneBuilder.cs` and `PlayerRigBuilder.cs` (about 2,100 and 2,000).

## Sources

- GitClear, [The AI code quality maintainability gap](https://www.gitclear.com/the_ai_code_quality_maintainability_gap) (2026)
- LeadDev, [Code maintainability plummets in the AI coding era](https://leaddev.com/ai/code-maintainability-plummets-in-the-ai-coding-era),
  [How AI-generated code accelerates technical debt](https://leaddev.com/technical-direction/how-ai-generated-code-accelerates-technical-debt)
- Shiplight, [AI-generated code has more bugs](https://www.shiplight.ai/blog/ai-generated-code-has-more-bugs) (citing CodeRabbit's report)
- Sawada et al., [To what extent does agent-generated code require maintenance?](https://arxiv.org/abs/2605.06464) (EASE 2026)
- DevClass, [AI is eroding code quality](https://devclass.com/2025/02/20/ai-is-eroding-code-quality-states-new-in-depth-report/)
- InfoWorld, [Why AI-generated code isn't good enough](https://www.infoworld.com/article/3844363/why-ai-generated-code-isnt-good-enough-and-how-it-will-get-better.html)
- Unity, [Organizing your project](https://unity.com/how-to/organizing-your-project) and
  [Programming best practices](https://docs.unity3d.com/6000.5/Documentation/Manual/programming-best-practices.html)
- Angry Shark Studio, [Unity component organization](https://www.angry-shark-studio.com/blog/unity-component-organization-monobehaviour-best-practices/)

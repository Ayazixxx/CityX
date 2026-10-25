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
   - `UI/Creator/CreatorKit`: the character creator's tool column, prompts and stage lights. The title screen's creator and ReDraw are both built
     from it, so they can no longer drift apart. They had drifted, which is how ReDraw's tools came to do nothing (fixed in fa8cefc).
   - `Character/Posing/StillPose`: fixed poses for a body shown without its Animator.
   - `Core/LitShader`: the shader every lit material is made with (CityX/Lit, cel shaded); never ask for URP's Lit by name.
   - `Character/Combat/SeeThroughMaterial.Apply`: a particle material made truly see-through (alpha or additive), so the cel lines skip it.
   - `Items/InertModel`: an item's model with nothing of the item acting (another player's copy holds one; a dead body drops one).
   - `Character/Interaction/InteractionReach.LookSource`, `CanReach`, `LocalPlayerBody` and `IsLeaving`: where a player looks from, whether a
     pickup is in reach, and which body entering or leaving its trigger is this machine's player.
   - `Tests/PlayMode/BuildingShapeCapture.Shoot` and `ShotsFolder`: render a camera to `Logs/CityShots/<name>.png` (20 captures use it).
   - `Tests/PlayMode/OpenRun.Face`: turn the player to look along an open, level run of street (the tests of moves that travel).
   - `Character/Editor/ClipAsset.Store`: save a generated animation clip into the one already at its path. Never delete and re-create an
     asset that prefabs or controllers hold: what still held the deleted one saves a blank.
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
   | `Interaction` | picking things up and graffiti |
   | `Story` | the intro cutscene and claiming buildings |

   `View`, `UI` and `Weapons` are sorted the same way (since 2026-10-20):

   | Area | Folders |
   |---|---|
   | `View` | `Streaming` (chunk streaming and mesh buffers), `Skyline`, `Buildings` (`CityMeshBuilder` and its partials, facades), `Streets` (`CitySurfaceBuilder`, props), `Glass`, `Interactables` (doors, switches, benches, books, the radio), `Lighting` (day and night, room lights, haze), `Weather` (rain, lightning, thunder), `Tools` (the fly camera, the frame benchmark) |
   | `UI` | `Hud`, `Menus`, `Map`, `Title`, `Creator` (the character creator and ReDraw), `DevConsole`; `UiKit` and `ButtonAction` at the root |
   | `Weapons` | `Melee`, `Firearms`, `Spells`, `Gear` (jetpack, parachute, grapple, spray can), `NPC` (the Shoota's and the swordsman's minds); `GripMarker` at the root |

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
  - the three icon painters (`MenuIcons`, `ToolIcons`, `SkillIcons`)
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

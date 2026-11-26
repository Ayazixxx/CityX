# Crowd test

Owner, 2026-10-27: "do the crowd test and make them have random various outfits and carrying various weapons as well, and make the players
the ones i have created already but a whole bunch of clones of them ... some doing spells, some doing attacks, some just running around.
start small and scale up see how much it can handle. if it starts getting laggy make less doing spells and attacks. no acid rain. no grow or
shrink spell." The plan it answers is docs/GAME_DESIGN.md, "Technical foundations still missing", item 5 (Crowds).

## How it runs

`Tests/PlayMode/CrowdCapture.cs` (Explicit, needs a graphics device):

```
./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CrowdCapture
```

- The real city, round the spawn, midday. The bodies are the joining player's body (`NetworkedPlayer.prefab`, which has every spell and
  item), its network parts taken off before it wakes, wearing the looks of the characters made in the creator on this machine (the local
  server's newest account list; 19 on 2026-10-27), each in random clothes (a hat on about a third) with a random weapon: the combat sword,
  the spike sword, the bat, the crowbar, the pistol, the AK-47, or bare fists.
- `Tests/PlayMode/CrowdMember.cs` drives each one as PlayerInputReader drives the player: it runs or walks between spots on the street. A
  fighter stops and attacks with what it holds (a sword's cut, a bat or fists, a shot). A caster throws a fireball, ice shards, a poison
  cloud, now and then Self Smite. No acid rain, no Grow or Shrink. Nobody can die (InfiniteHealth); blows and spells still land.
- Sizes 10, 25, 50, 75, 100, 150, 200, 300. At each, a frame is timed (150 frames after 90 to settle) first with everyone just running,
  then with fighting and casting; more than 8.3 ms over the empty street (half a 60 fps frame) counts as laggy, and fewer fight and cast
  from then on (60% doing something, then 30%, then nobody). It stops growing once the crowd alone costs over 60 ms a frame.
- Each timing is split by Unity's own profiler markers: scripts (Update), late scripts (LateUpdate: the procedural posing, IK), animation,
  skinning, physics, and the drawing (the crowd camera's render, waited for).
- Results: `Logs/CityShots/crowd-results.txt`, a picture per size (`crowd-<size>.png`), a street-level picture, and a 12 s video of 50 of
  them fighting and casting (`crowd.mp4`, written by Unity's own encoder, UnityEditor.Media.MediaEncoder) with one frame of it as
  `crowd-middle.png`.

## What it found (2026-10-27, Ryzen 7 5800H, RTX 3070 Laptop, in the editor)

| crowd | doing | frame ms | over the empty street | drawing | scripts | late scripts | physics |
|---|---|---|---|---|---|---|---|
| 0 | - | 15.0 (67 fps) | - | 7.8 | 3.2 | 0.3 | 0.4 |
| 10 | running | 25.5 (39 fps) | +10.5 | 13.6 | 4.1 | 1.1 | 0.7 |
| 10 | 60% fight/cast | 26.8 (37 fps) | +11.8 | 14.0 | 3.7 | 1.3 | 1.0 |
| 25 | running | 32.9 (30 fps) | +18.0 | 17.7 | 3.5 | 2.3 | 1.3 |
| 50 | running | 46.7 (21 fps) | +31.7 | 22.2 | 7.1 | 4.3 | 2.5 |
| 75 | running | 62.8 (16 fps) | +47.9 | 28.0 | 12.1 | 6.6 | 4.4 |
| 100 | running | 82.4 (12 fps) | +67.4 | 34.5 | 18.0 | 9.1 | 6.6 |

- **Each player costs about 0.65 ms a frame**: drawing about 0.27 ms (the 86k-triangle body, its clothes and weapon, drawn for the view,
  the cel black lines and the sun's shadows), scripts about 0.15 ms (its locomotion and the test's driver; Update), posing about 0.09 ms
  (LateUpdate: foot IK, hands, torso twist), physics about 0.06 ms (its controller and its ragdoll's colliders following the bones).
  Animation and skinning are small (under 0.02 ms each).
- **Fighting and casting cost almost nothing on top** (10 running 25.5 ms, 10 with 60% fighting and casting 26.8): the bodies are the cost,
  not the spells. Fewer of them fighting and casting did not make it smoother.
- The first ten cost more than the rest (about 1 ms each, then about 0.6): a fixed cost of having any crowd on screen at all.
- The numbers are on the slow side of the real game: the editor runs the scripts slower than a built game, and each frame waits for the
  picture to finish (the game overlaps the next frame's work with the graphics card's). What each player ADDS is the part to go by.

## What it means

The limit is the bodies themselves. Every player on screen is the full body, at full detail, posed and simulated, however far away. What
would raise it, in order of size (my suggestions, not decisions):

1. **Lighter bodies further away**: fewer triangles (a reduced body and its clothes), no black lines and no shadow past a distance, nothing
   at all past the view distance. The drawing is the biggest part.
2. **Cheaper far bodies**: no IK or procedural posing past a distance, the animator updated less often, the ragdoll's colliders asleep
   until needed.
3. **A crowd budget**, the plan in GAME_DESIGN.md: the nearest N players in full, lighter past that, the range shrinking as more are
   around.

Not measured yet: what the network sends and the server's load with that many players (the other half of the crowd test GAME_DESIGN.md
asks for).

## 2026-11-05: the test was showing an empty street; lighter bodies tried

**The crowd had been switched off.** The joining player's body has gained network parts since 2026-10-27
that the test did not take off, and with no session to spawn it in, the body was switched off as soon as it woke: every crowd size cost
about nothing and the pictures showed an empty pavement. The test now takes off every network part (`NetworkBehaviour`), not a list of
them. It also has a quick mode for a before and after: `CITYX_CROWD_STEPS=25,50` times only those sizes and makes no video
(`crowd-results-quick.txt`).

Measured again (same laptop, in the editor, everyone running):

| crowd | frame ms | over the empty street | drawing |
|---|---|---|---|
| 0 | 18.6 | - | 8.9 |
| 25 | 39.1 | +20.5 | 18.4 |
| 50 | 61.0 | +42.4 | 28.2 |

**Lighter bodies far off made no difference here, and were not kept.** Unity's Mesh LOD gave the body four lighter copies (86k down to
6.5k triangles; `MeshLodUtility.GenerateMeshLods` keeps the whole body's triangles and vertices as they are, so the garments' hidden-skin
masks still fit, and the cut under clothes can be carried into each copy). 50 players then cost +39.9 ms against +42.4: within the noise
between runs. At the distances a crowd stands at, a body's cost is not its triangles but its draws: about ten pieces (skin, head, hair,
garments, shoes, weapon), each drawn for the picture and again for the sun's shadows, plus its scripts and posing.

**The crowd's shadows** are about a fifth of it: with no shadows cast by the crowd, 50 players cost +33.7 ms (drawing 24.1 against 28.2).

What would help next (my suggestions, not decisions): fewer pieces per body (the head, hair and shoes merged into the body's own draw),
no shadow from a body past some distance, and far bodies posed and animated less often (`PlayerLocomotion.LateUpdate`, the animator).

## 2026-11-25: the crowd's level of detail

Owner: "something that will not tank performance in large crowds of people remember there is a hub", then "work on that until we have made a
significant improvement".

### Measuring a hub's crowd, not a crowd of players' own bodies

The test stepped every body as a player's own body is stepped (`PlayerLocomotion.Tick`: the controller, the ground, the walls). In a session
another player's body is a copy: placed by the network (`NetworkedBodyMotion`), its walk and run fed the owner's speeds
(`NetworkedAnimatorSync`), never stepped; only its posing runs (`PlayerLocomotion.LateUpdate`). `CITYX_CROWD_AS_COPIES=1` moves the crowd
that way (`CrowdMember.AsCopy`), which is what a hub is. Two more tools, run by name with -Graphics:

- `CrowdCapture.BreakDownWhatACopyCosts`: each kind of script on the copies switched off in turn (on, off, on: each "off" against the mean
  of the two around it, so the laptop's drift between timings cancels), then their shadows, their drawing, their animators.
  `crowd-breakdown.txt`.
- `CrowdCapture.CompareTheCrowdsDetail`: the level of detail on and off in turns in one run (four rounds each), the same with the camera
  turned away, where the drawing goes, and what a copy carries. `crowd-detail-ab.txt`. Single runs differ by 10 ms or more from one to the
  next on this laptop; only a comparison inside one run can be trusted for a small change.

### What a copy cost (before; 30 copies, everyone running, in the editor)

| what | for the 30 |
|---|---|
| `GarmentHelperBones`: a coat's skirt kept off the legs (`SkirtCloth`, `SkirtClearance`: every skirt point against the legs, up to 4 rounds of 8 passes a frame) | 8.5 ms |
| the crowd's shadows | 11.5 ms of the frame |
| `PlayerLocomotion`'s posing (of it, the foot placement was 48 of about 75 microseconds a body) | 4.3 ms |
| the animators (mostly what the bones then move: the skinning, the posing) | 14 ms of the frame |

Every other script was within the noise (about 0.06 ms a copy for all of them together: some fifty abilities each looking once a frame).

### What was done

- **`CrowdDetail`** (`Character/Network`), put on every copy of another player by `NetworkedPlayerIdentity`, never on this machine's own
  body. A few times a second the crowd is ranked by distance from the camera, and each body gets a tier (`CrowdTiers`):
  - **Full**: within 15 m and among the nearest 10. Everything, as before.
  - **Light**: the rest of the crowd on the screen. No shadow; no coat skirt kept off the legs (`GarmentHelperBones`); the feet and fingers as
    the clip puts them (`PlayerLocomotion.Crowd.cs`).
  - **Unseen**: off the screen. No shadow, nothing posed (only what counts time: a blow's freeze, the crouch), no garment helpers set; its
    animator no longer moves the bones while none of it is seen (`AnimatorCullingMode.CullUpdateTransforms`).
  - No game camera, or nothing drawn at all (a server, a run with no graphics): every body in full.
  - A copy's skinned meshes keep no last-frame shape for motion vectors: nothing in the game draws with them (SMAA, motion blur at 0), and
    keeping them cost 3.9 ms a frame for 50.
- **The body cut keeps only the points it draws** (`BodyCover.Compact`): the graphics card bends every point of a skinned mesh, drawn or
  not, and a dressed body hides most of its skin (a body in a T-shirt and jeans: 28,500 points of 43,500). For every body, the player's own
  too. What needs the whole body asks `WornBodyCover.Whole` (the cape's fit now does).

### After (same laptop, in the editor, as copies)

| crowd | before | after |
|---|---|---|
| 25 | +34.9 ms over the empty street (20 fps) | +22.2 ms (25 fps) |
| 50 | +64.9 ms (13 fps) | +40.4 ms (17 fps) |
| 100 | (the test stopped at 50) | +66.2 ms (12 fps) |

About 1.3 ms a copy before, about 0.66 ms after. In one run, 50 copies with the level of detail on and off in turns: 53 ms against 74 on the
screen; with the camera turned away from them (as most of a hub is at any moment), 30 to 36 ms against 55. With no body in full at all, 47.6
ms: the nearest in full (shadows, planted feet) cost about 0.35 ms each more, hence 10 of them.

Where the rest goes, for 50 on the screen: the drawing (about 0.4 ms a copy: its nine or so pieces, each drawn for the picture and its depth;
of it, hiding the skin saved 1 ms, the clothes 1.7, the rigid pieces (head, hair, shoes, held things, hats) 3.2, so the graphics card's
bending is not the cost), the scripts (about 0.06 ms a copy, mostly the editor's own cost of calling some fifty idle `Update`s, far less in
a built game), the physics (about 0.04 ms: the hit shapes, kept because blows and shots land on copies).

Next, if the hub needs more: fewer pieces a body (the rigid pieces merged into one draw), the crowd budget hiding the furthest when very many
are on the screen, abilities that sleep when idle instead of looking every frame, and a measurement in a built game (the editor overstates
the scripts).

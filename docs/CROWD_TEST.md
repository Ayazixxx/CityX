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

**The crowd had been switched off.** The joining player's body has gained network parts since 2026-10-27 (ragdoll, fire, voice and more)
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

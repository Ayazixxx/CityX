# The Wraith

The first real enemy (the swordsman, the Shoota and the Crimson Claw are testers): a low-level ghost that floats about the streets. Owner,
2026-10-26: "make a new enemy called "Wraith" its a low level enemies that dies very quickly but sneaks up on you and tries to bite you and
does a lot of damage if successful. we will do the sneak up bite function later for now just make the wraith and try to animate his
movement the best you can. make it so the top half of him is the same dimensions as the playermodel ... his legs wisp away into nothing
like a ghost. he is also somewhat transparent and glowing because he is a ghost." Then: "dont make the toothy smile il model it myself in
meshy and send you the head later just do plain no facial details head", and "make him float around".

`/spawn wraith` puts one in front of you (`/spawn wraith 5` a row of them).

## What it is

| Piece | Where | What |
|---|---|---|
| Body | `Character/Editor/WraithBuilder.cs` (menu CityX > Rebuild Wraith, and with the scene) | the player's own fused body mesh cut off at the hips (`CutY` 0.75 m) with a vertex alpha (`BodyAlphaAt`): the torso solid from the chest (1.55 m) down to where it turns to smoke, nothing at the cut; the arms solid to just over the hands (1.05 m), hanging beside the smoke. The body's own bone weights, skinned to the player's skeleton put into its arms-down rest |
| Head | the player's plain skull (`Head.asset`) under its own object "Head" on the head bone, with a "Face" on it | the face built in code from the owner's picture (below) |
| Face | `WraithBuilder.BuildFace`: "Face" under the skull, holding "EyeLeft", "EyeRight" and "Jaw" > "Teeth" | two solid red eyes (radius 5.5 cm) sunk 2 cm into the skull, 21 cm out either side and 8.75 cm up from its centre; no mouth line (owner: "get rid of the black bar above the teeth"); one row of nine sharp white teeth rooted on a lip 6.25 cm below the centre, 44 degrees round either side, hanging down and leaning a little outward with their tips curled back in, the middle ones 32 cm long (to the chin), the end ones 18. Opaque and lit (CityX/Lit), so cel shaded and outlined like everything solid; no shadows. The teeth's roots stand just clear of the skull: inside it, the see-through skull drew over them and greyed them as if behind glass. Dead, the face shrinks away into the skull as the body fades |
| Wisps | eight veils made in the builder, hung on the hips | from just off the torso's surface, each starting where the torso is already turning to smoke, 70% as solid as it is there (the highest at 1.4 m, every third one lower), billowing out a little, then twisting a third of a turn down and inward to a point just over the street; 38 cm wide at the top (overlapping), thin at the tip, fading out along their length and feathered to nothing at their sides (the shader, by uv.x; cut flat they showed as paper); the shader sways them |
| Surface | `View/Shaders/CityXGhost.shader`, two materials (`WraithGhost`, `WraithWisps`) | see-through, brighter and more solid at the edges (Fresnel), smoke drifting up inside (value noise); where the vertex alpha is under 1 the surface is **eroded into streaks** (tall noise against a threshold that rises as the alpha falls, scrolling upward), so the torso and the veils fray into tendrils of smoke rather than fading evenly; white with the faintest cold cast; unlit, drawn after the cel look's black lines so nothing outlines it, no shadow; `_Fade` and `_Flash` set live |
| Glow | a point light at its chest | a pool of pale blue on the street at night |
| Collider | a capsule, hips to the top of the head | what blows and bullets meet |

### What the research said (2026-10-26, owner: "research how games do ghost like enemies like this")

The first build faded the body evenly at the hips with twisted ribbons under it; the owner: "that just looks like a tornado sitting beneath
him, make his body turn to wisps and fade away". No shipped game's ghost has a public breakdown, but the VFX community's recipe is
consistent, and it is what the second build does:

- A **Fresnel rim** (brighter, more solid edges, see-through middle) is the base of every ghost shader ([godotshaders](https://godotshaders.com/shader/simple-fresnel-transparency/),
  [polycount](https://polycount.com/discussion/comment/937472)).
- A ghost's body **dissolves**: noise against a threshold, then alpha (Mirza Beig's [dissolve tutorial](https://mirzabeig.substack.com/p/unity-tutorial-dissolve-shader-part-1)),
  run along a height so it erodes from the hips down (Daniel Ilett's [world-space-height dissolve](https://github.com/daniel-ilett/dissolve-urp)).
  An even fade reads as a cut-off; erosion reads as smoke.
- The trails are **sheets of smoke playing off the body's own shape** with scrolling, warped noise, not thin ribbons: thin ribbon
  emitters were judged unconvincing ([realtimevfx, Sea of Thieves thread](https://realtimevfx.com/t/ue4-advice-on-how-to-achieve-sea-of-thieves-ghost-effect/15282),
  [realtimevfx, smoke ribbons](https://realtimevfx.com/t/cigarette-smoke-trail-vfx-ue4-question/14370)).
- Later options seen there: a screen-space afterimage trail (Sea of Thieves), drifting particles off the body (Skyrim's membrane and
  particle shaders, [Creation Kit wiki](https://ck.uesp.net/wiki/EffectShader)).

## How it moves (`Character/NPC/WraithDrift.cs`, pure)

- It drifts toward a heading for 4-9 s at 1.5 m/s, then lingers 2-5 s, hanging in the air and turning to look somewhere new, then drifts
  again. It turns through its headings at 45 degrees a second (a curving path) and its speed eases in and out (it glides). Past 45 m from
  where it was born it heads home.
- It hovers with its root 0.3 m over the real ground beneath it (a ray down each frame), so its hips are at 1.2 m and its head a little
  over a player's, and bobs 12 cm up and down every 2.8 s.
- `NpcSteering` steers it round walls, as the civilians are.

## How it holds itself (`Character/NPC/WraithPose.cs`, pure)

No Animator and no clips: `Wraith.LateUpdate` sets every bone from a few slow sines over the body's rest, each a turn about the body's own
axes.

- The trunk leans 12 degrees into its drift and banks 6 degrees into a turn.
- The arms hang and swing like pendulums (7 degrees either way, every 3.1 s), the two out of step, each joint lagging the one above it;
  drifting, they trail 10 degrees back.
- The head wanders idly, and turns to look at a player within 10 m (up to 70 degrees round, 35 up or down): the start of its sneaking up.
- Hit, it jerks 15 degrees back and its arms fly up 25, settling over 0.9 s, and it flashes white.

## Fighting it (`Character/NPC/Wraith.cs`)

- 18 health: one pistol round or two punches ("dies very quickly"). Killing it is worth 15 XP.
- It does not bleed: `IBloodless`, a marker `MeleeAttacker` and the firearms check before spawning blood.
- Dead, it fades away over 1.1 s, rising, and is gone.
- Its sneaking up and its bite are not built yet (the owner: later).
- Local for now, as every NPC is: it exists only on the machine that spawned it.

## Tests

- `WraithDriftTests` (EditMode, pure): level unit headings turned no faster than it turns; drifts and lingers in turn, gliding between
  them; home from far off; the bob; the same seed drifts the same way.
- `WraithPoseTests` (EditMode, pure): the arms' swing, out of step, lagging down the arm; the lean and the trail; the bank; the look and its
  limits; the flinch.
- `WraithTests` (EditMode, the prefab): the body from the hips up fading at the cut with its skinning; the plain skull on the head bone;
  the wisps from the hips to a point, swaying; every surface on the ghost shader, transparent, shadowless; two punches kill it, XP, no
  blood; dead it fades and rises; posed, its arms hang.
- `WraithPlayTests`: it hovers at its height and bobs; floats about and stays near home; turns its head to the player; killed it fades and
  is gone; `/spawn wraith` through the real console puts one in the city.
- `WraithCapture` (Explicit, `-Graphics`): by day beside the player, a strip of eight moments floating, by night, and hit.
  `Logs/CityShots/wraith-*.png`.

## The bite to come (owner: "they sink their teeth into the player ... plan for that")

What is in place for it:

- **The jaw pivot** (`Wraith.Jaw`): at the middle of the lip, on the skull's surface; the teeth hang under it in the skull's frame. A bite
  swings the jaw (a turn about the skull's right axis: up to open, snapping down to close) and the pose can add a `Jaw` turn next to the
  head's in `WraithPose.Result`.
- **The head** is its own bone, already posed by `WraithPose` (it looks at the player): the lunge is a pitch and a push of the head and
  spine toward the player over a few frames, the way the Crimson Claw's swing is phased (wind-up, a tell, the snap, recovery), with the
  teeth's reach checked against the player's head during the snap.
- **The face shrinks** with the fade, so a Wraith killed mid-bite leaves nothing behind.
- Still to decide: a second (lower) row of teeth that appears for the bite, the sneak (quiet drift up behind the player, out of their
  view), the damage ("a lot") and whether it latches on.

## Known

- The body is the player's full 72k-triangle upper half, with no LOD: fine for a few, not a crowd.
- Several spawned together each haunt their own way (the seed comes from where each is spawned) but know nothing of each other yet
  (no `EnemyPack`: nothing to take turns at until the bite).

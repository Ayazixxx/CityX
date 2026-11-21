# The Midnight Coat

A long black shearling coat with white fur trim, worn as a top. The owner asked on 2026-10-19: "put it in the game and fit it to the character",
named "Midnight Coat". Spawn it with `/spawn midnight_coat`, put it on with `/equip midnight_coat` or by picking it up (E).

## Where it comes from

- Meshy's "Midnight Shearling Coat": the mannequin was cut away (head, hands, legs; the two fur holes the first cut made were fixed) and it was
  reduced to 14,876 triangles with one 1024 texture.
- **The owner fitted it by hand** in Blender on this game's body (arms hanging, the pose every garment is bound in), with Claude's help:
  - the shoulders lifted just enough to cover the body's;
  - each cuff ends on the top of the thumb and its fur rolls back up into the sleeve like a donut, closing round the wrist;
  - the right cuff is a mirror copy of the left;
  - the spikes the shoulder lift had pushed out of the back of the collar were smoothed away.
- The texture is one 512 JPEG (52 KB). The whole source, `CityX/SourceArt/MidnightCoat/MidnightCoat.glb`, is 488 KB.

## How it is built

| Step | Where | What |
|---|---|---|
| Skinning | `SourceArt/MidnightCoat/coat_build.py` (Blender, headless), its shared steps in `SourceArt/garment_rig.py` (the T-Shirt uses them too) | the coat is not moved; every point takes the weights of the nearest skin, then the skirt, armpits and collar are reworked (below) |
| Mesh | `Wearables/Editor/BlenderGarmentMesh.cs` | the JSON into a skinned mesh bound to the arms-down skeleton plus the helper bones |
| Body under it | `Wearables/Editor/BodyUnderGarment.cs`, `LongCoatBody.cs` (shared with the Fur Hooded Parka) | which of the body's triangles the coat hides |
| Item | `MidnightCoatBuilder`, `MidnightCoat`, `ItemRegistry.MidnightCoat` | the prefab, built with the scene |

To refit it: move it in Blender, export over `MidnightCoat.glb`, then run the script and rebuild:

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/MidnightCoat/coat_build.py -- CityX/SourceArt/MidnightCoat/MidnightCoat.glb CityX/Assets/CityX/Wearables/Models/MidnightCoat.json CityX/Assets/CityX/Wearables/MidnightCoat.jpg
```

## Moving with the body: what games do, and what this does

Research (2026-10-19). The owner asked "research how other games do coats like this":

- **Skirt bones driven by the thighs.** Games give a long skirt or coat its own bones that turn with the leg, so the cloth goes where the knee
  goes, without paying for a cloth simulation. A Japanese patent describes skirt bones controlled by the thigh bone's movement for this reason.
- **Helper bones at the armpit.** Riggers add helper bones for areas that deform a lot (the armpit, the shoulders) and smooth the weights there.
  The blend between torso and arm alone pinches and stretches.
- **Hiding the body under clothes.** What is not drawn cannot poke through.
- **Cloth simulation** is the expensive option. It is not used here: this project's joint-physics attempts failed (the grapple), and many players
  will wear these.

`GarmentHelperBones` adds seven helper bones to the body while a garment that uses them is worn. They are set every frame after the body's pose
is final.

| Bone | Follows | Fixes |
|---|---|---|
| `skirt_front_l/r` | 95% of its thigh's swing when the knee comes forward, 15% when the leg goes back | the knee coming through the front panel |
| `skirt_back_l/r` | the other way round | the back panel; the skirt does not split between the legs |
| `shoulder_l/r` | half the upper arm's swing | the armpit pinching and stretching |
| `collar` | turns with the neck, goes where the head goes | the sprint clip lowers the head 3.4 cm along the neck; a collar on the neck alone let the head sink into it |

In `coat_build.py`:

- **Skirt:** below the hips (from 0.95 m down to 0.72 m) the skirt moves from the hips onto the skirt bones. The front faces go to the front
  bones, the back faces to the back ones, the sides to both. The body's own weights had given the front panels mostly to the hips: the knee
  went through them.
- **Armpits:** within 0.24 m of each shoulder joint, and no higher than 4 cm above it, the weights are smoothed. What torso and arm share there
  goes to the shoulder bone. Above that line is the collar's: the smoothing first reached the collar's back corners, and an arm swing lifted
  them 12.6 cm into the head.
- **Collar:** from the shoulders' top up (0.12 m lower under the chin, where the head dips forward), the collar bone takes over.

## Hiding the body under the coat

`BodyUnderGarment` hides two kinds of body triangle.

- **Always hidden:** every triangle skinned at least half to the torso (spine) or an upper arm, so the upper arms up to the elbow. The owner
  asked on 2026-10-19: "make the torso and top half of arms up until the elbow invisible while wearing the midnight coat. there are little
  bits that poke through". The exception is the coat's open front, from the waist to the collar: there the torso is hidden only where the
  coat covers it. The owner asked for "any amount that is in view regularly" at the top and toward the bottom, so the throat under the
  collar and the belly at the waist show.
- **Hidden where the coat covers them:** the neck and the forearms, when rays out along the triangle's normal and 35 degrees round it all hit
  the coat within 0.3 m.

Behind the body the hips and the top of the thighs, down to 0.75 m, are always hidden too: the back was clipping through in sword swings. Elsewhere the hips and legs are never hidden: the skirt swings off them and the legs come out of the slit. The coat hides 23,205 of the body's 85,838
triangles. It stores them as one bit each (about 11 KB in the prefab, not a 6.4 MB copy of the body). `BodyCover` builds the trimmed body once, when the coat goes on. A body with a different
triangle count is drawn whole, so a stale list never cuts the wrong holes.

## Hair goes down inside the collar (2026-11-15)

The fur collar stands up round the lower head: the wearer's hair below its top is tucked in under it on the graphics card, rather than coming out
through the coat's back (owner: "the back spikes are clipping through"). The coat says it has such a collar (`TucksHairUnderCollar`), and its rim is
read off its own mesh when it goes on (`GarmentCollar`). The how and the why: `docs/HAIR.md`, "Tucked into a high collar".

## Tests

- `LongCoatTests` (EditMode, shared with the Fur Hooded Parka): a top named "Midnight Coat"; within 15k triangles and one 512 map; never on the head, hands or shins, every
  weight adding up; the skirt on the skirt bones; hides part of the body; hangs from the shoulders to below the hips.
- `GarmentHelperPoseTests` (EditMode): helper bones at rest, their share of a swing, the skirt panels' direction rule.
- `LongCoatPlayTests` (PlayMode, shared with the parka), on the real player through a sprint:
  - the body is hidden while worn and given back when taken off;
  - the head goes at most 2 cm deeper into the collar than standing (measured: 0.6 cm);
  - no part of the coat comes inside a knee that comes forward (measured: 12.4 cm from the joint, the knee's skin is 12 cm out).
- `LongCoatCapture` (PlayMode, Explicit, `-Graphics`): standing, the cuff, walking, and four moments of a sprint (side, front, armpit,
  collar), to `Logs/CityShots/midnightcoat-*.png`.

## Known limits

- **Other players do not see it** yet. No clothing is shown on copies (`docs/MULTIPLAYER_STATE.md`); only the Knight Helmet is.
- The legs show through the open front when running. That is the coat's design.

Sources: [patent on thigh-driven skirt bones](https://patents.justia.com/patent/20140035933),
[Polycount: shoulder rigging and helper bones](https://polycount.com/discussion/118989/shoulder-rigging),
[Reallusion forum: smoothing armpit weights on a jacket](https://forum.reallusion.com/Topic463243.aspx),
[Blender Artists: rigging a long coat](https://blenderartists.org/t/rigging-charactor-with-a-long-coat-question/510291).

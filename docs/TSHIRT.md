# The T-Shirt

A plain white short-sleeved tee, worn as a top. The owner asked on 2026-10-19: "put this in as a wearable top called T-Shirt", with the
body separated from the shirt, "the lump and wrinks at the front" flattened "so we can cleanly put a various textures and logos on here for a
bunch of different shirts", and fewer faces. Then: "the fabric should wrap around the bicep tighter".

Spawn it with `/spawn tshirt`, put it on with `/equip tshirt` (also `shirt`, `t_shirt`) or by picking it up (E).

## Where it comes from

Meshy's "Blank Slate Character" (`Meshy_AI_Blank_Slate_Character_1020040122_generate.fbx`, in the owner's Downloads, 5 MB, not kept in the repo).
It is a ball-headed mannequin in a tee: one fused 180,738-triangle surface with no UVs, 1.9 m tall. Scaled to our body's 2.6 m and centred on
its head, it lies within 1-3 cm of our body everywhere, so nothing had to be moved by hand.

`CityX/SourceArt/TShirt/tshirt_build.py` (Blender, headless) makes everything from it:

| Step | What |
|---|---|
| Cut | Roughly by zones, then cleanly: the hem along one level (its fold is at 0.922-0.928 m all round), each cuff on a plane (its fold is further down the arm on the inside), the neckline lower in front and behind than at the sides. Every new edge point lies on the line, so the edges are straight and the hem and cuff bands stay. The small holes where Meshy's arms touched the sides are closed. |
| Sleeves | Pulled in toward each arm: they stood 4-8 cm off it like a bell. The smooth looseness is cut to 30%; the folds and the cuff's roll keep 80%. |
| Front and back | How far the cloth stands off the body is evened out (smoothed 200 times), never closer than 1.5 cm: no lumps or wrinkles under a design, and the chest no longer shows through. Meshy's folds stay at the sides, the armpits and the hem. |
| Reduced | to about 5,800 triangles; the seams then add about 400: 6,192 in all, inside the budget for a shirt (3-8k, at most 12k). A seam or cut line passing very near a corner moves the corner onto it rather than leaving a sliver face (they flicker). `tools/model_qa.py` passes it on every check. |
| Unwrapped | Four pieces at fixed places on the texture, each cut along a smooth seam line: down the sides and over the shoulders, round each armhole, under each sleeve. Every piece is upright, not mirrored, and at one scale (486 texture pixels per metre on a 1024 map). |
| Skinned | As the Midnight Coat is (`SourceArt/garment_rig.py`, now shared by both): the body's nearest weights, the hem on the hips and never a leg, the armpits smoothed onto the half-turning shoulder bones, the collar band on the collar bone (the sprint lowers the head 3.4 cm along the neck). |

To rebuild after changing the script:

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/TShirt/tshirt_build.py -- <the Meshy .fbx> CityX/Assets/CityX/Wearables/Models/TShirt.json
```

then rebuild the scene (`CityMassingSceneBuilder`), which runs `TShirtBuilder`. A third argument (a folder) also writes preview renders there.

## Making a new shirt design

`CityX/SourceArt/TShirt/tshirt_uv_template.png` (1024 x 1024) is the layout every design is drawn on:

| Part of the texture | Piece |
|---|---|
| top left (u 0-0.5, v 0.4-1) | the front, seen from in front: the wearer's left on the image's right |
| top right (u 0.5-1, v 0.4-1) | the back, seen from behind |
| bottom left | the right sleeve |
| bottom right | the left sleeve |

The grey grid is 10 cm of shirt per square, and the crosses mark the middle of the chest and of the back at the usual logo height. Paint
anywhere inside the outlines, and a little over them so the seams do not show a white line. `tshirt_test_design.png` is a check design (a dark
grid and a red disc on each cross): `TShirtCapture` wears it, and it lands round and centred on the chest and the back.

A new shirt is then one texture plus one item, with the same mesh. That step (which item carries which texture) is not built yet.

## In the game

| Part | Where |
|---|---|
| Item | `ItemRegistry.TShirt` (`t_shirt`, "T-Shirt", Torso slot), `TShirt : SkinnedGarment` |
| Prefab | `TShirtBuilder` (`CityX/Rebuild T-Shirt`), built with the scene: plain off-white cotton, matte, no texture |
| Body under it | the torso always hidden; the hips, upper arms and neck hidden where the shirt covers them (`BodyUnderGarment`): the upper arm below the short sleeve stays |
| Other players | not shown on copies, like all clothing (`docs/MULTIPLAYER_STATE.md`) |
| Tests | `TShirtTests` (registry, budget, weights, shape, a design's front on the chest and back on the back, upright and not mirrored), `TShirtPlayTests` (the body hidden and given back; the head never sinks into the collar in a sprint), `TShirtCapture` (renders, plain and with the test design) |

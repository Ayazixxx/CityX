# The T-Shirt

A plain white short-sleeved tee, worn as a top. The owner asked on 2026-10-19: "put this in as a wearable top called T-Shirt", with the
body separated from the shirt, "the lump and wrinks at the front" flattened "so we can cleanly put a various textures and logos on here for a
bunch of different shirts", and fewer faces. Then: "the fabric should wrap around the bicep tighter". Then, unhappy with the neckline of the
first cut, the owner cut the shirt out by hand: "i have my own open i selected more precisely please lower the face count and fill the missing
space on the collar from the meshy models head being removed", with the flattening and the snug sleeves as before; then "make it rounded
inward and evenly dont make it raise around the shoulders", "i want there to be a visible collar though", and "the shirt stretches around
the armpits please detatch the sleeve from the body".

Spawn it with `/spawn tshirt`, put it on with `/equip tshirt` (also `shirt`, `t_shirt`) or by picking it up (E).

## Where it comes from

Meshy's "Blank Slate Character" (`Meshy_AI_Blank_Slate_Character_1020040122_generate.fbx`, in the owner's Downloads, 5 MB, not kept in the repo)
is a ball-headed mannequin in a tee: one fused 180,738-triangle surface with no UVs, 1.9 m tall. **The owner cut the shirt out of it by hand**
in Blender: `CityX/SourceArt/TShirt/TShirtCut.glb`, 8,550 triangles, still where Meshy's figure stood. Scaled as the whole figure was (feet to
our body's 2.6 m, its head over ours), it lies within 1-3 cm of our body everywhere, so nothing had to be moved.

`CityX/SourceArt/TShirt/tshirt_build.py` (Blender, headless) makes everything from it:

| Step | What |
|---|---|
| Neck | Meshy's own collar is cut away along an oval round the neck (0.28 m to the sides, 0.2 m in front and behind): where the mannequin's neck had been joined to it, it was notched and folded over itself, and it stood up at the shoulders (owner: "collar looks like shit ... make it rounded inward and evenly dont make it raise around the shoulders", then "i want there to be a visible collar though"). |
| Collar | After the reduction, a crew neck's band is sewn round the oval, the same all the way round: its heights put on a smooth curve, then one cross-section swept along it (a groove at the seam, the rib raised 5 mm off the cloth and 2.6 cm wide, its edge rounding over and down into the neck). It leans up toward the neck by 40 degrees in front and behind and only 20 at the shoulders, so it never stands up beside the head, and nowhere sits closer than 8 mm to the body. |
| Sleeves | Pulled in toward each arm: they stood 4-8 cm off it like a bell. The smooth looseness is cut to 30%; the folds and the cuff's roll keep 80%. |
| Front and back | How far the cloth stands off the body is evened out (smoothed 200 times), never closer than 1.5 cm: no lumps or wrinkles under a design, and the chest no longer shows through. Cloth still inside the body is pushed out in small rounds spread over its neighbours. Meshy's folds stay at the sides, the armpits and the hem. |
| Reduced | to 5,500 triangles; the collar, the seams and the armholes then add about 2,200: 7,689 in all, inside the budget for a shirt (3-8k, at most 12k). A point standing out of the surface is put back level with its neighbours (moved only out of the surface: moved to their middle, a point of a long thin triangle once folded the front into a pit). A seam line passing very near a corner moves the corner onto it rather than leaving a sliver face (they flicker). |
| Unwrapped | Four pieces at fixed places on the texture, cut like a real tee's pattern along smooth seam lines: down the sides and over the shoulders, round each armhole, under each sleeve. Every piece is upright, not mirrored, and at one scale (423 texture pixels per metre on a 1024 map). |
| Armholes | Owner: "the shirt stretches around the armpits please detatch the sleeve from the body". Each sleeve is cut loose from the shirt's body along its armhole. A sleeve is the cloth nearer the arm than the torso and within 14 cm of the arm's axis (the sleeve lies within 12.5 cm of it, the shirt's side 16.4 cm off it): Meshy had webbed cloth between the arm and the ribs down to 1.4 m, and counted as sleeve, that flap hung from the arm and swung out 25 cm. The cuffs stay whole on the sleeves. Both edges of each armhole are turned in, like a seam allowance, each folding back under its own cloth (4 cm for the sleeve, 1.5 cm for the body). |
| Skinned | From the body under it (`SourceArt/garment_rig.py`, shared with the Midnight Coat). The shirt's body follows the torso and never an arm; the hem the hips, never a leg. Each sleeve follows its arm as the skin under it does (near the cuff partly the forearm: the elbow bends there, and on the upper arm alone the skin came through the back of the sleeve). Both edges of each armhole follow the half-turning shoulder bone wholly, so the seam can never open (skinned apart, the arm showed through a slit even standing); the sleeve goes over to its arm 8 cm down, the body back to the torso 5 cm in, which is all the cloth that bends. The collar's sides follow the neck by 60% (on the torso alone, the head sank 1.8 cm into it in a sprint). |

To rebuild after changing the cut or the script:

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/TShirt/tshirt_build.py -- CityX/SourceArt/TShirt/TShirtCut.glb CityX/Assets/CityX/Wearables/Models/TShirt.json
```

then rebuild the scene (`CityMassingSceneBuilder`), which runs `TShirtBuilder`. A third argument (a folder) also writes preview renders and a
`tshirt.glb` for the model check there. `tools/model_qa.py` passes it on every check but one, meant: "broken edges" lists the armhole
seams (welded by place, the sleeve's edge, the body's and their two turned-in bands meet on one line).

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
| Body under it | the torso and upper arms always hidden (`BodyUnderGarment`), except the skin the shirt leaves bare: inside the crew neck (an oval just inside the band, drawn whole along a clean line) and the upper arm below the cuffs; the hips hidden where the shirt covers them. Hidden only where covered, the upper arms came through the cut-loose armholes. |
| Other players | not shown on copies, like all clothing (`docs/MULTIPLAYER_STATE.md`) |
| Tests | `TShirtTests` (registry, budget, weights, shape, each sleeve on its arm and the body on no arm, no seam ever opens, a design's front on the chest and back on the back, upright and not mirrored), `TShirtPlayTests` (the body hidden and given back; the head never sinks into the collar in a sprint), `TShirtCapture` (renders, plain and with the test design, with close-ups of the collar, a sleeve and the back of an armhole) |

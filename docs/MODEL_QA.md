# Model QA

The owner asked on 2026-10-19: "write your own QA check for imported models", and then "make the QA check work in hand with my descriptions
so il point out if there are holes i dont want filled".

`tools/model_qa.py` checks a model before it goes into the game. Run it on every model that comes in (a Meshy zip, a glb the owner fitted, a
bake): it catches the faults this project has met before the owner has to spot them.

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python tools/model_qa.py -- <model> --kind coat --render qa.png
```

## The checks

| Check | Catches | Met in |
|---|---|---|
| triangles | over the budget for its kind (`--kind helmet/hat/top/trousers/coat/weapon/prop/character`) | Meshy's 50k-triangle sword |
| textures | maps over 1024 (WARN) or 2048 (FAIL), with the video memory they take in the game | Meshy's four 2048 maps, about 22 MB |
| size | the size and where it stands; against `--height` if given | |
| holes (H) | small holes in the surface; big openings (sleeves, a collar) are only counted | the shearling coat's fur |
| spikes (S) | points sticking out of the surface (measured out of it only: a long thin triangle's corner lies far to one side of its neighbours, in the surface, and is no fault) | the coat's collar after the shoulder lift |
| floating bits (F) | small loose pieces off the model | |
| z-fighting (Z) | faces lying on top of each other (they flicker) | |
| turned faces (T), broken edges (B), zero-size faces (D) | surfaces inside out, edges shared by three faces, faces with no area | |
| texture coordinates | missing, squashed or outside the map | the combat sword had none |

Each place a check finds gets a label (H1, H2, S1 ...) in the report. With `--render`, it also gets a ring in a picture of six views (front,
right, back / left, above, below). The rings and labels are drawn where that place can be seen. A place no view shows is marked "inside the model".

## With the owner's descriptions

Some findings are meant: a helmet's eye slits are holes. The owner says which ("H1 and H2 are eye slits, keep them") and the check is run
again with:

```
... -- <model> --keep H1,H2 --why "eye slits"
```

This writes them into the model's notes, `<model>.qa.json` beside it. Every later check of that model lists them as kept, with the reason
(green in the picture), and leaves them out of the verdict. They are matched by place, within 3 cm, so the labels can change between
checks. Anything new is still flagged.

## Verdicts

PASS, WARN (look at it), FAIL (too big, or the wrong size or no texture coordinates). The exit code is 1 on a FAIL.

`--selftest` builds broken models on purpose and checks each fault is caught, that a clean model passes, and that a noted hole is kept.
Run it after changing the script.

## First runs (2026-10-19)

| Model | Result |
|---|---|
| Midnight Coat | 14,876 triangles (over the 12k "fine" line for a coat, under 15k); 1 spike S1 at the back of the skirt's slit; no holes |
| Knight Helmet | 7,390 triangles; 3 small holes (H1 and H2 on the visor's side, H3 low at the back) and 1 z-fight, for the owner to judge |
| Black Jeans (2026-10-24) | 11,335 triangles (over the 8k "fine" line for trousers, under 12k; the owner's count, lower wiped the stitching); a 2048 map in the Blender file (1024 in the game); 2 z-fights from the fit, fixed in black_jeans_fit.py |
| Combat sword | within budget; 14 spike points along the blade's thin edge |
| Meshy White Sword zip | 50,698 triangles and 8 maps of 2048 (about 45 MB of video memory): why it was baked down |

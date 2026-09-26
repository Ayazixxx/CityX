# Hairstyles: adding one, choosing one, placing one

Requested 2026-09-25: put `AnimeHair0.glb` in as a hairstyle, add a hairstyle
selector to the character creator, fit it to the head as best as possible, and
make the placement adjustable -- in the Unity editor, not through console
commands.

## What there is now

- **Styles** (`CharacterHair.ValidStyles`): `none`, `embermane`, `animehair0`,
  `hair1`. A save file stores the string, so the names never change once shipped.
- **The selector**: the creator has one button per style (BALD / MANE / ANIME /
  BOB), built from `ValidStyles`, so a new style gets its button with no builder
  edit. The buttons share one row and narrow to fit the 200-unit column
  (`HairButtonRow`); past five or six styles that wants to become a picker. The
  same list is wired onto the character list's carousel heads, the player in the
  world and the in-game redraw creator (`HairAssets.StyleMeshes`, which walks
  `ValidStyles`, so the two lists cannot drift out of order).
- **The automatic fit**: `CharacterHair.FitToHead` scales the mesh so it is
  `WidthOverHead` (1.12x) the head's width, stands its top `CrownLift` above the
  crown and centres it on the head. It only needs the mesh's bounds.

## Placing a style by hand (no console)

1. Press **Play** (any scene with a head: the title's creator, or the world).
2. In the Hierarchy select the character (the creator's body, or the player)
   and look at its **Character Hair** component.
3. Pick the style in the **Style** drop-down there if it is not already worn.
4. Press **Select the hair**. The worn hair (an object called `Hair` under the
   head bone) is selected and the Scene view frames it.
5. Drag it with the Move / Rotate / Scale tools, or type numbers into its
   Transform. Every change is live on the head.
6. Press **Save this placement**. The numbers are written into
   `Assets/CityX/Character/Resources/HairFits.asset`, which survives leaving Play
   Mode and is read by every head wearing the style: creator, character list,
   player, redraw creator.

**Go back to the automatic fit** deletes the saved entry. The **Now** line in the
Inspector shows the current numbers if you want to paste them somewhere.

Nothing is applied until Save: while dragging you are moving one head's hair,
not the shared fit.

## What the anime hair model is

`AnimeHair0.glb` is one mesh, about 12,000 vertices, no material and a single
node that scales it by 1.74 and lifts it 1.1 m. The node's transform is
**baked into the vertices once** (`HairAssets.AnimeHair0` writes
`AnimeHair0Mesh.asset`), because a glTF with one node imports as that node
itself and a fit measured on the raw vertices lands the hair half a metre wide
and floating. Delete `AnimeHair0Mesh.asset` to re-bake from a changed model.

It only carries **fringe, side locks and tufts at the nape**. There is no crown or
back-of-head piece, so from behind the scalp shows between the tufts. That is
the model's coverage, not the placement (drawing both faces makes no
difference). A hair-coloured scalp cap under it, or a second mesh with the back
of the hair, would fix that.

## What the Hair1 model is

`Hair1.glb` is a full **bob cut**: one mesh of about 800 vertices, no UVs, one
plain material, a node that scales it 1.41x and lifts it 1.3 m (baked the same
way, into `Hair1Mesh.asset`). Unlike the anime hair it covers the whole head: a
straight fringe across the forehead, the sides down past the ears, and the back
to the nape. It faces +Z (fringe to the front), so its yaw is 0, and the
automatic fit sits it well without any hand tuning.

## Adding another style

1. Put the model in `Assets/CityX/Character/Hair/`.
2. Add its name to `CharacterHair.ValidStyles`, give it a short label in
   `LabelFor` (it has to fit a narrow button), and a yaw in `YawFor` if the
   model does not face +Z.
3. Add a case for it in `HairAssets.MeshFor`. A model file is baked with
   `BakedFromModel` (see `AnimeHair0()` / `Hair1()`).
4. Rebuild the scenes (Title, and CityMassing). Place it as above if the
   automatic fit is not right.

## Tests

- `CharacterHairFitTests` (EditMode): the fit's maths, saved-vs-automatic
  precedence, each model's baked mesh size and placement, every style having a
  label and a mesh, and the button row fitting the column.
- `CharacterCreatorPlayTests` (PlayMode): each model hairstyle on the real head,
  each button selecting its style, saving a fit moving the worn hair.
- `TitleScreenCapture.CaptureAnimeHair` / `CaptureHair1` write front / side / back
  head close-ups to `Logs/CityShots/` for judging a fit by eye.

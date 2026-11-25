# The Squirrel Mask

Owner, 2026-11-25, with their Meshy model of the character wearing it ("Meshy_AI_Foxmancer_20261125_generate(1).fbx") and two pictures: "im trying
to make this mask for my superhero character "squirrel boy" but the nose unintentionally went inward when generating. can you make it protrude
like the other image attached. that nose looks better but i like the more detailed furry model. once the nose is lower it to a reasonable face
count and separate the mask from the playermodel so we can use it ingame ... make the mask this orange/brown color with the eyes black and nose
black."

## The item

`squirrel_mask`, "Squirrel Mask", in the **helmet** slot: it covers the top, back and sides of the head with ears on top, so a hat over it would
go through it. Hair is hidden under it, as under the Claw Skull. `/equip squirrel` (or `squirrelmask`, `squirrel_mask`). My choices, for the
owner to change: the name, the slot, no armour, and no rarity set (so Common).

## The model

`SourceArt/SquirrelMask/squirrel_mask_build.py` makes `Wearables/Models/SquirrelMask.glb` from the Meshy file in one run (about 35 s):

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/SquirrelMask/squirrel_mask_build.py -- "<Meshy fbx>"
```

then `SquirrelMaskBuilder` (run by the scene builder) makes the prefab. The Meshy file is 1.2 million triangles, one piece, with no texture.

- **Cut off the body by texture of the surface.** Fur is rough, skin smooth: a point is smooth when its surface direction agrees with its
  neighbours'. The bare skin is spread to from the neck over smooth points, never above the nose's foot (the fur at the crown is smoother,
  and the first try leaked into it); what the skin cannot reach is the mask. The smooth hollows of the eyes and the insides of the ears are
  closed in by fur, so they stay with the mask.
- **Eyes black.** Each eye's almond hollow, spread to from its middle until the surface bends more than 12 degrees (the crease round it).
- **The nose.** Meshy's was a pit. It is covered by a new nose: an upside-down rounded triangle, 7 cm across and 6 cm tall on the game's head,
  standing about 2.5 cm out of the furthest fur round it (at first 1.5 cm, which barely broke the snout's outline from the side), rounded over the
  top and the tip like the owner's smoother model's. Its root is in the face.
- **Cut to 12,000 triangles.** At 7,200 (under a mask's 8,000 most) the fur was gone, only a ball with a few shards; at 12,000 it reads, as the
  Fur Hooded Parka's does at 14,600. The cut takes first where the fur stands out most (the face), so the sides and back keep some; it
  spares the eyes' outlines (cut freely, each eye became a few sharp triangles). Small holes it opens in the fringe are closed and loose tufts
  dropped. Shaded smooth, with hard edges past 30 degrees.
- **On the game's head.** The mask lies on a ball (radius 0.270 in Meshy's units, 7 mm spread). It is centred on the Head bone's pivot and
  scaled (1.39, about the body's own 2.6 m / 1.9 m) so all but its deepest 2% of fur clears the head (0.35 m) by 3 mm; those few points are
  lifted onto that. The mask's ball is then 0.375 m: it stands about 2.5 cm off the head, as a fur mask would.
- **Colours.** Fur: the owner's picture's orange-brown, which reads about (140, 72, 25) in their viewer's dim light; (165, 82, 25) in the
  game, so it reads the same in daylight. Eyes and nose: near black, the nose with a little sheen. Flat colours, no texture. It has texture
  coordinates, for a texture one day.

## Model check

`tools/model_qa.py --kind helmet`: triangles FAIL (12,382 against a helmet's 8,000, on purpose, above); three faces lie on others at the ears
(the cut folding the ears' thin rims; too small to see); holes, floating bits, spikes, broken edges, turned faces and texture coordinates pass.

## Tests

`SquirrelMaskTests`: the helmet slot, the triangle count (13,000 at most, so it does not grow), orange fur and black eyes and nose, an eye
either side at the eyes' height, the nose standing out of the fur round it, the fur outside the head with ears above it, worn on the Head bone
with the hair hidden, shown to other players (`NetworkedPlayerWiring.ItemPrefabPaths`, `ICosmeticMountable`), the console's names.
Pictures: `SquirrelMaskCapture` (`squirrelmask-*.png`).

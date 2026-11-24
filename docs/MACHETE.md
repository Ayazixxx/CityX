# The machete (2026-11-23)

Owner, with Meshy's "Machete" (`Meshy_AI_Machete_1024145801_generate.fbx`) and a photo of a plain machete, silver blade, black handle with three
rivets: "put this machete in the game as a one handed sword, texture it so the blade is silver and the handle is black with those 3 silver dots,
also lower the face count to a reasonable amount and get rid of the square grid pattern on the handle just make it smooth, i feel like it will
interfere with the 3 silver dots and is just a waste of faces".

`/equip machete`. Common (my placement: a farm tool, beside the kitchen knife).

## How it plays

A one-handed sword with an edge, exactly as the combat sword: the same one-handed chain, heavy, guard, parry and dash, the one-handed sword's blow
(`CombatTuning.OneHandedSwordDamage`), and its killing blows cut a part off (`ItemRegistry.HasEdge`). Slash.

## The model

`SourceArt/Machete/machete_build.py` (Blender, headless) makes `Weapons/Machete.glb` from Meshy's file:

| | Meshy's | Here |
|---|---|---|
| Triangles | 188,058 (the handle's chequered grip nearly all of them) | 3,979: the blade 1,599, the handle 1,876, the rivets 504 |
| Blade | its own | its own, cut where the handle starts, reduced |
| Handle | chequered | made new and smooth: rings round its line, each as tall and thick as the old handle there, so the finger lip by the blade and the hook at the butt stay; its grooves are stepped over |
| Rivets | none | three low silver domes on each face, an eighth, nearly a half and three quarters of the way down, as in the photo |
| Texture coordinates | none | unwrapped (no map is used: each part is one plain colour) |

- **Size**: 1.10 m end to end on the 2.6 m body (a real one's 70 cm at the game's 1.3x, a little more), the blade 0.89 m. Mine.
- **Colours** (`MacheteBuilder`): the blade and rivets silver (0.78, 0.79, 0.82; a little metal, fairly smooth), the handle black.
- **Frame**: laid in the sword frame in Blender: the guard (where the handle meets the blade) at the root on the handle's middle line, the blade
  up, the edge (Meshy's thin bottom side, 3 mm against the spine's 12) toward the sword's -X, where every swing leads; the butt's hook is on the
  edge's side, as a real machete's is.
- Meshy's tiny hole and three z-fights were all in the old handle, which is gone.

## Other players

A `SwordWeapon` in `NetworkedPlayerWiring`: held, swung and cut with on every machine as the combat sword is.

## Tests

`MacheteTests` (the item, one-handed and edged, the blow; under 6,000 triangles, the handle a smooth one; silver blade and rivets, black handle;
the handle down, the edge on the thin side and the hook on it, three rivets on each face). Pictures: `SwordStrikeCapture.RenderTheMacheteAttacks`
(Explicit, -Graphics): `strike-machete-*.png`.

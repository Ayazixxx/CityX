# The Farmer Hat

A wide straw hat with a dark band, worn on the head. The owner, 2026-10-26: "separate the white playermodel from the hat. then lower face count
to a reasonable amount and add it as a wearable hat ingame called "Farmer Hat"". Spawn it with `/spawn farmerhat` (or `farmer_hat`,
`strawhat`), put it on with `/equip farmerhat` or by picking it up (E).

## Where it comes from

- Meshy's "Straw Hat Wanderer": the hat fused to a white figure, one surface of 3 million triangles with a 4096 colour map.
- `SourceArt/FarmerHat/farmer_hat_build.py` (Blender, headless) makes it:
  - It traces the figure up from its feet through the white faces only: the figure is pure white and the straw is yellow and brown, so pale
    bits of straw stay with the hat.
  - It fits a sphere to the figure's head: radius 0.234 at Meshy's scale, 1.644 tall. The figure is our own body at about two thirds size.
    Scaled so that head is ours (0.35 m, centred 2.251 m up), the hat lands where Meshy's figure wore it.
  - It cuts the figure away, reduces the hat to 5,000 triangles, unwraps it and bakes Meshy's colour onto one 1024 map.
  - It holds the crown's inside 1.5 cm off the head. Meshy modelled it resting on the scalp, and reduced, the crown's top tore into shards:
    the head's skin showed through the gaps wherever the lining lay on it. Now the gaps show straw.
- Saved: `FarmerHat.glb` and `farmer_hat_color.jpg` in `SourceArt/FarmerHat`, copied to `Wearables/Models/FarmerHat.glb` and
  `Wearables/FarmerHat.jpg` (189 KB). `FarmerHat.blend` (not committed) holds the hat on our body for adjusting by hand.
- `tools/model_qa.py --kind hat`: 5,000 triangles (over the 4k "fine" line for a hat, under 6k), 2 faces lying on others; nothing else.

## In the game

| Step | Where | What |
|---|---|---|
| Item | `ItemRegistry.FarmerHat` (`farmer_hat`, helmet slot) | a wearable, kept on the character and shown on the select screen |
| Prefab | `Wearables/Editor/FarmerHatBuilder.cs` (menu CityX > Rebuild Farmer Hat, and with the scene) | the glb with the head's centre taken off, so it sits on the Head bone's pivot with no offset |
| Wearing | `Wearables/FarmerHat.cs` (`HeadWearable`) | rigid on the head; bald under it (below) |
| Other players | `ICosmeticMountable`, `NetworkedPlayerWiring` | a copy of the wearer wears it the same way |

**Hair:** rendered with every hairstyle (`FarmerHatCapture`, `farmerhat-hair-*.png`), five of eleven come out through the crown and the band:
Danny, the anime hair, the swipe cut, HYDE, the dreads. So the wearer goes bald under it, as under the Knight Helmet (the owner's choice for
that one); the hair comes back when it is taken off.

## Tests

- `FarmerHatTests` (EditMode): the slot and name; the budget and the map; the brim wider than the head and the head wholly inside the crown
  (rays from the head's centre up through the crown meet the hat only past the head's surface); bald under it and the hair back after; the
  copy's prefab.
- `FarmerHatCapture` (Explicit, `-Graphics`): front, three-quarters, side, from above behind, the whole player, and one shot per hairstyle.

To refit it: open `FarmerHat.blend`, move or scale the hat on the body, export it over `Wearables/Models/FarmerHat.glb` (selected only,
glTF binary, no materials) and rebuild the scene.

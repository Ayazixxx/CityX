# Clothes kept on a character

Owner, 2026-10-20: "make it so items you equipped stay equipped when you close the game and relaunch and select that character. and make
sure they have the items equipped on the character select screen (not weapons yet just wearables)".

- **What counts as clothing:** items registered with `ItemDefinition.WearableCategory` (`IsWearable`). These are the T-shirts, coats,
  cloak, Knight Helmet and Black Jeans (the Skelli jeans and the top hat until 2026-10-24, taken out to be replaced with better models).
  Weapons and back gear (jetpack, parachute) are not kept. Several worn at once each hide their part of the body (`WornBodyCover`,
  docs/BLACK_JEANS.md).
- **Saved:** `CharacterSave.Wearing` lists the worn item ids. An older save loads wearing nothing.
  - `CharacterIdentity.Write` copies `Inventory.WornClothingIds()` onto it on every autosave and on quit.
  - Any change to what is worn is saved at once (`Inventory.EquipmentChanged`).
- **Put back on:** `CharacterIdentity.Start` equips each saved piece through `Inventory.EquipItemId`. This runs after `Awake` has painted
  the body, because painting recolours every renderer under it.
- **Select screen:** `CharacterSelect.PutOnClothes` puts the saved pieces on each pedestal body with the item's own `OnEquip`, which only
  dresses the body. It runs after the paint and before the bodies are moved onto the stage's layer. The prefabs come from
  `CharacterSelect.ClothingPrefabs`, filled by `CharacterSelectBuilder` from `NetworkedPlayerWiring.ItemPrefabEntries(IsWearable)`.
  That is the one item list shared with `NetworkedPlayer.prefab`, which now lists the clothes too, so a joining player can wear them.
- **Tests:** `CharacterSaveTests` (round trip, old saves), `WornClothesPlayTests` (put back on, change saved) and
  `CharacterSelectCarouselPlayTests.ACharactersClothesAreOnTheirPedestalBody`.

## Known gaps

- Another player's copy of you shows only the Knight Helmet. The shirts and coats have no `ICosmeticMountable` yet, so
  `NetworkedEquipmentSync` cannot build them on a copy.
- The inventory's storage is not saved, only what is worn.

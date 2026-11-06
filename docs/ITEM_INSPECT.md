# Looking at items: the prompt and the appraisal card

Owner, 2026-10-31: "just make it like skyrim when you are hovering over and item and you can either press E to pickup, or F to inspect";
"make it kind of opens that sao menu and zooms into it a bit so its legible when you press F"; "make it smoothly transition into it not
just snap into a different camera position"; "look at the game doc and decide what information should be listed on the inspect card, i
think weapon type(s) and damage and mana cost if it does, stuff like that"; "we will figure out what armor is gonna look like later".
Earlier (2026-10-19) the owner sent a Sword Art Online screenshot of the "Appraisal" card hanging beside a sword, and on 2026-10-30
decided there is **no durability**; on 2026-10-31 they chose **compare arrows** against the item in hand and **no level requirement**.

## What you see

- **The prompt.** Look at an item lying in reach and its name appears under the crosshair in its rarity's colour, with `[E] Take  [F] Inspect`.
  A drawer or cupboard shows its name and `[E] Search`. Nothing shows while a card or the loot window is open.
- **The card.** F opens an appraisal card standing in the world beside the item, a pale sheet facing you like SAO's: "Appraisal" across the
  top, a round mark with the item's initial (the owner draws the icons; the first letter stands in, as the hotbar's spell icons do), the
  NAME in its rarity's colour, "Knife / One Hand" under it and the rarity word, then the numbers in two columns and the notes, and
  `[E] Take   [F] Close` along the bottom.
- **Beside a wall.** The card stands on the viewer's right unless something solid is there (a box check of the card's room), then on the
  left, else standing over the item; the camera swings to whichever side the card took (the owner, 2026-10-31: "the inspect thing is going
  through walls at certain angles").
- **The glide.** The camera does not cut: over half a second it swings round to the card's side (so your own body is out of the way),
  comes in to 2.7 m and looks down a little, the card unfolding from nothing as it comes; at 50 degrees of view the card fills nearly half
  the height, so it reads. F or Tab folds the card away and the camera glides back to exactly where it was. E takes the item and does the same.
- While the card is up the body does not move and the mouse does nothing (the controls are the card's, as the loot window's are).

## What the card lists

Decided from docs/GAME_DESIGN.md (the styles table, the 24 stats, the staffs and the items by rarity):

| Kind | Type line | Numbers | Notes |
|---|---|---|---|
| Blades, knives, the bat, the crowbar | Knife / Sword / Greatsword / Bat / Crowbar, One or Two Hands | Attack (a blow), Speed (the knife's x1.3), Reach (the knife's 90%) | Style (One- or Two-Handed Melee), scales with Melee Damage, Backstab x3 (knife) or x1.5 (one-handed blade), a dull blade's no edge |
| Guns | Pistol / Rifle / Crossbow | Attack (a shot), Fire rate (auto or semi), Magazine, Range | Handguns / Long Guns / Archery; scales with Firearm Damage |
| Bow, slingshot | Bow / Slingshot, Two Hands | Attack and Range by the draw (tap to full), Draw (seconds to full) | Archery and Throwing; scales with Firearm Damage |
| Staffs and the wand | Staff (Two Hands) / Wand (One Hand) | Casts (the spell), Attack (a shot, or a second for a held stream), Fire rate, Mana (a shot or a second; "Free" for the speck), Heals for the healing staff | Magic Weapons; scales with Spell Power; the wand's +5% damage and 10% less mana, frost's freeze, the jet's knockdown |
| Flamethrower, grenade, bottle | their own | Attack, Range or Radius, Fuse | Special / Explosives / Improvised |
| Jetpack, wings, parachute, grapple, spray can, flashlight, gas can | Flight gear, Parachute, Grapple, Tool, One Hand or On the back | none | one line on what it does |
| Clothes, hats, shoes, the shield | Top / Legs / Shoes / Headwear / Shield, Worn | armour pieces: Armour, the rating and the share it stops (docs/ARMOUR.md, 2026-11-06) | Clothing, or the armour's tier (Heavy Armour) |

Every number is read from the same constant or prefab field the weapon fights with (`Weapons/ItemCards.cs`): the sword's own blade settings
(`SwordWeapon`), the gun's `FirearmProfile`, the bow's `DrawnProfile`, each staff's rules (`FlameStreamRules`, `ArcaneOrbRules`, ...), so the
card cannot say one thing while the blow does another. **Compare arrows:** a green ▲ or red ▼ after a number that the item in your right
hand (the same line on its card) beats or loses to; lower is better for mana and the draw. No arrow where there is nothing to compare.

**Rarity** (`Items/ItemRarity.cs`): the owner's list in docs/GAME_DESIGN.md for the items it names. The rest are placed beside what they are
most like until the owner places them: the spike sword, the parachute, the grenade and the hooded cloak uncommon; the Midnight Coat and the
fireball, ice shard, arcane beam, healing and water staffs rare; the angel wings very rare; the jeans, sneakers, the farmer hat, the round
shield, the spray can, the flashlight and the gas can common. Colours: common grey, uncommon green, rare blue, very rare purple, legendary
orange, godly red (`UI/RarityLook`).

## How it is built

- `Character/Interaction/ItemInspector` (on the player, added with the pickup and the loot searcher): each frame finds what is looked at in
  reach (`Hovered`, `HoveredContainer`: the pickup's and the loot searcher's own rules, the item winning a draw); F opens the card; while
  open it drives the camera and reads E, F and Tab; the input reader and the third-person camera are switched off meanwhile and given back.
  A flashlight's F (its switch) stands aside when F would open a card (`ClaimsF`).
- `Character/Interaction/InspectShot` (pure): where the card stands (0.9 x 1.15 m, 0.3 m to the viewer's right of the item, its bottom
  0.1 m over it, leaning to face the camera) and where the camera goes (swung 40 degrees to the card's side, 18 degrees down, 2.7 m from
  the point between item and card), and the eased blend between the camera's own place and that one (a smoothstep: still at both ends).
  A wall behind the camera's place pulls it in (a sphere cast from the focus).
- `Items/ItemCard` (pure): the card's lines and the compare. `Weapons/ItemCards`: fills one from the item's prefab.
- `UI/Hud/ItemPromptView` (a screen canvas) and `UI/Hud/ItemCardView` (a world-space canvas scaled by the glide's progress), both built in
  code and put in the scene by `CityMassingSceneBuilder`.
- Nothing is sent to other players: a card is this machine's menu, as the loot window and the map are.

## Tests

`ItemCardTests` (EditMode: every item has a card with a name, a type and a rarity; the knife's and the butter knife's lines; the Glock's and
the AK's from their profiles; a staff's spell and mana; clothes have no numbers; the arrows point the better way; the rarity list),
`InspectShotTests` (the card beside and above the item facing the camera, the camera at its distance looking at the focus, the blend still
at both ends and never a jump), `ItemInspectPlayTests` (the real body: the prompt names the knife and its keys; F opens the card, the camera
glides without a jump and the controls go to the card; F closes it and the camera is back where it was; E takes the knife; F with a
flashlight in hand inspects rather than switching the light). `ItemInspectCapture` (Explicit, -Graphics) renders the prompt, the glide
half-way and the open card to `Logs/CityShots/inspect-*.png`.

## Not done

Icons (the owner draws them; the initial stands in). A gamepad's buttons. Inspecting from the inventory and the loot
window (the card is only for items lying in the world for now). The thumbnail of the item's model in the round mark.

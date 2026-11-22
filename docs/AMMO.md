# Ammunition

Owner, 2026-11-21: "make a basic ammunition model for each type of gun that is an in game right now as an item spawn you can spawn and pick
up into your inventory and press r to reload into your guns", "look at what ammunition models for those guns look like in other games as a
guide".

## What it is
- Four bag items (`ItemRegistry`, category `ammo`, never equipped): **Pistol Ammo** (the Glock), **Rifle Ammo** (the AK-47), **Shotgun Shells**
  (the pump shotgun), **RPG Rockets**. Rounds stack in the bag: 240, 300, 60 and 6 to a slot; a stack never goes past its size (the server
  refuses a bag that does, `BagRules`).
- A box of each on the ground (`AmmoBox`, built by `AmmoBuilder`): E puts its rounds in the bag (30, 60, 12 and 2); what does not fit stays in
  the box. `/spawn ammo`, `rifleammo`, `shells`, `rockets` (more spellings in `DevConsoleView.Spawning`).
- **R reloads from the bag** once the gun's own spare rounds (what it came with) are spent: `FirearmProfile.AmmoItemId`,
  `PistolWeapon.SpareRounds` and `DrawSpare`. The HUD's number after the slash is both together. A magazine gun, the shotgun's tube shell by
  shell, the RPG's rocket and a pair of pistols all draw the same way. Another gun's rounds are never taken. The crossbow (bolts) has none.
- Other players: the rounds are the owner's bag, saved by the server as any stack; the reload itself is already shown to everyone.

## The models
What games do (looked up): a small box per calibre, told apart by colour and a mark (stylized packs colour-code pistol, shotgun, rifle; Resident
Evil's handgun box shows its rounds). Stand-ins from simple shapes at the game's 1.3x (`PrimitiveParts`), until the owner's models:
- Pistol: a cardboard box with a yellow band, its brass rounds' copper tips out of the open top.
- Rifle: an olive ammo can, its lid's lip, latch, carrying handle and an orange band.
- Shotgun: a cardboard box with a red band, two rows of red shells in its top, one lying loose with its brass head.
- RPG: two of the RPG's own rockets (its `Rocket` part) side by side.

Tests: `AmmoTests` (stacks, a full bag, every gun's ammunition an item with a box every body can show, its card), `AmmoPlayTests` (a box
picked up, the pistol reloaded from the bag after its own rounds, the shotgun's tube shell by shell, no rounds no reload and no other gun's
taken). Pictures: `AmmoCapture` (`Logs/CityShots/ammo-*.png`).

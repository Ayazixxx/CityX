# The heat press (2026-11-24)

Owner, 2026-11-24: "should we just make the t shirt press now? it will also be a good ingame tool to make a bunch of shirt designs quick", "the
heat press is furniture", then, with a photograph of a clamshell press: "use this for the heat press model. I want parts of it to move it seems like
a pretty simple mechanism to replicate. when initiated, have it clamp down for 3 seconds and then steam puffs out of it and the shirt is done", and,
of the CGTrader "automatic flatbed heat press machine": "this one looks like it opens and closes. but make it look more like the image i sent. just
use this as a guide for the opening and closing".

Then: "make the whole thing go ahead. make the ui good, make it use images off your computer and visible across computers".

The whole plan (prints on the shirt you find, dye, the paint marker, a design staying with its shirt forever) is in `docs/GAME_DESIGN.md`,
Items and art. This page is the press and the designs it prints. Dye, bleach and the paint marker are not built.

## How to use it

1. `/spawn heatpress` puts one down (on every machine in a session). Have a T-shirt, worn or in your bag (`/spawn tshirt`, any colour).
2. Look at the press: "Heat Press, [E] Print a shirt". E opens the press screen.
3. Left: your shirts (worn ones first, then the bag's; "PRINTED" when one has a design already). Click one.
4. Middle: the shirt as it looks from the FRONT or the BACK (Tab turns it), its print area dotted round. Draw inside it.
5. Right: BRUSH, ERASER, the colour square and hue strip, the size (- / + or [ ]), UNDO / REDO (Ctrl+Z, Ctrl+Y), CLEAR THIS SIDE,
   Alt+click picks a colour off the shirt. ADD PICTURE... opens the Windows Open window for a PNG or JPG; the picture lies over the shirt:
   drag to move it, scroll to size it, right-drag (or TURN LEFT / TURN RIGHT) to turn it, FIT fills the area, PLACE lays it down, X drops it.
6. PRESS: the shirt comes off you (or out of the bag) and lies folded on the drawer, collar to the hinge; the drawer slides in, the lid
   clamps 3 seconds, steam, and the shirt comes out printed, back on you (or in your bag). With no room for it, it is left lying on the
   press for anyone.

What is printed on a shirt already stays: pressed again, new ink goes over the old print (the eraser only takes new ink). A design is for
good, as the owner asked: it stays with the shirt wherever the shirt goes.

## What is built

- **The model**, made in code from boxes and short round bars (`Wearables/Printing/HeatPressShape`), as in the owner's photograph: a black
  frame and rails, a silver lower plate under cream felt and the orange silicone pad, a column at the back with the pressure knob, the red
  power switch and a dial, and the lid: a pale heating plate in a black frame with the V handle and its rubber grip. It stands on a plain
  steel work table at working height (mine: a tabletop press needs something under it).
- **How it moves**, from the CGTrader press: the lid turns on a round pivot through the column's top (35 degrees open, the photograph's), and
  the lower platen is a drawer that slides out along the rails (30 cm) to lay a shirt on.
- **One pressing** (`HeatPressCycle`, seconds since it began): the drawer slides out for the shirt and back in, the lid comes down and holds
  **3 seconds** (the owner's), lifts with **steam** puffing out round the plates' front and sides, and the drawer slides out with the shirt
  done, then home. A pure function of time, so a machine told when a pressing began shows the same moment.
- **`/spawn heatpress`** (or `press`) puts one down, on every machine in a session.

## A design, and where it lives

- **What a design is** (`Core/ShirtDesignBlob`): a picture for the front and one for the back, each 448 x 560 pixels as a PNG, see-through
  where nothing is printed (`Wearables/Printing/DesignPng`). The print area (`ShirtDesignArea`) is 58 x 72.5 cm, from just under the front
  of the collar to near the hem, the same on the back. A side too heavy (a photograph over 640 KB) is kept with its colours a little coarser.
  Reading PNG and JPG needs Unity's Image Conversion module, switched on for this (`Packages/manifest.json`).
- **Its id** is its content's fingerprint (SHA-256, 32 hex digits, `DesignStore.IdOf`): the same design is the same id everywhere, an id can
  never name another picture, and a shirt carries only the id (`ItemInstanceData.DesignId`, `BagEntry.DesignId`).
- **Kept for good** (`Core/DesignStore`): every machine keeps the designs it knows as files, `<data>/server/designs/<id>.design`. The server
  keeps every design pressed on it; a player keeps the ones they were sent. Nothing deletes a design.
- **With its shirt**: worn (the shirt is drawn with it, `PrintedShirt`: its own cloth with the pictures laid over the front and back
  pieces of its texture, compressed, shared by everyone in that shirt), in the bag and the save, dropped (the folded shirt shows it),
  picked up, put back on, on the title screen's characters.

## Online

- **Designs** (`Character/Network/DesignNetwork`): a player sends the server each design they press, and each one they wear, once a session
  (one pressed offline is sent when worn into a session). A machine that meets an id it does not have asks the server; while the server has
  not got it yet (the wearer's upload on its way) it asks again every 3 seconds, ten times at most. The server keeps only a sane design
  named by its own content, one upload a second from a player.
- **What a player wears** is told as it always was (`NetworkedEquipmentSync`), the design's id after the shirt's (`t_shirt#<id>`).
- **Presses** (`Wearables/Printing/HeatPressNetwork`): one put down stands on every machine (and for anyone joining later); a pressing
  shows on every machine: the shirt on the drawer, the clamp, the steam, the print. The shirt comes back only on its owner's machine.

## Not built yet

- Finding a press in buildings and carrying it home (furniture and bases are not built); for now `/spawn heatpress`.
- Shirts with other players' designs turning up as loot (the designs are all kept for it).
- A sound for the clamp and the steam. Dye, bleach, the paint marker.
- A setting to hide other players' designs.

## Pieces

| Piece | What |
|---|---|
| `Wearables/Printing/HeatPressShape` | the meshes: table, base, drawer, lid; paints; the pivot, open angle, drawer travel |
| `Wearables/Printing/HeatPressCycle` | one pressing as time goes: phases, lid angle, drawer, when the steam comes |
| `Wearables/Printing/HeatPress` | on the prefab: moves the lid and drawer, puffs the steam |
| `Wearables/Printing/HeatPress.Pressing` | a shirt through the press: taken from the player, on the drawer, printed, given back |
| `Wearables/Printing/HeatPressUse` | "[E] Print a shirt" (HoverTarget) |
| `Wearables/Printing/HeatPressNetwork` | presses and pressings on every machine |
| `Wearables/Printing/ShirtDesignArea`, `ShirtDesignCanvas`, `ShirtSilhouette`, `DesignPng`, `PrintedShirt` | the print area; the design being drawn (layers, brush, eraser, pictures, undo); the shirt as the screen shows it; a side as a PNG; a shirt drawn with its design |
| `Core/ShirtDesignBlob`, `Core/DesignStore` | a design's bytes; the designs kept on disk, by id |
| `Character/Interaction/DesignLibrary`, `Character/Network/DesignNetwork` | where a design is found (here, or asked of the server); designs between machines |
| `Character/Player/Inventory.TakeOut` | one thing out of the inventory altogether (worn or carried) |
| `UI/Menus/HeatPressView` (+ `.Drawing`, `.Layout`), `UI/Menus/PictureFile` | the press screen; the Windows Open window and reading a picture |
| `Wearables/Editor/HeatPressBuilder` | `Wearables/HeatPress/HeatPress.prefab`, its meshes and paints; built with the scene |

## Tests

`HeatPressTests` (the 3-second clamp, steam as it ends, the drawer only moving under an open lid, the plates meeting closed, the lid's front
up when open, standing on its table), `ShirtDesignTests` (a design's bytes, id and checks; the store; waiting for a design; the canvas's
brush, eraser, undo and pictures; PNG sides; every shirt's print fit; the shirt seen flat; the design through the bag, a drop and the
wire), `HeatPressPlayTests` (a worn shirt back worn and printed, a carried one back in the bag, pressed again over its print; another
player's copy wears the print; another machine shows a pressing), `NetworkedDesignsPlayTests` (over real sockets: a design to the server,
to another player, asked again until it comes, junk refused), `HeatPressCapture` (the press at rest and pressing; the press screen, the
shirt on the drawer, and worn front and back).

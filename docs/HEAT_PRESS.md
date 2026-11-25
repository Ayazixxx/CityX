# The heat press (2026-11-24)

Owner, 2026-11-24: "should we just make the t shirt press now? it will also be a good ingame tool to make a bunch of shirt designs quick", "the
heat press is furniture", then, with a photograph of a clamshell press: "use this for the heat press model. I want parts of it to move it seems like
a pretty simple mechanism to replicate. when initiated, have it clamp down for 3 seconds and then steam puffs out of it and the shirt is done", and,
of the CGTrader "automatic flatbed heat press machine": "this one looks like it opens and closes. but make it look more like the image i sent. just
use this as a guide for the opening and closing".

The whole plan (prints on the shirt you find, dye, the paint marker, a design staying with its shirt forever) is in `docs/GAME_DESIGN.md`,
Items and art. This page is the press itself.

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
- **`/spawn heatpress`** (or `press`) puts one down and plays one pressing, to see it work.

## Not built yet

- Using it: walking up, putting a shirt in, drawing the design or placing a picture, and the shirt coming out printed. That needs each
  shirt to carry its own design first (in the bag, the save, dropped, looted, and shown to other players), the next step.
- Telling other players when a pressing starts (the cycle is ready for it: one start time).
- Finding one in buildings and carrying it home (furniture and bases are not built).
- A sound for the clamp and the steam.

## Pieces

| Piece | What |
|---|---|
| `Wearables/Printing/HeatPressShape` | the meshes: table, base, drawer, lid; paints; the pivot, open angle, drawer travel |
| `Wearables/Printing/HeatPressCycle` | one pressing as time goes: phases, lid angle, drawer, when the steam comes |
| `Wearables/Printing/HeatPress` | on the prefab: moves the lid and drawer, puffs the steam |
| `Wearables/Editor/HeatPressBuilder` | `Wearables/HeatPress/HeatPress.prefab`, its meshes and paints; built with the scene |

## Tests

`HeatPressTests` (the 3-second clamp, steam as it ends, the drawer only moving under an open lid, the plates meeting closed, the lid's front
up when open, standing on its table), `HeatPressCapture` (at rest from the front, side and above, beside the player, and moments of a
pressing).

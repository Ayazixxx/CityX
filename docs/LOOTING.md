# Looting: looking into drawers and cupboards

Owner, 2026-10-30: "i want the looting to be detailed like skyrim or project zomboid, you have look it cabinets and drawers and stuff". This is step 4 of
docs/LOOT_AND_BUILDING_LEVELS.md (closed containers: search, open, list), done first in Old Guy's house (docs/OLD_GUYS_HOUSE.md), the one interior
that has furniture so far.

## How it plays

- **Look at a drawer, a cupboard door, the fridge, the wardrobe, a chest or the toolbox and tap E.** It opens: a drawer slides out, a door swings on its
  hinge, a lid lifts, the oven door drops. The **loot window** shows what is inside: the container's name, its items one to a row, the picked one marked.
- **While the window is open the keys are its own**, as Skyrim's container menu takes them: W and S (or the arrows) pick, E takes the picked item, R takes
  everything, Tab or Space closes; a click on a row takes that item. E on an empty container closes it. What does not fit in the inventory stays inside.
- **A searched container stays open**, as a ransacked room does in Zomboid: you can see what you have already looked through.
- **Only what can be seen**: a cupboard on the far side of a wall is never in reach, whichever way the camera points (a ray from the body's eyes to
  the container must meet nothing else first). Upper cabinets over head height are in reach (3.2 m from the feet).
- **E means one thing at a time**: an item lying in the open (the Kitchen Knife in its block) that is looked at more directly than a drawer is picked up;
  otherwise the drawer opens.

## How it is built

| Piece | What |
|---|---|
| `Character/Interaction/LootContainer` | a part that opens, on its hinge: how it opens (`ContainerKind`: drawer, door hinged left or right, lid, flap), what it is called, what is in it (item ids), open or shut |
| `Character/Interaction/ContainerMotion` (pure) | the open pose of each kind; nothing ever swings back into the wall behind it (a lid stops short of upright for that reason) |
| `Character/Interaction/LootList` (pure) | the items while looked into: picking, taking one, taking all; taking removes from the container itself |
| `Character/Interaction/LootSearcher` | on every player body (added by the input reader): finds the container looked at, in reach and in sight; opens it; runs the window's keys; gives the controls back |
| `UI/Hud/LootWindowView` | the window, built in code on its own canvas (in the scene, `CityMassingSceneBuilder`) |
| `View/Buildings/FurnitureKit.Containers.cs` | which pieces open and how: counters (a drawer and two cupboard doors), the sink's cupboard, the oven, the fridge, wall cabinets, the sideboard, the dresser (three drawers), bedside drawers, desk drawers, the wardrobe (two doors, coats hanging inside), the toy chest, crates, the toolbox, the bathroom cabinet behind the mirror; their carcasses are hollow where a door opens on them, with shelves |
| `World/Tutorial/OldGuysHouse` | what is inside each piece (`Furniture.Stash`, in a numbered part) |

## What is in Old Guy's house (my picks, for the owner to change)

| Where | What |
|---|---|
| The kitchen drawer nearest the dining room | Flashlight |
| The fridge | Glass Bottle |
| The garage workbench's toolbox | Spray Can |
| Old Guy's wardrobe (left door) | T-Shirt, Blue Jeans (the tutorial's basic clothing) |
| Old Guy's dresser (middle drawer) | White Sneakers |
| His bedside drawer | Farmer Hat |
| The crate in the tree house | **the Baseball Bat** (hidden twice: in the tree house, inside its crate) |

Every other drawer and cupboard is there to be opened and is empty for now: the game has few small items yet (Zomboid's houses are full of junk
items: tins, tools, books). The starter weapons stay in the open where the owner put them (the knife block, the counter, the desk, the glass case).

## Other players

The house's containers, like its weapons, are this machine's own: the tutorial is played alone (docs/GAME_DESIGN.md). When containers come to the shared
city, the owner's rule applies (docs/LOOT_AND_BUILDING_LEVELS.md 4.6: whoever gets it first): the server decides who takes what and every machine sees a
container open and empty, as the doors and the glass are shared today.

## Tests

`LootingTests` (EditMode: taking one or all, a full inventory keeps the item inside, picking comes round, every kind opens outward and far enough, every
piece that opens has its parts), `LootingPlayTests` (the real body opens Old Guy's wardrobe, its door swings open and the clothes go into the inventory; a
bathroom cabinet behind a wall is never in reach but is from in front of it; the bat is in the tree house's crate), `OldGuysHousePlanTests` (what is inside
is real items, in parts the pieces have). `OldGuysHouseCapture` renders `house-loot-kitchen.png`, `house-loot-bedroom.png` and `house-loot-window.png`.

## Not done

Searching time (instant now; docs/LOOT_AND_BUILDING_LEVELS.md asks whether a few seconds of risk is wanted), locked containers and keys, putting things
back in, loot tables by room and furniture (the Zomboid distributions, for the generated buildings), sounds for drawers and doors, a gamepad's buttons.

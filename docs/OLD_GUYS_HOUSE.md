# Old Guy's House: the tutorial building

Owner, 2026-10-30: "we have our 3 starter items. the kitchen knife, the wand , the slingshot. I want these to be findable in the tutorial building
and the players will just continue using whichever one they like best. im also thinking we have the butter knife and the baseball bat hidden
somewhere in the tutorial building as well ... i kind of want to carefully design the tutorial building at least. probably use some garrys mod maps
as for reference". Then: "look at garrys mod maps with houses and on the community workshop", a link to gm_house4, "incorporate all your research
into it not just the 1 map i sent", "dont be afraid to make it way better than all the other buildings", "give it some basic furniture as well make
low effort models i will replace with better stuff later", "no enemies for now ... maybe just one swordsman upstairs", "put the butter knife in the
kitchen", "make it so the kitchen knife is in one of these things [a wooden knife block] and butter knife on the counter".

The tutorial in docs/GAME_DESIGN.md: Old Guy asks you to help clear out his house; you find basic clothing and the starter weapons; the house is
cleared, its power turned back on, then claimed; the front door is kicked in (the cutscene).

The drawn plan (route, three floors, a section, the weapons, the references, the sizes) is an artifact:
https://claude.ai/artifact/2hZuY7JFD2uiWAoTvDHJZH (private to the owner). It shows the first layout (2026-10-30); the house grew and lost its backyard on 2026-10-31 (below).

## Hand-built, not generated

Every other building is generated from the world seed. This one is designed by hand and placed on a reserved lot by the spawn, so every player gets
the same house. The parts made for it (framed doors, stairs with handrails, furniture) can later dress the generated interiors.

## The references (Steam Workshop, Garry's Mod)

| Map | Taken |
|---|---|
| gm_house4 (the owner's pick) | the shape: split level, the garage under the living floor, outside stairs up the side, a glass conservatory, a garden with a tree house, an old man's room (striped wallpaper, radiator, plaid sofa) |
| ph_fancyhouse | a living room two storeys tall with a gallery looking into it; a floor per room; a garage manhole to a hidden level (here: sealed, the Sewers later) |
| gm_8800blr, a 3D scan of a real house | real room order (kitchen by the dining room, bedrooms upstairs), handrails, clutter that tells who lived there |
| ph_house_2019 | each room readable by its colour and material; a light in every ceiling; hiding spots |
| gm_hillside_house | secrets for the curious, switchable lights (here: the power coming back on) |
| The Level Design Book (docs/LOOT_AND_BUILDING_LEVELS.md) | a main route with rewarding side rooms, a loop back, a locked way with its key elsewhere, quiet before the one fight |

ph_fancyhouse's author asks that its design not be used outside Garry's Mod: these are ideas, not layouts.


## Sizes: rooms for a third-person camera (2026-10-31)

Owner: "it feel very cramped in these rooms and doesnt feel like good level design make slightly taller ceilings and slightly bigger rooms, remember its
a third person game so with the camera positioning it feels very tight. research level design in third person games specifically in buildings or
houses, see how big the rooms should be".

What the research found (no studio publishes its metrics; these agree with each other):
- **Third-person rooms are built about twice their real size.** A real 4 x 5 m bedroom with a 2.5 m ceiling becomes about 8 x 10 m with a 4 m ceiling,
  because the camera shares the room with the player ([jmason.ie, after the 2002 Max Payne level design talk](https://jmason.ie/2002/07/02/164246a.html);
  [mapcore](https://www.mapcore.org/forums/thread/9408-need-help-with-scaling-a-room-headdesk/); forum consensus 1.5 to 3 times).
- **Furniture stays close to real size** (10-20 per cent bigger at most), or the characters look like children; only the room and the gaps between
  pieces grow ([jmason.ie](https://jmason.ie/2002/07/02/164246a.html); [polycount on Max Payne](https://polycount.com/discussion/comment/1426602)).
- **Doors, windows and halls are more generous than in a first-person game**; realistic sizes "usually feel really cramped"
  ([dou.ua, level design basics](https://gamedev.dou.ua/blogs/level-design-basics-part-2/?hl=en)). A hall is at least twice the player's width, a door
  narrower than its hall, stair landings every 12-16 steps at 30-35 degrees ([The Level Design Book, metrics](https://book.leveldesignbook.com/process/blockout/metrics)).
- **Camera clearance** is added on top of the player's own room ([Architecture for Level Design](https://fr.linkedin.com/in/kayomas)).

What that means here: the player is 2.6 m tall (real sizes times 1.3) and the camera hangs 5 m behind at head height, so a room shorter than about 10 m
pushes the camera in against the wall behind. The house was 1.3 times real (rooms 5-9 m, 4 m storeys): the owner's "slightly bigger" is now about
1.4 times that, short of the full doubling.

| | Was | Now |
|---|---|---|
| Storey (clear under the ceiling) | 4.0 m (3.7) | 5.0 m (4.7) |
| Doors | 1.6 x 3.0 m | 2.0 x 3.6 m |
| Halls | 3.5 m | 4.6 m |
| Rooms | 5-9 m | 7-12 m (the WC and the gallery's walk smaller) |
| The house | 20 x 16 m, ridge 15.6 m | 28 x 22 m, ridge 19.9 m |
| Stairs | 18 risers, flights of 8 | 22 risers of 0.23 m, flights of 10 with a landing, 32 degrees |

The tests hold the plan to it: every room but the WC and the gallery's walk at least 6 m one way (`OldGuysHouse.CameraRoomSide`) and a hall's width the
other, the rooms the tutorial is played in at least 7 m both ways, 4.6 m and more under every ceiling, and nothing standing in the 1.5 m before any
doorway (the owner found a sofa in front of a door in the old plan).

## The house (house frame: metres, x east, z north, y up; the street side faces south)

Three bays across (the west bay over the garage, the stair core, the east bay) and three rows deep (the front row, the hall strip, the back row).

- **Street level, 0 m:** the driveway (z -6.5 to 0) where the player lands; the garage (x 0-11, z 0-17, a roll-up door to the driveway, a sealed
  manhole); the stair core (x 11-17, z 0-7.4, a U stair, two flights with a landing, all three floors); the stair hall (x 11-28, z 7.4-12); the
  workshop (x 17-28, z 0-7.4, the fuse box); the cellar (x 11-28, z 12-17, the crate with the bat). The outdoor steps go up the east side
  (x 28.3-30.9) to the porch.
- **Main floor, +5 m:** the living room (x 0-11, z 0-12, two storeys tall, the fireplace between two windows, bottles on the mantel); the entry hall
  (x 11-28, z 7.2-12, the front door on the east wall onto the porch, boarded); the WC and the coats room (x 17-28, z 0-7.4); the kitchen (x 0-11,
  z 12-22: the Kitchen Knife in a knife block on the counter, the Butter Knife on the counter); the dining room (x 11-18, z 12-22); Old Guy's den
  (x 18-28, z 12-22). The porch (x 28-33, z 6.5-12.5).
- **Upper floor, +10 m:** the gallery (x 8-11 over the living room, and the hall strip by the stairs); the grandson's room (x 0-11, z 12-22: the
  Slingshot on his desk); the bathroom (x 11-17, z 12-22); the corridor (x 17-28, z 7.4-12); the study (x 17-28, z 0-7.4: the Magic Wand in a glass
  case); Old Guy's bedroom (x 17-28, z 12-22: clothes in the wardrobe; the swordsman).
- **Gone (owner, 2026-10-31: "remove the backyard and treehouse and fence in the back"):** the raised garden, its fence and the old tree, the tree house
  and its plank bridge, the balcony, the patio, the side path with its garden wall, the glass conservatory.

## The route

1. Land on the driveway; Old Guy at the foot of the outdoor steps. 2. Up to the porch: the front door is boarded. 3. Tear the planks off (or smash a
window). 4. The entry hall; the kitchen: the Kitchen Knife (knife block) and the Butter Knife (counter). 5. Living room, hall, stairs up to the gallery.
6. The grandson's room: the Slingshot. 7. From the gallery, the bottles on the mantel below. 8. The study: the Magic Wand; Old Guy explains spells.
9. The one fight: a swordsman in Old Guy's bedroom. 10. Hidden: the cellar's crate: the Baseball Bat. 11. The workshop: the fuse box, the power on.
12. Old Guy in his den: factions, the claim; the front door is kicked in.

Only walking, running, crouching, fighting and breaking things are needed: the tutorial must not need climbing (docs/GAME_DESIGN.md).

## Breaking in (2026-10-31)

Owner: "leave the boarded door, make it so you have to break the wood planks", "make the windows shatterable".

- **The front door's three planks** (`View/Buildings/BoardedPlank.cs`): each takes three punches, two blows of a blade or a bat, or three bullets (which
  stop in the wood), and is torn off and falls onto the porch; a sweep of a sword that meets it on several frames counts once. With all three gone
  the green door behind them swings open into the hall (`BoardedDoor`).
- **Every window** (`View/Buildings/HousePane.cs`) takes blows as the city's windows do (`BlowStrength`): a fist cracks it (it goes paler) and the next
  shatters it; a blade, a thrown bottle or a shoulder shatters it at once; a bullet cracks it and goes on through, ten per cent weaker
  (`GlassPenetration`). Shattered, it falls in pieces (`GlassFractureBurst`, the city's) with the city's glass sounds, and the opening is clear.
- **Shared** (docs/MULTIPLAYER_STATE.md): `World.TutorialHouseDamage` numbers the pieces from the plan (planks 0-2, then the panes in the order the
  walls are written) and `Character.TutorialHouseNetwork` keeps them the same on every machine; a player joining later finds them broken.

## What is built

- **The plan as data** (`World/Tutorial/OldGuysHouse.cs`, pure): the rooms (their floors, paint and finish), every wall with its doors, arches and
  windows, the stair core's steps, the outdoor steps, the porch and footing blocks, railings, the furniture, the weapons' spots, the swordsman's and
  Old Guy's, the landing. Everything below is built from it, so a change to the plan is a change to the house.
- **The building** (`View/Buildings/OldGuysHouseBuilder.cs`): floors and ceilings per room; walls with their openings cut and framed, each stretch
  painted for the room it faces (brick outside at street level, render above); windows with mullions and stone sills, each pane its own breakable
  object; the boarded front door; the closed roll-up garage door; the U stair (wood above, concrete below) with handrails; the outdoor steps with rails;
  the porch on its stone block; a modern single-slope roof in dark metal (owner, 2026-10-31: "make it a more modern home roof maybe flat or slanted"; the photo was "just to show the roof", "no thin strip of glass"), about 10 degrees, high over the driveway (19.9 m) and low at the back, the walls risen under it; a lamp and a warm light in every room; three loose bottles on the mantel (rigid bodies).
- **Furniture** (`View/Buildings/FurnitureKit.cs`): about forty low-effort pieces of tinted boxes, for the owner to replace: the car, workbenches,
  shelves, the fuse box, the boiler, the fireplace and its chimney breast up to the living room's ceiling, sofas (the den's plaid), armchairs, kitchen
  counters with a sink and a stove, the fridge, the knife block, the dining table and chairs, the old TV, the radiator, the bathroom, beds, desks,
  bookcases, the wand's pedestal under a glass case, the wardrobe, the grandson's toy chest, poster and drawing.
- **The weapons** lie where the plan puts them, held exactly there until picked up with E (`ItemDrop.Placed`: a dropped item that does not fall); the
  **swordsman** waits in Old Guy's bedroom (`OldGuysHouseSite`). The **Butter Knife** is new (docs/KNIFE.md).
- Not yet: the power, the claim, the faction talk and the cutscene (step 3); door leaves inside (doorways are framed and open); the bottles may not
  react to a pebble yet.
- Tests: `OldGuysHousePlanTests` (doorways fit the player, stairs under 34 degrees arriving on each floor, no overlapping rooms, every room reachable
  from the street through the front door, the weapons real items in rooms and the bat in the cellar, every piece of furniture in a room, the swordsman
  upstairs, rooms sized for the camera, nothing in front of a doorway, the breakable pieces numbered), `OldGuysHousePlayTests` (the real body walks up
  the stair core from the garage level to the upper floor; the weapons lie still where they were put; the swordsman stands upstairs and is put down only once
  the player gets in), `OldGuysHouseBreakablesPlayTests` (a pane cracks then shatters, a blade shatters it, a bullet cracks it and goes through; blows
  tear the planks off, the door opens and a body walks in from the porch; a piece broken elsewhere breaks here and a later house finds it broken),
  `NetworkedTutorialHousePlayTests`, `TutorialHouseDamageTests`. `OldGuysHouseCapture` (Explicit, -Graphics) renders `Logs/CityShots/house-*.png`.

## In the city (2026-10-30, step 2 of 3)

Owner: "im thinking it spawns as the closest building to you and the rest of the world is generated randomly".

- **Where** (`World/Plan/TutorialLot.cs`, pure): the plan looks along every street frontage of the blocks within 300 m of the spawn junction, a metre at
  a time, for the spot where the house's walls come nearest the spawn and its whole lot (35.5 x 30 m, the driveway and the porch) fits inside the
  block's lot line with its front on the street: the ground under it within 0.5 m (it stands on the highest point, so the step up from the pavement
  stays small), no water, no expressway, not a plaza, mall, island block or stairway block. A pure function of the seed: every machine finds the same
  land. Only in the game's own city (the varied districts, CityScale.Default): the classic city the test suites run in has blocks too small for it.
- **Cleared**: every generated building whose footprint comes within 2 m of the lot is left out of the city, with the rest of its lot's blocks (an
  L's wing goes with its bar); the others keep their places, the ids are numbered afresh. The spawn is chosen before the clearing, so the clearing
  never moves it. Footbridges never cross the lot. The map draws the house's roof.
- **Standing it**: the streamer stands the house (`OldGuysHouseSite.PlaceOn`) at the floor height the plan worked out, turned to face its street,
  on a stone footing under the whole lot down into the lowest ground, so no gap shows under its edges.
- **Landing**: the player lands on the driveway (`TutorialLot.LandingX/Z`, `OldGuysHouse.Landing`), the house the nearest building, and Old Guy waits at
  the foot of the outdoor steps facing them. Measured to the walls, the house was rarely nearer the spawn junction than every corner building (its
  walls stand behind the 6.5 m driveway), so the landing moved to the house rather than the house to the junction.
- **The swordsman** is put down when this machine's player first gets within 1 m of the walls (inside, or at a sill), not before: the house is small enough that 30 m reached the driveway, and tests finding "the player" found him; he is moved into the house's own scene, so he goes when the city unloads (Instantiate puts him in the active scene, which outlived an additive city and leaked him into later tests). As `/spawn` does it: in a session spawned for everyone and
  run by that player's machine (docs/MULTIPLAYER_STATE.md), alone a local one.
- **World seed** 23481876 -> 61256619 (2026-10-30), -> 81565117 (2026-10-31, the bigger house's lot): `tools/cityplan tutorialseeds` ranks 40 flat seeds
  by the sun and the ground where the player lands; on this one the driveway is in full sun, the step from the pavement onto it is 19 cm and the house's
  walls are 23 m from the spawn junction. `tools/cityplan tutoriallot [seed]` prints where the house lands on any seed.
- Tests: `TutorialLotTests` (beside the spawn, the nearest building where the player lands, inside its block with its front on the street, flat, dry,
  no building or footbridge on it, the same on every machine, the house's plan inside the land kept for it), `OldGuysHouseCityPlayTests` (the streamer
  stands it on its land facing its street with nothing generated inside it and the footing at its edge; a body walks from the landing to Old Guy's
  spot, round the steps' rail, up onto the porch and back out to the street). `OldGuysHouseCityCapture` (Explicit) renders `house-city-*.png`.

## Looting (2026-10-30)

Drawers, cupboards, the fridge, the wardrobe, the chests and the toolbox open with E and are looked into (docs/LOOTING.md). The Baseball Bat is inside a
crate at the back of the cellar (it was in the tree house's crate until the backyard went); the tutorial's basic clothing is in Old Guy's wardrobe, his
hat in his bedside drawer, a flashlight in a kitchen drawer, a bottle in the fridge, a spray can in the garage toolbox.

## Power (2026-10-31)

docs/GAME_DESIGN.md: "The claim follows the real rule: the house is cleared, Old Guy has you turn its power back on, then you claim it." The
house is **dark when the player arrives**: every lamp off (by day the windows light the rooms; the flashlight is in a kitchen drawer). The
**fuse box** on the workshop's east wall has a red lever on a hinge at its middle; looked at, it names itself under the crosshair ("Fuse box",
`[E] Turn the power on`, the same prompt as the items and the drawers), and E throws it over with a switch's click: the power comes on
(`World/TutorialHousePower`, kept apart from the house's objects so a rebuild keeps it, as the broken pieces are kept) and every lamp lights.
Thrown, the lever hangs down and offers nothing more. This machine's own, as the containers are (the tutorial is played in a private copy of
the streets); the claim is not gated on it yet (Old Guy's lines for it are the owner's to write; `TutorialHousePower.Shared.IsOn` is the hook).

How: `View/Interactables/FuseBoxSwitch` is a `Character/Interaction/HoverTarget`, a thing that names itself and is used with E; the inspector's
look (`ItemInspector`) picks the item, the container or the target looked at most directly, the prompt names it, and the pickup and the searcher
yield to a target looked at more directly. `FuseBoxPlayTests` (dark; the lever named; E lights every lamp and the lever swings over; a rebuilt
house is still lit with the lever down), `TutorialHousePowerTests`. `OldGuysHouseCapture` turns the power on before its room shots and renders
`house-fuse-box.png`.

## Open (the owner's)

Old Guy following or waiting (drawn waiting in the den); keeping the sealed manhole; the pool from gm_house4 (left out); whether the rooms want the full
doubling the research gives, should they still feel tight in play.

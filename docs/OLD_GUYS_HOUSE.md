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
https://claude.ai/artifact/2hZuY7JFD2uiWAoTvDHJZH (private to the owner).

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

## The house (house frame: metres, x east, z north, y up; the street side faces south)

- **Street level, 0 m:** the driveway (z -6.5 to 0); the garage (x 0-9, z 0-10.5, a roll-up door to the driveway, a sealed manhole); the stair core
  (x 9-14, z 0-5.5, a U stair, two flights of 1.8 m with a landing, all three floors); the stair hall (x 9-20, z 5.5-9); the workshop (x 14-20,
  z 0-5.5, the fuse box); the cellar (x 9-20, z 9-14). Outdoor steps up the east side (x 20.6-23, z -4.2 to 2, landing to z 5) to the porch.
- **Main floor, +4 m:** the living room (x 0-9, z 0-9, two storeys tall, fireplace, bottles on the mantel); the entry hall (x 9-20, z 5.5-9, the
  front door on the east wall, barricaded); WC and coats (x 14-20, z 0-5.5); the kitchen (x 0-9, z 9-16: the Kitchen Knife in a knife block on the
  counter, the Butter Knife on the counter); the dining room (x 9-14, z 9-16); Old Guy's den (x 14-20, z 9-16); the conservatory (x 0-9, z 16-20,
  glass). The porch (x 20-24.5, z 5-9.5) with a 1 m garden wall (vault) to the side path and the garden (z 16 on, a raised terrace on flat ground,
  a 2.6 m fence round it, the old tree at (5, 28)).
- **Upper floor, +8 m:** the gallery (x 7-14, z 0-9, a railing over the living room); the grandson's room (x 0-9, z 9-16: the Slingshot on his
  desk, his drawing of the bridge); the bathroom (x 9-14, z 9-16); the corridor (x 14-20, z 5.5-9); the study (x 14-20, z 0-5.5: the Magic Wand in
  a glass case); Old Guy's bedroom (x 14-20, z 9-16: clothes in the wardrobe; the swordsman); the balcony (x 9-20, z 16-18.5); the plank bridge
  to the tree house (x 2-8, z 25.5-30.5, +8 m: the Baseball Bat).

## The route

1. Old Guy at the foot of the outdoor steps. 2. The porch: the front door is barricaded. 3. Vault the garden wall. 4. The garden: the tree house,
its ladder pulled up. 5. The kitchen: the Kitchen Knife (knife block) and the Butter Knife (counter). 6. Living room, hall, stairs up to the gallery.
7. The grandson's room: the Slingshot. 8. From the gallery, the bottles on the mantel below. 9. The study: the Magic Wand; Old Guy explains spells.
10. The one fight: a swordsman steps out of Old Guy's bedroom (all three weapons in hand by then). 11. Hidden: balcony, plank bridge, tree house:
the Baseball Bat. 12. The workshop: the fuse box, the power on. 13. Old Guy in his den: factions, the claim; the front door is kicked in.

Only walking, running, vaulting, crouching and breaking glass are needed: the tutorial must not need climbing (docs/GAME_DESIGN.md).

## Sizes (the 2.6 m player, the camera 5 m behind)

Storey 4.0 m (3.7 m clear; the camera at 2.9 m fits); doors 1.6 x 3.0 m; halls 3.5 m; rooms 5 m and more; stairs 0.36 m treads, 0.22 m risers, 1.8 m
flights, a landing, 1.2 m handrails; garden wall 1.0 m (the vault band is 0.42 to 2.08 m); fence 2.6 m.

## What is built (2026-10-30, step 1 of 3)

- **The plan as data** (`World/Tutorial/OldGuysHouse.cs`, pure): the rooms (their floors, paint and finish), every wall with its doors, arches and
  windows, the stair core's steps, the outdoor steps, the terrace blocks, railings, the fence, the bridge, the furniture, the five weapons' spots,
  the swordsman's and Old Guy's. Everything below is built from it, so a change to the plan is a change to the house.
- **The building** (`View/Buildings/OldGuysHouseBuilder.cs`): floors and ceilings per room; walls with their openings cut and framed, each stretch
  painted for the room it faces (brick outside at street level, render above); see-through windows with mullions and stone sills; the boarded-up
  front door; the closed roll-up garage door; the U stair (wood above, concrete below) with handrails; the outdoor steps with rails; the raised garden
  on a stone terrace with a 1 m brick garden wall (the vault) and a 2.6 m fence; the slate gable roof; the glass conservatory on a brick sill; the
  balcony on posts; the plank bridge with rope rails; the tree house on four posts round the old tree's trunk, with its own little roof; a lamp and a
  warm light in every room; three loose bottles on the mantel (rigid bodies).
- **Furniture** (`View/Buildings/FurnitureKit.cs`): about forty low-effort pieces of tinted boxes, for the owner to replace: the car, workbenches,
  shelves, the fuse box, the boiler, the fireplace and chimney breast, sofas (the den's plaid), armchairs, kitchen counters with a sink and a stove,
  the fridge, the knife block, the dining table and chairs, the old TV, the radiator, the bathroom, beds, desks, bookcases, the wand's pedestal under
  a glass case, the wardrobe, the grandson's toy chest, poster and his drawing of the bridge, the tree, a garden bench.
- **The weapons** lie where the plan puts them, held exactly there until picked up with E (`ItemDrop.Placed`: a dropped item that does not fall); the
  **swordsman** waits in Old Guy's bedroom (`OldGuysHouseSite`). The **Butter Knife** is new (docs/KNIFE.md).
- Not yet: the house is not in the city (step 2: a reserved lot by the spawn, a world seed reroll); the power, the claim, the faction talk and the
  cutscene (step 3); door leaves (doorways are framed and open); the bottles may not react to a pebble yet.
- Tests: `OldGuysHousePlanTests` (doorways fit the player, stairs under 34 degrees arriving on each floor, no overlapping rooms, every room reachable
  from the street by the tutorial's way in, the weapons real items in rooms, every piece of furniture in a room, the swordsman upstairs),
  `OldGuysHousePlayTests` (the real body walks up the stair core from the garage level to the upper floor; the weapons lie still where they were put;
  the swordsman stands upstairs). `OldGuysHouseCapture` (Explicit, -Graphics) renders `Logs/CityShots/house-*.png`.

## Looting (2026-10-30)

Drawers, cupboards, the fridge, the wardrobe, the chests and the toolbox open with E and are looked into (docs/LOOTING.md). The Baseball Bat moved from
leaning in the tree house to inside the tree house's crate; the tutorial's basic clothing is in Old Guy's wardrobe, his hat in his bedside drawer, a
flashlight in a kitchen drawer, a bottle in the fridge, a spray can in the garage toolbox.

## Open (the owner's)

The slope beside a stair street (built as a raised garden terrace, it works on flat ground too); Old Guy following or waiting (drawn waiting in the
den); keeping the sealed manhole; the pool from gm_house4 (left out).

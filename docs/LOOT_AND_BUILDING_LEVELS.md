# Loot through buildings, and each building as a level: research (2026-10-18)

Asked (owner): "I want the items you can find to be spread out amongst the rooms generated at random ... project zomboid ... I mainly like the way they
disperse loot around the buildings. some things are like on shelves and out openly and some you have to search drawers and stuff. also research level
design in general each building should feel like a 'level' that you have to clear out and get loot from as well as kill enemies if there are any. I know
we dont have a lot of items or models for furniture and stuff but we can easily make those models."

**Nothing here is implemented.** Sections 1-3 are what the games and the level-design references do; section 4 is my proposal for this game, to be
decided by the owner. It builds on `docs/BUILDING_INTERIORS.md` (2026-10-01: floor plans, Fallout's buildings, the stair kit, which is built).

Today's interiors: stairs are done (`BuildingStairs`), but each floor is still one open room plus one carved corner room, with no furniture and nothing
to find. **Rooms with a purpose are the prerequisite for everything below**: loot hangs on what a room is.

---

## 1. Project Zomboid: how loot is spread through a building

- **Loot is decided by place, not placed by hand.** Every room has a type (the mapper names it: kitchen, bedroom, bathroom, garage, gun store, police
  storage...), every piece of furniture is a container type (counter, fridge, dresser, side table, wardrobe, shelves, crate, locker...), and the pair picks
  a loot table: "Bedroom Dresser", "Bedroom Sidetable", "Bathroom Cabinet", "Bathroom Counter", "Bakery Kitchen Fridge". There are 1,424 such tables
  ([GamesRef](https://gamesref.com/game/project-zomboid/loot)). An item that is not on a container's table can never spawn there; settings only scale the
  rolls ([pzfans](https://pzfans.com/ProjectZomboidB42LootSettings/)). That is why a Zomboid house feels real: toothpaste in the bathroom cabinet, tools
  in the garage, a pistol in the bedside drawer.
- **A table is a number of rolls plus items with weights**, with a separate junk list rolled on its own
  ([PZwiki, procedural distributions](https://pzwiki.net/wiki/Procedural_distributions), read through a search summary: the wiki blocks direct reading).
- **Loot is rolled the first time a container loads**, and does not come back unless the server's respawn setting is on
  ([lootlore](https://lootlore.online/en/project-zomboid/guides/loot-locations-guide), [pzfans](https://pzfans.com/ProjectZomboidB42LootSettings/)).
- **Buildings differ by more than their rooms.** A setting gives buildings a chance to be already picked over (trashed rooms, empty containers:
  `MaximumLooted`, `DaysUntilMaximumLooted`, `MaximumLootedBuildingRooms`), loot thins out over the days (`MaximumDiminishedLoot`), and each item category
  (food, weapons, ammo, medical) has its own rarity multiplier ([pzfans](https://pzfans.com/ProjectZomboidB42LootSettings/)).
- **Stories.** Five kinds of randomized stories: building, survivor, vehicle, zone and table
  ([PZwiki](https://pzwiki.net/wiki/Randomized_stories), via search summary). Building stories range from a small addition to a whole house overhauled
  (burnt-out houses were among the first); survivor stories leave dead or zombified survivors and change the loot ("Locked in Bathroom": a zombie barricaded
  in a bathroom, a dead survivor outside it with a pistol); **table stories lay items out on tables**: that is Zomboid's "out in the open" loot, next to the
  hidden loot inside furniture.
- Zombies inside a building scale with the population setting and the number of rooms (a mod's changelog says so; not confirmed from the game).
- What does not carry over: Zomboid's buildings are hand-mapped (rooms named in the map editor) and seen from above, so every container in a room is visible at
  once. Ours are generated, so the room type has to come from our own floor-plan generator, and in third person the open loot needs to be seen and the closed
  furniture needs to look searchable.

## 2. Other games worth taking from

- **Escape from Tarkov: furniture with a loot theme of its own.** Filing cabinets have four drawers, each searched on its own; jackets hold keys, barter items
  and food; safes hold cash and valuables; grenade boxes only explosives; dead bodies barter items and provisions. Not every container is used every raid.
  **Keys open locked rooms with the best loot, and those rooms are where the danger is** ([wiki page on containers](https://escapefromtarkov.gamepedia.com/File:Jackets.png),
  [Tom's Guide](https://www.tomsguide.com/news/escape-from-tarkov-loot-guide)).
- **DayZ: loot points belong to the building model.** Each building type has a fixed group of spawn points (position and rotation inside the model) with a
  cap on how much it holds (`lootmax`); items carry usage tags (Military, Police, Medic, Industrial, Town...) that tie them to kinds of place and value tiers
  that tie them to map regions; the economy keeps a target count of every item in the world (`nominal`, `min`, `restock`, `lifetime`)
  ([BI wiki](https://community.bistudio.com/wiki/DayZ:Central_Economy_setup_for_custom_terrains), [types.xml guide](https://xgamingserver.com/blog/how-to-edit-types-xml-and-increase-loot-on-your-dayz-server/),
  [mapgroupproto example](https://pastebin.com/xmCBGZsW)). The value-tier-by-region detail is from a hosting company's guide, not verified.
- **Fallout 76: a shared world.** Bethesda said world loot respawns on a fixed schedule for everyone, not per reload; players report about 24 hours for
  containers and a hidden per-character "recently looted" list (fan observations, not confirmed) ([GamingBolt](https://gamingbolt.com/fallout-76-new-details-house-looting-camps-and-dying-during-quests/amp),
  [Steam thread](https://steamcommunity.com/app/1151340/discussions/0/2259061617882007715)). This is the MMO problem: the first player through empties the building.
- **Bethesda's dungeons: the loop back.** The exit sits near the end (often above the entrance) so the player is not walking back through cleared rooms
  ([PCGamesN](https://www.pcgamesn.com/fallout-3/fallout-3-washington-dc)); the end is a boss and a "boss chest". Players say that once every dungeon is
  "fight, boss, chest, exit" it wears thin (a player discussion, anecdotal),
  so the end must vary.

## 3. Level design: a building as a level

From [The Level Design Book](https://book.leveldesignbook.com/):
- **Critical path**: the main route, the content every player should see; plus optional side rooms with rewards, which make a linear space feel explored.
  **Loopbacks** (a route that circles back to the start) are "most common in open world dungeons" ([flow](https://book.leveldesignbook.com/process/layout/flow)).
- **Encounters** are "a sequence of systemic challenges that support a variety of player tactics", with a before, a during and an after. Give the player a
  **foothold** to read the room first: the player ambushes visible, unaware enemies from the entrance; or the enemies ambush the player (empty room, they
  come once the player is past the middle; keep these fair); or a **vista with a one-way entrance** for bosses. Use **few enemy types at once**: one teaches,
  two make a relationship, three is complex, five is chaos ([encounters](https://book.leveldesignbook.com/process/combat/encounter)).
- **Pacing**: start low; alternate high and low ("players adjust to prolonged periods of high intensity"); a quiet space after a fight feels like a reward;
  one set piece is plenty; **open worlds escalate danger in concentric zones toward the objective** ([pacing](https://book.leveldesignbook.com/process/preproduction/pacing)).
- **Flow**: plausible, architecture-like circulation lets players predict a layout; a switchback stair slows the player and gives a ranged enemy a position.
- From the earlier interiors research (Fallout): a building has a programme, **a gate with its key elsewhere**, more than one way up, and an end worth reaching;
  damage (a collapsed floor, a blocked stair) makes one layout read as many places.

---

## 4. Proposal for this game (my suggestions; nothing decided)

### 4.1 Rooms with a purpose (the prerequisite)
The floor-plan generator from `BUILDING_INTERIORS.md` step 2: each building **archetype** (apartment block, office, shop with flats above, warehouse,
clinic, police station, bar, hotel, gang hideout...) gets a programme of **typed rooms** on the facade's 1.6 m grid, all a pure function of the building's
seed so every machine builds the same rooms. The room type is what the furniture and the loot hang on.

### 4.2 A furniture kit with loot spots
Each piece of furniture carries **open spots** (seen: shelves, worktops, tables, the floor) and/or **closed containers** (searched: drawers, cupboards,
lockers, fridges, safes). The loot leans by piece, as in Zomboid and Tarkov:

| Furniture | Rooms | Open (seen) | Closed (searched) | Leans to |
|---|---|---|---|---|
| Kitchen counter, cupboards | kitchen, break room | worktop | cupboards, drawers | food, drink, knives |
| Fridge | kitchen, break room | | inside | food, drink |
| Shelf, bookcase | living room, office, shop, storage | shelves | | books (skill books), electronics, shop stock |
| Desk | office, bedroom | top | drawers | documents, keys, cash, small tech |
| Wardrobe, dresser | bedroom | | doors, drawers | clothes, wearables, bags |
| Bedside table | bedroom | top | drawer | pistols, cash, notes |
| Bathroom cabinet | bathroom | | cabinet | medical |
| Locker | police, gym, staff rooms | | locker | weapons, armour, uniforms |
| Weapon rack, crate | police, gun shop, gang hideout | rack | crate | weapons, ammo |
| Workbench, toolbox | garage, warehouse | bench | box | tools, parts |
| Cash register | shops | | till | cash |
| Safe | office, back room, penthouse | | locked | rare and up (needs a key, a code or a skill) |
| A body | anywhere | | pockets | whatever its story says |

Most of the kit can be **built in code** from boxes and panels, like the city itself: cheap, one shared material, part of the chunk mesh, and nothing for
the owner to model. Special pieces (a shrine, a vault door, a boss's throne) come from Meshy, reduced and baked like the Knight Helmet.

### 4.3 What spawns
**Room type x furniture** picks a table (Zomboid) -> a category -> an item -> its **rarity** (the grey-to-gold system), shifted up by the building's
**tier** (district, height, special buildings: DayZ's value tiers, the Level Design Book's concentric danger) and by **depth** (better toward the building's
end). Every table has some junk. Open spots roll fewer and plainer things; closed containers more, and the best are locked.

### 4.4 Finding it, in third person
- **Open loot**: on the shelf, the table, the floor; walk up and press E to take it, F to inspect it; a faint glow in its rarity's colour so the good things
  catch the eye.
- **Closed loot**: look at the drawer or locker and press E: a short search (half a second to two seconds by size, cancelled by moving or being hit; other
  players see you doing it), the drawer slides open, a list of what is inside. Locked ones need a key, a code from a note, or a lock skill.

### 4.5 A building is a level
- **Way in**: the lobby or a window; low intensity, a few small finds, maybe a building directory (where the good rooms are).
- **The middle**: floors of typed rooms; enemies in some rooms, placed by the encounter rules (visible and unaware, an ambush room, never more than two or
  three enemy types at once); side rooms off the route with rewards.
- **A gate**: a locked floor, door or safe; its key or keycard is elsewhere in the building (on an enemy, in a desk drawer, a code on a note).
- **The end**: the building's best room (penthouse, vault, the gang leader's office, a boss), with its best container. Varied: not always a boss.
- **The way out**: a roof exit, a fire escape, the second stair or a drop: a loop, so you do not trek back through cleared floors.
- **Cleared**: enemies dead and the end room taken; the map marks the building cleared, which gives the feeling of finishing a level.
- **Stories** (Zomboid's building and survivor stories): a chance per building of already ransacked (trashed, emptied), a barricaded survivor (a body, a journal,
  a good stash), a gang hideout (more enemies and weapons), a burnt floor, a flooded basement, a shrine (lore). They change the loot and the enemies and tell a
  story without words.
- **Tiers**: buildings on the outskirts easy with plain loot, core towers hard with the best, as the Level Design Book's concentric zones.

### 4.6 In an MMO (decided by the owner, 2026-10-22)

**Decided: A, whoever gets it first**, with group quests and their bosses giving loot to everyone in the group (`docs/GAME_DESIGN.md`, Loot, refills
and repairs). The options as they were weighed:
- **A. Shared loot** (DayZ, Fallout 76): first come, first served; containers refill on a timer. Real competition, but the first player empties the building.
- **B. Personal loot**: every player gets their own roll in every container. It can be rolled on the server from the world seed, the container, the player
  and the respawn cycle, so nothing is stored until something is taken and a cheat cannot invent loot. Everyone can clear every building; nobody camps doors.
- My suggestion: **B for containers and open loot, A for a few contested prizes** (a vault, a world boss's chest), with containers refilling per player after
  some hours.

**Built 2026-10-30:** step 4, closed containers (search, open, list), in Old Guy's house first: docs/LOOTING.md (owner: "i want the looting to be detailed
like skyrim or project zomboid, you have look it cabinets and drawers and stuff").

### 4.7 Order, when the owner says go
1. Floor plans with typed rooms, per building archetype (no furniture yet), with a test that every room is reachable.
2. The furniture kit, built in code, placed by room type (pieces against the walls first, then free-standing), with collision.
3. Rarity, item stats and the loot tables; open loot on the furniture; the F inspect card. (Rarity and the card can also start before 1 and 2.)
4. Closed containers: search, open, list.
5. The level structure: gate and key, the end room, the way out, enemy and loot markers, "cleared".
6. Stories.
7. The MMO rules on the server: personal rolls, refill timers.
Each step with tests, renders, and the chunk build cost measured before and after (interiors are the next big cost: `BUILDING_INTERIORS.md`).

### Open questions for the owner
1. Shared or personal loot (4.6)? Does loot come back, and after how long?
2. Searching: instant, or a few seconds of risk (Tarkov-style tension)?
3. Enemies in every building, or some quiet buildings that are only looting?
4. Furniture mostly built in code (simple shapes, cheap) with Meshy for special pieces, or Meshy for all of it?

---

## 5. Claiming rooms and buildings, and taking furniture home (owner, 2026-10-18)

The owner's decisions: "instead of claiming a building i think first you have to claim a room. and there should be some requirements to claim a room like
you must place a bed in it or something. and in order to claim a whole building you have to clear the whole thing out of enemies and make sure it has
electricity or something. there will also be furniture items you can pick up and bring back to decorate your house and room".

- **A room first**, with a requirement (a bed placed in it, for example).
- **A whole building** once it is cleared of every enemy and has electricity.
- **Furniture is loot too**: picked up in buildings, carried home, placed to decorate your room and house.
- This replaces the prototype `/claim` (instant, whole building, no requirement, ported from Godot).

### How it fits what exists or is planned (my notes)
- **The bed is your sethome** (owner, 2026-10-18: "there is no respawns though i guess the bed would be your sethome for teleport crystals"): a teleport
  crystal takes you to it (GAME_DESIGN.md, Getting home). `ClaimSystem.ClaimedSpawnPosition`, ported from Godot and unused, is where that spot would live.
- **"Cleared" is already in the level plan** (4.5): the same state that marks a building cleared on the map is the first condition for owning it.
- **Electricity can build on the room lights**: buildings already have powered and dark rooms (`docs/ROOM_LIGHTS.md`). Ways to give a building power, to be
  decided: a generator brought in and fuelled (Project Zomboid does this), the building's breaker box repaired, or power from a station the faction holds. A
  powered, claimed building would light up at night, which other players can see from the street.
- **Furniture is the same kit** the rooms are furnished with (4.2): what the generator puts in a room is what a player can take. The carry already exists
  (`GrabCarry`, hold E); a bed or a sofa could be carried in the hands and slow you, or packed into the inventory as an item and placed with a preview.

### What it needs that does not exist yet
- **Rooms with a type**, and the furniture kit (4.1, 4.2): the same first steps as the loot.
- **Player-placed things that last**: a placed bed must be seen by every player and still be there tomorrow, so the server has to store what each player put
  where (the doors and glass are a small shared overlay today; this is a bigger one).
- **Taken furniture**: once a sofa leaves a building, that spot is empty for everyone (or refills after a while), which is the same shared-or-personal question
  as the loot (4.6).
- **Power** as a real state per building, not only a lighting look.

### Open questions for the owner
1. Can several players claim different rooms in the same building (neighbours in an apartment block), and who can then claim the whole building?
2. What happens to a claimed room when other players walk in: can they take things, break the bed, unclaim it? And to a claimed building (the faction rules
   already say no offline raiding: sieges only in windows the defenders chose)?
3. Electricity: generator, repair, a power station, or several of these?
4. Moving furniture: carried in the hands (slow, visible, can be dropped in a fight) or packed into the inventory?
5. Is there a limit on how much furniture a room or building can hold (it matters for performance with many players' homes in one area)?


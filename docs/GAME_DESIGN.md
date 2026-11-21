# CITY X: game design and roadmap

The whole-game plan, agreed with the owner on 2026-10-08. Per-feature notes live in the other `docs/*.md` files; this one says what the game is, how its systems fit together, and in what order they get built. **Nothing in here is built unless it says so.** Read it before starting any new system, and update it when a decision changes.

## The pitch

An online megacity where you level forever and get absurdly powerful, and where everything you have can be lost. Build your own fighting style from dozens of skill trees, join a faction, fight wars, explore a world that goes down to hell and the voids and up to space, and hunt the one player in the world who holds a god-tier spell.

**The core loop:** grow, risk it, fight, lose it or take it. Infinite levels give you something to grow; real death penalties put it at risk; karma decides who you fight; factions give fights a reason; god-tier spells are the prize that sets everyone off.

**The goal of the game is to be the most OP.** Tone (eerie, meta, TempleOS / "Poorly Planned") is the undertone, not the point.

## Lore (owner, 2026-10-12)

The owner's words, lightly tidied:

- **The world.** Terraplex 9 is an artificial exoplanet built in the year 30,000, nine times the size of Jupiter.
- **The rapture.** The Demiurge, the false god who knows of no god greater than itself, has awoken and begun the great rapture. The endless megacities are left mostly abandoned. Spirits, monsters and leftover AI and machinery now fight for control of this abandoned space station / artificial planet.
- **The players.** They are fallen angels who fell from the Demiurge's domain in the wake of the rapture, and have forgotten their memories. They must regain their strength and powers, and decide whether to join the Demiurge or fight for something greater.
- **The Organization.** A mysterious group of artificial superintelligences implanted into human-like artificial bodies.

How it lines up with what is already planned (not yet discussed with the owner):

- karma's angel/demon trees could read as siding with the Demiurge or against it;
- Heaven, at the top of the world, may be the Demiurge's domain the players fell from;
- "regaining your powers" fits starting weak and levelling forever.

## Servers

- **One world, its servers placed for America and the West** (owner, 2026-11-12): no regional worlds; players elsewhere join the same world at a higher ping. (China would need a licensed Chinese publisher and servers inside China, so it could only ever be a separate world: a question for much later.)
- **Official servers only.** No player-hosted or player-created worlds (decided 2026-10-08).
- **Launch: one server** in the region where most players are. Add regions when a real crowd appears elsewhere. Aim, long term: one big world.
- **Path to one big world:** each zone layer can later run on its own server machine (EVE runs each solar system on its own node); the gates between zones are the handover points. This only works if character saving is a **shared database any server can read**, never state held inside one server.
- The game already sends you only the players near you (player interest management).

## The world

Infinite both ways. Each zone is very tall (hours to cross). **No digging, no building**: every way down is found, behind a gate that takes curiosity or knowledge to open. Difficulty rises the further you go from the surface.

**Down:** surface (starter city, Cybertown, mossy Ruins; a gothic district was mentioned once, unconfirmed) → Sewers → Caves → Pillars → Underground city → Underground sewer → The Deep → Mantle → Hell → The Black (zero-gravity void) → The White (endless).
**Up:** surface → Clouds → Sky city → Stratosphere → Heaven → Space (endless; alien cities spawn by chance).

- **Zone-exclusive items**, and **skill trees found in zones** (read a tome; the game already has readable books).
- **Boss dungeons:** fixed ones placed by the world seed (same for everyone), plus rare timed event dungeons announced server-wide.
- **Waypoints:** the first time you find a zone's way down, it becomes a waypoint you can return to. Depth is earned once, not every session.
- **The two endless ends (The White, Space)** are the infinite difficulty sink that infinite levels need.

**Builders (generators) behind the zones**, which is what makes 17 zones affordable:

| Builder | Zones |
|---|---|
| City (exists) | starter city, Cybertown (new styles + neon), Ruins (decayed + moss), Underground city (with a ceiling) |
| Tunnel maze | Sewers, Underground sewer |
| Caves | Caves, The Deep, Mantle (lava) |
| Void | Pillars (+ pillars and torches), The Black, The White, Clouds, Stratosphere, Space |
| Floating islands | Sky city, Heaven |
| New city builder | alien cities, maybe Hell |

**The streets' gutters and manholes go with the Sewers (owner, 2026-11-06):** "we should save street gutters and manholes to be apart of
when we build the sewers" (docs/AMERICAN_STREETS.md).

**The Sewers have their own notes since 2026-11-09: docs/SEWERS.md** (the owner's words on the entrances, the flood tunnels and the dark,
the research, the design, and what is built: tunnels under every road, rooms, manhole ladders, mouths under bridges).

**The Sewers' look (owner, 2026-10-31):** the "fake liquid" streams (a bent, turning torus whose procedural texture drives its displacement and
normals; Tuatara Games' realtime-VFX post) belong in the sewer level: water pouring from pipes and grates into the channels.

Enemies are the real content cost, not terrain (see Enemies).

## The sky hub

Owner, 2026-10-22: **a giant island in the sky**, set up like a Minecraft factions server's spawn. **No PvP damage and no damage to anything there** (no broken glass, no props knocked over). **You jump off its edges and plummet down into the world.** There will also be a way to be put somewhere random: a random location portal, or a command like `/random` (not decided).

**Owner, 2026-11-16: a floating space station / city in the sky, not a rock island**: "what if instead of the hub being an island in the sky it was some sort of giant futuristic space station", then "the size of like orgrimmar or stormwind in wow. but like a floating space station/city in the sky", and "the height should be around the clouds though, maybe right above". So it is futuristic, about as big as a WoW capital, and hangs at or just over the clouds (the island built now sits at cloud height, `docs/SKY_HUB.md`), not up in Space (which stays the top layer). Not decided yet: whether the jump off the edge stays, and whose station it is in the lore.

**Owner, 2026-11-16: no staircase; a port on the ground and a flying ferry instead**: "i kind of dont like the staircase, i think on the surface somewhere below the hub will be some type of airport or something like that, and some type of giant flying transportation device will travel between the hub and the surface every 15 minutes or something. This will be where the guards that stop player fighting will be and will be akin to the boats in world of warcraft that take you to different islands where a bunch of players can go onto the boat and travel at once, however many can fit." So the spiral staircase (built 2026-11-02) goes when the station is built; the port below takes its place as the walk-in way up, and the guards move from the stair to the port. The ferry runs on a timetable (about every 15 minutes), carries as many players as fit, and everyone on it rides together, as on WoW's boats.

**Camping the port (owner, 2026-11-16):** the owner's worry: "some high level player is just going to camp the ferry and kill everyone who comes on once he is strong enough to fight the guards". The owner turned down making the port and ferry a no-damage zone ("thats lame though") and picked **a bounty**: attacking at the port marks the attacker on everyone's map with a reward for killing them, so the camper becomes the hunted and the players picked off have a reason to come back together. The port stays a place where fighting can happen. **The bounty lasts until the marked player dies** (owner, same day: "it should last until they die but there should be some way to get rid of your bounty but very difficult ... some sort of rare npc that can give you a new identity or something"). It must survive logging out, or logging off would clear it. Not decided: the reward's size and who pays it, how far round the port it counts, the identity NPC's details (where and how often it appears, what it costs, what changing identity does to you).

**NPCs you befriend and bring home (owner, 2026-11-16):** "im planning for there to be a bunch of npcs scattered across the map and you can befriend them and do their quests if they have any and eventually through some process you can get them to come back with you to your home and utilize whatever they offer. so like maybe there is one guy the "smuggler" or something who can reset your identity and notoriety but you have to change your username or something." So the bounty's way out is one of these NPCs: reset identity and notoriety, at the price of a new name. Not decided: how befriending works and what "some process" is, how many NPCs and what each offers, whether a recruited NPC can be used again and again or each use costs something (if free at home, the "very difficult" part is recruiting him once), and whether the new name is the character's or the account's (friends lists and factions follow the account).
**You don't pick the new name (owner, same day):** "maybe you dont even get to select the name. you have to pay him and he is like "ok here is your new identity:"", the feel of Breaking Bad's disappearer, whom Saul puts you in touch with. You pay, and he hands you a name the game makes up (unique, never one already taken). **It is the character's name that changes** (owner), and the names are plain, generic ones ("Stan Johnson, or something"), so in time players learn to spot who has been to the smuggler. **The price (owner):** pretty expensive, but affordable for someone who has the smuggler at their base; the exact number waits for the currency system, which is not designed yet. Offered alongside it and not picked: guards that scale to the attacker, endless stronger reinforcements, a heavy karma cost. My reading of the size (not the owner's): WoW gives no official city size; crossing Stormwind or Orgrimmar on foot takes about 2-3 minutes, so roughly 1-1.3 km across at this game's sprint (7.5 m/s), against the 600 m island built now; big open decks, not just corridors, so the crowd fits.

Owner, 2026-11-01:
- **The hub is a vibe to hang out at**: the social place between trips down.
- (Replaced 2026-11-16 by the port and the ferry, see above.) **A giant spiral staircase leads up to the island**, so anyone can walk home; you come down by jumping off, with no fall damage. **Island and stair built** (2026-11-02, `docs/SKY_HUB.md`): round, as high as the clouds, the stair round its circumference, its foot on a flat lot off the roads and clear of every building; nothing on the island yet.
- (Replaced 2026-11-16: no staircase, a ground port and a flying ferry; the guards are at the port. See above.) **The island is the safe zone; the staircase is guarded.** Attacking on the stairs is possible, but guards go after whoever attacks someone not fighting back ("gives me a chance to design some cool iconic guards of the safe zone": the owner designs them). The city below is free for all.
- My suggestions (not decided): a gate at the top of the stairs where the safe zone starts, with a "Safe Zone" message; a barrier round the island that stops shots from outside; no entering for about 15 s after a hit (combat tag); a flagged attacker cannot enter the hub until the flag wears off (ties into karma); four staircases, one per corner tower, to spread out campers.

My suggestions (not decided): the hub gives you a parachute for the drop (or a drop from the hub never does fall damage); the edge you jump from decides roughly where you land, so the island is a launch pad over the starting region; the fall is the intro's skydive (WASD drift, clouds thinning); a portal fits the world better than a command (keep `/random` for testing), it only sends you somewhere on your current layer and has a cooldown, so it is no escape from a fight; camping under the hub is held off by the island's size spreading drops out, spawn protection on landing, and karma (killing someone not fighting back turns you red, and reds cannot enter the hub).

- A floating safe city above the starter city: trading, socialising, faction recruiting, the market board, basic skill trainers.
- **You leave by jumping off with the parachute** (exists) to a random spot, or to your faction's base.
- You **respawn here** after death.
- **Spawn protection** for a few seconds after landing, or until you attack.
- **Crowds (owner, 2026-10-22): hoping for a lot more than 100 at once**, with space for everyone to stand, and fun things: parkour, shops, maybe a duelling arena, maybe players' own shops. My suggestions: aim for **300-500 in one hub** (other games: Minecraft hubs 100-200 a copy, FFXIV and WoW cities a few hundred a copy; over 1,000 in one place is rare); a **main island about 600-800 m across** (1.5 min sprinting edge to edge) with districts so the crowd spreads out (plaza where you arrive and respawn, NPC shops and trainers, the player market, faction hall), and **side islands** for the duelling arena (40-50 m ring with stands, small 1v1 pits) and parkour (falling off a course puts you back at its start, never down into the world). Player shops: rented stalls that show what the owner has listed on the market board, so buying works across copies and while the seller is offline (not decided).
- **Copies** only as a safety valve, opened when one is truly full; the cap is whatever the crowd test (Technical foundations) shows the game holds, not a guess. The market board works across copies.
- **Text chat only** in the hub at first (voice everywhere else).

## The tutorial (owner, 2026-10-22)

The owner's plan:

- **Old Guy asks you to help clear out his house.** Inside you find basic clothing and a basic weapon (a baseball bat, a kitchen knife).
  **Update (owner, 2026-10-30):** the three starter weapons, **the Kitchen Knife, the Magic Wand and the Slingshot**, are all findable in the tutorial building, and
  players keep using whichever they like best; **the Butter Knife and the Baseball Bat are hidden** somewhere in it. The owner wants the tutorial building
  carefully designed by hand (reference: Garry's Mod maps), not generated like the rest of the city, whose interiors (bar the staircases) are barely started.
- **He explains the basic mechanics** the game needs: spells, skill trees, stats.
- **The faction.** He explains factions and suggests starting one together. If you say yes, **he has you create the faction** (you lead it, he joins), then teaches claiming a base. Claiming it ends the tutorial proper.
- **The cutscene.** Someone bursts in the door, shoots Old Guy dead and aims the gun at you. Someone else shoots magic through the gunman's head and saves you at the last second. More lore follows and leads to them showing you **the hub**, where the tutorial ends.
  **Replaced (owner, 2026-11-02), a comedic bit instead:** just after you think you have reached the end of the tutorial, Old Guy says something like "alright i think we're safe now", and bullets come raining in through the windows and kill you both at the same time. So no gunman at the door and no rescue at the last second. **Then (owner, same day):** you wake up in Heaven or Hell, and the rescuer brings you from there to the hub and revives you.
- **Old Guy dies so that every player does not have their own Old Guy** forever in their faction. The faction you created stays yours.
- **Freedom to leave.** You can skip the tutorial and walk away, but once you are a certain distance from him you cannot go back. You can also say no to him several times through the tutorial: he says his lines, walks away and disappears once off screen.
- **Nobody else is involved.** Old Guy is invisible to other players, and a player doing the tutorial must not be player-killed.
- **About 10 minutes**, give or take. **Starting the game throws you straight into it**: no party invites or lobby on the title screen, and no doing the tutorial together with a friend. Friends meet up after it (or early, if one refuses it).
- **Saying no at the start** (when you first talk to him outside, or never talking to him at all): he disappears at some point once off screen and you are moved into the full multiplayer game with no tutorial.
- **The tutorial ends in the hub**, not with a jump off it (owner): the rescuer may show you the drop, but the first jump is your own.
- **You can leave the tutorial at any time by walking away** (owner, 2026-11-01), into the real game.
- **Old Guy's house is in the real city too** (owner, 2026-11-01, "if you walk away and then walk back and the house has changed or is gone wouldnt that be weird"): the same building on the same spot, abandoned, Old Guy gone. **It is claimable there, and it stands by the hub's drop-off, a dangerous area**: "like making your faction base right outside the spawn in minecraft factions". **No new-player protection** (owner, same day). **A player who skips meets the real world once they move a certain distance from the starting area** (owner, same day), not on the house's doorstep.
- **He asks about the faction inside the house, after you clear it**, so the cutscene still makes sense whatever you answer. (My reading, to confirm: with yes you create the faction and claim the house before the door is broken in; with no the cutscene follows straight away.)
- **Karma choices in the tutorial, deciding between Heaven and Hell** (owner picked these two from my suggestions, 2026-11-02): **the enemy in the house begs**: at low health he drops his weapon and pleads for his life, and you spare him or finish him (the swordsman is only a temporary model for that enemy, owner, same day; who it really is is still open). **The boxes in the basement**: Old Guy says something like "i have some boxes in the basement dont touch" (owner: not his wife's things, as I first suggested); you can open them anyway. In the boxes (owner, same day): a fun toy or unique item, still to be thought of. Not settled: how the choices add up to one result.

My suggestions (not decided):

- **The tutorial runs in your own private copy of the starting streets ("phasing", as WoW's Worgen zone does).** This is how nobody else sees Old Guy or can hurt you, and how many players do it at once without sharing a house, its loot or its claim. The copy is cheap: the city is built from the world seed, so it is the real streets with nobody else in them.
- **The lore reason: the newly fallen are "unanchored"**, not fully in the world yet, so other players cannot see or reach them. The gunman is one of the Demiurge's hunters, who can see the unanchored and kill them before they anchor; the rescuer is from whoever fights back. You **anchor in the hub**, appearing in the shared world for the first time.
- **Walking away = anchoring early:** past the edge of the private copy (about 60-100 m from Old Guy, with a warning first) you appear in the real world, Old Guy gone for good. Saying no lets you walk out the same way. You still reach the hub, as death respawns you there and it is visible in the sky.
- **Teach by doing:** vaulting on the way to the house (everyone gets it in the tutorial, see Skill trees); fighting in the house; the level-up from clearing it spends the first stat point; the first skill point is spent on the skill tree, and the spells and hotbar are taught by using them. The claim follows the real rule: the house is cleared, Old Guy has you turn its power back on, then you claim it.
- **The claim in the private copy cannot carry into the shared world** (every player would claim the same house): it is a practice claim, and your first real claim is the first thing to do after the hub, with a marker.
- **No death penalty in the tutorial:** you come back near Old Guy (the unanchored cannot fully die yet).

- **A 10-minute budget (mine):** fall and meeting him 1 min; walk to the house (vaulting) 1; clearing it and the gear 3; stats, skill tree, spells 2; the faction and the claim 1; cutscene, rescuer, lore 1-2; arriving at the hub 1. So his house is right by where you land, and each mechanic is one action and a line or two, not a lecture.
- **Where you land:** a quiet, unclaimed street with nobody near, inside a starting region under the hub (not anywhere in the endless city), so friends are never impossibly far apart; the hub is the meeting place. Later: friends and parties shown on the map, and teleport crystals that can take you to a party member who accepts (with the crystals' charge time and fight blocks).

Open: what a player who skips misses (the gear and the faction for sure; the first skill point?); who fired the bullets. The rescuer: the owner is thinking "some light entity maybe god" (2026-11-02, not final).

Exists now: the fall from the sky, Old Guy, vaulting, the baseball bat, the T-shirt, stats, skill tree, spells, a prototype `/claim`, ragdolls; since 2026-10-30/31 Old Guy's house itself with its loot (the knife, the bat, the wand, the slingshot, the drawers), its breakable door planks and windows, and its fuse box (the house is dark until its lever is thrown, docs/OLD_GUYS_HOUSE.md). **Since 2026-11-16 the whole tutorial plays through, as a first draft for the owner to rewrite** (docs/TUTORIAL.md; owner: "just looking for something to launch you into the game"): objectives on the HUD, Old Guy's lines for every step (all in `TutorialScript.cs`), the swordsman who waits until he sees you, Old Guy in his den, the power, the level-up and the stat and skill points, the faction question (kept, does nothing yet), the cellar boxes noticed, then the bullets through the den's windows, black, and the hub; Old Guy is gone for that character after. Not yet: the private copy and its edge, the "no" branches past the first offer, the claim itself, factions, the begging swordsman, Heaven or Hell and the rescuer (a black screen stands in), who fired.

## Getting home

- **Teleport crystals** (an item: works on controller, dropped on death, tradeable) with a hotkey or menu shortcut.
- **10 s charge**, cancelled by taking damage or moving. **Blocked** for 15 s after any player fight, during a siege, and in enemy territory.
- Takes you home or to waypoints you have found yourself, never anywhere deeper. **Home is your bed**: the bed in your claimed room is your sethome for teleport crystals (owner, 2026-10-18; it is not a respawn point). Without one, the faction base, else the hub (the order is not decided).
- **Owner, 2026-10-30 (how you get to the hub, also if you skip the tutorial):**
  - **Crystals are found, not handed out: fairly common but not super common.** Most NPC quests give some along with their other rewards.
  - **A crystal takes you to one of your homes, the hub, or a player you have met (if they accept).**
  - **An item like Terraria's Magic Mirror** teleports you back to your base with no crystal needed. **Super rare** (owner, later the same day; at first
    "when you medium level you will get" one).
  - **Finding your way back home after a long journey is part of the game** (owner: "i kind of like the idea of people having to find their way back home
    after a long journey"): a crystal is something to save, not something to use every trip.
  - Flying up to the sky island (jetpack, wings) is possible but high-level.
- **Owner, 2026-10-31: the ways to teleport.**
  - **Teleport crystals** (above).
  - **Portal spells:** they open a portal to whatever location you select (home, the hub, etc.). In the **Wizard** tree (owner, 2026-11-02).
  - **A Portal Gun:** the same idea, but more like the portal gun from Rick and Morty.
  - **"Godspeed" dropped (owner, same day):** a high-level light spell to anywhere on the map you had been, once every 24 real hours, was "too op you
    can just teleport into the enemies base"; "we will just get rid of it for now i will have some fast running ability later".
  - **Who goes through (owner, same day):** a portal is open to allies and faction members, and closes once you go through it, so your team has to go
    first. Playing solo and being chased, you can still use it to escape.
  - **Portals are for high-level players:** "i want it to be hard to die once you high level. once you are high level you will already have had your
    fair share of adventuring and will appreciate the fast travel back home."
  - **No block after a fight for the portal spell:** instead its cast takes a couple of seconds, and you can still take damage during it.
    **Getting hit does not cancel the cast** (owner, 2026-11-01): it only hurts.
  - **The Portal Gun has a long cooldown,** maybe about 10 hours.
  - Open (not decided): whether sieges and enemy territory block portals as they block crystals.
- My notes (not decided): a player who skips the tutorial starts with no crystals, so until they find one their ways up are dying (you respawn in the hub) or, my
  suggestion, a few free lift points in the starting streets. Whether the mirror keeps the crystals' 10 s charge and fight block is open (Terraria's mirror
  has neither, but an instant way home would be an escape from every fight).

## Character progression

### Levels and stats

Exists: level, XP (100 x level per level), 5 stat points per level (`PlayerVitals`, `CharacterSave`; saved locally, not on a server yet). Since 2026-10-14 they go into the owner's list of 24 stats (docs/STATS.md: Max Health, Health Regen, Max Stamina, Stamina Regen, Max Mana, Mana Regen, Melee Damage, Melee Speed, Firearm Damage, Reload Speed, Fire Rate, Spell Power, Cast Speed, Cooldown Reduction, Armor Penetration, Accuracy, Recoil Control, Movement Speed, Flight Speed, Vehicle Handling, Armor, Magic Resistance, Stability, Block Chance), each working in the game except Vehicle Handling (the new cars come later; Flight Speed works since the angel wings, 2026-10-28). Vitality, Endurance and Strength became Max Health, Max Stamina and Melee Damage.

Planned stats (each appears when it becomes relevant: Mana with your first spell, Command with your first summon):

- **Body:** Health, Health regen, Stamina, Stamina regen, Defense, Poise.
- **Offense:** Strength (melee), Spell Power (magic), Precision (guns, tech), Attack speed, Crit chance, Crit damage.
- **Magic:** Mana, Mana regen, Cooldown speed.
- **Movement:** Speed, Jump, Climb speed, Jetpack fuel.
- **Command:** how many summons you control.
- **Luck:** loot.

**Rules:**
- Stats help everything; skills help one style. A stat never unlocks a move, a skill never gives plain +Health.
- Flat stats (health, damage) grow forever. **Percentages cannot reach 100%**: use a curve that never arrives (armor / (armor + K)), overflow into something new (crit past 100% rolls bigger crit tiers, like Warframe), or a hard cap (Diablo 3 Paragon).
- **Speed has a ceiling**: the city builds itself around you, and an infinitely fast player outruns it. Faster than the cap comes from movement skills.

### Skill trees

Classless (like Terraria). Your **title** comes from your top one or two trees (Titan Quest style): all fire = Pyromancer, fire + sword = Flameblade; hand-name the good pairs, generate the rest.

**Kinds of skill:** passive (no slot) · ability (stamina, e.g. Sword Spin) · spell (mana, e.g. Fireball) · stance / transformation (e.g. Bloodrage) · summon · **modifier** (changes another skill: "Fireball splits into three") · god tier.

**Trees** (draft): Magic: Pyromancer, Cryomancer, Stormcaller, Tidecaller, Geomancer, Aeromancer, Voidwalker (gravity; the Almighty Push), Necromancer, Plaguebringer, Hemomancer, Lightbringer. Melee: Sword (Knight 1H+shield / Berserker 2H / Duelist dual), Brawler, Lancer, Juggernaut, Assassin. Guns and tech: Gunslinger, Marksman, Demolitionist, Engineer. Movement: Freerunner, Aeronaut, Grappler. Summons are spread across trees.

**Three groups of trees** (owner, 2026-11-12): **Warrior** (the melee styles), **Ranger** (the ranged styles: archery and throwing, guns, explosives; the owner is looking for a better name) and **Wizard** (the Wizard tree below and every magic type's own tree). How they show on the skill map is not decided.

**Wizard** (owner, 2026-11-02; 2026-11-12: "wizard would be base magic upgrades and miscellaneous spells that dont quiet fit in an element"): the base magic upgrades (what helps all magic), and the spells that belong to no element, while each magic type has its own tree: Grow and Shrink (built), the portal spells, invisibility, flight or levitation by magic alone (no wings or jetpack), and Reverse (all planned; how invisibility and the flight work is not decided). **Reverse** (owner, same day) reflects projectiles: not a hotbar spell ("it kinda would feel awkward pressing a number key to block") but a skill: once learnt, a well-timed block sends the projectile back. It costs mana when it reflects; without enough, it is a regular block. One block reflects 1 projectile to start, more as the skill is upgraded (owner). Open: what bullets do, the timing window, the mana cost, how many at each upgrade, and a timed block for wands and staffs, which have none.

**Where trees come from:** the basic trees are taught in the hub so a new player can build a real style; zones hold the rare trees and the top tiers (draft: Poison and Water in the Sewers, Earth in the Caves, Shadow in Pillars, top Fire in the Mantle, Necromancer and Blood in Hell, Void in The Black, Wind and Lightning up high, Light in Heaven, Ice in Space).

**Today's number keys are placeholders for tree unlocks:** 1 Fireball, 2 Sword Spin, 3 Ice Shards, 4 Poison Breath, 5 Acid Rain, 6 Octaslash, 7 Sword Wave, 8 Bloodrage.

**The skill menu** (owner, 2026-10-14): one big menu holding every tree, the trees side by side like Skyrim's constellations, each growing upward from its base. Not a fixed perspective: click and drag to move across it, and scroll to slide along the row. (The owner first asked for top to bottom, then kept Skyrim's side by side.) My suggestions, not decided: the row wraps around like Skyrim's; zoom with Ctrl+scroll or buttons, out to the whole row; a hybrid tree sits high in the gap between its two parents (Steam between Pyromancer and Hydromancer), so the row's order puts partners side by side; a learned star goes on the bar by dragging it there (or clicking it, then a slot); on a controller the stick moves around the map and the bumpers jump from tree to tree.

**Freerunner** (owner, 2026-10-14): vaulting is the first thing you unlock, in the tutorial, so everyone has it; from it the tree splits into three paths: Dash, Climb and Super Jump. For now one character can learn every path; what gets locked is decided later. Pulling yourself up onto a ledge is part of Climb (owner; I had suggested Vault), so a new character vaults over low walls and through windows but cannot pull up onto ledges, scale walls or climb into high windows until they take Climb: the tutorial and the starting area must not need climbing. My suggestion, not decided: Wall Stick on the Climb path.

**Built 2026-10-14** (docs/SKILLS.md): the skill map in the C menu (Player, Skills) with all eight trees, learning with skill points (one every second level, a new character starts with one), a free reset, the bar in the bottom-left corner, and Dash, Climb, Wall Stick, Super Jump and every spell locked until learnt. Characters made before it keep everything they had (which never included the charged jump).

### Infinite upgrades

Every skill that makes sense can be ranked up forever. The cost curve is the most important dial in the game. If you pour one point per level into one skill:

| Level | Cost doubles each rank | Cost +10% each rank | Cost +1 each rank |
|---|---|---|---|
| 100 | rank 6 | 25 | 13 |
| 1,000 | 9 | 48 | 44 |
| 10,000 | 13 | 72 | 140 |
| 1,000,000 | 19 | 120 | 1,413 |

**Recommended rule: cost and reward grow at the same rate** (each rank costs 10% more and gives 10% more), so every point is worth the same forever. Late-game numbers will reach millions and billions; deeper zones scale with them.

**Variety does not come from ranks** (two rank-50 Fireballs play the same). It comes from: a **limited hotbar** (Guild Wars 1: ~1,000 skills, 8 slots), **modifiers** (Path of Exile support gems; 2-3 per skill), and **element reactions** (wet + lightning = shock, frozen + heavy hit = shatter, poison gas + fire = explosion, fire weaker in rain; rain wetness already exists).

**Skill points are only awarded the first time you reach a level** (peak level), or dying and re-levelling would farm points. Allow respecs (cheap early, pricier later).

### Hotbar and weapons

- **Slots on the number keys 1 to 0, all equal** (owner, 2026-10-14: 9 and 0 should be usable). **No ultimate slot**: that was my suggestion, never the owner's. God-tier abilities (the owner's idea) sit in an ordinary slot. The slot count and the controller layout are still open. Must work on a controller (consoles later).
- **Bottom left of the screen, for now** (owner, 2026-10-14), with round icons rather than World of Warcraft's squares (the owner's idea, like DC Universe Online). In the skill menu the bar sits in the same corner (my suggestion).
- **One bar per weapon** (Elder Scrolls Online; my suggestion, not decided): switching weapons switches the bar. Weapon abilities need their weapon in hand; spells do not.
- **Movement stays off the bar** (owner, 2026-10-14): Dash, Climb and Super Jump keep their own controls (double-tap Shift, hold Space). Whether later movement spells are meaningful enough for a slot is decided later.

### Fighting styles and loadouts (2026-10-25)

**The owner's decisions:**
- Every player has **two loadouts: a primary and a secondary weapon**.
- Your **specialization is your class**; a **hybrid** is a class mixing two fighting types (magic + swords, guns + swords).
- You can **specialize in any fighting style**. Each weapon type climbs the rarity ladder (a grey baseball bat, then a green metal or wire bat, and so on), the same kind of progression for every type. Some types stop low (no legendary slingshot): you move up to another weapon that shares the skills and stats you built.
- Skill tree **paths to specialize** in one weapon type (swords or scythes), and **skills that upgrade a whole group** (all two-handed melee, all two-handed ranged).
- Late game: **so strong you dual wield weapons that were two-handed only** (two greatswords).
- The owner's list of styles: one-handed guns (pistols, small SMGs), dual wield guns, two-handed guns (ARs, snipers, minigun), explosives, laser guns, bow and arrow, crossbow, slingshot, boomerang, one-handed melee (1H swords, knives, 1H axes, crowbar, metal pipe, baton), two-handed melee (2H swords, scythe, golf club, bo staff, 2H axes, baseball bat), dual wield melee, magic staffs, punching.
- Decided for pistols (owner, 2026-11-19: "it will be a skill you have to unlock on the tree"): Dual Pistols, the root of the Gunslinger tree (the owner's name for now), docs/DUAL_PISTOLS.md. Still unsure for melee.

**My suggestions, not decided:**
- **Three layers.** A **style** is a skill tree (what you specialize in; your top one or two make your class). A **moveset** is how a weapon is held and swung (its animations), shared by many items. An **item** is one model with a rarity and stats. Already how the game works: the crowbar uses the combat sword's moves, the bat the greatsword's.
- **Each style has a full rarity ladder, not each item type**: the slingshot is the grey rung of Archery, and upgrading to a bow keeps every Archery skill. A style is decided by its skills (the crossbow fires bolts: Archery), a moveset by how it is held (the crossbow like a rifle). **Laser is not a style**: it is a high-rarity version inside other styles (laser pistol, laser sword, laser minigun). **Dual wield** is a branch of its one-handed style using the same items (two crowbars, two pistols).

| Style | Branches | Example ladder |
|---|---|---|
| Brawler (punching) | Boxer, Grappler | fists, brass knuckles, gauntlets |
| One-Handed Melee | Blades, Blunt, Knives, Dual Wield | butter knife or crowbar, combat knife or combat sword, laser sword |
| Two-Handed Melee | Greatswords and Axes, Blunt, Reaper (scythe), Staff Fighting (bo staff) | bat, nail or wire bat, tactical greatsword, combat scythe, demon scythe |
| Handguns | Gunslinger, Dual Pistols | Glock, Deagle, laser gun |
| Long Guns | Assault (AR, SMG, shotgun), Marksman (snipers), Heavy (minigun) | AK-47, Dragunov, Barrett, minigun, laser minigun |
| Explosives | Launchers, Grenades and Mines | grenade, RPG, RPG minigun |
| Archery and Throwing | Bows, Crossbows, Throwing (boomerang, knives) | slingshot, longbow, compound bow or crossbow; plastic, wooden, steel boomerang |
| Magic Weapons | Wands (1H), Staffs (2H) | magic wand, magic staff, flame, frost or arcane staff |

  The element trees (Pyromancer and the rest) stay separate and work with any loadout; a staff makes them stronger. These styles replace the weapon trees in the "Trees" draft above.
- **Three levels of upgrade**: a **stat** helps every weapon of its kind (Reload Speed for the crossbow and the AK alike); a **trunk skill** helps every weapon in one style ("Heavy Hands": two-handed melee hits harder and staggers longer, greatsword and scythe alike); a **branch skill** helps one weapon type ("Reaper's Pull": scythe sweeps drag enemies in). **Bridge skills** sit in the gap between two neighbouring trees and help both ("Steady Hands" between Long Guns and Archery: faster reloads, less sway with any two-handed ranged weapon). Trunk skills should change how you play where they can (new effects, modifiers), with plain percentages as the steps between.
- **Damage stats by kind**: melee styles use Melee Damage; guns, explosives and archery Firearm Damage (perhaps renamed Ranged Damage); wands and staffs Spell Power. Switching weapons within a kind loses nothing.
- **Dual wield is unlocked**: the first node of the Dual Wield branch (and Dual Pistols on Handguns). Before it, one weapon and a free off hand. A pair is one loadout (two crowbars are your primary).
- **Improvised weapons** (owner, 2026-10-25: a broken bottle you can throw, "if you dont have anything else and are in a pinch"; not something to specialize in): no tree and no rarity ladder; anyone uses them at full strength with plain stats (Melee or Firearm Damage), and no style's skills apply. Odd specials like the flamethrower are the same unless the owner gives them a home (my suggestion: Long Guns' Heavy branch, or the bottom of a fire line with the flame staff).
- **Tags decide what a skill boosts** (owner, 2026-10-25: fire damage from the fire tree should also reach the flamethrower, as should a "two-handed ranged weapons do more damage" skill). Every item carries tags (the flamethrower: Fire, Two-Handed, Ranged; the AK: Gun, Two-Handed, Ranged; a broken bottle: Improvised, Thrown) and every boost names the tags it applies to ("+fire damage" hits anything tagged Fire: Fireball, the flame staff, the flamethrower). Path of Exile works this way. A style is then the tags its skills ask for, an improvised weapon simply has no style tag, and a new weapon fits every skill its tags match with no special code.
- **How many combinations** (counted 2026-10-25): 8 weapon styles and 11 element trees make 190 classes (19 pure, 171 hybrids; 561 if each branch counts as its own); 36 style loadouts; the 45 weapons on the rarity list make 990 two-weapon loadouts, before dual wield and skill choices. All from about 12 movesets.
- **Titan's Grip** (World of Warcraft's Fury warriors dual wielded two-handed weapons): deep in the Two-Handed tree, needing many points there and the Dual Wield node; two greatswords at once. Its own moveset, so it is built late; balanced by more stamina per swing or no block.
- **One moveset at a time**, finished properly: about 12 movesets cover every item on the rarity list, and a finished one makes each new item mostly a model and numbers. Order for the December goal (fists, sword, fire): one-handed melee (the combat sword), fists, fire magic, then the pistol, then two-handed. Skill tree branches come after a moveset feels good.

### Staffs, magic weapons and enchants (2026-10-27)

**The owner's ideas (still being worked out, "just thinking out loud"):**
- **A hotbar spell is one cast**: press Fireball, one fireball. **A staff's attack is the spell on click**: a "Staff of Fireballs" shoots a fireball each click, every shot draining mana. The same for ice shards and other projectiles. Not every spell has a staff version.
- **Many staffs per element**, each with its own attack: fire has the Staff of Fireballs, the Staff of Flames (a flamethrower-like stream), the Staff of Lava, and more. Beside a flame staff, a spell that is one burst of flame.
- **An element staff boosts that element's spells** cast while holding it (a fire staff, every fire spell). **General staffs and wands** have no element and boost every spell cast through them (for example, less mana for all of them). Owning a staff that does a spell's job frees that slot for a better spell.
- **Magic swords and guns later**, and **enchant spells** that give your next hit, or your next bullet, an element.
- **Charged Blade**, an electric sword: holding click charges instead of the heavy attack (like Cloud's down special in Smash Bros); held 1 second, a mini boost; held to the maximum, a large boost. The charge stays until the next slash, which deals the extra damage, or until a minute passes unswung. You cannot move while charging; a high-level skill may unlock it ("pretty op"). Electric boosts from the skill tree, and perhaps Magic Damage among the stats, raise its damage. Other players see the charge. What happens to a held charge: a **dash** keeps it; **being hit** keeps it; **switching weapons** loses it; a **charged slash that misses or is blocked** spends it, and when blocked you are not staggered, or staggered for half as long; **blocking a blow while charged** spends it on the attacker, a boosted stagger and a little electric damage.

**My suggestions, not decided:** the charged slash is tagged both sword and Electric, so sword skills and electric boosts both raise it (by the tags rule above); a wand is one-handed with a smaller boost and leaves the off hand free, a staff two-handed with a bigger one; all staffs share one moveset, so a new staff is mostly a model and its attack. Open: what becomes of the flamethrower beside a flame staff.

**Built 2026-10-27** (docs/STAFFS.md): the Arcane Staff (Arcane Orb on the click, charged to 2 and 3 orbs), the Fireball Staff (held, a bigger fireball with a blast, up to 1.5 s) and the Ice Shard Staff (held, an automatic stream of shards), every shot paying mana, with stand-in models drawn in code. The element boosts, general staffs and wands are not built yet.

**Built 2026-10-30** (docs/MAGIC_WAND.md, the owner's ask): the **Magic Wand**, no element: every spell cast with it in hand leaves its tip, 5% stronger for 10% less mana, and its click a free speck of dust once a second; and the **Magic Staff**, the same speck a little harder and slower, boosting nothing. The element boosts are not built yet.

**Built 2026-10-29**: the Arcane Beam Staff and the Staff of Healing (docs/STAFFS.md, docs/HEALING.md); the first enchant, the **Fire Enhancement** (docs/FIRE_ENHANCEMENT.md): the next swing, bullet or arrow burns what it hits and sets it alight, the sword ablaze while it waits; no effect on staffs (owner: "for now").

## God-tier spells

**One of each in the whole game** (owner, 2026-11-12: "only 1 across the whole game not each server not each region"): not one per server, world or region.

**What they are for** (owner, 2026-11-12): **the players become the bosses.** Once the strongest bosses in the game are beaten, what is left is enemy players with boss-tier strength and abilities. A holder is not simply hunted by everyone: they can be a faction's most valuable member, protected by it.

One holder per world: a flood, meteors, an Almighty Push, a zombie horde, a giant transformation or summon. Modelled on Lineage 2's Cursed Swords (one per server; the world goes red when one appears; the holder is hunted).

- A node at the top of a tree; you need many points in that tree to compete. Everyone else's tree shows **"Held by [name]"**.
- Won at an event (boss, trial, a meteor crashing somewhere), not ground out.
- **Lost on death**: the killer takes it if they qualify, otherwise it returns to the world.
- **Returns to the world if the holder is offline too long.** The most important rule.
- Server-wide announcement; holder shown on the map.
- Telegraphed (sky turns red, ~15 s warning), never in safe zones, cooldown in real hours.
- **The city heals** after destruction (permanent destruction would mean saving and sending every bit of damage: a big project).

## Death

Undecided between the two; current recommendation:

- **Normal worlds:** drop all carried items (lootable), level and stats reset, **keep skills and skill ranks**, god-tier spell freed. Faster XP until you are back to half your old peak level.
- **Hardcore worlds:** permanent death; the dead character retires to a normal world (Path of Exile does this).
- **How you died matters:** monsters and the deep zones = full penalty; **declared faction war = items only, keep levels** (otherwise nobody fights); murdered outside war by a red player = items only, and the killer is marked.

## Karma: the red name
Owner, 2026-11-02: the tutorial must put this across, because it is what makes the game unique: **the game is hardcore; being good or bad earns positive or negative karma, with consequences: you end up in heaven or hell, and a character can be saved after it dies.** It must also put across **the endless combinations of skill trees, weapon styles and stats.** The rules below and in Death are older and do not yet say this; how the two fit is still to be written down with the owner. (Also that day: the opening stays the owner's, not a powerful start that is lost.)

Owner, same day, on death: when a character dies you make another one and must reach Heaven or Hell, a difficult task you will likely need to be high level for. Heaven has no enemies, so it is easier than Hell, and Hell is hard to rescue your character from. How the rescue itself works is not decided (finding them, an NPC quest or an item). A saved character comes back with its level, skills and stats; its items were dropped when it died. A soul waits forever, with no time limit ("it feels like a gimmick to get you to play the game like a mobile game"), and other players cannot kill it. A rescue does not change karma: it stays the same.

- A symbol and name that go from **white to darker red** with each murder (inspired by Sword Art Online's cursor; white-to-red reads well for colour-blind players).
- **Counts only killing someone who was not fighting back.** War kills, self-defence (whoever strikes first is marked) and killing red players do not count; killing reds is bounty hunting.
- **Fades with time played**, not time offline (Ultima Online: one murder per 8 hours played).
- **Shades:** light red: NPCs charge more and are less friendly; red: guards attack, **no entry to the hub**; dark red: shown on the map, bounties, bigger death penalty (Ashes of Creation: triple).
- **Red is also a way to play:** outlaw hideout, black market, outlaw-only quests, outlaw factions.

## Factions

Minecraft Factions, in the city.

- **Relations:** ally and truce need both sides to agree; neutral by default; enemy is one-sided.
- **Territory:** first **a room**, then **a whole building**, then blocks, then districts (owner, 2026-10-18). A room is claimed by meeting a requirement, such as placing a bed in it; a whole building only once it is **cleared of enemies and has electricity**. Furniture found in buildings can be picked up and brought home to decorate your room and house. **Big furniture is moved for real** (owner, 2026-11-01): a couch is dragged home, or picked up and carried with a friend, which is faster; with a lot of Strength one player can carry it alone. Details and open questions: `docs/LOOT_AND_BUILDING_LEVELS.md` section 5. (The existing `/claim` command, ported from Godot, claims a whole building at once with no requirement: a prototype.)
- **Faction power:** members' deaths drain it; the land you can hold is limited by it; lose too much and your land becomes raidable.
- **War:** declare, 24 h warm-up, a goal (take a district, raid a base, tribute), ends in surrender, truce or exhaustion.
- **No offline raiding: sieges only happen in windows the defenders chose** (EVE, Albion). Scheduled sieges are also what guarantees big battles.
- **Battle size capped per side** to start (New World uses 50 vs 50); raise it once measured.

## Social

- **Uncensored chat and names**, no forced filter. Still required by law and platforms: an in-game **report button** and removal of illegal content (UK Online Safety Act covers game chat with no small-company exemption; EU DSA notice-and-action), Xbox mute/block/report rules (XR-015), US child-privacy rules. Hence: **the game is 18+**, each player can switch on a filter for themselves, mute and block, a streamer mode that hides names and chat. Names: anything except impersonating staff and invisible characters.
- **Proximity voice:** fades with distance, muffled through walls, **faction radio** heard anywhere. Proximity voice is built, with its own codec (2026-11-01, `docs/VOICE.md`); faction radio waits for factions.
- **Text chat is proximity too** (owner, 2026-11-01), maybe with a `/g` for global chat ("we will figure it out later"). Today what you type only shows as your own speech bubble: nothing is sent yet.

## Melee (owner, 2026-11-02)

"the shooting with guns and magic feels descent enough for now", "the melee combat doesnt not feel very good", "animations obviously need to change". Of Dark Souls, God of War and Devil May Cry pacing: "i kind of like the combos but i feel like the dark souls style fits the game better". So: Dark Souls style, with combos in it.

Owner, 2026-11-03, on using a free or bought animation pack: "no i dont want to use the same animations as everyone else. lets make combos". So the melee animations are our own, and combos come first.

My suggestion, not yet agreed: keep the rules underneath (damage, stamina, guard, parry, hit detection, hit freeze, ragdolls, sync); replace the swings with real full-body animations with the feet planted; steer a swing at the nearest enemy in front and step in, gently; one system for every melee weapon (fists, bats and bottles still run the old one). Rebuild the one-handed sword first, with a command to compare old and new.

Owner, 2026-11-04, on combos and being locked: not sure about combos at first; now thinks they may give melee fighters a fighting chance against guns and ranged magic. No very long combos ("like naruto storm where you can spam attack and get like a 16 hit combo"); regular swings mixed with sword skills could make a longer one. Being combo locked would be annoying, "so there has to be a way to get out", like Naruto Storm's substitution jutsu, maybe a spell that does something similar, "but there has to be another way". "theres going to be a bunch of teleportation/substitution style moves. like flash step or instant transmission type stuff". "no you cannot flash step through walls"; the same rule for instant transmission and substitution.

Owner, 2026-11-04, on the one-handed sword's 3-hit combo: likes a diagonal finisher for the third hit; "i kind of like the idea of people being flung around and ragdolled by attacks with large knockback but we would have to figure out how to make it fair". You should not be able to spam attacks on someone who is ragdolled (owner, correcting a typo). A body flying ragdolled should be able to recover in mid air, as in Saints Row 4: press Space and regain control. Then, on knockback: "i dont think there should be a far knockback maybe not one at all to start, that should be stuff you have to upgrade to or unlock".

My suggestions, not agreed: 2-3 swings, extended only by skills that cancel a swing's recovery (5-6 hits at most, limited by stamina, mana and cooldowns); each hit in a row stuns less, and after 3-4 you stop flinching for a moment; a breakaway dash anyone can use, for half the stamina bar; a hit stops a shooter shooting.

## Enemies

- **Most are humanoid**, built on the player body (civilians already are). An enemy = **look** (paint, outfit, size) + **loadout** (the same abilities players unlock) + **stats** + **brain**. A new enemy is a settings file, not new code, and enemies hint at what a zone teaches.
- **About six reusable brains:** brawler, duelist, caster, gunner, summoner, ambusher.
- **Non-humanoid monsters** (like the Crimson Claw) are reserved for bosses and each zone's signature creature.
- **The first real enemy: the Wraith** (2026-10-26, `docs/WRAITH.md`): a low-level ghost, the player's upper body turning to smoke, that
  floats about, dies in two punches, and will sneak up and bite for heavy damage (the bite is not built yet). The swordsman, the Shoota and
  the Crimson Claw remain testers.
- Enemy syncing across players: built 2026-10-27 for the swordsman, the Shoota and the Wraith (the machine that spawns one runs it, everyone sees and fights it; docs/MULTIPLAYER_STATE.md "Enemies"). Needs: animation LOD for crowds.
- **Spawning in the world (owner, 2026-11-14):** "monster and ghost type enemies spawn outside and humanoid enemies spawn indoors for starters"; "it's not that the mobs have set spawn locations right? it should be more akin to minecraft where they spawn at random"; then "monsters should appear in the same location between players when online. Cleared buildings should stop spawning enemies. make sure that the mobs have patrol behaviors or in other words they wander around in a natural way rather than awkwardly stand still". Built that day (docs/MOB_SPAWNING.md): enemies appear at random spots round a player (ghosts and monsters on open ground, humanoids on the floors of ordinary buildings), up to a budget near the player that players standing together share; they are taken away when left behind; a building whose last enemy is killed stays empty for a day; with nobody to fight they wander; the Crimson Claw is shared between players; none near Old Guy's house; `/mobs on|off`. Uses the enemies there are (Wraith, Crimson Claw, swordsman, Shoota). The numbers are mine and to tune; levels are not in it (asked 2026-11-13 and 14 whether level should come from the place, my recommendation, or scale to the player: not answered).

### Hobos (owner, 2026-11-20; not built yet)

The owner's design for the surface city's homeless people:
- **Many looks:** the owner designs a bunch in the character creator (started with Hobo1) for a wide variety; each wears a **random outfit** from a selection of clothing items the owner will pick later.
- **Random weapons, low level for the most part:** glass bottles, bricks, knives, wrenches and the like, but **sometimes better stuff**, so they are a way to farm items and XP "if you dont mind the negative karma from killing innocents".
- **Not hostile until struck.** Other hobos **join in, until a gun or offensive magic is used: then they get scared and run.**
- **If you don't want to kill them:** still being thought about; maybe trading with them.

My suggestions for building them, not decided: looks copied from the owner's Hobo characters in the creator (as Old Guy's is); a weighted weapon table with what a hobo holds being exactly what drops (a visibly rare weapon is a temptation); the brick and wrench as items on existing movesets (thrown like the bottle; the crowbar's moves); the swordsman's brain tuned weaker; outdoors on the surface only, with their own small budget beside the monsters'; killing one who did not strike first is a murder for karma (the first thing to feed it).

## Loot, refills and repairs (owner, 2026-10-22)

The owner's plan, like Rust:

- **Loot in the world is whoever gets it first**: one stock per building, not a copy for each player.
- **Quests done with allies, and their bosses:** everyone in the group gets loot drops.
- **Unclaimed buildings get new enemies and loot at random, about once a day.**
- **Destroyed buildings are repaired by robots the corporations send down**, at some point.

My suggestions (not decided):

- **The world is its original state plus a list of changes** (looted, cleared, damaged, claimed), kept by the server. A refill clears a building's looted and cleared entries; a repair clears its damage. The original costs nothing to store, so only changes are saved and sent. The same in a generated or a designed world; in a designed, finite world the list is bounded.
- Each building refills at its own random time, so nobody camps a known refill. Claimed buildings never refill or get repaired: they are the faction's.
- Better loot and harder enemies in dangerous districts and deeper zones. What a room holds follows its type and furniture (`docs/LOOT_AND_BUILDING_LEVELS.md`).
- Destruction in pieces (doors, windows, wall sections, floors) is doable; glass already breaks and is shared between players. A whole building collapsing is costly; a middle way is swapping a badly damaged building for a ruined version.
- The repair robots as something to see and fight: drones and scaffolding arriving, a repair to attack or defend. In the lore they fit the "leftover AI and machinery" fighting over the abandoned city.

## Items by rarity (owner, 2026-10-24)

The owner's list, as given. ✅ = in the game now.

- **Common:** Crowbar ✅, Bat ✅, Glass bottle ✅, T-Shirt ✅, Long Sleeve T-Shirt ✅, Hoodie, Machete, Slingshot ✅, Magic Wand ✅, Butcher Knife, Butter Knife ✅, Kitchen Knife ✅, Plastic Boomerang, Wooden Plank, Metal pole.
- **Uncommon:** Combat Knife, Nail Bat, Wire Bat, Combat Sword ✅, Glock ✅ (the pistol), Fur Hood Parka, Longbow, Tactical Greatsword, Magic Staff, Wooden Boomerang, Golf club, Baton, Combat Axe, Bo Staff, Scythe.
- **Rare:** Knight Helmet ✅, Black Fur Coat, AK-47 ✅, Deagle, Compound Bow, Crossbow, Dragunov, Barrett, Minigun, Grappling Hook ✅, Flame Staff, Frost Staff, Arcane Staff, Flamethrower ✅, Steel Boomerang, Combat Scythe.
- **Very Rare:** Laser Gun, Laser Minigun, Laser Sword, RPG, Jetpack ✅, Demon Scythe.
- **Legendary:** RPG Minigun, Rocket Pack.
- **Godly:** (not filled in yet).

In the game but not on the list yet: the spike sword, the parachute, the spray can, the hooded cloak (to be worked on), the Midnight Coat, the Blood Raven, red camo and black T-shirts, and the Black Jeans (2026-10-24, from the owner's cut), with the Blue, Bred and Maroon Jeans (2026-10-25, the same pair retextured), and the White Sneaker (2026-10-25, the first shoes), then the Khakis (2026-10-26, the jeans in tan cotton) and the BW Sneakers (2026-10-26, the sneakers in black and white), and the Farmer Hat (2026-10-26, a straw hat). The Skelli jeans and the top hat were taken out on 2026-10-24, to be replaced with better models.

## Items and art

- **Looking at an item (built 2026-10-31, docs/ITEM_INSPECT.md):** its name under the crosshair with `[E] Take  [F] Inspect`, as Skyrim does; F
  opens an appraisal card beside it in the world (the owner's Sword Art Online screenshot), the camera gliding in so it reads. The card lists what
  the item is and how it is held, its style, its damage, speed, fire rate, range, mana and the like, what stat raises it, and green or red arrows
  against the item in hand. No durability (owner, 2026-10-30), no level line; clothes show no armour, armour pieces their tier and rating (docs/ARMOUR.md).
  Owner, 2026-11-06, on how a sledgehammer should differ from a sword: "there should certainly be stronger armor than others". So worn armour will
  differ in strength. Built 2026-11-06 on my suggestion, which the owner said go to (docs/ARMOUR.md): Light, Medium and Heavy pieces add a rating by slot, a full heavy set stops about 40%, and armour never stops more than half of a player's blow or two thirds of an enemy's; the sledgehammer ignores 35% of armour. Owner, same day: heavy armour does NOT slow you down,
  and carrying a lot does not either: "an annoying system we should avoid things that are annoying like carrying a lot of items slowing you down".
  Owner, same day: enemies LOOTABLE like Skyrim's ("grab the armor and weapons off of them"; built 2026-11-06 for what a body wears, docs/LOOTING.md); with dismemberment, "probably having armor makes
  you harder to be dismembered and if you do still get dismembered badly we could have the armor just come off the body and turn into its
  object form".

- Designs are the owner's own drawings; an image-to-3D tool models them; the owner edits them in Blender.
- **An import pipeline** (drop a model in a folder → cleaned, small, gripped, iconed item with stats in a settings file) and **random rolls** (one model, many versions) make hundreds to thousands of items realistic.
- Prefer rigid armor (rides one bone) over skinned clothing for volume.
- **Steam AI disclosure:** generated models that ship in the game need disclosing; code written with AI does not (January 2026 rules). Draft wording: "All designs are hand-drawn by the developer; an image-to-3D tool turns them into models, which are then edited by hand." Rebuilding the most visible models by hand over time can remove the need.

## Technical foundations still missing

1. **Server authority:** today each player's own computer controls their character. Level, XP, stats, skills, items and karma must be decided and saved by the server.
2. **A shared character database** any server can read (see Servers).
3. **Enemy/NPC sync** between players: enemies done 2026-10-27, run by the machine that spawned them (owner-authoritative like the players, so it moves to the server with item 1); civilians and the Crimson Claw still local.
4. **Hosting, crash reports, the report button, privacy policy.**
5. **Crowds (owner, 2026-10-22):** other players' bodies get fewer triangles the further away they are, until far enough that they are not drawn at all, and **that range shrinks when more people are around**. Every character is the 86k-triangle body today, with no lighter versions. My suggestions: a crowd budget (always show the N nearest players in full; past it, lighter versions, then nothing), distant players animated and updated less often, and anyone fighting you, your party and your faction always shown. Before the hub is built: **a crowd test** that walks 100, 300, then 500 fake players around a test scene and measures frame rate, data sent and server load, so the hub's size and cap come from measurements. **First run 2026-10-27** (docs/CROWD_TEST.md): clones of the owner's characters in the city, on this laptop in the editor, about 0.65 ms a frame each, 30 fps at 25 and 12 fps at 100; the bodies are the cost (drawing first), fighting and casting add almost nothing. Data sent and server load not measured yet.

## Roadmap (first year)

| When | Playable | Who |
|---|---|---|
| Dec 2026 | Fighting that feels good (fists, sword, fire), server-side saving, stats + first tree | owner + friends, one server |
| Mar 2027 | Sewers + first enemy family + a dungeon; death, karma, sky hub. **Steam page + trailer** | friends + small Discord |
| Jun 2027 | Factions (claims, relations, war); free Steam Playtest | strangers |
| Oct 2027 | **Early Access**, a few weeks after October Next Fest | everyone |

Cut from year one: every zone below the Sewers and above the hub, god-tier spells (maybe one launch event), most trees, huge battles.

**Rules for the year:** make the 30-second fight fun before anything else; finish things before starting new ones; no foundation restarts. Steam: a 30-day wait after paying the app fee for a first game; a Coming Soon page at least two weeks before launch; 1-5 days of review.

## Open decisions

| Question | Recommendation |
|---|---|
| Upgrade cost curve | cost and reward grow at the same rate |
| Huge late-game numbers OK? | yes |
| One Attack stat or three | three (Strength, Spell Power, Precision) |
| Hotbar | Keys 1 to 0, no ultimate slot, bottom left for now (owner); one bar per weapon |
| Skill menu | one big menu, trees side by side like Skyrim, drag and scroll (owner) |
| Movement skills | Vault for everyone in the tutorial, then Dash, Climb (with ledge pull-ups), Super Jump; every path learnable for now; own controls, not the bar (owner) |
| God-tier rules | kill-to-take, returns when the holder is gone too long |
| Class titles | from the top one or two trees |
| Death | normal worlds keep skills; hardcore worlds permanent |
| War deaths | items only, keep levels |
| Sieges | defender-chosen windows |
| Battle size | capped per side, start 50 vs 50 |
| Hub | in the sky, parachute drop |
| Age rating | 18+ |
| Gothic surface district | still in? |

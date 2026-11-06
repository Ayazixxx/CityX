# Agent report 2: items and systems (a98c06195f6d43250, 2026-11-05)

## Core loop (GAME_DESIGN) and biggest gaps
Loop: fall in as a fallen angel without memory; Old Guy's house gives first gear, faction, claim; hub; drop into the black city, clear buildings, Souls fights, level forever; claim a room then a powered building; find your way home; PvP under karma; death drops items, holds the character in Heaven/Hell until rescued; deeper for god-tier spells.
Gaps:
1. No economy (no money, trading, nothing consumed; no durability = owner's call). Losing gear stings only with a price; fair only if cheap to replace (Albion treats gear as consumable).
2. Nothing says "go there tonight": no jobs, quests, events.
3. Loot has one use: a third bat is junk.
4. Power and night only a look: claims need power, nothing restores it; `PowerGrid.Restore` never called.
5. Death has no floor: next character starts with nothing, no stash.

## Items
- Glow sticks, road flares (DayZ chemlights, GTA flares): thrown light; sticks glow minutes in team colours (breadcrumbs); flare burns red, lights gasoline. grenade throw, fire. S. shared (gives you away)
- Charge cells (Metro 2033 ammo-money): money and power in one: spend, or run flashlight/jetpack/laser/claim on it; keep cheap cells common (Metro scarcity killed choice). M. server holds counts
- Ichor flask (Estus): few sips, refilled at bed or in a lit building; drinking leaves you open. M (authored drinking clip). named state
- Smoke, firecrackers (Rust, Dying Light): smoke hides escape/revive; firecracker noise lures enemies/players. S. like frag grenade
- Walkie-talkie (DayZ radio): faction radio; numbered channel heard km; finder can listen in. S. server routes voice
- Night-vision goggles (Tarkov, DayZ): black city in green; light blinds; nights stay black for others. helmet slot. M. lenses glow
- Zipline kit (DL2, Far Cry): cable roof to roof anyone rides 5 min. grapple rope. M. shared line
- Inline skates (JSR): fast on flat, grind kerbs/railings/footbridges. shoe slot. L (full-body clips). synced movement state
- Town map (Zomboid): in bus shelters/stations, reveals a few km. map explored areas. S. personal
- Lockbox, padlock, bolt cutters (Rust, DayZ): lock a claim's door/box; cutters 20 loud s. M. street doors need sync first
- Hammer and planks (Zomboid): board up claim windows/doors; break like Old Guy's planks; fills holes, not building. M. like glass
- Glow wear (agent's idea): rarest cosmetics glow (neon trims, LED soles). S each
- Stencils, glow paint (JSR Graffiti Souls): hidden on roofs, unlock ready tags; glow paint costs a cell, shows at night. S-M
- Rapture heaps (lore): rapture took people not clothes: heaps on pavements, in cars, at desks, pockets to search. containers. S
- Memory shards (Destiny lore cards, OSRS collection log): lore + title/cosmetic, never power; a log. S each

## Systems
- Fixers and contracts (Cyberpunk gigs, EVE courier): NPC fixers give clear/fetch/haul/escort/tag/kill-a-red-name; players post jobs, server holds reward + deposit; vary job types (Cyberpunk panned "same three or four enemy types"). L
- Combo crafting (Dead Rising 2, Rust recycler): Bat+nails = Nail Bat, bat+wire = Wire Bat (on rarity list); junk breaks into parts; recipe cards in drawers; without one a weaker version. M
- A floor under death (OSRS kept items, Tarkov secure container): 3 best items to an account vault (none for red names); next character re-gears; softens "items drop". S
- Night pays (Dying Light): double XP + better loot at night (14-min night); Wraiths hunt by light and sound. S then M
- Heat (GTA stars, JSR police): gunfire/explosions/broken glass raise a district's heat; leftover machines answer in waves. M-L
- Building warnings (Deep Rock): a building rolls one warning (flooded basement, wraith nest, gas-soaked floor); DRG pays 15-30% more XP. S-M. from seed
- Clue trails (OSRS treasure trails, SoT riddles): a note describes a real spot ("red kerb by the hydrant under the double footbridge"), steps lead to a casket. M
- Rested at home (Valheim +50% XP, 2x stamina regen): furniture carried home sets duration. S-M
- Player-made routes (Mirror's Edge Catalyst): plant start/finish on roofs; others race your ghost; leaderboard; keep on own servers (Catalyst online closed 2023). M
- Weather with teeth (BotW, RDR2): snow prints trackable (bodies already leave them); rain puts out fire, sets up lightning; storms strike high flyers. S each
- Hotwiring (GTA, DayZ): parked cars run on a cell + gas; fast home, loud, headlights seen for blocks. M. cars need sync first
- Claim projects (New World town projects): pool cells/parts into lights, a stall, a turret. M
- The Marked (GTA Hunt the Beast): volunteer gets buff + prize for surviving 5 min while shown on every map; killing them costs no karma. S-M. needs ~10 players
- Pirate radio (JSR Professor K): DJ reads server news: claims, wars, events, reddest names. S-M

## Big bets
1. The Grid, light is territory (Albion power cores, Rust upkeep, DL2 metro, Zomboid generators): repair a district substation (fuse puzzle like Old Guy's), feed it cells; lamps, rooms, traffic lights, vending machines, train stations return; trains stop only at powered stations (earned fast travel); claims pay upkeep in cells (Rust ~10% build cost/day); unpaid = lights out, raidable (Albion); big cells carried like the couch, carrier shown on every map (Hunt bounties; SoT Reaper +150%); at night any roof shows who holds the city. Builds on PowerGrid.Restore, room lights (switches sync), carrying, claim rule, monorail plan. L. one power flag per building in the server's change list. AGENT'S PICK.
2. Rapture events, the sky keeps taking things (Destiny public events, SoT skull clouds, DayZ heli crashes, Fortnite live events): pillar of light marks a block: a building rises into the sky with loot+enemies (10 min to get out); a crashed Organization pod with guards; corporations' repair robots; a meteor with a god-tier spell; groups push to harder tier (Destiny heroic); once or twice a year a scripted map change (Travis Scott 12.3M concurrent). L. server schedules.
3. Tag Wars, the walls say whose street it is (JSR turf, New World territories): big walls/bridges/billboards are tag spots; faction tag earns influence; most influence colours the block on the map, cheaper fixers, first claim on buildings; rivals spray over/wash off; tags fade unless refreshed; war goal "hold six walls by Sunday". Graffiti already syncs. M-L.

## Sources
Dying Light night https://dyinglight.wiki.gg/wiki/Night ; DL2 metro https://www.gamesradar.com/dying-light-2-fast-travel-metro-stations , facilities https://www.gamesradar.com/dying-light-2-assign-faction-structures-peacekeepers-survivors ; Albion gear https://mein-mmo.de/en/8-year-old-mmorpg-from-berlin-is-successful-while-the-larger-competition-is-disappointing-now-it-reveals-the-secret,1517698/ , power cores https://www.altchar.com/game-news/hq-hideouts-are-albions-newest-feature-to-help-smaller-player-groups-aqSpk0U5XZei ; Rust TC https://wiki.facepunch.com/rust/The_Tool_Cupboard , upkeep https://www.corrosionhour.com/rust-upkeep-building-guide/ , monuments https://xgamingserver.com/blog/rust-monuments-guide-loot-puzzles-keycards/ ; OSRS kept on death https://oldschool.runescape.wiki/w/Items_Kept_on_Death , trails https://oldschool.runescape.wiki/w/Treasure_Trails ; Tarkov https://afkgaming.com/esports/guide/everything-about-secure-containers-in-escape-from-tarkov ; SoT emissaries https://seaofthieves.wiki.gg/wiki/Emissary , world events https://seaofthieves.wiki.gg/wiki/World_Events ; Hunt https://huntshowdown.fandom.com/wiki/Game_Modes ; Metro https://techraptor.net/gaming/features/metro-series-bullet-economy , https://www.destructoid.com/preview-metro-2033/ ; Valheim https://mobalytics.gg/gamebase/guides/valheim-comfort-rested-guide , https://gamever.io/knowledge-base/the-rested-buff-in-valheim-what-it-gives-you-and-how-long-it-lasts ; EVE https://wiki.eveuniversity.org/Moving_your_items ; Cyberpunk https://www.gameskinny.com/?p=67898 ; Dead Rising 2 https://www.gamesradar.com/dead-rising-2-combo-card-weapon-guide ; DRG https://deeprockgalactic.wiki.gg/wiki/Warning ; New World https://www.newworld.com/en-us/news/articles/making-your-mark-on-aternum-settlements-and-governance ; Catalyst https://ea.com/news/dev-diary-social-play ; JSR https://jetsetradio.fandom.com/wiki/Graffiti_Souls , https://www.giantbomb.com/games/3030-12117/ ; Zomboid https://pzfans.com/how-to-find-use-and-repair-generator-in-project-zomboid/ ; GTA Online https://support.rockstargames.com/articles/1Wgw74PudBdSjUrRC6tCKW/freemode-events-information-for-gta-online-on-ps4-xbox-one-and-pc ; DayZ https://xgamingserver.com/blog/dayz-helicopter-crash-sites-guide/ ; Destiny https://destinypedia.com/Public_Event ; Fortnite https://www.pcgamer.com/travis-scotts-fortnite-show-had-a-record-breaking-number-of-players ; weather from docs/RAIN.md, docs/SNOW.md

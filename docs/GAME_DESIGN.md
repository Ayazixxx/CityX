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

Enemies are the real content cost, not terrain (see Enemies).

## The sky hub

- A floating safe city above the starter city: trading, socialising, faction recruiting, the market board, basic skill trainers.
- **You leave by jumping off with the parachute** (exists) to a random spot, or to your faction's base.
- You **respawn here** after death.
- **Spawn protection** for a few seconds after landing, or until you attack.
- **Copies** of about 100 players each when it gets busy; the market board works across copies.
- **Text chat only** in the hub at first (voice everywhere else).

## Getting home

- **Teleport crystals** (an item: works on controller, dropped on death, tradeable) with a hotkey or menu shortcut.
- **10 s charge**, cancelled by taking damage or moving. **Blocked** for 15 s after any player fight, during a siege, and in enemy territory.
- Takes you home (faction base, else the hub) or to waypoints you have found yourself, never anywhere deeper.

## Character progression

### Levels and stats

Exists: level, XP (100 x level per level), 5 stat points per level (`PlayerVitals`, `CharacterSave`; saved locally, not on a server yet). Since 2026-10-14 they go into the owner's list of 24 stats (docs/STATS.md: Max Health, Health Regen, Max Stamina, Stamina Regen, Max Mana, Mana Regen, Melee Damage, Melee Speed, Firearm Damage, Reload Speed, Fire Rate, Spell Power, Cast Speed, Cooldown Reduction, Armor Penetration, Accuracy, Recoil Control, Movement Speed, Flight Speed, Vehicle Handling, Armor, Magic Resistance, Stability, Block Chance), each working in the game except Flight Speed and Vehicle Handling (wings and the new cars come later). Vitality, Endurance and Strength became Max Health, Max Stamina and Melee Damage.

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

## God-tier spells

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

- A symbol and name that go from **white to darker red** with each murder (inspired by Sword Art Online's cursor; white-to-red reads well for colour-blind players).
- **Counts only killing someone who was not fighting back.** War kills, self-defence (whoever strikes first is marked) and killing red players do not count; killing reds is bounty hunting.
- **Fades with time played**, not time offline (Ultima Online: one murder per 8 hours played).
- **Shades:** light red: NPCs charge more and are less friendly; red: guards attack, **no entry to the hub**; dark red: shown on the map, bounties, bigger death penalty (Ashes of Creation: triple).
- **Red is also a way to play:** outlaw hideout, black market, outlaw-only quests, outlaw factions.

## Factions

Minecraft Factions, in the city.

- **Relations:** ally and truce need both sides to agree; neutral by default; enemy is one-sided.
- **Territory:** claim buildings, then blocks, then districts (the `/claim` command for buildings exists, ported from Godot).
- **Faction power:** members' deaths drain it; the land you can hold is limited by it; lose too much and your land becomes raidable.
- **War:** declare, 24 h warm-up, a goal (take a district, raid a base, tribute), ends in surrender, truce or exhaustion.
- **No offline raiding: sieges only happen in windows the defenders chose** (EVE, Albion). Scheduled sieges are also what guarantees big battles.
- **Battle size capped per side** to start (New World uses 50 vs 50); raise it once measured.

## Social

- **Uncensored chat and names**, no forced filter. Still required by law and platforms: an in-game **report button** and removal of illegal content (UK Online Safety Act covers game chat with no small-company exemption; EU DSA notice-and-action), Xbox mute/block/report rules (XR-015), US child-privacy rules. Hence: **the game is 18+**, each player can switch on a filter for themselves, mute and block, a streamer mode that hides names and chat. Names: anything except impersonating staff and invisible characters.
- **Proximity voice:** fades with distance, muffled through walls, **faction radio** heard anywhere. Tool candidates: Dissonance (community FishNet connector) or Vivox. Microphone capture with push-to-talk already exists (`PlayerVoiceCapture`); nothing transmits yet.

## Enemies

- **Most are humanoid**, built on the player body (civilians already are). An enemy = **look** (paint, outfit, size) + **loadout** (the same abilities players unlock) + **stats** + **brain**. A new enemy is a settings file, not new code, and enemies hint at what a zone teaches.
- **About six reusable brains:** brawler, duelist, caster, gunner, summoner, ambusher.
- **Non-humanoid monsters** (like the Crimson Claw) are reserved for bosses and each zone's signature creature.
- Needs: enemy syncing across players (not built), animation LOD for crowds.

## Items and art

- Designs are the owner's own drawings; an image-to-3D tool models them; the owner edits them in Blender.
- **An import pipeline** (drop a model in a folder → cleaned, small, gripped, iconed item with stats in a settings file) and **random rolls** (one model, many versions) make hundreds to thousands of items realistic.
- Prefer rigid armor (rides one bone) over skinned clothing for volume.
- **Steam AI disclosure:** generated models that ship in the game need disclosing; code written with AI does not (January 2026 rules). Draft wording: "All designs are hand-drawn by the developer; an image-to-3D tool turns them into models, which are then edited by hand." Rebuilding the most visible models by hand over time can remove the need.

## Technical foundations still missing

1. **Server authority:** today each player's own computer controls their character. Level, XP, stats, skills, items and karma must be decided and saved by the server.
2. **A shared character database** any server can read (see Servers).
3. **Enemy/NPC sync** between players.
4. **Hosting, crash reports, the report button, privacy policy.**

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

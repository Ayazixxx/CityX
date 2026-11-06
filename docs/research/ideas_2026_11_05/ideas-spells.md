# Agent report 1: abilities and weapons (a61ac9b0f753fd368, 2026-11-05)
Facts from docs: 1 building in 10 and 1 street in 3 have power, a restore hook exists (ROOM_LIGHTS, AMERICAN_STREETS); hydrants built, manholes/drains not; carried objects not visible to others (CARRY).
Effort S days, M 1-2 weeks, L weeks.

## Principles
- Every big move has a tell and an answer, players' spells too (Sekiro one red warning, each attack kind one answer).
- Few visible states, few rules (BotW elements change materials and each other, materials never each other; Magicka opposites cancel). CityX already shows wet, burning, chilled, frozen, poisoned, gasoline.
- Meters not dice (Elden Ring status meters fill then pop; Frost Staff already does; victim can dash out; short immunity after).
- Setup and payoff across players (Genshin, Destiny 2 Jolt/Ignition, Hades duo boons): some spells apply a state (wet, oil, chill), others trigger (shock, fire, heavy blows); chains weaken as they spread (Risk of Rain 2 proc 20%).
- The city is the arsenal (inFAMOUS drains lamps/neon; Sifu, Dead Rising); city from the seed, so a network event only names the lamp/hydrant.
- One verb per weapon (Monster Hunter phials/kinsect, Lies of P parts, Hades aspects).
- The look comes from effects and physics: reuse strong poses (cast, throw, staff thrust); vary skills with modifiers (Path of Exile, Noita).

## Reactions (rows 1,3,5 = GAME_DESIGN's planned reactions made concrete; the machine that sets it off deals damage)
| Wet + shock | 1.5x dmg, arcs to wet bodies within 3 m; shocked puddle jolts everyone in it [Genshin, DOS2] | dry off with fire |
| Wet + cold | chill builds 2x; frosted puddles become grip-less ice 10 s [Magicka] | stay dry |
| Frozen + heavy/blunt | Shatter: bonus dmg + knockdown; killed while frozen breaks into ice chunks [Genshin] | no refreeze 1.5 s (exists) |
| Fire + water | steam blocks sight and scalds; shocked steam stuns [DOS2] | walk out |
| Gas + flame/spark | Poison Breath cloud explodes, even caster's own [DOS2] | cloud visible |
| Acid + armour/glass | armour down 10 s; windows crack [Warframe] | roofs |

## Spells
- Combust (Pyro): snap, every fire you lit within 25 m flares; burning bodies take remaining burn in one hit [Destiny]. fire grid. S
- Cloudburst (Hydro root): 20 m rain cloud 15 s: soaks, douses fire, slicks ground; setup for every combo [Genshin]. Acid Rain's storm. S
- Hydrant Geyser (Hydro): hydrant erupts, launches whoever stands on it, leaves puddle [inFAMOUS]. hydrants; MP event names hydrant. M
- Frost Bridge (Cryo): ice walkway to crosshair (30 m, 12 s) between towers; at water makes a pillar; fire/heavy breaks it [Frozone, BotW Cryonis]. M
- Devouring Swarm (Plague): rats pour from nearest drain, chase+poison; fire kills them [Dishonored]. drains not built; MP seed+target. M
- Arc Lash (Electro): bolt jumps to 3 more targets within 6 m, 12 m between wet ones [Destiny Jolt]. Self Smite bolt. S
- Bolt Step (Electro): turn into lightning, land on a metal pole within 25 m in clear sight; owner's flash step; never through walls [Dishonored Blink]. lamp/signal positions. M
- Blackout (Electro): cast at lamp/signal/fuse box: power dies 40 m for 20 s, sparks into puddles; dark = cover [inFAMOUS]. city power. M
- Arcane Coin (Arcanist): toss coin; a shot/spell hitting it ricochets harder to nearest enemy; coins chain [Ultrakill]. deflection rule. S-M
- Telekinesis (Wizard): lift/throw bench, bin, weapon, limp body; else rip up pavement [Control, Saints Row IV]. carry+ragdolls; MP carry must be visible first. L
- Scald Fog (Steam): steam bank hides everyone + scalds; shocked = stun [DOS2]. gas cloud. S
- Echo (Remnant): afterimage replays your last 2 s of moves, harmless bait [Apex Mirage]. copies replay by name. M

## New schools
- Technomancer (Rapture left machines running): Drain (lit lamp/sign gives mana, dark a minute) [inFAMOUS]; Override (a traffic light shoots your target); Restore (power a dead building; claims need power). M
- Sigil (the fallen remember the Words; glyphs found as words on walls in deep zones) [Skyrim]: spray-can glyphs Ward (doorway enemies cannot cross), Snare (visible floor mark roots), Beacon (anchor for Bolt Step and planned portals) [Okami, Jet Set Radio]; graffiti already syncs. M
- Remnant (memories the fallen lost): Echo; Recall (back to where you were 3 s ago) [Tracer]; Haunt (nearby players' last 10 s as ghost trails, bounty hunting). M

## Skills and trees
- Substitution: staggered mid-combo, tap dash: a bin takes the next blow, you appear 6 m away; mana, 15 s cd; owner's combo escape [Naruto Storm]. S-M
- Super Sprint: hold sprint to wind past the cap, scatter props, run up walls; owner's "fast running later" [Saints Row IV, Prototype]. M
- Grind: slide railings/parapets/highway barriers with sparks; one strong pose [Jet Set Radio, Sunset Overdrive]. M-L
- Meteor Drop: crouch in air to dive; landing shockwave sized by the fall, no fall damage [Dark Souls plunge]. S
- Grapple Yank: hook a person/prop and pull it in, or pull yourself [Just Cause]. S-M
- Flow: moving without stopping builds speed and charges next strike; repeating one move earns less [Titanfall 2, Ultrakill, Spider-Man]. S
- Guard Counter: straight after a block, a fast heavy that staggers [Elden Ring]. S
- Switch Strike: swap weapons right after a hit, the new weapon's first attack comes out instantly [DMC5]. S-M
- Duo stars between two trees: Storm Front (Hydro+Electro: lightning hits the wettest body in your Cloudburst), Fumigate (Pyro+Plague: casting fire lights your gas) [Hades]. S each
- Modifier stars: Wildfire Dash (dash leaves fire), Contagion (poison jumps on when host dies), Caltrops (stuck ice shards chill passers) [Path of Exile]. S each
Every launch (Geyser, Meteor Drop, STOP Sign) comes from something learnt or found: fits owner's rule that knockback is unlocked.

## Weapons
- STOP Sign: pulled from a corner; 2H; heavy = flat slap that throws; face stops bullets while held; breaks ~20 hits [Sifu, Dead Rising]. greatsword moves; needs list of world changes. S-M
- Manhole Cover: thrown discus bouncing off two walls; open hole leads into the Sewers [Captain America]. boomerang, manholes. M
- Umbrella: closed a rapier; open a guard + personal roof (dry in rain, safe from Acid Rain), slows falls [Kingsman]. parachute. M
- Molotov: bottle + gasoline, burning pool. S
- Fire Extinguisher: cloud blinds, douses, chills; club; bursts when shot. S-M
- Fluorescent Tube: from an office ceiling; lights a black room, shocks, bursts into glass after 6 hits. S
- Nail Gun: rapid nails; a kill pins the ragdoll to the wall [HL2]. Ice Shards' sticking. S-M
- Hand Cannon: shield slot, one heavy shot; hit an enemy during its wind-up = stagger open for a crit [Bloodborne]. M
- Sledgehammer: slow 2H, Shatter + guard-break specialist [Monster Hunter]. S
- Taser: darts on wires build shock, jolt a wet target at once, reel in. grapple rope. S-M
- Roman Candle: 8 slow fireballs in an arc; lights gasoline and dark streets. S
- Super Soaker: toy, no damage, soaks at range, puts out fires; deadly next to an Electromancer; could be in Old Guy's basement. S
Mods: combo recipes found on posters [Dead Rising 2, TotK Fuse]: bat+nails = Nail Bat, bat+wire = Wire Bat (owner's list), bottle+gasoline = Molotov; Infusions [Elden Ring affinities]; elemental rounds [Warframe]; weapon arts as sockets [Elden Ring, Lies of P] e.g. Stab Dash on the crowbar.

## Big bets
1. City Chemistry: grow the gasoline fire grid into one grid for every surface (water, ice, oil, acid, shocked water) + clouds + body states, run by the reaction table; trees multiply; rain/snow join. L, one rule at a time [BotW, DOS2, Magicka]
2. The Living Grid: power as territory; substations feed districts; power lights streets, opens doors, runs base defences; restore/steal (Drain)/cut (Blackout) before a siege; in black nights light is the map; one on/off per substation synced. L [inFAMOUS]
3. Domains: each tree's top star, 20 m sphere rewriting rules 10 s (Pyro gasoline floor; Cryo ice+chill; Electro metal arcs; Arcanist nothing flies in/out); answers Simple Domain (2 m bubble) and Domain Clash (higher rank wins); one pose + shader; L after bet 1 [Jujutsu Kaisen]
Start with (December fists/sword/fire goal): Guard Counter, Combust, Molotov, Shatter + gas explosion; then Cloudburst with Arc Lash.

## Sources
BotW chemistry https://gamesbeat.com/the-legend-of-zelda-breath-of-the-wild-makes-chemistry-just-as-important-as-physics/ ; Magicka https://lparchive.org/Magicka/Tutorial%203 , https://www.gameskinny.com/tips/complete-magicka-2-master-spell-combination-guide/ ; DOS2 https://www.gamepressure.com/originalsinii/environmental-effects-and-combinations/zea274 , https://www.gamepressure.com/originalsinii/clouds-and-combinations/zfa275 ; Genshin https://genshin-impact.fandom.com/wiki/Shattered , https://genshin-impact.fandom.com/wiki/Element_Durability ; Destiny 2 https://www.bungie.net/7/en/News/article/51678 , https://www.ginx.tv/en/destiny-2/solar-ignition-and-scorch-destiny-2 ; Elden Ring https://eldenring.wiki.gg/wiki/Status_Effects , https://eldenring.wiki.gg/wiki/Affinities , https://eldenring.wiki.gg/wiki/Ash_of_War ; Sekiro https://sekiroshadowsdietwice.wiki.fextralife.com/Combat , https://www.superjumpmagazine.com/the-art-and-science-of-sekiros-combat/ ; Risk of Rain 2 https://riskofrain2.wiki.gg/wiki/Proc_Coefficient ; Ultrakill https://ultrakill.wiki.gg/wiki/Coins , https://ultrakill.wiki.gg/wiki/Parry , https://intothebluesky.com/2020/12/19/interview-with-arsi-hakita-patala/ ; DMC5 https://twinfinite.net/guides/devil-may-cry-5-raise-style-rank-how-what/ , https://samurai-gamers.com/devil-may-cry-5/royal-guard-style/ ; Monster Hunter https://www.gematsu.com/2017/07/monster-hunter-world-14-weapon-types-overview-trailers , https://game8.co/articles/featured/the-history-of-monster-hunter-weapons ; Lies of P https://liesofp.wiki.fextralife.com/Fable+Arts , https://primagames.com/tips/lies-of-p-how-to-change-blade-handles ; Hades https://www.rpgsite.net/feature/10254-hades-infernal-arms-weapon-aspects-guideevery-weapon-aspect-and-upgrade-unlock ; PoE https://www.poewiki.net/wiki/Support ; Noita https://en.wikipedia.org/wiki/Noita_(video_game) ; Dishonored https://www.gamesradar.com/dishonoreds-defining-power-was-last-minute-call ; inFAMOUS SS https://en.wikipedia.org/wiki/Infamous_Second_Son , https://www.thesixthaxis.com/2013/08/01/behind-the-scenes-of-infamous-second-son/ ; Spider-Man https://gdcvault.com/play/1026084/Concrete-Jungle-Gym-Building-Traversal , https://www.theringer.com/2018/9/24/17895602/marvels-spider-man-swing-success ; Saints Row IV https://en.wikipedia.org/wiki/Saints_Row_IV ; Prototype https://en.wikipedia.org/wiki/Prototype_(video_game) ; TotK Fuse https://www.gonintendo.com/contents/21435-zelda-tears-of-the-kingdom-devs-on-their-intent-behind-the-fuse-and-ultrahand ; Dead Rising 2 https://www.gamesradar.com/dead-rising-2-combo-card-weapon-guide/ ; Sifu https://en.wikipedia.org/wiki/Sifu_(video_game) ; Skyrim https://en.m.uesp.net/wiki/Skyrim:Word_of_Power ; JJK https://sportskeeda.com/anime/what-is-domain-expansion-in-jujutsu-kaisen , https://www.sportskeeda.com/anime/what-simple-domain-jujutsu-kaisen-kusakabe-s-biggest-strength-explained ; Warframe https://wiki.warframe.com/w/Damage_type , https://wiki.warframe.com/w/Elemental_Damage
Caveats: from memory, not looked up: Hades duo boons, Frozone, Control, Apex, Overwatch, Okami, JSR, Sunset Overdrive, HL2, Bloodborne, Titanfall 2, Just Cause, Naruto Storm, Kingsman, Captain America. Fan wikis; every number a first guess.

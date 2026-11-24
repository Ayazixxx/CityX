# Mobs spawning in the world (first slice, 2026-11-14)

Owner, 2026-11-14: "Okay lets try implementing mobs spawning in the world. I think we could start by having monster and ghost type
enemies spawn outside and humanoid enemies spawn indoors for starters". And, of a first version that gave every enemy a set place
worked out from the world seed: "just to clarify it's not that the mobs have set spawn locations right? it should be more akin to
minecraft where they spawn at random". Asked the day before how to begin ("how mob levels, what types of mobs, where they spawn, how
they behave"), this is the first piece: WHERE and WHAT. Levels and new behaviour are not in it.

## What it does

- **Enemies appear at random round the player, as Minecraft's do.** Nothing has a set place. While there is room under the budget,
  every few seconds a random spot is rolled near the player and an enemy appears on it.
- **Ghosts and monsters appear out of doors; humanoids appear indoors.** Out of doors is any open ground outside every building (a
  street, a pavement, a square, an empty lot); indoors is a floor of an ordinary building. With the enemies that exist today: the
  ghost is the Wraith, the monster is the Crimson Claw (a quarter of the outdoor ones), the humanoids are the swordsman and the Shoota
  (six in ten carry a sword). The swordsman, the Shoota and the Claw are still the testers (`docs/GAME_DESIGN.md`, Enemies): they
  stand in until there are real ones.
- **Not in your face**: out of doors no nearer than 45 m, indoors no nearer than 18 m (behind walls), and no further than 95 m.
- **A budget near the player**: at most 4 alive out of doors and 6 indoors. One appears in each no sooner than 4 s after the last,
  so a cleared street fills again over a quarter of a minute, not at once.
- **Gone when you leave**: an enemy 130 m behind you is taken out of the world. A killed one is simply gone (its body lies where it
  fell, to be looted, until you leave it); another appears somewhere else in its own time.
- **A cleared building stays empty** (owner, 2026-11-14: "Cleared buildings should stop spawning enemies"): when the last enemy
  alive in a building is killed, nothing appears in that building again for a day (the owner's earlier "about once a day"). The other
  buildings go on being occupied.
- **With nobody to fight they wander** (owner, 2026-11-14: "make sure that the mobs have patrol behaviors or in other words they
  wander around in a natural way rather than awkwardly stand still"): a stroll of a few seconds, a stand of a few seconds (often
  turning to look another way), another stroll; round what is in their way, back from a drop, and never far from where they began.
  The swordsman, the Shoota and the Crimson Claw; the Wraith already drifted.
- **The same for every player online** (owner, 2026-11-14: "monsters should appear in the same location between players when
  online"): the Crimson Claw is now shared between machines like the others, so monsters appear in sessions too.
- **None within 120 m of Old Guy's house**: a new character starts there with nothing in its hands.
- **Hobos** (2026-11-24, docs/HOBO.md): out of doors too, 40% of the outdoor ones (mine); calm until hurt.
- **`/mobs off`** takes them all away and stops them appearing, **`/mobs on`** brings them back, **`/mobs`** says how many are alive.

## The numbers to tune

| Number | Now | Where |
|---|---|---|
| Most alive out of doors / indoors | 4 / 6 | `MobHabitats.MostOutdoors`, `MostIndoors` |
| Monsters among the outdoor ones | a quarter | `MobHabitats.MonsterShare` |
| Gunners among the humanoids | 40% | `MobSpawnRules.GunnerShare` |
| Appears from / to | 45 m (18 m indoors) to 95 m | `MobSpawnRules.OutdoorsFromMetres`, `IndoorsFromMetres`, `ToMetres` |
| Seconds between two appearing | 4 | `MobSpawnRules.SecondsBetweenSpawns` |
| Taken away past | 130 m | `MobHabitats.CrowdMetres` |
| A cleared building stays empty | a day | `MobHabitats.ClearedSeconds` |
| Stroll / stand, seconds | 2.5 to 6 / 1.5 to 4.5 | `NpcWander.WalkSeconds*`, `PauseSeconds*` |
| Strolling pace | a full walk (0.45 until 2026-11-24: the legs glided; the Claw 1.6 m/s) | `NpcWander.StrollShare`, `CrimsonClaw.WanderSpeed` |
| Stays within | 9 m of where it began | `NpcWander.HomeRadiusMetres` |
| Safe round Old Guy's house | 120 m | `MobHabitats.TutorialSafeMetres` |

**The budget is a frame-rate budget, not a design choice**: bodies are what a crowd costs (`docs/CROWD_TEST.md`: about 0.65 ms a frame
each on the owner's laptop in the editor, drawing included).

**What the ten cost, measured** (`MobCostBench`, 2026-11-14, the owner's laptop, in the editor, the processor's side only: scripts,
posing, physics): with both budgets full (4 out of doors, 6 indoors) a frame took **1.34 ms** longer than with none in the world, about
0.13 ms an enemy; ten lying dead cost about 0.1 ms between them. Before that day's two fixes it was 2.95 ms: one idle monster searched
the whole scene for someone to fight every frame (0.78 ms by itself, more than five swordsmen), and every body without a sword looked
for its sword component two or three times a frame (docs/PERFORMANCE.md). What they cost the graphics card, and the frame rate in a
built game, is still not measured.

Seen in the game's own city (`MobSpawnCapture`, 2026-11-14), the player stood in a street 280 m from the start: both budgets filled
(2 ghosts and 2 monsters out of doors; 3 swordsmen and 3 gunners in buildings), with 9 buildings near by able to hold enemies.

## How it is built

- **`MobHabitats`** (`World`, pure): what a spot of the city is to an enemy, from the plan alone.
  - `At(plan, x, z)`: `Outdoors` (outside every footprint and a metre clear of every wall), `Indoors` (inside an ordinary building,
    2.5 m clear of its outside walls) or `None` (a wall, a building that holds nobody, or near Old Guy's house).
  - `Holds(site)`: an ordinary building with floors, at least 9 m each way: not a car park, a mall, a sealed block, a wing or a block
    standing on another's roof. Much of the city's building stock is not.
  - `BuildingsNear` and `TryIndoorSpot`: the buildings near a chunk that can hold enemies, and a random spot inside one. (The insides
    of buildings are about three parts in a hundred of the ground, so a spot rolled over the ground would hardly ever be indoors: a
    humanoid's spot is rolled in a building rolled from the ones near by.)
  - `Floors`, `FloorY`: a building's floors; `FamilyFor`: ghost or monster out of doors, humanoid indoors; the budget.
- **`MobSpawner`** (`View/Mobs`, on the streamer's object in the generated scene): each half second, for this machine's own player
  (`LocalPlayerBody`), it takes away what has been left behind and, for each of out of doors and indoors with room under its budget,
  rolls up to eight spots until one is sound. Sound means: the city is built in full there on this machine, and a ray finds ground
  within 1.5 m of the height the plan gives an outdoor spot (a roof, an awning or an expressway deck over the street is not the
  street), or a floor within 0.6 m of the rolled floor's height with room for a body to stand (the plan knows a building's box, not
  its inner walls and stair). Every frame the few enemies held are checked for death (a dead ghost fades out of the world by itself).
- **`MobSpawnRules`** (`View/Mobs`, pure): the distances, the pace, which enemy a family is, the random spot in the ring (every part
  of the ring as likely as any other).

## Wandering

`NpcWander` (`Character/NPC`, pure) says each frame which way to face and how much to walk; the enemy's own brain tells it where the
body is and what is ahead (`NpcWanderProbe`: a ball cast ahead of the chest, and a ray for ground a stride ahead) and does what it
says, only while it has nothing to fight (the swordsman with nobody within his 40 m notice, the Shoota in his idle state, the Claw
with nobody within its 16 m). A fight over, it wanders from wherever it then is.

- The Claw turns where it hovers, then goes. The walking bodies turn only as they walk (and snap round if sent straight off another
  way), so they set off the way they face and come round to their heading in a curve; with something in the way they are steered,
  they shuffle round until it is clear.
- Measured in a walled yard with no player (`MobWanderPlayTests`): in a minute the swordsman covers 56 m, the Shoota 69 m, the Claw
  39 m; none stands longer than 5.5 s; none turns faster than 150 degrees a second.
- In a session the enemy's owner runs it and every copy is moved as the owner's body moves: every player sees the same wandering.

## In a session (two or more players)

Each machine runs its own spawner for its own player and **asks the server for each enemy** (`EnemyNetwork.TrySpawnWorldMob`: an
`EnemySpawnRequest` with the asker's own ticket for the ask and whether it is an indoor one). The server (`EnemyNetwork`):

- spawns it owned by the asker, whose machine runs its brain and whose city it stands in (as `/spawn` does), up to 140 m from the
  asker's body, and answers with the enemy (`WorldMobReply`);
- **counts what is alive near the asker, whoever runs it**: with as many alive within 130 m of the asker's body as the budget allows
  (out of doors and indoors each their own), it refuses. Players standing together share one budget, so two players in one street
  meet 4 outdoor enemies, not 8;
- is told of a kill by the machine that runs the enemy (`WorldMobKilled`; only that machine can), so a body lying dead does not count;
- **keeps the cleared buildings for everyone**: an indoor ask names its building; when the last of the world's enemies alive in a
  building is reported killed, the server gives that building no more, to anyone, for a day, and says so to whoever asks
  (`WorldMobReply.Cleared`), whose spawner then leaves the building alone.

**The Crimson Claw is shared** (`NetworkedCrimsonClaw`, 2026-11-14): its owner's machine runs it; every other machine's copy is moved
by the owner, decides nothing, plays the swipes the owner starts, sends the blows it takes to the owner, is told its health and dies
when the owner's does. It goes for the nearest player, on any machine. It seizes and throws only its own machine's player (the body
it holds is that machine's to move); another player is swiped at, and sees their friend thrown as any fallen body is seen. `/spawn
crimsonclaw` is shared too.

## Tests and pictures

- `MobHabitatsTests` (EditMode): open ground is outside and clear of every building and is for ghosts and monsters; indoors is inside
  an ordinary building, clear of its walls, with its floors one above the other under its roof, and is for humanoids; nowhere near
  Old Guy's house; the buildings listed near a place are ordinary ones and a spot rolled in one is inside it; a quarter monsters;
  the random spot is in the ring and favours no part of it.
- `MobSpawnerPlayTests` (PlayMode, the real scene, alone): standing in a street, enemies appear round the player, each (the frame it
  is first seen) the right kind in the right place at the right distance, never more than the budget; a humanoid stays on its floor;
  a killed one is counted no longer; the ones left behind are taken away; a building cleared of its enemies stays empty while others
  fill; turned off they are taken away and none appear.
- `NpcWanderTests` (EditMode) and `MobWanderPlayTests` (PlayMode): strolls and stands by turns, stays about where it began, turns
  aside from a wall and from a drop, wanders from where a fight left it; the real swordsman, Shoota and Claw in a walled yard.
- `NetworkedPlayerStateSyncPlayTests.TheWorldsEnemiesNearAPlaceAreOneBudgetForAllThePlayersThere` (two machines): one budget for
  players standing together, indoor ones their own, a dead one and one taken away count no longer, only its runner can call an enemy
  killed, a building one player cleared is refused to both, a kind the session does not have is not asked for.
- `NetworkedPlayerStateSyncPlayTests.ACrimsonClawIsInTheSamePlaceForBothPlayersAndItsCopySwipesIsHurtAndDiesWithIt` (two machines).
- `MobSpawnCapture` (Explicit, `-Graphics`): `Logs/CityShots/mobs-<n>-<kind>-<outdoors|indoors>.png` in the game's own city, and the
  counts.
- `MobCostBench` (Explicit, `-Graphics`): a frame timed with the world's enemies alive, lying dead and absent, in the game's own city
  (`Logs/Benchmark/mobs-cost.txt`), and a profile of each recorded for `ProfileReport`.
- `EnemyTargetSearchPlayTests`: each enemy with nobody near (the swordsman, the Shoota, the Giant, the Crimson Claw) looks for someone
  to fight about once a second, not every frame.

## Not in this slice

- **Levels.** Asked on 2026-11-13 whether a mob's level should come from the place (my recommendation) or scale to the player, the
  owner has not said. Every enemy is as strong as `/spawn` makes it.
- **Noticing and chasing.** Nothing is new about how an enemy notices the player or gives up a chase: the swordsman comes for anyone
  within 40 m, through walls; the Shoota shoots on sight, so one indoors will shoot from a window.
- **Minecraft's other rules**: nothing depends on light or the time of day, and nothing despawns on its own while the player is near.
- **Cleared buildings are remembered in memory only**: by the spawner playing alone (until the game is closed), by the server in a
  session (until it ends). Nothing is saved to disk. "Cleared" means the last enemy alive in it was killed, however many that was.
- **The Claw's grab is not shown on other machines** as a grab: they see the swipe, then the thrown player's body.
- **The server deciding the spawns itself** (today each player's machine asks; with server authority it moves there with the rest,
  `docs/GAME_DESIGN.md` "Technical foundations").
- **The sewers**: nothing appears below the streets.
- **Frame rate with ten enemies in a built game** was not measured (their cost to the processor in the editor was: above).

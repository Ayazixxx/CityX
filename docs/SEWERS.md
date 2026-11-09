# The Sewers: flood tunnels, old sewers and the ways in

Started 2026-11-09. The owner's words, the research and the plan; "Built so far" at the end says what exists.

## What the owner asked for

- 2026-11-09, the entrances: "the manholes in the ground will be an obvious entrance but those will be harder to utilize because it will
  require explosives to destroy or a high strength stat to just lift it up yourself. maybe telekinetic powers can also raise it which we will
  add later. but I also think there should be other entrances you can find" (a gated round stone arch in an old wall), and "there should be
  different types of sewer entrances".
- Flood tunnels "should lead to the sewers zone and be semi part of it"; they "need to be built into an elevated terrain or something like
  that they cant just be on surface level it wouldnt make sense"; remembered as "under bridges or beside train tracks".
- The city: "im planning to eliminate the large gaps between neighborhoods it should be a giant megacity"; "it needs to make sense on a grand
  scale dont focus on the current seed"; "im planning on adding a train and maybe subway system later"; "i want the city to feel very layered
  ... you may have to make changes to the city structure to make this all work".
- The look (refs in `docs/research/sewers_2026_11_09/refs/`): "all of this looks good to me. i think its a mixture of both flood tunnels and
  sewers".
- Answers to seven questions: tunnel size is mine to choose; **one world**, no loading screen; colour mine for now; "mostly pitch black aside
  from lights from encampments, maybe some rare hidden rooms down there with electricity that feel kind of like the hatch from the show lost
  but its like the organization has a hidden operating room in the sewer"; water "ankle deep mostly, up to waist deep in some areas", sludge
  effects undecided; inhabitants later ("lizard people will have encampments in the sewers and sometimes you can seem them on the surface and
  they run away from you and crawl into storm drains", and other enemies and NPCs not made yet); where to start is mine.
- First to build: "make the manhole ladder entrances ... i guess you will have to make ladder climbing so make that look good with hand ik.
  also make the flood tunnel entrances too". And: "variety is good this is an infinitely generating world needs lots of variety study the
  references images for inspiration".

`docs/GAME_DESIGN.md` already had: the Sewers are the first zone below the surface, a tunnel maze with an enemy family and a dungeon; every
way down is found, behind a gate that takes curiosity or knowledge; the street's gutters and manholes are built with the Sewers.

## What the research found

Four sets of notes, each fact with its source and a list of what could not be verified, in `docs/research/sewers_2026_11_09/`:
`real_cities.md`, `game_craft.md`, `ladders_and_entrances.md` (and the Las Vegas notes below, gathered first).

- **Las Vegas has flood tunnels, not sewers**: open concrete channels behind fences that dive underground where they meet roads, through a
  flat headwall with a row of square mouths; no gates; people live in them; a storm fills them about a foot a minute; floods blow manhole
  lids off and people have climbed out through them.
- **Paris lays a gallery under every street, sized by the street**: 1.3 m ovoids under small streets, 3 m collectors with a 1.2 m channel,
  5 to 6 m collectors with a 3.5 m channel under the boulevards, a walkway each side, and the street's name on a plate at every corner
  underground.
- **Hill cities send water down a level through shafts**: Hong Kong's West Drainage Tunnel takes the runoff off the Mid-Levels through 34
  intakes and drop shafts up to 195 m deep; Genoa's drops 42 m into a chamber. Vortex shafts, plunge pools and stepped cascades are how.
- **The big rooms are real**: Tokyo's pillared tank (177 x 78 x 25 m, 59 pillars) and silos (65 m deep, 32 m across), Hong Kong's tanks
  under sports pitches, Istanbul's cistern (336 columns 9 m tall).
- **Chongqing buried its streams** (channels tens of metres wide became sewers a few metres wide) and has over 1,600 air-raid tunnels in
  its hillsides, some reused as walkways joining a road to another 30 m lower; its riverside street levels flood 8 to 9 m deep.
- **Ladders**: rungs 25 to 36 cm apart and at least 18 cm off the wall (OSHA); one run up to 7.3 m, a landing for deeper; climbers keep
  their arms nearly straight and always three points of contact. Games' recurring faults: no clear way on at the top, players walking past
  ladders that do not stand out, being pushed off the bottom by a held stick.
- **Game craft**: a third-person corridor wants at least two body widths, and for our camera about 5 m wide and 4.5 m high; a camera that
  cannot fit should come in close, not go through the wall; dark games keep scenery faintly readable and use light as the landmark;
  flowing water shows the way; build the maze as a graph with loops; a kit of pieces on one footprint; no plain stretch past about 150 m.

## The design

**One network under the real streets, in three sizes, plus a deep level later.** The sewers are a pure function of the city plan (as the
city is of the seed), so every machine builds the same tunnels and nothing is sent.

| Layer | What | Where | State |
|---|---|---|---|
| Street | manholes, kerb drains big enough for a lizard person, grates | every junction; along streets later | manholes built (see below) |
| Galleries | old brick: a channel down the middle, a walkway each side, a barrel vault | under every ordinary street | built |
| Collectors | the same, larger | under arterials | built |
| Flood tunnels | plain concrete trunks | under boulevards, and out to the channels | built |
| Chambers | a room where tunnels meet, a shaft and ladder up to the street | under every junction | built |
| Mouths | a headwall and a gate where a tunnel comes out of a bank, an open cut beyond it | where a road meets a dry channel (under its bridge) | built |
| Deep level | cisterns, pillared halls, drop shafts and waterfalls, hidden lit rooms of the Organization, the dungeon, the way down to the Caves | below the galleries, on its own layout | not built |

- **Depth.** The floor is `SewerPlan.Depth` under the ground all along a road, so a tunnel climbs and falls with its street. The subway,
  when it comes, goes below the channels' floors; the deep level below that.
- **Sizes** are for the camera (5 m behind a 2.6 m body): the smallest gallery is 5.6 m wide and 5.2 m to its crown.
- **Darkness.** Below ground the sky gives nothing: the sky's light, its reflections and the haze fade out as the camera goes down
  (`DayNightCycle.Underground`). What lights a tunnel is the daylight falling through an open manhole or a mouth, a flashlight, and later
  the camps' lamps.
- **Variety** comes from families picked by the seed: the tunnel's section (brick barrel vault, stone arch, round concrete pipe, concrete
  box), the gate at a mouth (none, bars with a door ajar, bars with a gap), the ladder (step irons, a railed ladder), the manhole (lid on,
  lid lying beside the hole).
- **Manholes for now.** A share of them (`SewerPlan.OpenShare`) have their lid off, chosen by the seed, and those are the ways in. The rest
  are shut. Opening a shut one (explosives, Strength, later telekinesis) is shared world state and is the next step.

## The city above (not built; the order matters)

Removing the gaps between neighbourhoods, narrowing the dry channels (today a few hundred metres wide at the game's scale, with gentle
banks) into sunk concrete channels with straight walls, and keeping a strip for the railway beside them each reshape the plan, and each is
a new world seed. Corridors first (channels and rail), then fill the rest with city. The sewers follow whatever the streets become. The
owner has not yet said whether sunk channels as the city's main lines feel right.

## Built so far

### The network, the ways in, the ladder and the dark (2026-11-09)

**The plan** (`World/Sewers/SewerPlan.cs`, pure, one per city plan, cached):

- **A tunnel under every road** but a bridge and a step street (`SewerTube`): Gallery under a street, Collector under an arterial, Trunk
  under a boulevard (`SewerSection.HalfWidthOf`: 3.0, 3.6, 4.6 m each side). Its rings are about 6 m apart, each `Depth` (8.6 m) under the
  ground there, so it climbs and falls with its street.
- **Its section** (`World/Sewers/SewerSection.cs`) is one of four styles, shared by a neighbourhood of about 320 m with one tunnel in five
  its own: a brick barrel vault, a stone arch on taller walls, a round concrete pipe, a concrete box (boulevards: box or pipe). All have
  the channel and two walkways; the smallest floor is 4.7 m wide and the lowest crown 4.9 m.
- **Its colour** (`SewerPlan.ToneAt`) is its neighbourhood's too, of four: the brick is old brick, grey, blue, grey-yellow or
  green-black (four slices added to the facade textures, `FacadeTextures`: the houses' brick drawn in other colours, the joints dark with
  damp), and the stone is tinted cool grey, green-grey, blue-grey or dark. Owner, 2026-11-09, of the first pictures, whose vaults were the
  houses' red brick: "the red brick texture in the sewers really doesnt look right make them grey or something".
- **A room under every junction** (`SewerRoom`): the junction's own outline at the tunnels' widths (`PlanGeometry.Build` with a width per
  road and a least trim of 1.5 m, so even a straight-through junction is a room), its floor and ceiling following the ground, 7.4 m high.
  Each tunnel comes in through its own mouth in a wall.
- **A shaft** over the room's middle where there is 1.7 m clear round it and the street is level within 0.3 m (`SewerShaft`): an
  eight-sided neck 1.24 m across through the road's slab and a free-standing ladder from the floor to the street, facing down one of the
  tunnels. Three lids in ten are off (`OpenShare`); the rest are shut for now.
- **A mouth** (`SewerOutfall`) from each bank room of a road's bridge over a dry channel, where the bank falls far enough: the tunnel runs
  level from the room, the headwall stands where the bank has come down to 0.6 m over the crown, and an open cut between two wing walls
  goes on to where the bank meets the floor. A third are open, the rest barred: a door ajar in a yellow frame, or bars rusted through.
- `DarknessAt` says how far out of the sky's light a place is.

**The mesh** (`View/Streets/CitySurfaceBuilder.Sewers.cs`, `.SewerRooms.cs`, `.SewerMouths.cs`): written into the chunk's own mesh, so it
collides as the street does, near chunks only (a far chunk is still cut for a mouth, so the bank keeps its shape). Every face looks into
the tunnel: physics does not strike a face from behind and the shader does not draw one. An open manhole is a hole cut in its junction's
asphalt (`BuildPlate`), with a yellow ring painted round it and its lid lying beside it; a shut one is a lid in the road.

**The ladder** (anyone can climb one; no stamina):

- `Character/Movement/LadderGait.cs` (pure): which rung each hand and foot is on, from how high the body is and nothing else. Each limb
  takes every other rung, the diagonal pairs step together and one pair at a time, the hand a moment before its foot. Rungs are a real
  30 cm apart, not 1.3 times it: this body is 2.6 m tall on the legs of a 1.8 m one.
- `PlayerLocomotion.Ladder.cs`: two-bone IK after the Animator, each hand closed round its rung and turned to it, each foot on the ball
  on its rung, the hips swaying over the foot that carries the weight.
- `PlayerLadder.cs`: on at the foot by walking into it or with E; from the top with E, which turns the body round and backs it down (the
  pull-up's own path run backwards); W and S climb at 1.5 and 1.8 m/s and the body finishes its step when they are let go; Shift with S
  slides at 6.5 m/s; Space hops off backwards, Control drops; at an open top the body pulls up out onto the street (`PlayerClimbing.MantleOnto`:
  the same pull-up a wall's top gets); under a shut lid it stops.
- `Character/Interaction/LadderPrompt.cs`: the prompt, "[E] Climb" or "[E] Climb down" at an open manhole.
- Other players: `PlayerLadder` is an `IReplicatedAbility` ("ladder"). It says which ladder the body is on, again every second, and the
  copy poses its own limbs from its own height.

**The dark** (`View/Lighting/SewerAir.cs`, `DayNightCycle.Underground`): as the camera goes down a neck or in at a mouth the sky's light,
its reflections and the haze fade to `UndergroundSkyLight` (2 %) over about half a second. Daylight still falls down an open manhole and in
at a mouth. Under the ground the camera comes in as close as 0.3 m to stay inside the walls, and the body is not drawn within 0.95 m of it.

**Tests**: `SewerPlanTests`, `SewerMeshTests`, `LadderGaitTests` (EditMode); `SewerPlayTests` (a body falls down an open manhole and walks
25 m of tunnel; the street holds over a shut one; the player's body climbs the real ladder and out), `LadderPlayTests` (PlayMode).
Pictures: `SewerCapture` (`sewer-*.png`), `LadderCapture` (`ladder-*.png`).

**Not built yet**: water in the channels (the water shader takes only the sun's light, so it needs lamps and the flashlight first); camps
and their lamps; the deep level; opening a shut manhole; kerb drains; manholes along a street between junctions; mouths in hillsides and
retaining walls; step streets' cascades; street names underground; sound.

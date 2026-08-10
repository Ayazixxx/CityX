# CITY X — Godot → Unity port plan

Living document. Written at the point where the Godot build is ~30k lines of
game code + ~21k lines of tests across 94 headless test scenes, and the
target design outgrew what Godot's networking can carry.

---

## Why this port is happening

Not graphics. **Concurrent scale.**

The target design is dozens of players per world, faction warfare, NPCs
fighting alongside them, and player summons — meaning every player is a
source of additional AI entities. Realistically 200–400 networked entities
in active combat.

Godot's high-level multiplayer has no interest management: a
`MultiplayerSynchronizer` replicates every entity to every peer, so
bandwidth scales as players × entities. There's no client prediction with
server reconciliation, no lag compensation, and no dedicated-server tooling.
All of that would have to be written from scratch.

A second wall arrives sooner than the first: GDScript throughput. The
current build already runs three steering raycasts per NPC per frame across
8 NPC types. At 200+ entities that is a scripting-language bottleneck before
combat, animation or pathing are counted.

**Explicitly NOT reasons for this port:** the renderer, the art, or the
lighting. The city looks unfinished because it is untextured, which is an
art pass that would be identical in either engine.

---

## The one rule that governs everything

> **There is exactly one simulation, it is server-authoritative, and
> singleplayer is a server with one local client.**

**The WORLD is identical in all three modes.** Same infinite city, same
generator, same seed space, same content. What differs is only *how many
players are in it* and *who is running the server*. There is no "small
world" build — a private session is the same city as a regional shard, with
two people in it instead of forty.

| Mode | Players | Topology |
|---|---|---|
| **Singleplayer** | 1 | Local server, no transport, no relay |
| **Private session** | ~2–8 | Listen server (a player hosts), friends connect |
| **Regional shard** | dozens | Headless dedicated server build, persistent |

This is the single most important decision in the plan, and the classic way
these ports fail is violating it: building singleplayer with client-side
logic first, then trying to bolt networking on afterwards. Do that and
singleplayer and multiplayer become two divergent games that break each
other forever.

Concretely this means **no gameplay logic ever runs "just on the client."**
Client sends input intent → server simulates → server replicates state →
client predicts locally and reconciles. Even in singleplayer. The cost is a
little indirection in singleplayer; the benefit is that the mode matrix
above is a config flag rather than three codebases.

This is a genuine rewrite of `player.gd`'s shape, not a translation. Today
it acts locally and checks `is_multiplayer_authority()` in places. That
inverts.

---

## The biggest asset we're carrying over

**The world is a seed, not a level.**

`city_chunk.gd` generates everything from `building_params_for_seed`. Server
and client can generate byte-identical cities from the same seed, so **no
geometry is ever sent over the network**. Only *dynamic* state replicates:

- players, NPCs, summons
- broken glass, opened doors
- claimed buildings
- dropped items

That is an enormous head start on the networking problem, and it is worth
protecting. **Keep world generation deterministic and side-effect free.**
Any change that makes generation depend on runtime state breaks this.

---

## Phase 0 — Decide the stack, then prove it (BLOCKING)

Nothing else starts until this is answered, because it is the entire reason
for the port. If it fails, the port is off and we stay in Godot.

### Networking library

| Option | For | Against |
|---|---|---|
| **FishNet** *(recommended)* | Free, self-hostable, built-in prediction + reconciliation, observer/interest management, supports dedicated + host + offline modes cleanly | Smaller community than Mirror |
| **Mirror** | Most mature, biggest community, built-in spatial interest management, free, self-hostable | Prediction is more DIY |
| **Photon Fusion 2** | Best-in-class prediction/AoI/lag-comp out of the box | Room-based, Photon-hosted, per-CCU pricing at scale; persistence is still yours |

Recommendation is **FishNet**, because self-hosting is a hard requirement
for regional shards you control and per-CCU pricing on a persistent world
gets punishing. Validate in the spike; this is reversible now and expensive
later.

### The spike — build ONLY this

Capsules and cubes. No art, no city, no combat, no inventory.

- Headless dedicated server build
- 30 simulated clients connected
- 200 server-driven AI capsules moving and "fighting"
- Interest management on, so a client only receives nearby entities
- Client-side prediction + reconciliation on player movement

**Exit criteria — all must hold:**

- [ ] Server holds tick rate with 30 clients + 200 AI
- [ ] Per-client bandwidth stays flat as total entity count grows (proves AoI works)
- [ ] Movement feels responsive at 100ms simulated latency
- [ ] The same build runs offline with zero transport (singleplayer path)
- [ ] A client can host and others join (private-session path)

Note that entity count does **not** drop much in a private session: the
world is the same size, so faction NPCs and wildlife are still there. Two
players in an infinite city still need interest management — it's the world
that makes AoI necessary, not the player count.

**Estimate: days, not weeks. Do not skip. Do not port content first.**

---

## Phase 1 — Project skeleton and mode switching

- Unity project, URP, assembly definitions per system
- `GameMode` bootstrap: `Singleplayer | HostedWorld | DedicatedShard`
- One entry scene that starts the right topology from that flag
- CI running the test framework headless from day one

**Exit:** all three modes boot to an empty world with a controllable capsule.

---

## Phase 2 — Deterministic world generation

Port `city_chunk.gd` and `city.gd`. This is the most mechanically portable
system in the project — it's pure math over a seed.

- Chunk streaming keyed on player position (XZ only, as today)
- Server and client generate independently from the shared seed
- Verify byte-identical output between server and client for a given seed
- Port the existing generation tests first — they encode a lot of hard-won
  geometry knowledge (door walls, glass bands, stair corners, basements)

**Watch for:** `SurfaceTool` → Unity `Mesh` API. Straightforward, but
Unity's mesh is index-buffer oriented; budget a day for the translation
layer rather than fighting it per-call-site.

**Exit:** two independently-generated cities from one seed are identical,
and streaming holds frame time while running.

---

## Phase 3 — Character, rig and animation

- Port `fused_body.gd` → runtime `SkinnedMeshRenderer` (bindposes, bone
  weights). Unity supports this properly; it is not the blocker it would
  have been in Unreal.
- Port the paint atlas + character creator
- **Replace the bespoke Mixamo retargeting with Unity's Humanoid rig.**
  This is a system that gets *deleted*, not ported — one of the real wins.
- Animation state driven by replicated state, not by local input

**Carry over as knowledge, not code:** the rig constants
(`HIP_Y 0.9`, `SHOULDER_Y 1.9`, `SHOULDER_X 0.35`, etc.) and the reasons
they are what they are. Those were tuned against real problems.

**Exit:** a character generates, is painted in-creator, animates from
replicated state, and looks correct on a remote client.

---

## Phase 4 — Combat, server-authoritative

The rewrite-heaviest phase. Today combat resolves locally and reports
results; it must invert.

- Input intent → server → hit resolution → replicated result
- Lag compensation for melee/hitscan (rewind targets to the attacker's view)
- Port `combo_data.gd` chains, hitstun, blocking, dash i-frames, lock-on
- Ragdolls: **cosmetic and client-side.** Do not replicate ragdoll physics.
  Replicate "died, with this impulse" and let each client simulate it.

**Exit:** two real clients can fight at 100ms latency and both agree on who
hit whom.

---

## Phase 5 — Entities at scale

- Summons and faction NPCs as server-simulated entities
- Server-side AI LOD: distant entities tick at reduced rate
- Jobs/Burst for steering and target acquisition if profiling demands it
  (do **not** reach for full DOTS/ECS pre-emptively — it's a large learning
  cost and mixes awkwardly with GameObjects)
- Port `npc_steering.gd`; consider Unity's NavMesh with runtime carving as a
  replacement, which would fix the known "won't follow you indoors" gap

**Exit:** the Phase 0 spike numbers hold with *real* NPCs, not capsules.

---

## Phase 6 — Persistence (new subsystem — does not exist today)

Today saves are ConfigFile in `user://` on the client. For regional shards
that has to move server-side and become authoritative.

- Character data, progression, inventory, unlocked skills
- Claimed buildings, explored chunks
- SQLite for singleplayer and private sessions; Postgres for regional shards
- Same interface behind both, chosen by `GameMode` — the *schema is
  identical*, because the world and its content are identical; only the
  store and the identity model change
- Account identity for shards; local profiles otherwise
- A private session's save is a file the host owns, so it must be portable
  and self-contained (the host is somebody's PC, not infrastructure)

**This is the sleeper item.** It's not hard, it's just genuinely new work
with no Godot equivalent to port, and it's easy to leave until it blocks
everything.

---

## Phase 7 — Content parity

Everything else, in rough value order: inventory + paper doll, item
registry, wearables (the cloak and jeans glb files import directly), the
weapon set, loot, map, day/night, weather, the intro cutscene, Old Guy and
the tutorial.

The Blender pipeline (`tools/meshwork/*.py`) is **engine-agnostic and moves
unchanged** — it outputs glb.

---

## Explicitly NOT being ported

Decided 2026-08-10. These exist and work in the Godot build, but they did
not come out well and are **not** worth carrying to parity. They stay in the
Godot repo as reference; rebuild them fresh in Unity later if and when they
are actually wanted.

This is a real saving, not just a deletion — it is roughly a fifth of the
content-parity surface, and the three heaviest physics/animation problems in
the whole port are all in this list.

**Vehicles and driving.** `vehicles/car.gd`, `vehicles/mustang.tscn`, the
mustang glb, and everything downstream of them: the multi-seat
DriverSeat/PassengerSeat system, the nearest-seat E interaction, the
`entering_car`/`exiting_car` retargeted animations and their hip-reset
special cases in `character.gd`'s `_on_animation_finished`, the `_seated_car`
early-return in `player.gd::_physics_process`, and `/spawn mustang`.

Worth knowing before any future rebuild: driving feel, handbrake/drift,
chase camera and collision tuning were never finished in Godot either — they
were staged out and left pending. So there is no tuned reference to port.
A Unity rebuild would start from Unity's own WheelCollider / vehicle physics
rather than translating `car.gd`'s RigidBody3D forces, which is a better
starting point anyway.

**Grappling hook.** `items/grappling_hook.gd` / `.tscn`, its
`items/item_registry.gd` entry, its hooks in `player.gd`, its `/spawn`
command, and `tests/test_grappling_hook_check.gd`.

**Wolf and sloth.** `npc/wolf.gd` and `npc/sloth.gd`. Both are standalone
procedurally-built creatures with no dependency on the player rig, so they
drop out cleanly — nothing else references them.

### Which enemies DO get ported

| Keep | Why it survives |
|---|---|
| **Mutant** (`npc/mutant.gd`) | Meshy Skulljaw rig with native animations — imports to Unity directly |
| **Scratcher** (`npc/scratcher.gd`) | glb + baked clips, same |
| **Crimson Claw** (`npc/crimson_claw_monster.gd`) | split body/claw meshes, real hit detection |
| **Player-model NPCs** | `civilian.gd`, `cpu.gd`, `gunman.gd`, `unique_npc.gd`, `old_guy.gd`, `npc_design.gd` + `npc/designs/*.tres` — these ride the same rig and animation set as the player, so they come almost free once Phase 3 lands |

`npc/npc_steering.gd` still ports (or is replaced by NavMesh per Phase 5) —
it is shared by every NPC above, not tied to the dropped ones.

---

## What survives, what dies, what improves

**Survives (port directly):**
city generation math · Blender asset pipeline · glb/audio assets ·
item registry data · combo tables · the design decisions recorded in code
comments (unusually thorough here — treat as spec)

**Dies (rewrite):**
`player.gd`'s local-authority shape · all Godot node/scene wiring ·
the multiplayer layer · save/load

**Improves:**
Mixamo retargeting → Unity Humanoid · NPC nav → NavMesh · profiling →
Unity Profiler · tests → Unity Test Framework

**Carried as knowledge, not code:**
Lighting values that took real work — warm sun `(1.0, 0.955, 0.885)`,
~8:1 sun-to-ambient, filmic (AgX/ACES) tonemap, sun angular size ~1°,
glow off. These are engine-independent and were arrived at by measurement.

---

## Honest costs and risks

**Estimate to parity: 1–2 months** of focused work, not 1–2 weeks. The line
count isn't the driver; the un-portable part is the bug-fix debt. Crouch
clipping, stair z-fighting, UV degeneracy, the character-select freeze, the
streaming hitch, the shadow cascade seam — those were *found by playing*
over months. A fresh implementation inherits a fresh crop instead.

**Risks, highest first:**

1. **Phase 0 fails.** Mitigated by doing it first, in days, before anything
   else is invested.
2. **Server-authoritative rewrite is underestimated.** It's the real work of
   this port. Combat especially.
3. **Persistence gets deferred** until it blocks a shard launch.
4. **Scope creep during the rewrite** — "while I'm in here I'll improve it."
   Port to parity first, improve after.

**Keep the Godot project.** Don't delete it, don't treat it as a failure.
It is a working reference implementation you can run side by side, and a
spec that answers "how was this supposed to behave?" That's worth a lot.

---

## Immediate next step

Phase 0 spike. Nothing else.

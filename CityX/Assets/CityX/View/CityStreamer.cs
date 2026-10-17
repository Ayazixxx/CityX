using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Builds and drops chunk meshes around a viewer.
    ///
    /// This began as the simplest thing that let the world be looked at and walked through, and it built every
    /// chunk on the main thread -- about 3ms each, 4-6ms every frame of walking, and the biggest single item in
    /// a measured profile of the running game (13ms a frame while the ring filled). A chunk is a pure function of
    /// its coordinates (CityWorld, CityLayout and CityMeshBuilder read only their inputs, and the one cache is
    /// [ThreadStatic]), so the GEOMETRY is now generated on worker threads and the main thread only turns
    /// finished chunks into scene objects, within a small time budget per frame. See GeneratorThreads. Their
    /// colliders are cooked on workers too, the chunk going on screen when that is done (BeginSwap). The
    /// synchronous path (StreamAround, BuildChunkObject) is unchanged in behaviour for the spawn pre-stream, for
    /// rebuilding a chunk when a window breaks with no workers, and for tests and captures: solid at once.
    /// </summary>
    public sealed class CityStreamer : MonoBehaviour
    {
        [Header("World")]
        // Matches CityMassingSceneBuilder.WorldSeed -- a fallback default
        // for a CityStreamer added without one explicitly assigned; the
        // real scenes always set this from that same constant at build
        // time. EXPLICIT ASK: "generate a new world every time you make a
        // change to the city generation."
        public int WorldSeed = 19077151;

        [Tooltip("Put the player (and the Old Guy, who is placed relative to them) at the spawn point of the world this streamer makes, when it wakes. The scene bakes a spawn point at build time, but the world depends on the city scale in force when the game runs.")]
        public bool PlaceAtSpawn = false;

        [Tooltip("Chunks of radius kept loaded around the viewer.")]
        public int RadiusChunks = 14;

        [Tooltip("Chunks of radius around the viewer built in FULL detail (rooms, stairs, windows you can see through). " +
                 "Beyond it a chunk is the far version: the building's outside only. 0 keeps every chunk full detail.")]
        public int FullDetailRadiusChunks = 8;

        [Tooltip("A full-detail chunk only goes back to the far version this many chunks beyond FullDetailRadiusChunks, " +
                 "so walking along the edge does not rebuild the same chunks over and over.")]
        public int DetailHysteresisChunks = 2;

        [Tooltip("Most chunks built in one frame. Low keeps the frame smooth; high fills in faster.")]
        public int ChunkBuildBudgetPerFrame = 6;

        /// <summary>
        /// The most milliseconds one frame's streaming may spend building chunks,
        /// checked after each one (so a frame always builds at least one).
        ///
        /// A COUNT ALONE IS NOT A BUDGET WHEN CHUNKS DO NOT COST THE SAME. A
        /// chunk's geometry cost grows with its building's height -- about 0.5ms
        /// under 40m, 2ms at 100m, 4.5ms at 200m, 10ms at 300m -- and the zoned
        /// city puts its tall buildings together in downtown cores. Six chunks
        /// a frame was ~8ms of streaming in the old uniform city and up to
        /// ~27ms walking through a core of towers. Whichever limit is reached
        /// first ends the frame's streaming.
        /// </summary>
        [Tooltip("Most milliseconds of chunk building per frame; whichever of this and the chunk count comes first ends it. Only the synchronous path uses this now (see GeneratorThreads).")]
        public float ChunkBuildTimeBudgetMs = 6.0f;

        [Header("Background generation")]
        [Tooltip("Worker threads that generate chunk geometry while the game runs. The main thread then only makes the " +
                 "objects. -1: as many as the machine can spare (half its logical cores less one, 3 to 6). " +
                 "0 goes back to building every chunk on the main thread (the old, hitchy way).")]
        public int GeneratorThreads = -1;

        [Tooltip("Most milliseconds per frame the main thread spends turning generated chunks into scene objects " +
                 "(at least one chunk a frame is always finished).")]
        public float AssembleTimeBudgetMs = 2.0f;

        [Tooltip("Most chunks being generated at once (at least twice the worker threads); more just queues work that may be stale by the time it lands.")]
        public int MaxChunksInFlight = 6;

        /// <summary>
        /// The worker threads actually used: <see cref="GeneratorThreads"/>, or for -1 what the machine can spare. Fixed at three until
        /// 2026-10-12; the streaming bench (StreamingPopInBench) showed the detailed version of the chunks round a 45 m/s flyer arriving late on
        /// three, and a Ryzen 7 5800H has sixteen.
        /// </summary>
        public int WorkerThreads => GeneratorThreads >= 0 ? GeneratorThreads : Mathf.Clamp(SystemInfo.processorCount / 2 - 1, 3, 6);

        /// <summary>
        /// How many chunks around the real player get force-built
        /// synchronously in Awake, before anyone's own Update can run at
        /// all -- EXPLICIT BUG REPORT: "the thing where i fall through at
        /// the beginning... occurs every time i enter the game." Ordinary
        /// streaming is lazy and budgeted PER FRAME (see Update's own
        /// StreamAround call, ChunkBuildBudgetPerFrame chunks at a time) --
        /// on the very first frame this scene exists, zero chunks have
        /// been built yet, and whichever of PlayerInputReader.Update or
        /// this class's own Update happens to run first that frame is not
        /// something either script controls. If gravity integrates even
        /// once before the ground under the player's own feet exists, that
        /// is a real, if brief, fall into empty space -- "temporary" is
        /// exactly what a several-frame gap at 60fps reads as. Awake, not
        /// Start or Update: Unity runs every object's own Awake before any
        /// object's first Update, every frame including the first, which
        /// is what actually closes this race rather than narrowing it.
        /// 49 (a 7x7 grid) comfortably covers a spawn point and the
        /// ground immediately around it at a one-time cost of roughly
        /// 100ms (see BuildChunkObject's own "about two milliseconds"
        /// comment) -- a brief load pause, not the stutter a much larger
        /// synchronous build would be.
        /// </summary>
        public int SpawnPreStreamBudget = 49;

        [Header("Look")]
        public Material GroundMaterial;

        public Material PavementMaterial;

        public Material StructureMaterial;

        [Tooltip("Window glass with no light behind it.")]
        public Material DarkGlassMaterial;

        [Tooltip("Window glass with a light on. DayNightCycle brings this one up at dusk.")]
        public Material LitGlassMaterial;

        [Tooltip("A window somebody has already put a fist through. Drawn, never collided with.")]
        public Material CrackedGlassMaterial;

        [Tooltip("The real crack-texture decal at a known shot's own impact point. Drawn, never collided with.")]
        public Material CrackDecalMaterial;

        [Tooltip("The glowing faces of a powered room's light fittings while its switch is on (unlit; the colour is set per room).")]
        public Material RoomGlowMaterial;

        /// <summary>A lit room's window veil (Shaders/CityXWindowLight.shader). Made from the shader at run time if the scene does not set it.</summary>
        public Material WindowLightMaterial;

        [Header("Doors")]
        [Tooltip("The door leaf. Warm wood brown, a fixed prop accent.")]
        public Material DoorMaterial;

        public Material DoorHandleMaterial;

        [Header("Lane paint")]
        [Tooltip("The double yellow down a straight street.")]
        public Material RoadLineMaterial;

        [Tooltip("The white zebra bars at a junction.")]
        public Material CrosswalkMaterial;

        [Header("Terrace edges")]
        [Tooltip("The short guardrail along a street-level drop.")]
        public Material GuardRailMaterial;

        [Header("Open blocks")]
        [Tooltip("Lawns and tree canopies in parks -- see CityMeshBuilder.AddOpenBlockSurface. Leave empty to skip them.")]
        public Material GrassMaterial;

        [Tooltip("Tree trunks. Leave empty to skip the greenery entirely.")]
        public Material BarkMaterial;

        [Header("Props")]
        [Tooltip("Park benches -- see BenchSeat.cs. Leave empty to skip spawning them.")]
        public Material BenchMaterial;

        [Tooltip("Readable books -- see Book.cs. Leave empty to skip spawning them.")]
        public Material BookMaterial;

        // A SPECIAL BUILDING WEARS NOTHING THAT SAYS SO, and that is a decision
        // rather than an omission. city_chunk.gd hangs a coloured sign band
        // over the door of every supermarket, bank, police station and
        // hospital, and this port had one too for about an hour. Taken back off
        // on request: the types still roll, with their own footprints, heights
        // and roofs, but nothing labels them. Anyone reading city_chunk.gd and
        // "fixing" the divergence would be undoing that.

        [Tooltip("Leave empty to follow the main camera.")]
        public Transform Viewer;

        private CityWorld _world;
        private readonly Dictionary<Vector2Int, GameObject> _loaded = new Dictionary<Vector2Int, GameObject>();

        /// <summary>The loaded chunks that are the far version. Everything else in <see cref="_loaded"/> is full detail.</summary>
        private readonly HashSet<Vector2Int> _far = new HashSet<Vector2Int>();

        /// <summary>Whether a chunk is loaded as the far version. For tests and captures.</summary>
        public bool IsChunkFar(Vector2Int key) => _far.Contains(key);

        /// <summary>Whether a chunk is on screen at all (either version). For tests and the streaming bench.</summary>
        public bool IsChunkLoaded(Vector2Int key) => _loaded.ContainsKey(key);

        /// <summary>A chunk to build (or rebuild at another detail): where, and whether it is the far version.</summary>
        private readonly struct ChunkRequest
        {
            public readonly Vector2Int Key;
            public readonly bool Far;

            public ChunkRequest(Vector2Int key, bool far)
            {
                Key = key;
                Far = far;
            }
        }

        /// <summary>
        /// Whether a chunk this far (in chunks, either axis) from the viewer should be the far version.
        /// A chunk already loaded full keeps it until it is DetailHysteresisChunks past the boundary.
        /// </summary>
        private bool WantsFar(int distance, bool loadedFar)
        {
            if (FullDetailRadiusChunks <= 0)
            {
                return false;
            }
            int limit = loadedFar ? FullDetailRadiusChunks : FullDetailRadiusChunks + Mathf.Max(0, DetailHysteresisChunks);
            return distance > limit;
        }

        /// <summary>
        /// Everything generated for ONE chunk, before any scene object exists: each mesh buffer, the openings, and
        /// the main mesh already laid out the way the Mesh keeps it. Made on a worker thread and read on the main thread,
        /// so it owns its buffers (nothing here is shared between chunks) and touches no Unity object.
        /// </summary>
        private sealed class ChunkPayload
        {
            public Vector2Int Key;
            public int Generation;
            /// <summary>The distant version of the chunk (see CityMeshBuilder.BuildChunk's far parameter).</summary>
            public bool Far;
            public readonly MeshBuffer CrackedGlass = new MeshBuffer();
            public readonly MeshBuffer CrackDecals = new MeshBuffer();
            public readonly List<CityMeshBuilder.CrackedOpening> CrackedOpenings = new List<CityMeshBuilder.CrackedOpening>();
            public readonly MeshBuffer Markings = new MeshBuffer();
            public readonly MeshBuffer LaneDividers = new MeshBuffer();
            public readonly MeshBuffer GuardRail = new MeshBuffer();

            /// <summary>The powered rooms' lights (CityMeshBuilder.RoomGlow), built near only.</summary>
            public readonly List<CityMeshBuilder.RoomGlow> RoomGlows = new List<CityMeshBuilder.RoomGlow>();

            /// <summary>
            /// The main mesh, laid out off the main thread with its tangents (MeshTangents: Mesh.RecalculateTangents cost ~5 ms on the main
            /// thread) and its bounds, so that making it there is a few block copies (ChunkMeshData). Null when generating the chunk failed.
            /// </summary>
            public ChunkMeshData Main;

            /// <summary>Given back (Recycle) and waiting to be used for another chunk: nothing may read it.</summary>
            public bool Recycled;
        }

        // ---- storage used again, chunk after chunk (2026-10-14) ----------------------------------------------------------------------------
        // Every chunk was generated into fresh buffers whose lists grew from nothing, then copied into fresh arrays, all of it thrown away once
        // its mesh was made: ~60 MB a second while flying over the city, a garbage collection every tenth of a second in the built game, each
        // stopping every thread (the main one too) for 2-12 ms -- the game stood still ~2 s of a 30 s flight (PlayerFrameBenchmark, FLYING).
        // Now the buffers a main mesh is built in are used again (ChunkScratch), its arrays come from ChunkArrayPool and go back once the Mesh
        // is made, and the payloads themselves are used again (Recycle). What a chunk is does not change: a payload, a scratch and an array
        // are emptied or fully written before they are used, so a chunk is still a pure function of its coordinates (ChunkStorageReuseTests).

        /// <summary>
        /// The five buffers a chunk's main mesh is built in -- ground, pavement, walls, dark glass, lit glass -- in the groups ChunkMeshData
        /// combines them into (a submesh, and its material, each). Only the worker generating a chunk uses them, until they are combined into
        /// the payload's arrays; then the next chunk does, its lists already the size they grew to. One per chunk generated at once (a worker
        /// each, and the main thread's synchronous path), handed out and back here.
        /// </summary>
        private sealed class ChunkScratch
        {
            public readonly MeshBuffer Ground = new MeshBuffer();
            public readonly MeshBuffer Pavement = new MeshBuffer();
            public readonly MeshBuffer Structures = new MeshBuffer();
            public readonly MeshBuffer DarkGlass = new MeshBuffer();
            public readonly MeshBuffer LitGlass = new MeshBuffer();

            /// <summary>The submeshes in order, drawn with AssembleChunk's materialFor: the opaque surfaces (one material), dark glass, lit glass.</summary>
            public readonly MeshBuffer[][] Groups;

            private static readonly Stack<ChunkScratch> Free = new Stack<ChunkScratch>();

            private ChunkScratch()
            {
                Groups = new[] { new[] { Ground, Pavement, Structures }, new[] { DarkGlass }, new[] { LitGlass } };
            }

            public static ChunkScratch Take()
            {
                lock (Free)
                {
                    if (Free.Count > 0)
                    {
                        return Free.Pop();
                    }
                }
                return new ChunkScratch();
            }

            /// <summary>Emptied (MeshBuffer.Reset: exactly what a new buffer is, but with its room) and handed back for the next chunk.</summary>
            public void GiveBack()
            {
                foreach (MeshBuffer[] group in Groups)
                {
                    foreach (MeshBuffer buffer in group)
                    {
                        buffer.Reset();
                    }
                }
                lock (Free)
                {
                    Free.Push(this);
                }
            }

            /// <summary>The room the waiting scratches' lists have grown to, bytes, and how many there are (for the benchmark).</summary>
            public static (long bytes, int count) Held()
            {
                long bytes = 0;
                lock (Free)
                {
                    foreach (ChunkScratch scratch in Free)
                    {
                        foreach (MeshBuffer[] group in scratch.Groups)
                        {
                            foreach (MeshBuffer buffer in group)
                            {
                                bytes += buffer.Vertices.Capacity * 12L + buffer.Normals.Capacity * 12L + buffer.Uvs.Capacity * 8L
                                         + buffer.Colors.Capacity * 4L + buffer.Triangles.Capacity * 4L;
                            }
                        }
                    }
                    return (bytes, Free.Count);
                }
            }
        }

        /// <summary>What the buffers chunks are built in (ChunkScratch) hold between chunks, bytes, and how many sets of them there are. For the benchmark.</summary>
        public static (long bytes, int sets) ScratchHeld => ChunkScratch.Held();

        /// <summary>Payloads given back (Recycle), for the next chunks. Workers take from it, the main thread gives back.</summary>
        private readonly Stack<ChunkPayload> _freePayloads = new Stack<ChunkPayload>();

        /// <summary>How many payloads to keep for reuse: more than ever wait at once (the chunks in flight).</summary>
        private const int MaxFreePayloads = 64;

        private int _payloadsOut;

        /// <summary>Payloads handed out and not given back yet: the chunks being generated or waiting to be made. 0 once streaming is settled, or one leaked. For tests.</summary>
        public int PayloadsOut => Volatile.Read(ref _payloadsOut);

        /// <summary>A payload for one chunk, empty (any thread).</summary>
        private ChunkPayload TakePayload(Vector2Int key, bool far)
        {
            ChunkPayload payload = null;
            lock (_freePayloads)
            {
                if (_freePayloads.Count > 0)
                {
                    payload = _freePayloads.Pop();
                }
            }
            payload ??= new ChunkPayload();
            payload.Recycled = false;
            payload.Key = key;
            payload.Far = far;
            payload.Generation = _generation;
            Interlocked.Increment(ref _payloadsOut);
            return payload;
        }

        /// <summary>
        /// Gives a payload back once nothing needs it any more (any thread; nothing may read it after this): its main mesh's arrays to
        /// ChunkArrayPool, its buffers emptied, itself to the next chunk. Whatever was made from it keeps its own copy: a Mesh copies what it
        /// is given, and the powered rooms (ChunkRoomLights) keep the rooms, not the list they came in.
        /// </summary>
        private void Recycle(ChunkPayload payload)
        {
            if (payload == null)
            {
                return;
            }
            if (payload.Recycled)
            {
                Debug.LogError($"CityStreamer: chunk {payload.Key}'s payload was given back twice; its arrays could have ended up in two chunks");
                return;
            }
            payload.Recycled = true;
            Interlocked.Decrement(ref _payloadsOut);
            payload.Main?.Release();
            payload.Main = null;
            payload.CrackedGlass.Reset();
            payload.CrackDecals.Reset();
            payload.CrackedOpenings.Clear();
            payload.Markings.Reset();
            payload.LaneDividers.Reset();
            payload.GuardRail.Reset();
            payload.RoomGlows.Clear();
            lock (_freePayloads)
            {
                if (_freePayloads.Count < MaxFreePayloads)
                {
                    _freePayloads.Push(payload);
                }
            }
        }

        // The chunk being turned into scene objects right now (main thread only). The Add* methods below read
        // their buffers through these, so they did not have to change when the buffers moved into a payload.
        private ChunkPayload _current;
        private MeshBuffer _crackedGlass => _current.CrackedGlass;
        private MeshBuffer _crackDecals => _current.CrackDecals;
        private List<CityMeshBuilder.CrackedOpening> _crackedOpenings => _current.CrackedOpenings;
        private MeshBuffer _markings => _current.Markings;
        private MeshBuffer _laneDividers => _current.LaneDividers;
        private MeshBuffer _guardRail => _current.GuardRail;

        // Background generation state (main thread, except the queue and the semaphore).
        private readonly ConcurrentQueue<ChunkPayload> _finished = new ConcurrentQueue<ChunkPayload>();
        private readonly HashSet<Vector2Int> _inFlight = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _changedInFlight = new HashSet<Vector2Int>();

        // Chunks being rebuilt because a window in them was hit (true: only their cracked glass needs swapping), and the last state seen of each
        // pane, which tells another hole in a cracked pane from a pane changing state (OnPaneChanged).
        private readonly Dictionary<Vector2Int, bool> _refreshing = new Dictionary<Vector2Int, bool>();
        private readonly Dictionary<PaneKey, PaneState> _paneSeen = new Dictionary<PaneKey, PaneState>();
        private SemaphoreSlim _workers;
        private int _generation;
        private Vector2Int _wantedCentre = new Vector2Int(int.MinValue, int.MinValue);
        private bool _wantedStale = true;
        private readonly HashSet<int> _openDoors = new HashSet<int>();
        private readonly List<Vector2Int> _wanted = new List<Vector2Int>();
        private readonly List<ChunkRequest> _requests = new List<ChunkRequest>();
        private readonly List<Vector2Int> _stale = new List<Vector2Int>();
        private readonly ExploredChunks _explored = new ExploredChunks();

        private void Awake()
        {
            Rebuild();

            // See SpawnPreStreamBudget's own doc comment. Looked up by
            // PlayerInputReader specifically, not PlayerLocomotion -- the
            // scene also has Old Guy and, potentially, several Civilians,
            // every one of them carrying their own PlayerLocomotion, which
            // makes a type-only lookup genuinely ambiguous about which
            // body it finds (the same caution CityMassingSceneBuilder's
            // own player/old-guy wiring already documents). Only the real,
            // keyboard-driven player has a PlayerInputReader.
            var player = FindFirstObjectByType<PlayerInputReader>();
            if (player != null)
            {
                if (PlaceAtSpawn && _world.Plan.TryFindSpawn(out double spawnX, out double spawnZ))
                {
                    Vector3 from = player.transform.position;
                    float ground = (float)_world.Terrain.ContinuousHeightAt(spawnX, spawnZ);
                    var to = new Vector3((float)spawnX, ground + 0.5f, (float)spawnZ);
                    if ((to - from).sqrMagnitude > 0.25f)
                    {
                        var controller = player.GetComponent<CharacterController>();
                        bool wasEnabled = controller != null && controller.enabled;
                        if (controller != null) controller.enabled = false;   // a controller keeps its own copy of where it is
                        player.transform.position = to;
                        if (controller != null) controller.enabled = wasEnabled;
                        foreach (CityX.Character.OldGuySpawner oldGuy in FindObjectsByType<CityX.Character.OldGuySpawner>(FindObjectsSortMode.None))
                        {
                            oldGuy.SpawnPosition += to - from;
                        }
                    }
                }
                StreamAround(player.transform.position, SpawnPreStreamBudget);
            }
        }

        /// <summary>
        /// Which windows in this world are broken. Survives chunks streaming
        /// out and back, which is the whole reason it lives here rather than on
        /// a chunk -- see GlassDamage.
        /// </summary>
        public GlassDamage Damage { get; } = new GlassDamage();

        /// <summary>Drops everything and starts again. Called on seed changes.</summary>
        public void Rebuild()
        {
            // Anything a worker is still generating belongs to the old world: bumping the generation makes
            // its result be thrown away when it lands.
            _generation++;
            AbandonSwaps();   // before anything is destroyed: a worker may still be cooking a chunk's collider
            _inFlight.Clear();
            _changedInFlight.Clear();
            _refreshing.Clear();
            _pendingRefresh.Clear();
            _paneSeen.Clear();
            while (_finished.TryDequeue(out ChunkPayload stale))
            {
                Recycle(stale);
            }
            _wantedStale = true;

            _world = new CityWorld(WorldSeed);
            _landmarks = new HashSet<Vector2Int>();
            foreach ((int x, int y) in _world.Plan.LandmarkChunks)
            {
                _landmarks.Add(new Vector2Int(x, y));
            }
            foreach (KeyValuePair<Vector2Int, GameObject> pair in _loaded)
            {
                DestroyChunk(pair.Value);
            }
            _loaded.Clear();
            _far.Clear();
            // A different seed makes the old explored coordinates meaningless.
            _explored.Clear();

            // See CurrentWorld's own doc comment: this is the one place the
            // real, currently-running world actually gets built, so this is
            // the one place that can publish it for CityX.Character (which
            // has no reference to this assembly) to read.
            CurrentWorld.Instance = _world;
        }

        // The chunks that build a tall landmark building (see PlanWorld.LandmarkHeight). Their DISTANT version is kept much further out than
        // the ordinary radius: a 700 m tower that vanishes at 576 m is not a skyline.
        private HashSet<Vector2Int> _landmarks;

        // Only for a real view distance: a streamer with a tiny ring (tests, captures of one street) loads exactly its ring.
        private int LandmarkRadiusChunks => RadiusChunks >= 8 ? RadiusChunks * 5 / 2 : 0;

        private bool IsLandmarkInRange(Vector2Int key, int centreX, int centreY)
        {
            return _landmarks != null && _landmarks.Contains(key)
                && Mathf.Max(Mathf.Abs(key.x - centreX), Mathf.Abs(key.y - centreY)) <= LandmarkRadiusChunks;
        }

        /// <summary>Whether the player has ever stood within ExploredChunks.Radius of this chunk. Drives the map panel's fog of war.</summary>
        public bool IsChunkExplored(Vector2Int coord) => _explored.Contains(coord);

        /// <summary>Every chunk the player has been to, as a snapshot -- see ExploredChunks.ToArray.</summary>
        public Vector2Int[] GetExploredChunks() => _explored.ToArray();

        /// <summary>The world being streamed (null until the first build).</summary>
        public CityWorld World => _world;

        private void OnEnable()
        {
            Damage.Changed += OnPaneChanged;
            // A door onto a balcony opened or shut (World/BalconyDoors, drawn in the chunk's own mesh): its chunk is built again.
            BalconyDoorState.Shared.Changed += OnBalconyDoorChanged;
            // The glass other players break comes into this city's record (Character.GlassDamageNetwork).
            GlassDamage.Current = Damage;
        }

        private void OnDisable()
        {
            Damage.Changed -= OnPaneChanged;
            BalconyDoorState.Shared.Changed -= OnBalconyDoorChanged;
            if (ReferenceEquals(GlassDamage.Current, Damage))
            {
                GlassDamage.Current = null;
            }
            AbandonSwaps();
        }

        /// <summary>
        /// A window changed, so the chunk it belongs to is built again.
        ///
        /// REBUILDING THE WHOLE CHUNK FOR ONE PANE looks heavy and is the right
        /// trade here. A chunk costs about two milliseconds to build, upload
        /// and cook; breaking a window is something a player does a few times a
        /// minute, not a few times a frame. The alternative -- keeping every
        /// pane as its own object so one can be removed on its own -- is the
        /// arrangement city_chunk.gd measured at four thousand collision nodes
        /// per chunk and sixty-three milliseconds, which is what made a
        /// sprinting player stutter. One rebuild on a punch is cheaper than
        /// that permanently.
        /// </summary>
        private void OnPaneChanged(PaneKey key, PaneState state)
        {
            BuildingSite owner = _world.Building(key.Building);
            var chunk = new Vector2Int(owner.OwnerChunkX, owner.OwnerChunkY);

            // ANOTHER HOLE IN AN ALREADY CRACKED PANE changes only the cracked glass and its decals, which are objects of their own: the chunk's
            // big mesh and its collider are exactly what they were. Rebuilding all of it on every bullet (2026-10-09: "frame drops when spamming
            // shots with the pistol at a window") cost a whole chunk's mesh upload and collider cook per shot.
            bool glassOnly = _paneSeen.TryGetValue(key, out PaneState before) && before == state && state == PaneState.Cracked;
            _paneSeen[key] = state;
            RefreshChunk(chunk, glassOnly);
        }

        /// <summary>A balcony door opened or shut (BalconyDoorState): the chunk its building belongs to is built again with the door drawn the new way.</summary>
        private void OnBalconyDoorChanged(int building)
        {
            if (_world == null || building < 0 || building >= _world.BuildingCount)
            {
                return;
            }
            BuildingSite owner = _world.Building(building);
            var chunk = new Vector2Int(owner.OwnerChunkX, owner.OwnerChunkY);
            if (_far.Contains(chunk))
            {
                return;   // the distant version of a building is its shell: no doors to draw (it is built in full when you come close)
            }
            RefreshChunk(chunk, false);
        }

        /// <summary>Builds a loaded chunk again (in the background where it can), after something drawn in it changed; <paramref name="glassOnly"/>: only its cracked glass.</summary>
        private void RefreshChunk(Vector2Int chunk, bool glassOnly)
        {
            if (_loaded.TryGetValue(chunk, out GameObject loadedNow) && WorkerThreads > 0)
            {
                // In the background: the chunk on screen stays until its replacement is ready, and a burst of hits on one chunk is one rebuild.
                if (_inFlight.Contains(chunk))
                {
                    // Something is already on its way for this chunk; this change goes in the NEXT one, and only that one's kind (glass or
                    // whole) is decided by it. Folding it into the one in flight (as until 2026-10-14) turned every extra hole into a whole
                    // rebuild for as long as an AK kept hitting the block: "frame drops when rapid firing the ak at windows".
                    if (!_refreshing.ContainsKey(chunk) && !IsSwapPending(chunk))
                    {
                        _refreshing[chunk] = false;   // in flight for streaming: its result is swapped in whole, then this change follows
                    }
                    _pendingRefresh[chunk] = _pendingRefresh.TryGetValue(chunk, out bool wasGlassOnly) ? wasGlassOnly && glassOnly : glassOnly;
                    _changedInFlight.Add(chunk);
                    return;
                }
                _refreshing[chunk] = glassOnly;
                _inFlight.Add(chunk);
                QueueGeneration(chunk, _far.Contains(chunk));
                return;
            }
            if (loadedNow != null && glassOnly && !_inFlight.Contains(chunk))
            {
                ChunkPayload glass = GenerateChunk(chunk, false, _far.Contains(chunk));
                ReplaceGlass(loadedNow, glass);
                Recycle(glass);
                return;
            }

            if (!_loaded.TryGetValue(chunk, out GameObject existing))
            {
                // Not on screen. The rebuild happens when it streams back in,
                // and it reads the same damage. If a worker is generating it right now it may have read the
                // pane before it changed, so that result is redone when it lands.
                if (_inFlight.Contains(chunk))
                {
                    _changedInFlight.Add(chunk);
                }
                return;
            }
            if (_inFlight.Contains(chunk))
            {
                _changedInFlight.Add(chunk);   // a worker is regenerating it: that result may predate this change
            }
            DestroyChunk(existing);
            _loaded[chunk] = BuildChunkObject(chunk, _far.Contains(chunk));
        }

        /// <summary>How long this frame's streaming took on the main thread, milliseconds (putting chunks on screen, swapping rebuilt ones in).</summary>
        public double LastUpdateMs { get; private set; }

        /// <summary>How many chunks the background streaming has put on screen so far (new ones and detail swaps). For the frame benches.</summary>
        public int ChunksStreamedIn { get; private set; }

        private int _chunksGenerated;

        /// <summary>How many chunks have been generated, on any thread (for the frame benches): more than went on screen is work thrown away.</summary>
        public int ChunksGenerated => Volatile.Read(ref _chunksGenerated);

        /// <summary>
        /// Of this frame's streaming, the milliseconds spent switching on chunks whose collider was cooked on a worker (CompleteSwaps): what
        /// is left on the main thread of cooking, which took 10-110 ms a chunk there. For the frame benches.
        /// </summary>
        public double LastSwapInMs { get; private set; }

        private readonly System.Diagnostics.Stopwatch _updateClock = new System.Diagnostics.Stopwatch();

        private void Update()
        {
            Transform viewer = Viewer != null ? Viewer : (Camera.main != null ? Camera.main.transform : null);
            if (viewer == null)
            {
                LastUpdateMs = 0.0;
                return;
            }
            _updateClock.Restart();
            LastSwapInMs = 0.0;
            if (WorkerThreads > 0)
            {
                StreamAroundInBackground(viewer.position);
            }
            else
            {
                StreamAround(viewer.position, Mathf.Max(1, ChunkBuildBudgetPerFrame), ChunkBuildTimeBudgetMs);
            }
            LastUpdateMs = _updateClock.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// The per-frame streaming: finish what the workers have generated (within AssembleTimeBudgetMs), drop
        /// what has fallen out of range, and start generating the nearest chunks still missing.
        ///
        /// The list of wanted chunks is only worked out again when the viewer has crossed into another chunk or
        /// something has changed (the old code scanned and sorted the whole 29x29 ring every single frame, even
        /// with nothing to build).
        /// </summary>
        private void StreamAroundInBackground(Vector3 position)
        {
            if (_world == null)
            {
                Rebuild();
            }
            if (_workers == null)
            {
                _workers = new SemaphoreSlim(Mathf.Max(1, WorkerThreads));
            }

            int centreX = Mathf.FloorToInt(position.x / CityLayout.ChunkSize);
            int centreY = Mathf.FloorToInt(position.z / CityLayout.ChunkSize);
            var centre = new Vector2Int(centreX, centreY);
            _explored.Mark(centre);

            AssembleFinished(centre);

            if (centre != _wantedCentre || _wantedStale)
            {
                _wantedCentre = centre;
                _wantedStale = false;

                _stale.Clear();
                int dropRadius = RadiusChunks + 2;
                foreach (KeyValuePair<Vector2Int, GameObject> pair in _loaded)
                {
                    if ((Mathf.Abs(pair.Key.x - centreX) > dropRadius || Mathf.Abs(pair.Key.y - centreY) > dropRadius)
                        && !IsLandmarkInRange(pair.Key, centreX, centreY))
                    {
                        _stale.Add(pair.Key);
                    }
                }
                foreach (Vector2Int stale in _stale)
                {
                    DestroyChunk(_loaded[stale]);
                    _loaded.Remove(stale);
                    _far.Remove(stale);
                }

                // Nearest first, so walking forward fills in ahead of you rather than in a corner you cannot see.
                // A chunk already loaded is asked for again only if it is at the wrong detail for how far away it now is.
                _requests.Clear();
                for (int dx = -RadiusChunks; dx <= RadiusChunks; dx++)
                {
                    for (int dy = -RadiusChunks; dy <= RadiusChunks; dy++)
                    {
                        var key = new Vector2Int(centreX + dx, centreY + dy);
                        int distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                        bool loaded = _loaded.ContainsKey(key);
                        bool loadedFar = _far.Contains(key);
                        bool wantFar = WantsFar(distance, !loaded || loadedFar);
                        if (!loaded || loadedFar != wantFar)
                        {
                            _requests.Add(new ChunkRequest(key, wantFar));
                        }
                    }
                }
                // The distant version of every landmark within the wider radius that is not already in the ring.
                foreach (Vector2Int landmark in _landmarks)
                {
                    int distance = Mathf.Max(Mathf.Abs(landmark.x - centreX), Mathf.Abs(landmark.y - centreY));
                    if (distance > RadiusChunks && distance <= LandmarkRadiusChunks && (!_loaded.ContainsKey(landmark)))
                    {
                        _requests.Add(new ChunkRequest(landmark, true));
                    }
                }
                _requests.Sort((a, b) =>
                {
                    int da = (a.Key.x - centreX) * (a.Key.x - centreX) + (a.Key.y - centreY) * (a.Key.y - centreY);
                    int db = (b.Key.x - centreX) * (b.Key.x - centreX) + (b.Key.y - centreY) * (b.Key.y - centreY);
                    return da.CompareTo(db);
                });
            }

            // Start generating the nearest chunks that are neither built (at the right detail) nor already being built.
            int maxInFlight = Mathf.Max(Mathf.Max(1, MaxChunksInFlight), 2 * WorkerThreads);
            bool grewInFlight = false;
            for (int i = 0; i < _requests.Count && _inFlight.Count < maxInFlight; i++)
            {
                ChunkRequest request = _requests[i];
                if (_inFlight.Contains(request.Key) || IsBuiltAt(request))
                {
                    continue;
                }
                _inFlight.Add(request.Key);
                QueueGeneration(request.Key, request.Far);
                grewInFlight = true;
            }

            if (grewInFlight)
            {
                // Some of the front of the list are now in flight or built: drop them so the next frame's scan is short.
                _requests.RemoveAll(r => _inFlight.Contains(r.Key) || IsBuiltAt(r));
            }
        }

        /// <summary>Whether a request is already satisfied: the chunk is loaded at the detail asked for.</summary>
        private bool IsBuiltAt(ChunkRequest request)
        {
            return _loaded.ContainsKey(request.Key) && _far.Contains(request.Key) == request.Far;
        }

        /// <summary>Whether a chunk arriving now is too far from the viewer (round <paramref name="centre"/>) to go on screen: it would only be dropped.</summary>
        private bool IsOutOfRange(Vector2Int key, Vector2Int centre)
        {
            return (Mathf.Abs(key.x - centre.x) > RadiusChunks + 1 || Mathf.Abs(key.y - centre.y) > RadiusChunks + 1)
                && !IsLandmarkInRange(key, centre.x, centre.y);
        }

        private void QueueGeneration(Vector2Int key, bool far)
        {
            // Read here, on the main thread: a UnityEngine.Object is not something to touch from a worker.
            bool withGreenery = GrassMaterial != null && BarkMaterial != null && !far;
            int generation = _generation;
            SemaphoreSlim workers = _workers;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                workers.Wait();
                try
                {
                    ChunkPayload payload = GenerateChunk(key, withGreenery, far);
                    payload.Generation = generation;
                    _finished.Enqueue(payload);
                }
                catch (System.Exception exception)
                {
                    // A chunk that fails to generate must not silently leave a hole in the city forever.
                    Debug.LogException(exception);
                    ChunkPayload failed = TakePayload(key, far);   // no main mesh: generated again on the main thread when it lands
                    failed.Generation = generation;
                    _finished.Enqueue(failed);
                }
                finally
                {
                    workers.Release();
                }
            });
        }

        /// <summary>
        /// Turns generated chunks into scene objects until the frame's budget is spent (always at least one, so
        /// streaming cannot stall), throwing away any that no longer matter -- the viewer moved on, the world was
        /// rebuilt, or it was built some other way in the meantime. Each is made switched off and goes on screen
        /// once its collider has been cooked on a worker (BeginSwap, CompleteSwaps).
        /// </summary>
        private void AssembleFinished(Vector2Int centre)
        {
            CompleteSwaps(centre);   // chunks whose collider has been cooked go on screen
            if (_finished.IsEmpty)
            {
                return;
            }

            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            while (_finished.TryDequeue(out ChunkPayload payload))
            {
                if (payload.Generation != _generation)
                {
                    Recycle(payload);   // from a world that has since been rebuilt
                    continue;
                }
                _inFlight.Remove(payload.Key);
                _wantedStale = true;

                bool outOfRange = IsOutOfRange(payload.Key, centre);

                // A refresh after a window was hit (OnPaneChanged): swap in the new glass, or the whole chunk, and go again if it was hit again meanwhile.
                if (_refreshing.TryGetValue(payload.Key, out bool glassOnly) && _loaded.TryGetValue(payload.Key, out GameObject onScreen)
                    && payload.Main != null && _far.Contains(payload.Key) == payload.Far)
                {
                    _refreshing.Remove(payload.Key);
                    if (glassOnly)
                    {
                        ReplaceGlass(onScreen, payload);
                        if (_changedInFlight.Remove(payload.Key))
                        {
                            RefreshAgain(payload.Key, TakePendingRefresh(payload.Key));
                        }
                        Recycle(payload);   // last: once given back, a worker may take it for another chunk
                    }
                    else
                    {
                        // The whole chunk: built here, its collider cooked on a worker, swapped in when that is done (CompleteSwaps), which
                        // then starts the next refresh if more hits came meanwhile.
                        BeginSwap(payload, onScreen);
                    }
                    if (clock.Elapsed.TotalMilliseconds >= AssembleTimeBudgetMs)
                    {
                        break;
                    }
                    continue;
                }
                _refreshing.Remove(payload.Key);

                bool alreadyThere = _loaded.ContainsKey(payload.Key) && _far.Contains(payload.Key) == payload.Far;
                if (outOfRange || alreadyThere)
                {
                    _changedInFlight.Remove(payload.Key);
                    Recycle(payload);
                    continue;
                }

                // A pane broke while this was being generated, or generation failed: do it again, here.
                // A window in it changed while this was being generated (a block being upgraded from its distant version when it was shot):
                // put this version in now and have the change follow in the background. It used to be generated again here, on the main
                // thread -- a 20-30 ms stall (seen 2026-10-14 when an AK sprayed a block still streaming in).
                bool changedMeanwhile = payload.Main != null && _changedInFlight.Remove(payload.Key);
                _pendingRefresh.Remove(payload.Key);
                if (payload.Main == null)
                {
                    ChunkPayload failed = payload;   // the worker failed
                    payload = GenerateChunk(failed.Key, GrassMaterial != null && BarkMaterial != null && !failed.Far, failed.Far);
                    Recycle(failed);
                }

                // Made here but switched off, its collider cooked on a worker, and put on screen when that is done (CompleteSwaps), in place
                // of the same chunk at another detail if that is on screen now -- which stays meanwhile, drawn and solid. Cooking it here took
                // ~10 ms for an ordinary chunk and 80-110 ms for a block of towers, in the frame it landed: the hitches walking or flying.
                _loaded.TryGetValue(payload.Key, out GameObject replaced);
                BeginSwap(payload, replaced, streamed: true, changedMeanwhile);

                if (clock.Elapsed.TotalMilliseconds >= AssembleTimeBudgetMs)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Brings the loaded set up to date around a point, building at most
        /// <paramref name="budget"/> chunks.
        ///
        /// Public and budget-parameterised so an offline capture can pass
        /// int.MaxValue and get a complete city in one call. A screenshot tool
        /// that only ever saw a partially streamed world would be checking
        /// something the player never sees.
        ///
        /// <paramref name="timeBudgetMs"/> additionally ends the building once
        /// that many milliseconds have gone (after at least one chunk); the
        /// default is no limit, which is what the synchronous pre-stream and
        /// the captures want.
        /// </summary>
        public void StreamAround(Vector3 position, int budget, float timeBudgetMs = float.PositiveInfinity)
        {
            if (_world == null)
            {
                Rebuild();
            }

            Vector3 p = position;
            int centreX = Mathf.FloorToInt(p.x / CityLayout.ChunkSize);
            int centreY = Mathf.FloorToInt(p.z / CityLayout.ChunkSize);
            _explored.Mark(new Vector2Int(centreX, centreY));

            // Drop first: freeing before allocating keeps the peak lower when
            // the viewer is moving fast.
            _stale.Clear();
            int dropRadius = RadiusChunks + 2;
            foreach (KeyValuePair<Vector2Int, GameObject> pair in _loaded)
            {
                if ((Mathf.Abs(pair.Key.x - centreX) > dropRadius ||
                    Mathf.Abs(pair.Key.y - centreY) > dropRadius) && !IsLandmarkInRange(pair.Key, centreX, centreY))
                {
                    _stale.Add(pair.Key);
                }
            }
            foreach (Vector2Int key in _stale)
            {
                DestroyChunk(_loaded[key]);
                _loaded.Remove(key);
                _far.Remove(key);
            }

            // Build nearest-first, so walking forward fills in ahead of you
            // rather than filling a corner of the ring you cannot see.
            _wanted.Clear();
            for (int dx = -RadiusChunks; dx <= RadiusChunks; dx++)
            {
                for (int dy = -RadiusChunks; dy <= RadiusChunks; dy++)
                {
                    Vector2Int key = new Vector2Int(centreX + dx, centreY + dy);
                    if (!_loaded.ContainsKey(key))
                    {
                        _wanted.Add(key);
                    }
                }
            }
            foreach (Vector2Int landmark in _landmarks)
            {
                int landmarkDistance = Mathf.Max(Mathf.Abs(landmark.x - centreX), Mathf.Abs(landmark.y - centreY));
                if (landmarkDistance > RadiusChunks && landmarkDistance <= LandmarkRadiusChunks && !_loaded.ContainsKey(landmark))
                {
                    _wanted.Add(landmark);
                }
            }
            _wanted.Sort((a, b) =>
            {
                int da = (a.x - centreX) * (a.x - centreX) + (a.y - centreY) * (a.y - centreY);
                int db = (b.x - centreX) * (b.x - centreX) + (b.y - centreY) * (b.y - centreY);
                return da.CompareTo(db);
            });

            System.Diagnostics.Stopwatch clock = float.IsPositiveInfinity(timeBudgetMs)
                ? null
                : System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < _wanted.Count && i < budget; i++)
            {
                Vector2Int wanted = _wanted[i];
                int distance = Mathf.Max(Mathf.Abs(wanted.x - centreX), Mathf.Abs(wanted.y - centreY));
                _loaded[wanted] = BuildChunkObject(wanted, WantsFar(distance, true));
                if (clock != null && clock.Elapsed.TotalMilliseconds >= timeBudgetMs)
                {
                    break;
                }
            }
        }

        /// <summary>Number of chunks currently built. For tests and capture.</summary>
        public int LoadedChunkCount => _loaded.Count;

        /// <summary>The whole chunk, generated and made, on this thread (the spawn pre-stream, a window breaking, tests, captures).</summary>
        private GameObject BuildChunkObject(Vector2Int key, bool far = false)
        {
            ChunkPayload payload = GenerateChunk(key, GrassMaterial != null && BarkMaterial != null && !far, far);
            GameObject go = AssembleChunk(payload);
            Recycle(payload);
            if (far)
            {
                _far.Add(key);
            }
            else
            {
                _far.Remove(key);
            }
            return go;
        }

        /// <summary>
        /// Generates one chunk's geometry: every mesh buffer, and the main mesh combined into flat arrays.
        /// Touches no scene object and no UnityEngine.Object, so it can run on any thread.
        /// </summary>
        private ChunkPayload GenerateChunk(Vector2Int key, bool withGreenery, bool far = false)
        {
            ChunkPayload payload = TakePayload(key, far);
            ChunkScratch scratch = ChunkScratch.Take();
            try
            {
                // Chunks are built relative to their own corner and then placed, so
                // vertex coordinates stay small. Building in absolute world space
                // would lose float precision a long way from the origin -- which is
                // exactly where an infinite world spends most of its time.
                Vector3 origin = new Vector3(
                    key.x * CityLayout.ChunkSize, 0.0f, key.y * CityLayout.ChunkSize);

                // The far version has no cracked glass, no decals and no markings; its guard-rail buffer is still handed
                // in (elevated decks are only built when there is one) and simply never turned into an object.
                CityMeshBuilder.BuildChunk(_world, key.x, key.y, origin,
                    scratch.Ground, scratch.Pavement, scratch.Structures, scratch.LitGlass, scratch.DarkGlass,
                    far ? null : payload.CrackedGlass, Damage,
                    far ? null : payload.CrackedOpenings, far ? null : payload.Markings, payload.GuardRail,
                    far ? null : payload.LaneDividers, far ? null : payload.CrackDecals,
                    includeSurface: true, far: far, roomGlows: far ? null : payload.RoomGlows);

                // Road, pavement, concrete and glass go into one mesh as submeshes rather than separate objects: same vertex buffer, same
                // transform, one cull. The ground, the pavement and the walls are one material now (the facade shader reads what each surface is
                // off its vertices), so they are ONE submesh: a draw call each used to be spent on up to three of them in every chunk. Laid out
                // here, the way the Mesh keeps it, tangents and bounds included: the main thread only copies it in (AssembleChunk).
                payload.Main = ChunkMeshData.Combine(scratch.Groups);
                Interlocked.Increment(ref _chunksGenerated);
                return payload;
            }
            catch
            {
                Recycle(payload);
                throw;
            }
            finally
            {
                scratch.GiveBack();   // combined into the payload's own arrays: the next chunk can have it
            }
        }

        /// <summary>
        /// Turns a generated chunk into scene objects. Main thread only. With <paramref name="deferCollider"/> (everything the background
        /// streaming makes) the chunk is made INACTIVE and its big collider is left without its mesh: the caller has it cooked on a worker
        /// (BeginSwap) and switches the chunk on when that is done. Without it (the synchronous path) the collider is cooked here, at once.
        /// </summary>
        private GameObject AssembleChunk(ChunkPayload payload, bool deferCollider = false)
        {
            _current = payload;
            Vector2Int key = payload.Key;
            Vector3 origin = new Vector3(
                key.x * CityLayout.ChunkSize, 0.0f, key.y * CityLayout.ChunkSize);

            GameObject go = new GameObject($"Chunk {key.x},{key.y}");
            if (deferCollider)
            {
                go.SetActive(false);   // nothing in it shows, collides or wakes up until its collider is cooked
            }
            go.transform.SetParent(transform, false);
            go.transform.localPosition = origin;

            // Laid out by the worker (ChunkMeshData): here it is the layout, one block copy per vertex stream and one for the indices, with
            // nothing checked or recalculated, 2-3 ms for a block of towers. Built here with the classic Mesh API (SetVertices, SetNormals,
            // SetUVs, SetColors, SetTriangles, SetTangents, RecalculateBounds), which repacks the whole vertex buffer for each attribute it
            // adds, the same block cost 20-46 ms of one frame: the worst hitch left streaming in (2026-10-14).
            Mesh mesh = new Mesh { name = $"chunk-{key.x}-{key.y}" };
            ChunkMeshData main = payload.Main;
            main.ApplyTo(mesh);
            // Only the submeshes that have something in them, each with its material. A street chunk has no walls
            // or glass, and a draw call per empty submesh is exactly the per-draw cost that frame time followed.
            Material[] materialFor = { StructureMaterial, DarkGlassMaterial, LitGlassMaterial };   // GenerateChunk's groups, in order
            var usedMaterials = new Material[main.SubMeshes.Length];
            usedMaterials[0] = GroundMaterial;   // a chunk with nothing at all still has one (empty) submesh
            for (int slot = 0; slot < main.SubMeshGroups.Length; slot++)
            {
                usedMaterials[slot] = materialFor[main.SubMeshGroups[slot]];
            }

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = usedMaterials;

            // The glass is in the collider along with everything else, which is
            // what makes an unbroken window something you walk into rather than
            // through. Cooking it is most of what a chunk costs: ~10 ms, 80-110 ms for a block of towers. Only the
            // synchronous path (the spawn pre-stream, captures, tests: solid at once) still does it here; see BeginSwap.
            MeshCollider collider = go.AddComponent<MeshCollider>();
            if (!deferCollider)
            {
                collider.sharedMesh = mesh;
            }

            if (payload.Far)
            {
                // Just the outside of the chunk: the shell is the collider (a closed solid, so nobody flies
                // through a building whose full version has not streamed in), and none of the small things
                // that only matter up close.
                _current = null;
                return go;
            }

            AddCrackedGlass(go, key);
            AddCrackDecals(go, key);
            AddMarkings(go, key);
            AddGuardRail(go, key);
            AddDoor(go, key);
            AddEntranceLamps(go, key);
            AddInteriorVolume(go, key);
            AddRoomLights(go, payload);
            _current = null;
            return go;
        }

        /// <summary>
        /// The short guardrail CityMeshBuilder built along a street-level
        /// terrace drop, if this chunk has one.
        ///
        /// A SEPARATE OBJECT, same shape as AddMarkings -- but UNLIKE
        /// markings (paint nobody stands on, see that method's own comment),
        /// this one gets a real MeshCollider: the whole point of a guardrail
        /// is that it actually stops a pedestrian walking off the ledge.
        /// </summary>
        private void AddGuardRail(GameObject chunk, Vector2Int key)
        {
            if (_guardRail.IsEmpty || GuardRailMaterial == null)
            {
                return;
            }

            var go = new GameObject("GuardRail");
            go.transform.SetParent(chunk.transform, false);

            var mesh = new Mesh { name = $"guardrail-{key.x}-{key.y}" };
            mesh.SetVertices(_guardRail.Vertices);
            mesh.SetNormals(_guardRail.Normals);
            mesh.SetUVs(0, _guardRail.Uvs);
            mesh.SetTriangles(_guardRail.Triangles, 0);
            mesh.RecalculateBounds();

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GuardRailMaterial;
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }


        /// <summary>
        /// The "you are inside this building" trigger, which is what makes the
        /// street go quiet as you step through the door. One per building the
        /// chunk builds, turned the way the building is.
        /// </summary>
        private void AddInteriorVolume(GameObject chunk, Vector2Int key)
        {
            List<int> owned = _world.BuildingsOwnedBy(key.x, key.y);
            for (int i = 0; i < owned.Count; i++)
            {
                BuildingSite site = _world.Building(owned[i]);
                if (site.Shell || site.IsOpenGround)
                {
                    continue;   // sealed: there is no inside to be in; an open car park has no inside at all
                }
                var go = new GameObject("Interior");
                go.transform.SetParent(chunk.transform, true);
                var volume = go.AddComponent<BuildingInteriorVolume>();
                volume.Fit(BuildingInteriorVolume.BoundsFor(site), Quaternion.Euler(0.0f, CityMeshBuilder.YawDegreesOf(site), 0.0f));
            }
        }

        private Material _upperDoorMaterial;

        /// <summary>The dark grey door of a volume standing on a roof (a tower on a podium), so it reads differently from the brown street-level entrances. Made once from the street door's material.</summary>
        private Material UpperDoorMaterial()
        {
            if (_upperDoorMaterial == null)
            {
                _upperDoorMaterial = new Material(DoorMaterial) { name = "BuildingDoorUpper" };
                var grey = new Color(0.17f, 0.18f, 0.20f);
                if (_upperDoorMaterial.HasProperty("_BaseColor")) _upperDoorMaterial.SetColor("_BaseColor", grey);
                if (_upperDoorMaterial.HasProperty("_Color")) _upperDoorMaterial.SetColor("_Color", grey);
            }
            return _upperDoorMaterial;
        }

        /// <summary>
        /// The front door of every building the chunk builds.
        ///
        /// WHETHER IT IS OPEN IS REMEMBERED HERE, NOT ON THE DOOR, and that is
        /// a departure from Godot forced by this port's own streaming. A Godot
        /// door lives as long as its chunk and simply resets when that chunk
        /// unloads. Here a chunk is also rebuilt whenever a window in it
        /// breaks, so a door on the door itself would slam shut the moment you
        /// punched a pane on the same building -- which looks like a bug rather
        /// than like streaming.
        ///
        /// Still local-only and still forgotten when the chunk really unloads,
        /// exactly as Godot's is.
        /// </summary>
        private void AddDoor(GameObject chunk, Vector2Int key)
        {
            if (DoorMaterial == null)
            {
                return;
            }

            List<int> owned = _world.BuildingsOwnedBy(key.x, key.y);
            for (int i = 0; i < owned.Count; i++)
            {
                BuildingSite site = _world.Building(owned[i]);
                if (site.Shell || site.Params.IsSpecial)
                {
                    continue;   // sealed: no door; a garage, a car park, a mall's corridors and anchors have open entrances of their own
                }
                int buildingId = site.Id;

                // One door, or a pair (BuildingInterior.HasDoubleEntrance): two leaves that open and shut as one.
                BuildingDoor.Panel[] panels = BuildingDoor.PanelsFor(site);
                site.LocalToWorld(0.0, site.FrontHalfDepth + 2.0, out double streetX, out double streetZ);
                var street = new Vector3((float)streetX, site.BaseHeight, (float)streetZ);   // in front of the door: a restored-open door stands swung in
                BuildingDoor first = null;
                for (int p = 0; p < panels.Length; p++)
                {
                    var go = new GameObject(panels.Length > 1 ? "DoorLeaf" : "Door");
                    go.transform.SetParent(chunk.transform, true);

                    BuildingDoor door = go.AddComponent<BuildingDoor>();
                    door.Toggled += open =>
                    {
                        if (open)
                        {
                            _openDoors.Add(buildingId);
                        }
                        else
                        {
                            _openDoors.Remove(buildingId);
                        }
                    };
                    door.Hang(panels[p], site.OnRoof ? UpperDoorMaterial() : DoorMaterial, DoorHandleMaterial, _openDoors.Contains(buildingId), street);
                    if (first == null)
                    {
                        first = door;
                    }
                    else
                    {
                        first.Partner = door;
                        door.Partner = first;
                    }
                }
            }
        }

        /// <summary>A powered building's door has a lamp over it, lit from dusk to dawn (World.EntranceLighting, EntranceLamp).</summary>
        private void AddEntranceLamps(GameObject chunk, Vector2Int key)
        {
            List<int> owned = _world.BuildingsOwnedBy(key.x, key.y);
            for (int i = 0; i < owned.Count; i++)
            {
                EntranceLamp.Build(_world.Building(owned[i]), chunk.transform, RoomGlowMaterial, RoomGlowBrightness);
            }
        }

        /// <summary>
        /// Lane paint, on its own object.
        ///
        /// NOT IN THE CHUNK MESH, for the same reason the signs are not: it is
        /// unlit flat colour where everything else in that mesh is lit
        /// concrete. It also has no business in the collider -- paint is not
        /// something you can stand on top of, it is part of the road that
        /// already holds you up.
        ///
        /// A chunk's MAIN markings buffer gets yellow OR white, never both: a
        /// junction has crossings and no centre line, a straight run has a
        /// centre line and no crossings. One buffer carries either.
        ///
        /// laneDividers is a separate, always-white third channel (see
        /// CityMeshBuilder.AddRoadMarkings's own doc comment) -- an
        /// arterial's dashed lane dividers coexist with that same chunk's own
        /// yellow centreline, which the single yellow-or-white buffer above
        /// has no way to express. Built as its own GameObject/mesh exactly
        /// like _markings is, just keyed off CrosswalkMaterial unconditionally
        /// rather than the junction test.
        /// </summary>
        private void AddMarkings(GameObject chunk, Vector2Int key)
        {
            BuildMarkingsMesh(chunk, key, _markings, "markings", RoadLineMaterial);
            BuildMarkingsMesh(chunk, key, _laneDividers, "lane-dividers", CrosswalkMaterial);
        }


        private static void BuildMarkingsMesh(GameObject chunk, Vector2Int key, MeshBuffer buffer,
            string name, Material material)
        {
            if (buffer.IsEmpty || material == null)
            {
                return;
            }

            var go = new GameObject(name);
            go.transform.SetParent(chunk.transform, false);

            var mesh = new Mesh { name = $"{name}-{key.x}-{key.y}" };
            mesh.SetVertices(buffer.Vertices);
            mesh.SetNormals(buffer.Normals);
            mesh.SetUVs(0, buffer.Uvs);
            mesh.SetTriangles(buffer.Triangles, 0);
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// Cracked panes, on their own object with no collider.
        ///
        /// THIS IS THE ONLY REASON IT IS A SEPARATE OBJECT. The first hit on a
        /// window has to make it walkable immediately and still leave something
        /// frosted to look at -- city_chunk.gd's own rule, and the user's own
        /// "you can now just walk through it". The chunk's collider is built
        /// from its render mesh, so drawn-but-not-solid is impossible within
        /// it. A second mesh with no MeshCollider is the whole trick.
        ///
        /// Almost always empty: it costs one object per chunk with a broken
        /// window and nothing at all for the rest.
        /// </summary>
        private void AddCrackedGlass(GameObject chunk, Vector2Int key)
        {
            if (_crackedGlass.IsEmpty)
            {
                return;
            }

            var go = new GameObject("CrackedGlass");
            go.transform.SetParent(chunk.transform, false);

            var mesh = new Mesh { name = $"cracked-{key.x}-{key.y}" };
            mesh.indexFormat = _crackedGlass.Vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_crackedGlass.Vertices);
            mesh.SetNormals(_crackedGlass.Normals);
            mesh.SetUVs(0, _crackedGlass.Uvs);
            mesh.SetTriangles(_crackedGlass.Triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = CrackedGlassMaterial;

            // One trigger per cracked opening, so climbing through what is left
            // of a window finishes it off. The builder hands these out because
            // it is the only thing that knows exactly where a pane went.
            foreach (CityMeshBuilder.CrackedOpening opening in _crackedOpenings)
            {
                var watcher = new GameObject("Opening");
                watcher.transform.SetParent(go.transform, false);
                watcher.transform.localPosition = opening.Centre;
                watcher.transform.localRotation = opening.Rotation;

                BoxCollider box = watcher.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = opening.Size;

                watcher.AddComponent<CrackedPane>().Watch(Damage, opening.Key, opening.Rotation);
            }
        }

        /// <summary>
        /// The real crack-texture decal (CityMeshBuilder.AddCrackDecal) at a
        /// known shot's own impact point, on its own object exactly like
        /// AddCrackedGlass -- same reason: it sits proud of the base glass
        /// and has no business in the chunk's own collider.
        ///
        /// Almost always empty, same as AddCrackedGlass -- only a pane with
        /// a KNOWN impact point (a real gunshot/punch, not a loaded/set
        /// state with no recorded position) ever puts anything in this
        /// buffer at all.
        /// </summary>
        private void AddCrackDecals(GameObject chunk, Vector2Int key)
        {
            if (_crackDecals.IsEmpty || CrackDecalMaterial == null)
            {
                return;
            }

            var go = new GameObject("CrackDecals");
            go.transform.SetParent(chunk.transform, false);

            var mesh = new Mesh { name = $"crack-decals-{key.x}-{key.y}" };
            mesh.SetVertices(_crackDecals.Vertices);
            mesh.SetNormals(_crackDecals.Normals);
            mesh.SetUVs(0, _crackDecals.Uvs);
            mesh.SetTriangles(_crackDecals.Triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = CrackDecalMaterial;
        }

        /// <summary>How bright a lit fitting's face is, above white (it should bloom a little, like a lamp).</summary>
        public const float RoomGlowBrightness = 3.0f;

        /// <summary>
        /// How much light a lit room's window veil adds (Shaders/CityXWindowLight.shader), over the real lit room that shows through the glass: a
        /// lift, so the window still reads from down the street, not the window itself (an opaque panel at 0.85 read as a painted rectangle).
        /// </summary>
        public const float RoomWindowBrightness = 0.15f;

        /// <summary>
        /// A powered room's lights (ChunkRoomLights): the glow under its fittings and on its windows and its few real lights, off until its switch
        /// is on. Only the one building in ten with power has any (World.PowerGrid); the rest have fittings and switches drawn into the chunk and
        /// nothing to light. NOTHING IS MADE HERE FOR A ROOM: its glow is drawn and its lights are lent (RoomLightDirector) when its switch is on.
        /// Making every powered room's meshes and spot lights up front cost 0.5-0.8 ms a room in the frame its chunk landed, 14-24 ms for a
        /// block of 20-30 rooms (2026-10-14), nearly all of it for rooms nobody ever lights; now such a block costs 0.1-0.2 ms.
        /// </summary>
        private void AddRoomLights(GameObject chunk, ChunkPayload payload)
        {
            if (payload.RoomGlows.Count > 0)
            {
                ChunkRoomLights.Attach(chunk, payload.RoomGlows, RoomGlowMaterial, WindowLightMaterialOrDefault());
            }
        }

        private Material WindowLightMaterialOrDefault()
        {
            if (WindowLightMaterial == null)
            {
                Shader shader = Shader.Find("CityX/WindowLight");
                if (shader != null)
                {
                    WindowLightMaterial = new Material(shader) { name = "WindowLight (runtime)" };
                }
            }
            return WindowLightMaterial;
        }

        /// <summary>Another hit landed while a chunk's refresh was being built: build it once more, in the background, the same kind as before.</summary>
        private void RefreshAgain(Vector2Int chunk, bool glassOnly)
        {
            _refreshing[chunk] = glassOnly;
            _inFlight.Add(chunk);
            QueueGeneration(chunk, _far.Contains(chunk));
        }

        /// <summary>The kind of the changes that came while a refresh was on its way (true: only cracked glass), forgotten once read.</summary>
        private bool TakePendingRefresh(Vector2Int chunk)
        {
            bool glassOnly = _pendingRefresh.TryGetValue(chunk, out bool pending) && pending;
            _pendingRefresh.Remove(chunk);
            return glassOnly;
        }

        // ---- a chunk's collider, cooked off the main thread (2026-10-14) ------------------------------------------------------------------
        // Cooking a chunk's collider is most of what putting it on screen costs: ~10 ms for an ordinary chunk, 80-110 ms for a block of towers
        // in the game's own districts (CityStreamerFramePlayTests). It was done on the main thread in the frame the chunk landed, so every
        // chunk streaming in was a hitch, and every window hit (which rebuilds its chunk) a stall. Physics.BakeMesh is thread-safe and works on
        // the thread that calls it; a MeshCollider then given the mesh with the same cooking options takes the cooked data as it is. So a chunk
        // is made switched off, its collider cooked on a worker, and it is switched on when that is done (a frame or two; a few for a block of
        // towers). Until then the version on screen, if there is one, stays, drawn and solid: nothing falls through, nothing blinks.
        //
        // ON THE STREAMER'S OWN THREADS, NOT IN UNITY'S JOB SYSTEM (where window rebuilds cooked first): a job is never interrupted, so a 100 ms
        // cook holds one of the engine's own workers all that time, and a main thread waiting on some other job may pick a waiting cook up and
        // run it itself -- the stall moved, not gone.

        /// <summary>One chunk's collider being cooked: shared by the main thread, which made the mesh, and the worker cooking it.</summary>
        private sealed class ColliderCook
        {
            private const int Waiting = 0, Cooking = 1, Done = 2, Abandoned = 3;
            private int _state;

            public int MeshId;
            public MeshColliderCookingOptions Options;

            public bool IsDone => Volatile.Read(ref _state) == Done;

            /// <summary>On a worker: cooks it, unless it was abandoned first.</summary>
            public void Run()
            {
                if (Interlocked.CompareExchange(ref _state, Cooking, Waiting) != Waiting)
                {
                    return;
                }
                try
                {
                    Physics.BakeMesh(MeshId, false, Options);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);   // the collider then cooks it itself when it is given the mesh, on the main thread
                }
                finally
                {
                    Volatile.Write(ref _state, Done);
                }
            }

            /// <summary>Main thread: once this returns no worker is cooking it, or ever will, so its mesh may be destroyed.</summary>
            public void Abandon()
            {
                if (Interlocked.CompareExchange(ref _state, Abandoned, Waiting) == Waiting)
                {
                    return;
                }
                var spin = new SpinWait();
                while (Volatile.Read(ref _state) == Cooking)
                {
                    spin.SpinOnce();
                }
            }
        }

        private readonly ConcurrentQueue<ColliderCook> _cooks = new ConcurrentQueue<ColliderCook>();
        private int _cookingThreads;   // workers taking cooks off _cooks (Interlocked)

        /// <summary>
        /// How many colliders are cooked at once: half the generator threads, at least one. The cooking threads are the thread pool's, taken
        /// only while there is something to cook, and never left waiting.
        /// </summary>
        private int CookingThreads => Mathf.Max(1, WorkerThreads / 2);

        /// <summary>Has a collider cooked on a worker, starting another cooking thread if fewer than <see cref="CookingThreads"/> are at it.</summary>
        private void Cook(ColliderCook cook)
        {
            _cooks.Enqueue(cook);
            int limit = CookingThreads;
            if (TakeCookingThread(limit))
            {
                ThreadPool.QueueUserWorkItem(_ => CookWhatIsWaiting(limit));
            }
        }

        private bool TakeCookingThread(int limit)
        {
            while (true)
            {
                int running = Volatile.Read(ref _cookingThreads);
                if (running >= limit)
                {
                    return false;
                }
                if (Interlocked.CompareExchange(ref _cookingThreads, running + 1, running) == running)
                {
                    return true;
                }
            }
        }

        /// <summary>On a worker: cooks until nothing is waiting, then goes -- unless a cook came in just as it was going, which it then takes on.</summary>
        private void CookWhatIsWaiting(int limit)
        {
            do
            {
                while (_cooks.TryDequeue(out ColliderCook cook))
                {
                    cook.Run();
                }
                Interlocked.Decrement(ref _cookingThreads);
            }
            while (!_cooks.IsEmpty && TakeCookingThread(limit));
        }

        /// <summary>A chunk made and waiting, switched off, for its collider to be cooked before it goes on screen.</summary>
        private sealed class PendingSwap
        {
            public Vector2Int Key;
            public GameObject Fresh;

            /// <summary>The version on screen when this one was made (null: none), which it replaces.</summary>
            public GameObject Replaces;

            public MeshCollider Collider;
            public Mesh Mesh;
            public ColliderCook Cook;

            /// <summary>Streaming in (a new chunk, or the other detail); false: rebuilt after a window in it was hit.</summary>
            public bool Streamed;

            public bool Far;

            /// <summary>A window in it changed while it was being generated: a rebuild follows it on screen.</summary>
            public bool ChangedMeanwhile;
        }

        private readonly List<PendingSwap> _pendingSwaps = new List<PendingSwap>();
        private readonly Dictionary<Vector2Int, bool> _pendingRefresh = new Dictionary<Vector2Int, bool>();

        /// <summary>How many chunks (streaming in, or rebuilt after a window was hit) are made and waiting for their collider. For tests.</summary>
        public int PendingSwapCount => _pendingSwaps.Count;

        /// <summary>True while a window's change is still on its way to the screen: a refresh generating, a swap cooking, or more hits waiting.</summary>
        public bool WindowRefreshPending => _refreshing.Count > 0 || _changedInFlight.Count > 0 || SwapCarriesAWindowChange();

        /// <summary>True while any chunk is still being generated or waiting to be put on screen (for tests that need the city settled).</summary>
        public bool IsStreaming => _inFlight.Count > 0 || !_finished.IsEmpty;

        private bool IsSwapPending(Vector2Int chunk)
        {
            foreach (PendingSwap swap in _pendingSwaps)
            {
                if (swap.Key == chunk)
                {
                    return true;
                }
            }
            return false;
        }

        private bool SwapCarriesAWindowChange()
        {
            foreach (PendingSwap swap in _pendingSwaps)
            {
                if (!swap.Streamed || swap.ChangedMeanwhile)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Makes a chunk switched off and has its collider cooked on a worker; CompleteSwaps puts it on screen in place of
        /// <paramref name="replaces"/> (the version on screen now, or null) once that is done. <paramref name="streamed"/>: streaming in, not a
        /// rebuild after a window was hit. <paramref name="changedMeanwhile"/>: a window in it changed while it was being generated.
        /// </summary>
        private void BeginSwap(ChunkPayload payload, GameObject replaces, bool streamed = false, bool changedMeanwhile = false)
        {
            GameObject fresh = AssembleChunk(payload, deferCollider: true);
            var collider = fresh.GetComponent<MeshCollider>();
            Mesh mesh = fresh.GetComponent<MeshFilter>().sharedMesh;
            var cook = new ColliderCook { MeshId = mesh.GetInstanceID(), Options = collider.cookingOptions };
            _pendingSwaps.Add(new PendingSwap
            {
                Key = payload.Key, Fresh = fresh, Replaces = replaces, Collider = collider, Mesh = mesh, Cook = cook,
                Streamed = streamed, Far = payload.Far, ChangedMeanwhile = changedMeanwhile,
            });
            Cook(cook);
            _inFlight.Add(payload.Key);   // still on its way: nothing else is started for it, and more hits on it wait for this one
            Recycle(payload);   // made (the Mesh has its own copy): the payload goes to the next chunk
        }

        /// <summary>
        /// Puts on screen, oldest first, every chunk whose collider has been cooked, in place of the version it was made to replace. One is
        /// thrown away instead if what it was to replace is no longer what is there (dropped, or built some other way), or, streaming in, if the
        /// viewer (round <paramref name="centre"/>) has left it behind meanwhile.
        /// </summary>
        private void CompleteSwaps(Vector2Int centre)
        {
            _swapInClock.Restart();
            for (int i = 0; i < _pendingSwaps.Count;)
            {
                PendingSwap swap = _pendingSwaps[i];
                if (!swap.Cook.IsDone)
                {
                    i++;
                    continue;
                }
                _pendingSwaps.RemoveAt(i);
                _inFlight.Remove(swap.Key);
                _wantedStale = true;   // what is on screen changes, or a chunk thrown away is wanted again

                // Windows that changed while it was on its way decide what follows it (read, and forgotten, either way).
                bool changed = _changedInFlight.Remove(swap.Key);
                changed |= swap.ChangedMeanwhile;
                bool glassOnly = TakePendingRefresh(swap.Key) && !swap.ChangedMeanwhile;

                // By reference: a version dropped since is destroyed, and a destroyed object equals null to Unity.
                bool onScreen = _loaded.TryGetValue(swap.Key, out GameObject current);
                if (!ReferenceEquals(current, swap.Replaces) || (swap.Streamed && IsOutOfRange(swap.Key, centre)))
                {
                    DestroyChunk(swap.Fresh);
                    if (changed && onScreen)
                    {
                        RefreshAgain(swap.Key, false);   // the version still there predates the change
                    }
                    continue;
                }
                swap.Collider.sharedMesh = swap.Mesh;   // cooked already: no stall
                swap.Fresh.SetActive(true);
                DestroyChunk(swap.Replaces);
                _loaded[swap.Key] = swap.Fresh;
                if (swap.Streamed)
                {
                    ChunksStreamedIn++;
                    if (swap.Far)
                    {
                        _far.Add(swap.Key);
                    }
                    else
                    {
                        _far.Remove(swap.Key);
                    }
                }
                if (changed)
                {
                    RefreshAgain(swap.Key, glassOnly);
                }
            }
            LastSwapInMs = _swapInClock.Elapsed.TotalMilliseconds;
        }

        private readonly System.Diagnostics.Stopwatch _swapInClock = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// Throws away every chunk still waiting for its collider (the world is being rebuilt, or the streamer switched off), each once no
        /// worker is cooking it: a mesh must never be destroyed under a cook. The version on screen, if any, stays.
        /// </summary>
        private void AbandonSwaps()
        {
            if (_pendingSwaps.Count == 0)
            {
                return;
            }
            foreach (PendingSwap swap in _pendingSwaps)
            {
                swap.Cook.Abandon();
                _inFlight.Remove(swap.Key);
                _changedInFlight.Remove(swap.Key);
                _pendingRefresh.Remove(swap.Key);
                DestroyChunk(swap.Fresh);
            }
            _pendingSwaps.Clear();
            _wantedStale = true;   // wanted again
        }

        /// <summary>
        /// Swaps a chunk's cracked glass and crack decals (and the openings' triggers) for a freshly generated chunk's, leaving its big mesh and
        /// collider alone: what another bullet hole in an already cracked pane changes.
        /// </summary>
        private void ReplaceGlass(GameObject chunk, ChunkPayload payload)
        {
            foreach (string part in new[] { "CrackedGlass", "CrackDecals" })
            {
                Transform old = chunk.transform.Find(part);
                if (old == null)
                {
                    continue;
                }
                MeshFilter filter = old.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    if (Application.isPlaying) Destroy(filter.sharedMesh); else DestroyImmediate(filter.sharedMesh);
                }
                old.SetParent(null);
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            _current = payload;
            AddCrackedGlass(chunk, payload.Key);
            AddCrackDecals(chunk, payload.Key);
            _current = null;
        }

        private static void DestroyChunk(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            // Every mesh here is created here, so all of them are this object's
            // to free. Unity does not collect meshes with the GameObject, and a
            // streamer that leaks one mesh per chunk will eat a machine alive
            // over a few minutes of walking.
            //
            // CHILDREN INCLUDED, and that word is the fix for a real leak: the
            // cracked-glass mesh hangs off a child object, and freeing only the
            // root's mesh left one behind on every chunk with a broken window
            // in it -- which is exactly the leak this comment was already
            // warning about, reintroduced one level down.
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    Destroy(filter.sharedMesh);
                }
                else
                {
                    DestroyImmediate(filter.sharedMesh);
                }
            }

            if (Application.isPlaying)
            {
                // Switched off BEFORE the deferred destroy. A chunk rebuilt
                // mid-frame -- which is what breaking a window does -- would
                // otherwise leave the old copy's colliders in the world
                // alongside the new one until the end of the frame, and the
                // player would be standing inside two walls.
                go.SetActive(false);
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<Vector2Int, GameObject> pair in _loaded)
            {
                DestroyChunk(pair.Value);
            }
            _loaded.Clear();
            while (_finished.TryDequeue(out ChunkPayload left))
            {
                Recycle(left);   // its arrays back to ChunkArrayPool, for whatever streams next
            }

            // Stops publishing once this streamer is gone -- otherwise a
            // scene-load test that unloads CityMassing.unity leaves
            // CurrentWorld pointing at a CityWorld with no live streamer
            // behind it any more, which the NEXT, entirely unrelated test
            // to run in the same batch would silently inherit. Guarded on
            // reference equality rather than a blind null-out: a second
            // streamer that has since taken over (a rebuild mid-session)
            // must not have its own, still-current publish undone by the
            // first one's teardown.
            if (CurrentWorld.Instance == _world)
            {
                CurrentWorld.Instance = null;
            }
        }
    }
}

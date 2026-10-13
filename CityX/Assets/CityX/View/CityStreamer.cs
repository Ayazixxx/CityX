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
    /// finished chunks into scene objects, within a small time budget per frame. See GeneratorThreads. The
    /// synchronous path (StreamAround, BuildChunkObject) is unchanged in behaviour for the spawn pre-stream, for
    /// rebuilding a chunk when a window breaks, and for tests and captures.
    /// </summary>
    public sealed class CityStreamer : MonoBehaviour
    {
        [Header("World")]
        // Matches CityMassingSceneBuilder.WorldSeed -- a fallback default
        // for a CityStreamer added without one explicitly assigned; the
        // real scenes always set this from that same constant at build
        // time. EXPLICIT ASK: "generate a new world every time you make a
        // change to the city generation."
        public int WorldSeed = 38461927;

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
                 "objects. 0 goes back to building every chunk on the main thread (the old, hitchy way).")]
        public int GeneratorThreads = 3;

        [Tooltip("Most milliseconds per frame the main thread spends turning generated chunks into scene objects " +
                 "(at least one chunk a frame is always finished).")]
        public float AssembleTimeBudgetMs = 2.0f;

        [Tooltip("Most chunks being generated at once; more just queues work that may be stale by the time it lands.")]
        public int MaxChunksInFlight = 6;

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
        /// the main mesh already combined into flat arrays. Made on a worker thread and read on the main thread,
        /// so it owns its buffers (nothing here is shared between chunks) and touches no Unity object.
        /// </summary>
        private sealed class ChunkPayload
        {
            public Vector2Int Key;
            public int Generation;
            /// <summary>The distant version of the chunk (see CityMeshBuilder.BuildChunk's far parameter).</summary>
            public bool Far;
            public readonly MeshBuffer Ground = new MeshBuffer();
            public readonly MeshBuffer Pavement = new MeshBuffer();
            public readonly MeshBuffer Structures = new MeshBuffer();
            public readonly MeshBuffer LitGlass = new MeshBuffer();
            public readonly MeshBuffer DarkGlass = new MeshBuffer();
            public readonly MeshBuffer CrackedGlass = new MeshBuffer();
            public readonly MeshBuffer CrackDecals = new MeshBuffer();
            public readonly List<CityMeshBuilder.CrackedOpening> CrackedOpenings = new List<CityMeshBuilder.CrackedOpening>();
            public readonly MeshBuffer Markings = new MeshBuffer();
            public readonly MeshBuffer LaneDividers = new MeshBuffer();
            public readonly MeshBuffer GuardRail = new MeshBuffer();
            public readonly MeshBuffer Greenery = new MeshBuffer();
            public readonly MeshBuffer Bark = new MeshBuffer();

            /// <summary>The powered rooms' lights (CityMeshBuilder.RoomGlow), built near only.</summary>
            public readonly List<CityMeshBuilder.RoomGlow> RoomGlows = new List<CityMeshBuilder.RoomGlow>();

            // The main mesh's five submeshes, combined off the main thread.
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public Color32[] Colors;
            public int[][] SubmeshTriangles;
        }

        // The chunk being turned into scene objects right now (main thread only). The Add* methods below read
        // their buffers through these, so they did not have to change when the buffers moved into a payload.
        private ChunkPayload _current;
        private MeshBuffer _ground => _current.Ground;
        private MeshBuffer _pavement => _current.Pavement;
        private MeshBuffer _structures => _current.Structures;
        private MeshBuffer _litGlass => _current.LitGlass;
        private MeshBuffer _darkGlass => _current.DarkGlass;
        private MeshBuffer _crackedGlass => _current.CrackedGlass;
        private MeshBuffer _crackDecals => _current.CrackDecals;
        private List<CityMeshBuilder.CrackedOpening> _crackedOpenings => _current.CrackedOpenings;
        private MeshBuffer _markings => _current.Markings;
        private MeshBuffer _laneDividers => _current.LaneDividers;
        private MeshBuffer _guardRail => _current.GuardRail;
        private MeshBuffer _greenery => _current.Greenery;
        private MeshBuffer _bark => _current.Bark;

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
            _inFlight.Clear();
            _changedInFlight.Clear();
            _refreshing.Clear();
            _paneSeen.Clear();
            while (_finished.TryDequeue(out _))
            {
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

        private void OnEnable()
        {
            Damage.Changed += OnPaneChanged;
        }

        private void OnDisable()
        {
            Damage.Changed -= OnPaneChanged;
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

            if (_loaded.TryGetValue(chunk, out GameObject loadedNow) && GeneratorThreads > 0)
            {
                // In the background: the chunk on screen stays until its replacement is ready, and a burst of hits on one chunk is one rebuild.
                _refreshing[chunk] = _refreshing.TryGetValue(chunk, out bool wasGlassOnly) ? wasGlassOnly && glassOnly : glassOnly;
                if (_inFlight.Contains(chunk))
                {
                    _changedInFlight.Add(chunk);
                    return;
                }
                _inFlight.Add(chunk);
                QueueGeneration(chunk, _far.Contains(chunk));
                return;
            }
            if (loadedNow != null && glassOnly && !_inFlight.Contains(chunk))
            {
                ReplaceGlass(loadedNow, GenerateChunk(chunk, false, _far.Contains(chunk)));
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

        private void Update()
        {
            Transform viewer = Viewer != null ? Viewer : (Camera.main != null ? Camera.main.transform : null);
            if (viewer == null)
            {
                return;
            }
            if (GeneratorThreads > 0)
            {
                StreamAroundInBackground(viewer.position);
            }
            else
            {
                StreamAround(viewer.position, Mathf.Max(1, ChunkBuildBudgetPerFrame), ChunkBuildTimeBudgetMs);
            }
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
                _workers = new SemaphoreSlim(Mathf.Max(1, GeneratorThreads));
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
            int maxInFlight = Mathf.Max(1, MaxChunksInFlight);
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
                    _finished.Enqueue(new ChunkPayload { Key = key, Generation = generation, Far = far, Vertices = null });
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
        /// rebuilt, or it was built some other way in the meantime.
        /// </summary>
        private void AssembleFinished(Vector2Int centre)
        {
            if (_finished.IsEmpty)
            {
                return;
            }

            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            while (_finished.TryDequeue(out ChunkPayload payload))
            {
                if (payload.Generation != _generation)
                {
                    continue;   // from a world that has since been rebuilt
                }
                _inFlight.Remove(payload.Key);
                _wantedStale = true;

                bool outOfRange = (Mathf.Abs(payload.Key.x - centre.x) > RadiusChunks + 1 || Mathf.Abs(payload.Key.y - centre.y) > RadiusChunks + 1)
                    && !IsLandmarkInRange(payload.Key, centre.x, centre.y);

                // A refresh after a window was hit (OnPaneChanged): swap in the new glass, or the whole chunk, and go again if it was hit again meanwhile.
                if (_refreshing.TryGetValue(payload.Key, out bool glassOnly) && _loaded.TryGetValue(payload.Key, out GameObject onScreen)
                    && payload.Vertices != null && _far.Contains(payload.Key) == payload.Far)
                {
                    _refreshing.Remove(payload.Key);
                    if (glassOnly)
                    {
                        ReplaceGlass(onScreen, payload);
                    }
                    else
                    {
                        DestroyChunk(onScreen);
                        _loaded[payload.Key] = AssembleChunk(payload);
                    }
                    if (_changedInFlight.Remove(payload.Key))
                    {
                        RefreshAgain(payload.Key, glassOnly);
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
                    continue;
                }

                // A pane broke while this was being generated, or generation failed: do it again, here.
                bool redo = payload.Vertices == null || _changedInFlight.Remove(payload.Key);
                if (redo)
                {
                    payload = GenerateChunk(payload.Key, GrassMaterial != null && BarkMaterial != null && !payload.Far, payload.Far);
                }

                // The same chunk at another detail replaces the one on screen.
                if (_loaded.TryGetValue(payload.Key, out GameObject replaced))
                {
                    DestroyChunk(replaced);
                }
                _loaded[payload.Key] = AssembleChunk(payload);
                if (payload.Far)
                {
                    _far.Add(payload.Key);
                }
                else
                {
                    _far.Remove(payload.Key);
                }

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
            GameObject go = AssembleChunk(GenerateChunk(key, GrassMaterial != null && BarkMaterial != null && !far, far));
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
            var payload = new ChunkPayload { Key = key, Generation = _generation, Far = far };

            // Chunks are built relative to their own corner and then placed, so
            // vertex coordinates stay small. Building in absolute world space
            // would lose float precision a long way from the origin -- which is
            // exactly where an infinite world spends most of its time.
            Vector3 origin = new Vector3(
                key.x * CityLayout.ChunkSize, 0.0f, key.y * CityLayout.ChunkSize);

            // The far version has no cracked glass, no decals and no markings; its guard-rail buffer is still handed
            // in (elevated decks are only built when there is one) and simply never turned into an object.
            CityMeshBuilder.BuildChunk(_world, key.x, key.y, origin,
                payload.Ground, payload.Pavement, payload.Structures, payload.LitGlass, payload.DarkGlass,
                far ? null : payload.CrackedGlass, Damage,
                far ? null : payload.CrackedOpenings, far ? null : payload.Markings, payload.GuardRail,
                far ? null : payload.LaneDividers, far ? null : payload.CrackDecals,
                includeSurface: true, far: far, roomGlows: far ? null : payload.RoomGlows);

            // Road, pavement and concrete want different materials, so they go
            // into one mesh as submeshes rather than separate objects. Same
            // vertex buffer, same transform, one cull -- and each submesh's
            // triangle indices have to be shifted by however many vertices came
            // before it, which is the only fiddly part.
            MeshBuffer[] buffers = { payload.Ground, payload.Pavement, payload.Structures, payload.DarkGlass, payload.LitGlass };
            int vertexCount = 0;
            foreach (MeshBuffer buffer in buffers)
            {
                vertexCount += buffer.Vertices.Count;
            }
            payload.Vertices = new Vector3[vertexCount];
            payload.Normals = new Vector3[vertexCount];
            payload.Uvs = new Vector2[vertexCount];
            payload.Colors = new Color32[vertexCount];
            payload.SubmeshTriangles = new int[buffers.Length][];
            int written = 0;
            for (int b = 0; b < buffers.Length; b++)
            {
                MeshBuffer buffer = buffers[b];
                buffer.Vertices.CopyTo(payload.Vertices, written);
                buffer.Normals.CopyTo(payload.Normals, written);
                buffer.Uvs.CopyTo(payload.Uvs, written);
                buffer.Colors.CopyTo(payload.Colors, written);

                var shifted = new int[buffer.Triangles.Count];
                for (int i = 0; i < shifted.Length; i++)
                {
                    shifted[i] = buffer.Triangles[i] + written;
                }
                payload.SubmeshTriangles[b] = shifted;
                written += buffer.Vertices.Count;
            }

            // The ground, the pavement and the walls are one material now (the facade shader reads what each surface is off its vertices),
            // so they are ONE submesh: a draw call each used to be spent on up to three of them in every chunk.
            var everything = new int[payload.SubmeshTriangles[0].Length + payload.SubmeshTriangles[1].Length + payload.SubmeshTriangles[2].Length];
            payload.SubmeshTriangles[0].CopyTo(everything, 0);
            payload.SubmeshTriangles[1].CopyTo(everything, payload.SubmeshTriangles[0].Length);
            payload.SubmeshTriangles[2].CopyTo(everything, payload.SubmeshTriangles[0].Length + payload.SubmeshTriangles[1].Length);
            payload.SubmeshTriangles[0] = everything;
            payload.SubmeshTriangles[1] = new int[0];
            payload.SubmeshTriangles[2] = new int[0];
            return payload;
        }

        /// <summary>Turns a generated chunk into scene objects. Main thread only.</summary>
        private GameObject AssembleChunk(ChunkPayload payload)
        {
            _current = payload;
            Vector2Int key = payload.Key;
            Vector3 origin = new Vector3(
                key.x * CityLayout.ChunkSize, 0.0f, key.y * CityLayout.ChunkSize);

            GameObject go = new GameObject($"Chunk {key.x},{key.y}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = origin;

            Mesh mesh = new Mesh { name = $"chunk-{key.x}-{key.y}" };
            mesh.indexFormat = payload.Vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(payload.Vertices);
            mesh.SetNormals(payload.Normals);
            mesh.SetUVs(0, payload.Uvs);
            mesh.SetColors(payload.Colors);
            // Only the submeshes that have something in them, each with its material. A street chunk has no walls
            // or glass, and a draw call per empty submesh is exactly the per-draw cost that frame time followed.
            Material[] materialFor = { StructureMaterial, PavementMaterial, StructureMaterial, DarkGlassMaterial, LitGlassMaterial };
            int used = 0;
            for (int i = 0; i < payload.SubmeshTriangles.Length; i++)
            {
                if (payload.SubmeshTriangles[i].Length > 0)
                {
                    used++;
                }
            }
            var usedMaterials = new Material[Mathf.Max(1, used)];
            usedMaterials[0] = GroundMaterial;   // a chunk with nothing at all still has one (empty) submesh
            mesh.subMeshCount = usedMaterials.Length;
            int slot = 0;
            for (int i = 0; i < payload.SubmeshTriangles.Length; i++)
            {
                if (payload.SubmeshTriangles[i].Length == 0)
                {
                    continue;
                }
                mesh.SetTriangles(payload.SubmeshTriangles[i], slot);
                usedMaterials[slot] = materialFor[i];
                slot++;
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = usedMaterials;

            // The glass is in the collider along with everything else, which is
            // what makes an unbroken window something you walk into rather than
            // through.
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

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

                var go = new GameObject("Door");
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
                door.Hang(BuildingDoor.PanelFor(site), site.OnRoof ? UpperDoorMaterial() : DoorMaterial, DoorHandleMaterial,
                    _openDoors.Contains(buildingId));
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

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// A powered room's lights (RoomLightGroup): the glow under its fittings and its few real lights, off until its switch is on. Only the one
        /// building in ten with power has any (World.PowerGrid); the rest have fittings and switches drawn into the chunk and nothing to light.
        /// </summary>
        private void AddRoomLights(GameObject chunk, ChunkPayload payload)
        {
            foreach (CityMeshBuilder.RoomGlow glow in payload.RoomGlows)
            {
                var go = new GameObject($"RoomLight {glow.Key.Building}/{glow.Key.Floor}/{glow.Key.Room}");
                go.transform.SetParent(chunk.transform, false);

                Renderer renderer = null;
                if (!glow.Glow.IsEmpty)
                {
                    var mesh = new Mesh { name = "room-glow" };
                    mesh.SetVertices(glow.Glow.Vertices);
                    mesh.SetNormals(glow.Glow.Normals);
                    mesh.SetTriangles(glow.Glow.Triangles, 0);
                    mesh.RecalculateBounds();
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = RoomGlowMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var block = new MaterialPropertyBlock();
                    block.SetColor(BaseColorId, glow.Colour * RoomGlowBrightness);
                    renderer.SetPropertyBlock(block);
                }

                // The room's windows from outside: a soft veil of its light just behind the glass, over the real lit room that shows through it.
                Renderer windows = null;
                Material windowLight = WindowLightMaterialOrDefault();
                if (!glow.Windows.IsEmpty && windowLight != null)
                {
                    var windowObject = new GameObject("Windows");
                    windowObject.transform.SetParent(go.transform, false);
                    var mesh = new Mesh { name = "room-windows" };
                    mesh.SetVertices(glow.Windows.Vertices);
                    mesh.SetNormals(glow.Windows.Normals);
                    mesh.SetUVs(0, glow.Windows.Uvs);
                    mesh.SetTriangles(glow.Windows.Triangles, 0);
                    mesh.RecalculateBounds();
                    windowObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    windows = windowObject.AddComponent<MeshRenderer>();
                    windows.sharedMaterial = windowLight;
                    windows.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    windows.receiveShadows = false;
                    var block = new MaterialPropertyBlock();
                    block.SetColor(BaseColorId, glow.Colour * RoomWindowBrightness);
                    windows.SetPropertyBlock(block);
                }

                // The lamps, spots shining down from just under the fittings, and under each the light the floor throws back up at the ceiling
                // (World.RoomLighting). All marked as inside (CityLightLayers): they light the room and whoever is in it, never the street.
                var lamps = new Light[glow.Lights.Length];
                for (int i = 0; i < lamps.Length; i++)
                {
                    lamps[i] = AddRoomSpot(go.transform, "Lamp", glow.Lights[i], Vector3.down, glow.Colour, glow.Range, glow.Intensity,
                        RoomLighting.LampOuterAngle, RoomLighting.LampInnerAngle);
                }
                Vector3[] bouncePoints = glow.Bounces ?? System.Array.Empty<Vector3>();
                var bounces = new Light[bouncePoints.Length];
                // The bounce is the room's light after a pale floor has had it: the same colour, a little greyed.
                Color bounced = Color.Lerp(glow.Colour, new Color(0.85f, 0.85f, 0.85f), 0.3f);
                for (int i = 0; i < bounces.Length; i++)
                {
                    bounces[i] = AddRoomSpot(go.transform, "Bounce", bouncePoints[i], Vector3.up, bounced, glow.BounceRange, glow.BounceIntensity,
                        RoomLighting.BounceOuterAngle, RoomLighting.BounceInnerAngle);
                }

                go.AddComponent<RoomLightGroup>().Bind(glow.Key, new[] { renderer, windows }, lamps, bounces);
            }
        }

        /// <summary>One of a room's spot lights. No shadow unless RoomLightDirector gives it one (it is near the camera).</summary>
        private static Light AddRoomSpot(Transform parent, string name, Vector3 localPosition, Vector3 shining, Color colour, float range,
            float intensity, float outerAngle, float innerAngle)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition;
            lightObject.transform.rotation = Quaternion.LookRotation(shining, Mathf.Abs(shining.y) > 0.9f ? Vector3.forward : Vector3.up);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.spotAngle = outerAngle;
            light.innerSpotAngle = innerAngle;
            light.shadows = LightShadows.None;
            light.shadowStrength = 0.85f;
            CityLightLayers.MarkInside(light);
            return light;
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

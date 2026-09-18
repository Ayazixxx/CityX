using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Builds and drops chunk meshes around a viewer.
    ///
    /// Deliberately simple: this exists so the world can be looked at and
    /// walked through, which is the only way to judge whether the massing is
    /// right. It is not the shipping streamer -- no threading, no LOD, no
    /// pooling. Those are worth building once the thing being streamed is
    /// settled, and not before.
    /// </summary>
    public sealed class CityStreamer : MonoBehaviour
    {
        [Header("World")]
        // Matches CityMassingSceneBuilder.WorldSeed -- a fallback default
        // for a CityStreamer added without one explicitly assigned; the
        // real scenes always set this from that same constant at build
        // time. EXPLICIT ASK: "generate a new world every time you make a
        // change to the city generation."
        public int WorldSeed = 29481073;

        [Tooltip("Chunks of radius kept loaded around the viewer.")]
        public int RadiusChunks = 14;

        [Tooltip("Most chunks built in one frame. Low keeps the frame smooth; high fills in faster.")]
        public int ChunkBuildBudgetPerFrame = 6;

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
        private SkybridgeLayout _bridges;
        private readonly Dictionary<Vector2Int, GameObject> _loaded = new Dictionary<Vector2Int, GameObject>();
        private readonly MeshBuffer _ground = new MeshBuffer();
        private readonly MeshBuffer _pavement = new MeshBuffer();
        private readonly MeshBuffer _structures = new MeshBuffer();
        private readonly MeshBuffer _litGlass = new MeshBuffer();
        private readonly MeshBuffer _darkGlass = new MeshBuffer();
        private readonly MeshBuffer _crackedGlass = new MeshBuffer();
        private readonly MeshBuffer _crackDecals = new MeshBuffer();
        private readonly List<CityMeshBuilder.CrackedOpening> _crackedOpenings =
            new List<CityMeshBuilder.CrackedOpening>();
        private readonly MeshBuffer _markings = new MeshBuffer();
        private readonly MeshBuffer _laneDividers = new MeshBuffer();
        private readonly MeshBuffer _guardRail = new MeshBuffer();
        private readonly HashSet<Vector2Int> _openDoors = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _wanted = new List<Vector2Int>();
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
            _world = new CityWorld(WorldSeed);
            _bridges = new SkybridgeLayout(WorldSeed);
            foreach (KeyValuePair<Vector2Int, GameObject> pair in _loaded)
            {
                DestroyChunk(pair.Value);
            }
            _loaded.Clear();
            // A different seed makes the old explored coordinates meaningless.
            _explored.Clear();

            // See CurrentWorld's own doc comment: this is the one place the
            // real, currently-running world actually gets built, so this is
            // the one place that can publish it for CityX.Character (which
            // has no reference to this assembly) to read.
            CurrentWorld.Instance = _world;
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
            var chunk = new Vector2Int(key.ChunkX, key.ChunkY);
            if (!_loaded.TryGetValue(chunk, out GameObject existing))
            {
                // Not on screen. The rebuild happens when it streams back in,
                // and it reads the same damage.
                return;
            }
            DestroyChunk(existing);
            _loaded[chunk] = BuildChunkObject(chunk);
        }

        private void Update()
        {
            Transform viewer = Viewer != null ? Viewer : (Camera.main != null ? Camera.main.transform : null);
            if (viewer == null)
            {
                return;
            }
            StreamAround(viewer.position, Mathf.Max(1, ChunkBuildBudgetPerFrame));
        }

        /// <summary>
        /// Brings the loaded set up to date around a point, building at most
        /// <paramref name="budget"/> chunks.
        ///
        /// Public and budget-parameterised so an offline capture can pass
        /// int.MaxValue and get a complete city in one call. A screenshot tool
        /// that only ever saw a partially streamed world would be checking
        /// something the player never sees.
        /// </summary>
        public void StreamAround(Vector3 position, int budget)
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
                if (Mathf.Abs(pair.Key.x - centreX) > dropRadius ||
                    Mathf.Abs(pair.Key.y - centreY) > dropRadius)
                {
                    _stale.Add(pair.Key);
                }
            }
            foreach (Vector2Int key in _stale)
            {
                DestroyChunk(_loaded[key]);
                _loaded.Remove(key);
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
            _wanted.Sort((a, b) =>
            {
                int da = (a.x - centreX) * (a.x - centreX) + (a.y - centreY) * (a.y - centreY);
                int db = (b.x - centreX) * (b.x - centreX) + (b.y - centreY) * (b.y - centreY);
                return da.CompareTo(db);
            });

            for (int i = 0; i < _wanted.Count && i < budget; i++)
            {
                _loaded[_wanted[i]] = BuildChunkObject(_wanted[i]);
            }
        }

        /// <summary>Number of chunks currently built. For tests and capture.</summary>
        public int LoadedChunkCount => _loaded.Count;

        private GameObject BuildChunkObject(Vector2Int key)
        {
            _ground.Clear();
            _pavement.Clear();
            _structures.Clear();
            _litGlass.Clear();
            _darkGlass.Clear();
            _crackedGlass.Clear();
            _crackDecals.Clear();
            _crackedOpenings.Clear();
            _markings.Clear();
            _laneDividers.Clear();
            _guardRail.Clear();

            // Chunks are built relative to their own corner and then placed, so
            // vertex coordinates stay small. Building in absolute world space
            // would lose float precision a long way from the origin -- which is
            // exactly where an infinite world spends most of its time.
            Vector3 origin = new Vector3(
                key.x * CityLayout.ChunkSize, 0.0f, key.y * CityLayout.ChunkSize);

            CityMeshBuilder.BuildChunk(_world, _bridges, key.x, key.y, origin,
                _ground, _pavement, _structures, _litGlass, _darkGlass, _crackedGlass, Damage,
                _crackedOpenings, _markings, _guardRail, _laneDividers, _crackDecals);

            GameObject go = new GameObject($"Chunk {key.x},{key.y}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = origin;

            Mesh mesh = new Mesh { name = $"chunk-{key.x}-{key.y}" };

            // Road, pavement and concrete want different materials, so they go
            // into one mesh as three submeshes rather than three objects. Same
            // vertex buffer, same transform, one cull -- and each submesh's
            // triangle indices have to be shifted by however many vertices came
            // before it, which is the only fiddly part.
            var buffers = new[] { _ground, _pavement, _structures, _darkGlass, _litGlass };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var submeshTriangles = new List<int[]>(buffers.Length);

            foreach (MeshBuffer buffer in buffers)
            {
                int offset = vertices.Count;
                vertices.AddRange(buffer.Vertices);
                normals.AddRange(buffer.Normals);
                uvs.AddRange(buffer.Uvs);

                var shifted = new int[buffer.Triangles.Count];
                for (int i = 0; i < buffer.Triangles.Count; i++)
                {
                    shifted[i] = buffer.Triangles[i] + offset;
                }
                submeshTriangles.Add(shifted);
            }

            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = buffers.Length;
            for (int i = 0; i < buffers.Length; i++)
            {
                mesh.SetTriangles(submeshTriangles[i], i);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                GroundMaterial, PavementMaterial, StructureMaterial,
                DarkGlassMaterial, LitGlassMaterial,
            };

            // The glass is in the collider along with everything else, which is
            // what makes an unbroken window something you walk into rather than
            // through.
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            AddCrackedGlass(go, key);
            AddCrackDecals(go, key);
            AddMarkings(go, key);
            AddGuardRail(go, key);
            AddDoor(go, key);
            AddInteriorVolume(go, key);
            AddBenches(go, key);
            AddBooks(go, key);
            return go;
        }

        /// <summary>
        /// One readable book on a pedestrian-zone chunk's own sidewalk --
        /// EXPLICIT ASK: "add books and when you press E on them it opens a
        /// page on screen with text ive prepared but just make the system
        /// for now." Same placement idiom AddBenches already establishes
        /// (real terrain height via ContinuousHeightAt, parented onto the
        /// chunk with world position kept), just CityLayout.IsBookChunk's
        /// own independent, rarer roll instead of every pedestrian zone,
        /// and only ever one rather than one per side.
        /// </summary>
        private void AddBooks(GameObject chunk, Vector2Int key)
        {
            if (BookMaterial == null || !CityLayout.IsBookChunk(key.x, key.y))
            {
                return;
            }

            bool runsAlongX = CityLayout.StreetRunsAlongX(key.x, key.y);
            float size = CityLayout.ChunkSize;
            float half = size * 0.5f;
            float sidewalkCentre = half - CityMeshBuilder.SidewalkWidth * 0.5f;
            double worldAlong = (runsAlongX ? key.x : key.y) * (double)size + half;
            double worldAcross = (runsAlongX ? key.y : key.x) * (double)size + half + sidewalkCentre;
            double wx = runsAlongX ? worldAlong : worldAcross;
            double wz = runsAlongX ? worldAcross : worldAlong;
            float y = (float)_world.Terrain.ContinuousHeightAt(wx, wz);

            Vector3 facing = runsAlongX ? Vector3.back : Vector3.left;

            var go = new GameObject("Book");
            go.transform.position = new Vector3((float)wx, y, (float)wz);
            go.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            go.transform.SetParent(chunk.transform, true);

            Book book = go.AddComponent<Book>();
            book.Build(BookMaterial);
        }

        /// <summary>
        /// Two park benches, one on each sidewalk, in a pedestrian zone
        /// chunk -- EXPLICIT ASK: "add benches you can go up to and press E
        /// to sit as well," layered onto the pedestrian-areas work. A real
        /// GameObject with a live MonoBehaviour (BenchSeat), not baked into
        /// the chunk mesh like the plaza/skatepark geometry CityMeshBuilder
        /// already adds -- sitting needs a component to poll E, the same
        /// reason AddDoor/AddInteriorVolume are also real spawned objects
        /// rather than static mesh.
        ///
        /// QUERIES CityWorld/CityLayout DIRECTLY rather than routing bench
        /// placement through CityMeshBuilder's own MeshBuffers -- exactly
        /// how AddDoor already finds its own BuildingSite independently of
        /// whatever CityMeshBuilder baked for that chunk. Keeps this prop
        /// spawn decoupled from the baked-geometry pass entirely.
        ///
        /// PLACED ON THE SIDEWALK, FACING THE STREET -- the same real-world
        /// arrangement a plaza bench actually has, sampled at the chunk's
        /// own along-axis midpoint from the real terrain height
        /// (ContinuousHeightAt), matching how every other real prop this
        /// project spawns sits at genuine ground height rather than a
        /// quantised pad.
        /// </summary>
        private void AddBenches(GameObject chunk, Vector2Int key)
        {
            if (BenchMaterial == null || !CityLayout.IsPedestrianZoneChunk(key.x, key.y))
            {
                return;
            }

            bool runsAlongX = CityLayout.StreetRunsAlongX(key.x, key.y);
            float size = CityLayout.ChunkSize;
            float half = size * 0.5f;
            float sidewalkCentre = half - CityMeshBuilder.SidewalkWidth * 0.5f;
            double worldAlong = (runsAlongX ? key.x : key.y) * (double)size + half;

            for (int side = -1; side <= 1; side += 2)
            {
                double worldAcross = (runsAlongX ? key.y : key.x) * (double)size + half + side * sidewalkCentre;
                double wx = runsAlongX ? worldAlong : worldAcross;
                double wz = runsAlongX ? worldAcross : worldAlong;
                float y = (float)_world.Terrain.ContinuousHeightAt(wx, wz);

                // Facing INWARD, toward the street's own centreline -- a
                // sidewalk bench's backrest is against the building side,
                // its seat opens onto the street/plaza.
                Vector3 facing = runsAlongX
                    ? new Vector3(0.0f, 0.0f, -side)
                    : new Vector3(-side, 0.0f, 0.0f);

                var go = new GameObject("Bench");
                go.transform.position = new Vector3((float)wx, y, (float)wz);
                go.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
                go.transform.SetParent(chunk.transform, true);

                BenchSeat bench = go.AddComponent<BenchSeat>();
                bench.Build(BenchMaterial);
            }
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
        /// street go quiet as you step through the door.
        ///
        /// ON THE ANCHOR ONLY, like the door and the geometry: a building that
        /// spans four chunks is one building and wants one volume, not four
        /// overlapping ones counting the same body in four times over.
        /// </summary>
        private void AddInteriorVolume(GameObject chunk, Vector2Int key)
        {
            BuildingSite site = _world.BuildingAt(key.x, key.y);
            if (!site.Exists || site.AnchorX != key.x || site.AnchorY != key.y)
            {
                return;
            }

            var go = new GameObject("Interior");
            go.transform.SetParent(chunk.transform, true);
            var volume = go.AddComponent<BuildingInteriorVolume>();
            volume.Fit(BuildingInteriorVolume.BoundsFor(site));
        }

        /// <summary>
        /// The building's front door, if it has one.
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
            BuildingSite site = _world.BuildingAt(key.x, key.y);
            if (!site.Exists || site.AnchorX != key.x || site.AnchorY != key.y ||
                DoorMaterial == null)
            {
                return;
            }

            var go = new GameObject("Door");
            go.transform.SetParent(chunk.transform, true);

            BuildingDoor door = go.AddComponent<BuildingDoor>();
            door.Toggled += open =>
            {
                if (open)
                {
                    _openDoors.Add(key);
                }
                else
                {
                    _openDoors.Remove(key);
                }
            };
            door.Hang(BuildingDoor.PanelFor(site), DoorMaterial, DoorHandleMaterial,
                _openDoors.Contains(key));
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
            BuildMarkingsMesh(chunk, key, _markings, "markings", MarkingsMaterialFor(key));
            BuildMarkingsMesh(chunk, key, _laneDividers, "lane-dividers", CrosswalkMaterial);
        }

        private Material MarkingsMaterialFor(Vector2Int key)
        {
            bool junction = CityLayout.IsStreetChunk(key.x - 1, key.y) &&
                            CityLayout.IsStreetChunk(key.x + 1, key.y) &&
                            CityLayout.IsStreetChunk(key.x, key.y - 1) &&
                            CityLayout.IsStreetChunk(key.x, key.y + 1);
            return junction ? CrosswalkMaterial : RoadLineMaterial;
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

                BoxCollider box = watcher.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = opening.Size;

                watcher.AddComponent<CrackedPane>().Watch(Damage, opening.Key);
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

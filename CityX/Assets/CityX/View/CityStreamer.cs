using System.Collections.Generic;
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
        public int WorldSeed = 20260810;

        [Tooltip("Chunks of radius kept loaded around the viewer.")]
        public int RadiusChunks = 14;

        [Tooltip("Most chunks built in one frame. Low keeps the frame smooth; high fills in faster.")]
        public int ChunkBuildBudgetPerFrame = 6;

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

        [Header("Signage (city_chunk.gd's own preset colours)")]
        public Material SupermarketSignMaterial;

        public Material BankSignMaterial;

        public Material PoliceSignMaterial;

        public Material HospitalSignMaterial;

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
        private readonly List<CityMeshBuilder.CrackedOpening> _crackedOpenings =
            new List<CityMeshBuilder.CrackedOpening>();
        private readonly MeshBuffer _sign = new MeshBuffer();
        private readonly List<Vector2Int> _wanted = new List<Vector2Int>();
        private readonly List<Vector2Int> _stale = new List<Vector2Int>();

        private void Awake()
        {
            Rebuild();
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
        }

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
            _crackedOpenings.Clear();
            _sign.Clear();

            // Chunks are built relative to their own corner and then placed, so
            // vertex coordinates stay small. Building in absolute world space
            // would lose float precision a long way from the origin -- which is
            // exactly where an infinite world spends most of its time.
            Vector3 origin = new Vector3(
                key.x * CityLayout.ChunkSize, 0.0f, key.y * CityLayout.ChunkSize);

            CityMeshBuilder.BuildChunk(_world, _bridges, key.x, key.y, origin,
                _ground, _pavement, _structures, _litGlass, _darkGlass, _crackedGlass, Damage,
                _crackedOpenings, _sign);

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
            AddSign(go, key);
            return go;
        }

        /// <summary>
        /// A special building's sign band, on its own object because it is the
        /// one thing in the city that is a flat unlit colour.
        ///
        /// A SUBMESH PER TYPE WOULD BE FOUR MORE ON EVERY CHUNK IN THE WORLD to
        /// say something about the one lot in twelve that is special at all,
        /// and most chunks have no building. One object with one material, only
        /// where there is a sign, costs nothing everywhere else.
        ///
        /// The colour is looked up here rather than carried through the mesh
        /// builder: the generator's own comment already puts colours on this
        /// side of the line, and the streamer can ask the world what type a
        /// building is as cheaply as anyone.
        /// </summary>
        private void AddSign(GameObject chunk, Vector2Int key)
        {
            if (_sign.IsEmpty)
            {
                return;
            }

            BuildingSite site = _world.BuildingAt(key.x, key.y);
            Material material = SignMaterialFor(site.Params.SpecialType);
            if (material == null)
            {
                return;
            }

            var go = new GameObject("Sign");
            go.transform.SetParent(chunk.transform, false);

            var mesh = new Mesh { name = $"sign-{key.x}-{key.y}" };
            mesh.SetVertices(_sign.Vertices);
            mesh.SetNormals(_sign.Normals);
            mesh.SetUVs(0, _sign.Uvs);
            mesh.SetTriangles(_sign.Triangles, 0);
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private Material SignMaterialFor(string specialType)
        {
            switch (specialType)
            {
                case "supermarket": return SupermarketSignMaterial;
                case "bank": return BankSignMaterial;
                case "police": return PoliceSignMaterial;
                case "hospital": return HospitalSignMaterial;
                default: return null;
            }
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
        }
    }
}

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// THE SKYLINE BEYOND THE STREAMED CITY (2026-10-15, the owner, of another procedural city: "the render distance on his looks waaay better than
    /// ours i want mine like that"). CityStreamer builds the city out to its radius (30 chunks, 720 m) and the air hid the end of it at 690 m: a
    /// city that ended a few blocks away. This streams a third, far cheaper tier past it: every building of nine metres or more as a plain box
    /// in its own wall colour (a round tower as its prism) and the ground as a plain floor following the land, one mesh per GROUP of 16 x 16
    /// chunks (384 m square), out to <see cref="RadiusMetres"/>, with no collider and nothing small on it. At a kilometre, in the haze, a box in
    /// the building's colour is what a building looks like; the fog's end moves out to the same distance so the far city melts into the sky
    /// instead of stopping at a wall.
    ///
    /// Where the real chunks are, the skyline keeps out of the way: a box stands a little inside its building's walls (CityMeshBuilder.SkylineInset)
    /// and the floor two metres under the ground, so a FAR chunk (an opaque shell with its glass floating in front) hides them; a FULL-DETAIL chunk
    /// has real windows and rooms, which a box inside would fill, so the chunks within <see cref="ExcludeRadiusChunks"/> of the viewer get no
    /// boxes and no floor at all, and a group near the viewer is built again once the viewer has moved <see cref="RebuildStepChunks"/> chunks
    /// from where it was built for. Generated on a worker (the world and its plan are pure), assembled on the main thread one group a frame.
    /// </summary>
    public sealed class SkylineStreamer : MonoBehaviour
    {
        /// <summary>A group of chunks one skyline mesh covers, chunks a side (16 x 24 m = 384 m).</summary>
        public const int GroupChunks = 16;

        /// <summary>How far out the skyline reaches, metres: the fog's end (CityMassingSceneBuilder.ClearFogEndMetres), past which nothing shows anyway.</summary>
        public const float RadiusMetres = 2200.0f;

        /// <summary>A group stays loaded this far past the radius before it is dropped (no churn flying along the edge).</summary>
        public const float DropMargin = 300.0f;

        /// <summary>A group built for a viewer chunk this far from the viewer's current chunk is built again (only the groups it matters for: see NearViewer).</summary>
        public const int RebuildStepChunks = 4;

        /// <summary>
        /// No boxes and no floor within this many chunks (Chebyshev) of the chunk the viewer was in when the group was built: the streamer's
        /// full-detail radius, its hysteresis, and the step the viewer may take before the group is rebuilt.
        /// </summary>
        public const int ExcludeRadiusChunks = 8 + 2 + RebuildStepChunks;

        [Tooltip("Whose position the skyline is kept around (the main camera, as CityStreamer's).")]
        public Transform Viewer;

        public int WorldSeed = CitySeed.Default;

        public Material GroundMaterial;
        public Material StructureMaterial;

        [Tooltip("Build every group on the spot the first time (the game's start: the horizon is there from the first frame).")]
        public bool BuildAllAtStart = true;

        [Tooltip("Groups assembled per frame, at most.")]
        public int AssemblePerFrame = 1;

        [Tooltip("Groups generated at once on the worker threads, at most.")]
        public int InFlightLimit = 3;

        private sealed class Group
        {
            public GameObject Object;
            public Vector2Int BuiltAround;   // the viewer's chunk the exclusion was computed for
        }

        private readonly Dictionary<Vector2Int, Group> _loaded = new Dictionary<Vector2Int, Group>();
        private readonly HashSet<Vector2Int> _inFlight = new HashSet<Vector2Int>();
        private readonly ConcurrentQueue<(Vector2Int key, Vector2Int around, ChunkMeshData mesh, int generation)> _finished =
            new ConcurrentQueue<(Vector2Int, Vector2Int, ChunkMeshData, int)>();
        private readonly List<Vector2Int> _wanted = new List<Vector2Int>();
        private readonly List<Vector2Int> _stale = new List<Vector2Int>();
        private CityWorld _world;
        private int _generation;
        private bool _startedAll;

        /// <summary>Groups on screen now. For tests.</summary>
        public int LoadedGroupCount => _loaded.Count;

        public bool IsGroupLoaded(Vector2Int group) => _loaded.ContainsKey(group);

        /// <summary>The viewer's chunk a loaded group was built for; (int.MinValue, int.MinValue) when it is not loaded. For tests.</summary>
        public Vector2Int BuiltAround(Vector2Int group) => _loaded.TryGetValue(group, out Group g) ? g.BuiltAround : new Vector2Int(int.MinValue, int.MinValue);

        public static float GroupSize => GroupChunks * CityLayout.ChunkSize;

        public static Vector2Int GroupOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / GroupSize), Mathf.FloorToInt(p.z / GroupSize));

        public static Vector2Int ChunkOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CityLayout.ChunkSize), Mathf.FloorToInt(p.z / CityLayout.ChunkSize));

        /// <summary>Starts again on a new world: every group dropped, the ones in flight ignored when they land.</summary>
        public void Rebuild()
        {
            _generation++;
            _world = new CityWorld(WorldSeed);
            foreach (KeyValuePair<Vector2Int, Group> pair in _loaded)
            {
                DestroyGroup(pair.Value.Object);
            }
            _loaded.Clear();
            _inFlight.Clear();
            _startedAll = false;
        }

        /// <summary>Every group round a point, built and put on screen now, on this thread (the game's start, captures, tests).</summary>
        public void StreamAround(Vector3 at)
        {
            if (_world == null)
            {
                Rebuild();
            }
            Vector2Int around = ChunkOf(at);
            Vector2Int centre = GroupOf(at);
            int radius = Mathf.CeilToInt(RadiusMetres / GroupSize);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var key = new Vector2Int(centre.x + dx, centre.y + dy);
                    if (Distance(key, at) > RadiusMetres)
                    {
                        continue;
                    }
                    if (_loaded.TryGetValue(key, out Group had))
                    {
                        if (!NeedsRebuild(key, had, around))
                        {
                            continue;
                        }
                        DestroyGroup(had.Object);
                        _loaded.Remove(key);
                    }
                    ChunkMeshData mesh = Generate(_world, key, around);
                    _loaded[key] = new Group { Object = Assemble(key, mesh), BuiltAround = around };
                }
            }
            _startedAll = true;
        }

        private void Update()
        {
            Transform viewer = Viewer != null ? Viewer : (Camera.main != null ? Camera.main.transform : null);
            if (viewer == null)
            {
                return;
            }
            if (_world == null)
            {
                Rebuild();
            }
            Vector3 at = viewer.position;
            if (BuildAllAtStart && !_startedAll)
            {
                StreamAround(at);
                return;
            }
            Vector2Int around = ChunkOf(at);
            Vector2Int centre = GroupOf(at);
            int radius = Mathf.CeilToInt(RadiusMetres / GroupSize);

            // Drop what is well out of range.
            _stale.Clear();
            foreach (KeyValuePair<Vector2Int, Group> pair in _loaded)
            {
                if (Distance(pair.Key, at) > RadiusMetres + DropMargin)
                {
                    _stale.Add(pair.Key);
                }
            }
            foreach (Vector2Int key in _stale)
            {
                DestroyGroup(_loaded[key].Object);
                _loaded.Remove(key);
            }

            // Ask for what is missing or stale, nearest first.
            _wanted.Clear();
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var key = new Vector2Int(centre.x + dx, centre.y + dy);
                    if (_inFlight.Contains(key) || Distance(key, at) > RadiusMetres)
                    {
                        continue;
                    }
                    if (!_loaded.TryGetValue(key, out Group had) || NeedsRebuild(key, had, around))
                    {
                        _wanted.Add(key);
                    }
                }
            }
            if (_wanted.Count > 1)
            {
                Vector3 sortAt = at;
                _wanted.Sort((a, b) => Distance(a, sortAt).CompareTo(Distance(b, sortAt)));
            }
            for (int i = 0; i < _wanted.Count && _inFlight.Count < Mathf.Max(1, InFlightLimit); i++)
            {
                Queue(_wanted[i], around);
            }

            // Put the finished ones on screen (a rebuilt group replaces the old one in the same frame: no gap).
            int assembled = 0;
            while (assembled < Mathf.Max(1, AssemblePerFrame) && _finished.TryDequeue(out (Vector2Int key, Vector2Int around, ChunkMeshData mesh, int generation) done))
            {
                _inFlight.Remove(done.key);
                if (done.generation != _generation || done.mesh == null || Distance(done.key, at) > RadiusMetres + DropMargin)
                {
                    done.mesh?.Release();
                    continue;
                }
                if (_loaded.TryGetValue(done.key, out Group old))
                {
                    DestroyGroup(old.Object);
                    _loaded.Remove(done.key);
                }
                _loaded[done.key] = new Group { Object = Assemble(done.key, done.mesh), BuiltAround = done.around };
                assembled++;
            }
        }

        /// <summary>
        /// A group built for another viewer chunk needs building again only if it is near enough for the exclusion to matter: within the
        /// exclusion radius plus the step of either the chunk it was built for or the viewer's chunk now. A group far from both is the same mesh
        /// whichever chunk it was built for.
        /// </summary>
        private static bool NeedsRebuild(Vector2Int key, Group had, Vector2Int around)
        {
            if (Mathf.Max(Mathf.Abs(had.BuiltAround.x - around.x), Mathf.Abs(had.BuiltAround.y - around.y)) < RebuildStepChunks)
            {
                return false;
            }
            return NearViewer(key, had.BuiltAround) || NearViewer(key, around);
        }

        /// <summary>Whether any chunk of a group is within the exclusion radius (plus the step) of a viewer chunk.</summary>
        public static bool NearViewer(Vector2Int group, Vector2Int around)
        {
            int x0 = group.x * GroupChunks, x1 = x0 + GroupChunks - 1;
            int y0 = group.y * GroupChunks, y1 = y0 + GroupChunks - 1;
            int dx = Mathf.Max(Mathf.Max(x0 - around.x, 0), around.x - x1);   // two-argument Max: the three-argument form allocates
            int dy = Mathf.Max(Mathf.Max(y0 - around.y, 0), around.y - y1);
            return Mathf.Max(dx, dy) <= ExcludeRadiusChunks + RebuildStepChunks;
        }

        /// <summary>
        /// How far a group's nearest edge is from a point, horizontally, metres. Two-argument Max only: the three-argument form is the
        /// params overload and allocates an array, and this runs for every nearby group every frame (it was three quarters of the garbage,
        /// docs/PERFORMANCE.md).
        /// </summary>
        public static float Distance(Vector2Int group, Vector3 p)
        {
            float size = GroupSize;
            float x0 = group.x * size, z0 = group.y * size;
            float dx = Mathf.Max(Mathf.Max(x0 - p.x, 0.0f), p.x - (x0 + size));
            float dz = Mathf.Max(Mathf.Max(z0 - p.z, 0.0f), p.z - (z0 + size));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static ChunkMeshData Generate(CityWorld world, Vector2Int key, Vector2Int around)
        {
            var ground = new MeshBuffer();
            var structures = new MeshBuffer();
            var origin = new Vector3(key.x * GroupSize, 0.0f, key.y * GroupSize);
            CityMeshBuilder.BuildSkyline(world, key.x * GroupChunks, key.y * GroupChunks, GroupChunks, origin, ground, structures, around, ExcludeRadiusChunks);
            return ChunkMeshData.Combine(new[] { new[] { ground }, new[] { structures } });
        }

        private void Queue(Vector2Int key, Vector2Int around)
        {
            _inFlight.Add(key);
            CityWorld world = _world;
            int generation = _generation;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                ChunkMeshData mesh = null;
                try
                {
                    mesh = Generate(world, key, around);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
                _finished.Enqueue((key, around, mesh, generation));
            });
        }

        private GameObject Assemble(Vector2Int key, ChunkMeshData data)
        {
            var go = new GameObject($"Skyline {key.x},{key.y}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(key.x * GroupSize, 0.0f, key.y * GroupSize);
            if (data.SubMeshGroups.Length > 0)
            {
                var mesh = new Mesh { name = $"skyline-{key.x}-{key.y}" };
                data.ApplyTo(mesh);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                Material[] materialFor = { GroundMaterial, StructureMaterial };   // BuildSkyline's groups, in order
                var materials = new Material[data.SubMeshes.Length];
                for (int slot = 0; slot < data.SubMeshGroups.Length; slot++)
                {
                    materials[slot] = materialFor[data.SubMeshGroups[slot]];
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // far beyond the shadow distance
                renderer.receiveShadows = false;
            }
            data.Release();
            return go;
        }

        private static void DestroyGroup(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            var filter = go.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Destroy(filter.sharedMesh);
            }
            Destroy(go);
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<Vector2Int, Group> pair in _loaded)
            {
                DestroyGroup(pair.Value.Object);
            }
            _loaded.Clear();
        }
    }
}

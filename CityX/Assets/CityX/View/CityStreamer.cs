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

        public Material StructureMaterial;

        [Tooltip("Leave empty to follow the main camera.")]
        public Transform Viewer;

        private CityWorld _world;
        private SkybridgeLayout _bridges;
        private readonly Dictionary<Vector2Int, GameObject> _loaded = new Dictionary<Vector2Int, GameObject>();
        private readonly MeshBuffer _ground = new MeshBuffer();
        private readonly MeshBuffer _structures = new MeshBuffer();
        private readonly List<Vector2Int> _wanted = new List<Vector2Int>();
        private readonly List<Vector2Int> _stale = new List<Vector2Int>();

        private void Awake()
        {
            Rebuild();
        }

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
            _structures.Clear();

            // Chunks are built relative to their own corner and then placed, so
            // vertex coordinates stay small. Building in absolute world space
            // would lose float precision a long way from the origin -- which is
            // exactly where an infinite world spends most of its time.
            Vector3 origin = new Vector3(
                key.x * CityLayout.ChunkSize, 0.0f, key.y * CityLayout.ChunkSize);

            CityMeshBuilder.BuildChunk(_world, _bridges, key.x, key.y, origin, _ground, _structures);

            GameObject go = new GameObject($"Chunk {key.x},{key.y}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = origin;

            Mesh mesh = new Mesh { name = $"chunk-{key.x}-{key.y}" };

            int groundVerts = _ground.Vertices.Count;
            var vertices = new List<Vector3>(groundVerts + _structures.Vertices.Count);
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            vertices.AddRange(_ground.Vertices);
            vertices.AddRange(_structures.Vertices);
            normals.AddRange(_ground.Normals);
            normals.AddRange(_structures.Normals);
            uvs.AddRange(_ground.Uvs);
            uvs.AddRange(_structures.Uvs);

            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(_ground.Triangles, 0);

            var shifted = new List<int>(_structures.Triangles.Count);
            foreach (int index in _structures.Triangles)
            {
                shifted.Add(index + groundVerts);
            }
            mesh.SetTriangles(shifted, 1);
            mesh.RecalculateBounds();

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { GroundMaterial, StructureMaterial };

            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            return go;
        }

        private static void DestroyChunk(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            // The mesh is created here, so it is this object's to free. Unity
            // does not collect meshes with the GameObject, and a streamer that
            // leaks one mesh per chunk will eat a machine alive over a few
            // minutes of walking.
            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
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

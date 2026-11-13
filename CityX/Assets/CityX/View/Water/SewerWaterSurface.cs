using System.Collections.Generic;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.View
{
    /// <summary>
    /// THE SEWERS' WATER IN THE SCENE (docs/SEWERS.md): while enabled, the game's bodies wade in it (<see cref="SewerWaterSource"/> in
    /// <see cref="CurrentWater"/>: slowed, splashing, soaked, as in any water), and the sheet of it round the camera is drawn in the sewer
    /// water's material (<see cref="SewerWaterMesh"/>), built again when the camera has moved on. The water is the plan's, so every
    /// machine has the same without a word sent.
    /// </summary>
    public sealed class SewerWaterSurface : MonoBehaviour
    {
        [Tooltip("The sewers' water (CityX/Water, murky and lit only by what shines underground).")]
        public Material Material;

        /// <summary>The water is drawn this far round the camera, metres (the dark underground hides it long before).</summary>
        public const float DrawRadius = 90.0f;

        /// <summary>The sheet is built again once the camera is this far from where it was last built round.</summary>
        public const float RebuildAfter = 20.0f;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _flows = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private CityWorld _builtFor;
        private Vector2 _builtAround;

        /// <summary>The camera the sheet follows; the main camera when not set.</summary>
        public Camera Eye;

        /// <summary>Triangles in the sheet as last built, for tests.</summary>
        public int TriangleCount => _triangles.Count / 3;

        private void OnEnable()
        {
            CurrentWater.AddSource(SewerWaterSource.Instance);
            if (_renderer == null)
            {
                var host = new GameObject("SewerWaterSheet");
                host.transform.SetParent(transform, false);
                _mesh = new Mesh { name = "SewerWater", indexFormat = IndexFormat.UInt32 };
                _mesh.MarkDynamic();
                host.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _renderer = host.AddComponent<MeshRenderer>();
                _renderer.sharedMaterial = Material;
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
            }
            _builtFor = null;
        }

        private void OnDisable()
        {
            CurrentWater.RemoveSource(SewerWaterSource.Instance);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        private void LateUpdate()
        {
            Camera eye = Eye != null ? Eye : Camera.main;
            CityWorld world = CurrentWorld.Instance;
            if (eye == null || world == null)
            {
                return;
            }
            Vector3 at = eye.transform.position;
            var around = new Vector2(at.x, at.z);
            if (world == _builtFor && (around - _builtAround).sqrMagnitude < RebuildAfter * RebuildAfter)
            {
                return;
            }
            Rebuild(world, around);
        }

        /// <summary>Builds the sheet round a point now.</summary>
        public void Rebuild(CityWorld world, Vector2 around)
        {
            _builtFor = world;
            _builtAround = around;
            SewerWaterMesh.Build(world.Plan, around, DrawRadius, _vertices, _flows, _triangles);
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(1, _flows);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
            _renderer.enabled = _triangles.Count > 0;
        }
    }
}

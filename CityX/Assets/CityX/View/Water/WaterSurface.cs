using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A body of water in the scene (docs/WATER.md): the <see cref="WaterBody"/> the game asks about (registered in
    /// <see cref="CurrentWater"/> while this is enabled) and the box of water drawn for it, its top at the surface and its sides down to its
    /// floor, in the one water material (CityX/Water). A surface that moves (a flood) is redrawn when it does: a box is twenty vertices.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WaterSurface : MonoBehaviour
    {
        private static readonly int FlowId = Shader.PropertyToID("_Flow");

        /// <summary>The water this draws.</summary>
        public WaterBody Body { get; private set; }

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _drawnSurface = float.NaN;
        private float _drawnBottom = float.NaN;
        private Vector2 _drawnFlow = new Vector2(float.NaN, float.NaN);

        /// <summary>A new body of water drawn in <paramref name="material"/>.</summary>
        public static WaterSurface Create(WaterBody body, Material material, string name = "Water")
        {
            var host = new GameObject(name);
            host.SetActive(false);
            host.AddComponent<MeshFilter>();
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var surface = host.AddComponent<WaterSurface>();
            surface.Body = body;
            host.SetActive(true);
            return surface;
        }

        private void OnEnable()
        {
            CurrentWater.Add(Body);
        }

        private void OnDisable()
        {
            CurrentWater.Remove(Body);
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
            Redraw();
        }

        /// <summary>Redraws the box if the water has moved since it was last drawn (and its flow into the material).</summary>
        public void Redraw()
        {
            if (Body == null)
            {
                return;
            }
            if (_renderer == null)
            {
                _renderer = GetComponent<MeshRenderer>();
                _block = new MaterialPropertyBlock();
            }
            if (Body.Flow != _drawnFlow)
            {
                _drawnFlow = Body.Flow;
                _block.SetVector(FlowId, new Vector4(Body.Flow.x, 0.0f, Body.Flow.y, 0.0f));
                _renderer.SetPropertyBlock(_block);
            }
            if (Mathf.Abs(Body.SurfaceY - _drawnSurface) < 0.001f && Mathf.Abs(Body.BottomY - _drawnBottom) < 0.001f)
            {
                return;
            }
            _drawnSurface = Body.SurfaceY;
            _drawnBottom = Body.BottomY;
            bool any = Body.SurfaceY > Body.BottomY + 0.005f;
            _renderer.enabled = any;
            if (!any)
            {
                return;
            }
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "WaterBox" };
                _mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
            BuildBox(_mesh, Body);
        }

        /// <summary>The top (facing up) and the four sides (facing out), in world space on an unmoved host.</summary>
        private static void BuildBox(Mesh mesh, WaterBody body)
        {
            float x0 = body.MinX, x1 = body.MaxX, z0 = body.MinZ, z1 = body.MaxZ, top = body.SurfaceY, bottom = body.BottomY;
            var vertices = new[]
            {
                // The top.
                new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0),
                // South (-z), north (+z), west (-x), east (+x): each bottom-left, top-left, top-right, bottom-right as seen from outside.
                new Vector3(x0, bottom, z0), new Vector3(x0, top, z0), new Vector3(x1, top, z0), new Vector3(x1, bottom, z0),
                new Vector3(x1, bottom, z1), new Vector3(x1, top, z1), new Vector3(x0, top, z1), new Vector3(x0, bottom, z1),
                new Vector3(x0, bottom, z1), new Vector3(x0, top, z1), new Vector3(x0, top, z0), new Vector3(x0, bottom, z0),
                new Vector3(x1, bottom, z0), new Vector3(x1, top, z0), new Vector3(x1, top, z1), new Vector3(x1, bottom, z1),
            };
            var triangles = new int[5 * 6];
            for (int face = 0; face < 5; face++)
            {
                int v = face * 4, t = face * 6;
                // Clockwise as seen from outside (Unity's front face): 0-1-2, 0-2-3 with the corners above in order.
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}

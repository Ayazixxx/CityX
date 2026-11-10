using System.Collections.Generic;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The sky the conveyor belt hangs in (docs/BELT_INTRO.md): one wall of open sky far behind it, deep blue overhead and pale far below,
    /// and nothing else (owner, 2026-11-10: "no clouds in the sky ... and no building remanence left in the background"). The body is
    /// dropped off the belt's end into it, and the game's own fall from the sky comes next. Built in code when it wakes, on this object's
    /// layer, the only one the creator's cameras see.
    /// </summary>
    public sealed class BeltSky : MonoBehaviour
    {
        public static readonly Color Overhead = new Color(0.16f, 0.36f, 0.78f);
        public static readonly Color Middle = new Color(0.45f, 0.66f, 0.93f);
        public static readonly Color Below = new Color(0.86f, 0.92f, 0.98f);

        /// <summary>How far behind the belt the wall of sky stands, how wide and tall it is, and how far under the belt it is palest, metres.</summary>
        private const float WallDistance = 150.0f;

        private const float WallWidth = 1200.0f;
        private const float WallHeight = 700.0f;
        private const float PalestBelow = 60.0f;

        private readonly List<Object> _made = new List<Object>();
        private bool _built;

        private void Awake()
        {
            Build();
        }

        private void OnDestroy()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                {
                    Destroy(made);
                }
            }
        }

        /// <summary>Makes the sky if it has not been made. Awake does; public for a render made outside play.</summary>
        public void Build()
        {
            if (_built)
            {
                return;
            }
            _built = true;

            // Three bands of colour run into each other, unlit.
            float w = WallWidth * 0.5f, top = WallHeight * 0.5f, bottom = -WallHeight * 0.5f;
            var mesh = new Mesh { name = "BeltSkyWall", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-w, bottom, 0.0f), new Vector3(w, bottom, 0.0f),
                new Vector3(-w, -PalestBelow, 0.0f), new Vector3(w, -PalestBelow, 0.0f),
                new Vector3(-w, 10.0f, 0.0f), new Vector3(w, 10.0f, 0.0f),
                new Vector3(-w, top, 0.0f), new Vector3(w, top, 0.0f),
            });
            mesh.SetColors(new List<Color> { Below, Below, Below, Below, Middle, Middle, Overhead, Overhead });
            // Both ways round: it is seen from one side only, whichever way the stage is turned.
            var triangles = new List<int>();
            for (int band = 0; band < 3; band++)
            {
                int a = band * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles.AddRange(new[] { a, c, b, b, c, d, a, b, c, b, d, c });
            }
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            _made.Add(mesh);

            var wall = new GameObject("Wall");
            wall.layer = gameObject.layer;
            wall.transform.SetParent(transform, false);
            wall.transform.localPosition = new Vector3(0.0f, 0.0f, -WallDistance);
            wall.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            _made.Add(material);
            MeshRenderer drawn = wall.AddComponent<MeshRenderer>();
            drawn.sharedMaterial = material;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            drawn.receiveShadows = false;
        }
    }
}

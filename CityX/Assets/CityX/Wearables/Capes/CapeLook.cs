using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The cape as it is drawn (docs/CAPE.md): the yoke, the falling cloth and the collar as sheets with two faces (each drawn only from its own
    /// side, as the game draws everything), all in the one fabric. Remade from the cloth's points every frame, under the cape's own object so it
    /// goes where the wearer goes (and onto the title screen's stage layer with them).
    /// </summary>
    public sealed class CapeLook : System.IDisposable
    {
        private readonly Transform _owner;
        private readonly Mesh _mesh;
        private readonly GameObject _object;
        private readonly MeshRenderer _renderer;
        private readonly Vector3[] _vertices = new Vector3[CapeShape.Points * 2];

        public CapeLook(Transform owner, Material fabric)
        {
            _owner = owner;
            _mesh = new Mesh { name = "Cape" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.SetTriangles(Faces(), 0);
            _object = new GameObject("CapeCloth");
            _object.transform.SetParent(_owner, false);
            _object.layer = _owner.gameObject.layer;
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _object.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = fabric;
        }

        /// <summary>Whether any camera drew it last frame (its shadow counts).</summary>
        public bool Seen => _renderer != null && _renderer.isVisible;

        /// <summary>
        /// Every face, outside and inside: the points' first copy faces away from the body, the second (<see cref="CapeShape.Points"/> on) towards
        /// it, so each side is lit from its own side.
        /// </summary>
        public static int[] Faces()
        {
            var triangles = new List<int>();
            void Quad(int a, int b, int c, int d)   // a b along, c d the next row
            {
                triangles.AddRange(new[] { a, b, c, b, d, c });
                int o = CapeShape.Points;
                triangles.AddRange(new[] { a + o, c + o, b + o, b + o, c + o, d + o });
            }
            for (int row = 0; row + 1 < CapeShape.Rows; row++)
            {
                for (int column = 0; column + 1 < CapeShape.Columns; column++)
                {
                    Quad(CapeShape.GridIndex(column, row), CapeShape.GridIndex(column + 1, row), CapeShape.GridIndex(column, row + 1),
                        CapeShape.GridIndex(column + 1, row + 1));
                }
            }
            for (int along = 0; along + 1 < CapeShape.CollarPoints; along++)
            {
                Quad(CapeShape.CollarIndex(along + 1, false), CapeShape.CollarIndex(along, false), CapeShape.CollarIndex(along + 1, true),
                    CapeShape.CollarIndex(along, true));
            }
            return triangles.ToArray();
        }

        /// <summary>Draws the cape at the cloth's points (world space).</summary>
        public void Draw(Vector3[] points)
        {
            Matrix4x4 toLocal = _owner.worldToLocalMatrix;
            for (int i = 0; i < CapeShape.Points; i++)
            {
                Vector3 p = toLocal.MultiplyPoint3x4(points[i]);
                _vertices[i] = p;
                _vertices[i + CapeShape.Points] = p;
            }
            _mesh.vertices = _vertices;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        public void Dispose()
        {
            if (_object != null) Object.Destroy(_object);
            Object.Destroy(_mesh);
        }
    }
}

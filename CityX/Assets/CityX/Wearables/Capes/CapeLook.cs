using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The cape as it is drawn (docs/CAPE.md): the cloth as a sheet with two faces, its outside in one material and its lining in another (each
    /// face drawn only from its own side, as the game draws everything), and its top edge rolled into a hem (a tube in the outside's material)
    /// that shows as a band over each shoulder from in front, as the owner drew it. Remade from the cloth's points every frame, under the cape's
    /// own object so it goes where the wearer goes (and onto the title screen's stage layer with them).
    /// </summary>
    public sealed class CapeLook : System.IDisposable
    {
        /// <summary>Sides round the rolled hem.</summary>
        public const int TubeSides = 8;

        private readonly Transform _owner;
        private readonly Mesh _cloth;
        private readonly Mesh _edge;
        private readonly GameObject _clothObject;
        private readonly GameObject _edgeObject;
        private readonly Vector3[] _clothVertices = new Vector3[CapeShape.Points * 2];
        private readonly Vector3[] _edgeVertices = new Vector3[(CapeShape.Columns + 2) * TubeSides];

        public CapeLook(Transform owner, Material outside, Material lining)
        {
            _owner = owner;
            _cloth = new Mesh { name = "Cape cloth" };
            _cloth.MarkDynamic();
            _cloth.vertices = _clothVertices;
            _cloth.subMeshCount = 2;
            _cloth.SetTriangles(Faces(false), 0);
            _cloth.SetTriangles(Faces(true), 1);
            _clothObject = Part("CapeCloth", _cloth, new[] { outside, lining });
            _edge = new Mesh { name = "Cape hem" };
            _edge.MarkDynamic();
            _edge.vertices = _edgeVertices;
            _edge.SetTriangles(EdgeTriangles(), 0);
            _edgeObject = Part("CapeHem", _edge, new[] { outside });
        }

        private GameObject Part(string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_owner, false);
            go.layer = _owner.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        /// <summary>The sheet's triangles: the outside facing away from the body, or the lining facing it (its own copy of the points).</summary>
        public static int[] Faces(bool lining)
        {
            var triangles = new List<int>();
            int offset = lining ? CapeShape.Points : 0;
            for (int row = 0; row + 1 < CapeShape.Rows; row++)
            {
                for (int column = 0; column + 1 < CapeShape.Columns; column++)
                {
                    int a = CapeShape.GridIndex(column, row) + offset, b = CapeShape.GridIndex(column + 1, row) + offset;
                    int c = CapeShape.GridIndex(column, row + 1) + offset, d = CapeShape.GridIndex(column + 1, row + 1) + offset;
                    if (lining) triangles.AddRange(new[] { a, c, b, b, c, d });
                    else triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            }
            return triangles.ToArray();
        }

        /// <summary>The hem's tube: a ring at each point of the top edge, and one more at each end closed to a point (its rings are joined in order).</summary>
        private static int[] EdgeTriangles()
        {
            var triangles = new List<int>();
            int rings = CapeShape.Columns + 2;
            for (int k = 0; k + 1 < rings; k++)
            {
                for (int s = 0; s < TubeSides; s++)
                {
                    int a = k * TubeSides + s, b = k * TubeSides + (s + 1) % TubeSides;
                    int c = a + TubeSides, d = b + TubeSides;
                    triangles.AddRange(new[] { a, b, c, b, d, c });   // faces outward
                }
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
                _clothVertices[i] = p;
                _clothVertices[i + CapeShape.Points] = p;
            }
            _cloth.vertices = _clothVertices;
            _cloth.RecalculateNormals();
            _cloth.RecalculateBounds();
            Hem();
            _edge.vertices = _edgeVertices;
            _edge.RecalculateNormals();
            _edge.RecalculateBounds();
        }

        /// <summary>The rolled hem along the top edge (the cloth's top row), its ends tapered shut just past the corners.</summary>
        private void Hem()
        {
            int columns = CapeShape.Columns;
            for (int k = 0; k < columns + 2; k++)
            {
                int column = Mathf.Clamp(k - 1, 0, columns - 1);
                Vector3 p = _clothVertices[CapeShape.GridIndex(column, 0)];
                Vector3 next = _clothVertices[CapeShape.GridIndex(Mathf.Min(column + 1, columns - 1), 0)];
                Vector3 back = _clothVertices[CapeShape.GridIndex(Mathf.Max(column - 1, 0), 0)];
                Vector3 along = (next - back).sqrMagnitude > 1e-10f ? (next - back).normalized : Vector3.right;
                Vector3 side = Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
                Vector3 other = Vector3.Cross(along, side);
                bool cap = k == 0 || k == columns + 1;
                float radius = cap ? 0.0f : CapeShape.EdgeRadius;
                if (cap) p += along * (k == 0 ? -CapeShape.EdgeRadius : CapeShape.EdgeRadius);
                for (int s = 0; s < TubeSides; s++)
                {
                    float angle = s * Mathf.PI * 2.0f / TubeSides;
                    _edgeVertices[k * TubeSides + s] = p + (side * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * radius;
                }
            }
        }

        public void Dispose()
        {
            if (_clothObject != null) Object.Destroy(_clothObject);
            if (_edgeObject != null) Object.Destroy(_edgeObject);
            Object.Destroy(_cloth);
            Object.Destroy(_edge);
        }
    }
}

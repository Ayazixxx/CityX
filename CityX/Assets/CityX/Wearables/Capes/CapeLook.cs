using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The cape as it is drawn (docs/CAPE.md): the cloth as a sheet with two faces, its outside in one material and its lining in another (each
    /// face drawn only from its own side, as the game draws everything), and the cords, the knot and its ends as thin tubes in the ties'
    /// material. Remade from the cloth's points every frame, under the cape's own object so it goes where the wearer goes (and onto the title
    /// screen's stage layer with them).
    /// </summary>
    public sealed class CapeLook : System.IDisposable
    {
        /// <summary>Sides round each cord's tube, and the knot ball's rings and segments.</summary>
        public const int TubeSides = 6;
        public const int KnotRings = 5;
        public const int KnotSegments = 8;

        private readonly Transform _owner;
        private readonly Mesh _cloth;
        private readonly Mesh _ties;
        private readonly GameObject _clothObject;
        private readonly GameObject _tiesObject;
        private readonly Vector3[] _clothVertices = new Vector3[CapeShape.GridPoints * 2];
        private readonly List<Vector3> _tieVertices = new List<Vector3>();
        private readonly List<int> _tieTriangles = new List<int>();

        /// <summary>The cords and ends as chains of points, each from where it starts.</summary>
        public static readonly int[][] Chains = BuildChains();

        public CapeLook(Transform owner, Material outside, Material lining, Material ties)
        {
            _owner = owner;
            _cloth = new Mesh { name = "Cape cloth" };
            _cloth.MarkDynamic();
            _cloth.vertices = _clothVertices;
            _cloth.subMeshCount = 2;
            _cloth.SetTriangles(Faces(false), 0);
            _cloth.SetTriangles(Faces(true), 1);
            _clothObject = Part("CapeCloth", _cloth, new[] { outside, lining });
            _ties = new Mesh { name = "Cape ties" };
            _ties.MarkDynamic();
            _tiesObject = Part("CapeTies", _ties, new[] { ties });
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
            int offset = lining ? CapeShape.GridPoints : 0;
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

        private static int[][] BuildChains()
        {
            var chains = new List<int[]>();
            foreach (bool first in new[] { true, false })
            {
                var cord = new List<int> { CapeShape.GridIndex(first ? 0 : CapeShape.Columns - 1, 0) };
                for (int i = 1; i < CapeShape.CordPoints - 1; i++) cord.Add(CapeShape.CordIndex(first, i));
                cord.Add(CapeShape.KnotIndex);
                chains.Add(cord.ToArray());
                var end = new List<int> { CapeShape.KnotIndex };
                for (int i = 0; i < CapeShape.EndPoints; i++) end.Add(CapeShape.EndIndex(first, i));
                chains.Add(end.ToArray());
            }
            return chains.ToArray();
        }

        /// <summary>Draws the cape at the cloth's points (world space).</summary>
        public void Draw(Vector3[] points)
        {
            Matrix4x4 toLocal = _owner.worldToLocalMatrix;
            for (int i = 0; i < CapeShape.GridPoints; i++)
            {
                Vector3 p = toLocal.MultiplyPoint3x4(points[i]);
                _clothVertices[i] = p;
                _clothVertices[i + CapeShape.GridPoints] = p;
            }
            _cloth.vertices = _clothVertices;
            _cloth.RecalculateNormals();
            _cloth.RecalculateBounds();

            _tieVertices.Clear();
            _tieTriangles.Clear();
            foreach (int[] chain in Chains)
            {
                Tube(chain, points, toLocal);
            }
            Ball(toLocal.MultiplyPoint3x4(points[CapeShape.KnotIndex]), CapeShape.KnotRadius);
            _ties.Clear();
            _ties.SetVertices(_tieVertices);
            _ties.SetTriangles(_tieTriangles, 0);
            _ties.RecalculateNormals();
            _ties.RecalculateBounds();
        }

        private void Tube(int[] chain, Vector3[] points, Matrix4x4 toLocal)
        {
            int start = _tieVertices.Count;
            for (int k = 0; k < chain.Length; k++)
            {
                Vector3 p = toLocal.MultiplyPoint3x4(points[chain[k]]);
                Vector3 next = toLocal.MultiplyPoint3x4(points[chain[Mathf.Min(k + 1, chain.Length - 1)]]);
                Vector3 back = toLocal.MultiplyPoint3x4(points[chain[Mathf.Max(k - 1, 0)]]);
                Vector3 along = (next - back).sqrMagnitude > 1e-10f ? (next - back).normalized : Vector3.down;
                Vector3 side = Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 other = Vector3.Cross(along, side);
                for (int s = 0; s < TubeSides; s++)
                {
                    float angle = s * Mathf.PI * 2.0f / TubeSides;
                    _tieVertices.Add(p + (side * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * CapeShape.CordRadius);
                }
            }
            for (int k = 0; k + 1 < chain.Length; k++)
            {
                for (int s = 0; s < TubeSides; s++)
                {
                    int a = start + k * TubeSides + s, b = start + k * TubeSides + (s + 1) % TubeSides;
                    int c = a + TubeSides, d = b + TubeSides;
                    _tieTriangles.AddRange(new[] { a, b, c, b, d, c });   // faces outward
                }
            }
        }

        private void Ball(Vector3 centre, float radius)
        {
            int start = _tieVertices.Count;
            for (int ring = 0; ring <= KnotRings; ring++)
            {
                float v = ring / (float)KnotRings * Mathf.PI;
                for (int segment = 0; segment < KnotSegments; segment++)
                {
                    float u = segment / (float)KnotSegments * Mathf.PI * 2.0f;
                    _tieVertices.Add(centre + new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u)) * radius);
                }
            }
            for (int ring = 0; ring < KnotRings; ring++)
            {
                for (int segment = 0; segment < KnotSegments; segment++)
                {
                    int a = start + ring * KnotSegments + segment, b = start + ring * KnotSegments + (segment + 1) % KnotSegments;
                    int c = a + KnotSegments, d = b + KnotSegments;
                    _tieTriangles.AddRange(new[] { a, b, c, b, d, c });
                }
            }
        }

        public void Dispose()
        {
            if (_clothObject != null) Object.Destroy(_clothObject);
            if (_tiesObject != null) Object.Destroy(_tiesObject);
            Object.Destroy(_cloth);
            Object.Destroy(_ties);
        }
    }
}

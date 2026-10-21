using System.Collections.Generic;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// A garment mesh's openings (a shirt's neck, hem and cuffs): the rims of its open edges, each with its area vector (the sum of its edges'
    /// cross products, along the opening's normal as the mesh winds round it). The mesh's points are welded first: it is split along its
    /// texture seams, which are not openings.
    /// </summary>
    public sealed class MeshOpenings
    {
        /// <summary>Points of the mesh nearer than this are one point.</summary>
        private const float WeldDistance = 1e-4f;

        /// <summary>The mesh's points, welded.</summary>
        public readonly List<Vector3> Points = new List<Vector3>();

        /// <summary>Each opening's rim (its points) and area vector.</summary>
        public readonly List<(List<Vector3> Rim, Vector3 Area)> Rims = new List<(List<Vector3>, Vector3)>();

        public static MeshOpenings Of(Mesh mesh)
        {
            var openings = new MeshOpenings();
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            var weld = new Dictionary<Vector3Int, int>();
            var id = new int[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3Int key = Vector3Int.RoundToInt(vertices[v] / WeldDistance);
                if (!weld.TryGetValue(key, out id[v]))
                {
                    id[v] = weld[key] = openings.Points.Count;
                    openings.Points.Add(vertices[v]);
                }
            }
            // An open edge is used by one triangle only, in its winding: so walked, round an opening they all go the same way.
            var edges = new Dictionary<(int, int), (int from, int to, int uses)>();
            for (int t = 0; t < triangles.Length; t++)
            {
                int a = id[triangles[t]], b = id[triangles[t % 3 == 2 ? t - 2 : t + 1]];
                var key = (Mathf.Min(a, b), Mathf.Max(a, b));
                edges[key] = edges.TryGetValue(key, out var edge) ? (edge.from, edge.to, edge.uses + 1) : (a, b, 1);
            }
            var opening = new int[openings.Points.Count];
            for (int p = 0; p < opening.Length; p++)
            {
                opening[p] = p;
            }
            var open = new List<(int from, int to)>();
            foreach (var edge in edges.Values)
            {
                if (edge.uses == 1)
                {
                    open.Add((edge.from, edge.to));
                    opening[Root(opening, edge.from)] = Root(opening, edge.to);
                }
            }
            var rims = new Dictionary<int, (List<Vector3> rim, Vector3 area)>();
            foreach ((int from, int to) in open)
            {
                int root = Root(opening, from);
                (List<Vector3> rim, Vector3 area) = rims.TryGetValue(root, out var found) ? found : (new List<Vector3>(), Vector3.zero);
                rim.Add(openings.Points[from]);
                rims[root] = (rim, area + Vector3.Cross(openings.Points[from], openings.Points[to]));
            }
            openings.Rims.AddRange(rims.Values);
            return openings;
        }

        private static int Root(int[] parent, int p)
        {
            while (parent[p] != p)
            {
                p = parent[p] = parent[parent[p]];
            }
            return p;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// A short sleeve's cuff, read off a garment's open edge round an arm (bind pose), and which skin lies past it, down the arm: the bare arm
    /// the garment leaves drawn. A flat height for it let through the strip of arm inside a slanted cuff, which came through the sleeve.
    /// </summary>
    public sealed class SleeveCuff
    {
        /// <summary>Points of the garment's mesh nearer than this are one point (the mesh is split along its texture seams).</summary>
        private const float WeldDistance = 1e-4f;

        /// <summary>The garment's cloth this near the rim is its cuff, whose lip may hang past the rim.</summary>
        private const float LipReach = 0.03f;

        private readonly Vector3 _centre;
        private readonly Vector3 _down;
        private readonly Vector3 _across;
        private readonly Vector3 _ahead;

        /// <summary>The rim's points, by their turn round the arm: each one's turn and how far down the arm it lies.</summary>
        private readonly float[] _turns;
        private readonly float[] _alongs;

        private SleeveCuff(List<Vector3> rim, Vector3 area)
        {
            foreach (Vector3 p in rim)
            {
                _centre += p / rim.Count;
            }
            _down = area.y < 0.0f ? area.normalized : -area.normalized;
            _across = Vector3.Cross(_down, Vector3.forward).normalized;
            _ahead = Vector3.Cross(_down, _across);
            var byTurn = new SortedList<float, float>();
            foreach (Vector3 p in rim)
            {
                byTurn[Turn(p)] = Vector3.Dot(p - _centre, _down);
            }
            _turns = new float[byTurn.Count];
            _alongs = new float[byTurn.Count];
            byTurn.Keys.CopyTo(_turns, 0);
            byTurn.Values.CopyTo(_alongs, 0);
        }

        /// <summary>How far the garment's cloth near the rim reaches down the arm past it (a cuff rolled under hangs past its own edge).</summary>
        private float _lip;

        /// <summary>
        /// Whether <paramref name="point"/> (bind pose) lies past the cuff, down its arm, at its own turn round the arm, or within
        /// <paramref name="tuck"/> of it inside the sleeve.
        /// </summary>
        public bool IsPast(Vector3 point, float tuck) => Mathf.Sign(point.x) == Mathf.Sign(_centre.x) && PastRim(point) > _lip - tuck;

        /// <summary>How far <paramref name="point"/> lies down the arm past the rim, at its own turn round the arm.</summary>
        private float PastRim(Vector3 point)
        {
            float along = Vector3.Dot(point - _centre, _down);
            float turn = Turn(point);
            int next = System.Array.BinarySearch(_turns, turn);
            if (next >= 0)
            {
                return along - _alongs[next];
            }
            next = ~next % _turns.Length;
            int previous = (next + _turns.Length - 1) % _turns.Length;
            float span = Mathf.Repeat(_turns[next] - _turns[previous], 2.0f * Mathf.PI);
            float t = span > 0.0f ? Mathf.Repeat(turn - _turns[previous], 2.0f * Mathf.PI) / span : 0.0f;
            return along - Mathf.Lerp(_alongs[previous], _alongs[next], t);
        }

        /// <summary>
        /// The cuffs of <paramref name="garment"/> (bind pose): its openings whose middle lies further out to a side than
        /// <paramref name="armFromX"/> (a shirt's neck and hem lie in the middle).
        /// </summary>
        public static List<SleeveCuff> Of(Mesh garment, float armFromX)
        {
            Vector3[] vertices = garment.vertices;
            int[] triangles = garment.triangles;
            var weld = new Dictionary<Vector3Int, int>();
            var points = new List<Vector3>();
            var id = new int[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3Int key = Vector3Int.RoundToInt(vertices[v] / WeldDistance);
                if (!weld.TryGetValue(key, out id[v]))
                {
                    id[v] = weld[key] = points.Count;
                    points.Add(vertices[v]);
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
            var opening = new int[points.Count];
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
                rim.Add(points[from]);
                rims[root] = (rim, area + Vector3.Cross(points[from], points[to]));
            }
            var cuffs = new List<SleeveCuff>();
            foreach ((List<Vector3> rim, Vector3 area) in rims.Values)
            {
                var cuff = new SleeveCuff(rim, area);
                if (Mathf.Abs(cuff._centre.x) <= armFromX)
                {
                    continue;
                }
                foreach (Vector3 p in points)
                {
                    if (rim.Exists(r => (r - p).sqrMagnitude < LipReach * LipReach))
                    {
                        cuff._lip = Mathf.Max(cuff._lip, cuff.PastRim(p));
                    }
                }
                cuffs.Add(cuff);
            }
            return cuffs;
        }

        private float Turn(Vector3 point)
        {
            Vector3 q = point - _centre;
            return Mathf.Atan2(Vector3.Dot(q, _ahead), Vector3.Dot(q, _across));
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

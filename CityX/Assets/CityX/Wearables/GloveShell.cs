using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The fingerless gloves' shape (docs/FINGERLESS_GLOVES.md): the body's own hands, lifted a few millimetres off the skin, cut off half way
    /// up each finger and the thumb and a hand's length up the forearm, with a wall round every opening so the leather has a thickness and the
    /// game's black line runs round it. Made from the skin's own vertices, bone weights and bind poses, so the gloves bend with every finger
    /// exactly as the hand does (a fist, a grip on a weapon) and fit any hand the body mesh is rebuilt with, with nothing to refit.
    ///
    /// Pure: give it the body mesh's arrays and which of its bones is which; nothing here needs a scene.
    /// </summary>
    public static class GloveShell
    {
        /// <summary>How far the leather stands off the skin (the body is 2.6 m: a real glove's 3 mm, a little over).</summary>
        public const float Thickness = 0.0035f;

        /// <summary>The cuff: how far it comes up the forearm past the wrist, its band's width and how much prouder the band stands.</summary>
        public const float CuffLength = 0.085f;
        public const float CuffBand = 0.018f;
        public const float CuffRaise = 0.0018f;

        /// <summary>How far along each finger's first bone (root to first knuckle) the glove ends, and along the thumb's.</summary>
        public const float FingerCover = 0.7f;
        public const float ThumbCover = 0.55f;

        /// <summary>How far under its digits' cuts the palm's points always are (a point of the palm is never cut out).</summary>
        private const float PalmKept = 0.002f;

        private static bool IsPalm(int bone, IReadOnlyDictionary<int, HumanBodyBones> boneOf) =>
            boneOf.TryGetValue(bone, out HumanBodyBones human) && (human == HumanBodyBones.LeftHand || human == HumanBodyBones.RightHand);

        /// <summary>How far the wall round an opening goes in under the skin, so no gap shows between the leather's edge and the skin.</summary>
        public const float WallSunk = 0.0015f;

        /// <summary>The digits' first bones on each hand (the second is the next in HumanBodyBones).</summary>
        private static readonly HumanBodyBones[] LeftDigits =
        {
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftThumbProximal,
        };

        private static readonly HumanBodyBones[] RightDigits =
        {
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightThumbProximal,
        };

        /// <summary>A cut through the body: covered where <c>dot(point - At, Out) &lt; 0</c>.</summary>
        private readonly struct Cut
        {
            public readonly Vector3 At;
            public readonly Vector3 Out;

            public Cut(Vector3 at, Vector3 outward)
            {
                At = at;
                Out = outward.normalized;
            }

            public float Past(Vector3 point) => Vector3.Dot(point - At, Out);
        }

        /// <summary>
        /// The gloves for a body mesh. <paramref name="boneOf"/> names the bone at each of the mesh's bone indices (null where it is none a glove
        /// cares about). The mesh comes back bound to the same bones as the body, in the same order, with the body's bind poses: draw it with
        /// the body skin's own bone list.
        /// </summary>
        public static Mesh Build(Mesh body, IReadOnlyDictionary<int, HumanBodyBones> boneOf)
        {
            Vector3[] vertices = body.vertices;
            Vector3[] normals = body.normals;
            BoneWeight[] weights = body.boneWeights;
            int[] triangles = body.triangles;
            Matrix4x4[] bindposes = body.bindposes;

            var indexOf = new Dictionary<HumanBodyBones, int>();
            foreach (KeyValuePair<int, HumanBodyBones> pair in boneOf)
            {
                indexOf[pair.Value] = pair.Key;
            }
            Vector3 Joint(HumanBodyBones bone) =>
                indexOf.TryGetValue(bone, out int index) ? bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero) : Vector3.zero;

            // Every cut: each forearm's cuff, and each hand's digits' (a digit's cut with the line down the digit it ends).
            var cuffOf = new Dictionary<int, Cut>();
            var digitsOf = new Dictionary<int, List<(Cut cut, Vector3 root, Vector3 tip)>>();
            var cuffs = new List<Cut>();
            foreach (bool left in new[] { true, false })
            {
                HumanBodyBones hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                HumanBodyBones forearm = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                if (!indexOf.TryGetValue(hand, out int handIndex) || !indexOf.TryGetValue(forearm, out int forearmIndex))
                {
                    continue;
                }
                Vector3 wrist = Joint(hand);
                Vector3 up = (Joint(forearm) - wrist).normalized;
                var cuff = new Cut(wrist + up * CuffLength, up);
                cuffs.Add(cuff);
                cuffOf[forearmIndex] = cuff;
                var digits = new List<(Cut, Vector3, Vector3)>();
                digitsOf[handIndex] = digits;
                foreach (HumanBodyBones first in left ? LeftDigits : RightDigits)
                {
                    HumanBodyBones second = first + 1;
                    if (!indexOf.TryGetValue(first, out int firstIndex))
                    {
                        continue;
                    }
                    Vector3 root = Joint(first), knuckle = Joint(second);
                    float share = first == HumanBodyBones.LeftThumbProximal || first == HumanBodyBones.RightThumbProximal ? ThumbCover : FingerCover;
                    digits.Add((new Cut(root + (knuckle - root) * share, knuckle - root), root, knuckle + (knuckle - root)));
                    digitsOf[firstIndex] = digits;
                    if (indexOf.TryGetValue(second, out int secondIndex))
                    {
                        digitsOf[secondIndex] = digits;
                    }
                }
            }

            // How far past its cut each point is (negative: under the glove). On a forearm, past the cuff; on a hand, past the cut of the digit it
            // is nearest (the palm included, so the field runs on smoothly from the palm into each finger and the cut is one clean ring round
            // it, wherever its bones' weights change over).
            var past = new float[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                int bone = Heaviest(weights[i]);
                if (cuffOf.TryGetValue(bone, out Cut cuff))
                {
                    past[i] = cuff.Past(vertices[i]);
                }
                else if (digitsOf.TryGetValue(bone, out List<(Cut cut, Vector3 root, Vector3 tip)> digits))
                {
                    float nearest = float.MaxValue;
                    foreach ((Cut cut, Vector3 root, Vector3 tip) in digits)
                    {
                        float away = DistanceToSegment(vertices[i], root, tip);
                        if (away < nearest)
                        {
                            nearest = away;
                            past[i] = cut.Past(vertices[i]);
                        }
                    }
                    // The palm itself is always under the glove: where the thumb's web meets the index finger's, the thumb's cut reached a few of its
                    // points and opened a hole.
                    if (digitsOf.ContainsKey(bone) && IsPalm(bone, boneOf))
                    {
                        past[i] = Mathf.Min(past[i], -PalmKept);
                    }
                }
                else
                {
                    past[i] = 1.0f;
                }
            }

            var shell = new ShellBuilder(vertices, normals, weights, cuffs);
            var kept = new List<int>();
            var polygon = new List<int>(4);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (past[a] >= 0.0f && past[b] >= 0.0f && past[c] >= 0.0f)
                {
                    continue;
                }
                polygon.Clear();
                int[] corners = { a, b, c };
                for (int k = 0; k < 3; k++)
                {
                    int from = corners[k], to = corners[(k + 1) % 3];
                    if (past[from] < 0.0f)
                    {
                        polygon.Add(shell.Original(from));
                    }
                    if ((past[from] < 0.0f) != (past[to] < 0.0f))
                    {
                        polygon.Add(shell.OnCut(from, to, past[from] / (past[from] - past[to])));
                    }
                }
                for (int k = 1; k + 1 < polygon.Count; k++)
                {
                    kept.Add(polygon[0]);
                    kept.Add(polygon[k]);
                    kept.Add(polygon[k + 1]);
                }
            }
            shell.AddWalls(kept);

            var mesh = new Mesh { name = "FingerlessGloves" };
            if (shell.Vertices.Count > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(shell.Vertices);
            mesh.SetNormals(shell.Normals);
            mesh.boneWeights = shell.Weights.ToArray();
            mesh.bindposes = bindposes;
            mesh.SetTriangles(kept, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab));
            return Vector3.Distance(point, a + ab * t);
        }

        private static int Heaviest(BoneWeight w)
        {
            int bone = w.boneIndex0;
            float most = w.weight0;
            if (w.weight1 > most) { most = w.weight1; bone = w.boneIndex1; }
            if (w.weight2 > most) { most = w.weight2; bone = w.boneIndex2; }
            if (w.weight3 > most) { bone = w.boneIndex3; }
            return bone;
        }

        /// <summary>The gloves' points as they are made: the body's own lifted off the skin, and new ones on the cuts.</summary>
        private sealed class ShellBuilder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<BoneWeight> Weights = new List<BoneWeight>();

            private readonly Vector3[] _vertices;
            private readonly Vector3[] _normals;
            private readonly BoneWeight[] _weights;
            private readonly List<Cut> _cuffs;
            private readonly Dictionary<int, int> _original = new Dictionary<int, int>();
            private readonly Dictionary<long, int> _onCut = new Dictionary<long, int>();

            // Each made point's place on the skin (for the walls).
            private readonly List<Vector3> _onSkin = new List<Vector3>();

            public ShellBuilder(Vector3[] vertices, Vector3[] normals, BoneWeight[] weights, List<Cut> cuffs)
            {
                _vertices = vertices;
                _normals = normals;
                _weights = weights;
                _cuffs = cuffs;
            }

            public int Original(int i)
            {
                if (!_original.TryGetValue(i, out int made))
                {
                    made = Add(_vertices[i], _normals[i], _weights[i]);
                    _original[i] = made;
                }
                return made;
            }

            /// <summary>The point <paramref name="t"/> of the way from <paramref name="from"/> to <paramref name="to"/>, shared by both triangles on that side.</summary>
            public int OnCut(int from, int to, float t)
            {
                long key = from < to ? ((long)from << 32) | (uint)to : ((long)to << 32) | (uint)from;
                if (!_onCut.TryGetValue(key, out int made))
                {
                    Vector3 point = Vector3.Lerp(_vertices[from], _vertices[to], t);
                    Vector3 normal = Vector3.Lerp(_normals[from], _normals[to], t).normalized;
                    made = Add(point, normal, Blend(_weights[from], _weights[to], t));
                    _onCut[key] = made;
                }
                return made;
            }

            private int Add(Vector3 onSkin, Vector3 normal, BoneWeight weight)
            {
                Vertices.Add(onSkin + normal * (Thickness + CuffRaiseAt(onSkin)));
                Normals.Add(normal);
                Weights.Add(weight);
                _onSkin.Add(onSkin);
                return Vertices.Count - 1;
            }

            /// <summary>The cuff's band stands a little prouder: the last <see cref="CuffBand"/> below its edge.</summary>
            private float CuffRaiseAt(Vector3 point)
            {
                float raise = 0.0f;
                foreach (Cut cuff in _cuffs)
                {
                    float below = -cuff.Past(point);
                    if (below > -0.001f && below < CuffBand + 0.004f)
                    {
                        raise = Mathf.Max(raise, CuffRaise * Mathf.Clamp01((CuffBand + 0.004f - below) / 0.004f));
                    }
                }
                return raise;
            }

            /// <summary>
            /// A wall down from every open edge of <paramref name="triangles"/> (an edge with one triangle, the same points counted by place:
            /// the body has two points at its texture's seams) to under the skin, its points facing out of the opening: a hard edge.
            /// </summary>
            public void AddWalls(List<int> triangles)
            {
                var count = new Dictionary<(Vector3Int, Vector3Int), int>();
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        (Vector3Int, Vector3Int) key = EdgeKey(triangles[t + k], triangles[t + (k + 1) % 3]);
                        count[key] = count.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                }
                int existing = triangles.Count;
                for (int t = 0; t + 2 < existing; t += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int a = triangles[t + k], b = triangles[t + (k + 1) % 3], c = triangles[t + (k + 2) % 3];
                        if (count[EdgeKey(a, b)] != 1)
                        {
                            continue;
                        }
                        // Out of the opening: across the edge, away from the triangle's third point, along the skin.
                        Vector3 along = (_onSkin[b] - _onSkin[a]).normalized;
                        Vector3 normal = (Normals[a] + Normals[b]).normalized;
                        Vector3 outward = Vector3.Cross(along, normal).normalized;
                        if (Vector3.Dot(outward, _onSkin[c] - _onSkin[a]) > 0.0f)
                        {
                            outward = -outward;
                        }
                        int topA = Wall(Vertices[a], outward, Weights[a]);
                        int topB = Wall(Vertices[b], outward, Weights[b]);
                        int footA = Wall(_onSkin[a] - Normals[a] * WallSunk, outward, Weights[a]);
                        int footB = Wall(_onSkin[b] - Normals[b] * WallSunk, outward, Weights[b]);
                        // The same turn as the triangle's edge a -> b, which goes round the glove's outside: the wall then faces out of the opening.
                        triangles.Add(topB); triangles.Add(topA); triangles.Add(footA);
                        triangles.Add(topB); triangles.Add(footA); triangles.Add(footB);
                    }
                }
            }

            private int Wall(Vector3 point, Vector3 normal, BoneWeight weight)
            {
                Vertices.Add(point);
                Normals.Add(normal);
                Weights.Add(weight);
                _onSkin.Add(point);
                return Vertices.Count - 1;
            }

            private (Vector3Int, Vector3Int) EdgeKey(int a, int b)
            {
                Vector3Int p = Vector3Int.RoundToInt(_onSkin[a] * 100000.0f), q = Vector3Int.RoundToInt(_onSkin[b] * 100000.0f);
                bool pFirst = p.x < q.x || (p.x == q.x && (p.y < q.y || (p.y == q.y && p.z <= q.z)));
                return pFirst ? (p, q) : (q, p);
            }
        }

        /// <summary>Two points' bone weights mixed <paramref name="t"/> of the way, kept to the four heaviest.</summary>
        private static BoneWeight Blend(BoneWeight a, BoneWeight b, float t)
        {
            var sum = new Dictionary<int, float>();
            void Put(int bone, float weight)
            {
                if (weight > 0.0f)
                {
                    sum[bone] = (sum.TryGetValue(bone, out float w) ? w : 0.0f) + weight;
                }
            }
            Put(a.boneIndex0, a.weight0 * (1.0f - t)); Put(a.boneIndex1, a.weight1 * (1.0f - t));
            Put(a.boneIndex2, a.weight2 * (1.0f - t)); Put(a.boneIndex3, a.weight3 * (1.0f - t));
            Put(b.boneIndex0, b.weight0 * t); Put(b.boneIndex1, b.weight1 * t);
            Put(b.boneIndex2, b.weight2 * t); Put(b.boneIndex3, b.weight3 * t);
            var heaviest = new List<KeyValuePair<int, float>>(sum);
            heaviest.Sort((x, y) => y.Value.CompareTo(x.Value));
            float total = 0.0f;
            for (int i = 0; i < heaviest.Count && i < 4; i++)
            {
                total += heaviest[i].Value;
            }
            var blended = new BoneWeight();
            for (int i = 0; i < heaviest.Count && i < 4; i++)
            {
                float w = heaviest[i].Value / total;
                switch (i)
                {
                    case 0: blended.boneIndex0 = heaviest[i].Key; blended.weight0 = w; break;
                    case 1: blended.boneIndex1 = heaviest[i].Key; blended.weight1 = w; break;
                    case 2: blended.boneIndex2 = heaviest[i].Key; blended.weight2 = w; break;
                    default: blended.boneIndex3 = heaviest[i].Key; blended.weight3 = w; break;
                }
            }
            return blended;
        }
    }
}

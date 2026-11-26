using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The cape as a skinned mesh (docs/CAPE.md), bent by the graphics card as the body and its clothes are: the yoke and the collar follow the
    /// body's own bones (each point the bones and weights of the body's point nearest it), and the hanging part a short chain of
    /// <see cref="ChainBones"/> bones down its middle from the yoke's back edge, hung from the body's Spine, which CapeSwing swings. The first
    /// rows of the hanging part follow the yoke's back edge as they leave it, so the two never come apart. Two faces a point: the outside, and
    /// the inside facing the body. Pure, so it is tested without a scene.
    /// </summary>
    public static class CapeSkin
    {
        public const int ChainBones = 4;

        /// <summary>How far down the hanging part (a share of it) its rows still follow the yoke's back edge, fading to the chain alone.</summary>
        public const float FollowsYoke = 0.15f;

        /// <summary>Where each chain bone sits at rest (body space): down the middle of the hanging part, the first on the yoke's back edge.</summary>
        public static Vector3[] ChainPoints(Vector3[] rest)
        {
            var points = new Vector3[ChainBones];
            for (int k = 0; k < ChainBones; k++)
            {
                points[k] = rest[CapeShape.GridIndex(CapeShape.Columns / 2, CapeShape.YokeRows - 1 + k * CapeShape.HangingRows / ChainBones)];
            }
            return points;
        }

        /// <summary>How far down the hanging part a point is (0 the yoke's back edge, 1 the hem); below 0 for the yoke and the collar.</summary>
        public static float Down(int point)
        {
            if (point >= CapeShape.GridPoints)
            {
                return -1.0f;
            }
            return (point / CapeShape.Columns - (CapeShape.YokeRows - 1)) / (float)CapeShape.HangingRows;
        }

        /// <summary>
        /// Each point's bones and weights (indices into the body's bones, then <paramref name="firstChainBone"/> on for the chain):
        /// <paramref name="onBody"/> for the yoke and the collar (the body's nearest point's), the chain for the hanging part, the two blended
        /// where it leaves the yoke. Four at most, adding to one.
        /// </summary>
        public static BoneWeight[] Weights(BoneWeight[] onBody, int firstChainBone)
        {
            var weights = new BoneWeight[CapeShape.Points];
            for (int i = 0; i < CapeShape.Points; i++)
            {
                float t = Down(i);
                if (t <= 0.0f)
                {
                    weights[i] = onBody[i];
                    continue;
                }
                var parts = new Dictionary<int, float>();
                // Along the chain: between the two bones it lies between.
                float along = t * ChainBones;
                int k = Mathf.Min(Mathf.FloorToInt(along), ChainBones - 1);
                float f = Mathf.Clamp01(along - k);
                float onYoke = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, t / FollowsYoke);
                Add(parts, firstChainBone + k, (1.0f - f) * (1.0f - onYoke));
                if (k + 1 < ChainBones) Add(parts, firstChainBone + k + 1, f * (1.0f - onYoke));
                else Add(parts, firstChainBone + k, f * (1.0f - onYoke));
                // Near the yoke: with its back edge above it, as the body moves that.
                BoneWeight edge = onBody[CapeShape.GridIndex(i % CapeShape.Columns, CapeShape.YokeRows - 1)];
                Add(parts, edge.boneIndex0, edge.weight0 * onYoke);
                Add(parts, edge.boneIndex1, edge.weight1 * onYoke);
                Add(parts, edge.boneIndex2, edge.weight2 * onYoke);
                Add(parts, edge.boneIndex3, edge.weight3 * onYoke);
                weights[i] = Strongest(parts);
            }
            return weights;
        }

        private static void Add(Dictionary<int, float> parts, int bone, float weight)
        {
            if (weight <= 0.0f) return;
            parts.TryGetValue(bone, out float was);
            parts[bone] = was + weight;
        }

        /// <summary>The four strongest, made to add to one.</summary>
        private static BoneWeight Strongest(Dictionary<int, float> parts)
        {
            var sorted = new List<KeyValuePair<int, float>>(parts);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            float total = 0.0f;
            for (int i = 0; i < Mathf.Min(4, sorted.Count); i++) total += sorted[i].Value;
            var w = new BoneWeight();
            for (int i = 0; i < Mathf.Min(4, sorted.Count); i++)
            {
                float share = sorted[i].Value / total;
                switch (i)
                {
                    case 0: w.boneIndex0 = sorted[i].Key; w.weight0 = share; break;
                    case 1: w.boneIndex1 = sorted[i].Key; w.weight1 = share; break;
                    case 2: w.boneIndex2 = sorted[i].Key; w.weight2 = share; break;
                    default: w.boneIndex3 = sorted[i].Key; w.weight3 = share; break;
                }
            }
            return w;
        }

        /// <summary>
        /// Each chain bone's place in its parent's space at rest (the first's in the anchor bone's, each next one's in the one before's), turned
        /// as the anchor is: <paramref name="anchorBind"/> is the anchor's bind pose (body space to its own).
        /// </summary>
        public static Vector3[] ChainLocalPositions(Vector3[] chainPoints, Matrix4x4 anchorBind)
        {
            var local = new Vector3[chainPoints.Length];
            Vector3 parent = Vector3.zero;
            for (int k = 0; k < chainPoints.Length; k++)
            {
                Vector3 inAnchor = anchorBind.MultiplyPoint3x4(chainPoints[k]);
                local[k] = inAnchor - parent;
                parent = inAnchor;
            }
            return local;
        }

        /// <summary>Each chain bone's bind pose: the anchor's, moved to the bone (the bones lie turned as the anchor does at rest).</summary>
        public static Matrix4x4[] ChainBindposes(Vector3[] chainPoints, Matrix4x4 anchorBind)
        {
            var binds = new Matrix4x4[chainPoints.Length];
            for (int k = 0; k < chainPoints.Length; k++)
            {
                binds[k] = Matrix4x4.Translate(-anchorBind.MultiplyPoint3x4(chainPoints[k])) * anchorBind;
            }
            return binds;
        }

        /// <summary>
        /// The mesh: every point twice (its outside, then its inside facing the body), the faces of both, the bones and weights
        /// (<see cref="Weights"/>) and the bind poses (the body's, then the chain's).
        /// </summary>
        public static Mesh Build(Vector3[] rest, BoneWeight[] weights, Matrix4x4[] bodyBindposes, Matrix4x4[] chainBindposes)
        {
            var vertices = new Vector3[CapeShape.Points * 2];
            var doubled = new BoneWeight[CapeShape.Points * 2];
            for (int i = 0; i < CapeShape.Points; i++)
            {
                vertices[i] = vertices[i + CapeShape.Points] = rest[i];
                doubled[i] = doubled[i + CapeShape.Points] = weights[i];
            }
            var binds = new Matrix4x4[bodyBindposes.Length + chainBindposes.Length];
            bodyBindposes.CopyTo(binds, 0);
            chainBindposes.CopyTo(binds, bodyBindposes.Length);
            var mesh = new Mesh { name = "Cape" };
            mesh.vertices = vertices;
            mesh.SetTriangles(Triangles(), 0);
            mesh.boneWeights = doubled;
            mesh.bindposes = binds;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Every face, outside and inside: the points' first copy faces away from the body, the second (<see cref="CapeShape.Points"/> on) towards
        /// it, so each side is lit from its own side.
        /// </summary>
        public static int[] Triangles()
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
    }
}

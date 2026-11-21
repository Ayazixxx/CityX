using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A long coat's skirt kept outside the legs (owner, 2026-11-21: "when swinging with pretty much any melee weapon the knee clips through the
    /// jacket", "do what you can with minimal or preferably no alteration to the animations"). The skirt bones follow a thigh's swing (GarmentHelperPose),
    /// but in a swing the leg steps out wide or comes up to the waist, and the shin bends back below a hem that hangs past the knee: the thigh went
    /// 12 cm into the coat. Here a side's two skirt bones, as posed, are turned about the hip joint, and where turning cannot reach (near the joint)
    /// shifted out, just far enough that the coat's points, skinned as they are drawn, lie outside both legs' skin. What games call bone-based cloth
    /// collision: a few bones pushed out of capsules round the legs, rather than simulating the cloth itself. Pure: SkirtCloth gives it the legs and
    /// the bones as they stand this frame.
    /// </summary>
    public static class SkirtClearance
    {
        /// <summary>A point counts as inside a leg past this (metres): a millimetre's touch is not worth a turn.</summary>
        public const float Tolerance = 0.001f;

        /// <summary>A bone carrying at least this much of the point being pushed turns with it.</summary>
        public const float Carry = 0.1f;

        /// <summary>The grid a side's tested points are spread on (bind pose, metres): finer than a shin is wide, so no part of it goes untested.</summary>
        public const float Cell = 0.05f;

        /// <summary>One piece of a leg, its skin a tapered capsule: the axis between two joints and the radius at either end.</summary>
        public struct Limb
        {
            public Vector3 From, To;
            public float RadiusFrom, RadiusTo;

            /// <summary>The box round the limb's skin: a point outside it, grown by the clearance, is outside the limb.</summary>
            public Vector3 Low, High;

            public Limb(Vector3 from, Vector3 to, float radiusFrom, float radiusTo)
            {
                From = from;
                To = to;
                RadiusFrom = radiusFrom;
                RadiusTo = radiusTo;
                Vector3 reach = Vector3.one * Mathf.Max(radiusFrom, radiusTo);
                Low = Vector3.Min(from, to) - reach;
                High = Vector3.Max(from, to) + reach;
            }

            /// <summary>Whether a point is near enough the limb to be worth measuring.</summary>
            public bool Near(Vector3 point, float clearance) =>
                point.x > Low.x - clearance && point.x < High.x + clearance && point.y > Low.y - clearance && point.y < High.y + clearance &&
                point.z > Low.z - clearance && point.z < High.z + clearance;

            /// <summary>How far a point is inside the skin grown by <paramref name="clearance"/> (negative: outside), and the way out.</summary>
            public float Inside(Vector3 point, float clearance, out Vector3 outward)
            {
                Vector3 along = To - From;
                float t = Mathf.Clamp01(Vector3.Dot(point - From, along) / Mathf.Max(along.sqrMagnitude, 1e-8f));
                Vector3 off = point - (From + along * t);
                float distance = off.magnitude;
                outward = distance > 1e-6f ? off / distance : Vector3.Cross(along, Vector3.up).normalized;
                return Mathf.Lerp(RadiusFrom, RadiusTo, t) + clearance - distance;
            }
        }

        /// <summary>
        /// The points of one side of a coat's skirt (a thigh's front and back skirt bones), skinned as the garment is: each point's share on the side's
        /// two bones and where it sits in their bind frames, and its other bones (the hips, the other leg's), whose part is worked out once a push
        /// begins (<see cref="Prepare"/>): only the side's own two bones move while it is pushed.
        /// </summary>
        public sealed class Panel
        {
            public int Front, Back;
            public int Count;
            public float[] OnFront, OnBack;
            public Vector3[] FrontLocal, BackLocal;
            public int[] OtherBones;
            public float[] OtherWeights;
            public Vector3[] OtherLocals;
            private Vector3[] _fixed;

            /// <summary>
            /// The garment's points at least <paramref name="onPanel"/> on the two bones and at least <paramref name="fromJoint"/> from their joint
            /// (bind pose), one a cell of an even grid (<see cref="Cell"/>), at most <paramref name="most"/>.
            /// </summary>
            public static Panel Of(IReadOnlyList<Vector3> vertices, IReadOnlyList<BoneWeight> weights, IReadOnlyList<Matrix4x4> bindposes, int front,
                int back, float onPanel, float fromJoint, int most)
            {
                var chosen = new List<int>();
                var cells = new HashSet<Vector3Int>();
                for (int v = 0; v < vertices.Count && v < weights.Count; v++)
                {
                    BoneWeight w = weights[v];
                    if (On(w, front) + On(w, back) >= onPanel && bindposes[front].MultiplyPoint3x4(vertices[v]).magnitude >= fromJoint &&
                        cells.Add(Vector3Int.FloorToInt(vertices[v] / Cell)))
                    {
                        chosen.Add(v);
                    }
                }
                int count = Mathf.Min(chosen.Count, most);
                var panel = new Panel
                {
                    Front = front, Back = back, Count = count,
                    OnFront = new float[count], OnBack = new float[count], FrontLocal = new Vector3[count], BackLocal = new Vector3[count],
                    OtherBones = new int[count * 4], OtherWeights = new float[count * 4], OtherLocals = new Vector3[count * 4],
                    _fixed = new Vector3[count],
                };
                for (int p = 0; p < count; p++)
                {
                    int v = chosen[p * chosen.Count / count];
                    BoneWeight w = weights[v];
                    int[] bones = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                    float[] shares = { w.weight0, w.weight1, w.weight2, w.weight3 };
                    panel.OnFront[p] = On(w, front);
                    panel.OnBack[p] = On(w, back);
                    panel.FrontLocal[p] = bindposes[front].MultiplyPoint3x4(vertices[v]);
                    panel.BackLocal[p] = bindposes[back].MultiplyPoint3x4(vertices[v]);
                    for (int k = 0; k < 4; k++)
                    {
                        bool other = bones[k] != front && bones[k] != back && bones[k] < bindposes.Count;
                        panel.OtherBones[p * 4 + k] = other ? bones[k] : 0;
                        panel.OtherWeights[p * 4 + k] = other ? shares[k] : 0.0f;
                        panel.OtherLocals[p * 4 + k] = other ? bindposes[bones[k]].MultiplyPoint3x4(vertices[v]) : Vector3.zero;
                    }
                }
                return panel;
            }

            /// <summary>Each point's part from bones other than the side's two, as they stand in <paramref name="bones"/>.</summary>
            public void Prepare(Matrix4x4[] bones)
            {
                for (int p = 0; p < Count; p++)
                {
                    Vector3 at = Vector3.zero;
                    for (int k = p * 4; k < p * 4 + 4; k++)
                    {
                        if (OtherWeights[k] > 0.0f)
                        {
                            at += OtherWeights[k] * bones[OtherBones[k]].MultiplyPoint3x4(OtherLocals[k]);
                        }
                    }
                    _fixed[p] = at;
                }
            }

            /// <summary>Where a point is drawn with the side's two bones at these matrices (after <see cref="Prepare"/>).</summary>
            public Vector3 At(int point, Matrix4x4 front, Matrix4x4 back) =>
                _fixed[point] + OnFront[point] * front.MultiplyPoint3x4(FrontLocal[point]) + OnBack[point] * back.MultiplyPoint3x4(BackLocal[point]);

            private static float On(BoneWeight weight, int bone) =>
                (weight.boneIndex0 == bone ? weight.weight0 : 0.0f) + (weight.boneIndex1 == bone ? weight.weight1 : 0.0f) +
                (weight.boneIndex2 == bone ? weight.weight2 : 0.0f) + (weight.boneIndex3 == bone ? weight.weight3 : 0.0f);
        }

        /// <summary>A side's two skirt bones as set this frame: their turns, and how far both are shifted off the hip joint.</summary>
        public struct Pose
        {
            public Quaternion Front, Back;
            public Vector3 Shift;
        }

        /// <summary>The panel's deepest point inside a leg now (below <see cref="Tolerance"/>: none), the way out of it, and the point's index.</summary>
        public static float Deepest(Panel panel, Matrix4x4[] bones, IReadOnlyList<Limb> limbs, float clearance, out Vector3 at, out Vector3 outward,
            out int point)
        {
            panel.Prepare(bones);
            return Deepest(panel, bones[panel.Front], bones[panel.Back], limbs, clearance, out at, out outward, out point);
        }

        private static float Deepest(Panel panel, Matrix4x4 front, Matrix4x4 back, IReadOnlyList<Limb> limbs, float clearance, out Vector3 at,
            out Vector3 outward, out int point)
        {
            float deepest = Tolerance;
            at = outward = Vector3.zero;
            point = -1;
            for (int p = 0; p < panel.Count; p++)
            {
                Vector3 world = panel.At(p, front, back);
                for (int l = 0; l < limbs.Count; l++)
                {
                    if (!limbs[l].Near(world, clearance))
                    {
                        continue;
                    }
                    float inside = limbs[l].Inside(world, clearance, out Vector3 way);
                    if (inside > deepest)
                    {
                        deepest = inside;
                        at = world;
                        outward = way;
                        point = p;
                    }
                }
            }
            return point >= 0 ? deepest : 0.0f;
        }

        /// <summary>
        /// The side's pose moved off the legs, a pass at a time, each on the deepest point: turned about the joint where that moves it out (the
        /// bones carrying it turn), shifted out where it cannot (a point near the joint, or one pushed straight away from it), the shift at most
        /// <paramref name="maxShift"/>. <paramref name="bones"/> holds every bone's world matrix this frame; the side's own two are set here.
        /// <paramref name="moved"/>: whether anything was inside a leg to push.
        /// </summary>
        public static Pose Clear(Vector3 joint, Pose pose, float scale, Panel panel, Matrix4x4[] bones, IReadOnlyList<Limb> limbs, float clearance,
            float turnReach, float maxShift, int passes, out bool moved)
        {
            panel.Prepare(bones);
            moved = false;
            Vector3 size = Vector3.one * scale;
            for (int pass = 0; pass < passes; pass++)
            {
                Vector3 pivot = joint + pose.Shift;
                float depth = Deepest(panel, Matrix4x4.TRS(pivot, pose.Front, size), Matrix4x4.TRS(pivot, pose.Back, size), limbs, clearance,
                    out Vector3 at, out Vector3 outward, out int point);
                if (point < 0)
                {
                    break;
                }
                moved = true;
                float onFront = panel.OnFront[point], onBack = panel.OnBack[point];
                Vector3 radius = at - pivot;
                bool turns = radius.magnitude >= turnReach * scale && Vector3.Cross(radius.normalized, outward).magnitude > 0.5f;
                if (turns)
                {
                    Quaternion delta = Quaternion.FromToRotation(radius, at + outward * depth - pivot);
                    if (onFront >= Carry) pose.Front = delta * pose.Front;
                    if (onBack >= Carry) pose.Back = delta * pose.Back;
                }
                else
                {
                    pose.Shift = Vector3.ClampMagnitude(pose.Shift + outward * depth / Mathf.Max(onFront + onBack, 0.25f), maxShift * scale);
                }
            }
            Vector3 settled = joint + pose.Shift;
            bones[panel.Front] = Matrix4x4.TRS(settled, pose.Front, size);
            bones[panel.Back] = Matrix4x4.TRS(settled, pose.Back, size);
            return pose;
        }
    }
}

using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A coat's skirt as cloth, on its four skirt bones (GarmentHelperBones): kept out of the legs (SkirtClearance; owner, 2026-11-21: "when
    /// swinging with pretty much any melee weapon the knee clips through the jacket"), and swinging a moment behind them. Nothing of the body's
    /// own pose changes ("do what you can with minimal or preferably no alteration to the animations"): only where the coat hangs.
    /// </summary>
    public sealed class SkirtCloth
    {
        /// <summary>A garment point at least this much on a side's two skirt bones is kept out of the legs with them.</summary>
        private const float PanelWeight = 0.2f;

        /// <summary>Points this near their hip joint (bind pose, metres) are the coat's waist, held on the hips, and are left alone.</summary>
        private const float FromJoint = 0.1f;

        /// <summary>The most points tested on a side: its points on a 5 cm grid (SkirtClearance.Cell) come to about 500 on either coat.</summary>
        private const int PanelPoints = 1000;

        /// <summary>How far outside a leg's skin the coat is kept: the lining just off it, so it reads as lying on the leg, never in it.</summary>
        private const float LegClearance = 0.015f;

        /// <summary>Pushes a side may take to clear the legs in each of a frame's rounds.</summary>
        private const int Passes = 8;

        /// <summary>Rounds a frame, the two sides in turn.</summary>
        private const int Rounds = 4;

        /// <summary>A point at least this far from the hip joint is turned off a leg about it; a nearer one is shifted.</summary>
        private const float TurnReach = 0.2f;

        /// <summary>The furthest a side is shifted off its hip joint: a thigh raised out to the side lifts the coat's side, as cloth over it would.</summary>
        private const float MaxShift = 0.2f;

        /// <summary>The thigh is a limb from this far down it: above, it meets the hips under the coat's waist.</summary>
        private const float ThighFrom = 0.2f;

        /// <summary>
        /// How long (seconds) a side takes to come most of the way to where the leg puts it: the hem swings a moment behind the leg, as cloth with
        /// some weight does, and a push off a knee eases back rather than snapping.
        /// </summary>
        private const float Lag = 0.06f;

        /// <summary>A side this far from where the leg puts it (a teleport, a ragdoll getting up) goes there at once.</summary>
        private const float SnapDegrees = 60.0f;

        private readonly SkirtClearance.Panel[] _panels = new SkirtClearance.Panel[2];
        private readonly Quaternion[] _trail;
        private readonly bool[] _trailing;
        private readonly Vector3[] _shift = new Vector3[2];
        private readonly Vector3[] _joints = new Vector3[2];
        private readonly SkirtClearance.Pose[] _poses = new SkirtClearance.Pose[2];
        private readonly Transform[] _legs;
        private readonly Transform[] _bodyBones;
        private readonly Matrix4x4[] _bones;
        private readonly List<SkirtClearance.Limb> _limbs = new List<SkirtClearance.Limb>(4);

        /// <summary>The skirt of <paramref name="worn"/> (a readable mesh skinned to the body's bones and the helpers after them) on this body.</summary>
        public SkirtCloth(Animator body, Matrix4x4[] bindposes, Mesh worn, int helperCount)
        {
            _trail = new Quaternion[helperCount];
            _trailing = new bool[helperCount];
            _bones = new Matrix4x4[SkinnedGarment.Bones.Length + helperCount];
            _bodyBones = new Transform[SkinnedGarment.Bones.Length];
            for (int b = 0; b < _bodyBones.Length; b++)
            {
                _bodyBones[b] = body.GetBoneTransform(SkinnedGarment.Bones[b]);
            }
            _legs = new[]
            {
                body.GetBoneTransform(HumanBodyBones.LeftUpperLeg), body.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                body.GetBoneTransform(HumanBodyBones.LeftFoot), body.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                body.GetBoneTransform(HumanBodyBones.RightLowerLeg), body.GetBoneTransform(HumanBodyBones.RightFoot),
            };
            Vector3[] vertices = worn.vertices;
            BoneWeight[] weights = worn.boneWeights;
            for (int side = 0; side < 2; side++)
            {
                int front = SkinnedGarment.Bones.Length + side * 2;
                _panels[side] = SkirtClearance.Panel.Of(vertices, weights, bindposes, front, front + 1, PanelWeight, FromJoint, PanelPoints);
            }
        }

        /// <summary>Whether the worn mesh has anything on the skirt bones to keep out of the legs.</summary>
        public bool HasSkirt => _panels[0].Count + _panels[1].Count > 0;

        /// <summary>
        /// The skirt bones' rotations and places this frame (helper indices 0 to 3: left front, left back, right front, right back), from where
        /// the legs put them: eased behind them in the hips' frame (so the body turning does not drag the coat), then pushed off the legs; the push
        /// stays in what they ease from, so the cloth comes back off a knee rather than snapping.
        /// </summary>
        public void Settle(Quaternion[] rotations, Vector3[] places, Transform[] helpers, Transform hips, float scale, float dt)
        {
            PoseLegs(scale);
            for (int b = 0; b < _bodyBones.Length; b++)
            {
                _bones[b] = _bodyBones[b] != null ? _bodyBones[b].localToWorldMatrix : Matrix4x4.identity;
            }
            for (int h = 0; h < helpers.Length; h++)
            {
                _bones[SkinnedGarment.Bones.Length + h] = Matrix4x4.TRS(places[h], rotations[h], Vector3.one * scale);
            }
            float ease = dt > 0.0f ? 1.0f - Mathf.Exp(-dt / Lag) : 0.0f;
            for (int side = 0; side < 2; side++)
            {
                int front = side * 2, back = front + 1;
                _joints[side] = places[front];
                _poses[side] = new SkirtClearance.Pose
                {
                    Front = Trail(front, rotations[front], hips, ease),
                    Back = Trail(back, rotations[back], hips, ease),
                    Shift = hips.rotation * (_shift[side] * (1.0f - ease)),
                };
                _bones[SkinnedGarment.Bones.Length + front] = Matrix4x4.TRS(_joints[side] + _poses[side].Shift, _poses[side].Front, Vector3.one * scale);
                _bones[SkinnedGarment.Bones.Length + back] = Matrix4x4.TRS(_joints[side] + _poses[side].Shift, _poses[side].Back, Vector3.one * scale);
            }
            // The two sides in turn, a few rounds: the back's middle hangs from both legs' back bones, and one side's push must not shove it into
            // the other leg unseen.
            for (int round = 0; round < Rounds; round++)
            {
                bool pushed = false;
                for (int side = 0; side < 2; side++)
                {
                    if (_panels[side].Count > 0)
                    {
                        _poses[side] = SkirtClearance.Clear(_joints[side], _poses[side], scale, _panels[side], _bones, _limbs, LegClearance * scale,
                            TurnReach, MaxShift, Passes, out bool moved);
                        pushed |= moved;
                    }
                }
                if (!pushed)
                {
                    break;   // both sides clear of the legs: nothing more to do this frame
                }
            }
            for (int side = 0; side < 2; side++)
            {
                int front = side * 2, back = front + 1;
                rotations[front] = _poses[side].Front;
                rotations[back] = _poses[side].Back;
                places[front] = places[back] = _joints[side] + _poses[side].Shift;
                _trail[front] = Quaternion.Inverse(hips.rotation) * _poses[side].Front;
                _trail[back] = Quaternion.Inverse(hips.rotation) * _poses[side].Back;
                _shift[side] = Quaternion.Inverse(hips.rotation) * _poses[side].Shift;
            }
        }

        private Quaternion Trail(int helper, Quaternion target, Transform hips, float ease)
        {
            Quaternion local = Quaternion.Inverse(hips.rotation) * target;
            if (!_trailing[helper] || Quaternion.Angle(_trail[helper], local) > SnapDegrees)
            {
                _trail[helper] = local;
                _trailing[helper] = true;
            }
            else
            {
                _trail[helper] = Quaternion.Slerp(_trail[helper], local, ease);
            }
            return hips.rotation * _trail[helper];
        }

        /// <summary>Both legs as they stand now: each thigh from a little below its hip joint to the knee, each shin from the knee to the ankle.</summary>
        private void PoseLegs(float scale)
        {
            _limbs.Clear();
            float knee = (FusedBodyShape.LegRadius + FusedBodyShape.AnkleRadius) * 0.5f * scale;
            for (int leg = 0; leg + 2 < _legs.Length; leg += 3)
            {
                Transform hip = _legs[leg], shin = _legs[leg + 1], foot = _legs[leg + 2];
                if (hip == null || shin == null || foot == null)
                {
                    continue;
                }
                _limbs.Add(new SkirtClearance.Limb(Vector3.Lerp(hip.position, shin.position, ThighFrom), shin.position,
                    Mathf.Lerp(FusedBodyShape.LegRadius * scale, knee, ThighFrom), knee));
                _limbs.Add(new SkirtClearance.Limb(shin.position, foot.position, knee, FusedBodyShape.AnkleRadius * scale));
            }
        }
    }
}

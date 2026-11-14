using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A BAKED POSE: a move animated in Blender (BakedMotion). Played after the Animator like every pose here, and over all of them but
    /// the wrists: each bone set to the frame's own local rotation, the hips moved, the whole pose turned about a pivot if asked. The
    /// caller samples the motion and eases the weight in and out (OctaslashAbility).
    ///
    /// A motion with no arm bones (BakedPose.LeavesTheArms: a sword attack) is laid on EARLIER, before the sword arm is solved, so the arm is
    /// solved from the shoulders the clip has turned; every arm pose runs as if no baked pose were on the body.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private BakedPose _bakedPose;

        private float _bakedPoseWeight;

        private Transform[] _bakedBones;

        private bool[] _bakedLowerBody;

        /// <summary>Which of the baked pose's bones is the spine (-1: none).</summary>
        private int _bakedSpineIndex = -1;

        /// <summary>
        /// How much of a sword attack's hip turn the pelvis takes on a body that is running or walking under it, 0 to 1. The legs are the
        /// run's: a pelvis turned the whole way (up to 48 degrees) over legs running straight on twisted the body at the groin. The spine
        /// takes the rest, so the shoulders are turned as far as the clip has them either way.
        /// </summary>
        public const float HipsTurnOnTheMove = 0.4f;

        private bool _bakedHasFingers;

        private System.Collections.Generic.IReadOnlyList<string> _bakedBonesFor;

        private readonly System.Collections.Generic.Dictionary<string, Transform> _bakedProps = new System.Collections.Generic.Dictionary<string, Transform>();

        private float _bakedFingerWeight;

        private ISwordBlade _swordHeldForThrust;

        /// <summary>How much of the body a baked pose holds, 0 to 1.</summary>
        public float BakedPoseWeight => _bakedPoseWeight;

        /// <summary>
        /// True while the baked pose leaves the hips and legs to the Animator, wholly or in part (a gun reload; a sword attack on a body
        /// that is walking): the foot IK stays on, and what the pose still has of the legs is laid over it.
        /// </summary>
        public bool BakedPoseIsUpperBodyOnly => _bakedPose != null && (_bakedPose.UpperBodyOnly || _bakedPose.LegsWeight < 1.0f);

        /// <summary>How much of the baked pose the hips and legs take, 0 to 1.</summary>
        private float BakedLegsWeight => _bakedPose == null || _bakedPose.UpperBodyOnly ? 0.0f : Mathf.Clamp01(_bakedPose.LegsWeight);

        /// <summary>True while the baked pose has no arm bones (a sword attack's clip): the arms are posed as they are without it.</summary>
        public bool BakedPoseLeavesTheArms => _bakedPose != null && _bakedPose.LeavesTheArms;

        /// <summary>How much a baked pose holds the ARMS, 0 to 1: what the arm poses give way to.</summary>
        private float BakedArmsWeight => BakedPoseLeavesTheArms ? 0.0f : _bakedPoseWeight;

        /// <summary>How much a baked pose that leaves the arms holds the trunk, 0 to 1: a sword pose's own torso turn gives way to it.</summary>
        private float BakedTrunkOnlyWeight => BakedPoseLeavesTheArms ? _bakedPoseWeight : 0.0f;

        /// <summary>
        /// Puts <paramref name="pose"/> on the body from this frame's LateUpdate on, at <paramref name="weight"/> (1: the pose exactly; between:
        /// blended with whatever the Animator and the poses before it made). Set again every frame it changes; a null pose or a weight of 0 lets go.
        /// </summary>
        public void SetBakedPose(BakedPose pose, float weight)
        {
            _bakedPoseWeight = pose != null ? Mathf.Clamp01(weight) : 0.0f;
            _bakedPose = _bakedPoseWeight > 0.0f ? pose : null;
        }

        /// <summary>Whether the baked pose on the body is <paramref name="pose"/>: the slot is one, and whoever set it last has it.</summary>
        public bool HoldsBakedPose(BakedPose pose) => pose != null && ReferenceEquals(_bakedPose, pose);

        private float _clipStride = 1.0f;

        /// <summary>
        /// How long a clip's stride really is, as a share of the clip's: a punch's or a sword attack's clip plants its feet on ground the body
        /// is stepped over, and a body stopped short has gone less far (FistPoseDriver, SwordPoseDriver). 1 leaves the clip's legs as made.
        /// </summary>
        public void SetClipStride(float share)
        {
            _clipStride = share;
        }

        /// <summary>The stride the legs are being drawn to (for tests).</summary>
        public float ClipStride => _clipStride;

        /// <summary>The body must have been meant to go this far, metres, before how far it went says anything about the stride.</summary>
        private const float LeastStepToMeasure = 0.03f;

        /// <summary>A body still carrying some pace into a clip goes further than its stride: the feet reach no further than this share of it.</summary>
        private const float LongestStride = 1.25f;

        /// <summary>
        /// How far the body has really gone forward since it stood at <paramref name="beganAt"/>, as a share of the
        /// <paramref name="meantMetres"/> a clip's feet are planted for by now (a giant's stride is a giant's: the clip's metres are the body's own).
        /// </summary>
        public float StrideShareSince(Vector3 beganAt, float meantMetres)
        {
            if (meantMetres < LeastStepToMeasure)
            {
                return 1.0f;
            }
            Vector3 gone = transform.position - beganAt;
            gone.y = 0.0f;
            return Mathf.Clamp(Vector3.Dot(gone, transform.forward) / (meantMetres * transform.lossyScale.y), 0.0f, LongestStride);
        }

        /// <summary>
        /// Right after the baked pose: the legs of a clip drawn in (or out) to the stride the body really took. Each foot keeps its height and
        /// its turn and goes <see cref="_clipStride"/> of its distance ahead of or behind the body; the knee bends for it.
        /// </summary>
        private void ApplyClipStride()
        {
            float legs = BakedLegsWeight;
            if (_bakedPoseWeight <= 0.0f || legs <= 0.0f || Mathf.Abs(_clipStride - 1.0f) < 0.02f)
            {
                return;
            }
            float share = Mathf.Lerp(1.0f, _clipStride, _bakedPoseWeight * legs);
            DrawFootToStride(_leftUpperLeg, _leftLowerLeg, _leftFoot, share);
            DrawFootToStride(_rightUpperLeg, _rightLowerLeg, _rightFoot, share);
        }

        private void DrawFootToStride(Transform thigh, Transform shin, Transform foot, float share)
        {
            if (thigh == null || shin == null || foot == null)
            {
                return;
            }
            Vector3 place = transform.InverseTransformPoint(foot.position);
            place.z *= share;
            Quaternion turn = foot.rotation;
            // The knee keeps bowing the way the clip had it.
            Vector3 knee = shin.position - (thigh.position + foot.position) * 0.5f;
            TwoBoneIK.Solve(thigh, shin, foot, transform.TransformPoint(place), knee.sqrMagnitude > 1e-6f ? knee : transform.forward);
            foot.rotation = turn;
        }

        /// <summary>
        /// The sword in the right hand held in the thrust grip, <paramref name="weight"/> of the way (BakedPose.ThrustGrip), and handed back to its own
        /// hold at 0. A sword that has left the hand meanwhile (put away mid-move) is left alone.
        /// </summary>
        /// <summary>The sword in the right hand held the way the baked move was posed: the thrust grip, or the mocap grip (the Octaslash), <paramref name="weight"/> of the way.</summary>
        private void HoldSwordForBakedMove(float weight, bool mocap)
        {
            if (weight > 0.0f && _swordHeldForThrust == null && _handR != null)
            {
                _swordHeldForThrust = _handR.GetComponentInChildren<ISwordBlade>();
            }
            if (_swordHeldForThrust == null)
            {
                return;
            }
            if (!(_swordHeldForThrust is Component held) || held == null || _handR == null || !held.transform.IsChildOf(_handR))
            {
                _swordHeldForThrust = null;
                return;
            }
            if (mocap)
            {
                _swordHeldForThrust.HoldForMocap(weight);
            }
            else
            {
                _swordHeldForThrust.HoldForThrust(weight);
            }
            if (weight <= 0.0f)
            {
                _swordHeldForThrust = null;
            }
        }

        /// <summary>
        /// A held thing's moving part (a pistol's slide, its magazine) that a baked pose may drive by name (BakedMotion.Props): its local position,
        /// rotation and visibility are set in the held thing's own frame. The gun registers its parts on equip and takes them back on unequip.
        /// </summary>
        public void RegisterBakedProp(string name, Transform part)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (part == null)
            {
                _bakedProps.Remove(name);
            }
            else
            {
                _bakedProps[name] = part;
            }
        }

        public void UnregisterBakedProp(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                _bakedProps.Remove(name);
            }
        }

        /// <summary>The part registered under <paramref name="name"/>, or null (for tests).</summary>
        public Transform BakedProp(string name)
        {
            return name != null && _bakedProps.TryGetValue(name, out Transform part) ? part : null;
        }

        /// <summary>
        /// The pelvis of a body running under a sword attack: turned <see cref="HipsTurnOnTheMove"/> of the way to the clip's (all of it as
        /// the body comes to a stand, <paramref name="legs"/>), each thigh kept turned as the run had it so the legs go on running; returns
        /// the spine's turn in the world that puts the shoulders where the clip has them (as if the pelvis had turned the whole way).
        /// </summary>
        private Quaternion MoveTrunkOverRunningLegs(Transform hips, Quaternion clipHips, Quaternion clipSpine, float legs)
        {
            Quaternion parent = hips.parent != null ? hips.parent.rotation : Quaternion.identity;
            Quaternion spineWanted = parent * clipHips * clipSpine;
            // A SPIN (the pose's whole turn: SwordStrikes.SpinDegrees) takes the pelvis and the running legs round together; only what the
            // clip turns the hips beyond it is shared out between the pelvis and the spine.
            hips.localRotation = Quaternion.Slerp(Quaternion.identity, _bakedPose.Turn, _bakedPoseWeight) * hips.localRotation;
            Quaternion leftThigh = _leftUpperLeg != null ? _leftUpperLeg.rotation : Quaternion.identity;
            Quaternion rightThigh = _rightUpperLeg != null ? _rightUpperLeg.rotation : Quaternion.identity;
            hips.localRotation = Quaternion.Slerp(hips.localRotation, clipHips, _bakedPoseWeight * Mathf.Lerp(HipsTurnOnTheMove, 1.0f, legs));
            // (The thighs then go toward the clip's by the legs' share, in the loop: from the run's, not from a pelvis-turned run.)
            if (_leftUpperLeg != null) _leftUpperLeg.rotation = leftThigh;
            if (_rightUpperLeg != null) _rightUpperLeg.rotation = rightThigh;
            return spineWanted;
        }

        /// <summary>
        /// Where <paramref name="pose"/> has the left ankle as it was made (before its turn), in the frame its hips are placed in: what a
        /// turn that leaves that foot where it stands is about.
        /// </summary>
        public Vector3 BakedLeftAnklePlace(BakedPose pose)
        {
            if (pose == null || _leftUpperLeg == null || _leftLowerLeg == null || _leftFoot == null)
            {
                return Vector3.zero;
            }
            Quaternion hips = Quaternion.identity, thigh = Quaternion.identity, shin = Quaternion.identity;
            for (int i = 0; i < pose.Bones.Count; i++)
            {
                switch (pose.Bones[i])
                {
                    case BakedMotion.RootBone: hips = pose.Rotations[i]; break;
                    case "thigh_l": thigh = pose.Rotations[i]; break;
                    case "shin_l": shin = pose.Rotations[i]; break;
                }
            }
            return pose.Hips + hips * (_leftUpperLeg.localPosition + thigh * (_leftLowerLeg.localPosition + shin * _leftFoot.localPosition));
        }

        /// <summary>A baked pose that leaves the arms (a sword attack's clip), before the arm poses.</summary>
        private void ApplyBakedTrunk()
        {
            if (BakedPoseLeavesTheArms)
            {
                LayBakedPose();
                ApplyClipStride();
            }
        }

        /// <summary>A baked pose of the whole body, over the arm poses.</summary>
        private void ApplyBakedPose()
        {
            if (!BakedPoseLeavesTheArms)
            {
                LayBakedPose();
                ApplyClipStride();
            }
        }

        private void LayBakedPose()
        {
            _bakedFingerWeight = 0.0f;
            if (_bakedPose == null || _bakedPoseWeight <= 0.0f || _animator == null)
            {
                HoldSwordForBakedMove(0.0f, false);   // a move in its own grip, once it has let go, hands the sword back to its own hold
                return;
            }
            if (!ReferenceEquals(_bakedBonesFor, _bakedPose.Bones))
            {
                _bakedBonesFor = _bakedPose.Bones;
                _bakedBones = new Transform[_bakedPose.Bones.Count];
                _bakedLowerBody = new bool[_bakedBones.Length];
                _bakedHasFingers = false;
                _bakedSpineIndex = -1;
                for (int i = 0; i < _bakedBones.Length; i++)
                {
                    if (_bakedPose.Bones[i] == "spine")
                    {
                        _bakedSpineIndex = i;
                    }
                    _bakedBones[i] = TransformSearch.FindDeep(_animator.transform, _bakedPose.Bones[i]);
                    _bakedLowerBody[i] = BakedMotion.IsLowerBody(_bakedPose.Bones[i]);
                    _bakedHasFingers |= _bakedBones[i] != null && BakedMotion.IsFingerBone(_bakedPose.Bones[i]);
                }
            }

            float legs = BakedLegsWeight;
            // A SWORD ATTACK ON THE MOVE: the legs (and where the hips are) stay the run's, but the pelvis still turns part of the way
            // with the cut, and the spine is set so the shoulders are where the clip has them (MoveTrunkOverRunningLegs).
            bool onTheMove = !_bakedPose.UpperBodyOnly && legs < 1.0f && _bakedSpineIndex >= 0;
            Quaternion spineWanted = Quaternion.identity;
            bool spineSet = false;
            for (int i = 0; i < _bakedBones.Length; i++)
            {
                Transform bone = _bakedBones[i];
                bool root = _bakedPose.Bones[i] == BakedMotion.RootBone;
                // A reload leaves the hips and legs to the Animator and the foot IK; a sword attack on the move, as much of them as it moves.
                float weight = _bakedLowerBody[i] && !(root && onTheMove) ? _bakedPoseWeight * legs : _bakedPoseWeight;
                if (bone == null || weight <= 0.0f)
                {
                    continue;
                }
                Quaternion rotation = _bakedPose.Rotations[i];
                if (root)
                {
                    // The whole pose's turn goes on the root, about the pivot: the rest of the skeleton hangs from it.
                    rotation = _bakedPose.Turn * rotation;
                    Vector3 position = _bakedPose.Pivot + _bakedPose.Turn * (_bakedPose.Hips - _bakedPose.Pivot);
                    bone.localPosition = Vector3.Lerp(bone.localPosition, position, _bakedPoseWeight * legs);
                    if (onTheMove)
                    {
                        spineWanted = MoveTrunkOverRunningLegs(bone, rotation, _bakedPose.Rotations[_bakedSpineIndex], legs);
                        spineSet = true;
                        continue;
                    }
                }
                else if (spineSet && i == _bakedSpineIndex && bone.parent != null)
                {
                    rotation = Quaternion.Inverse(bone.parent.rotation) * spineWanted;
                }
                bone.localRotation = weight >= 1.0f ? rotation : Quaternion.Slerp(bone.localRotation, rotation, weight);
            }
            float whole = _bakedPoseWeight;
            _bakedFingerWeight = _bakedHasFingers ? whole : 0.0f;
            HoldSwordForBakedMove(_bakedPose.ThrustGrip || _bakedPose.MocapGrip ? whole : 0.0f, _bakedPose.MocapGrip);

            // The held thing's parts, in its own frame: the gun's slide, its magazine (which vanishes once it has dropped, and reappears in
            // the free hand). Visibility switches once the pose has mostly taken hold, so a part never blinks during the blend in.
            for (int p = 0; p < _bakedPose.Props.Count; p++)
            {
                if (!_bakedProps.TryGetValue(_bakedPose.Props[p], out Transform part) || part == null)
                {
                    continue;
                }
                part.localPosition = Vector3.Lerp(part.localPosition, _bakedPose.PropPositions[p], whole);
                part.localRotation = Quaternion.Slerp(part.localRotation, _bakedPose.PropRotations[p], whole);
                if (whole >= 0.5f && part.gameObject.activeSelf != _bakedPose.PropVisible[p])
                {
                    part.gameObject.SetActive(_bakedPose.PropVisible[p]);
                }
            }
        }
    }
}

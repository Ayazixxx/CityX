using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A BAKED POSE: a move animated in Blender (BakedMotion). Played after the Animator like every pose here, and over all of them but
    /// the wrists: each bone set to the frame's own local rotation, the hips moved, the whole pose turned about a pivot if asked. The
    /// caller samples the motion and eases the weight in and out (OctaslashAbility).
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private BakedPose _bakedPose;

        private float _bakedPoseWeight;

        private Transform[] _bakedBones;

        private bool[] _bakedLowerBody;

        private bool _bakedHasFingers;

        private System.Collections.Generic.IReadOnlyList<string> _bakedBonesFor;

        private readonly System.Collections.Generic.Dictionary<string, Transform> _bakedProps = new System.Collections.Generic.Dictionary<string, Transform>();

        private float _bakedFingerWeight;

        private ISwordBlade _swordHeldForThrust;

        /// <summary>How much of the body a baked pose holds, 0 to 1.</summary>
        public float BakedPoseWeight => _bakedPoseWeight;

        /// <summary>How much the baked pose being applied holds the FINGERS this frame (0 when it has no finger bones): the hand grips keep off that much.</summary>
        public float BakedFingerWeight => _bakedFingerWeight;

        /// <summary>True while the baked pose leaves the hips and legs to the Animator (a gun reload): the foot IK and the lean stay on.</summary>
        public bool BakedPoseIsUpperBodyOnly => _bakedPose != null && _bakedPose.UpperBodyOnly;

        /// <summary>
        /// Puts <paramref name="pose"/> on the body from this frame's LateUpdate on, at <paramref name="weight"/> (1: the pose exactly; between:
        /// blended with whatever the Animator and the poses before it made). Set again every frame it changes; a null pose or a weight of 0 lets go.
        /// </summary>
        public void SetBakedPose(BakedPose pose, float weight)
        {
            _bakedPoseWeight = pose != null ? Mathf.Clamp01(weight) : 0.0f;
            _bakedPose = _bakedPoseWeight > 0.0f ? pose : null;
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

        private void ApplyBakedPose()
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
                for (int i = 0; i < _bakedBones.Length; i++)
                {
                    _bakedBones[i] = TransformSearch.FindDeep(_animator.transform, _bakedPose.Bones[i]);
                    _bakedLowerBody[i] = BakedMotion.IsLowerBody(_bakedPose.Bones[i]);
                    _bakedHasFingers |= _bakedBones[i] != null && BakedMotion.IsFingerBone(_bakedPose.Bones[i]);
                }
            }

            float weight = _bakedPoseWeight;
            bool upperOnly = _bakedPose.UpperBodyOnly;
            for (int i = 0; i < _bakedBones.Length; i++)
            {
                Transform bone = _bakedBones[i];
                if (bone == null || (upperOnly && _bakedLowerBody[i]))
                {
                    continue;   // a reload leaves the hips and legs to the Animator and the foot IK
                }
                Quaternion rotation = _bakedPose.Rotations[i];
                if (_bakedPose.Bones[i] == BakedMotion.RootBone)
                {
                    // The whole pose's turn goes on the root, about the pivot: the rest of the skeleton hangs from it.
                    rotation = _bakedPose.Turn * rotation;
                    Vector3 position = _bakedPose.Pivot + _bakedPose.Turn * (_bakedPose.Hips - _bakedPose.Pivot);
                    bone.localPosition = Vector3.Lerp(bone.localPosition, position, weight);
                }
                bone.localRotation = weight >= 1.0f ? rotation : Quaternion.Slerp(bone.localRotation, rotation, weight);
            }
            _bakedFingerWeight = _bakedHasFingers ? weight : 0.0f;
            HoldSwordForBakedMove(_bakedPose.ThrustGrip || _bakedPose.MocapGrip ? weight : 0.0f, _bakedPose.MocapGrip);

            // The held thing's parts, in its own frame: the gun's slide, its magazine (which vanishes once it has dropped, and reappears in
            // the free hand). Visibility switches once the pose has mostly taken hold, so a part never blinks during the blend in.
            for (int p = 0; p < _bakedPose.Props.Count; p++)
            {
                if (!_bakedProps.TryGetValue(_bakedPose.Props[p], out Transform part) || part == null)
                {
                    continue;
                }
                part.localPosition = Vector3.Lerp(part.localPosition, _bakedPose.PropPositions[p], weight);
                part.localRotation = Quaternion.Slerp(part.localRotation, _bakedPose.PropRotations[p], weight);
                if (weight >= 0.5f && part.gameObject.activeSelf != _bakedPose.PropVisible[p])
                {
                    part.gameObject.SetActive(_bakedPose.PropVisible[p]);
                }
            }
        }
    }
}

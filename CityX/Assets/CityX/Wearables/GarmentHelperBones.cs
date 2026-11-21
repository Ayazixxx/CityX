using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The helper bones a garment can be skinned to besides the body's own 14 (SkinnedGarment.Bones): a skirt bone in front of and behind each
    /// thigh, a half-turning bone on each shoulder (GarmentHelperPose has the why), and a collar bone that turns with the neck but goes where the
    /// head goes (the clips move the head along the neck, which a collar on the neck alone would let it sink into). Made on the body when a worn
    /// garment uses them, and set
    /// every frame after the body's pose is final (the Animator, then PlayerLocomotion's procedural poses).
    /// </summary>
    [DefaultExecutionOrder(31000)]
    public sealed class GarmentHelperBones : MonoBehaviour
    {
        public enum Kind { SkirtFront, SkirtBack, Shoulder, Collar }

        /// <summary>In skinning order, after SkinnedGarment.Bones: a garment's bone index 14 is the first of these.</summary>
        public static readonly (string name, HumanBodyBones parent, HumanBodyBones child, HumanBodyBones knee, Kind kind)[] Helpers =
        {
            ("skirt_front_l", HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, Kind.SkirtFront),
            ("skirt_back_l", HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, Kind.SkirtBack),
            ("skirt_front_r", HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, Kind.SkirtFront),
            ("skirt_back_r", HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, Kind.SkirtBack),
            ("shoulder_l", HumanBodyBones.Spine, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftUpperArm, Kind.Shoulder),
            ("shoulder_r", HumanBodyBones.Spine, HumanBodyBones.RightUpperArm, HumanBodyBones.RightUpperArm, Kind.Shoulder),
            ("collar", HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.Head, Kind.Collar),
        };

        private Transform _body;
        private Transform[] _helpers;
        private Transform[] _parents;
        private Transform[] _children;
        private Transform[] _knees;
        private Quaternion[] _parentBind;
        private Quaternion[] _childBind;

        // The skirt kept out of the legs and swinging behind them (SkirtCloth), when the worn mesh has one.
        private SkirtCloth _skirt;
        private Quaternion[] _rotations;
        private Vector3[] _places;

        /// <summary>
        /// The helper transforms, in <see cref="Helpers"/> order, made under the body's own bones on first call. The bind rotations come from the
        /// garment's bindposes (indexed as SkinnedGarment.Bones), so the helpers sit where the garment was bound. The worn mesh, if given, says which
        /// of its points each skirt bone carries, to keep out of the legs.
        /// </summary>
        public Transform[] Bind(Animator animator, Matrix4x4[] bindposes, Mesh worn = null)
        {
            _body = animator.transform;
            if (_helpers == null)
            {
                int count = Helpers.Length;
                _helpers = new Transform[count];
                _parents = new Transform[count];
                _children = new Transform[count];
                _knees = new Transform[count];
                _parentBind = new Quaternion[count];
                _childBind = new Quaternion[count];
                for (int i = 0; i < count; i++)
                {
                    _parents[i] = animator.GetBoneTransform(Helpers[i].parent);
                    _children[i] = animator.GetBoneTransform(Helpers[i].child);
                    _knees[i] = animator.GetBoneTransform(Helpers[i].knee);
                    _helpers[i] = new GameObject(Helpers[i].name).transform;
                    _helpers[i].SetParent(_parents[i], false);
                }
            }
            for (int i = 0; i < Helpers.Length; i++)
            {
                _parentBind[i] = BindRotation(bindposes, Helpers[i].parent);
                _childBind[i] = BindRotation(bindposes, Helpers[i].child);
            }
            _rotations = new Quaternion[Helpers.Length];
            _places = new Vector3[Helpers.Length];
            _skirt = worn != null && worn.isReadable ? new SkirtCloth(animator, bindposes, worn, Helpers.Length) : null;
            if (_skirt != null && !_skirt.HasSkirt)
            {
                _skirt = null;
            }
            LateUpdate();
            return _helpers;
        }

        private static Quaternion BindRotation(Matrix4x4[] bindposes, HumanBodyBones bone) =>
            bindposes[System.Array.IndexOf(SkinnedGarment.Bones, bone)].inverse.rotation;

        private void LateUpdate()
        {
            if (_helpers == null)
            {
                return;
            }
            for (int i = 0; i < _helpers.Length; i++)
            {
                Transform parent = _parents[i];
                Transform child = _children[i];
                if (parent == null || child == null)
                {
                    continue;
                }
                float share;
                switch (Helpers[i].kind)
                {
                    case Kind.Shoulder:
                        share = GarmentHelperPose.ShoulderShare;
                        break;
                    case Kind.Collar:
                        share = 0.0f;
                        break;
                    default:
                        float kneeLean = Vector3.Dot((_knees[i].position - child.position).normalized, _body.forward);
                        Vector3 ownSide = Helpers[i].child == HumanBodyBones.LeftUpperLeg ? -_body.right : _body.right;
                        float kneeOut = Vector3.Dot((_knees[i].position - child.position).normalized, ownSide);
                        share = GarmentHelperPose.SkirtShare(kneeLean, kneeOut, Helpers[i].kind == Kind.SkirtFront);
                        break;
                }
                _rotations[i] = GarmentHelperPose.Follow(parent.rotation, _parentBind[i], child.rotation, _childBind[i], share);
                _places[i] = child.position;
            }
            if (_skirt != null && _parents[0] != null)
            {
                _skirt.Settle(_rotations, _places, _helpers, _parents[0], _body != null ? _body.lossyScale.x : 1.0f, Time.deltaTime);
            }
            for (int i = 0; i < _helpers.Length; i++)
            {
                if (_parents[i] != null && _children[i] != null)
                {
                    _helpers[i].SetPositionAndRotation(_places[i], _rotations[i]);
                }
            }
        }
    }
}

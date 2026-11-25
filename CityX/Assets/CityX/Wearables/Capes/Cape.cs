using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A CAPE (docs/CAPE.md; owner, 2026-11-24: "make a basic cape that we can make a bunch of different colorways of easily, make it tied around
    /// the front. and research how to make good cape physics in video games"; then "make it fit something like this", a LEGO cape). Worn on the
    /// back, fitted as a LEGO cape is (CapeShape): a yoke over the whole top of the shoulders and round behind the neck, a collar round the front
    /// of the neck, both held to the body, and the rest cloth (<see cref="CapeCloth"/>) that falls, swings and streams behind a runner, kept out
    /// of the body by capsules on its bones. All one colour. Lying on the ground it is a folded cape, picked up with E.
    ///
    /// The body is followed after its animation and its posing (late in the frame), and part of the body's own movement is carried straight
    /// into the cloth (<see cref="CarryShare"/>) so a turn or a sprint does not whip it about; a body put somewhere new (a respawn) starts the
    /// cloth again at rest. On another player's copy of the wearer it runs the same from that body's moves (ICosmeticKeepsRunning): a cape is
    /// worn the same on every screen with nothing sent but which cape it is.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class Cape : GroundWearable, IEquippableItem, ICosmeticMountable, ICosmeticKeepsRunning
    {
        [SerializeField] private string _itemId = CapeColourways.RedItemId;

        [Tooltip("The cape's cloth, every face of it (one colour a colourway).")]
        public Material Fabric;

        [Tooltip("The folded cape lying on the ground; hidden while worn.")]
        public GameObject GroundPreview;

        /// <summary>How far outside the body's capsules the cloth stays, metres.</summary>
        public const float ClothGap = 0.035f;

        /// <summary>The share of the body's own movement carried straight into the cloth each frame (the rest it feels as inertia).</summary>
        public const float CarryShare = 0.3f;

        /// <summary>A body moved further than this in a frame was put somewhere, not walked there: the cloth starts again at rest.</summary>
        public const float TeleportMetres = 2.5f;

        /// <summary>The most steps run in a frame (a long frame does not freeze everything to catch up).</summary>
        public const int MostSteps = 6;

        /// <summary>A light breeze, metres a second, swelling and dying away.</summary>
        public static readonly Vector3 Breeze = new Vector3(0.7f, 0.0f, 0.25f);

        protected override string ItemId => _itemId;

        public void SetItemId(string itemId) => _itemId = itemId;

        /// <summary>The cloth (null until worn).</summary>
        public CapeCloth Cloth { get; private set; }

        /// <summary>The body's capsules as they were last step, for tests.</summary>
        public IReadOnlyList<CapeCloth.Capsule> Capsules => _capsules;

        private readonly struct BodyCapsule
        {
            public readonly Transform A, B;
            public readonly float Radius;

            public BodyCapsule(Transform a, Transform b, float radius)
            {
                A = a;
                B = b;
                Radius = radius;
            }
        }

        private Transform _body;
        private Transform _anchor;
        private Vector3[] _restOnAnchor;
        private Vector3[] _shape;
        private readonly List<(int, Vector3)> _pins = new List<(int, Vector3)>();
        private readonly List<BodyCapsule> _bodyCapsules = new List<BodyCapsule>();
        private readonly List<CapeCloth.Capsule> _capsules = new List<CapeCloth.Capsule>();
        private Vector3 _anchorWas;
        private Quaternion _anchorTurnWas;
        private float _owed;
        private CapeLook _look;

        public void OnEquip(GameObject player, ItemInstanceData instanceData) => PutOn(player);

        public void OnUnequip(GameObject player)
        {
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body) => PutOn(body);

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        private void PutOn(GameObject body)
        {
            MarkEquipped();
            if (GroundPreview != null)
            {
                GroundPreview.SetActive(false);
            }
            transform.SetParent(body.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            var animator = body.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null)
            {
                return;   // a body with no skeleton: nothing to hang it from
            }
            _body = body.transform;
            _anchor = animator.GetBoneTransform(HumanBodyBones.Spine) ?? animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform skinTransform = TransformSearch.FindDeep(body.transform, BodySurface.SkinName);
            var skin = skinTransform != null ? skinTransform.GetComponent<SkinnedMeshRenderer>() : null;
            Vector3[] rest = CapeShape.RestPoints();
            _restOnAnchor = new Vector3[rest.Length];
            for (int i = 0; i < rest.Length; i++)
            {
                _restOnAnchor[i] = OnBone(_anchor, skin, body.transform, rest[i]);
            }
            _shape = new Vector3[rest.Length];
            Shape(_anchor.localToWorldMatrix);
            Cloth = CapeShape.MakeCloth(_shape);
            FindCapsules(animator);
            _anchorWas = _anchor.position;
            _anchorTurnWas = _anchor.rotation;
            _look = new CapeLook(transform, Fabric);
            _look.Draw(Cloth.Position);
        }

        /// <summary>
        /// Where a point of the standing body (bind pose, body space) is in <paramref name="bone"/>'s own space: through the body skin's bind
        /// pose when it has one, so the cape sits on the bones as the body's own skin does, whatever pose the body is in when it is put on.
        /// </summary>
        private static Vector3 OnBone(Transform bone, SkinnedMeshRenderer skin, Transform body, Vector3 point)
        {
            if (skin != null && skin.sharedMesh != null)
            {
                int index = System.Array.IndexOf(skin.bones, bone);
                Matrix4x4[] binds = skin.sharedMesh.bindposes;
                if (index >= 0 && index < binds.Length)
                {
                    return binds[index].MultiplyPoint3x4(point);
                }
            }
            return bone.InverseTransformPoint(body.TransformPoint(point));
        }

        private void FindCapsules(Animator animator)
        {
            _bodyCapsules.Clear();
            Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);
            void Add(HumanBodyBones a, HumanBodyBones b, float radius)
            {
                Transform ta = Bone(a), tb = Bone(b);
                if (ta != null && tb != null) _bodyCapsules.Add(new BodyCapsule(ta, tb, radius + ClothGap));
            }
            Add(HumanBodyBones.Hips, HumanBodyBones.Neck, FusedBodyShape.TorsoRadius);
            Add(HumanBodyBones.Head, HumanBodyBones.Head, FusedBodyShape.HeadRadius);   // the Head bone's pivot is the head ball's middle
            foreach (bool left in new[] { true, false })
            {
                Add(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                    FusedBodyShape.LegRadius);
                Add(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
                    FusedBodyShape.AnkleRadius);
                Add(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                    FusedBodyShape.ArmRadius);
                Add(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                    FusedBodyShape.WristRadius);
            }
        }

        private void Shape(Matrix4x4 anchor)
        {
            for (int i = 0; i < _shape.Length; i++)
            {
                _shape[i] = anchor.MultiplyPoint3x4(_restOnAnchor[i]);
            }
        }

        private void LateUpdate()
        {
            if (Cloth == null || _anchor == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0.0f)
            {
                return;
            }
            Vector3 anchorNow = _anchor.position;
            Quaternion turnNow = _anchor.rotation;
            if (Vector3.Distance(anchorNow, _anchorWas) > TeleportMetres)
            {
                Shape(_anchor.localToWorldMatrix);
                Cloth.Reset(_shape);
                _owed = 0.0f;
            }
            else
            {
                // Part of the body's move this frame carried straight into the cloth; the rest it feels as inertia.
                Quaternion turn = Quaternion.Slerp(Quaternion.identity, turnNow * Quaternion.Inverse(_anchorTurnWas), CarryShare);
                Vector3 to = _anchorWas + (anchorNow - _anchorWas) * CarryShare;
                Cloth.Carry(Matrix4x4.Translate(to) * Matrix4x4.Rotate(turn) * Matrix4x4.Translate(-_anchorWas));
            }
            _owed = Mathf.Min(_owed + dt, CapeCloth.StepSeconds * MostSteps);
            int steps = Mathf.FloorToInt(_owed / CapeCloth.StepSeconds);
            _owed -= steps * CapeCloth.StepSeconds;
            ReadCapsules();
            float floor = _body.position.y + 0.02f;
            Vector3 wind = Breeze * (0.55f + 0.45f * Mathf.Sin(Time.time * 0.8f));
            for (int step = 1; step <= steps; step++)
            {
                // The held edge and the shape to keep, moved with the body through the frame step by step.
                float s = step / (float)steps;
                Matrix4x4 anchor = Matrix4x4.TRS(Vector3.Lerp(_anchorWas, anchorNow, s), Quaternion.Slerp(_anchorTurnWas, turnNow, s), _anchor.lossyScale);
                Shape(anchor);
                _pins.Clear();
                for (int i = 0; i < _shape.Length; i++)
                {
                    if (Cloth.Pinned[i]) _pins.Add((i, _shape[i]));   // the yoke and the collar, carried by the body
                }
                Cloth.Step(_pins, _shape, _capsules, wind, floor);
            }
            _anchorWas = anchorNow;
            _anchorTurnWas = turnNow;
            _look.Draw(Cloth.Position);
        }

        private void ReadCapsules()
        {
            _capsules.Clear();
            foreach (BodyCapsule capsule in _bodyCapsules)
            {
                _capsules.Add(new CapeCloth.Capsule(capsule.A.position, capsule.B.position, capsule.Radius));
            }
        }

        private void OnDestroy()
        {
            _look?.Dispose();
        }
    }
}

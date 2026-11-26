using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A CAPE (docs/CAPE.md; owner, 2026-11-24: "make a basic cape that we can make a bunch of different colorways of easily"; then "make it fit
    /// something like this", a LEGO cape). Worn on the back, fitted as a LEGO cape is (CapeShape): a yoke over the whole top of the shoulders and
    /// round behind the neck, a collar round the front of the neck, and the cloth hanging from the yoke behind to the calves. All one colour.
    /// Lying on the ground it is a folded cape, picked up with E.
    ///
    /// Light enough for a crowded hub (owner, 2026-11-25: "world of warcraft cape physics are like nothing it just fits over the clothing and
    /// looks good. maybe the back flies up a bit when you jump"): no cloth is worked out. It is a skinned mesh (CapeSkin) bent by the graphics
    /// card as the body is: the yoke and the collar on the body's own bones, over what is worn under them (fitted again when the clothes
    /// change), the hanging part on a short chain swung by two springs (CapeSwing) from how the wearer moves. On another player's copy of the
    /// wearer it runs the same from that body's moves (ICosmeticKeepsRunning): nothing is sent but which cape it is.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class Cape : GroundWearable, IEquippableItem, ICosmeticMountable, ICosmeticKeepsRunning
    {
        [SerializeField] private string _itemId = CapeColourways.RedItemId;

        [Tooltip("The cape's cloth, every face of it (one colour a colourway).")]
        public Material Fabric;

        [Tooltip("The folded cape lying on the ground; hidden while worn.")]
        public GameObject GroundPreview;

        /// <summary>
        /// A body moved faster than this was put somewhere, not moved there: the cape hangs still again, metres a second (or at least
        /// <see cref="TeleportMetres"/> in one frame). A speed, not a distance a frame: a skydive at a low frame rate crossed the old 2.5 m a
        /// frame every frame, and the cape hung straight down (bug sweep 2026-11-25).
        /// </summary>
        public const float TeleportSpeed = 150.0f;
        public const float TeleportMetres = 2.5f;

        /// <summary>How quickly the wearer's measured movement follows their real one, seconds (a frame's jolt does not throw the cape).</summary>
        public const float MovementSmoothing = 0.08f;

        /// <summary>How often a body with no clothes yet is looked at for some, seconds.</summary>
        private const float ClothesLookEvery = 0.5f;

        /// <summary>The name of the drawn cape under the cape's object.</summary>
        public const string LookName = "CapeCloth";

        protected override string ItemId => _itemId;

        public void SetItemId(string itemId) => _itemId = itemId;

        /// <summary>The cape at rest as it was fitted to the body it is worn on (body space), for tests.</summary>
        public Vector3[] Rest { get; private set; }

        /// <summary>The hanging part's swing.</summary>
        public CapeSwing Swing { get; } = new CapeSwing();

        /// <summary>The drawn cape (null until worn).</summary>
        public SkinnedMeshRenderer Look { get; private set; }

        /// <summary>The fit to each body mesh (one for every body of the same mesh): the cape at rest on it and each point's nearest body point.</summary>
        private static readonly Dictionary<Mesh, (Vector3[] fitted, int[] nearest)> Fits = new Dictionary<Mesh, (Vector3[], int[])>();

        private Transform _body;
        private Transform _anchor;
        private SkinnedMeshRenderer _skin;
        private SkinnedGarmentCombiner _clothes;
        private Mesh _clothesFittedOver;
        private float _clothesLookedFor;
        private Transform[] _chain = System.Array.Empty<Transform>();
        private Vector3 _sideways = Vector3.right;
        private Vector3 _ahead = Vector3.forward;
        private Vector3 _anchorWas;
        private float _headingWas;
        private Transform _carrierWas;
        private Vector3 _velocity;
        private Mesh _mesh;

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
            _skin = skinTransform != null ? skinTransform.GetComponent<SkinnedMeshRenderer>() : null;
            _clothes = body.GetComponent<SkinnedGarmentCombiner>();
            var look = new GameObject(LookName);
            look.transform.SetParent(transform, false);
            look.layer = body.layer;
            Look = look.AddComponent<SkinnedMeshRenderer>();
            Look.sharedMaterial = Fabric;
            Look.quality = SkinQuality.Bone4;
            Fit();
        }

        /// <summary>
        /// The cape fitted to the body and over what it wears now (CapeShape.FitToBody, FitOver, ClearHanging), and its mesh made on the body's
        /// bones and a new chain (CapeSkin), hanging straight. Without a readable body skin, the cape as made, all on the Spine bone.
        /// </summary>
        private void Fit()
        {
            _clothesFittedOver = _clothes != null ? _clothes.CombinedMesh : null;
            Mesh body = _skin != null ? WornBodyCover.Whole(_skin) : null;   // the whole body, not what clothes leave of it
            bool readable = body != null && body.isReadable && _skin.bones.Length == body.bindposes.Length;
            Vector3[] rest;
            var onBody = new BoneWeight[CapeShape.Points];
            Transform[] bodyBones;
            Matrix4x4[] bodyBinds;
            int anchorIndex;
            if (readable)
            {
                if (!Fits.TryGetValue(body, out (Vector3[] fitted, int[] nearest) fit))
                {
                    fit.fitted = CapeShape.FitToBody(CapeShape.RestPoints(), body.vertices, body.normals, out fit.nearest);
                    Fits[body] = fit;
                }
                rest = fit.fitted;
                if (_clothesFittedOver != null && _clothesFittedOver.isReadable)
                {
                    Vector3[] worn = _clothesFittedOver.vertices;
                    rest = CapeShape.FitOver(rest, fit.nearest, body.normals, worn);
                    rest = CapeShape.ClearHanging(rest, worn, _clothesFittedOver.normals);
                }
                BoneWeight[] weights = body.boneWeights;
                for (int i = 0; i < CapeShape.Points; i++)
                {
                    if (fit.nearest[i] >= 0) onBody[i] = weights[fit.nearest[i]];
                }
                bodyBones = _skin.bones;
                bodyBinds = body.bindposes;
                anchorIndex = System.Array.IndexOf(bodyBones, _anchor);
            }
            else
            {
                rest = CapeShape.RestPoints();
                bodyBones = new[] { _anchor };
                bodyBinds = new[] { _anchor.worldToLocalMatrix * _body.localToWorldMatrix };
                anchorIndex = 0;
            }
            if (anchorIndex < 0)
            {
                anchorIndex = 0;
            }
            for (int i = 0; i < CapeShape.Points; i++)
            {
                if (CapeSkin.Down(i) <= 0.0f && onBody[i].weight0 + onBody[i].weight1 + onBody[i].weight2 + onBody[i].weight3 <= 0.0f)
                {
                    onBody[i] = new BoneWeight { boneIndex0 = anchorIndex, weight0 = 1.0f };
                }
            }
            Rest = rest;

            Matrix4x4 anchorBind = bodyBinds[anchorIndex];
            Vector3[] chainPoints = CapeSkin.ChainPoints(rest);
            MakeChain(CapeSkin.ChainLocalPositions(chainPoints, anchorBind));
            _sideways = anchorBind.MultiplyVector(Vector3.right).normalized;
            _ahead = anchorBind.MultiplyVector(Vector3.forward).normalized;

            var bones = new Transform[bodyBones.Length + _chain.Length];
            bodyBones.CopyTo(bones, 0);
            _chain.CopyTo(bones, bodyBones.Length);
            if (_mesh != null) Destroy(_mesh);
            _mesh = CapeSkin.Build(rest, CapeSkin.Weights(onBody, bodyBones.Length), bodyBinds, CapeSkin.ChainBindposes(chainPoints, anchorBind));
            Look.bones = bones;
            Look.rootBone = _anchor;
            Look.sharedMesh = _mesh;
            Look.localBounds = new Bounds(Vector3.zero, Vector3.one * 4.0f);   // round the Spine, room for the cape swung right up
            Swing.Reset();
            _carrierWas = _body.parent;
            _anchorWas = AnchorNow(_carrierWas);
            _headingWas = HeadingNow(_carrierWas);
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Where the cape hangs from and which way the body faces, against whatever carries the body: a car's seat (the body is put under
        /// it), else the world. A body sitting in a moving car is not running: the cape flew up and swung at every turn (bug sweep 2026-11-25).
        /// </summary>
        private Vector3 AnchorNow(Transform carrier) => carrier != null ? carrier.InverseTransformPoint(_anchor.position) : _anchor.position;

        private float HeadingNow(Transform carrier) => carrier != null ? _body.localEulerAngles.y : _body.eulerAngles.y;

        /// <summary>The chain down the hanging part: a new one, each bone under the one before, the first under the Spine.</summary>
        private void MakeChain(Vector3[] localPositions)
        {
            foreach (Transform bone in _chain)
            {
                if (bone != null) Destroy(bone.gameObject);
            }
            _chain = new Transform[localPositions.Length];
            Transform parent = _anchor;
            for (int k = 0; k < localPositions.Length; k++)
            {
                var bone = new GameObject($"CapeChain{k}").transform;
                bone.SetParent(parent, false);
                bone.localPosition = localPositions[k];
                bone.localRotation = Quaternion.identity;
                _chain[k] = bone;
                parent = bone;
            }
        }

        /// <summary>Fitted again when the clothes under it change (a shirt put on or taken off while it is worn).</summary>
        private void WatchClothes()
        {
            if (_clothes == null)
            {
                _clothesLookedFor += Time.deltaTime;
                if (_clothesLookedFor < ClothesLookEvery)
                {
                    return;
                }
                _clothesLookedFor = 0.0f;
                _clothes = _body.GetComponent<SkinnedGarmentCombiner>();
            }
            if (_clothes != null && _clothes.CombinedMesh != _clothesFittedOver)
            {
                Fit();
            }
        }

        private void LateUpdate()
        {
            if (Look == null || _anchor == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0.0f)
            {
                return;
            }
            WatchClothes();
            Transform carrier = _body.parent;
            Vector3 anchorNow = AnchorNow(carrier);
            float headingNow = HeadingNow(carrier);
            Vector3 moved = carrier != null ? carrier.TransformVector(anchorNow - _anchorWas) : anchorNow - _anchorWas;
            if (carrier != _carrierWas || moved.magnitude > Mathf.Max(TeleportMetres, TeleportSpeed * dt))
            {
                Swing.Reset();   // sat down, got up, or put somewhere
                _velocity = Vector3.zero;
                _carrierWas = carrier;
            }
            else
            {
                // How the wearer moves, in their own frame, smoothed a little; and how fast they turn.
                Vector3 moving = _body.InverseTransformDirection(moved / dt);
                _velocity = Vector3.Lerp(_velocity, moving, 1.0f - Mathf.Exp(-dt / MovementSmoothing));
                Swing.Update(_velocity, Mathf.DeltaAngle(_headingWas, headingNow) / dt, Time.time, dt);
            }
            _anchorWas = anchorNow;
            _headingWas = headingNow;

            // The swing shared down the chain, so the cape curves as it lifts.
            Quaternion bend = Quaternion.AngleAxis(Swing.Lift / _chain.Length, _sideways) * Quaternion.AngleAxis(Swing.Sway / _chain.Length, _ahead);
            foreach (Transform bone in _chain)
            {
                bone.localRotation = bend;
            }
        }

        private void OnDestroy()
        {
            foreach (Transform bone in _chain)
            {
                if (bone != null) Destroy(bone.gameObject);
            }
            if (_mesh != null) Destroy(_mesh);
        }
    }
}

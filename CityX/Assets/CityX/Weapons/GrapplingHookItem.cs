using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Weapons
{
    /// <summary>
    /// The grappling hook launcher: aim at a building, fire, and swing. This
    /// is only the item -- the raycast that finds an anchor and the rope
    /// visual -- because the actual swing physics live on the player's own
    /// GrappleSwing, which has to own the whole movement frame for as long
    /// as the rope is attached. Ported from items/grappling_hook.gd.
    ///
    /// THE ROPE IS A SINGLE STRETCHED TUBE, re-drawn every frame between the
    /// muzzle and the anchor, rather than a real segmented/simulated rope --
    /// matching the Godot original's own reasoning: a rope that visibly
    /// sagged would need its own solver and a chain of segments, and a taut
    /// line both looks like a grappling hook under load and honestly
    /// represents the physics underneath (the swing's own constraint is a
    /// rigid distance, not a spring).
    ///
    /// A REAL PROCEDURAL TUBE MESH, NOT A LineRenderer. An earlier version
    /// used one (Unity's own dedicated "two points, drawn as a strip" tool)
    /// on the reasoning that it was the same visual for less code -- it is
    /// not the same visual: a LineRenderer's strip is a single FLAT ribbon
    /// that only ever gets reoriented to face the camera (or, with
    /// TransformZ alignment, a fixed axis), and a flat ribbon viewed from
    /// ANYWHERE within its own plane -- which happens constantly while
    /// swinging, since the player and camera are often moving roughly
    /// along the rope's own direction toward the anchor -- presents a true
    /// zero-width edge and vanishes outright, confirmed directly with a
    /// scratch render from a camera placed near the muzzle looking toward
    /// the anchor. A real tube has actual cross-sectional volume and shows
    /// SOME visible surface from every angle except looking exactly down
    /// its own bore, which a flat strip cannot do no matter how its width
    /// vector is computed.
    ///
    /// SAME PICKUP/EQUIP LIFECYCLE as every other held item (see
    /// BottleWeapon) -- proximity + look-check, Inventory reparents a fresh
    /// copy onto the hand bone.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GrapplingHookItem : MonoBehaviour, IEquippableItem, IGrappleLauncher
    {
        public const float BodyLength = 0.22f;
        public const float BodyRadius = 0.035f;
        public const float BarrelLength = 0.16f;
        public const float BarrelRadius = 0.018f;

        public static readonly Color BodyColour = new Color(0.2f, 0.22f, 0.26f);
        public static readonly Color BarrelColour = new Color(0.5f, 0.52f, 0.56f);
        public static readonly Color HookColour = new Color(0.62f, 0.64f, 0.68f);
        public static readonly Color RopeColour = new Color(0.14f, 0.13f, 0.12f);

        public const float RopeRadius = 0.018f;

        /// <summary>The flying hook head parked at the anchor while attached -- purely cosmetic, so the rope visibly ends in something.</summary>
        public const float HookHeadSize = 0.09f;

        public const float HookMass = 1.1f;
        public static readonly Vector3 GripHandPosition = new Vector3(0.0f, 0.03f, -0.05f);
        public static readonly Vector3 GripHandRotationEuler = new Vector3(-90.0f, 0.0f, 0.0f);
        public const float InteractRange = 2.5f;

        [Tooltip("Where the rope is drawn FROM -- tracks the barrel tip through whatever animation the hand happens to be playing.")]
        public Transform Muzzle;

        [Tooltip("Built once by GrapplingHookBuilder -- runtime code only applies these, matching BottleWeapon's own materials-built-at-edit-time convention.")]
        public Material RopeMaterial;

        public Material HookHeadMaterial;

        private Rigidbody _rigidbody;
        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;

        // Rope + hook head live in the WORLD, not under this item: both span
        // from the hand to an anchor tens of metres away, and a child of a
        // bone that is being animated (and may carry its own scale) is the
        // wrong space to express that in. Built on ShowRope, destroyed on
        // HideRope -- the rope must never outlive the launcher.
        private MeshFilter _rope;
        private Mesh _ropeMesh;
        private Transform _hookHead;

        /// <summary>
        /// Low. This is a thin cord, not a showpiece -- just enough sides
        /// that its silhouette reads as round rather than as an obviously
        /// faceted rod, at a radius most players will never see up close.
        /// </summary>
        private const int RopeSides = 6;

        // Reused every frame UpdateRope runs rather than reallocated --
        // MarkDynamic + SetVertices/SetNormals on one persistent Mesh is
        // the standard "this changes every frame" path; only the vertex
        // POSITIONS change per frame (both endpoints move), never the
        // triangle winding, so the arrays and the mesh's own triangles are
        // built once in ShowRope and just overwritten in UpdateRope.
        private Vector3[] _ropeVertices;
        private Vector3[] _ropeNormals;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void OnDestroy()
        {
            HideRope();
        }

        private void Update()
        {
            if (_equipped)
            {
                return;
            }
            TickPickup();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerVitals>() != null)
            {
                _nearbyPlayer = other.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform == _nearbyPlayer || other.GetComponentInParent<PlayerVitals>() != null)
            {
                _nearbyPlayer = null;
            }
        }

        private void TickPickup()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }
            if (Vector3.Distance(transform.position, _nearbyPlayer.position) > InteractRange)
            {
                return;
            }

            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory != null && inventory.AddItem(ItemRegistry.GrapplingHook, 1))
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Fires at whatever <paramref name="direction"/> is pointing at
        /// from <paramref name="origin"/> (the camera, in practice).
        ///
        /// WORLD GEOMETRY ONLY: a hit carrying an IDamageable is rejected --
        /// a rope anchored to a person who then walks away is a physics
        /// problem with no good answer, and "grapple onto an enemy" is a
        /// different mechanic from "swing between buildings", matching the
        /// Godot original's own collision-mask restriction.
        /// </summary>
        public bool FindAnchor(Vector3 origin, Vector3 direction, float maxRange, out Vector3 anchor)
        {
            anchor = Vector3.zero;
            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, maxRange,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (hit.collider.GetComponentInParent<IDamageable>() != null)
            {
                return false;
            }
            anchor = hit.point;
            return true;
        }

        /// <summary>Shows the rope + hook head. Called by GrappleSwing the moment a grapple actually attaches.</summary>
        public void ShowRope(Vector3 anchor)
        {
            HideRope();

            var ropeObject = new GameObject("GrappleRope");
            _rope = ropeObject.AddComponent<MeshFilter>();
            var renderer = ropeObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = RopeMaterial;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _ropeMesh = new Mesh { name = "GrappleRopeMesh" };
            _ropeMesh.MarkDynamic();
            _ropeVertices = new Vector3[RopeSides * 2];
            _ropeNormals = new Vector3[RopeSides * 2];

            var triangles = new int[RopeSides * 6];
            for (int i = 0; i < RopeSides; i++)
            {
                int next = (i + 1) % RopeSides;
                int a0 = i;
                int b0 = i + RopeSides;
                int a1 = next;
                int b1 = next + RopeSides;
                int t = i * 6;
                triangles[t + 0] = a0;
                triangles[t + 1] = b0;
                triangles[t + 2] = b1;
                triangles[t + 3] = a0;
                triangles[t + 4] = b1;
                triangles[t + 5] = a1;
            }
            _ropeMesh.vertices = _ropeVertices;
            _ropeMesh.triangles = triangles;
            _rope.sharedMesh = _ropeMesh;

            var headObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headObject.name = "GrappleHookHead";
            Object.Destroy(headObject.GetComponent<Collider>());
            headObject.transform.localScale = Vector3.one * HookHeadSize;
            headObject.GetComponent<MeshRenderer>().sharedMaterial = HookHeadMaterial;
            _hookHead = headObject.transform;
            _hookHead.position = anchor;

            UpdateRope(anchor);
        }

        /// <summary>
        /// Re-stretches the rope tube between the muzzle and the anchor.
        /// Called every frame by GrappleSwing while swinging, since both
        /// ends move. Rebuilds the ring of vertices at each end around the
        /// current muzzle-to-anchor axis -- the same tapered-tube technique
        /// SwordBuilder's own AddTube uses at edit time, run live here
        /// since both endpoints move every frame a real LineRenderer strip
        /// could update by just moving two points, but a real tube needs
        /// its whole cross-section re-oriented to the new axis instead.
        /// </summary>
        public void UpdateRope(Vector3 anchor)
        {
            if (_rope == null)
            {
                return;
            }
            Vector3 from = Muzzle != null ? Muzzle.position : transform.position;

            Vector3 axis = anchor - from;
            float length = axis.magnitude;
            Vector3 direction = length > 0.0001f ? axis / length : Vector3.up;
            Vector3 helper = Mathf.Abs(Vector3.Dot(direction, Vector3.forward)) < 0.95f
                ? Vector3.forward
                : Vector3.right;
            Vector3 xAxis = Vector3.Cross(direction, helper).normalized;
            Vector3 zAxis = Vector3.Cross(xAxis, direction).normalized;

            for (int i = 0; i < RopeSides; i++)
            {
                float angle = (float)i / RopeSides * Mathf.PI * 2.0f;
                Vector3 radial = xAxis * Mathf.Cos(angle) + zAxis * Mathf.Sin(angle);
                _ropeVertices[i] = from + radial * RopeRadius;
                _ropeVertices[i + RopeSides] = anchor + radial * RopeRadius;
                _ropeNormals[i] = radial;
                _ropeNormals[i + RopeSides] = radial;
            }
            _ropeMesh.SetVertices(_ropeVertices);
            _ropeMesh.SetNormals(_ropeNormals);
            _ropeMesh.RecalculateBounds();

            if (_hookHead != null)
            {
                _hookHead.position = anchor;
            }
        }

        public void HideRope()
        {
            if (_rope != null)
            {
                Destroy(_rope.gameObject);
                _rope = null;
            }
            if (_ropeMesh != null)
            {
                Destroy(_ropeMesh);
                _ropeMesh = null;
            }
            if (_hookHead != null)
            {
                Destroy(_hookHead.gameObject);
                _hookHead = null;
            }
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripHandPosition;
                transform.localEulerAngles = GripHandRotationEuler;
            }

            _equipped = true;
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
            _rigidbody.isKinematic = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

            var swing = player.GetComponent<GrappleSwing>();
            if (swing != null)
            {
                swing.EquippedHook = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var swing = player.GetComponent<GrappleSwing>();
            if (swing != null)
            {
                if (swing.IsActive)
                {
                    swing.Release(false);
                }
                if (ReferenceEquals(swing.EquippedHook, this))
                {
                    swing.EquippedHook = null;
                }
            }
            HideRope();
            Destroy(gameObject);
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}

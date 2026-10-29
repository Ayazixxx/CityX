using CityX.Character;
using CityX.Items;
using UnityEngine;

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
    public sealed class GrapplingHookItem : MonoBehaviour, IEquippableItem, IGrappleLauncher, ICosmeticMountable
    {
        public static readonly Color HookColour = new Color(0.62f, 0.64f, 0.68f);
        public static readonly Color RopeColour = new Color(0.14f, 0.13f, 0.12f);

        public const float RopeRadius = 0.018f;

        /// <summary>The flying hook head parked at the anchor while attached -- purely cosmetic, so the rope visibly ends in something.</summary>
        public const float HookHeadSize = 0.09f;

        public const float HookMass = 1.1f;

        /// <summary>
        /// THE SAME NUMBERS AS PistolWeapon's OWN, by explicit request --
        /// "make it so he holds it the same as the pistol". A one-handed
        /// grip low against the hip, not the old procedural body's own
        /// two-handed (-90,0,0) pose built around a straight cylinder body
        /// with no real grip shape to speak of. GrapplingHookBuilder's own
        /// Visual child carries a fixed corrective rotation (see its own
        /// BuildVisual doc comment) so THIS root's grip transform can be
        /// identical to the pistol's while the differently-modelled mesh
        /// underneath still points its own muzzle the same way the
        /// Beretta model's does.
        /// </summary>
        public static readonly Vector3 GripHandPosition = PistolWeapon.GripHandPosition;

        public static readonly Vector3 GripHandRotationEuler = PistolWeapon.GripHandRotationEuler;

        /// <summary>
        /// Requested: "scale up the grapple gun size by 1.5x." Applied
        /// on top of whatever GrapplingHookBuilder's own NormalizeScale
        /// already baked into the Visual child at build time -- this is a
        /// runtime multiplier on the WHOLE held item's root, not a change
        /// to that baked value, so it stays a single, easy-to-retune
        /// number here rather than needing an editor rebuild to adjust
        /// again.
        /// </summary>
        public const float GripScale = 1.5f;

        public const float InteractRange = 2.5f;

        [Tooltip("Fallback rope origin, only used before OnEquip has ever run (see RopeOrigin's own doc comment) -- otherwise the rope draws from the wearer's own stable root instead.")]
        public Transform Muzzle;

        [Tooltip("Built once by GrapplingHookBuilder -- runtime code only applies these, matching BottleWeapon's own materials-built-at-edit-time convention.")]
        public Material RopeMaterial;

        public Material HookHeadMaterial;

        private Rigidbody _rigidbody;
        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;

        /// <summary>
        /// The wearer's own root -- set in OnEquip, used ONLY to draw the
        /// rope from a stable point. See RopeOrigin's own doc comment for
        /// why the hand bone itself stopped being safe to use for this.
        /// </summary>
        private Transform _wearerRoot;

        private PlayerLocomotion _wearerLocomotion;
        private Ragdoll _wearerRagdoll;

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
            Transform body = InteractionReach.LocalPlayerBody(other);
            if (body != null)
            {
                _nearbyPlayer = body;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (InteractionReach.IsLeaving(other, _nearbyPlayer))
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
            if (!InteractKey.Tapped)
            {
                return;
            }
            if (Vector3.Distance(transform.position, _nearbyPlayer.position) > InteractRange)
            {
                return;
            }

            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory != null && inventory.PickUp(ItemRegistry.GrapplingHook))
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
        /// current muzzle-to-anchor axis: a LineRenderer strip could update
        /// by just moving two points, but a real tube needs its whole
        /// cross-section re-oriented to the new axis instead.
        /// </summary>
        public void UpdateRope(Vector3 anchor)
        {
            if (_rope == null)
            {
                return;
            }
            Vector3 from = RopeOrigin();

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

        /// <summary>
        /// Bug report: "the rope is glitching disappearing and
        /// tweaking out" / "teleporting all over the place." Muzzle (and
        /// the fallback, this item's own transform) are both parented to
        /// the RightHand bone -- fine when the old GrappleSwing held that
        /// hand in one fixed reach pose for the rope's whole duration, but
        /// GrappleSwing was rebuilt to never pose the arm at all any more
        /// (see its own class doc comment): the hand now swings through
        /// whatever the ORDINARY running/falling animation does with it, a
        /// real, fairly wide swing under normal locomotion. Rebuilding this
        /// tube's own vertices from a point riding that swing every frame
        /// is what read as the rope itself glitching/teleporting -- the
        /// geometry was correct, its own moving ORIGIN was the problem.
        /// Rebuilt from the wearer's own ROOT transform instead -- moved
        /// only by CharacterController.Move, never touched by the Animator
        /// -- plus a fixed local offset approximating a held gun's
        /// position. Since GrappleSwing also never rotates the body during
        /// a swing, this point is completely stationary relative to the
        /// character for the swing's whole duration, moving only as the
        /// body itself actually travels, matching "make it stay still".
        /// </summary>
        private Vector3 RopeOrigin()
        {
            // Hanging limp from the rope (GrappleSwing.Rope.cs) the root stays where the body left the ground and the hand rides the ragdoll's
            // forearm, moved by physics, not the Animator: the rope comes from the hook in that hand.
            if (_wearerRoot == null || (_wearerRagdoll != null && _wearerRagdoll.IsLimp))
            {
                return Muzzle != null ? Muzzle.position : transform.position;
            }
            float height = (_wearerLocomotion != null ? _wearerLocomotion.StandingHeight : 1.8f) * 0.68f;
            Vector3 sideAndForward = _wearerRoot.TransformDirection(new Vector3(0.3f, 0.0f, 0.35f));
            return _wearerRoot.position + Vector3.up * height + sideAndForward;
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
                transform.localScale = Vector3.one * GripScale;
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

            // See RopeOrigin's own doc comment -- the rope draws from the
            // wearer's own root, never the animated hand, so it needs to
            // know who that is.
            _wearerRoot = player.transform;
            _wearerLocomotion = player.GetComponent<PlayerLocomotion>();
            _wearerRagdoll = player.GetComponent<Ragdoll>();

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

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.RightHand, GripHandPosition, GripHandRotationEuler, GripScale);
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}

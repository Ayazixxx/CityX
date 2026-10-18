using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Wearables
{
    /// <summary>
    /// A classic black top hat, worn on the head. Ported from
    /// wearables/top_hat.gd -- a rigid single piece rather than something
    /// SkinnedGarment's per-bone deformation is needed for (nothing about a
    /// hat bends with the body), so this follows Jetpack's own shape
    /// instead: parented straight onto one bone on equip.
    ///
    /// Slot-based replacement (evicting whatever else already occupies the
    /// same slot on a second equip) is Inventory.EquipItemId's own job, not
    /// this file's -- unlike wearable.gd's own hand-rolled loop over
    /// head.get_children().
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class TopHat : MonoBehaviour, IEquippableItem
    {
        public const float InteractRange = 2.5f;

        /// <summary>
        /// Local offset from the Head bone. NOT a direct copy of
        /// top_hat.gd's own 0.68 -- confirmed by an actual render (first
        /// attempt floated the hat a whole head-height above the scalp)
        /// that this rig's Head bone pivot sits at the head sphere's own
        /// CENTRE, not at Godot's "neck-top" convention. CharacterHair's own
        /// placement formula (FusedBodyShape.HeadRadius - RootsBelowCrown +
        /// mesh.bounds.max.y) confirms the same thing: HeadRadius (0.35) by
        /// itself already reaches the TOP of the head from here, one full
        /// radius short of Godot's own 0.70 "head top" value. Matches
        /// Godot's own small embed below the true top (0.70 - 0.68 = 0.02)
        /// proportionally: 0.35 - 0.02.
        /// </summary>
        public static readonly Vector3 HeadPosition = new Vector3(0.0f, 0.33f, 0.0f);

        public static readonly Vector3 HeadRotationEuler = Vector3.zero;

        /// <summary>
        /// Matches TopHatBuilder's own former per-piece materials exactly --
        /// now applied here at runtime instead of baked into a unique
        /// Material asset per piece, so Brim/Crown/Band can all reference
        /// ONE shared "Helmet" category material (see
        /// CharacterSkin.EnsureSharedCategory) while still looking like a
        /// black hat with a dark red band.
        /// </summary>
        public static readonly Color HatColour = new Color(0.03f, 0.03f, 0.035f);
        public static readonly Color BandColour = new Color(0.09f, 0.02f, 0.02f);

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
            ApplyTints();
        }

        /// <summary>
        /// Re-applied every time this component wakes up (ground pickup,
        /// equipped instance, editor capture) rather than trusted to
        /// survive PrefabUtility.SaveAsPrefabAsset -- see WearableTint's
        /// own class comment for why a MaterialPropertyBlock is applied
        /// here instead of baked once at build time.
        /// </summary>
        private void ApplyTints()
        {
            WearableTint.Apply(transform.Find("Brim")?.GetComponent<Renderer>(), HatColour, 0.4f);
            WearableTint.Apply(transform.Find("Crown")?.GetComponent<Renderer>(), HatColour, 0.4f);
            WearableTint.Apply(transform.Find("Band")?.GetComponent<Renderer>(), BandColour, 0.5f);
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

        /// <summary>Proximity + E, matching every other pickup item's own convention.</summary>
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
            if (inventory != null && inventory.AddItem(ItemRegistry.TopHat, 1))
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// NO ANIMATOR, OR AN ANIMATOR WITH NO AVATAR, BOTH TOLERATED.
        /// GetBoneTransform THROWS rather than returning null when the
        /// Animator has no avatar at all (as opposed to an avatar that
        /// simply lacks one particular bone, which it tolerates) -- the
        /// same gap SkinnedGarment.OnEquip's own doc comment describes
        /// catching, checked here up front rather than repeating it.
        /// </summary>
        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.avatar != null
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            if (head != null)
            {
                transform.SetParent(head, false);
                transform.localPosition = HeadPosition;
                transform.localEulerAngles = HeadRotationEuler;
            }

            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }
        }

        public void OnUnequip(GameObject player)
        {
            Destroy(gameObject);
        }

        /// <summary>Plain clothing carries no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}

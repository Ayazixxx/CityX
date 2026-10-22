using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A parachute backpack -- Requested: "add a parachute as an item
    /// parachute backpack and it functions exactly the same as a fortnite
    /// glider but its a parachute. press space and open and close it and it
    /// slows down your fall pretty basic." Press Space while airborne to
    /// deploy it, press again to pack it away; while deployed, falling is
    /// clamped to a slow, controlled descent instead of accelerating under
    /// gravity the way an ordinary fall does.
    ///
    /// WASD STEERING IS UNTOUCHED -- PlayerLocomotion's own horizontal
    /// movement was never coupled to vertical velocity in the first place
    /// (Jetpack already relies on the exact same fact), so slowing the fall
    /// alone already gives the "glide around while slowly descending" feel
    /// without this needing to touch horizontal speed at all.
    ///
    /// SHARES JETPACK'S OWN "back" EQUIP SLOT -- see ItemRegistry.Parachute's
    /// own doc comment. Wearing a parachute and a jetpack at once would not
    /// mean anything (which one does Space even talk to?), and Inventory's
    /// own slot eviction already resolves that for free, the same way
    /// putting on a second coat already replaces the first.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Parachute : MonoBehaviour, IEquippableItem, IParachute, ICosmeticMountable
    {
        public string ItemId => ItemRegistry.Parachute;

        public const float InteractRange = 2.5f;

        /// <summary>
        /// The slow, controlled descent speed once open. Comfortably below
        /// anything that could accumulate real fall damage before the next
        /// ResetFallTracking call catches up -- see Tick's own comment for
        /// why that combination makes an open parachute effectively
        /// fall-damage-proof, matching a real glider.
        /// </summary>
        public const float OpenFallSpeed = 4.0f;

        /// <summary>Matches Jetpack.BackPosition's own reasoning -- flush against the spine, just far enough out not to sink into the torso.</summary>
        public static readonly Vector3 BackPosition = new Vector3(0.0f, 0.02f, -0.30f);

        public static readonly Vector3 BackRotationEuler = Vector3.zero;

        [Tooltip("The folded pack, worn whenever this is equipped.")]
        public GameObject PackVisual;

        [Tooltip("The open canopy -- shown only while deployed.")]
        public GameObject CanopyVisual;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
            SetCanopyVisible(false);
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

        /// <summary>Proximity + E, matching Jetpack's own identical pickup convention.</summary>
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
            if (inventory != null && inventory.PickUp(ItemRegistry.Parachute))
            {
                Destroy(gameObject);
            }
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform spine = animator != null ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            if (spine != null)
            {
                transform.SetParent(spine, false);
                transform.localPosition = BackPosition;
                transform.localEulerAngles = BackRotationEuler;
            }

            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

            _locomotion = player.GetComponent<PlayerLocomotion>();
            _vitals = player.GetComponent<PlayerVitals>();

            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedParachute = this;
            }

            SetOpen(false);
        }

        public void OnUnequip(GameObject player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && ReferenceEquals(input.EquippedParachute, this))
            {
                input.EquippedParachute = null;
            }
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.Spine, BackPosition, BackRotationEuler, 1.0f);
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>
        /// See IParachute.Tick's own doc comment for what the parameters
        /// mean. Called every frame while equipped, matching Jetpack.Tick's
        /// own "grounded or not" shape -- this is also where landing gets
        /// noticed and the parachute auto-closes.
        /// </summary>
        public void Tick(bool toggledThisFrame, bool grounded, float deltaTime)
        {
            if (grounded)
            {
                // AUTO-CLOSES ON LANDING -- matches a real glider's own
                // canopy collapsing the instant your feet are down. Nothing
                // left to fall through, so there is no reason to stay
                // deployed, let alone keep clamping vertical velocity while
                // just standing around.
                SetOpen(false);
                return;
            }

            if (toggledThisFrame)
            {
                SetOpen(!IsOpen);
            }

            if (IsOpen)
            {
                _locomotion?.SetVerticalVelocity(-OpenFallSpeed);
                // Same mechanism Jetpack.Tick already uses for exactly the
                // same reason -- see PlayerVitals.ResetFallTracking's own
                // doc comment ("it negates fall damage from the last height
                // it was on"). Called every open frame, so any fall damage
                // on eventual landing only ever measures the drop since the
                // parachute was LAST open, never the whole original fall --
                // an open parachute is effectively fall-damage-proof.
                if (_vitals != null && _locomotion != null)
                {
                    _vitals.ResetFallTracking(_locomotion.transform.position.y);
                }
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            SetCanopyVisible(open);
        }

        private void SetCanopyVisible(bool visible)
        {
            if (CanopyVisual != null)
            {
                CanopyVisual.SetActive(visible);
            }
        }
    }
}

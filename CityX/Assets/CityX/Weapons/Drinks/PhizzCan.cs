using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A CAN OF PHIZZ IN THE HAND (docs/PHIZZ_CAN.md; owner, 2026-11-23: "he holds it in his right hand ... thumb wrapped around for grip", "make it heal
    /// how we described 80 hp over 10 seconds"). Carried upright in the right fist, raised as a bottle is; a click drinks it (CanDrinker, on the body,
    /// plays the drink and times it): the tab lifts, the hole shows black, the drink heals, and the can is dropped empty. Not a weapon. Lying on the
    /// ground it is picked up with E. Other players see it in your hand (ICosmeticMountable) and see you drink it (CanDrinker).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhizzCan : MonoBehaviour, IEquippableItem, ICosmeticMountable, IHandGripStyle, IRaisedCarry, IHeldDrink
    {
        public const string ItemId = ItemRegistry.PhizzCan;

        /// <summary>What a can heals, and over how long (the owner's).</summary>
        public const float HealAmount = 80.0f;
        public const float HealSeconds = 10.0f;

        /// <summary>
        /// The can in the right hand bone's frame (x down the fingers, y the back of the hand, z the thumb's side): its axis along the thumb's side,
        /// the fist round its middle, its side on the palm, its hole toward the wrist (the body as it is carried). Keep in step with
        /// CityX/SourceArt/Drink/can_drink_build.py's CAN_HOLD.
        /// </summary>
        public static readonly Vector3 HoldPosition = new Vector3(0.10f, -0.121f, -0.148f);

        public static Quaternion HoldRotation => Quaternion.LookRotation(Vector3.down, Vector3.forward);

        /// <summary>How far the tab lifts on its rivet, degrees.</summary>
        public const float TabOpenDegrees = 75.0f;

        public const float InteractRange = 2.5f;
        public const float CanMass = 0.4f;

        [Tooltip("The tab's hinge at the rivet: turned about its Z to lift the tab (PhizzCanBuilder).")]
        public Transform TabHinge;

        [Tooltip("The black of the opened hole, hidden until the tab is lifted.")]
        public GameObject Hole;

        [Tooltip("Played as the tab cracks the lid (the owner's sound).")]
        public AudioClip OpenSound;

        [Tooltip("The can emptied, as it is dropped: falls, lies about and goes (DroppedCan).")]
        public GameObject EmptyPrefab;

        private Rigidbody _rigidbody;
        private readonly GroundPickup _pickup = new GroundPickup();
        private bool _equipped;
        private GameObject _holder;

        /// <summary>The tab lifted, 0 shut to 1 open.</summary>
        public float Opened { get; private set; }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            SetOpened(0.0f);
        }

        private void Update()
        {
            if (!_equipped && _pickup.Player != null)
            {
                TryPickup(_pickup.Player, InteractKey.Tapped);
            }
        }

        private void OnTriggerEnter(Collider other) => _pickup.Entered(other);

        private void OnTriggerExit(Collider other) => _pickup.Left(other);

        public bool TryPickup(Transform player, bool ePressed)
        {
            return ePressed && InteractionReach.CanReach(transform.position, player, InteractRange, 0.0f, out _)
                && GroundPickup.Take(player, gameObject, ItemId, GetInstanceData());
        }

        /// <summary>The tab lifted <paramref name="opened"/> of the way (0 to 1), and the hole shown once it has cracked the lid.</summary>
        public void SetOpened(float opened)
        {
            Opened = Mathf.Clamp01(opened);
            if (TabHinge != null)
            {
                TabHinge.localRotation = Quaternion.AngleAxis(TabOpenDegrees * Opened, Vector3.forward);
            }
            bool cracked = Opened > 0.5f;
            if (Hole != null)
            {
                if (cracked && !Hole.activeSelf && OpenSound != null)
                {
                    AudioSource.PlayClipAtPoint(OpenSound, transform.position);
                }
                Hole.SetActive(cracked);
            }
        }

        /// <summary>The empty can let go of where it is, falling as the hand left it; this one goes. Harmless either way: nothing is hurt by it.</summary>
        public GameObject DropEmpty(Vector3 velocity)
        {
            GameObject empty = null;
            if (EmptyPrefab != null)
            {
                empty = Instantiate(EmptyPrefab, transform.position, transform.rotation);
                var body = empty.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = velocity;
                }
            }
            gameObject.SetActive(false);
            return empty;
        }

        // ---- in the hand ----

        public bool Drink()
        {
            var drinker = _holder != null ? _holder.GetComponent<CanDrinker>() : null;
            return drinker != null && drinker.Begin(this);
        }

        /// <summary>The drink is done: the slot is emptied and this can goes (the empty one is already falling).</summary>
        public void Finished()
        {
            if (_holder == null)
            {
                return;
            }
            GameObject holder = _holder;
            Release(holder);
            var inventory = holder.GetComponent<Inventory>();
            inventory?.ClearEquipSlotWithoutReturning(inventory.ActiveWeaponSlot);
            Destroy(gameObject);
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Hold(player);
            _holder = player;
            _equipped = true;
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedDrink = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var drinker = player != null ? player.GetComponent<CanDrinker>() : null;
            if (drinker != null)
            {
                drinker.Stop(this);
            }
            Release(player);
            Destroy(gameObject);
        }

        private void Release(GameObject player)
        {
            var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            if (input != null && ReferenceEquals(input.EquippedDrink, this))
            {
                input.EquippedDrink = null;
            }
            _holder = null;
        }

        private void Hold(GameObject body)
        {
            var animator = body.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = HoldPosition;
                transform.localRotation = HoldRotation;
                transform.localScale = Vector3.one;
            }
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
            _rigidbody.isKinematic = true;
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
        }

        public void MountCosmetic(GameObject body)
        {
            Hold(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        // ---- the fingers (IHandGripStyle) ----

        /// <summary>
        /// Round the can's side (owner: "thumb wrapped around for grip no not clipping into the can, maybe a very slight amount of the finger tips can
        /// clip through"), measured on the skin against the can (CanGripPlayTests): a can is far wider than a handle, so the fingers bend at the knuckle
        /// and hardly at the tip to lie round it (curled evenly, their tips hit it a third of the way to a fist); the thumb turned a little away
        /// from the fingers and closed onto the can's near side.
        /// </summary>
        public FingerAmounts StrongHandGrip => new FingerAmounts(0.0f, 0.0f, 0.0f, 0.0f, CanThumb, ThumbRoundTheCanDegrees)
        {
            Authored = 1.0f,
            IndexRootDegrees = FingerKnuckleDegrees,
            IndexTipDegrees = FingerTipDegrees,
            FingerRootDegrees = FingerKnuckleDegrees,
            FingerTipDegrees = FingerTipDegrees,
            ThumbRootDegrees = HandGrip.ThumbProximalDegrees * CanThumb,
            ThumbTipDegrees = HandGrip.ThumbIntermediateDegrees * CanThumb,
        };

        public const float FingerKnuckleDegrees = 30.0f;
        public const float FingerTipDegrees = 5.0f;
        public const float CanThumb = 0.4f;
        public const float ThumbRoundTheCanDegrees = -30.0f;

        public FingerAmounts SupportHandGrip => FingerAmounts.Open;
    }
}

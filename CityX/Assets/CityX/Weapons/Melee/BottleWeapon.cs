using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A GLASS BOTTLE (docs/GLASS_BOTTLE.md; owner, 2026-11-23): held by the neck with its body standing up out of the fist ("carry it like
    /// the demoman"), swung as a one-handed club (the sword's own swings, SoulsSwordCombat), blunt until it hits anything at all, a body, a
    /// wall, the ground: then its body bursts and what is left is the neck and half the bottle ending in sharp points, which stabs harder
    /// ("starts as a blunt", "a broken bottle that does pierce damage"). Whole, right-click held winds the arm back and a click throws it
    /// ("hold right click to aim and throw it"): in the air it is a <see cref="ThrownBottle"/>, and shatters whole on whatever it lands on.
    /// Broken, right-click is the guard, as any one-handed weapon's is.
    ///
    /// Every kind of bottle (Items/GlassBottleKinds) is this component with its own models (BottleBuilder, from BottleShape). The bottle's own
    /// frame is the held one: the fist's middle on the neck at the origin, the bottle running out along +Y to its base.
    ///
    /// Other players see it in the hand (ICosmeticMountable), swung (the attack's name is sent as every swing's is), aimed, thrown and
    /// broken (the body's <see cref="BottleThrower"/>).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BottleWeapon : MonoBehaviour, IEquippableItem, IEquippableWeapon, ISwordBlade, IRaisedCarry, IAimedThrow, ICosmeticMountable,
        IHandGripStyle
    {
        public enum DurabilityState
        {
            Intact,
            Broken,
            FullyBroken,
        }

        public const float IntactMeleeDamage = 15.0f;
        public const float BrokenMeleeDamage = 22.0f;
        public const float FullyBrokenMeleeDamage = 0.0f;
        public const int BrokenHitsToFullyBroken = 10;
        public const float MeleeCooldownSecondsConst = 0.5f;
        public const float ThrowSpeedConst = 16.0f;
        public const float BottleMass = 0.4f;
        public const float BottleBounce = 0.25f;
        public const float BottleFriction = 0.7f;
        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f; // cos(35 degrees)

        /// <summary>How fast the pieces of a bottle broken on a swing are carried on along the swing, m/s.</summary>
        public const float BrokenOnASwingSpeed = 5.0f;

        /// <summary>The fist round the neck, in the right hand bone's frame: where a one-handed sword's handle lies, the bottle out past the thumb.</summary>
        public static Vector3 HoldPosition => SwordWeapon.FistPoint;

        public static Quaternion HoldRotation => SwordWeapon.DefaultOneHandGripRotation;

        [SerializeField] private string _itemId = GlassBottleKinds.Classic;

        [Tooltip("The whole bottle, the half a blow leaves, and the neck that is left when that has broken down too (BottleBuilder).")]
        public GameObject IntactVisual;
        public GameObject BrokenVisual;
        public GameObject ShatteredVisual;

        [Tooltip("The bottle in the air (ThrownBottle).")]
        public GameObject ThrownBottlePrefab;

        [Tooltip("The bottle's own pieces (BottleShatter): those past the break fly when it breaks in the hand.")]
        public GameObject ShatterPrefab;

        public AudioClip BreakSound;

        public string ItemId => _itemId;

        /// <summary>Makes this the bottle <paramref name="itemId"/> (BottleBuilder).</summary>
        public void SetItemId(string itemId) => _itemId = itemId;

        public DurabilityState State { get; private set; } = DurabilityState.Intact;

        private int _brokenHitCount;
        private bool _equipped;
        private Rigidbody _rigidbody;
        private SphereCollider _pickupTrigger;
        private readonly GroundPickup _pickup = new GroundPickup();
        private GameObject _holder;
        private SoulsSwordCombat _souls;
        private BottleThrower _thrower;
        private Vector3 _aimPoint;
        private bool _throwing;
        private Vector3 _lastBase, _lastTip;
        private bool _swept;

        private BottleShape Shape => BottleShape.For(_itemId) ?? BottleShape.For(GlassBottleKinds.Classic);

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _pickupTrigger = GetComponent<SphereCollider>();
            RefreshVisual();
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

        /// <summary>
        /// See PistolWeapon.TryPickup's own comment -- same split, same reason: Keyboard.current is null outside a real Input System device,
        /// so a test drives this with an explicit flag instead.
        /// </summary>
        public bool TryPickup(Transform player, bool ePressed)
        {
            return ePressed && InteractionReach.CanReach(transform.position, player, InteractRange, LookDotThreshold, out _)
                && GroundPickup.Take(player, gameObject, _itemId, GetInstanceData());
        }

        // ---- the club ----

        public Vector3 BladeBaseWorld => transform.position;

        /// <summary>The far end: the base while it is whole, the longest point of what is left once it is not.</summary>
        public Vector3 BladeTipWorld => transform.TransformPoint(new Vector3(0.0f, Reach, 0.0f));

        private float Reach
        {
            get
            {
                BottleShape shape = Shape;
                switch (State)
                {
                    case DurabilityState.Intact: return shape.Reach;
                    case DurabilityState.Broken: return shape.BrokenReach;
                    default: return (shape.GripHeight - shape.ShatteredAt) * BottleShape.Scale;
                }
            }
        }

        public bool IsOneHanded => true;

        public float GetMeleeDamage()
        {
            switch (State)
            {
                case DurabilityState.Intact: return IntactMeleeDamage;
                case DurabilityState.Broken: return BrokenMeleeDamage;
                default: return FullyBrokenMeleeDamage;
            }
        }

        public float MeleeCooldownSeconds => MeleeCooldownSecondsConst;

        public string MovesetId => ComboChain.Sword;

        public ComboStage? HeavyStage => CombatTuning.SwordHeavyAttack;

        /// <summary>A blow landed on a body: a whole bottle breaks on it, a broken one wears down toward nothing.</summary>
        public void RegisterLandedMeleeHit()
        {
            switch (State)
            {
                case DurabilityState.Intact:
                    BreakOnABlow();
                    break;
                case DurabilityState.Broken:
                    _brokenHitCount++;
                    if (_brokenHitCount >= BrokenHitsToFullyBroken)
                    {
                        BreakOnABlow();
                    }
                    break;
                default:
                    break; // fully broken is inert
            }
        }

        /// <summary>
        /// A whole bottle breaks on ANYTHING a swing hits (owner: "when swung and hits anything while in hand it turns into the half broken
        /// version"): the bodies are told of by the swing itself (RegisterLandedMeleeHit); walls, the ground and loose things are found here,
        /// the bottle traced from where it was last frame to where it is now through the swing's hitting frames.
        /// </summary>
        private void LateUpdate()
        {
            if (!_equipped || _holder == null || State != DurabilityState.Intact || _souls == null || _souls.Phase != SoulsPhase.Active)
            {
                _swept = false;
                return;
            }
            Vector3 nowBase = BladeBaseWorld, nowTip = BladeTipWorld;
            if (_swept)
            {
                for (int i = 1; i <= 3 && State == DurabilityState.Intact; i++)
                {
                    float along = i / 3.0f;
                    Vector3 from = Vector3.Lerp(_lastBase, _lastTip, along);
                    Vector3 travel = Vector3.Lerp(nowBase, nowTip, along) - from;
                    float length = travel.magnitude;
                    if (length < 0.02f)
                    {
                        continue;
                    }
                    foreach (RaycastHit hit in Physics.RaycastAll(from, travel / length, length, _souls.TargetLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform.IsChildOf(_holder.transform) || hit.collider.GetComponentInParent<IDamageable>() != null)
                        {
                            continue;   // itself; and a body is the swing's own to land on
                        }
                        BreakOnABlow();
                        break;
                    }
                }
            }
            _lastBase = nowBase;
            _lastTip = nowTip;
            _swept = true;
        }

        /// <summary>The next state down, its pieces flying on the way the bottle is going; other players are told.</summary>
        private void BreakOnABlow()
        {
            if (State == DurabilityState.FullyBroken)
            {
                return;
            }
            Vector3 way = _swept ? BladeTipWorld - _lastTip : transform.up;
            if (way.sqrMagnitude < 1e-6f)
            {
                way = _holder != null ? _holder.transform.forward : transform.up;
            }
            DurabilityState to = State == DurabilityState.Intact ? DurabilityState.Broken : DurabilityState.FullyBroken;
            BreakTo(to, way);
            _brokenHitCount = 0;
            if (_thrower != null)
            {
                _thrower.TellBroke(to, way);
                _thrower.Driver.Stop();   // a bottle that is no longer whole is not thrown
            }
        }

        /// <summary>
        /// Broken down to <paramref name="to"/> on a blow going <paramref name="way"/>: what is past the new break flies off as the bottle's
        /// own pieces, and what is left shows. Public for another player's copy of the bottle (BottleThrower).
        /// </summary>
        public void BreakTo(DurabilityState to, Vector3 way)
        {
            if (to <= State)
            {
                return;
            }
            BottleShape shape = Shape;
            float from = (shape.GripHeight - (to == DurabilityState.Broken ? shape.BrokenAt : shape.ShatteredAt)) * BottleShape.Scale;
            float until = State == DurabilityState.Intact ? float.PositiveInfinity : (shape.GripHeight - shape.BrokenAt) * BottleShape.Scale;
            State = to;
            RefreshVisual();
            Vector3 along = way.sqrMagnitude > 1e-6f ? way.normalized : transform.up;
            BottleShatter.Spawn(ShatterPrefab, transform.position, transform.rotation, along * BrokenOnASwingSpeed, -along, from, until);
            if (BreakSound != null)
            {
                AudioSource.PlayClipAtPoint(BreakSound, transform.position);
            }
        }

        // ---- the fingers (IHandGripStyle) ----

        /// <summary>
        /// The fist round the neck with the thumb curled round it too, turned away from the bottle's body: left to point along the bottle as a
        /// sword's grip has it, the thumb went into the shoulder.
        /// </summary>
        public const float ThumbRoundTheNeckDegrees = 50.0f;

        public FingerAmounts StrongHandGrip => new FingerAmounts(NeckFingers, NeckFingers, NeckFingers, NeckFingers, NeckThumb, ThumbRoundTheNeckDegrees);

        /// <summary>How far the fingers and the thumb close on the neck (a neck is narrower than a sword's handle: closed all the way they went into the glass).</summary>
        public const float NeckFingers = 0.85f;
        public const float NeckThumb = 0.15f;

        public FingerAmounts SupportHandGrip => FingerAmounts.Wrap(1.0f);

        // ---- the throw ----

        public bool CanThrow() => State == DurabilityState.Intact;

        public float ThrowSpeed => ThrowSpeedConst;

        public bool IsThrowing => _throwing;

        /// <summary>Right-click held with a whole bottle: the arm winds back with it, or comes back to the carry.</summary>
        public void SetAiming(GameObject player, bool aiming)
        {
            if (_thrower != null && !_throwing && (CanThrow() || !aiming))
            {
                _thrower.Aim(aiming);
            }
        }

        /// <summary>
        /// A click from the aim: the arm comes forward, and the bottle leaves the hand at the throw's release, from where the hand then is,
        /// toward where <paramref name="direction"/> pointed from <paramref name="origin"/> when the click came.
        /// </summary>
        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
            if (!CanThrow() || _throwing)
            {
                return;
            }
            _holder = player;
            _aimPoint = origin + direction.normalized * AimMetres;
            if (_thrower == null)
            {
                LetGo();
                return;
            }
            _throwing = true;
            _thrower.Driver.Released += LetGo;
            if (!_thrower.BeginThrow())
            {
                _thrower.Driver.Released -= LetGo;
                _throwing = false;
            }
        }

        /// <summary>How far ahead a throw is aimed at: the bottle leaves the hand toward the point this far along the crosshair's line.</summary>
        public const float AimMetres = 20.0f;

        /// <summary>The hand lets go: the bottle is in the air as it lay in the hand, and gone from the hand and the inventory.</summary>
        private void LetGo()
        {
            if (_thrower != null)
            {
                _thrower.Driver.Released -= LetGo;
            }
            _throwing = false;
            GameObject player = _holder;
            if (player == null)
            {
                return;
            }
            // From the bottle's middle, so it turns about itself and not about its neck.
            Vector3 from = transform.TransformPoint(new Vector3(0.0f, Shape.Reach * 0.5f, 0.0f));
            Vector3 way = _aimPoint - from;
            way = way.sqrMagnitude > 1e-6f ? way.normalized : player.transform.forward;
            Quaternion held = transform.rotation;
            if (_thrower != null)
            {
                _thrower.Throw(ThrownBottlePrefab, _itemId, from, held, way * ThrowSpeedConst);
            }
            else
            {
                var vitals = player.GetComponent<PlayerVitals>();
                ThrownBottle.Spawn(ThrownBottlePrefab, from, held, way * ThrowSpeedConst, vitals != null ? vitals.Strength : 0,
                    player.GetComponentsInChildren<Collider>(), vitals != null ? vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f, player);
            }

            Release(player);
            var inventory = player.GetComponent<Inventory>();
            inventory?.ClearEquipSlotWithoutReturning(inventory.ActiveWeaponSlot);
            Destroy(gameObject);
        }

        // ---- in the hand ----

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            if (instanceData.HasDurability)
            {
                State = (DurabilityState)Mathf.Clamp(instanceData.Durability, 0, 2);
                RefreshVisual();
            }

            Hold(player);
            _holder = player;
            _equipped = true;

            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null)
            {
                melee.EquippedWeapon = this;
            }
            // Swung the soulslike way, as every one-handed weapon is (SoulsSwordCombat).
            _souls = player.GetComponent<SoulsSwordCombat>();
            if (_souls == null)
            {
                _souls = player.AddComponent<SoulsSwordCombat>();
            }
            _souls.Equip(this);
            _thrower = player.GetComponent<BottleThrower>();
        }

        public void OnUnequip(GameObject player)
        {
            if (_thrower != null)
            {
                _thrower.Driver.Released -= LetGo;
                _thrower.Driver.Stop();
            }
            Release(player);
            Destroy(gameObject);
        }

        private void Release(GameObject player)
        {
            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null && ReferenceEquals(melee.EquippedWeapon, this))
            {
                melee.EquippedWeapon = null;
            }
            if (_souls != null)
            {
                _souls.Unequip(this);
            }
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
            }
            // GetComponent RATHER THAN THE CACHED FIELD: Awake() isn't guaranteed to have run yet when a caller reaches this in the same
            // frame as Instantiate.
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

        public ItemInstanceData GetInstanceData() =>
            new ItemInstanceData { HasDurability = true, Durability = (int)State };

        private void RefreshVisual()
        {
            if (IntactVisual != null) IntactVisual.SetActive(State == DurabilityState.Intact);
            if (BrokenVisual != null) BrokenVisual.SetActive(State == DurabilityState.Broken);
            if (ShatteredVisual != null) ShatteredVisual.SetActive(State == DurabilityState.FullyBroken);
        }
    }
}

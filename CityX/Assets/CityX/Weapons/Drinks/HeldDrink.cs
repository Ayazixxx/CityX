using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A DRINK IN THE HAND (docs/PHIZZ_CAN.md): what every drink shares, the can of PHIZZ and the full PHIZZ bottle alike. Carried upright in the right
    /// fist, the fingers round its side; a click drinks it (Drinker, on the body, plays its clip and times it); lying on the ground it is picked up with E.
    /// Not a weapon. Other players see it in your hand (ICosmeticMountable) and see you drink it (Drinker). What is particular to each drink (how it is
    /// opened, what is left after, what it heals) is the drink's own.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class HeldDrink : MonoBehaviour, IEquippableItem, ICosmeticMountable, IHandGripStyle, IRaisedCarry, IHeldDrink, IAimedThrow,
        CityX.World.IBreakableSurface
    {
        public const float InteractRange = 2.5f;

        private Rigidbody _rigidbody;
        private readonly GroundPickup _pickup = new GroundPickup();
        private bool _equipped;
        private BottleThrower _thrower;
        private bool _throwing;
        private Vector3 _aimPoint;

        [Tooltip("It in the air, thrown full: it lands with a splash (ThrownBottle).")]
        public GameObject ThrownPrefab;

        /// <summary>The body holding it (its owner's, or another player's copy of it); null on the ground.</summary>
        protected GameObject Holder { get; private set; }

        /// <summary>Its item id (ItemRegistry).</summary>
        public abstract string DrinkItemId { get; }

        /// <summary>Its clip in the drinks' motion (Drinker.MotionPath).</summary>
        public abstract string Clip { get; }

        /// <summary>What it heals, over how long.</summary>
        public abstract float HealAmount { get; }

        public abstract float HealSeconds { get; }

        /// <summary>It is held where its hold puts it in the right hand bone's frame (x down the fingers, y the back of the hand, z the thumb's side).</summary>
        protected abstract Vector3 HoldPosition { get; }

        /// <summary>Every drink stands upright out of the fist, its axis along the thumb's side.</summary>
        public static Quaternion HoldRotation => Quaternion.LookRotation(Vector3.down, Vector3.forward);

        /// <summary>Opened (the tab lifted, the cap off), 0 shut to 1 open.</summary>
        public float Opened { get; private set; }

        /// <summary>Carried in the first frame of its own drink (the can at the chest, the long bottle lower).</summary>
        public string CarryMotion => Drinker.MotionPath;

        public string CarryClip => Clip;

        protected virtual void Awake()
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
                && GroundPickup.Take(player, gameObject, DrinkItemId, GetInstanceData());
        }

        // ---- the drink, timed by Drinker ----

        /// <summary>Opened <paramref name="opened"/> of the way (0 to 1).</summary>
        public void SetOpened(float opened)
        {
            float was = Opened;
            Opened = Mathf.Clamp01(opened);
            ShowOpened(was, Opened);
        }

        /// <summary>How it shows being opened from <paramref name="was"/> to <paramref name="now"/>.</summary>
        protected abstract void ShowOpened(float was, float now);

        /// <summary><paramref name="drunk"/> of it drunk, 0 full to 1 empty (the bottle's liquid goes down; a can shows nothing).</summary>
        public virtual void SetDrunk(float drunk)
        {
        }

        /// <summary>The moment the hand is done with it (the can is let fall: Drinker.DropSeconds). Harmless: nothing is hurt by it.</summary>
        public virtual void LetGo(Vector3 velocity)
        {
        }

        /// <summary>The drink is done, on the drinker's own machine: what is left of it, in the hand or in the bag.</summary>
        public abstract void Finished();

        /// <summary>A copy of another player's drink is done.</summary>
        public virtual void FinishedCopy()
        {
        }

        // ---- in the hand ----

        public bool Drink()
        {
            var drinker = Holder != null ? Holder.GetComponent<Drinker>() : null;
            return drinker != null && drinker.Begin(this);
        }

        // ---- broken where it lies (owner: "a yellow liquid splash effect when the bottle is broken (when shot or thrown)") ----

        /// <summary>How hard a drink splashes when it is broken where it lies (BloodBurst's strength).</summary>
        public const float BrokenSplashStrength = 1.6f;

        /// <summary>A blow or a shot on it where it lies (not in a hand): it bursts and splashes; a shot goes on through.</summary>
        public bool TakePunch(Vector3 point) => TakeBlow(point, Vector3.down, CityX.World.BlowStrength.Light);

        public bool TakeBlow(Vector3 point, Vector3 direction, CityX.World.BlowStrength strength)
        {
            if (Holder != null || !gameObject.activeInHierarchy)
            {
                return false;
            }
            BloodBurst.Splash(point, direction.sqrMagnitude > 1e-6f ? direction : Vector3.up, BrokenSplashStrength, FullBottle.LiquidColour);
            Burst(point, direction);
            Destroy(gameObject);
            return true;
        }

        /// <summary>What is left when it is broken where it lies (the bottle's pieces, the can lying open).</summary>
        protected abstract void Burst(Vector3 point, Vector3 direction);

        // ---- thrown (the bottle's aim and throw: BottleThrower, its moves seen by other players) ----

        private bool BeingDrunk => Holder != null && Holder.GetComponent<Drinker>() is Drinker drinker && drinker.Drinking;

        public bool CanThrow => !_throwing && !BeingDrunk;

        public bool IsThrowing => _throwing;

        public void SetAiming(GameObject player, bool aiming)
        {
            if (_thrower != null && !_throwing)
            {
                _thrower.Aim(aiming);
            }
        }

        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
            if (!CanThrow)
            {
                return;
            }
            _aimPoint = origin + direction.normalized * BottleWeapon.AimMetres;
            if (_thrower == null)
            {
                LetGoThrown();
                return;
            }
            _throwing = true;
            _thrower.Driver.Released += LetGoThrown;
            if (!_thrower.BeginThrow())
            {
                _thrower.Driver.Released -= LetGoThrown;
                _throwing = false;
            }
        }

        /// <summary>Its middle, the game's metres up from its base: a thrown drink turns about itself.</summary>
        protected abstract float MiddleHeight { get; }

        /// <summary>The throw lets it go: it flies at the crosshair, full, and the next of a stack comes out of the bag.</summary>
        private void LetGoThrown()
        {
            if (_thrower != null)
            {
                _thrower.Driver.Released -= LetGoThrown;
            }
            _throwing = false;
            GameObject player = Holder;
            if (player == null)
            {
                return;
            }
            Vector3 from = transform.TransformPoint(Vector3.up * MiddleHeight);
            Vector3 way = _aimPoint - from;
            way = way.sqrMagnitude > 1e-6f ? way.normalized : player.transform.forward;
            if (_thrower != null)
            {
                _thrower.Throw(ThrownPrefab, DrinkItemId, BottleWeapon.DurabilityState.Intact, from, transform.rotation, way * BottleWeapon.ThrowSpeedConst,
                    BottleWeapon.IntactThrowDamage);
            }
            else
            {
                var vitals = player.GetComponent<PlayerVitals>();
                ThrownBottle.Spawn(ThrownPrefab, from, transform.rotation, way * BottleWeapon.ThrowSpeedConst, vitals != null ? vitals.Strength : 0,
                    player.GetComponentsInChildren<Collider>(), 0.0f, player, false, 0, BottleWeapon.IntactThrowDamage);
            }
            DrinkStack.Drunk(this, player, null);
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Hold(player);
            Holder = player;
            _equipped = true;
            _thrower = player.GetComponent<BottleThrower>();
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedDrink = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            if (_thrower != null)
            {
                _thrower.Driver.Released -= LetGoThrown;
                if (_throwing)
                {
                    _thrower.Driver.Stop();
                }
            }
            var drinker = player != null ? player.GetComponent<Drinker>() : null;
            if (drinker != null)
            {
                drinker.Stop(this);
            }
            Release(player);
            Destroy(gameObject);
        }

        /// <summary>The input lets go of this drink and it holds nobody.</summary>
        protected void Release(GameObject player)
        {
            var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            if (input != null && ReferenceEquals(input.EquippedDrink, this))
            {
                input.EquippedDrink = null;
            }
            Holder = null;
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
            Holder = body;
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
        /// Round the drink's side (owner: "thumb wrapped around for grip no not clipping into the can, maybe a very slight amount of the finger tips can
        /// clip through"), measured on the skin against the can (CanGripPlayTests; the PHIZZ bottle's body is as wide): a can is far wider than a handle,
        /// so the fingers bend at the knuckle and hardly at the tip to lie round it (curled evenly, their tips hit it a third of the way to a fist); the
        /// thumb turned a little away from the fingers and closed onto the near side.
        /// </summary>
        public FingerAmounts StrongHandGrip => new FingerAmounts(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, ThumbTurn)
        {
            Authored = 1.0f,
            IndexRootDegrees = FingerKnuckleDegrees,
            IndexTipDegrees = FingerTipDegrees,
            FingerRootDegrees = FingerKnuckleDegrees,
            FingerTipDegrees = FingerTipDegrees,
            ThumbRootDegrees = ThumbRootFold,
            ThumbTipDegrees = ThumbTipFold,
        };

        public const float FingerKnuckleDegrees = 30.0f;
        public const float FingerTipDegrees = 5.0f;

        /// <summary>
        /// The thumb: turned away from the fingers, and folded at its root and tip, degrees. The can's thumb lies along its side to the lid; the bottle's
        /// is turned further round its side (along it, it went over the shoulder into the glass).
        /// </summary>
        protected virtual float ThumbTurn => -30.0f;

        protected virtual float ThumbRootFold => HandGrip.ThumbProximalDegrees * 0.4f;

        protected virtual float ThumbTipFold => HandGrip.ThumbIntermediateDegrees * 0.4f;

        public FingerAmounts SupportHandGrip => FingerAmounts.Open;
    }
}

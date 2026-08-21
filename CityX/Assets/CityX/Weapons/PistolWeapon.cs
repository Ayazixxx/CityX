using CityX.Character;
using CityX.Items;
using CityX.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Weapons
{
    /// <summary>
    /// A pick-up-able semi-automatic pistol. Ported from weapons/pistol.gd.
    ///
    /// NOT AN IEquippableWeapon -- that interface is the melee/throw contract
    /// MeleeAttacker and the bottle/grapple hook share, and a gun has neither
    /// a swing nor a throw. pistol.gd's own class comment makes the same call
    /// for the same reason: hitscan + ammo/reload state is a different enough
    /// shape that forcing it into the melee contract would mean stubbing out
    /// half of it. PlayerInputReader checks EquippedGun directly instead,
    /// the same way it already checks GrappleSwing.EquippedHook.
    ///
    /// Copies Fortnite's classic pistol feel: semi-automatic (one shot per
    /// click), hitscan (instant raycast, not a travelling projectile), a
    /// magazine separate from a reserve pool, a timed reload, and hip-fire
    /// spread that tightens while aiming down sights. No headshot multiplier
    /// or damage falloff -- pistol.gd doesn't have either either.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class PistolWeapon : MonoBehaviour, IEquippableItem, IEquippableGun
    {
        public string ItemId => ItemRegistry.Pistol;

        // Matches the real Beretta M9's actual 15-round magazine.
        public const int MagazineSize = 15;
        public const int StartingReserveAmmo = 45;
        public const float Damage = 20.0f;
        public const float Range = 60.0f;

        /// <summary>Rounds per second, enforced as a cooldown rather than gated on click speed.</summary>
        public const float FireRatePerSecond = 5.5f;
        public const float FireCooldownSeconds = 1.0f / FireRatePerSecond;
        public const float ReloadSeconds = 1.5f;

        /// <summary>Half-angle of the hip-fire spread cone -- a shot lands anywhere within this of the aim.</summary>
        public const float HipFireSpreadDegrees = 3.0f;

        /// <summary>Tighter while aiming down sights. Not zero -- a pistol is never laser-accurate.</summary>
        public const float AdsSpreadDegrees = 0.5f;

        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f; // cos(35 degrees)

        public static readonly Vector3 GripHandPosition = new Vector3(0.02f, -0.05f, 0.10f);

        /// <summary>
        /// Not a direct port of pistol.gd's own GRIP_HAND_ROTATION -- that
        /// constant is in Godot's own radians/axis convention, and the
        /// bottle's grip (see BottleWeapon.GripHandRotationEuler) shows past
        /// sessions re-tuned rather than numerically converted these between
        /// engines. A reasonable starting pose (barrel forward, grip natural)
        /// rather than a false-precision conversion -- flagged, same as
        /// pistol.gd's own history flags GRIP_HAND_ROTATION as something that
        /// needed live in-hand tuning before it looked right.
        /// </summary>
        public static readonly Vector3 GripHandRotationEuler = new Vector3(-5.0f, 90.0f, 0.0f);

        /// <summary>
        /// pistol.gd's own GRIP_SCALE, ported directly (a plain uniform
        /// multiplier survives an engine change fine, unlike a rotation).
        /// The visual is normalized to the real Beretta M9's true 22cm
        /// length in NormalizeScale, which reads as a tiny nub next to this
        /// rig's own oversized hand -- this rig is taller than a real
        /// person even before the game's own stylised proportions, and the
        /// gun needs to read as a gun rather than true-to-life. Confirmed
        /// by rendering it without this: barely visible at the wrist.
        /// </summary>
        public const float GripScale = 2.515f;

        [Tooltip("Carries the imported Beretta_M9 mesh.")]
        public Transform VisualRoot;

        public GameObject MuzzleFlashObject;
        public Light MuzzleLight;
        public ParticleSystem MuzzleSparks;
        public AudioSource ShotPlayer;
        public AudioSource ReloadPlayer;

        [Tooltip("For a bullet landing on a body -- same prefab MeleeAttacker uses.")]
        public GameObject BloodPrefab;

        public LayerMask TargetLayers = ~0;

        public int MagazineAmmo { get; private set; } = MagazineSize;
        public int ReserveAmmo { get; private set; } = StartingReserveAmmo;
        public bool IsReloading { get; private set; }

        private float _fireCooldownRemaining;
        private float _reloadTimeRemaining;
        private float _muzzleFlashRemaining;
        private bool _equipped;
        private Transform _nearbyPlayer;
        private SphereCollider _pickupTrigger;

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
            if (MuzzleFlashObject != null)
            {
                MuzzleFlashObject.SetActive(false);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _fireCooldownRemaining = Mathf.Max(0.0f, _fireCooldownRemaining - deltaTime);

            if (IsReloading)
            {
                _reloadTimeRemaining -= deltaTime;
                if (_reloadTimeRemaining <= 0.0f)
                {
                    FinishReload();
                }
            }

            if (_muzzleFlashRemaining > 0.0f)
            {
                _muzzleFlashRemaining -= deltaTime;
                if (_muzzleFlashRemaining <= 0.0f && MuzzleFlashObject != null)
                {
                    MuzzleFlashObject.SetActive(false);
                }
            }

            if (_equipped)
            {
                UpdateHeldPose();
                return;
            }
            TickPickup();
        }

        /// <summary>
        /// The held grip pose, plus a procedural down-and-up dip while
        /// reloading -- pistol.gd's own _update_held_pose, since no dedicated
        /// reload mocap clip exists (unlike shoot/aim-in/locomotion, all real
        /// retargeted Mixamo clips). A sine envelope across ReloadSeconds so
        /// it reads as one continuous motion rather than a snap in and out.
        /// </summary>
        private void UpdateHeldPose()
        {
            transform.localPosition = GripHandPosition;
            transform.localEulerAngles = GripHandRotationEuler;
            transform.localScale = Vector3.one * GripScale;

            if (!IsReloading)
            {
                return;
            }
            float progress = Mathf.Clamp01(1.0f - _reloadTimeRemaining / ReloadSeconds);
            float dip = Mathf.Sin(progress * Mathf.PI);
            transform.localPosition += new Vector3(0.0f, -0.06f, 0.02f) * dip;
            transform.localEulerAngles += new Vector3(28.0f, 0.0f, 0.0f) * dip;
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

            Vector3 toSelf = transform.position - _nearbyPlayer.position;
            if (toSelf.sqrMagnitude > InteractRange * InteractRange)
            {
                return;
            }
            if (toSelf.sqrMagnitude > 0.0001f &&
                Vector3.Dot(_nearbyPlayer.forward, toSelf.normalized) < LookDotThreshold)
            {
                return;
            }

            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory == null)
            {
                return;
            }
            if (inventory.AddItem(ItemRegistry.Pistol, 1))
            {
                Destroy(gameObject);
            }
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
            if (other.transform == _nearbyPlayer || (other.GetComponentInParent<PlayerVitals>() != null))
            {
                _nearbyPlayer = null;
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
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedGun = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && ReferenceEquals(input.EquippedGun, this))
            {
                input.EquippedGun = null;
            }
            Destroy(gameObject);
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>
        /// Fires along <paramref name="direction"/> from <paramref name="origin"/>
        /// -- both the camera's own current world position/facing, same
        /// reasoning pistol.gd's own fire() callers use (the camera is
        /// genuinely where the player is aiming; MeleeAttacker's punch
        /// origin is the body instead, for a much shorter range where that
        /// distinction actually matters).
        ///
        /// Returns false (a dry/empty click) without firing if on cooldown,
        /// mid-reload, or the magazine is empty -- matches the "denied
        /// action does nothing" convention every other gated input in this
        /// project already follows.
        /// </summary>
        public bool Fire(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder)
        {
            if (IsReloading || _fireCooldownRemaining > 0.0f || MagazineAmmo <= 0)
            {
                return false;
            }
            _fireCooldownRemaining = FireCooldownSeconds;
            MagazineAmmo--;

            if (MuzzleFlashObject != null)
            {
                MuzzleFlashObject.SetActive(true);
            }
            _muzzleFlashRemaining = 0.05f;
            if (MuzzleSparks != null)
            {
                // Restart, not just Play() -- a burst still mid-flight from
                // the previous shot (very possible at this fire rate) would
                // otherwise keep playing uninterrupted and silently swallow
                // this one, the same issue player.gd's own shoot animation
                // hit (see pistol.gd's own comment on _muzzle_sparks.restart()).
                MuzzleSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                MuzzleSparks.Play();
            }
            if (ShotPlayer != null)
            {
                ShotPlayer.Play();
            }

            float spreadDegrees = aiming ? AdsSpreadDegrees : HipFireSpreadDegrees;
            Vector3 spreadDirection = ApplySpread(direction, spreadDegrees);

            if (Physics.Raycast(origin, spreadDirection, out RaycastHit hit, Range,
                    TargetLayers, QueryTriggerInteraction.Ignore))
            {
                var breakable = hit.collider.GetComponentInParent<IBreakableSurface>();
                if (breakable != null)
                {
                    breakable.TakePunch(hit.point);
                }
                else
                {
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        int strength = wielder != null
                            ? wielder.GetComponent<PlayerVitals>()?.Strength ?? 0
                            : 0;
                        float actualDamage = CombatTuning.DamageFor(Damage, strength);
                        // No hitstun -- pistol.gd's own take_damage() call never
                        // passes one either; a bullet interrupts nothing here.
                        damageable.ApplyHit(actualDamage, origin, 0.0f);
                        if (BloodPrefab != null)
                        {
                            BloodSplatter.Spawn(BloodPrefab, hit.point, origin - hit.point);
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Perturbs direction within a cone of half-angle spreadDegrees --
        /// two independent small-angle rotations around an arbitrary pair of
        /// axes perpendicular to direction, matching pistol.gd's own
        /// _apply_spread exactly.
        /// </summary>
        private static Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
        {
            if (spreadDegrees <= 0.0f)
            {
                return direction;
            }
            Vector3 helper = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.95f ? Vector3.up : Vector3.right;
            Vector3 right = Vector3.Cross(direction, helper).normalized;
            Vector3 up = Vector3.Cross(right, direction).normalized;
            float yaw = Random.Range(-spreadDegrees, spreadDegrees);
            float pitch = Random.Range(-spreadDegrees, spreadDegrees);
            Vector3 result = Quaternion.AngleAxis(yaw, up) * direction;
            result = Quaternion.AngleAxis(pitch, right) * result;
            return result.normalized;
        }

        /// <summary>
        /// No-op if already reloading, the magazine's already full, or
        /// there's no reserve ammo to reload from -- matches the plain
        /// "nothing happens" convention this project uses for every other
        /// denied action.
        /// </summary>
        public void TryReload()
        {
            if (IsReloading || MagazineAmmo >= MagazineSize || ReserveAmmo <= 0)
            {
                return;
            }
            IsReloading = true;
            _reloadTimeRemaining = ReloadSeconds;
            if (ReloadPlayer != null)
            {
                ReloadPlayer.Play();
            }
        }

        private void FinishReload()
        {
            IsReloading = false;
            int needed = MagazineSize - MagazineAmmo;
            int drawn = Mathf.Min(needed, ReserveAmmo);
            MagazineAmmo += drawn;
            ReserveAmmo -= drawn;
        }

        /// <summary>Read by the HUD's own ammo counter.</summary>
        public string GetAmmoText()
        {
            return IsReloading ? "RELOADING..." : $"{MagazineAmmo} / {ReserveAmmo}";
        }
    }
}

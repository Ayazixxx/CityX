using CityX.Character;
using CityX.Items;
using CityX.World;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// EVERY FIREARM: a pick-up-able gun whose numbers, shape and moving parts are its <see cref="FirearmProfile"/>. The Glock and the AK-47 are
    /// this component with different profiles on their prefabs (named PistolWeapon because it began as the only gun: pistol.gd's port).
    ///
    /// NOT AN IEquippableWeapon -- that interface is the melee/throw contract MeleeAttacker and the bottle/grapple hook share, and a gun has
    /// neither a swing nor a throw. PlayerInputReader checks EquippedGun directly.
    ///
    /// THE SHOT is hitscan along the crosshair (every bullet lands exactly where the crosshair is: the owner's ask), resolved by Monday Night
    /// Combat's three traces so a barrel behind a wall cannot shoot through it. THE FEEL (2026-10-14, the third-person-shooter research in
    /// docs/THIRD_PERSON_PISTOL.md): a learnable camera kick per shot from the profile's pattern (an AK's spray climbs and wanders), the gun
    /// shoved back and tipped up, the slide or bolt cycling, the trigger pulled by the trigger finger, a spent case flying out of the port.
    /// THE RELOAD is a motion made in Blender (CityX/SourceArt/Glock, /AK47) and played by name on the body (GunReloadDriver), so other players
    /// see it: the magazine drops (and lies on the floor), a fresh one is fetched and seated -- the ammo is in at the click -- and an empty gun
    /// has its action racked. Firing once the ammo is in cuts the rest of the motion short, as in Call of Duty's reload cancel.
    /// </summary>
    // AFTER PlayerLocomotion (order 0): the kick and the moving parts are put on the gun once the arms have been solved -- see LateUpdate.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class PistolWeapon : MonoBehaviour, IEquippableItem, IEquippableGun, ICosmeticMountable, IHandGripStyle
    {
        [Tooltip("What this gun is: its numbers, its hold, its points and parts. The builders set the Glock's or the AK-47's.")]
        public FirearmProfile Profile = FirearmProfile.Glock();

        public string ItemId => Profile != null ? Profile.ItemId : ItemRegistry.Pistol;

        // ---- the Glock's numbers, for the code and tests that predate the profile (FirearmProfile.Glock() has the same) ----
        public const int MagazineSize = 15;
        public const int StartingReserveAmmo = 45;
        public const float Damage = 20.0f;
        public const float Range = 60.0f;
        public const float FireRatePerSecond = 5.5f;
        public const float FireCooldownSeconds = 1.0f / FireRatePerSecond;

        /// <summary>The longest a reload is allowed when no motion times it (a bare test gun): the Glock's tactical one.</summary>
        public const float ReloadSeconds = 1.5f;

        /// <summary>
        /// No random spread, hip or aimed. Requested: "i want you to do bullets the same as csgo... make every shot hit exact where you
        /// clicked in the middle of your screen." The recoil moves the VIEW (the crosshair with it), never the bullet off the crosshair.
        /// </summary>
        public const float HipFireSpreadDegrees = 0.0f;

        public const float AdsSpreadDegrees = 0.0f;

        /// <summary>
        /// CS:GO'S INACCURACY (2026-10-14, so the Accuracy stat has something to work on): degrees of cone at a full sprint, in the air, and
        /// for every shot of a held trigger after the first (up to a limit). Standing or creeping, a first shot stays exact.
        /// </summary>
        public const float RunningSpreadDegrees = 2.5f;
        public const float AirborneSpreadDegrees = 4.0f;
        public const float SpraySpreadPerShotDegrees = 0.4f;
        public const float MaxSpraySpreadDegrees = 3.0f;

        /// <summary>Below this speed (metres a second) the body counts as still: creeping does not spread a shot.</summary>
        public const float StillSpeed = 1.0f;

        /// <summary>Aiming down the sights halves the cone.</summary>
        public const float AimedSpreadFraction = 0.5f;

        /// <summary>The cone a shot lands in, in degrees: the movement, the air and the held trigger, halved aiming, less Accuracy's share.</summary>
        public static float SpreadDegrees(float speed, float sprintSpeed, bool grounded, int shotInBurst, bool aiming, float accuracy)
        {
            float moving = sprintSpeed > StillSpeed ? Mathf.Clamp01((speed - StillSpeed) / (sprintSpeed - StillSpeed)) * RunningSpreadDegrees : 0.0f;
            float air = grounded ? 0.0f : AirborneSpreadDegrees;
            float spray = Mathf.Min(MaxSpraySpreadDegrees, Mathf.Max(0, shotInBurst) * SpraySpreadPerShotDegrees);
            float cone = (aiming ? AdsSpreadDegrees : HipFireSpreadDegrees) + moving + air + spray;
            if (aiming)
            {
                cone *= AimedSpreadFraction;
            }
            return cone * Mathf.Clamp01(1.0f - accuracy);
        }

        private float SpreadFor(GameObject wielder, bool aiming, int shotInBurst)
        {
            PlayerLocomotion locomotion = wielder != null ? wielder.GetComponent<PlayerLocomotion>() : null;
            PlayerVitals vitals = wielder != null ? wielder.GetComponent<PlayerVitals>() : null;
            float speed = locomotion != null ? locomotion.CurrentSpeed : 0.0f;
            float sprint = locomotion != null ? locomotion.SprintSpeed : 7.5f;
            bool grounded = locomotion == null || locomotion.IsGrounded;
            float accuracy = vitals != null ? vitals.Stats.Effect(StatId.Accuracy) : 0.0f;
            return SpreadDegrees(speed, sprint, grounded, shotInBurst, aiming, accuracy);
        }

        /// <summary>
        /// How far each shot's own pitch is randomised, as a fraction either side of the profile's pitch: overlapping plays of the identical
        /// waveform a few frames apart comb-filter into a stutter; a few percent decorrelates them without being heard as a pitch change.
        /// </summary>
        public const float ShotPitchJitter = 0.04f;

        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f; // cos(35 degrees)

        /// <summary>Below this the muzzle is effectively AT the target; no barrel trace (it would only find noise).</summary>
        public const float MinBarrelTraceMetres = 0.25f;

        /// <summary>Stop the barrel trace this short of the aim point, so it never reports the target's own surface as a blocker.</summary>
        public const float BarrelTraceMargin = 0.05f;

        /// <summary>A click on an empty gun with nothing to reload: how long before the next click registers.</summary>
        public const float DryClickCooldownSeconds = 0.25f;

        /// <summary>How long the trigger finger stays pulled after a shot, seconds.</summary>
        public const float TriggerPullSeconds = 0.07f;

        /// <summary>
        /// The OLD pistol's hold (the Beretta model, on its side and scaled 2.5x), kept because the grappling hook borrows it
        /// (GrapplingHookItem.GripHandPosition). The guns themselves are held by their profile (FirearmProfile.HoldPosition/HoldRotation).
        /// </summary>
        public static readonly Vector3 GripHandPosition = new Vector3(0.02f, -0.05f, 0.10f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(-5.0f, 90.0f, 180.0f);

        /// <summary>Where the Glock's free palm holds, in its own frame (the support hand's marker starts here).</summary>
        public static readonly Vector3 DefaultSupportGripLocal = FirearmProfile.Glock().SupportHandLocal;

        // ---- the built parts (PistolBuilder / AK47Builder) ----
        [Tooltip("Carries the model's parts.")]
        public Transform VisualRoot;

        [Tooltip("The slide or the bolt: slides back on every shot, is racked in an empty reload.")]
        public Transform ActionPart;

        public Transform MagazinePart;

        public Transform TriggerPart;

        public GameObject MuzzleFlashObject;

        [Tooltip("A crossbow (docs/ARCHERY.md): its shot is this arrow, seen flying to where the hitscan ended and sticking there, or falling on past the range (FirearmProfile.ArrowFallSpeed), instead of a tracer.")]
        public GameObject ArrowPrefab;
        public Light MuzzleLight;
        public ParticleSystem MuzzleSparks;
        public AudioSource ShotPlayer;

        [Tooltip("Plays the reload's own sounds: the magazine out, in, the action racked (FirearmEffects).")]
        public AudioSource ReloadPlayer;

        /// <summary>
        /// Extra voices, all sharing ShotPlayer's own clip, cycled round-robin: AudioSource.pitch belongs to the SOURCE, so re-pitching one
        /// shared source for a new shot warped every shot still ringing on it (the reported "glitchy, choppy" gunshots).
        /// </summary>
        public AudioSource[] ShotPlayerPool;

        private int _shotPoolIndex;

        public LayerMask TargetLayers = ~0;

        // ---- live-tunable feel (/gun), on the profile ----
        private float _recoilScale = 1.0f;

        /// <summary>How much faster the kick settles after the last shot: Recoil Control's (1 with none).</summary>
        private float _settleFaster = 1.0f;

        /// <summary>The first shot's camera kick, degrees; setting it scales the whole pattern.</summary>
        public float RecoilPitchDegrees
        {
            get => Profile.RecoilFor(0).y * _recoilScale;
            set => _recoilScale = value / Mathf.Max(0.01f, Profile.RecoilFor(0).y);
        }

        public float AdsRecoilMultiplier { get => Profile.AdsRecoilMultiplier; set => Profile.AdsRecoilMultiplier = value; }
        public float ShakeDegrees { get => Profile.ShakeDegrees; set => Profile.ShakeDegrees = value; }
        public float KickBackMetres { get => Profile.KickBackMetres; set => Profile.KickBackMetres = value; }
        public float KickUpDegrees { get => Profile.KickUpDegrees; set => Profile.KickUpDegrees = value; }
        public float KickRecoverPerSecond { get => Profile.KickRecoverPerSecond; set => Profile.KickRecoverPerSecond = value; }

        // ---- the hold, live-tunable with /pistol ----
        private bool _gripInitialised;
        private Vector3 _liveGripPosition;
        private Vector3 _liveGripRotation;
        private float _liveGripScale = 1.0f;

        public Vector3 LiveGripPosition => _liveGripPosition;
        public Vector3 LiveGripRotation => _liveGripRotation;
        public float LiveGripScale => _liveGripScale;

        public void SetLiveGripPosition(Vector3 value) => _liveGripPosition = value;
        public void SetLiveGripRotation(Vector3 value) => _liveGripRotation = value;
        public void SetLiveGripScale(float value) => _liveGripScale = value;

        // ---- state ----
        public int MagazineAmmo { get; private set; } = MagazineSize;
        public int ReserveAmmo { get; private set; } = StartingReserveAmmo;
        public bool IsReloading { get; private set; }

        /// <summary>How far through a timed reload, 0..1 (a reload a baked motion drives keeps its own clock: 0 here). The crossbow's spanning is keyed to it.</summary>
        public float ReloadProgress => IsReloading && !_reloadByMotion && _reloadDuration > 0.0f ? Mathf.Clamp01(1.0f - _reloadTimeRemaining / _reloadDuration) : 0.0f;

        public bool IsAutomatic => Profile != null && Profile.Automatic;

        /// <summary>The reload in progress is the empty-gun one (the action racked at the end).</summary>
        public bool IsEmptyReload => _reloadEmpty;

        /// <summary>The fresh magazine is in (the reload's "seat"): a shot now cuts the rest of the motion short.</summary>
        public bool ReloadAmmoIn => _reloadAmmoIn;

        public event System.Action<GunShot> ShotResolved;

        private float _kick;
        private float _fireCooldownRemaining;
        private float _reloadTimeRemaining;
        private float _reloadDuration;
        private bool _reloadEmpty;
        private bool _reloadAmmoIn;
        private bool _reloadReadyToFire;
        private bool _reloadByMotion;
        private bool _reloadRecording;   // the reload in progress plays the gun's own recording (FirearmProfile.ReloadSound), not the clicks
        private float _muzzleFlashRemaining;
        private bool _equipped;
        private Transform _nearbyPlayer;
        private SphereCollider _pickupTrigger;
        private int _burstIndex;
        private float _sinceShot = 999.0f;

        private GameObject _wielder;
        private PlayerLocomotion _locomotion;
        private PlayerAnimationDriver _animationDriver;
        private GunReloadDriver _reloader;
        private GameObject _cosmeticBody;
        private GunReloadDriver _cosmeticReloader;

        private Vector3 _actionRest;
        private Quaternion _triggerRest = Quaternion.identity;
        private Vector3 _magazineRest;
        private Quaternion _magazineRestRotation = Quaternion.identity;
        private bool _partsRestKnown;

        public bool HasNearbyPlayer => _nearbyPlayer != null;

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
            if (MuzzleFlashObject != null)
            {
                MuzzleFlashObject.SetActive(false);
            }
            if (Profile == null)
            {
                Profile = FirearmProfile.Glock();
            }
            MagazineAmmo = Profile.MagazineSize;
            ReserveAmmo = Profile.StartingReserveAmmo;
            InitialiseGrip();
            RememberPartRests();
        }

        private void InitialiseGrip()
        {
            if (_gripInitialised)
            {
                return;
            }
            _gripInitialised = true;
            _liveGripPosition = Profile.HoldPosition;
            _liveGripRotation = Profile.HoldRotation.eulerAngles;
            _liveGripScale = Profile.HoldScale;
        }

        private void RememberPartRests()
        {
            if (_partsRestKnown)
            {
                return;
            }
            _partsRestKnown = true;
            if (ActionPart != null) _actionRest = ActionPart.localPosition;
            if (TriggerPart != null) _triggerRest = TriggerPart.localRotation;
            if (MagazinePart != null)
            {
                _magazineRest = MagazinePart.localPosition;
                _magazineRestRotation = MagazinePart.localRotation;
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _fireCooldownRemaining = Mathf.Max(0.0f, _fireCooldownRemaining - deltaTime);
            _sinceShot += deltaTime;
            if (_sinceShot > Profile.RecoilResetSeconds)
            {
                _burstIndex = 0;
            }
            if (_kick > 0.0f)
            {
                _kick = Mathf.Max(0.0f, _kick - _kick * (1.0f - Mathf.Exp(-Profile.KickRecoverPerSecond * _settleFaster * deltaTime)) - 0.0005f);
            }

            if (IsReloading)
            {
                TickReload(deltaTime);
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

        /// <summary>The held grip pose; while a reload has no motion to play (a bare rig), the old procedural dip stands in for it.</summary>
        private void UpdateHeldPose()
        {
            transform.localPosition = _liveGripPosition;
            transform.localEulerAngles = _liveGripRotation;
            transform.localScale = Vector3.one * _liveGripScale;

            if (IsReloading && !_reloadByMotion && _reloadDuration > 0.0f)
            {
                float progress = Mathf.Clamp01(1.0f - _reloadTimeRemaining / _reloadDuration);
                float dip = Mathf.Sin(progress * Mathf.PI);
                transform.localPosition += new Vector3(0.0f, -0.06f, 0.02f) * dip;
                transform.localEulerAngles += new Vector3(28.0f, 0.0f, 0.0f) * dip;
            }
            // NOT the kick: that goes on in LateUpdate, after the arms are solved.
        }

        /// <summary>
        /// THE KICK AND THE MOVING PARTS ARE APPLIED LAST, after PlayerLocomotion has solved the arms against the UNKICKED gun: the arm's
        /// barrel correction would otherwise see the kick as an aiming error and snap the arm to cancel it (the old "arms spazz out when
        /// shooting"). The slide or bolt and the trigger are moved here too, unless a reload's motion is holding them.
        /// </summary>
        private void LateUpdate()
        {
            if (_equipped)
            {
                ApplyKick();
                ApplyMovingParts();
            }
        }

        private void ApplyKick()
        {
            if (_kick <= 0.0f || MuzzleFlashObject == null || transform.parent == null)
            {
                return;
            }
            Vector3 barrel = MuzzleFlashObject.transform.forward;
            Vector3 back = -barrel * (Profile.KickBackMetres * _kick);
            transform.localPosition += transform.parent.InverseTransformVector(back);
            Vector3 axis = Vector3.Cross(barrel, Vector3.up);
            if (axis.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.AngleAxis(Profile.KickUpDegrees * _kick, axis.normalized) * transform.rotation;
            }
        }

        /// <summary>A reload's motion is holding the parts (and the hands): the shot's own cycling keeps off them.</summary>
        private bool PartsHeldByMotion => _reloader != null && _reloader.Weight > 0.0f && ReferenceEquals(_reloader.Motion, Motion);

        /// <summary>
        /// The slide or bolt cycles on every shot -- back fast, home a little slower -- and the trigger is pulled for a moment. Real
        /// pistols' slides run back about their whole travel in a few hundredths of a second: at 60 fps it reads as a blur and a snap.
        /// </summary>
        private void ApplyMovingParts()
        {
            if (PartsHeldByMotion)
            {
                return;
            }
            RememberPartRests();
            float cycle = Profile.ActionCycleSeconds > 0.0f ? _sinceShot / Profile.ActionCycleSeconds : 1.0f;
            float back = cycle < 0.35f ? cycle / 0.35f : (cycle < 1.0f ? 1.0f - (cycle - 0.35f) / 0.65f : 0.0f);
            if (ActionPart != null)
            {
                ActionPart.localPosition = _actionRest + Vector3.back * (Profile.ActionTravel * back);
            }
            if (TriggerPart != null)
            {
                float pull = _sinceShot < TriggerPullSeconds ? 1.0f : 0.0f;
                TriggerPart.localRotation = _triggerRest * Quaternion.AngleAxis(Profile.TriggerPullDegrees * pull, Vector3.right);
            }
            if (MagazinePart != null && !IsReloading)
            {
                MagazinePart.localPosition = _magazineRest;
                MagazinePart.localRotation = _magazineRestRotation;
                if (!MagazinePart.gameObject.activeSelf)
                {
                    MagazinePart.gameObject.SetActive(true);
                }
            }
        }

        // ---- the fingers (IHandGripStyle: PlayerLocomotion.ApplyFingerGrips asks) ----

        /// <summary>
        /// The trigger finger lies straight along the frame while the gun is carried, rests on the trigger while it is aimed, and is pulled
        /// for a moment on each shot; the thumb lies forward along the frame (the thumbs-forward grip).
        /// </summary>
        public FingerAmounts StrongHandGrip
        {
            get
            {
                bool aiming = _locomotion != null && _locomotion.IsAimingGun;
                float trigger = _sinceShot < TriggerPullSeconds ? 1.0f : (aiming || _sinceShot < 0.4f ? 0.45f : 0.0f);
                return Profile.IsRifle ? FingerAmounts.RifleStrong(trigger) : FingerAmounts.PistolStrong(trigger);
            }
        }

        public FingerAmounts SupportHandGrip => Profile.IsRifle ? FingerAmounts.RifleSupport : FingerAmounts.PistolSupport;

        // ---- pickup ----

        private void TickPickup()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            bool ePressed = InteractKey.Tapped;
            TryPickup(_nearbyPlayer, ePressed);
        }

        public bool TryPickup(Transform player, bool ePressed)
        {
            if (!ePressed)
            {
                return false;
            }
            // RANGE FROM THE PLAYER'S OWN BODY, NOT THE CAMERA (the camera trails metres behind the body); the camera only says where they look.
            Vector3 toPlayer = transform.position - player.position;
            if (toPlayer.sqrMagnitude > InteractRange * InteractRange)
            {
                return false;
            }
            Transform lookSource = InteractionReach.LookSource(player);
            Vector3 toSelf = transform.position - lookSource.position;
            if (toSelf.sqrMagnitude > 0.0001f &&
                Vector3.Dot(lookSource.forward, toSelf.normalized) < LookDotThreshold)
            {
                return false;
            }
            var inventory = player.GetComponentInParent<Inventory>();
            if (inventory == null)
            {
                return false;
            }
            if (inventory.PickUp(ItemId))
            {
                Destroy(gameObject);
                return true;
            }
            return false;
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

        // ---- equip ----

        /// <summary>The marker the free hand reaches for, a child of the gun. Null while the gun is not held.</summary>
        public Transform LeftHandGrip => _leftHandGrip;

        private Transform _leftHandGrip;

        /// <summary>Where the free palm holds, in the gun's frame: a saved pistol hold if there is one, else the profile's.</summary>
        public Vector3 SupportGripLocal
        {
            get
            {
                WeaponGripSettings settings = WeaponGripSettings.Current;
                return !Profile.IsRifle && settings.PistolTuned ? settings.PistolLeftHand : Profile.SupportHandLocal;
            }
        }

        /// <summary>Puts the left-hand marker where the current hold (saved or derived) says.</summary>
        public void ApplyGrip()
        {
            if (_leftHandGrip != null)
            {
                _leftHandGrip.localPosition = SupportGripLocal;
            }
        }

        /// <summary>This gun's reload motion (loaded once, shared by every body), or null when there is none.</summary>
        public BakedMotion Motion => string.IsNullOrEmpty(Profile.MotionResource) ? null : BakedMotion.Load(Profile.MotionResource);

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            InitialiseGrip();
            var animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = _liveGripPosition;
                transform.localEulerAngles = _liveGripRotation;
                transform.localScale = Vector3.one * _liveGripScale;
            }
            RememberPartRests();

            _equipped = true;
            _wielder = player;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedGun = this;
            }

            // Camera kick, hit-confirm tick, dry click and the reticle's blocked state all hang off this one component, driven by ShotResolved.
            var feel = player.GetComponent<GunFeel>();
            if (feel == null)
            {
                feel = player.AddComponent<GunFeel>();
            }
            feel.Attach(this);

            _locomotion = player.GetComponent<PlayerLocomotion>();
            _animationDriver = player.GetComponent<PlayerAnimationDriver>();
            if (_locomotion != null && MuzzleFlashObject != null)
            {
                _locomotion.GunMuzzle = MuzzleFlashObject.transform;   // the arm aims the BARREL, not just the upper arm
            }
            if (_locomotion != null)
            {
                // The free hand reaches for a marker on the gun (draggable in the editor while the game runs: WeaponGripSettings): a pistol's
                // only once it is raised to aim, a rifle's always.
                if (_leftHandGrip == null)
                {
                    _leftHandGrip = GripMarker.Create(transform, "LeftHandGrip", "left hand", new Color(1.0f, 0.55f, 0.1f));
                }
                ApplyGrip();
                _locomotion.SetSupportHand(_leftHandGrip, Vector3.zero, onlyWhenAiming: Profile.SupportHandOnlyWhenAiming);
                if (Profile.IsRifle)
                {
                    _locomotion.SetRifle(transform, Profile.ButtLocal, Profile.MuzzleLocal, Profile.HoldPosition, Profile.HoldRotation);
                }
            }
            HookReload(player, ref _reloader);
            FirearmEffects.Warm();
            BulletTracer.Warm();
        }

        /// <summary>Lends the body this gun's reload motion and parts, and listens for the motion's events (on an owner's body and a copy alike).</summary>
        private void HookReload(GameObject body, ref GunReloadDriver reloader)
        {
            var locomotion = body.GetComponent<PlayerLocomotion>();
            var driver = body.GetComponent<PlayerAnimationDriver>();
            reloader = driver != null ? driver.GunReloader : body.GetComponent<GunReloadDriver>();
            if (locomotion != null)
            {
                locomotion.RegisterBakedProp(Profile.ActionPartName, ActionPart);
                locomotion.RegisterBakedProp(Profile.MagazinePartName, MagazinePart);
                locomotion.RegisterBakedProp(Profile.TriggerPartName, TriggerPart);
            }
            if (reloader != null)
            {
                reloader.SetMotion(Motion);
                reloader.Marker -= OnReloadMarker;
                reloader.Marker += OnReloadMarker;
                reloader.Finished -= OnReloadFinished;
                reloader.Finished += OnReloadFinished;
                reloader.Started -= OnReloadStarted;
                reloader.Started += OnReloadStarted;
                reloader.SpeedChanged -= SetReloadRecordingSpeed;
                reloader.SpeedChanged += SetReloadRecordingSpeed;
            }
        }

        private void UnhookReload(GameObject body, GunReloadDriver reloader)
        {
            var locomotion = body != null ? body.GetComponent<PlayerLocomotion>() : null;
            if (locomotion != null)
            {
                if (locomotion.BakedProp(Profile.ActionPartName) == ActionPart) locomotion.UnregisterBakedProp(Profile.ActionPartName);
                if (locomotion.BakedProp(Profile.MagazinePartName) == MagazinePart) locomotion.UnregisterBakedProp(Profile.MagazinePartName);
                if (locomotion.BakedProp(Profile.TriggerPartName) == TriggerPart) locomotion.UnregisterBakedProp(Profile.TriggerPartName);
            }
            if (reloader != null)
            {
                reloader.Marker -= OnReloadMarker;
                reloader.Finished -= OnReloadFinished;
                reloader.Started -= OnReloadStarted;
                reloader.SpeedChanged -= SetReloadRecordingSpeed;
                if (ReferenceEquals(reloader.Motion, Motion))
                {
                    reloader.SetMotion(null);
                }
            }
        }

        public void OnUnequip(GameObject player)
        {
            StopReloadRecording();   // put away mid-reload
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && ReferenceEquals(input.EquippedGun, this))
            {
                input.EquippedGun = null;
            }
            var feel = player.GetComponent<GunFeel>();
            if (feel != null)
            {
                feel.Detach(this);
            }
            var locomotionOnUnequip = player.GetComponent<PlayerLocomotion>();
            if (locomotionOnUnequip != null)
            {
                locomotionOnUnequip.ClearSupportHand(_leftHandGrip);
                locomotionOnUnequip.ClearRifle(transform);
                if (locomotionOnUnequip.GunMuzzle == (MuzzleFlashObject != null ? MuzzleFlashObject.transform : null))
                {
                    locomotionOnUnequip.GunMuzzle = null;
                }
            }
            UnhookReload(player, _reloader);
            _reloader = null;
            Destroy(gameObject);
        }

        // ---- on another player's copy: the model in the right hand, its parts lent to the copy's reload, a rifle held two-handed ----
        public void MountCosmetic(GameObject body)
        {
            InitialiseGrip();
            CosmeticMount.ToBone(transform, body, HumanBodyBones.RightHand, _liveGripPosition, _liveGripRotation, _liveGripScale);
            RememberPartRests();
            _cosmeticBody = body;
            HookReload(body, ref _cosmeticReloader);
            _cosmeticDriver = body.GetComponent<PlayerAnimationDriver>();
            if (_cosmeticDriver != null)
            {
                _cosmeticDriver.RemoteGunFired -= OnRemoteShot;
                _cosmeticDriver.RemoteGunFired += OnRemoteShot;
            }
            var locomotion = body.GetComponent<PlayerLocomotion>();
            if (locomotion != null && Profile.IsRifle)
            {
                if (_leftHandGrip == null)
                {
                    _leftHandGrip = GripMarker.Create(transform, "LeftHandGrip", "left hand", new Color(1.0f, 0.55f, 0.1f));
                }
                ApplyGrip();
                locomotion.SetSupportHand(_leftHandGrip, Vector3.zero, onlyWhenAiming: false);
                locomotion.SetRifle(transform, Profile.ButtLocal, Profile.MuzzleLocal, Profile.HoldPosition, Profile.HoldRotation);
            }
        }


        // ---- a shot on another player's copy (GunReload.FireName, PlayerAnimationDriver.RemoteGunFired) ----

        private PlayerAnimationDriver _cosmeticDriver;
        private int _remoteShotIndex;
        private float _lastRemoteShot = -999.0f;

        /// <summary>Whether the n-th round of a burst draws a tracer: the first always, then every TracerEvery-th (Counter-Strike's rifles: every third).</summary>
        public bool ShowsTracer(int shotInBurst) => Profile.TracerEvery <= 1 || shotInBurst % Profile.TracerEvery == 0;

        /// <summary>
        /// A crossbow's bolt (docs/ARCHERY.md): seen flying from the muzzle to where the shot ended and stuck in <paramref name="landedOn"/> there,
        /// or falling on as a real projectile past the range where it met nothing; the string's clack with it. The hit itself was the hitscan's.
        /// </summary>
        private void ShowArrow(Vector3 from, Vector3 end, Collider landedOn, Vector3 direction, GameObject wielder, bool cosmetic)
        {
            PlayerVitals shooter = wielder != null ? wielder.GetComponent<PlayerVitals>() : null;
            float damage = CombatTuning.DamageFor(Profile.Damage, shooter != null ? shooter.Stats[StatId.FirearmDamage] : 0);
            float penetration = shooter != null ? shooter.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            Collider[] own = wielder != null ? wielder.GetComponentsInChildren<Collider>() : null;
            ArrowProjectile.Show(ArrowPrefab, from, end, landedOn, direction, Profile.ArrowFallSpeed, damage, penetration, wielder, own, cosmetic);
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcherySounds.Clack, from, 0.9f);
            }
        }

        /// <summary>
        /// The player this copy belongs to fired: the flash, the crack (in the world, so it is quieter far away), the tracer down the barrel
        /// to whatever it meets, the brass and the slide or bolt -- everything the shooter saw, but no damage (that went its own way).
        /// </summary>
        private void OnRemoteShot()
        {
            if (_cosmeticBody == null)
            {
                return;
            }
            if (_cosmeticReloader != null && _cosmeticReloader.IsPlaying)
            {
                _cosmeticReloader.Cancel();   // the shooter cut their reload short with this shot
            }
            Transform muzzle = MuzzleFlashObject != null ? MuzzleFlashObject.transform : transform;
            FirearmEffects.Flash(MuzzleFlashObject, 0.05f);
            if (MuzzleSparks != null)
            {
                MuzzleSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                MuzzleSparks.Play();
            }
            if (ShotPlayer != null && ShotPlayer.clip != null)
            {
                AudioSource.PlayClipAtPoint(ShotPlayer.clip, muzzle.position, 1.0f);
            }
            Vector3 end = muzzle.position + muzzle.forward * Profile.Range;
            Collider landedOn = null;
            if (Physics.Raycast(muzzle.position, muzzle.forward, out RaycastHit hit, Profile.Range, TargetLayers, QueryTriggerInteraction.Ignore)
                && !IsWieldersOwnCollider(hit.collider, _cosmeticBody))
            {
                // On through the windows the shooter's bullet went through (only looked for: the glass broke on the shooter's machine).
                Vector3 forward = muzzle.forward;
                bool onSomething = GlassPenetration.Follow(ref hit, forward, Profile.Range - hit.distance, false,
                    (Vector3 from, float distance, out RaycastHit next) =>
                        Physics.Raycast(from, forward, out next, distance, TargetLayers, QueryTriggerInteraction.Ignore),
                    out _, out end);
                landedOn = onSomething ? hit.collider : null;
            }
            _remoteShotIndex = Time.time - _lastRemoteShot <= Profile.RecoilResetSeconds ? _remoteShotIndex + 1 : 0;
            _lastRemoteShot = Time.time;
            if (ArrowPrefab != null)
            {
                ShowArrow(muzzle.position, end, landedOn, muzzle.forward, _cosmeticBody, true);
            }
            else if (ShowsTracer(_remoteShotIndex))
            {
                BulletTracer.Spawn(muzzle.position, end, muzzle);
            }
            if (Profile.CasingLength > 0.0f)
            {
                FirearmEffects.EjectCasing(transform.TransformPoint(Profile.EjectionPortLocal), transform.rotation,
                    transform.TransformDirection(Profile.EjectionDirectionLocal), Profile.CasingLength, Profile.CasingRadius, _cosmeticBody);
            }
            RememberPartRests();
            FirearmEffects.Cycle(ActionPart, _actionRest, Profile.ActionTravel, Profile.ActionCycleSeconds);
        }
        public void UnmountCosmetic(GameObject body)
        {
            var locomotion = body != null ? body.GetComponent<PlayerLocomotion>() : null;
            if (locomotion != null)
            {
                locomotion.ClearSupportHand(_leftHandGrip);
                locomotion.ClearRifle(transform);
            }
            UnhookReload(body, _cosmeticReloader);
            if (_cosmeticDriver != null)
            {
                _cosmeticDriver.RemoteGunFired -= OnRemoteShot;
                _cosmeticDriver = null;
            }
            _cosmeticReloader = null;
            _cosmeticBody = null;
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        // ---- the reload ----

        /// <summary>
        /// Starts a reload: no-op if already reloading, the magazine is full, or there is no reserve. An empty gun gets the EMPTY reload (the
        /// action racked at the end). Played as a motion by name on the body (so other players see it); a body without one (a bare test rig)
        /// runs the old timer with a dip of the gun.
        /// </summary>
        public void TryReload()
        {
            if (IsReloading || MagazineAmmo >= Profile.MagazineSize || ReserveAmmo <= 0)
            {
                return;
            }
            _reloadEmpty = MagazineAmmo <= 0;
            _reloadAmmoIn = false;
            _reloadReadyToFire = false;
            IsReloading = true;

            // Reload Speed (2026-10-14): the whole reload -- the motion, its sounds, the timer -- runs this much faster.
            float speed = PlayerVitals.SpeedOf(WielderVitals, StatId.ReloadSpeed);
            string state = GunReload.NameFor(_reloadEmpty);
            if (_reloader != null && _reloader.CanPlay(_reloadEmpty) && _animationDriver != null && _animationDriver.HasState(state))
            {
                _animationDriver.PlayLocalState(state);
                if (!Mathf.Approximately(speed, 1.0f))
                {
                    _animationDriver.SetStateSpeed(state, speed);   // the motion's clock, sent on to other players
                }
                if (_reloader.IsPlaying)
                {
                    _reloadByMotion = true;
                    _reloadDuration = _reloader.Duration;
                    return;
                }
            }
            _reloadByMotion = false;
            _reloadDuration = (_reloadEmpty ? Profile.EmptyReloadSeconds : Profile.ReloadSeconds) / speed;
            _reloadTimeRemaining = _reloadDuration;
            PlayReloadRecording(_reloadEmpty);   // as long as the timer (ReloadSeconds, EmptyReloadSeconds)
            SetReloadRecordingSpeed(speed);
        }

        private void TickReload(float deltaTime)
        {
            if (_reloadByMotion)
            {
                // The motion was cut short by something taking the body (a ragdoll, a climb): the magazine is in only if it had seated.
                if (_reloader == null || !_reloader.IsPlaying)
                {
                    StopReloadRecording();
                    EndReload();
                }
                return;
            }
            _reloadTimeRemaining -= deltaTime;
            if (_reloadTimeRemaining <= 0.0f)
            {
                Refill();
                EndReload();
            }
        }

        /// <summary>The motion's events, raised by the body's GunReloadDriver as their frames pass (here and on other players' copies).</summary>
        private void OnReloadMarker(string marker)
        {
            switch (GunReload.EventKind(marker))
            {
                case GunReload.DropEvent:
                    FirearmEffects.DropMagazine(MagazinePart, transform.TransformDirection(Profile.MagazineOutLocal), _wielder != null ? _wielder : _cosmeticBody);
                    if (!_reloadRecording) PlayReloadSound(FirearmEffects.MagazineOut);
                    break;
                case GunReload.SeatEvent:
                    if (!_reloadRecording) PlayReloadSound(FirearmEffects.MagazineIn);
                    if (_equipped && IsReloading)
                    {
                        Refill();
                        _reloadAmmoIn = true;
                        _reloadReadyToFire = !_reloadEmpty;
                    }
                    break;
                case GunReload.RackEvent:
                    if (!_reloadRecording) PlayReloadSound(FirearmEffects.Rack);
                    if (_equipped && IsReloading)
                    {
                        _reloadReadyToFire = _reloadAmmoIn;
                    }
                    break;
            }
        }

        private void OnReloadFinished()
        {
            if (_equipped && IsReloading && _reloadByMotion)
            {
                if (!_reloadAmmoIn)
                {
                    Refill();   // a motion without a seat event still reloads
                }
                EndReload();
            }
        }

        /// <summary>
        /// A reload began, here or on another player's copy (GunReloadDriver.Started). A gun with its own recording of a reload (owner, 2026-10-14:
        /// the AK-47's, sped up to the motion's length at its own pitch) plays it from the start, and the motion's events make no clicks of their own.
        /// </summary>
        private void OnReloadStarted(bool empty)
        {
            PlayReloadRecording(empty);
        }

        private void PlayReloadRecording(bool empty)
        {
            AudioClip clip = Profile.ReloadSoundFor(empty);
            _reloadRecording = clip != null && ReloadPlayer != null;
            if (!_reloadRecording || !ReloadPlayer.isActiveAndEnabled)
            {
                return;
            }
            ReloadPlayer.Stop();
            ReloadPlayer.clip = clip;
            ReloadPlayer.pitch = 1.0f;
            ReloadPlayer.Play();
        }

        /// <summary>
        /// A faster reload (Reload Speed) plays its recording faster too, so the sounds stay on the motion -- which raises its pitch with it
        /// (+10% speed, a little higher). Here and on other players' copies (GunReloadDriver.SpeedChanged).
        /// </summary>
        private void SetReloadRecordingSpeed(float speed)
        {
            if (_reloadRecording && ReloadPlayer != null && speed > 0.0f)
            {
                ReloadPlayer.pitch = speed;
            }
        }

        /// <summary>The body holding this gun's vitals (its stats), or null when nothing holds it.</summary>
        private PlayerVitals WielderVitals => _wielder != null ? _wielder.GetComponent<PlayerVitals>() : null;

        /// <summary>A reload cut short (a shot once the fresh magazine is in, the body taken by a climb or a fall, the gun put away) stops its recording.</summary>
        private void StopReloadRecording()
        {
            if (_reloadRecording && ReloadPlayer != null)
            {
                ReloadPlayer.Stop();
            }
            _reloadRecording = false;
        }

        private void PlayReloadSound(AudioClip clip)
        {
            AudioSource source = ReloadPlayer != null ? ReloadPlayer : ShotPlayer;
            if (source != null && clip != null && source.isActiveAndEnabled)
            {
                source.PlayOneShot(clip, 0.8f);
            }
        }

        private void Refill()
        {
            int needed = Profile.MagazineSize - MagazineAmmo;
            int drawn = Mathf.Min(needed, ReserveAmmo);
            MagazineAmmo += drawn;
            ReserveAmmo -= drawn;
        }

        /// <summary>Sets the rounds in the magazine and in reserve (clamped to what fits): ammo found, a test rig's setup.</summary>
        public void SetAmmo(int magazine, int reserve)
        {
            MagazineAmmo = Mathf.Clamp(magazine, 0, Profile.MagazineSize);
            ReserveAmmo = Mathf.Max(0, reserve);
        }

        private void EndReload()
        {
            IsReloading = false;
            _reloadByMotion = false;
            _reloadReadyToFire = false;
        }

        /// <summary>Read by the HUD's own ammo counter.</summary>
        public string GetAmmoText()
        {
            return IsReloading && !_reloadAmmoIn ? "RELOADING..." : $"{MagazineAmmo} / {ReserveAmmo}";
        }

        // ---- the shot ----

        /// <summary>
        /// Fires along <paramref name="direction"/> from <paramref name="origin"/> (the camera's position and facing: the camera is where the
        /// player aims). Returns false without firing on cooldown, mid-reload (until the fresh magazine is in), or on an empty magazine (a dry
        /// click, which starts a reload when there is reserve: Gears, Fortnite and The Division all do).
        /// </summary>
        public bool Fire(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder)
        {
            if (_fireCooldownRemaining > 0.0f)
            {
                return false;
            }
            if (IsReloading)
            {
                if (!_reloadReadyToFire)
                {
                    return false;
                }
                // The reload cancel: the ammo is in (and racked), so the rest of the motion gives way to the shot.
                if (_reloader != null && _reloader.IsPlaying)
                {
                    _reloader.Cancel();
                }
                StopReloadRecording();
                EndReload();
            }

            if (MagazineAmmo <= 0)
            {
                bool reloading = false;
                if (ReserveAmmo > 0)
                {
                    TryReload();
                    reloading = IsReloading;
                }
                _fireCooldownRemaining = DryClickCooldownSeconds;
                ShotResolved?.Invoke(new GunShot
                {
                    DryClick = true,
                    AutoReloading = reloading,
                    Aiming = aiming,
                });
                return false;
            }

            // Fire Rate shortens the gap between shots (2026-10-14).
            _fireCooldownRemaining = Profile.FireCooldownSeconds / PlayerVitals.SpeedOf(wielder != null ? wielder.GetComponent<PlayerVitals>() : null, StatId.FireRate);
            MagazineAmmo--;
            int shotInBurst = _sinceShot <= Profile.RecoilResetSeconds ? _burstIndex : 0;
            _burstIndex = shotInBurst + 1;
            _sinceShot = 0.0f;

            if (MuzzleFlashObject != null)
            {
                MuzzleFlashObject.SetActive(true);
            }
            _muzzleFlashRemaining = 0.05f;
            if (MuzzleSparks != null)
            {
                // Restart, not just Play(): a burst still in flight from the last shot would swallow this one.
                MuzzleSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                MuzzleSparks.Play();
            }
            // PlayOneShot on a pooled voice: an independent, overlapping play per shot (see ShotPlayerPool).
            AudioSource voice = NextShotVoice();
            if (voice != null && voice.clip != null)
            {
                voice.pitch = Profile.ShotPitch * RandomShotPitch();
                voice.PlayOneShot(voice.clip);
            }

            // SPREAD (2026-10-14, for Accuracy): still exact on a standing first shot -- the owner's CS:GO ask -- but, as in CS:GO itself,
            // running, jumping and holding the trigger down open a cone the shots land in. Accuracy narrows it.
            float spreadDegrees = SpreadFor(wielder, aiming, shotInBurst);
            Vector3 spreadDirection = ApplySpread(direction, spreadDegrees);

            // HITSCAN (CS:GO's zero travel time), THE THREE-TRACE SHOT -- see ResolveTrace.
            ShotTrace trace = ResolveTrace(origin, spreadDirection, wielder);
            RaycastHit hit = trace.Hit
                ? trace.HitInfo
                : default;
            bool realHit = trace.Hit;
            Collider arrowStuckIn = null;
            bool hitDamageable = false;
            bool killed = false;
            Vector3 shotEnd = origin + spreadDirection * Profile.Range;

            if (realHit)
            {
                // A BULLET, not a punch: it makes its own hole in each window and goes on through it (GlassPenetration), a little weaker for each.
                bool onSomething = GlassPenetration.Follow(ref hit, spreadDirection, Profile.Range - Vector3.Distance(origin, hit.point), true,
                    (Vector3 from, float distance, out RaycastHit next) => TraceIgnoringWielder(from, spreadDirection, distance, wielder, out next),
                    out int panes, out shotEnd);
                if (onSomething)
                {
                    arrowStuckIn = hit.collider;
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        // Firearm Damage makes a shot hurt more (2% a point; it scaled with Strength, the melee stat, until 2026-10-14), and the
                        // shooter's Armor Penetration goes with it: a bullet is a blow armour stops and a block can deflect.
                        PlayerVitals shooter = wielder != null ? wielder.GetComponent<PlayerVitals>() : null;
                        int firearmDamage = shooter != null ? shooter.Stats[StatId.FirearmDamage] : 0;
                        float penetration = shooter != null ? shooter.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
                        float actualDamage = CombatTuning.DamageFor(Profile.Damage, firearmDamage) * GlassPenetration.DamageShare(panes);
                        // No hitstun: a bullet interrupts nothing (pistol.gd's own take_damage passes none either).
                        bool wasAlive = damageable.IsAlive;
                        DamageContext.Strike(damageable, actualDamage, origin, 0.0f, DamageKind.Bullet, penetration, hit.point, wielder);
                        hitDamageable = true;
                        killed = !damageable.IsAlive;
                        // A kill pays its XP, as a fist's or a sword's does (2026-10-15: a Shoota is worth some; a civilian always was). Once:
                        // a round into a body already dead pays nothing.
                        if (killed && wasAlive && shooter != null && damageable is IXpReward reward)
                        {
                            shooter.AddXp(reward.XpReward);
                        }
                        if (!(damageable is PlayerVitals) && !(damageable is IBloodless))
                        {
                            BloodBurst.Spawn(hit.point, hit.point - origin, 0.6f);   // a body with vitals shows its own wound (HitImpactEffects)
                        }
                    }
                }
            }

            // THE TRACER IS THE BULLET'S PATH: straight from the muzzle to where the shot really ended, whatever the mode (the arm points the
            // gun at the same point: PlayerLocomotion.NotifyGunFired and LocomotionInput.GunAimPoint).
            Vector3 muzzlePosition = MuzzleFlashObject != null ? MuzzleFlashObject.transform.position : origin;
            if (ArrowPrefab != null)
            {
                ShowArrow(muzzlePosition, shotEnd, arrowStuckIn, spreadDirection, wielder, false);   // a crossbow's bolt, in place of a tracer
            }
            else if (ShowsTracer(shotInBurst))
            {
                BulletTracer.Spawn(muzzlePosition, shotEnd, MuzzleFlashObject != null ? MuzzleFlashObject.transform : null);
            }

            // Other players' copies of this body see and hear the shot (the damage went its own way: NetworkedDamage).
            if (_animationDriver != null && _equipped)
            {
                _animationDriver.AnnounceState(GunReload.FireName);
            }

            // The spent case out of the port (a crossbow has none).
            if (_equipped && Profile.CasingLength > 0.0f)
            {
                Vector3 port = transform.TransformPoint(Profile.EjectionPortLocal);
                FirearmEffects.EjectCasing(port, transform.rotation, transform.TransformDirection(Profile.EjectionDirectionLocal),
                    Profile.CasingLength, Profile.CasingRadius, wielder);
            }
            if (Profile.ReloadsAfterEveryShot && MagazineAmmo <= 0)
            {
                TryReload();   // a crossbow spans itself the moment it fires (docs/ARCHERY.md)
            }

            // THE KICK. Camera recoil is the camera's job (whoever listens to ShotResolved applies it, from the profile's pattern); the gun's
            // own snap-back is ours. Applied AFTER the shot resolved, so the shot itself is always exactly on the crosshair.
            // Recoil Control (2026-10-14) takes its share off the kick, the camera's climb and shake and how long the climb holds.
            PlayerVitals shooterVitals = wielder != null ? wielder.GetComponent<PlayerVitals>() : null;
            float steadier = shooterVitals != null ? shooterVitals.Stats.Decrease(StatId.RecoilControl) : 1.0f;
            _kick = steadier;
            _settleFaster = shooterVitals != null ? shooterVitals.Stats.Increase(StatId.RecoilControl) : 1.0f;
            Vector2 recoil = Profile.RecoilFor(shotInBurst) * _recoilScale * (aiming ? Profile.AdsRecoilMultiplier : 1.0f) * steadier;
            ShotResolved?.Invoke(new GunShot
            {
                Fired = true,
                HitSomething = realHit,
                HitDamageable = hitDamageable,
                Killed = killed,
                BarrelBlocked = trace.BarrelBlocked,
                Point = shotEnd,
                Aiming = aiming,
                RecoilPitchDegrees = recoil.y,
                RecoilYawDegrees = recoil.x,
                RecoilHoldSeconds = Profile.RecoilHoldSeconds * steadier,
                ShakeDegrees = Profile.ShakeDegrees * (aiming ? Profile.AdsRecoilMultiplier : 1.0f) * steadier,
            });
            return true;
        }

        /// <summary>Seconds before the next shot can go (Fire Rate shortens the wait).</summary>
        public float FireCooldownRemaining => _fireCooldownRemaining;

        /// <summary>How many shots of the current burst have been fired (0 once it has reset).</summary>
        public int ShotsInBurst => _sinceShot <= Profile.RecoilResetSeconds ? _burstIndex : 0;

        private struct ShotTrace
        {
            public bool Hit;
            public RaycastHit HitInfo;
            public bool BarrelBlocked;
        }

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        /// <summary>
        /// The nearest thing a bullet stops on along a ray, skipping the shooter's own colliders (ragdoll bones included) and anything a shot
        /// ignores; a cracked window's opening trigger counts (it is the only thing a bullet can hit there).
        /// </summary>
        private bool TraceIgnoringWielder(Vector3 origin, Vector3 direction, float distance,
            GameObject wielder, out RaycastHit best)
        {
            best = default;
            if (distance <= 0.0f)
            {
                return false;
            }

            RaycastHit[] hits = HitBuffer;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance,
                TargetLayers, QueryTriggerInteraction.Collide);
            if (count >= hits.Length)
            {
                // A full buffer may have dropped the nearest hit: pay for the exact answer.
                hits = Physics.RaycastAll(origin, direction, distance, TargetLayers, QueryTriggerInteraction.Collide);
                count = hits.Length;
            }
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = hits[i];
                if (candidate.collider == null || IsWieldersOwnCollider(candidate.collider, wielder)
                    || IgnoredByShots.Covers(candidate.collider))
                {
                    continue;
                }
                if (candidate.collider.isTrigger && candidate.collider.GetComponent<IShootableTrigger>() == null)
                {
                    continue;   // a pickup sphere, a rain or room volume: not something a bullet stops on
                }
                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    best = candidate;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// MONDAY NIGHT COMBAT'S THREE-TRACE SHOT: (1) the CAMERA trace is the shot (the crosshair wins in open ground); (2) a HAND trace from
        /// the body to the muzzle -- scenery there means the barrel is inside a wall, and it takes the bullet; (3) a BARREL trace from the muzzle
        /// to the camera's hit point -- scenery other than the target takes the bullet. With no wielder or no muzzle only (1) runs.
        /// </summary>
        private ShotTrace ResolveTrace(Vector3 origin, Vector3 direction, GameObject wielder)
        {
            var result = new ShotTrace();
            float range = Profile.Range;
            result.Hit = TraceIgnoringWielder(origin, direction, range, wielder, out RaycastHit camera);
            result.HitInfo = camera;

            if (wielder == null || MuzzleFlashObject == null)
            {
                return result;
            }

            Vector3 muzzle = MuzzleFlashObject.transform.position;
            Vector3 aimPoint = result.Hit ? camera.point : origin + direction * range;

            var locomotion = wielder.GetComponent<PlayerLocomotion>();
            float bodyHeight = (locomotion != null ? locomotion.StandingHeight : 1.8f) * 0.6f;
            Vector3 body = wielder.transform.position + Vector3.up * bodyHeight;
            Vector3 toMuzzle = muzzle - body;
            float toMuzzleDistance = toMuzzle.magnitude;
            if (toMuzzleDistance > 0.01f
                && TraceIgnoringWielder(body, toMuzzle / toMuzzleDistance, toMuzzleDistance, wielder, out RaycastHit hand))
            {
                result.Hit = true;
                result.HitInfo = hand;
                result.BarrelBlocked = true;
                return result;
            }

            Vector3 toAim = aimPoint - muzzle;
            float aimDistance = toAim.magnitude;
            if (aimDistance > MinBarrelTraceMetres && Vector3.Dot(toAim, direction) > 0.0f
                && TraceIgnoringWielder(muzzle, toAim / aimDistance, aimDistance - BarrelTraceMargin, wielder,
                    out RaycastHit blocker)
                && !(result.Hit && blocker.collider == camera.collider))
            {
                result.Hit = true;
                result.HitInfo = blocker;
                result.BarrelBlocked = true;
            }
            return result;
        }

        public bool IsBarrelBlocked(Vector3 origin, Vector3 direction, GameObject wielder)
        {
            return ResolveTrace(origin, direction, wielder).BarrelBlocked;
        }

        /// <summary>
        /// Where a shot fired now would END (the hit point, or a far point on the crosshair ray on a miss) and whether scenery is taking it.
        /// The arm aims the gun at this point, so the barrel points where the bullet and its tracer go.
        /// </summary>
        public void AimTrace(Vector3 origin, Vector3 direction, GameObject wielder,
            out Vector3 point, out bool barrelBlocked)
        {
            ShotTrace trace = ResolveTrace(origin, direction, wielder);
            point = trace.Hit ? trace.HitInfo.point : origin + direction * Profile.Range;
            barrelBlocked = trace.BarrelBlocked;
        }

        /// <summary>
        /// "whenever i hold right click with the pistol the crosshair is on me and just shoots me": checked by identity against the wielder's
        /// hierarchy (the ragdoll has colliders on every bone, on several layers), walking up to PlayerVitals.
        /// </summary>
        private static bool IsWieldersOwnCollider(Collider hitCollider, GameObject wielder)
        {
            if (wielder == null)
            {
                return false;
            }
            PlayerVitals hitOwner = hitCollider.GetComponentInParent<PlayerVitals>();
            if (hitOwner == null)
            {
                return false;
            }
            PlayerVitals wielderVitals = wielder.GetComponent<PlayerVitals>();
            return wielderVitals != null && hitOwner == wielderVitals;
        }

        public static float RandomShotPitch()
        {
            return 1.0f + Random.Range(-ShotPitchJitter, ShotPitchJitter);
        }

        private AudioSource NextShotVoice()
        {
            if (ShotPlayerPool == null || ShotPlayerPool.Length == 0)
            {
                return ShotPlayer;
            }
            _shotPoolIndex = (_shotPoolIndex + 1) % ShotPlayerPool.Length;
            return ShotPlayerPool[_shotPoolIndex];
        }

        /// <summary>Perturbs direction within a cone of half-angle spreadDegrees (pistol.gd's _apply_spread). Zero spread leaves it exact.</summary>
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
    }
}

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
    // AFTER PlayerLocomotion (order 0): the kick is put on the gun once the arms have been solved -- see LateUpdate.
    [DefaultExecutionOrder(100)]
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

        /// <summary>
        /// Half-angle of the hip-fire spread cone -- was 3.0, giving every
        /// shot up to 3 degrees of random miss. EXPLICIT ASK: "i want you
        /// to do bullets the same as csgo... make every shot hit exact
        /// where you clicked in the middle of your screen." Zeroed rather
        /// than just narrowed -- CS:GO's own accuracy model still has some
        /// spread (movement, jumping, recoil), but "exact where you
        /// clicked" is the literal, explicit ask here, not a request to
        /// match CS:GO's specific accuracy curve.
        /// </summary>
        public const float HipFireSpreadDegrees = 0.0f;

        /// <summary>Same zeroing, same reason, as HipFireSpreadDegrees' own doc comment -- aiming down sights was already tighter, now exact too.</summary>
        public const float AdsSpreadDegrees = 0.0f;

        /// <summary>
        /// How far each shot's own pitch is randomised, as a fraction either
        /// side of 1.0 -- "still choppy if you spam shots", reported even
        /// after PlayOneShot already stopped one shot's audio from cutting
        /// another's off (see Fire's own comment on that fix). Overlapping
        /// PLAYS OF THE IDENTICAL WAVEFORM, a few frames apart at this fire
        /// rate, sum into audible comb-filtering -- a stutter baked into the
        /// waveform itself, not a scheduling bug PlayOneShot could fix a
        /// second time. A few percent of pitch jitter is enough to decorrelate
        /// the overlapping copies without the variation itself being audible
        /// as a pitch change.
        /// </summary>
        public const float ShotPitchJitter = 0.04f;

        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f; // cos(35 degrees)

        // --- RECOIL, from the third-person-shooter research (see
        // docs/THIRD_PERSON_PISTOL.md). A small, DETERMINISTIC vertical kick that
        // recovers fast: learnable rather than random, and applied AFTER the
        // shot has left, so the first shot is always exactly where the
        // crosshair was (the explicit "exact where you clicked" ask). All
        // instance fields so /gun can retune them live. ---

        [Header("Recoil / feel (live-tunable with /gun)")]
        [Tooltip("Camera kick per shot, degrees up. A pistol is small; the recovery below is quick.")]
        public float RecoilPitchDegrees = 1.2f;

        [Tooltip("Aiming down sights steadies the gun: the kick is scaled by this.")]
        public float AdsRecoilMultiplier = 0.7f;

        [Tooltip("Camera roll jolt per shot, degrees (random side).")]
        public float ShakeDegrees = 0.25f;

        [Tooltip("The pistol itself snaps back this far (metres) and tips its muzzle up this many degrees, then settles.")]
        public float KickBackMetres = 0.035f;

        public float KickUpDegrees = 7.0f;

        [Tooltip("How fast the gun's own kick settles, per second (bigger = snappier).")]
        public float KickRecoverPerSecond = 16.0f;

        // --- THE THREE-TRACE SHOT (Monday Night Combat's method): camera trace
        // for what the crosshair is on, a hand trace so the barrel is not inside
        // a wall, and a barrel trace so scenery between the gun and the target
        // takes the bullet. ---

        /// <summary>Below this the muzzle is effectively AT the target; no barrel trace (it would only find noise).</summary>
        public const float MinBarrelTraceMetres = 0.25f;

        /// <summary>Stop the barrel trace this short of the aim point, so it never reports the target's own surface as a blocker.</summary>
        public const float BarrelTraceMargin = 0.05f;

        /// <summary>A click on an empty gun with nothing to reload: how long before the next click registers.</summary>
        public const float DryClickCooldownSeconds = 0.25f;

        public static readonly Vector3 GripHandPosition = new Vector3(0.02f, -0.05f, 0.10f);

        /// <summary>
        /// Where the FREE hand's palm goes to steady the gun, in the pistol's own space. This model is
        /// built on its side -- local +Z is the barrel, +Y is the shooter's LEFT and +X is UP (measured
        /// against the body while aiming) -- and the whole thing is scaled ~2.5x, so 0.04 here is ~10cm:
        /// the shooting palm sits at (-0.036, 0, 0.019); this is one palm's width to its left (+Y), a
        /// touch lower and forward, which is where a two-handed grip wraps the fingers of the first.
        /// Tuned against WeaponHandsPlayTests' pictures. Overridden by a saved hold (WeaponGripSettings), which
        /// is set by dragging the LeftHandGrip marker in the editor while the game runs.
        /// </summary>
        public static readonly Vector3 DefaultSupportGripLocal = new Vector3(-0.042f, 0.040f, 0.025f);

        /// <summary>Where the free palm holds, in the pistol's space: the saved point if there is one, else the derived one.</summary>
        public static Vector3 SupportGripLocal =>
            WeaponGripSettings.Current.PistolTuned ? WeaponGripSettings.Current.PistolLeftHand : DefaultSupportGripLocal;

        /// <summary>The marker the free hand reaches for, a child of the pistol. Null while the pistol is not held.</summary>
        public Transform LeftHandGrip => _leftHandGrip;

        private Transform _leftHandGrip;

        /// <summary>Puts the left-hand marker where the current hold (saved or derived) says.</summary>
        public void ApplyGrip()
        {
            if (_leftHandGrip != null)
            {
                _leftHandGrip.localPosition = SupportGripLocal;
            }
        }

        /// <summary>
        /// Not a direct port of pistol.gd's own GRIP_HAND_ROTATION -- that
        /// constant is in Godot's own radians/axis convention, and the
        /// bottle's grip (see BottleWeapon.GripHandRotationEuler) shows past
        /// sessions re-tuned rather than numerically converted these between
        /// engines.
        ///
        /// THE Z=180 IS LOAD-BEARING, not cosmetic -- the starting guess
        /// this shipped with (X=-5, Y=90, Z=0) rendered the gun genuinely
        /// upside down (slide at the bottom, grip up near the wrist),
        /// confirmed by PistolBuilder.CaptureAiming against the real
        /// pistol_aim_in pose, not just the arms-down carry. Adding the
        /// 180-degree roll fixed both poses at once with this one constant --
        /// no clip or bone offset needed rework. Reconfirm with
        /// CaptureGrip/CaptureAiming before changing this again; a wrong
        /// value here is not obvious from source alone.
        /// </summary>
        public static readonly Vector3 GripHandRotationEuler = new Vector3(-5.0f, 90.0f, 180.0f);

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

        /// <summary>
        /// Extra voices, all sharing ShotPlayer's own clip -- EXPLICIT BUG
        /// REPORT, still open after both PlayOneShot and ShotPitchJitter
        /// already shipped: "the gunshot noise is glitchy make it consistent
        /// and not choppy." ROOT CAUSE: AudioSource.pitch belongs to the
        /// SOURCE, not to an individual PlayOneShot call -- every shot's
        /// `ShotPlayer.pitch = RandomShotPitch()` was retroactively
        /// re-pitching every OTHER copy of the clip still overlapping from
        /// recent shots on that same source, not just starting the new one
        /// at its own pitch. At this fire rate (FireCooldownSeconds ~0.18s)
        /// against a clip several times that long, several shots are
        /// routinely overlapping at once, so this was firing on nearly
        /// every shot -- confirmed as a well-documented Unity AudioSource
        /// limitation (PlayOneShot's own pitch is inherited from the
        /// source at call time and stays tied to it), not something
        /// PlayOneShot's own overlap support was ever meant to fix.
        ///
        /// FIX: give overlapping shots their own independent AudioSource
        /// each, cycled round-robin (_shotPoolIndex), so setting one's pitch
        /// can never reach back and warp a shot already playing on another.
        /// OPTIONAL, NOT REQUIRED -- Fire() falls back to ShotPlayer alone
        /// (the original, single-source behaviour) whenever this is left
        /// empty, so nothing that only wires ShotPlayer directly (existing
        /// tests included) needs to change.
        /// </summary>
        public AudioSource[] ShotPlayerPool;

        private int _shotPoolIndex;

        [Tooltip("For a bullet landing on a body -- same prefab MeleeAttacker uses.")]
        public GameObject BloodPrefab;

        public LayerMask TargetLayers = ~0;

        /// <summary>
        /// Live-tunable grip transform, for DevConsoleView's own /pistol
        /// pos/rot/scale/pose commands -- default to the same baked
        /// constants above, same "read live, change with a console
        /// command, bake the final numbers back in once confirmed" idiom
        /// SkinnedGarmentCombiner.RendererTransform's own doc comment
        /// establishes for the jacket. UpdateHeldPose/OnEquip apply these
        /// instead of the constants directly, so nudging one in real time
        /// needs no rebuild to see.
        /// </summary>
        private Vector3 _liveGripPosition = GripHandPosition;
        private Vector3 _liveGripRotation = GripHandRotationEuler;
        private float _liveGripScale = GripScale;

        public Vector3 LiveGripPosition => _liveGripPosition;
        public Vector3 LiveGripRotation => _liveGripRotation;
        public float LiveGripScale => _liveGripScale;

        public void SetLiveGripPosition(Vector3 value) => _liveGripPosition = value;
        public void SetLiveGripRotation(Vector3 value) => _liveGripRotation = value;
        public void SetLiveGripScale(float value) => _liveGripScale = value;

        public int MagazineAmmo { get; private set; } = MagazineSize;
        public int ReserveAmmo { get; private set; } = StartingReserveAmmo;
        public bool IsReloading { get; private set; }

        /// <summary>Raised for every trigger pull -- see <see cref="GunShot"/>.</summary>
        public event System.Action<GunShot> ShotResolved;

        /// <summary>0..1: the pistol's own snap-back, decaying. Read by UpdateHeldPose.</summary>
        private float _kick;

        /// <summary>
        /// Whether OnTriggerEnter has actually seen a player, independent
        /// of the look/range gate in TryPickup -- lets a test tell "the
        /// trigger never fired at all" apart from "it fired but the look
        /// check said no", which used to be indistinguishable from either
        /// side of the pickup ever going quiet.
        /// </summary>
        public bool HasNearbyPlayer => _nearbyPlayer != null;

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
            if (_kick > 0.0f)
            {
                _kick = Mathf.Max(0.0f, _kick - _kick * (1.0f - Mathf.Exp(-KickRecoverPerSecond * deltaTime)) - 0.0005f);
            }

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
            transform.localPosition = _liveGripPosition;
            transform.localEulerAngles = _liveGripRotation;
            transform.localScale = Vector3.one * _liveGripScale;

            if (IsReloading)
            {
                float progress = Mathf.Clamp01(1.0f - _reloadTimeRemaining / ReloadSeconds);
                float dip = Mathf.Sin(progress * Mathf.PI);
                transform.localPosition += new Vector3(0.0f, -0.06f, 0.02f) * dip;
                transform.localEulerAngles += new Vector3(28.0f, 0.0f, 0.0f) * dip;
            }

            // NOT the kick, though: that goes on in LateUpdate, after the arms are solved.
        }

        /// <summary>
        /// THE KICK IS APPLIED LAST. It used to be put on the gun here in Update's pose, BEFORE the animator and
        /// PlayerLocomotion.LateUpdate solved the arms -- and the arm's barrel correction measures where the
        /// muzzle points and turns the arm to point it at the target, so it saw the 7-degree kick as an error
        /// and cancelled it by snapping the whole arm the other way in the very frame of the shot (measured:
        /// 5-6.6 degrees in one frame on every shot, on both arms once the free hand followed the gun). That is
        /// the reported "arms spazz out a bit when shooting". Now the arms are solved against the UNKICKED gun
        /// and the kick is laid on top for rendering, so the recoil shows on the pistol and the arms stay steady.
        /// (Runs after PlayerLocomotion.LateUpdate: see the execution order on this class.)
        /// </summary>
        private void LateUpdate()
        {
            if (_equipped)
            {
                ApplyKick();
            }
        }

        /// <summary>
        /// The pistol's own snap-back when it fires: shoved back along its
        /// own barrel and tipped muzzle-up, then settling. Worked out from the
        /// muzzle's actual world facing rather than from hand-tuned local
        /// axes -- this gun's grip carries a 180-degree roll and a rig-scaled
        /// hand (see GripHandRotationEuler), so "local -Z" is not "back" in
        /// any way that survives a re-tune of the grip.
        /// </summary>
        private void ApplyKick()
        {
            if (_kick <= 0.0f || MuzzleFlashObject == null || transform.parent == null)
            {
                return;
            }

            Vector3 barrel = MuzzleFlashObject.transform.forward;
            Vector3 back = -barrel * (KickBackMetres * _kick);
            transform.localPosition += transform.parent.InverseTransformVector(back);

            Vector3 axis = Vector3.Cross(barrel, Vector3.up);
            if (axis.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.AngleAxis(KickUpDegrees * _kick, axis.normalized) * transform.rotation;
            }
        }

        private void TickPickup()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            bool ePressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            TryPickup(_nearbyPlayer, ePressed);
        }

        /// <summary>
        /// The actual pickup decision, factored out from TickPickup so a
        /// test can drive it with an explicit "was E pressed" flag --
        /// Keyboard.current is null outside a real Input System device
        /// (nothing in this project's own tests exercises it), and this
        /// gate had never actually been run end to end by anything before
        /// this. Matches PlayerLocomotion.Tick's own split: the thing that
        /// reads real input is a thin wrapper around the thing that
        /// decides, so the decision is what gets tested.
        /// </summary>
        public bool TryPickup(Transform player, bool ePressed)
        {
            if (!ePressed)
            {
                return false;
            }

            // RANGE FROM THE PLAYER'S OWN BODY, NOT THE CAMERA. LookSourceFor
            // falls back to Camera.main, which under the real ThirdPersonCamera
            // trails Distance (4.5m) behind the player's own pivot -- measuring
            // range from there means this can read 6+ metres to a pistol lying
            // right at the player's feet, permanently failing the InteractRange
            // gate no matter how close the player actually stands. Confirmed
            // directly from a real play session's own CityX-PICKUP-DIAG output.
            // Direction still reads the camera below -- "is the camera pointed
            // at it" and "is the player's body close enough" are different
            // questions, and conflating them into one vector from the camera
            // is exactly what broke the second one.
            Vector3 toPlayer = transform.position - player.position;
            if (toPlayer.sqrMagnitude > InteractRange * InteractRange)
            {
                return false;
            }

            Transform lookSource = LookSourceFor(player);
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
            if (inventory.AddItem(ItemRegistry.Pistol, 1))
            {
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        /// <summary>
        /// THE CAMERA'S FACING, NOT THE BODY'S -- pistol.gd's own
        /// _player_is_looking_at_me (and glass_bottle.gd's identical
        /// _player_is_looking_at_me) reads the camera directly
        /// (camera.global_transform.basis.z), not the body, and porting
        /// that literally matters here: PlayerLocomotion only turns the
        /// body to match the camera WHILE ACTUALLY MOVING (see its own
        /// "the body faces the camera, not where it is going" comment),
        /// so a player who walked up to a dropped pickup and stopped to
        /// press E -- the natural way to pick up something stationary --
        /// can be facing anywhere at all, and the old body-forward check
        /// silently rejected the pickup no matter how many times E was
        /// pressed. Same YawSource-or-Camera.main fallback chain
        /// PlayerInputReader's own gunYawSource/swingYawSource already use,
        /// so this asks the same question the same way everything else
        /// that needs "where is the player actually looking" already does.
        /// Shared with BottleWeapon.TickPickup, which had the identical bug.
        /// </summary>
        internal static Transform LookSourceFor(Transform player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && input.YawSource != null)
            {
                return input.YawSource;
            }
            return Camera.main != null ? Camera.main.transform : player;
        }

        private void OnTriggerEnter(Collider other)
        {
            // THE PlayerVitals TRANSFORM ITSELF, NOT other.transform. The
            // player's ragdoll carries a real collider on every bone (see
            // RagdollBuilder), so whichever one actually swept into this
            // trigger first can just as easily be "head" as the root --
            // confirmed live via CityX-PICKUP-DIAG showing nearbyPlayer=head.
            // TryPickup measures range from this transform directly, and a
            // bone a metre away from the body's own centre is a much smaller
            // error than the one it used to make, but there is no reason to
            // keep it when the actual root is one hop away right here.
            PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
            if (vitals != null)
            {
                _nearbyPlayer = vitals.transform;
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
                transform.localPosition = _liveGripPosition;
                transform.localEulerAngles = _liveGripRotation;
                transform.localScale = Vector3.one * _liveGripScale;
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

            // Camera kick, hit-confirm tick, dry click and the reticle's
            // blocked state all hang off this one component, driven by
            // ShotResolved -- added at runtime so no prefab has to change.
            var feel = player.GetComponent<GunFeel>();
            if (feel == null)
            {
                feel = player.AddComponent<GunFeel>();
            }
            feel.Attach(this);

            // The arm aims the BARREL, not just the upper arm -- see
            // PlayerLocomotion.ApplyGunAimPose.
            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion != null && MuzzleFlashObject != null)
            {
                locomotion.GunMuzzle = MuzzleFlashObject.transform;
            }
            if (locomotion != null)
            {
                // The free hand comes up to steady the gun as it is raised to aim, reaching for a marker on the
                // pistol that can be dragged in the editor while the game runs (see WeaponGripSettings).
                if (_leftHandGrip == null)
                {
                    _leftHandGrip = GripMarker.Create(transform, "LeftHandGrip", "left hand", new Color(1.0f, 0.55f, 0.1f));
                }
                ApplyGrip();
                locomotion.SetSupportHand(_leftHandGrip, Vector3.zero, onlyWhenAiming: true);
            }
        }

        public void OnUnequip(GameObject player)
        {
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
            }
            if (locomotionOnUnequip != null && locomotionOnUnequip.GunMuzzle == (MuzzleFlashObject != null ? MuzzleFlashObject.transform : null))
            {
                locomotionOnUnequip.GunMuzzle = null;
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
            if (IsReloading || _fireCooldownRemaining > 0.0f)
            {
                return false;
            }

            // AN EMPTY CLICK RELOADS FOR YOU (Gears, Fortnite, The Division
            // all do) -- while there is reserve ammo to reload from. With none,
            // it is just a dry click, throttled so mashing does not machine-gun it.
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
            // PlayOneShot, NOT Play() -- Play() restarts the SAME playback,
            // so spamming shots faster than the clip's own length cut it off
            // every time (confirmed reported: "the whole thing" never got a
            // chance to play). PlayOneShot fires an independent, overlapping
            // instance instead, the same fix MuzzleSparks already needed a
            // different solution for (that one restarts on purpose, per its
            // own comment -- a burst of sparks stacking indefinitely looks
            // wrong where overlapping gunshot audio does not).
            // See ShotPlayerPool's own doc comment: a shared source's pitch
            // is retroactive across everything already overlapping on it,
            // so a pooled, independent voice per shot is used when one is
            // wired -- falling back to the single ShotPlayer (the original
            // behaviour, pitch-jitter-and-all) when it isn't.
            AudioSource voice = NextShotVoice();
            if (voice != null && voice.clip != null)
            {
                // See ShotPitchJitter's own doc comment -- PlayOneShot alone
                // fixed shots CUTTING each other off; it does not stop
                // several overlapping copies of the identical waveform from
                // phase-cancelling into an audible stutter at this fire
                // rate, which is a separate problem needing this separate fix.
                voice.pitch = RandomShotPitch();
                voice.PlayOneShot(voice.clip);
            }

            float spreadDegrees = aiming ? AdsSpreadDegrees : HipFireSpreadDegrees;
            Vector3 spreadDirection = ApplySpread(direction, spreadDegrees);

            // HITSCAN, ALREADY -- EXPLICIT ASK: "i want you to do bullets
            // the same as csgo... zero travel time." This raycast resolves
            // synchronously, right here, in the same call that started the
            // shot -- there was never a travelling bullet object to remove.
            // See BulletTracer's own doc comment: what CS:GO actually has
            // that this didn't yet is the cosmetic half, a visible streak
            // that plays no part in hit resolution.
            // THE THREE-TRACE SHOT -- see ResolveTrace. In open ground it is the
            // same single camera ray as ever; it only changes the outcome when
            // scenery is between the GUN and the crosshair's target.
            ShotTrace trace = ResolveTrace(origin, spreadDirection, wielder);
            RaycastHit hit = trace.HitInfo;
            bool realHit = trace.Hit;
            bool hitDamageable = false;
            bool killed = false;

            if (realHit)
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
                        hitDamageable = true;
                        killed = !damageable.IsAlive;
                        if (BloodPrefab != null)
                        {
                            BloodSplatter.Spawn(BloodPrefab, hit.point, origin - hit.point);
                        }
                    }
                }
            }

            // BULLET-TRACER HISTORY (each entry an explicit report): "coming from
            // all sorts of different directions" -> tracer drawn along the muzzle's
            // own forward; "still coming from all over" -> hip fire never pointed
            // the arm at the target, so muzzle-forward meant nothing, and the aimed
            // path got a split; and now "not showing up at all, and when they do
            // it is going in the wrong direction". Root causes: (1) the streak was
            // 1.5cm wide for 0.05s, about one pixel for three frames -- see
            // BulletTracer; (2) the arm aimed at a fixed point 15m down the camera
            // ray, so at close range the barrel pointed well off the hit while the
            // tracer followed the barrel and stopped short of the target, and hip
            // fire never raised the arm at all. Fixed at the source: the arm now
            // aims at the shot's real end point (LocomotionInput.GunAimPoint) and
            // raises for half a second after every shot (NotifyGunFired), so the
            // barrel, the bullet and the tracer are one line.
            Vector3 muzzlePosition = MuzzleFlashObject != null ? MuzzleFlashObject.transform.position : origin;
            // THE TRACER IS THE BULLET'S PATH: straight from the muzzle to where the
            // shot really ended (the hit point, or a far point on the crosshair ray
            // for a miss), whatever the mode. This is the very line the barrel trace
            // validated, so the streak always ends on what was hit -- the earlier
            // "follow the muzzle's own forward while aiming" version stopped short of
            // near targets, because the arm aims at a point down the camera ray, not
            // at the hit (a 1.4m shoulder offset is 20 degrees at 4m). The GUN now
            // points at the hit point too (PlayerLocomotion.NotifyGunFired and
            // LocomotionInput.GunAimPoint), so barrel, tracer and bullet agree.
            Vector3 tracerEnd = realHit ? hit.point : origin + spreadDirection * Range;
            BulletTracer.Spawn(muzzlePosition, tracerEnd, MuzzleFlashObject != null ? MuzzleFlashObject.transform : null);

            // THE KICK. Camera recoil is the camera's job (whoever listens to
            // ShotResolved applies RecoilPitchDegrees); the pistol's own
            // snap-back is ours. Applied AFTER the shot resolved, so the shot
            // itself is always exactly on the crosshair.
            _kick = 1.0f;
            ShotResolved?.Invoke(new GunShot
            {
                Fired = true,
                HitSomething = realHit,
                HitDamageable = hitDamageable,
                Killed = killed,
                BarrelBlocked = trace.BarrelBlocked,
                Point = realHit ? hit.point : origin + spreadDirection * Range,
                Aiming = aiming,
                RecoilPitchDegrees = RecoilPitchDegrees * (aiming ? AdsRecoilMultiplier : 1.0f),
                ShakeDegrees = ShakeDegrees * (aiming ? AdsRecoilMultiplier : 1.0f),
            });

            return true;
        }

        private struct ShotTrace
        {
            public bool Hit;
            public RaycastHit HitInfo;
            public bool BarrelBlocked;
        }

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        /// <summary>
        /// The nearest thing a ray hits that is not the wielder's own body --
        /// a RaycastAll that skips the shooter instead of stopping at them.
        /// (The old plain Raycast reported the shooter's own capsule as the
        /// first hit and then discarded the shot, so a ray that began inside or
        /// grazed the wielder hit NOTHING instead of what was behind them.)
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
                TargetLayers, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length)
            {
                // A full buffer means the nearest hit may have been dropped --
                // the unsorted non-alloc version keeps an arbitrary subset. A
                // ray through this many colliders is rare; pay for the exact answer.
                hits = Physics.RaycastAll(origin, direction, distance, TargetLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = hits[i];
                if (candidate.collider == null || IsWieldersOwnCollider(candidate.collider, wielder))
                {
                    continue;
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
        /// MONDAY NIGHT COMBAT'S THREE-TRACE SHOT, from its developer's own
        /// write-up of the third-person aiming problem: the camera sits away
        /// from the gun, so a bullet that follows the crosshair can go THROUGH
        /// a wall corner the barrel is actually behind (the player is safe and
        /// shooting through geometry). The crosshair must still win in open
        /// ground -- "exact where you clicked" -- so:
        ///  1. CAMERA TRACE: what the crosshair is on. This is the shot.
        ///  2. HAND TRACE: from the wielder's body to the muzzle. If scenery is
        ///     in the way, the barrel is inside/behind a wall: the bullet hits it.
        ///  3. BARREL TRACE: from the muzzle to the camera trace's hit point. If
        ///     scenery (other than the target itself) is in the way, that takes
        ///     the bullet instead.
        /// With no wielder or no muzzle (a bare test gun) only step 1 runs.
        /// </summary>
        private ShotTrace ResolveTrace(Vector3 origin, Vector3 direction, GameObject wielder)
        {
            var result = new ShotTrace();
            result.Hit = TraceIgnoringWielder(origin, direction, Range, wielder, out RaycastHit camera);
            result.HitInfo = camera;

            if (wielder == null || MuzzleFlashObject == null)
            {
                return result;
            }

            Vector3 muzzle = MuzzleFlashObject.transform.position;
            Vector3 aimPoint = result.Hit ? camera.point : origin + direction * Range;

            // 2. Hand trace.
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

            // 3. Barrel trace.
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
        /// Where a shot fired now would END (the hit point, or a far point on the
        /// crosshair ray on a miss) and whether scenery is taking it. The arm
        /// aims the gun at this point, so the barrel points where the bullet and
        /// its tracer go.
        /// </summary>
        public void AimTrace(Vector3 origin, Vector3 direction, GameObject wielder,
            out Vector3 point, out bool barrelBlocked)
        {
            ShotTrace trace = ResolveTrace(origin, direction, wielder);
            point = trace.Hit ? trace.HitInfo.point : origin + direction * Range;
            barrelBlocked = trace.BarrelBlocked;
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: "whenever i hold right click with the pistol
        /// the crosshair is on me and just shoots me." The aim-down-sights
        /// camera pulls in close to the character (ThirdPersonCamera's own
        /// aiming behaviour), which can put the raycast's own origin at or
        /// inside the player's own collider -- and Fire's raycast used
        /// TargetLayers with no exclusion at all (the default is literally
        /// "every layer"), so the very first thing a shot could hit was
        /// whoever fired it. Checked by identity against the wielder's own
        /// hierarchy rather than a layer mask: the player's ragdoll carries
        /// a real collider on every bone (RagdollBuilder), potentially on
        /// several different layers, so a layer-based exclusion would need
        /// to enumerate all of them and silently miss one; walking up from
        /// whatever was actually hit to see if it belongs to the wielder at
        /// all covers every one of them at once, the same way PistolWeapon.
        /// OnTriggerEnter and TryPickup already identify "is this the
        /// player" by walking up to PlayerVitals rather than trusting a
        /// layer.
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

        /// <summary>
        /// Perturbs direction within a cone of half-angle spreadDegrees --
        /// two independent small-angle rotations around an arbitrary pair of
        /// axes perpendicular to direction, matching pistol.gd's own
        /// _apply_spread exactly.
        /// </summary>
        /// <summary>
        /// See ShotPitchJitter's own doc comment. Pure and static so the
        /// jitter itself is directly testable without needing to fight
        /// Fire's own cooldown gate to get several calls through it.
        /// </summary>
        public static float RandomShotPitch()
        {
            return 1.0f + Random.Range(-ShotPitchJitter, ShotPitchJitter);
        }

        /// <summary>
        /// The AudioSource this shot's own voice plays on -- round-robins
        /// through ShotPlayerPool when one is wired (see its own doc
        /// comment for why that's the actual fix), otherwise the single
        /// ShotPlayer, unchanged from before the pool existed.
        /// </summary>
        private AudioSource NextShotVoice()
        {
            if (ShotPlayerPool == null || ShotPlayerPool.Length == 0)
            {
                return ShotPlayer;
            }
            _shotPoolIndex = (_shotPoolIndex + 1) % ShotPlayerPool.Length;
            return ShotPlayerPool[_shotPoolIndex];
        }

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

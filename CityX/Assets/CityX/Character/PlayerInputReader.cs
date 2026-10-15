using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Turns the keyboard into a <see cref="LocomotionInput"/> and hands it to
    /// the body.
    ///
    /// Separated from PlayerLocomotion so that the only thing that reads a
    /// device is this one small component. A networked player replaces it with
    /// one that reads a replicated input instead, and nothing about how the
    /// body moves has to change.
    ///
    /// Uses the Input System package directly rather than an action asset --
    /// this project sets activeInputHandler to the new system only, so the old
    /// UnityEngine.Input calls would compile and then silently do nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerLocomotion))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Whose yaw movement is relative to. Falls back to the main camera.")]
        public Transform YawSource;

        /// <summary>
        /// Set by a gun's own OnEquip/OnUnequip (see PistolWeapon), the same
        /// pattern GrappleSwing.EquippedHook and MeleeAttacker.EquippedWeapon
        /// already use. Typed as the interface rather than a concrete class
        /// so this assembly (CityX.Character) never has to reference
        /// CityX.Weapons -- see IEquippableGun's own doc comment.
        /// </summary>
        public IEquippableGun EquippedGun;

        private GunFeel _gunFeel;

        /// <summary>
        /// Set by Flamethrower's own OnEquip/OnUnequip, same pattern as
        /// EquippedGun. While non-null, left-click takes over entirely --
        /// see TickCombat's own flamethrower branch and
        /// IEquippableFlamethrower's own doc comment for why this is not
        /// EquippedGun itself.
        /// </summary>
        public IEquippableFlamethrower EquippedFlamethrower;

        /// <summary>
        /// Set by the spray can's own OnEquip/OnUnequip (2026-10-15), same pattern as EquippedGun. While non-null, a right-click asks for the
        /// graffiti canvas (<see cref="SprayRequested"/>) and the fists stand down.
        /// </summary>
        public ISprayCan EquippedSprayCan;

        /// <summary>Right-click with a spray can in hand: the UI (GraffitiMenuView) finds the wall in front and opens the canvas on it.</summary>
        public static event System.Action<PlayerInputReader> SprayRequested;

        /// <summary>
        /// Set by Jetpack's own OnEquip/OnUnequip, same pattern as
        /// EquippedGun. While non-null, Space thrusts instead of charging a
        /// jump -- see the Update() dispatch and IJetpack's own doc comment.
        /// </summary>
        public IJetpack EquippedJetpack;

        /// <summary>
        /// Set by Parachute's own OnEquip/OnUnequip, same pattern as
        /// EquippedJetpack. While non-null, a fresh press of Space while
        /// airborne toggles it open/closed instead of doing nothing -- see
        /// the Update() dispatch and IParachute's own doc comment. Shares
        /// Jetpack's own "back" equip slot (see ItemRegistry.Parachute's
        /// own doc comment), so Inventory's own slot eviction already
        /// guarantees this and EquippedJetpack are never both non-null at
        /// once -- no extra mutual-exclusion needed here.
        /// </summary>
        public IParachute EquippedParachute;

        /// <summary>
        /// Edge-detects a jetpack wall hit (see the crash-damage block in
        /// Update()) so a player held against a wall under thrust takes
        /// ONE impact, not the same damage re-applied every single frame
        /// they stay pinned there. Reset to false the instant CollisionFlags.
        /// Sides stops appearing, so backing off and hitting something else
        /// later is a fresh crash.
        /// </summary>
        private bool _wasJetpackCrashing;

        /// <summary>
        /// The wall normal _wasJetpackCrashing's own gate was last armed
        /// against -- see NewWallContactAngleDegrees' own doc comment for
        /// why a plain bool isn't enough on its own.
        /// </summary>
        private Vector3 _lastJetpackCrashNormal;

        /// <summary>
        /// How much a contact's own normal has to rotate from
        /// _lastJetpackCrashNormal before this counts as hitting a
        /// DIFFERENT wall rather than continuing to press into the same
        /// one -- EXPLICIT BUG REPORT: "i bugged out when running into the
        /// corner of the building with jetpack and got stuck into it
        /// instead ragdolling." A plain "was I touching A wall last frame"
        /// bool cannot tell a genuinely new impact apart from continuing to
        /// touch the SAME wall: flying at an angle into a building corner
        /// typically grazes one face first (a shallow, low-damage or
        /// zero-damage touch that still sets HitWallThisFrame, arming
        /// _wasJetpackCrashing), then immediately slams into the SECOND,
        /// near-perpendicular face a frame or two later -- a real, hard,
        /// damage-worthy impact that the edge-detection gate silently
        /// swallowed because it still believed the player was mid-way
        /// through the SAME wall-touch as the earlier graze. With the real
        /// crash knockdown never firing, the player never gets
        /// Ragdoll.Activate's own DepenetrateFromWorld correction either
        /// (see its own doc comment -- that is what actually clears a body
        /// stuck overlapping geometry), leaving them visibly wedged in the
        /// corner with no automatic fix and no ragdoll. 90 degrees is a
        /// square building corner's own angle; comfortably lower catches
        /// off-square architecture too, while staying comfortably above
        /// the near-zero jitter a static flat wall's own reported normal
        /// has frame to frame.
        /// </summary>
        public const float NewWallContactAngleDegrees = 45.0f;

        /// <summary>
        /// Whether this frame's own wall touch is a fresh impact worth
        /// checking for crash damage -- see NewWallContactAngleDegrees' own
        /// doc comment for the building-corner bug this exists to fix.
        /// A PURE, STATIC decision extracted specifically so a test can
        /// drive the exact geometric cases (continuing to touch the same
        /// wall, a genuine corner's second face, the very first touch)
        /// directly, without needing a real keyboard, jetpack, or physics
        /// scene just to reach a few lines of vector math.
        /// </summary>
        public static bool IsNewWallContact(
            bool hitWall, bool wasJetpackCrashing, Vector3 currentWallNormal, Vector3 lastCrashWallNormal)
        {
            return hitWall && (!wasJetpackCrashing ||
                Vector3.Angle(currentWallNormal, lastCrashWallNormal) > NewWallContactAngleDegrees);
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: "the crosshair doesnt seem to allign with
        /// where the bullet goes... maybe that was just for the fireball
        /// spell." It was: the fireball launches from a point on the
        /// player's own CHEST, but its own direction used to be
        /// aimSource.forward alone -- parallel to the camera's own sightline,
        /// not aimed AT it. Since the camera sits well behind and to the
        /// side of the chest (ThirdPersonCamera's own Distance/
        /// ShoulderOffset), two parallel rays with different origins never
        /// actually coincide at any finite distance -- the fireball's real
        /// path only ever approached the crosshair's own line as it
        /// receded toward infinity, never actually crossing it up close,
        /// which is exactly the kind of miss a player notices most.
        ///
        /// SAME FIX AS PlayerLocomotion.ApplyGunAimPose's own convergence
        /// point (GunAimConvergenceDistance ahead of the camera) --
        /// reused here rather than a second, independently-chosen distance,
        /// so every "aim at the crosshair from somewhere else on the body"
        /// case in this project agrees on what "the crosshair" even means.
        /// </summary>
        private static Vector3 ConvergedDirection(Vector3 origin, Transform aimSource)
        {
            Vector3 aimPoint = aimSource.position +
                aimSource.forward * PlayerLocomotion.GunAimConvergenceDistance;
            Vector3 toAimPoint = aimPoint - origin;
            return toAimPoint.sqrMagnitude > 0.0001f ? toAimPoint.normalized : aimSource.forward;
        }

        /// <summary>
        /// Found once via GetComponent in Awake, not set externally like
        /// EquippedGun/EquippedJetpack -- see IFireballCaster's own doc
        /// comment for why: the spell is an innate ability with no equip
        /// event to hang the wiring off, so this is discovered the same
        /// way PlayerVitals/MeleeAttacker already are.
        /// </summary>
        private IFireballCaster _fireballCaster;
        private IIceShardCaster _iceShards;
        private IPoisonBreathCaster _poisonBreath;
        private ISwordWaveCaster _swordWave;
        private IAcidRainCaster _acidRain;
        private SelfSmiteAbility _selfSmite;
        private BodySizeAbility _bodySize;
        private OctaslashAbility _octaslash;

        /// <summary>5: ACID RAIN, a green storm called down where the crosshair points (no further than its reach), burning everyone out in the open.</summary>
        private void CastAcidRain(Transform aimSource)
        {
            if (_acidRain == null || aimSource == null)
            {
                return;
            }
            _acidRain.TryCast(aimSource.position, aimSource.forward, GetComponentsInChildren<Collider>());
        }

        /// <summary>The raised-hand pose strength of whichever spell is being cast (fireball, ice shards or poison gas).</summary>
        private float SpellCastFraction =>
            Mathf.Max(
                Mathf.Max(_fireballCaster != null ? _fireballCaster.CastFraction : 0.0f, _iceShards != null ? _iceShards.CastFraction : 0.0f),
                Mathf.Max(_poisonBreath != null ? _poisonBreath.CastFraction : 0.0f, _bodySize != null ? _bodySize.CastFraction : 0.0f));

        private bool IsCastingSpell =>
            (_fireballCaster != null && _fireballCaster.IsCasting) || (_iceShards != null && _iceShards.IsCasting)
            || (_poisonBreath != null && _poisonBreath.IsCasting);

        /// <summary>While the scream winds up, the breath follows the crosshair: the point it is on in the world, handed in every frame.</summary>
        private void AimPoisonBreath(Transform aimSource)
        {
            if (_poisonBreath == null || aimSource == null || !_poisonBreath.IsCasting)
            {
                return;
            }
            _poisonBreath.Aim(aimSource.position + aimSource.forward * PlayerLocomotion.GunAimConvergenceDistance);
        }

        /// <summary>4: the character screams and breathes a cloud of poison gas out of their mouth at the crosshair (the fallback origin is the chest).</summary>
        private void CastPoisonBreath(Transform aimSource)
        {
            if (_poisonBreath == null || aimSource == null)
            {
                return;
            }
            float height = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f;
            Vector3 origin = transform.position + Vector3.up * height;
            _poisonBreath.TryCast(origin, ConvergedDirection(origin, aimSource), GetComponentsInChildren<Collider>());
        }

        /// <summary>3: starts an ice shard volley from the chest down the crosshair, from wherever the camera is looking.</summary>
        private void CastIceShards(Transform aimSource)
        {
            if (_iceShards == null || aimSource == null)
            {
                return;
            }
            float height = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f;
            Vector3 origin = transform.position + Vector3.up * height;
            _iceShards.TryCast(origin, ConvergedDirection(origin, aimSource), GetComponentsInChildren<Collider>());
        }

        /// <summary>While a volley is going out its remaining shards follow the crosshair.</summary>
        private void AimIceShards(Transform aimSource)
        {
            if (_iceShards == null || aimSource == null || !_iceShards.IsVolleying)
            {
                return;
            }
            float height = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f;
            Vector3 origin = transform.position + Vector3.up * height;
            _iceShards.Aim(origin, ConvergedDirection(origin, aimSource));
        }
        private BloodrageAbility _bloodrageAbility;

        /// <summary>
        /// How long after tapping sprint a second tap still counts as a dash.
        ///
        /// A press-release-press inside this window is the double-tap; a single
        /// press, or sprint simply held down, never crosses it, because the
        /// timer only starts on the FIRST press and is consumed the moment a
        /// second one lands. 0.3s is generous enough to catch a deliberate
        /// double-tap without firing on two unrelated ones.
        /// </summary>
        public const float DoubleTapSeconds = 0.3f;

        private float _sprintTapWindow;

        /// <summary>
        /// How long held space takes to reach a FULL charge -- Saints Row
        /// 4's own charge jump, sized only as a first test value: hold less
        /// and the release scales smoothly down toward an ordinary jump
        /// (see PlayerLocomotion.ChargedJumpHeightMultiplier for what
        /// "full" means in real height), hold longer and it just caps here.
        /// </summary>
        public const float ChargedJumpMaxHoldSeconds = 1.5f;

        private bool _jumpHeld;
        private float _jumpHeldSeconds;
        private int _lastJumpChargeFrame = -10;

        /// <summary>
        /// Set inside TickCombat's own gun branch, read back here in Update()
        /// once the LocomotionInput for this same frame is built -- TickCombat
        /// runs first and returns before movement is ever touched, so there is
        /// no other way for "is the gun's right-click held" to reach the
        /// animator without threading a whole extra parameter through.
        /// </summary>
        private bool _isAimingGun;
        private float _nextWeaponSwitch;

        /// <summary>
        /// Set by PlayerChat's own /dance and /breakdance commands (see
        /// StartDance/StartBreakdance) -- ported from character.gd's own
        /// _is_dancing, "loop an emote clip until something else takes
        /// over." Godot clears it individually at the start of every other
        /// action (punch, jump, block, shoot...); this port checks once,
        /// up front in Update() (see RequestsDanceCancel), for the same
        /// end result with one check instead of one scattered through every
        /// action's own start.
        /// </summary>
        private bool _isDancing;
        private bool _isBreakdancing;

        public bool IsDancing => _isDancing;
        public bool IsBreakdancing => _isBreakdancing;

        /// <summary>The push-up clips' lengths, seconds: going down, one rep, getting up (written by PlayerRigBuilder from the clips themselves).</summary>
        [SerializeField] private float _pushUpEnterSeconds = 2.0f;
        [SerializeField] private float _pushUpRepSeconds = 1.3f;
        [SerializeField] private float _pushUpExitSeconds = 2.0f;

        /// <summary>The /pushups emote (owner, 2026-10-14). See PushUpEmote and StartPushUps.</summary>
        private PushUpEmote _pushUps;

        public PushUpEmote PushUps => _pushUps ??= new PushUpEmote(_pushUpEnterSeconds, _pushUpRepSeconds, _pushUpExitSeconds);

        /// <summary>True from /pushups until the body is standing again: it is held, and the keys only call it off.</summary>
        public bool IsDoingPushUps => _pushUps != null && _pushUps.HoldsTheBody;

        /// <summary>For PlayerRigBuilder: the three clips' lengths, so the count and the poses keep time with the animation.</summary>
        public void SetPushUpClipLengths(float enterSeconds, float repSeconds, float exitSeconds)
        {
            _pushUpEnterSeconds = enterSeconds;
            _pushUpRepSeconds = repSeconds;
            _pushUpExitSeconds = exitSeconds;
            PushUps.SetClipLengths(enterSeconds, repSeconds, exitSeconds);
        }

        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private MeleeAttacker _melee;
        private Ragdoll _ragdoll;
        private PlayerAudio _audio;
        private GrabCarry _grabCarry;
        private Inventory _inventory;
        private GrappleSwing _grappleSwing;
        private PlayerClimbing _climbing;
        private PlayerVaulting _vaulting;
        private WallStick _wallStick;

        /// <summary>
        /// What this body has learnt and what is on its bar (owner, 2026-10-14). The number keys cast whatever is in their slot, and Dash, Climb,
        /// Wall Stick and Super Jump work only once learnt; vaulting always does. A body with no character knows what the game had before the
        /// skill tree: everything but Super Jump (PlayerSkills).
        /// </summary>
        private PlayerSkills _skills;

        /// <summary>Whether the player is asking to grab a wall (Space held a moment): see GrabIntent.</summary>
        private readonly GrabIntent _grabIntent = new GrabIntent();
        private ThirdPersonCamera _orbitCamera;
        private readonly WeaponAimInput _weaponAim = new WeaponAimInput();

        /// <summary>
        /// Exposed so Jetpack.OnEquip/OnUnequip can widen MinPitch/MaxPitch
        /// for full-vertical flight without each doing its own scene-wide
        /// Object.FindFirstObjectByType search for the one camera this
        /// class already found -- see Jetpack.cs's own doc comment on why
        /// jetpacking needs a wider pitch range than ordinary looking-around.
        /// </summary>
        public ThirdPersonCamera OrbitCamera => _orbitCamera;

        /// <summary>
        /// The car this body is seated in (driver or passenger), or null.
        /// Ported from player.gd's own _seated_car -- set/cleared only by
        /// Vehicles/Car.cs's own _begin_enter/_complete_exit equivalents,
        /// via BeginCarTransition/EndCarTransition below. Not used for
        /// anything beyond the null-check in Update(): which car it is
        /// doesn't matter to this class itself, only whether one owns the
        /// frame right now.
        /// </summary>
        private GameObject _seatedCar;

        /// <summary>
        /// The BenchSeat this body is sitting on, or null. Same "something
        /// else owns the frame right now" shape as _seatedCar, but its own
        /// separate field rather than reusing that one: a bench needs none
        /// of the car's own extra machinery (no camera-distance widening, no
        /// transition-in-progress polling), and naming the field after a
        /// car while a bench uses it would read as a bug to the next person
        /// touching this file. See BenchSeat.cs's own doc comment.
        /// </summary>
        private GameObject _seatedBench;

        private float _savedCameraDistance;

        /// <summary>
        /// Metres, ported from player.gd's own DRIVING_SPRING_LENGTH -- a
        /// car reads much bigger on screen than the player's own body,
        /// which the default on-foot ThirdPersonCamera.Distance was tuned
        /// against. Applies to the passenger seat too, not just the
        /// driver's -- both are inside the same car needing the same wider
        /// view.
        /// </summary>
        public const float DrivingCameraDistance = 7.0f;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            // All optional. A body with no vitals, no fists and no ragdoll
            // still walks -- useful for tests and for anything that is not the
            // player.
            _vitals = GetComponent<PlayerVitals>();
            _melee = GetComponent<MeleeAttacker>();
            if (_melee != null)
            {
                _melee.SwingStarted += OnMeleeSwingStarted;
            }
            _ragdoll = GetComponent<Ragdoll>();
            _audio = GetComponent<PlayerAudio>();
            _grabCarry = GetComponent<GrabCarry>();
            _inventory = GetComponent<Inventory>();
            _grappleSwing = GetComponent<GrappleSwing>();
            _climbing = GetComponent<PlayerClimbing>();
            _vaulting = GetComponent<PlayerVaulting>();
            _wallStick = GetComponent<WallStick>();
            _skills = PlayerSkills.On(gameObject);
            _fireballCaster = GetComponent<IFireballCaster>();
            _iceShards = GetComponent<IIceShardCaster>();
            _poisonBreath = GetComponent<IPoisonBreathCaster>();
            _swordWave = GetComponent<ISwordWaveCaster>();
            _acidRain = GetComponent<IAcidRainCaster>();
            _selfSmite = GetComponent<SelfSmiteAbility>();
            _bodySize = GetComponent<BodySizeAbility>();

            // The player gets wet in the rain and dries under cover. Added here
            // rather than baked into the prefab: only the real, local player has
            // an input reader, and this needed no prefab rebuild.
            if (GetComponent<PlayerWetness>() == null)
            {
                gameObject.AddComponent<PlayerWetness>();
            }

            // The sparks and the ting of a blow stopped outright by a block (Block Chance), here and on other players' copies of this body.
            if (GetComponent<BlockDeflectEffect>() == null)
            {
                gameObject.AddComponent<BlockDeflectEffect>();
            }

            // A stagger cuts short what was winding up (2026-10-14: the Stability stat makes it rarer).
            if (_vitals != null)
            {
                _vitals.Staggered += OnStaggered;
            }
        }

        /// <summary>
        /// STAGGERED (2026-10-14): a blow that put the body in hitstun cuts short whatever was winding up -- the poison scream before the gas
        /// leaves, the rest of an ice volley, Octaslash's wind-up, a charging sword spin. A blow the Stability stat shrugs off never gets here.
        /// </summary>
        private void OnStaggered()
        {
            _poisonBreath?.Interrupt();
            _iceShards?.Interrupt();
            if (_octaslash == null)
            {
                _octaslash = GetComponent<OctaslashAbility>();
            }
            _octaslash?.Interrupt();
            _melee?.InterruptSpinCharge();
        }

        private void OnDestroy()
        {
            if (_vitals != null)
            {
                _vitals.Staggered -= OnStaggered;
            }
        }

        /// <summary>
        /// A LOOKUP, NOT A GetComponent -- the camera lives on its own
        /// GameObject, not this one, matching UniqueNpc's own reasoning for
        /// finding it in Start() rather than Awake(): Awake ordering between
        /// separate objects is not guaranteed, and Start runs only after
        /// every object's own Awake has already run.
        /// </summary>
        private void Start()
        {
            _orbitCamera = Object.FindFirstObjectByType<ThirdPersonCamera>();
        }

        /// <summary>Called by PlayerChat's /pushups: down into push-ups (PushUpEmote), unless airborne, dead or stunned. It stops a dance.</summary>
        public void StartPushUps()
        {
            if (_locomotion != null && !_locomotion.IsGrounded)
            {
                return;   // not mid-jump, as /dance
            }
            if (_vitals != null && (_vitals.IsDead || _vitals.IsStunned))
            {
                return;
            }
            _isDancing = false;
            _isBreakdancing = false;
            PushUps.Start();
        }

        /// <summary>
        /// One frame of push-ups: what calls them off (a movement key -- WASD, Space, Shift, Ctrl, as a dance -- a spell key 1 to 0, a mouse
        /// button), the count (a push-up's XP to the vitals as each one finishes), and the body ticked with no movement, so gravity and the
        /// Animator's phase carry on and other players are sent it.
        /// </summary>
        private void TickPushUps(Keyboard keyboard, float deltaTime)
        {
            Mouse mouse = Mouse.current;
            bool stop = RequestsDanceCancel(keyboard) || AnyDigitPressed(keyboard) ||
                        (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                                           mouse.middleButton.wasPressedThisFrame));
            int reps = _pushUps.Tick(deltaTime, stop);
            if (reps > 0 && _vitals != null)
            {
                _vitals.AddXp(reps * PushUpEmote.XpPerRep);
            }
            _audio?.SilenceFootsteps();
            _locomotion.Tick(new LocomotionInput { YawDegrees = transform.eulerAngles.y, PushUpPhase = _pushUps.AnimatorPhase }, deltaTime);
        }

        private static bool AnyDigitPressed(Keyboard keyboard)
        {
            return keyboard.digit1Key.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame || keyboard.digit3Key.wasPressedThisFrame ||
                   keyboard.digit4Key.wasPressedThisFrame || keyboard.digit5Key.wasPressedThisFrame || keyboard.digit6Key.wasPressedThisFrame ||
                   keyboard.digit7Key.wasPressedThisFrame || keyboard.digit8Key.wasPressedThisFrame || keyboard.digit9Key.wasPressedThisFrame ||
                   keyboard.digit0Key.wasPressedThisFrame;
        }

        /// <summary>
        /// Called by PlayerChat's own /dance command. No-op while airborne,
        /// matching character.gd's own dance()/_play_emote guard -- dancing
        /// mid-jump doesn't read as anything sensible, and the jump
        /// animation already owns the body at that point anyway.
        /// </summary>
        public void StartDance()
        {
            if (_locomotion != null && !_locomotion.IsGrounded)
            {
                return;
            }
            _isDancing = true;
            _isBreakdancing = false;
        }

        /// <summary>
        /// Called by Vehicles/Car.cs the instant a seat transition starts
        /// (before the entering_car animation even finishes) -- deliberately
        /// not deferred until the player is actually seated, so they can't
        /// walk away mid-animation. Ported from player.gd's own
        /// begin_car_transition; car itself isn't used by this class beyond
        /// the null-check (see _seatedCar's own doc comment).
        /// </summary>
        public void BeginCarTransition(GameObject car)
        {
            _seatedCar = car;
            if (_orbitCamera != null)
            {
                _savedCameraDistance = _orbitCamera.Distance;
                _orbitCamera.Distance = DrivingCameraDistance;
                // A locked-on/aiming camera has no business fighting the
                // car's own view swap -- same release entering any other
                // input-freezing state already uses (see the ragdoll/dead
                // branches above resetting _orbitCamera.IsAiming).
                _orbitCamera.IsAiming = false;
            }
            if (_locomotion != null)
            {
                _locomotion.InCarSeat = true;
            }
        }

        /// <summary>
        /// Called by Vehicles/Car.cs's own _complete_exit, once the player
        /// is already back in the world at a safe position. Ported from
        /// player.gd's own end_car_transition -- its own camera_pivot yaw/
        /// pitch fold-back has no Unity equivalent here: ThirdPersonCamera
        /// orbits Target's own live world transform rather than
        /// accumulating rotation onto a node the body itself carries, so
        /// there is nothing left over on the camera side for a reparent to
        /// leave stuck at an angle.
        /// </summary>
        public void EndCarTransition()
        {
            _seatedCar = null;
            if (_orbitCamera != null)
            {
                _orbitCamera.Distance = _savedCameraDistance;
            }
            if (_locomotion != null)
            {
                _locomotion.InCarSeat = false;
            }
        }

        /// <summary>
        /// Called by BenchSeat the instant a player sits down -- no
        /// transition-in-progress step to wait out, unlike
        /// BeginCarTransition, since PlayerLocomotion.ApplySittingPose
        /// bends the leg bones directly every frame rather than waiting on
        /// an Animator clip to finish.
        /// </summary>
        public void BeginBenchSeat(GameObject bench)
        {
            _seatedBench = bench;
            if (_locomotion != null)
            {
                _locomotion.SetSitting(true);
            }
        }

        /// <summary>Called by BenchSeat the instant a player stands back up.</summary>
        public void EndBenchSeat()
        {
            _seatedBench = null;
            if (_locomotion != null)
            {
                _locomotion.SetSitting(false);
            }
        }

        /// <summary>Called by PlayerChat's own /breakdance command. See StartDance's own doc comment.</summary>
        public void StartBreakdance()
        {
            if (_locomotion != null && !_locomotion.IsGrounded)
            {
                return;
            }
            _isBreakdancing = true;
            _isDancing = false;
        }

        /// <summary>
        /// Whether this frame's input should end a dance in progress --
        /// ported from character.gd's own "movement always cancels a dance"
        /// rule, plus the individual _is_dancing=false clears its own
        /// punch()/jump()/block() etc. each carry, folded into one check
        /// here instead of one per action.
        /// </summary>
        private static bool RequestsDanceCancel(Keyboard keyboard)
        {
            return keyboard.wKey.isPressed || keyboard.aKey.isPressed ||
                   keyboard.sKey.isPressed || keyboard.dKey.isPressed ||
                   keyboard.spaceKey.isPressed || keyboard.leftShiftKey.isPressed ||
                   keyboard.leftCtrlKey.isPressed;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // THE CAR OWNS THE WHOLE FRAME WHILE SEATED, matching player.gd's
            // own _seated_car early-return -- it's been reparented onto one
            // of the car's own seat nodes (see Vehicles/Car.cs's own
            // _complete_enter), so there is nothing for this class's own
            // movement/combat/dance handling to do until it's unparented
            // again. Placed before every other early-return below (ragdoll,
            // dead, climbing, grapple...) since none of those states can
            // coexist with being seated. Vehicles/Car.cs reads WASD/Space
            // itself, directly, entirely independently of this class -- the
            // same split car.gd's own Input.get_axis reads already have
            // from player.gd's own input handling.
            //
            // A BENCH OWNS THE FRAME THE SAME WAY, with no WASD reading of
            // its own -- BenchSeat.Update() polls E/WASD only to decide when
            // to stand back up, not to move anything, so there is genuinely
            // nothing for this class to do while seated, same as the car.
            if (_seatedCar != null || _seatedBench != null)
            {
                return;
            }

            if ((_isDancing || _isBreakdancing) && RequestsDanceCancel(keyboard))
            {
                _isDancing = false;
                _isBreakdancing = false;
            }

            float deltaTime = Time.deltaTime;
            if (_vitals != null)
            {
                _vitals.TickHitstun(deltaTime);
                _vitals.TickMana(deltaTime);
                _vitals.TickHealthRegen(deltaTime);
            }
            if (_melee != null)
            {
                _melee.Tick(deltaTime);
            }
            SoulsSword?.Tick(deltaTime);
            _fireballCaster?.Tick(deltaTime);
            _iceShards?.Tick(deltaTime);
            _poisonBreath?.Tick(deltaTime);
            _swordWave?.Tick(deltaTime);

            // DEATH IS POLLED HERE, NOT EVENT-DRIVEN -- see GrabCarry's own
            // doc comment for why an OnEnable subscription to
            // PlayerVitals.Died is unsafe in this codebase's own
            // scene-builder field-assignment order. Checked BEFORE every
            // early-return below (ragdoll/dead/stunned all lead into
            // ordinary falling, which knows nothing about a rope) so a
            // player who dies mid-swing is never left visibly tethered --
            // matches player.gd's own _die() calling release_grapple(false)
            // for exactly the same reason.
            if (_grappleSwing != null && _grappleSwing.IsActive &&
                _vitals != null && _vitals.IsDead)
            {
                _grappleSwing.Release(false);
            }

            // /PUSHUPS HOLDS THE BODY until it is standing again (PushUpEmote): a movement key, a spell key or a click calls it off and it gets
            // up. Mouse look still turns the camera (the camera reads the mouse, not this). A blow, death or a fall ends it at once, standing.
            if (IsDoingPushUps)
            {
                bool knockedOut = (_vitals != null && (_vitals.IsDead || _vitals.IsStunned)) || (_ragdoll != null && _ragdoll.IsLimp);
                if (knockedOut)
                {
                    _pushUps.Abort();
                }
                else
                {
                    TickPushUps(keyboard, deltaTime);
                    return;
                }
            }

            if (_ragdoll != null && _ragdoll.IsLimp)
            {
                // Physics owns the body now. Ticking locomotion here would call
                // Move on a CharacterController the ragdoll has just disabled,
                // which does nothing useful and complains about it every frame.
                // The footsteps have to be stopped by hand on every one of
                // these early returns: they are loops, so they do not run out
                // on their own, and a corpse walking on the spot is audible.
                _audio?.SilenceFootsteps();

                // SAME REASONING, FOR THE JETPACK -- EXPLICIT BUG REPORT:
                // "when your using jetpack and crash into a surface and
                // ragdoll, the jetpack also turns off even if your holding
                // space." EquippedJetpack.Tick (the only place that stops
                // its own flame/thrust audio) is never reached again while
                // this branch keeps returning early below, so holding
                // Space through the crash would otherwise leave the flame
                // burning and the thrust loop playing over a body that
                // is not flying anywhere any more.
                EquippedJetpack?.StopThrustEffects();

                // EXPLICIT BUG REPORT: "im still getting up at an angle when
                // i ragdoll." Jetpack.Tick is the only thing that ever eases
                // the body's own flight lean back toward upright, and it
                // never runs while this branch keeps returning early -- so
                // whatever lean was baked in at the exact instant of the
                // crash used to sit there completely frozen through the
                // whole limp ragdoll AND the entire "GettingUp" clip,
                // several frames longer than it took the player to actually
                // regain control. See ResetTilt's own doc comment for the
                // full reasoning; called every frame while limp (same as
                // StopThrustEffects just above) rather than once, so it also
                // catches a jetpack that gets equipped mid-ragdoll.
                EquippedJetpack?.ResetTilt();

                // A DEATH RAGDOLL NEVER TICKS ANY OF THIS -- IsDead is
                // checked here directly rather than relying on the ordinary
                // IsDead early-return further down, which this branch's own
                // `return` never reaches. Without this guard a dead body
                // would still accumulate rest time and let Space stand it
                // back up, which only makes sense for a knockdown, not a
                // death -- death stays permanent until an explicit Revive.
                bool dead = _vitals != null && _vitals.IsDead;
                if (!dead)
                {
                    // Drives the rest-timer/GettingUp recovery -- see
                    // Ragdoll.Tick's own doc comment for why this has to be
                    // called explicitly from here rather than Ragdoll
                    // running its own Update().
                    _ragdoll.Tick(deltaTime);

                    // EXPLICIT ASK: "the ragdoll can continue to take fall
                    // damage." The torso's own Rigidbody is what is
                    // actually falling now, not the (disabled)
                    // CharacterController -- IsRestingOnGround is the
                    // closest thing it has to isGrounded while limp (see
                    // its own doc comment).
                    //
                    // RETURN VALUE NOW CHECKED -- EXPLICIT BUG REPORT: "no
                    // second impact noise once i crash and then hit the
                    // ground and take fall damage a second time." A wall
                    // crash's own impact plays through the jetpackThrusting
                    // branch further down, but that branch is never reached
                    // again once ragdolled -- this call was already dealing
                    // the real, second round of damage for a genuine second
                    // impact (an initial wall crash knocking the body into a
                    // ragdoll that then actually falls and lands for real),
                    // it just silently discarded the return value instead of
                    // playing anything, matching the exact same
                    // "only a landing that actually hurt" rule the ordinary
                    // grounded landing case already follows below.
                    if (_vitals != null && _ragdoll.Torso != null)
                    {
                        float ragdollFallDamage =
                            _vitals.TickFallDamage(_ragdoll.IsRestingOnGround, _ragdoll.Torso.position.y);
                        if (ragdollFallDamage > 0.0f)
                        {
                            _audio?.PlayFallImpact();
                        }
                    }

                    if (_ragdoll.CanStandUp && keyboard.spaceKey.wasPressedThisFrame)
                    {
                        _ragdoll.BeginStandingUp();
                    }
                }
                return;
            }

            if (_vitals != null && _vitals.IsDead)
            {
                // A corpse with no ragdoll still falls, so keep ticking the
                // body with no input rather than stopping it dead in the air.
                _locomotion.Tick(default, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // Being hit takes the controls away for a moment. Gravity still
            // applies, so a stunned body in mid-air keeps falling instead of
            // hanging there.
            if (_vitals != null && _vitals.IsStunned)
            {
                _locomotion.Tick(default, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // SELF SMITE HOLDS THE BODY, as Pikachu's Thunder does (SelfSmiteAbility): from the cast until it frees the caster nothing else is
            // read, and the body is ticked with no movement (it is rooted), so gravity and the Animator carry on. Mouse look still turns the camera.
            if (_selfSmite != null && _selfSmite.HoldsTheBody)
            {
                _locomotion.Tick(new LocomotionInput { YawDegrees = transform.eulerAngles.y }, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // FROZEN FOR THE ONE-SHOT MANTLE CLIP'S OWN DURATION, checked
            // first of all -- matches player.gd's own _is_climb_topping_out
            // early-return being the very first thing _physics_process
            // checks, ahead of even the grapple. The animation's own root
            // motion is what is moving the visible body; nothing here
            // should fight it, and TickTopOut is what notices the clip has
            // actually finished and relocates the real CharacterController.
            if (_climbing != null && _climbing.IsToppingOut)
            {
                _climbing.TickTopOut();
                _audio?.SilenceFootsteps();
                return;
            }

            // A VAULT OWNS THE FRAME TOO: the body follows a path over the obstacle (PlayerVaulting).
            if (_vaulting != null && _vaulting.IsVaulting)
            {
                _vaulting.Tick(deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // THE SWING OWNS THE WHOLE FRAME WHILE ACTIVE, matching
            // player.gd's own _physics_process early-return into
            // _process_grapple_swing -- placed above TickCombat and the
            // ordinary WASD-to-locomotion dispatch below so neither ever
            // runs while a rope is attached. Reel/payout reuse the sprint/
            // crouch keys and release reuses jump/punch, the same keys
            // Godot's own version does, since the grapple has no other use
            // for them while it is active (see TickCombat's own weapon-
            // equipped branch, which this pre-empts by never being reached).
            //
            // CHECKED BEFORE CLIMBING, matching player.gd's own comment on
            // this exact ordering: "a swing that carries you past a wall
            // doesn't get hijacked into a wall-grab by the same held W
            // that's pumping it."
            if (_grappleSwing != null && _grappleSwing.IsActive)
            {
                // "Ragdolling until he reaches land or dashes" -- the SAME
                // double-tap gesture ground movement already dashes on, not
                // a second key. Checked before anything else in this block:
                // a dash is a deliberate bail-out, worth honouring even on
                // the exact frame reel/payout also reads the held half of
                // this same shift key.
                if (ConsumeDoubleTapShift(keyboard, deltaTime))
                {
                    _grappleSwing.Release(false);
                    _audio?.SilenceFootsteps();
                    return;
                }

                Vector2 swingMove = Vector2.zero;
                if (keyboard.wKey.isPressed) swingMove.y += 1.0f;
                if (keyboard.sKey.isPressed) swingMove.y -= 1.0f;
                if (keyboard.dKey.isPressed) swingMove.x += 1.0f;
                if (keyboard.aKey.isPressed) swingMove.x -= 1.0f;

                Transform swingYawSource = YawSource != null
                    ? YawSource
                    : (Camera.main != null ? Camera.main.transform : null);
                float swingYaw = swingYawSource != null ? swingYawSource.eulerAngles.y : 0.0f;

                bool boostedRelease = keyboard.spaceKey.wasPressedThisFrame;
                bool plainRelease = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                bool reelHeld = keyboard.leftShiftKey.isPressed;
                bool payoutHeld = keyboard.leftCtrlKey.isPressed;

                // THE GRAPPLE GUN OWNS THE RIGHT HAND, so a cast while
                // swinging goes through the left instead -- explicit ask:
                // "it still works just comes from your left hand". Same
                // digit1Key/origin-height/ignore-colliders shape as the
                // ordinary ground cast below.
                if (_fireballCaster != null && _skills.Pressed(SkillCatalog.Fireball, keyboard) && swingYawSource != null)
                {
                    float height = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f;
                    Vector3 origin = transform.position + Vector3.up * height;
                    _fireballCaster.TryCast(origin, ConvergedDirection(origin, swingYawSource), GetComponentsInChildren<Collider>());
                }
                if (_skills.Pressed(SkillCatalog.IceShards, keyboard))
                {
                    CastIceShards(swingYawSource);
                }
                if (_skills.Pressed(SkillCatalog.PoisonBreath, keyboard))
                {
                    CastPoisonBreath(swingYawSource);
                }
                if (_skills.Pressed(SkillCatalog.AcidRain, keyboard))
                {
                    CastAcidRain(swingYawSource);
                }
                AimIceShards(swingYawSource);
                AimPoisonBreath(swingYawSource);

                // THE ORDINARY WASD-MOVEMENT BRANCH BELOW NEVER RUNS WHILE
                // SWINGING (this whole block returns before reaching it),
                // and that branch is the only other place SetCastPose gets
                // called -- so without this, casting while swinging would
                // fire the fireball but show no left-arm raise at all.
                // GrappleSwing itself never touches this bone any more (see
                // its own class doc comment), so PlayerLocomotion's normal
                // ApplyCastingPose is free to layer this on top of whatever
                // the swing's own DriveAnimatorFromExternalVelocity pose is,
                // exactly like it would during ordinary ground movement.
                _locomotion.SetCastPose(SpellCastFraction, useLeftArm: true);

                _grappleSwing.Tick(swingMove, swingYaw, boostedRelease, plainRelease,
                    reelHeld, payoutHeld, deltaTime);

                // EXPLICIT BUG REPORT: "make it so my limbs cant clip
                // through walls and get stuck... swinging around with
                // grapple." A swing never ragdolls (Ragdoll.Activate's own
                // "NEVER RAGDOLLS OUT OF AN ACTIVE SWING"), so the bones
                // stay kinematic/animator-driven the whole time -- see
                // Ragdoll.TickKinematicLimbClearance's own doc comment for
                // why that means nothing else ever pulls a swinging limb
                // back out of a wall it swung into.
                _ragdoll?.TickKinematicLimbClearance();

                _audio?.SilenceFootsteps();
                return;
            }

            // WALL GRABS ARE ASKED FOR, NOT STUMBLED INTO. Holding Space for a moment (a tap is still a jump)
            // is how the player says "grab that wall"; running, jumping through a window or walking through a
            // doorway never asks. Updated every frame, before the attached branch below, so letting go of the
            // key while hanging on is seen (a grab needs a fresh hold). Not with a jetpack or parachute on the
            // back: Space means thrust or canopy there.
            _grabIntent.Update(keyboard.spaceKey.isPressed, deltaTime);
            // Nor while Grow or Shrink has the body at another size: the grab and the ledge are measured for a 2.6 m body.
            bool wallGrabsAllowed = EquippedJetpack == null && EquippedParachute == null && !_locomotion.IsResized;

            // ALREADY ATTACHED TO A WALL OWNS THE WHOLE FRAME TOO, matching
            // player.gd's own _is_climbing early-return into
            // _process_climbing -- a real reported bug there ("the sprint
            // noise is playing while climbing") is why footsteps are
            // silenced here rather than only at grab time: nothing
            // downstream ever runs again to turn them off otherwise.
            if (_climbing != null && _climbing.IsClimbing)
            {
                _climbing.Tick(keyboard.spaceKey.wasPressedThisFrame,
                    keyboard.wKey.isPressed, keyboard.sKey.isPressed, _vitals, deltaTime,
                    dropPressed: keyboard.leftCtrlKey.wasPressedThisFrame, leftHeld: keyboard.aKey.isPressed, rightHeld: keyboard.dKey.isPressed);
                _audio?.SilenceFootsteps();
                return;
            }

            // NOT YET CLIMBING -- try to grab on. Gated the same way
            // player.gd's own _try_start_climb call is: not carrying
            // something (both hands full) and not mid-dash (a committed
            // burst nothing should interrupt). Checked ahead of TickCombat/
            // ordinary movement, and if it succeeds THIS frame, returns
            // immediately -- see PlayerClimbing.Tick's own doc comment on
            // why the very first climbing frame must not also fall through
            // to ordinary WASD movement computed from the same held W that
            // just triggered the grab.
            // CLIMBING IS A SKILL (the Freerunner tree's Climb, which also pulls you up onto ledges): not learnt, no wall is ever grabbed.
            if (_climbing != null && wallGrabsAllowed && _skills.Knows(SkillCatalog.Climb) &&
                (_grabCarry == null || !_grabCarry.IsCarrying) &&
                !_locomotion.IsDashing)
            {
                float stamina = _vitals != null ? _vitals.Stamina : float.MaxValue;
                if (_climbing.TryStart(_grabIntent, keyboard.wKey.isPressed, stamina))
                {
                    _climbing.Tick(keyboard.spaceKey.wasPressedThisFrame,
                        keyboard.wKey.isPressed, keyboard.sKey.isPressed, _vitals, deltaTime,
                    dropPressed: keyboard.leftCtrlKey.wasPressedThisFrame, leftHeld: keyboard.aKey.isPressed, rightHeld: keyboard.dKey.isPressed);
                    _audio?.SilenceFootsteps();
                    return;
                }
            }

            // WHERE THE PLAYER IS TRYING TO GO (camera yaw + WASD), not where the body happens to face: the vault and the auto-climb look along this, so they work
            // however the camera is turned and however the body has caught up with it.
            Vector3 wishDirection = Vector3.zero;
            {
                Transform wishYaw = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
                float yaw = wishYaw != null ? wishYaw.eulerAngles.y : transform.eulerAngles.y;
                float wx = (keyboard.dKey.isPressed ? 1.0f : 0.0f) - (keyboard.aKey.isPressed ? 1.0f : 0.0f);
                float wz = (keyboard.wKey.isPressed ? 1.0f : 0.0f) - (keyboard.sKey.isPressed ? 1.0f : 0.0f);
                if (wx != 0.0f || wz != 0.0f)
                {
                    wishDirection = Quaternion.Euler(0.0f, yaw, 0.0f) * new Vector3(wx, 0.0f, wz).normalized;
                }
            }

            // RUNNING AT A LOW OBSTACLE VAULTS IT (Shift + W; holding Space is still the way to ask for a climb, checked above).
            if (_vaulting != null && !_locomotion.IsDashing && !_locomotion.IsResized && (_grabCarry == null || !_grabCarry.IsCarrying)
                && _vaulting.TryStart(keyboard.wKey.isPressed, keyboard.leftShiftKey.isPressed, _locomotion.IsGrounded, _locomotion.CurrentSpeed, deltaTime, wishDirection))
            {
                _vaulting.Tick(deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // RUNNING INTO A TALL WALL CLIMBS IT (Shift + W, held; guarded against stairs, doors and grazing -- see PlayerClimbing.TryStartAuto). After the
            // vault above so a low obstacle is vaulted, and after the Space-hold grab so a deliberate grab always wins.
            if (_climbing != null && wallGrabsAllowed && _skills.Knows(SkillCatalog.Climb) && !_locomotion.IsDashing
                && (_grabCarry == null || !_grabCarry.IsCarrying))
            {
                float stamina = _vitals != null ? _vitals.Stamina : float.MaxValue;
                if (_climbing.TryStartAuto(keyboard.wKey.isPressed, keyboard.leftShiftKey.isPressed, _locomotion.IsGrounded, stamina, deltaTime, wishDirection))
                {
                    _climbing.Tick(keyboard.spaceKey.wasPressedThisFrame,
                        keyboard.wKey.isPressed, keyboard.sKey.isPressed, _vitals, deltaTime,
                        dropPressed: keyboard.leftCtrlKey.wasPressedThisFrame, leftHeld: keyboard.aKey.isPressed, rightHeld: keyboard.dKey.isPressed);
                    _audio?.SilenceFootsteps();
                    return;
                }
            }

            // ALREADY STUCK TO A WALL OWNS THE WHOLE FRAME TOO -- same
            // pattern as the climbing block above.
            if (_wallStick != null && _wallStick.IsSticking)
            {
                Vector2 stickMove = Vector2.zero;
                if (keyboard.wKey.isPressed) stickMove.y += 1.0f;
                if (keyboard.sKey.isPressed) stickMove.y -= 1.0f;
                if (keyboard.dKey.isPressed) stickMove.x += 1.0f;
                if (keyboard.aKey.isPressed) stickMove.x -= 1.0f;

                _wallStick.Tick(keyboard.spaceKey.wasPressedThisFrame, stickMove, _vitals, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // NOT YET STICKING -- try to grab on. Checked AFTER climbing's
            // own grab attempt above (see WallStick's own doc comment on why
            // the two never fight over the same frame: this one is gated on
            // being airborne, climbing's own grab is not gated on grounded
            // at all, so an airborne sprint+W into a shallow-enough wall
            // still climbs a ladder exactly as before this existed).
            if (_wallStick != null && wallGrabsAllowed && _skills.Knows(SkillCatalog.WallStick) &&
                (_grabCarry == null || !_grabCarry.IsCarrying) &&
                !_locomotion.IsDashing)
            {
                float stamina = _vitals != null ? _vitals.Stamina : float.MaxValue;
                if (_wallStick.TryStart(_grabIntent, keyboard.leftShiftKey.isPressed, _locomotion.IsGrounded, stamina))
                {
                    Vector2 stickMove = Vector2.zero;
                    if (keyboard.wKey.isPressed) stickMove.y += 1.0f;
                    if (keyboard.sKey.isPressed) stickMove.y -= 1.0f;
                    if (keyboard.dKey.isPressed) stickMove.x += 1.0f;
                    if (keyboard.aKey.isPressed) stickMove.x -= 1.0f;

                    _wallStick.Tick(keyboard.spaceKey.wasPressedThisFrame, stickMove, _vitals, deltaTime);
                    _audio?.SilenceFootsteps();
                    return;
                }
            }

            // TAB LOCKS ON, and pressing it again cycles through the
            // surrounding enemies (then releases). Read HERE rather than in
            // the camera, behind every menu/console/stun early-return above:
            // the last lock-on read its key inside the camera and raced the
            // player menu's own binding (see LockOn's doc comment).
            if (keyboard.tabKey.wasPressedThisFrame && _orbitCamera != null)
            {
                _orbitCamera.PressLockOn();
            }

            // Q SWAPS THE CAMERA TO THE OTHER SHOULDER -- standard
            // third-person-shooter kit (RE4, Gears, Fortnite): peek round a
            // corner without exposing the body, or clear a wall your usual
            // shoulder is against. Works with anything in hand.
            if (keyboard.qKey.wasPressedThisFrame && _orbitCamera != null)
            {
                _orbitCamera.ToggleShoulder();
            }

            if (EquippedGun != null && _gunFeel == null)
            {
                _gunFeel = GetComponent<GunFeel>();   // added at runtime by the gun's OnEquip
            }

            TickCombat(deltaTime);

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1.0f;
            if (keyboard.sKey.isPressed) move.y -= 1.0f;
            if (keyboard.dKey.isPressed) move.x += 1.0f;
            if (keyboard.aKey.isPressed) move.x -= 1.0f;

            Transform yawSource = YawSource != null
                ? YawSource
                : (Camera.main != null ? Camera.main.transform : null);

            // THE FIREBALL SPELL, independent of whatever is equipped --
            // press 1 and it fires regardless of fists, bottle, sword or
            // gun, since it is an innate ability rather than something in
            // the weapon slot. Placed after every early-return above
            // (ragdoll/dead/stunned/topping-out/grapple-active all bail out
            // of Update long before reaching here), so those states block
            // a cast the same way they already block everything else.
            //
            // EVERY SPELL NOW CASTS FROM ITS SLOT ON THE BAR (PlayerSkills.Pressed): whichever of the keys 1 to 0 holds it, once learnt.
            if (_fireballCaster != null && _skills.Pressed(SkillCatalog.Fireball, keyboard) && yawSource != null)
            {
                // Roughly chest height, matching the same fraction
                // MeleeAttacker.SwingOrigin/the weapon-throw origin below
                // already use for "a sensible point on the body to launch
                // something from."
                float height = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f;
                Vector3 origin = transform.position + Vector3.up * height;
                _fireballCaster.TryCast(origin, ConvergedDirection(origin, yawSource), GetComponentsInChildren<Collider>());
            }

            // 3: ICE SHARDS, innate like the fireball: four shards one after another down the crosshair, each sticking where it lands. The
            // rest of a volley follows the crosshair as it goes out.
            if (_skills.Pressed(SkillCatalog.IceShards, keyboard))
            {
                CastIceShards(yawSource);
            }
            // 4: POISON GAS, innate like the fireball: a cloud of purple gas blown out down the crosshair; whatever breathes it is poisoned
            // (never the one who blew it).
            if (_skills.Pressed(SkillCatalog.PoisonBreath, keyboard))
            {
                CastPoisonBreath(yawSource);
            }
            // 5: ACID RAIN (a god-tier test spell): a storm of acid over a wide area where the crosshair points; roofs shelter.
            if (_skills.Pressed(SkillCatalog.AcidRain, keyboard))
            {
                CastAcidRain(yawSource);
            }
            // SELF SMITE (Electromancer): Pikachu's Thunder, a bolt from the clouds onto the caster, who bursts with electricity. Not while
            // swinging on the grapple (the swing branch above never reaches here): the move roots the body.
            if (_skills.Pressed(SkillCatalog.SelfSmite, keyboard) && (_selfSmite != null || (_selfSmite = GetComponent<SelfSmiteAbility>()) != null))
            {
                _selfSmite.TryCast();
            }
            // GROW AND SHRINK (Wizard): ten times as big, or a tenth, for 30 s (BodySizeAbility). The same key again ends it early.
            if (_skills.Pressed(SkillCatalog.Grow, keyboard) && (_bodySize != null || (_bodySize = GetComponent<BodySizeAbility>()) != null))
            {
                _bodySize.TryGrow();
            }
            if (_skills.Pressed(SkillCatalog.Shrink, keyboard) && (_bodySize != null || (_bodySize = GetComponent<BodySizeAbility>()) != null))
            {
                _bodySize.TryShrink();
            }
            AimIceShards(yawSource);
            AimPoisonBreath(yawSource);

            // 8: BLOODRAGE, innate like the fireball (see BloodrageAbility): +25% speed and melee damage, the Bloodrage walk/run/jump, a red aura, 7.5 s.
            if (_skills.Pressed(SkillCatalog.Bloodrage, keyboard))
            {
                if (_bloodrageAbility == null)
                {
                    _bloodrageAbility = GetComponent<BloodrageAbility>();
                }
                _bloodrageAbility?.TryActivate();
            }

            // A JETPACK TAKES SHIFT OVER TOO WHILE ACTUALLY THRUSTING, not
            // just Space -- the explicit ask ("shift is a boost when midair
            // using jetpack") redefines what the key means the instant
            // thrust is live. Computed before wantsSprint below so that
            // check can exclude this case rather than double-booking one
            // key with two unrelated meanings and draining stamina for a
            // mechanic that is not supposed to cost any.
            //
            // AIRBORNE ONLY -- EXPLICIT BUG REPORT: "when you press space on
            // the floor with jetpack on it just does boost right away but
            // make it so you can still jump with jetpack on and but when you
            // hold space mid air then it activates." Space used to mean
            // thrust the instant the jetpack was equipped, grounded or not,
            // which left no way to jump at all while wearing it. Requiring
            // !_locomotion.IsGrounded here is also what makes the dispatch
            // just below correctly fall through to a jump instead while
            // still standing on the ground.
            bool jetpackThrusting = EquippedJetpack != null && keyboard.spaceKey.isPressed &&
                !_locomotion.IsGrounded;

            // A PARACHUTE TAKES A FRESH PRESS OF SPACE WHILE AIRBORNE, not a
            // hold -- EXPLICIT ASK: "press space and open and close it."
            // wasPressedThisFrame rather than isPressed, since this is a
            // toggle (open, then a second press later closes it again), not
            // a held state the way jetpack thrust is. Airborne-only for the
            // same reason jetpackThrusting is: while grounded this stays
            // false, so the dispatch below still falls through to an
            // ordinary jump instead, and there is nothing to "open" while
            // standing on the ground anyway. EquippedJetpack and
            // EquippedParachute share Jetpack's own "back" equip slot (see
            // ItemRegistry.Parachute's own doc comment), so Inventory's own
            // slot eviction already guarantees at most one of them is ever
            // equipped at once -- no extra mutual-exclusion needed here.
            bool parachuteToggledThisFrame = EquippedParachute != null &&
                keyboard.spaceKey.wasPressedThisFrame && !_locomotion.IsGrounded;

            // Sprint only forwards. Sprinting sideways or backwards has no
            // animation and reads as a glitch rather than as speed.
            //
            // AIMING DOWN SIGHTS ENDS A SPRINT (and holding aim stops you
            // starting one) -- you cannot run flat out and hold a steady
            // sight picture (Fortnite's sprint puts the weapon away outright).
            bool wantsSprint = !jetpackThrusting && !_isAimingGun
                && keyboard.leftShiftKey.isPressed && move.y > 0.0f;

            // Stamina decides, not the key. Asking rather than checking a flag
            // keeps the exhaustion latch in one place.
            bool sprinting = _vitals != null
                ? _vitals.TickStamina(wantsSprint, deltaTime)
                : wantsSprint;

            // Swinging roots the body, which is what makes a combo a
            // commitment rather than something you can walk out of.
            //
            // A soulslike swing roots the same way, from the click to the end
            // of its recovery -- and a held guard walks at half speed.
            if (_melee == null || !_melee.IsSwinging)
            {
                _swingFacingActive = false;
            }
            Vector2 heldMove = move;
            SoulsSwordCombat soulsNow = SoulsSword;
            bool soulsArmed = soulsNow != null && soulsNow.IsEquipped;
            if (_melee != null && _melee.IsSwinging)
            {
                // The aerial sword spin can be steered a little (half pace), as Link's can. An UNARMED strike keeps part of the pace (the legs walk
                // under it, no longer frozen) and STEPS INTO the blow toward where the body faces while the fist is going out (see
                // MeleeAttacker.UnarmedMoveFraction / LungeFraction).
                move *= _melee.SwingMoveFraction;
                float lunge = _melee.LungeFraction;
                if (lunge > 0.0f && yawSource != null)
                {
                    Vector3 stepWorld = _swingFacingActive ? Quaternion.Euler(0.0f, _swingYaw, 0.0f) * Vector3.forward : transform.forward;
                    Vector3 stepLocal = Quaternion.Euler(0.0f, -yawSource.eulerAngles.y, 0.0f) * stepWorld;
                    move = Vector2.ClampMagnitude(move + new Vector2(stepLocal.x, stepLocal.z) * lunge, 1.0f);
                }
                sprinting = false;
            }
            else if (soulsArmed && soulsNow.IsCommitted)
            {
                // THE SWORD'S SWINGS PLAY ON THE UPPER BODY, so the legs walk
                // under them -- EXPLICIT ASK: "make the legs walk and not freeze
                // up during sword use too." A committed swing still slows you
                // hard (CommittedMoveSpeedFraction), it just no longer nails your
                // feet to the floor; 0 there restores the old full root.
                move *= soulsNow.CommittedMoveSpeedFraction;
                sprinting = false;
            }
            else if (soulsArmed && soulsNow.IsBlocking)
            {
                move *= 0.5f;
                sprinting = false;
            }

            float yawDegrees = yawSource != null ? yawSource.eulerAngles.y : 0.0f;
            _lastYawDegrees = yawDegrees;
            // A roll goes where the keys were held even out of a swing that
            // was rooting the body -- the pre-rooting input, not the zeroed one.
            TickDash(keyboard, soulsArmed ? heldMove : move, yawDegrees, deltaTime);

            bool chargingJumpNow = false;
            float jumpChargeFraction = 0.0f;
            bool jumpReleased;
            // (A sword spin on the ground holds the body, charge included: no jumping out of it.)
            bool spinHoldsTheBody = _melee != null && _melee.IsSpinAttacking;
            // SUPER JUMP IS A SKILL NOW (owner, 2026-10-14: the Freerunner
            // tree's Super Jump). The charged jump was kept in the vault
            // ("get rid of charged jump for now that is going to be a skill
            // tree ability keep it in the vault"); learning Super Jump routes
            // Space back through TickJumpCharge, untouched: held, it charges;
            // let go, it jumps, up to ChargedJumpHeightMultiplier times as
            // high. Without the skill, a bare tap of Space while grounded
            // fires the jump at once at PlayerLocomotion's base JumpVelocity
            // (JumpReleased with JumpChargeFraction 0 resolves the height
            // multiplier to exactly 1). Computed unconditionally (not just
            // when the jetpack skips its own Tick below): it is grounded-gated
            // on its own and jetpackThrusting is forced false while grounded,
            // so Space on the ground always means jump, jetpack or not.
            if (_skills.Knows(SkillCatalog.SuperJump) && !spinHoldsTheBody)
            {
                TickJumpCharge(keyboard, _locomotion.IsGrounded, deltaTime,
                    out chargingJumpNow, out jumpReleased, out jumpChargeFraction);
                // The leap's own height never hurts its landing (PlayerVitals.ForgiveFallAbove).
                if (jumpReleased && jumpChargeFraction > 0.0f && _locomotion.IsGrounded)
                {
                    _vitals?.ForgiveFallAbove(transform.position.y);
                }
            }
            else
            {
                _jumpHeld = false;
                jumpReleased = _locomotion.IsGrounded && keyboard.spaceKey.wasPressedThisFrame && !spinHoldsTheBody;
            }

            // A JETPACK TAKES SPACE OVER ONLY ONCE ACTUALLY AIRBORNE --
            // EXPLICIT BUG REPORT: "when you press space on the floor with
            // jetpack on it just does boost right away but make it so you
            // can still jump with jetpack on and but when you hold space
            // mid air then it activates." While grounded, jetpackThrusting
            // is already false (see its own doc comment above), so Tick
            // below never reactivates thrust/flame/audio on the ground --
            // WASD keeps steering horizontally exactly the way ordinary
            // falling already lets it, matching the jetpack's own explicit
            // ask ("mixed with wasd to move around").
            //
            // STILL CALLED EVERY FRAME WHILE EQUIPPED, GROUNDED OR NOT --
            // EXPLICIT BUG REPORT: "after i crash and recover sometimes i
            // am stuck at an angle." This used to be gated on
            // `!_locomotion.IsGrounded`, so the instant the player landed
            // (including standing back up from a crash ragdoll) Tick simply
            // stopped being called at all -- and Tick's own TickTilt is the
            // ONLY thing that ever eases _currentTilt/the spine's own
            // localRotation back toward level (see TickTilt's own doc
            // comment). Whatever lean the body had at the exact instant of
            // the crash stayed baked into the spine transform forever,
            // since nothing else ever touches it. Now Tick runs
            // unconditionally while equipped; with thrustHeld already false
            // on the ground, TickTilt's own target collapses to
            // Vector2.zero and SmoothDamps the lean back to upright over
            // TiltSmoothTime, the same graceful ease-out used everywhere
            // else in this system -- not an instant snap.
            if (EquippedJetpack != null)
            {
                float cameraPitch = _orbitCamera != null ? _orbitCamera.Pitch : 0.0f;
                EquippedJetpack.Tick(jetpackThrusting, move, keyboard.leftShiftKey.isPressed, deltaTime, cameraPitch);
            }

            // EXPLICIT BUG REPORT: "make it so my limbs cant clip through
            // walls and get stuck when im flying around with jetpack." Only
            // while actually thrusting (matches jetpackThrusting's own
            // airborne-and-holding-space gate) -- a crash hard enough to
            // ragdoll is already handled by Ragdoll.Activate's own
            // DepenetrateFromWorld further below; this instead covers every
            // softer graze/near-miss along the way that never crosses that
            // threshold, where a Superman-dive arm can still reach a nearby
            // wall with nothing else correcting it. See Ragdoll.
            // TickKinematicLimbClearance's own doc comment for the mechanism.
            if (jetpackThrusting)
            {
                _ragdoll?.TickKinematicLimbClearance();
            }

            // CALLED EVERY FRAME WHILE EQUIPPED, GROUNDED OR NOT -- same
            // reasoning EquippedJetpack.Tick just above is, per that block's
            // own doc comment: Tick is also where a parachute notices it
            // landed and auto-closes itself (see IParachute.Tick's own doc
            // comment), which has to keep happening even after the toggle
            // press itself is long past and even once grounded, not just on
            // the one frame Space was actually pressed.
            if (EquippedParachute != null)
            {
                EquippedParachute.Tick(parachuteToggledThisFrame, _locomotion.IsGrounded, deltaTime);
            }

            // A CAST NO LONGER BORROWS THE GUN'S OWN AIM POSE -- the explicit
            // ask was a dedicated animation (a raised right hand), so
            // isCastingFireball now only feeds the jetpack-suppression check
            // below, the same way gun-aiming does; the actual visual is a
            // separate procedural pose (see PlayerLocomotion.SetCastPose)
            // rather than a second state sharing LocomotionInput.Aiming.
            bool isCastingFireball = IsCastingSpell;
            bool isAiming = _isAimingGun || isCastingFireball;
            // THE GRAPPLE GUN OWNS THE RIGHT HAND -- see SetCastPose's own
            // doc comment. EquippedHook alone, not GrappleSwing.IsActive:
            // the gun is in that hand whether or not a rope currently
            // happens to be attached.
            bool castRaisesLeftArm = _grappleSwing != null && _grappleSwing.EquippedHook != null;
            _locomotion.SetCastPose(SpellCastFraction, castRaisesLeftArm);

            var input = new LocomotionInput
            {
                Move = move,
                YawDegrees = yawDegrees,
                Sprint = sprinting,
                Crouch = keyboard.leftCtrlKey.isPressed,
                // Charging kept in the vault for now -- see the dispatch
                // above (and TickJumpCharge's own doc comment) for why
                // Space fires a plain, un-charged jump directly instead.
                ChargingJump = chargingJumpNow,
                JumpReleased = jumpReleased,
                JumpChargeFraction = jumpChargeFraction,
                // Gun-aiming only now -- see isCastingFireball's own comment
                // just above for why a cast no longer sets this too.
                Aiming = _isAimingGun,
                AimCameraPosition = _orbitCamera != null ? _orbitCamera.transform.position : Vector3.zero,
                AimCameraForward = _orbitCamera != null ? _orbitCamera.transform.forward : Vector3.forward,
                // The two-handed carry (the 2hsw clips). A one-handed sword is carried by the ordinary walk's own arm, its attacks posed on top.
                SwordEquipped = _melee != null && _melee.EquippedWeapon != null &&
                                _melee.EquippedWeapon.MovesetId == ComboChain.Sword &&
                                !(_melee.EquippedWeapon is ISwordBlade oneHandedBlade && oneHandedBlade.IsOneHanded),
                GunEquipped = EquippedGun != null,
                // Where the gun's shot would end -- the arm aims at it (see
                // LocomotionInput.GunAimPoint).
                GunAimPoint = _gunFeel != null ? _gunFeel.AimPoint : Vector3.zero,
                HasGunAimPoint = EquippedGun != null && _gunFeel != null && _gunFeel.HasAimPoint,
                LockedOn = _orbitCamera != null && _orbitCamera.IsLockedOn,
                // AIMING WINS, not jetpackThrusting raw -- "aim and shoot
                // while using the jetpack starts glitching", an explicit
                // report. Aiming and JetpackThrusting are both bool-gated
                // Any State entries (see PlayerRigBuilder's own
                // AddAimingState/AddJetpackIdleState); Unity re-evaluates
                // every Any State condition every frame regardless of the
                // current state, and with both true at once there is no
                // single well-defined winner, which is exactly what reads
                // as glitching -- the state machine has no reason to settle
                // on one over the other. This only gates the ANIMATION bool;
                // EquippedJetpack.Tick above still gets the real,
                // un-suppressed jetpackThrusting, so holding right-click
                // never weakens the actual thrust/lean it's flying on.
                JetpackThrusting = jetpackThrusting && !isAiming,
                // UN-SUPPRESSED BY isAiming, matching JetpackThrusting's own
                // comment just above on why EquippedJetpack.Tick's real
                // thrust/lean never weakens while aiming -- this drives real
                // movement speed, not an Animator Any-State bool, so there
                // is no state-machine conflict here to suppress in the
                // first place.
                JetpackBoosting = jetpackThrusting && keyboard.leftShiftKey.isPressed,
                JetpackDiving = jetpackThrusting && keyboard.leftShiftKey.isPressed && move.y > 0.0f,
                Dancing = _isDancing,
                Breakdancing = _isBreakdancing,
            };

            float horizontalSpeedBeforeMove = _locomotion.CurrentSpeed;
            _locomotion.Tick(input, deltaTime);

            // EXPLICIT ASK: "horizontal 'fall damage' when using the jetpack
            // and you crash into a wall... if the amount of damage you take
            // doesnt kill you, you still ragdoll." Gated on jetpackThrusting
            // specifically, not just EquippedJetpack != null -- an ordinary
            // walk into a wall (no thrust) is not a crash, it is just
            // bumping into something, and ApplyCrashDamage's own
            // SafeFallDistance floor already makes a slow, harmless jetpack
            // nudge deal no damage either way.
            //
            // EDGE-DETECTED via _wasJetpackCrashing -- a real contact
            // reports true every frame the controller is still pressed into
            // the wall, and applying fresh damage each of those frames
            // would be continuous, not a single impact.
            //
            // HitWallThisFrame, NOT CollisionFlags.Sides -- EXPLICIT BUG
            // REPORT: "the jetpack sideways fall damage thing still hasnt
            // worked, make it do it upon my playermodel coming into
            // contact with the wall or object." CollisionFlags is a coarse
            // summary bitmask of the whole Move call; PlayerLocomotion's
            // own OnControllerColliderHit now reads the actual per-contact
            // normal Unity reports for the real hit, which is the more
            // direct and reliable signal for "did the body really touch a
            // wall" -- see PlayerLocomotion.HitWallThisFrame's own doc
            // comment.
            if (jetpackThrusting)
            {
                bool hitWall = _locomotion.HitWallThisFrame;
                bool isNewWallContact = IsNewWallContact(
                    hitWall, _wasJetpackCrashing, _locomotion.LastWallHitNormal, _lastJetpackCrashNormal);

                if (isNewWallContact && _vitals != null)
                {
                    float damage = _vitals.ApplyCrashDamage(horizontalSpeedBeforeMove);
                    if (damage > 0.0f)
                    {
                        _audio?.PlayFallImpact();
                        if (!_vitals.IsDead)
                        {
                            // EXPLICIT BUG REPORT: "whenever i fly into a
                            // wall or window i just get stuck in it" --
                            // STILL true after a first attempt at a fixed
                            // pull-back nudge here (a guess at how far a
                            // limb tunnelled, which could not know how far
                            // a Superman-dive's outstretched arm actually
                            // reached past the wall on any given hit).
                            // Ragdoll.Activate now measures and clears the
                            // real overlap itself (see its own
                            // DepenetrateFromWorld), so this only has to
                            // hand it a knockback DIRECTION -- the real
                            // contact normal Unity just gave us, preferred
                            // over -velocity since velocity is often more
                            // steeply angled into the wall than the surface
                            // itself.
                            Vector3 awayFromWall = _locomotion.LastWallHitNormal.sqrMagnitude > 0.0001f
                                ? _locomotion.LastWallHitNormal
                                : -_locomotion.Velocity;
                            _vitals.TriggerCrashKnockdown(awayFromWall);
                        }
                    }
                }

                // Tracked whenever hitWall is true, not only when a fresh
                // impact actually fired -- the NEXT frame's own angle check
                // needs to compare against the MOST RECENT face touched
                // (so smoothly sliding along one continuous curved surface
                // never falsely accumulates into "a new wall" one small
                // step at a time), not a stale snapshot from several frames
                // back.
                if (hitWall)
                {
                    _lastJetpackCrashNormal = _locomotion.LastWallHitNormal;
                }
                _wasJetpackCrashing = hitWall;
            }
            else
            {
                _wasJetpackCrashing = false;
            }

            // AFTER the move, so grounded and height are this frame's, not the
            // previous one's. Checking before would measure the fall one frame
            // short and let a lethal drop land as a survivable one.
            if (_vitals != null)
            {
                float fallDamage =
                    _vitals.TickFallDamage(_locomotion.IsGrounded, transform.position.y);
                if (fallDamage > 0.0f)
                {
                    // Only a landing that actually HURT. A drop inside the safe
                    // distance deals nothing and makes no noise, which is what
                    // keeps the sound meaning something rather than firing on
                    // every hop off a kerb.
                    _audio?.PlayFallImpact();
                }
            }

            _audio?.UpdateFootsteps(
                _locomotion.CurrentSpeed > 0.01f, _locomotion.IsGrounded, _locomotion.IsSprinting);
        }

        // ---- UNARMED STRIKES: turn to the target, remember the facing for the lunge -------------------------------------------------------------

        private bool _swingFacingActive;
        private float _swingYaw;
        private float _lastYawDegrees;

        /// <summary>
        /// A punch goes where the body faces, and the body only turned to the camera while MOVING: standing still after turning the camera the punch went
        /// the way you used to face. So a punch turns the body, quickly, to the nearest enemy near where the camera looks (punch magnetism), else to the camera.
        /// </summary>
        private void OnMeleeSwingStarted(ComboStage stage)
        {
            if (_melee == null || _locomotion == null || _melee.EquippedWeapon != null || !FistStrikes.Has(stage.Animation))
            {
                return;
            }
            Transform yawSource = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
            float cameraYaw = yawSource != null ? yawSource.eulerAngles.y : _lastYawDegrees;
            float yaw = _melee.TryFindMagnetYaw(cameraYaw, out float toTarget) ? toTarget : cameraYaw;
            _swingYaw = yaw;
            _swingFacingActive = true;
            _locomotion.SetFacingOverride(yaw, 1200.0f, 0.22f);
        }

        /// <summary>
        /// How long left-click has to be held before it becomes a heavy punch.
        ///
        /// Long enough that an ordinary click -- typically well under 150ms
        /// between press and release -- never rolls one by accident, short
        /// enough that deliberately holding does not feel like waiting for a
        /// laggy input to register.
        /// </summary>
        public const float HeavyPunchHoldSeconds = 0.4f;

        private bool _punchHeld;
        private float _punchHeldSeconds;

        /// <summary>
        /// Punching, holding for a heavy one, and blocking.
        ///
        /// THE PUNCH FIRES ON RELEASE, NOT ON PRESS, which it has to: whether
        /// this was a jab or a haymaker is not known until the button comes
        /// back up. That is the cost of a hold-to-charge attack and it is the
        /// arrangement Godot uses.
        /// </summary>
        private void TickCombat(float deltaTime)
        {
            if (Mouse.current == null)
            {
                return;
            }

            // DEFAULTS OFF, SET BACK ON BELOW WHEREVER RIGHT-CLICK ACTUALLY
            // MEANS AIMING. Every early return below this line (carrying
            // something, the grapple hook equipped, unarmed) is a state
            // that is not aiming, so this only has to be re-armed by the
            // two branches that are.
            if (_orbitCamera != null)
            {
                _orbitCamera.IsAiming = false;
            }
            _isAimingGun = false;

            // Both hands are full while carrying something -- matches
            // player.gd's own "entirely while carrying something" guard on
            // punch/block input.
            if (_grabCarry != null && _grabCarry.IsCarrying)
            {
                return;
            }

            // SCROLL WHEEL: put the weapon in hand away and take out the other (primary <-> secondary). Not while carrying (returned above), and not
            // mid-aim: a gun being aimed down would otherwise vanish out of the player's hands.
            if (_inventory != null && !(Mouse.current.rightButton.isPressed))
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && Time.unscaledTime >= _nextWeaponSwitch)
                {
                    if (_inventory.CycleWeapon(gameObject))
                    {
                        _nextWeaponSwitch = Time.unscaledTime + 0.25f;
                    }
                }
            }

            // A SPRAY CAN (2026-10-15): right-click at a wall opens the graffiti canvas; nothing is punched or blocked with a can in hand.
            if (EquippedSprayCan != null)
            {
                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    SprayRequested?.Invoke(this);
                }
                return;
            }

            // A GUN TAKES OVER LEFT/RIGHT-CLICK AND RELOAD ENTIRELY WHILE
            // EQUIPPED, matching player.gd's own separate `if _equipped_gun
            // != null` branch: semi-automatic (a fresh press fires once,
            // none of the fist system's hold-for-heavy tracking or cooldown
            // gating here -- the gun tracks its own internally), aiming is
            // continuous for as long as right is HELD (unlike block()'s own
            // one-shot press), and reload is its own key rather than sharing
            // one with anything else. Checked ahead of the grapple hook and
            // the generic weapon branch below since a gun is neither.
            if (EquippedGun != null)
            {
                bool aiming = Mouse.current.rightButton.isPressed;
                if (_orbitCamera != null)
                {
                    _orbitCamera.IsAiming = aiming;
                }
                _isAimingGun = aiming;

                Transform gunYawSource = YawSource != null
                    ? YawSource
                    : (Camera.main != null ? Camera.main.transform : null);

                // An automatic (the AK-47) fires for as long as the button is held, at its own rate (the gun gates it); anything else once per press.
                bool trigger = EquippedGun.IsAutomatic ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
                if (trigger && gunYawSource != null)
                {
                    EquippedGun.Fire(gunYawSource.position, gunYawSource.forward, aiming, gameObject);
                }
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    EquippedGun.TryReload();
                }
                return;
            }

            // A FLAMETHROWER TAKES OVER LEFT-CLICK ENTIRELY WHILE EQUIPPED,
            // same "exclusive dispatch" shape as EquippedGun just above --
            // but HELD, not a single press: see
            // IEquippableFlamethrower.Tick's own doc comment for why this
            // isn't folded into EquippedGun itself. isPressed rather than
            // wasPressedThisFrame is what makes the stream continuous for
            // as long as the button stays down.
            if (EquippedFlamethrower != null)
            {
                Transform flameYawSource = YawSource != null
                    ? YawSource
                    : (Camera.main != null ? Camera.main.transform : null);
                if (flameYawSource != null)
                {
                    EquippedFlamethrower.Tick(Mouse.current.leftButton.isPressed,
                        flameYawSource.position, flameYawSource.forward, gameObject, deltaTime);
                }
                return;
            }

            // THE GRAPPLING HOOK TAKES OVER LEFT-CLICK ENTIRELY WHILE
            // EQUIPPED, matching player.gd's own separate
            // `elif _equipped_grapple != null` branch. FIRING ONLY: once a
            // shot attaches, GrappleSwing.IsActive goes true and
            // PlayerInputReader.Update's own swing-active early return
            // means this whole method never runs again until the rope
            // drops -- releasing is handled entirely inside
            // GrappleSwing.Tick instead. Checked ahead of the `_melee ==
            // null` guard below since firing needs no fists at all.
            if (_grappleSwing != null && _grappleSwing.EquippedHook != null)
            {
                // THE SAME AIM POSE A PISTOL USES -- EXPLICIT ASK: "use the
                // pistol animation for the grapple gun." Right-click raises
                // the arm via ApplyGunAimPose, the exact mechanism
                // EquippedGun's own branch above uses -- purely the visual;
                // firing itself is unaffected, a plain left-click launches
                // the rope whether or not right is held, the same way the
                // pistol fires regardless of whether `aiming` happens to be
                // true.
                bool aimingGrapple = Mouse.current.rightButton.isPressed;
                if (_orbitCamera != null)
                {
                    _orbitCamera.IsAiming = aimingGrapple;
                }
                _isAimingGun = aimingGrapple;

                if (Mouse.current.leftButton.wasPressedThisFrame && Camera.main != null)
                {
                    _grappleSwing.TryFire(Camera.main.transform.position, Camera.main.transform.forward);
                }
                return;
            }

            if (_melee == null)
            {
                return;
            }

            // A THROWABLE WEAPON TAKES OVER LEFT/RIGHT-CLICK ENTIRELY,
            // matching the bottle's own aim/melee/throw sequencing in
            // player.gd: hold right = aim, release right without ever
            // pressing left = cancel free, left while aiming = throw, left
            // while not aiming = melee swing. Nothing below this branch --
            // the ordinary block/punch-hold logic -- ever runs while a
            // throwable weapon is equipped.
            //
            // GATED ON weapon.CanThrow(), NOT JUST weapon != null -- a
            // sword can't be thrown (CanThrow() is false), so it has no
            // aim-to-throw sequence to run at all. Routing it through here
            // anyway was a real bug: right-click held it in a permanent
            // "aiming to throw" pose that could never resolve (nowhere for
            // Action.Throw to go), and left-click fired Action.Melee on the
            // very first frame of a press with no hold-for-heavy timing --
            // silently eating both the block button and the charged-attack
            // hold. A sword falls through to the ordinary dispatch below
            // instead, unchanged from bare fists: _melee.TryAttack()/
            // TryAttack(heavy: true) already branch on EquippedWeapon
            // internally (see MeleeAttacker's own doc comment), so nothing
            // here needs to know a sword is equipped at all -- it already
            // gets its own moveset/chain/heavy stage for free.
            IEquippableWeapon weapon = _inventory != null
                ? _inventory.EquippedInstance(_inventory.ActiveWeaponSlot)?.GetComponent<IEquippableWeapon>()
                : null;

            // THE SWORD IS FOUGHT THE SOULSLIKE WAY -- see SoulsSwordCombat.
            // It owns left-click, F, right-click and the roll outright, so
            // nothing below (the fist system's hold-for-heavy, the timed
            // block) runs while a sword is in hand.
            SoulsSwordCombat souls = SoulsSword;
            if (weapon != null && souls != null && souls.IsEquipped && weapon.MovesetId == ComboChain.Sword)
            {
                TickSoulsSword(souls);
                return;
            }

            if (weapon != null && weapon.CanThrow())
            {
                WeaponAimInput.Action action = _weaponAim.Tick(
                    Mouse.current.rightButton.wasPressedThisFrame,
                    Mouse.current.rightButton.wasReleasedThisFrame,
                    Mouse.current.leftButton.wasPressedThisFrame);
                if (_orbitCamera != null)
                {
                    _orbitCamera.IsAiming = _weaponAim.IsAiming;
                }

                if (action == WeaponAimInput.Action.Melee)
                {
                    _melee.TryAttack();
                }
                else if (action == WeaponAimInput.Action.Throw && weapon.CanThrow())
                {
                    Transform yawSource = YawSource != null
                        ? YawSource
                        : (Camera.main != null ? Camera.main.transform : null);
                    if (yawSource != null)
                    {
                        Vector3 direction = -yawSource.forward;
                        Vector3 origin = transform.position + Vector3.up * (_melee.OriginHeightFraction *
                            (_locomotion != null ? _locomotion.WorldHeight : 1.8f)) + direction * 0.5f;
                        weapon.Throw(gameObject, origin, direction);
                    }
                }
                return;
            }

            // Right-click, timed, NOT holdable. There is no start/stop pair the
            // way crouch has one: a press throws the block, and the window is
            // exactly as long as the clip. Holding the button past that does
            // nothing -- you cannot hold a block, you have to time it.
            if (Mouse.current.rightButton.wasPressedThisFrame && _vitals != null)
            {
                _vitals.StartBlock();
                _melee.PlayBlock();
            }

            // THE SWORD'S OWN SPIN ATTACK -- explicit ask: "press 2 to use
            // it. its like links up special in smash bros he just spins
            // around with the sword for a bit". TrySpinAttack itself
            // refuses without a sword equipped (MeleeAttacker.EquippedWeapon
            // .MovesetId == ComboChain.Sword), so pressing 2 bare-handed or
            // with any other weapon silently does nothing -- same "ask,
            // decide nothing here" split digit1Key's own fireball check
            // above already uses, just scoped to melee instead of an
            // innate ability available regardless of loadout.
            TickSpinKey(Keyboard.current, canStart: true);

            // A PUNCH GOES OUT ON THE PRESS, not on the release. It used to wait for the button to come back up (to tell a tap from a hold), which put
            // 100 ms or more between the click and the fist: the "horrible" lag. Now the click throws the punch at once; holding the button on
            // past HeavyPunchHoldSeconds and letting go follows it with the haymaker (buffered if the first punch is still going).
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _punchHeld = true;
                _punchHeldSeconds = 0.0f;
                _melee.TryAttack();
            }
            else if (_punchHeld && Mouse.current.leftButton.isPressed)
            {
                _punchHeldSeconds += deltaTime;
            }

            if (_punchHeld && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _punchHeld = false;
                if (_punchHeldSeconds >= HeavyPunchHoldSeconds)
                {
                    _melee.TryAttack(heavy: true);
                }
            }
        }

        /// <summary>
        /// 2, the sword's spin (Link's Up Special): pressed on the ground it starts CHARGING for as long as the key is held (up to a
        /// second, for up to 1.6x the damage) and spins when let go; pressed in the air it goes at once, climbing. MeleeAttacker
        /// decides everything else, including that there is a sword in hand.
        /// </summary>
        private void TickSpinKey(Keyboard keyboard, bool canStart)
        {
            if (keyboard == null || _melee == null)
            {
                return;
            }
            // From whichever slot of the bar holds it (2 by default).
            if (canStart && _skills.Pressed(SkillCatalog.SwordSpin, keyboard))
            {
                _melee.TrySpinAttack(charge: true);
            }
            if (_melee.IsChargingSpin && !_skills.Held(SkillCatalog.SwordSpin, keyboard))
            {
                _melee.ReleaseSpinAttack();
            }
        }

        private SoulsSwordCombat _soulsSword;

        /// <summary>
        /// The soulslike sword combat, or null when no sword has ever been
        /// equipped. Looked up lazily: SwordWeapon.OnEquip adds it at runtime.
        /// </summary>
        private SoulsSwordCombat SoulsSword
        {
            get
            {
                if (_soulsSword == null)
                {
                    _soulsSword = GetComponent<SoulsSwordCombat>();
                }
                return _soulsSword;
            }
        }

        /// <summary>
        /// Left-click = light attack (pressed, not released: a soulslike
        /// swing starts on the click), right-click HELD = guard. The heavy
        /// attack has no key for now.
        /// Every swing goes where the camera looks.
        /// </summary>
        private void TickSoulsSword(SoulsSwordCombat souls)
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            Transform source = YawSource != null
                ? YawSource
                : (Camera.main != null ? Camera.main.transform : null);
            float yaw = source != null ? source.eulerAngles.y : transform.eulerAngles.y;

            souls.Locked = _locomotion != null && _locomotion.IsDashing;

            // The bare-handed spin attack (2) is still MeleeAttacker's own;
            // while it plays the souls swing stands aside rather than stacking.
            bool spinning = _melee != null && _melee.IsSwinging;

            // 6: OCTASLASH (Sephiroth's held up special), only here, with the sword in hand: a 2.5 s wind-up, then an eight-cut dash where the
            // camera looks. A left click during the wind-up sends it at once -- and is not also a swing.
            if (_octaslash == null)
            {
                _octaslash = GetComponent<OctaslashAbility>();
            }
            bool octaslashCharging = _octaslash != null && _octaslash.IsCharging;
            if (_octaslash != null && source != null)
            {
                float chest = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.55f;
                Vector3 look = ConvergedDirection(transform.position + Vector3.up * chest, source);
                _octaslash.Aim(look);
                if (_skills.Pressed(SkillCatalog.Octaslash, keyboard) && !spinning && !souls.IsCommitted)
                {
                    _octaslash.TryStart(look);
                }
            }
            bool octaslashBusy = _octaslash != null && _octaslash.IsActive;
            spinning |= octaslashBusy;

            // THE HEAVY IS THE MIDDLE MOUSE BUTTON (the wheel's click): the overhead with a one-handed sword, the slam with a two-handed one.
            // F stays unbound -- explicit ask: "F shouldnt do anything yet."
            if (mouse.middleButton.wasPressedThisFrame && !octaslashCharging && !spinning)
            {
                souls.RequestHeavy(yaw);
            }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (octaslashCharging)
                {
                    _octaslash.Release();
                }
                else if (!spinning)
                {
                    souls.RequestLight(yaw);
                }
            }

            if (!octaslashBusy)
            {
                TickSpinKey(keyboard, canStart: !souls.IsCommitted);
            }

            // 7: THE SWORD'S WAVE (a Getsuga Tensho style crescent). The sword swings (a light attack) and a crescent of light leaves the
            // blade down the crosshair, exactly where the fireball would go. Only with the sword in hand, and not mid-swing.
            if (_skills.Pressed(SkillCatalog.SwordWave, keyboard) && _swordWave != null && source != null
                && !spinning && !souls.IsCommitted && _swordWave.CanCast)
            {
                float height = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f;
                Vector3 origin = transform.position + Vector3.up * height;
                if (_swordWave.TryCast(height, ConvergedDirection(origin, source), GetComponentsInChildren<Collider>()))
                {
                    souls.RequestLight(yaw);
                }
            }

            souls.SetBlockHeld(mouse.rightButton.isPressed && !spinning);
        }

        /// <summary>
        /// Space, held then released -- the same shape TickCombat's own
        /// punch-hold uses, and for the same reason: how high this should
        /// send the body is not known until release.
        ///
        /// UNLIKE THE PUNCH, THIS NEVER FIRES ON A BARE PRESS. There is no
        /// "light jump on press, heavy jump replaces it on a long-enough
        /// hold" branch here, because a tap and a hold are the SAME gesture
        /// at opposite ends of one continuous charge: a near-instant release
        /// reports a JumpChargeFraction near 0, which PlayerLocomotion's own
        /// height math already resolves to an ordinary jump. One mechanic,
        /// not two competing ones.
        ///
        /// A CHARGE ONLY STARTS GROUNDED, matching an ordinary jump's own
        /// gate, and CANCELS -- reporting a release with whatever fraction
        /// it had reached, so the animator still has a way out of the pose
        /// -- the moment the ground disappears out from under it, rather
        /// than letting a charge that started on solid ground pay out as a
        /// mid-air launch once it releases.
        ///
        /// KEPT IN THE VAULT, NOT CALLED FROM Update() RIGHT NOW -- EXPLICIT
        /// ASK: "get rid of charged jump for now that is going to be a
        /// skill tree ability keep it in the vault." Update's own dispatch
        /// fires a plain, un-charged jump directly instead (JumpReleased
        /// with JumpChargeFraction left at 0 -- see that call site's own
        /// doc comment for why that alone is enough, with no changes needed
        /// in PlayerLocomotion). This method is untouched and still fully
        /// correct; a future skill-tree unlock can route back through it
        /// exactly as before rather than rebuilding it.
        /// </summary>
        private void TickJumpCharge(Keyboard keyboard, bool grounded, float deltaTime,
            out bool chargingJumpNow, out bool jumpReleased, out float chargeFraction)
        {
            chargingJumpNow = false;
            jumpReleased = false;
            chargeFraction = 0.0f;

            // A charge that something else interrupted (a wall grab, a vault or a swing took the frame) is dropped, never paid out later.
            if (_jumpHeld && _lastJumpChargeFrame != Time.frameCount - 1)
            {
                _jumpHeld = false;
            }
            _lastJumpChargeFrame = Time.frameCount;

            if (keyboard.spaceKey.wasPressedThisFrame && grounded)
            {
                _jumpHeld = true;
                _jumpHeldSeconds = 0.0f;
            }

            if (!_jumpHeld)
            {
                return;
            }

            if (!grounded || keyboard.spaceKey.wasReleasedThisFrame)
            {
                _jumpHeld = false;
                jumpReleased = true;
                chargeFraction = ChargedJumpMaxHoldSeconds > 0.0f
                    ? Mathf.Clamp01(_jumpHeldSeconds / ChargedJumpMaxHoldSeconds)
                    : 1.0f;
                return;
            }

            if (keyboard.spaceKey.isPressed)
            {
                _jumpHeldSeconds = Mathf.Min(_jumpHeldSeconds + deltaTime, ChargedJumpMaxHoldSeconds);
                chargingJumpNow = true;
            }
        }

        /// <summary>
        /// The shared double-tap-shift GESTURE, split out from what it DOES
        /// (start a ground dash) so a grapple swing can react to the exact
        /// same gesture a different way -- explicit ask: dashing should cut
        /// a swing's own ragdoll short, the same "land or dash" exit a
        /// released/grounded swing already has. Only one of TickDash and the
        /// swing-active branch runs on a given frame (GrappleSwing.IsActive
        /// owns the whole frame while true, same as everywhere else it
        /// early-returns), so _sprintTapWindow's own decrement below still
        /// only happens once per frame either way.
        /// </summary>
        private bool ConsumeDoubleTapShift(Keyboard keyboard, float deltaTime)
        {
            _sprintTapWindow = Mathf.Max(0.0f, _sprintTapWindow - deltaTime);

            if (!keyboard.leftShiftKey.wasPressedThisFrame)
            {
                return false;
            }

            if (_sprintTapWindow <= 0.0f)
            {
                // The first tap only opens the window. Holding sprint down
                // never gets here twice, which is what keeps hold-to-sprint and
                // double-tap-to-dash from fighting over the same key.
                _sprintTapWindow = DoubleTapSeconds;
                return false;
            }
            _sprintTapWindow = 0.0f;
            return true;
        }

        /// <summary>
        /// Double-tap sprint to dash.
        ///
        /// WHICH WAY IT GOES IS WHATEVER WAS HELD AT THAT INSTANT, and a bare
        /// double-tap with nothing held dashes BACKWARD -- that is Godot's own
        /// choice, and it is the useful one: a dash with no direction is a
        /// dodge, and a dodge goes away from whatever you are facing.
        /// </summary>
        private void TickDash(Keyboard keyboard, Vector2 move, float yawDegrees, float deltaTime)
        {
            if (!ConsumeDoubleTapShift(keyboard, deltaTime))
            {
                return;
            }

            if (_locomotion.IsDashing)
            {
                return;
            }

            // WITH A SWORD IT IS A SOULSLIKE ROLL: its own stamina cost that
            // is allowed down to the last drop, longer i-frames, and legal out
            // of the tail of a swing (never its startup or active frames).
            SoulsSwordCombat souls = SoulsSword;
            bool soulsRoll = souls != null && souls.IsEquipped;
            if (soulsRoll && !souls.CanRoll)
            {
                return;
            }

            // THE DASH IS A SKILL (owner, 2026-10-14: the Freerunner tree's Dash). The sword's roll is not: it is the sword fighter's dodge,
            // part of sword combat, and stays with the sword whatever is learnt.
            if (!soulsRoll && !_skills.Knows(SkillCatalog.Dash))
            {
                return;
            }

            // Stamina is spent through the vitals rather than checked and
            // deducted here, so the exhaustion latch stays in one place.
            if (_vitals != null)
            {
                bool paid = soulsRoll
                    ? _vitals.TrySpendCombatStamina(souls.RollStaminaCost, souls.RollRegenDelaySeconds)
                    : _vitals.TrySpendStamina(PlayerVitals.BaseMaxStamina * PlayerLocomotion.DashStaminaFraction);
                if (!paid)
                {
                    return;
                }
            }
            if (soulsRoll)
            {
                souls.BeginRoll();
            }

            Vector2 held = move.sqrMagnitude > 0.01f ? move : new Vector2(0.0f, -1.0f);
            var direction = new LocomotionInput { Move = held, YawDegrees = yawDegrees };
            _locomotion.StartDash(direction.WorldDirection());
            _vitals?.GrantInvulnerability(soulsRoll ? souls.RollInvulnerabilitySeconds : PlayerLocomotion.DashSeconds);
            _audio?.PlayDash();
        }
    }
}

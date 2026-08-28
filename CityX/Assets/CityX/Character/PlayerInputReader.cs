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

        /// <summary>
        /// Set by Jetpack's own OnEquip/OnUnequip, same pattern as
        /// EquippedGun. While non-null, Space thrusts instead of charging a
        /// jump -- see the Update() dispatch and IJetpack's own doc comment.
        /// </summary>
        public IJetpack EquippedJetpack;

        /// <summary>
        /// Found once via GetComponent in Awake, not set externally like
        /// EquippedGun/EquippedJetpack -- see IFireballCaster's own doc
        /// comment for why: the spell is an innate ability with no equip
        /// event to hang the wiring off, so this is discovered the same
        /// way PlayerVitals/MeleeAttacker already are.
        /// </summary>
        private IFireballCaster _fireballCaster;

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

        /// <summary>
        /// Set inside TickCombat's own gun branch, read back here in Update()
        /// once the LocomotionInput for this same frame is built -- TickCombat
        /// runs first and returns before movement is ever touched, so there is
        /// no other way for "is the gun's right-click held" to reach the
        /// animator without threading a whole extra parameter through.
        /// </summary>
        private bool _isAimingGun;

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

        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private MeleeAttacker _melee;
        private Ragdoll _ragdoll;
        private PlayerAudio _audio;
        private GrabCarry _grabCarry;
        private Inventory _inventory;
        private GrappleSwing _grappleSwing;
        private PlayerClimbing _climbing;
        private ThirdPersonCamera _orbitCamera;
        private readonly WeaponAimInput _weaponAim = new WeaponAimInput();

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
            _ragdoll = GetComponent<Ragdoll>();
            _audio = GetComponent<PlayerAudio>();
            _grabCarry = GetComponent<GrabCarry>();
            _inventory = GetComponent<Inventory>();
            _grappleSwing = GetComponent<GrappleSwing>();
            _climbing = GetComponent<PlayerClimbing>();
            _fireballCaster = GetComponent<IFireballCaster>();
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
            if (_seatedCar != null)
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
            }
            if (_melee != null)
            {
                _melee.Tick(deltaTime);
            }
            _fireballCaster?.Tick(deltaTime);

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

            if (_ragdoll != null && _ragdoll.IsLimp)
            {
                // Physics owns the body now. Ticking locomotion here would call
                // Move on a CharacterController the ragdoll has just disabled,
                // which does nothing useful and complains about it every frame.
                // The footsteps have to be stopped by hand on every one of
                // these early returns: they are loops, so they do not run out
                // on their own, and a corpse walking on the spot is audible.
                _audio?.SilenceFootsteps();
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

                _grappleSwing.Tick(swingMove, swingYaw, boostedRelease, plainRelease,
                    reelHeld, payoutHeld, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // ALREADY ATTACHED TO A WALL OWNS THE WHOLE FRAME TOO, matching
            // player.gd's own _is_climbing early-return into
            // _process_climbing -- a real reported bug there ("the sprint
            // noise is playing while climbing") is why footsteps are
            // silenced here rather than only at grab time: nothing
            // downstream ever runs again to turn them off otherwise.
            if (_climbing != null && _climbing.IsClimbing)
            {
                _climbing.Tick(keyboard.spaceKey.wasPressedThisFrame,
                    keyboard.wKey.isPressed, keyboard.sKey.isPressed, _vitals, deltaTime);
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
            if (_climbing != null &&
                (_grabCarry == null || !_grabCarry.IsCarrying) &&
                !_locomotion.IsDashing)
            {
                float stamina = _vitals != null ? _vitals.Stamina : float.MaxValue;
                if (_climbing.TryStart(keyboard.leftShiftKey.isPressed, keyboard.wKey.isPressed, stamina))
                {
                    _climbing.Tick(keyboard.spaceKey.wasPressedThisFrame,
                        keyboard.wKey.isPressed, keyboard.sKey.isPressed, _vitals, deltaTime);
                    _audio?.SilenceFootsteps();
                    return;
                }
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
            if (_fireballCaster != null && keyboard.digit1Key.wasPressedThisFrame && yawSource != null)
            {
                // Roughly chest height, matching the same fraction
                // MeleeAttacker.SwingOrigin/the weapon-throw origin below
                // already use for "a sensible point on the body to launch
                // something from."
                float height = (_locomotion != null ? _locomotion.StandingHeight : 1.8f) * 0.68f;
                Vector3 origin = transform.position + Vector3.up * height;
                _fireballCaster.TryCast(origin, yawSource.forward, GetComponentsInChildren<Collider>());
            }

            // A JETPACK TAKES SHIFT OVER TOO WHILE ACTUALLY THRUSTING, not
            // just Space -- the explicit ask ("shift is a boost when midair
            // using jetpack") redefines what the key means the instant
            // thrust is live, the same way Space already stopped meaning
            // "charged jump" the moment EquippedJetpack was assigned.
            // Computed before wantsSprint below so that check can exclude
            // this case rather than double-booking one key with two
            // unrelated meanings and draining stamina for a mechanic that
            // is not supposed to cost any.
            bool jetpackThrusting = EquippedJetpack != null && keyboard.spaceKey.isPressed;

            // Sprint only forwards. Sprinting sideways or backwards has no
            // animation and reads as a glitch rather than as speed.
            bool wantsSprint = !jetpackThrusting && keyboard.leftShiftKey.isPressed && move.y > 0.0f;

            // Stamina decides, not the key. Asking rather than checking a flag
            // keeps the exhaustion latch in one place.
            bool sprinting = _vitals != null
                ? _vitals.TickStamina(wantsSprint, deltaTime)
                : wantsSprint;

            // Swinging roots the body, which is what makes a combo a
            // commitment rather than something you can walk out of.
            if (_melee != null && _melee.IsSwinging)
            {
                move = Vector2.zero;
                sprinting = false;
            }

            float yawDegrees = yawSource != null ? yawSource.eulerAngles.y : 0.0f;
            TickDash(keyboard, move, yawDegrees, deltaTime);

            bool chargingJumpNow = false;
            bool jumpReleased = false;
            float jumpChargeFraction = 0.0f;
            // A JETPACK TAKES SPACE OVER ENTIRELY WHILE EQUIPPED, the same
            // way a gun/grapple hook already take over click -- Space means
            // thrust now, not a charged jump, so TickJumpCharge (and the
            // pose it would have driven) never runs at all. Left/right/
            // crouch above are untouched: WASD keeps steering horizontally
            // exactly the way ordinary falling already lets it, matching
            // the explicit ask ("mixed with wasd to move around").
            if (EquippedJetpack != null)
            {
                EquippedJetpack.Tick(jetpackThrusting, move, keyboard.leftShiftKey.isPressed, deltaTime);
            }
            else
            {
                TickJumpCharge(keyboard, _locomotion.IsGrounded, deltaTime,
                    out chargingJumpNow, out jumpReleased, out jumpChargeFraction);
            }

            // A CAST NO LONGER BORROWS THE GUN'S OWN AIM POSE -- the explicit
            // ask was a dedicated animation (a raised right hand), so
            // isCastingFireball now only feeds the jetpack-suppression check
            // below, the same way gun-aiming does; the actual visual is a
            // separate procedural pose (see PlayerLocomotion.SetCastPose)
            // rather than a second state sharing LocomotionInput.Aiming.
            bool isCastingFireball = _fireballCaster != null && _fireballCaster.IsCasting;
            bool isAiming = _isAimingGun || isCastingFireball;
            _locomotion.SetCastPose(_fireballCaster != null ? _fireballCaster.CastFraction : 0.0f);

            var input = new LocomotionInput
            {
                Move = move,
                YawDegrees = yawDegrees,
                Sprint = sprinting,
                Crouch = keyboard.leftCtrlKey.isPressed,
                // Space no longer fires Jump directly -- see TickJumpCharge's
                // own comment for why a tap and a hold are one mechanic now,
                // not a plain press here plus a competing charge below it.
                ChargingJump = chargingJumpNow,
                JumpReleased = jumpReleased,
                JumpChargeFraction = jumpChargeFraction,
                // Gun-aiming only now -- see isCastingFireball's own comment
                // just above for why a cast no longer sets this too.
                Aiming = _isAimingGun,
                SwordEquipped = _melee != null && _melee.EquippedWeapon != null &&
                                _melee.EquippedWeapon.MovesetId == ComboChain.Sword,
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
                Dancing = _isDancing,
                Breakdancing = _isBreakdancing,
            };

            _locomotion.Tick(input, deltaTime);

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

                if (Mouse.current.leftButton.wasPressedThisFrame && gunYawSource != null)
                {
                    EquippedGun.Fire(gunYawSource.position, gunYawSource.forward, aiming, gameObject);
                }
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    EquippedGun.TryReload();
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

            // A WEAPON EQUIPPED IN THE RIGHT HAND TAKES OVER LEFT/RIGHT-CLICK
            // ENTIRELY, matching the bottle's own aim/melee/throw sequencing
            // in player.gd: hold right = aim, release right without ever
            // pressing left = cancel free, left while aiming = throw, left
            // while not aiming = melee swing. Nothing below this branch --
            // the ordinary block/punch-hold logic -- ever runs while a
            // weapon is equipped.
            IEquippableWeapon weapon = _inventory != null
                ? _inventory.EquippedInstance("hand_r")?.GetComponent<IEquippableWeapon>()
                : null;
            if (weapon != null)
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
                            (_locomotion != null ? _locomotion.StandingHeight : 1.8f)) + direction * 0.5f;
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

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _punchHeld = true;
                _punchHeldSeconds = 0.0f;
            }
            else if (_punchHeld && Mouse.current.leftButton.isPressed)
            {
                _punchHeldSeconds += deltaTime;
            }

            if (_punchHeld && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _punchHeld = false;
                _melee.TryAttack(heavy: _punchHeldSeconds >= HeavyPunchHoldSeconds);
            }
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
        /// </summary>
        private void TickJumpCharge(Keyboard keyboard, bool grounded, float deltaTime,
            out bool chargingJumpNow, out bool jumpReleased, out float chargeFraction)
        {
            chargingJumpNow = false;
            jumpReleased = false;
            chargeFraction = 0.0f;

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
        /// Double-tap sprint to dash.
        ///
        /// WHICH WAY IT GOES IS WHATEVER WAS HELD AT THAT INSTANT, and a bare
        /// double-tap with nothing held dashes BACKWARD -- that is Godot's own
        /// choice, and it is the useful one: a dash with no direction is a
        /// dodge, and a dodge goes away from whatever you are facing.
        /// </summary>
        private void TickDash(Keyboard keyboard, Vector2 move, float yawDegrees, float deltaTime)
        {
            _sprintTapWindow = Mathf.Max(0.0f, _sprintTapWindow - deltaTime);

            if (!keyboard.leftShiftKey.wasPressedThisFrame)
            {
                return;
            }

            if (_sprintTapWindow <= 0.0f)
            {
                // The first tap only opens the window. Holding sprint down
                // never gets here twice, which is what keeps hold-to-sprint and
                // double-tap-to-dash from fighting over the same key.
                _sprintTapWindow = DoubleTapSeconds;
                return;
            }
            _sprintTapWindow = 0.0f;

            if (_locomotion.IsDashing)
            {
                return;
            }

            // Stamina is spent through the vitals rather than checked and
            // deducted here, so the exhaustion latch stays in one place.
            float cost = PlayerVitals.BaseMaxStamina * PlayerLocomotion.DashStaminaFraction;
            if (_vitals != null && !_vitals.TrySpendStamina(cost))
            {
                return;
            }

            Vector2 held = move.sqrMagnitude > 0.01f ? move : new Vector2(0.0f, -1.0f);
            var direction = new LocomotionInput { Move = held, YawDegrees = yawDegrees };
            _locomotion.StartDash(direction.WorldDirection());
            _vitals?.GrantInvulnerability(PlayerLocomotion.DashSeconds);
            _audio?.PlayDash();
        }
    }
}

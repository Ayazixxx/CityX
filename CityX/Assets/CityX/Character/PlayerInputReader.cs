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

        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private MeleeAttacker _melee;
        private Ragdoll _ragdoll;
        private PlayerAudio _audio;
        private GrabCarry _grabCarry;
        private Inventory _inventory;
        private readonly WeaponAimInput _weaponAim = new WeaponAimInput();

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
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
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

            TickCombat(deltaTime);

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1.0f;
            if (keyboard.sKey.isPressed) move.y -= 1.0f;
            if (keyboard.dKey.isPressed) move.x += 1.0f;
            if (keyboard.aKey.isPressed) move.x -= 1.0f;

            Transform yawSource = YawSource != null
                ? YawSource
                : (Camera.main != null ? Camera.main.transform : null);

            // Sprint only forwards. Sprinting sideways or backwards has no
            // animation and reads as a glitch rather than as speed.
            bool wantsSprint = keyboard.leftShiftKey.isPressed && move.y > 0.0f;

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

            var input = new LocomotionInput
            {
                Move = move,
                YawDegrees = yawDegrees,
                Sprint = sprinting,
                Crouch = keyboard.leftCtrlKey.isPressed,
                Jump = keyboard.spaceKey.wasPressedThisFrame,
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
            if (_melee == null || Mouse.current == null)
            {
                return;
            }

            // Both hands are full while carrying something -- matches
            // player.gd's own "entirely while carrying something" guard on
            // punch/block input.
            if (_grabCarry != null && _grabCarry.IsCarrying)
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

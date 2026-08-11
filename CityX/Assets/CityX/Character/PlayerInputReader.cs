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

        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private MeleeAttacker _melee;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            // Both optional. A body with no vitals and no fists still walks --
            // useful for tests and for anything that is not the player.
            _vitals = GetComponent<PlayerVitals>();
            _melee = GetComponent<MeleeAttacker>();
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

            if (_vitals != null && _vitals.IsDead)
            {
                // A corpse still falls, so keep ticking the body with no input
                // rather than stopping it dead in the air.
                _locomotion.Tick(default, deltaTime);
                return;
            }

            // Being hit takes the controls away for a moment. Gravity still
            // applies, so a stunned body in mid-air keeps falling instead of
            // hanging there.
            if (_vitals != null && _vitals.IsStunned)
            {
                _locomotion.Tick(default, deltaTime);
                return;
            }

            if (_melee != null && Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                _melee.TryAttack();
            }

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

            var input = new LocomotionInput
            {
                Move = move,
                YawDegrees = yawSource != null ? yawSource.eulerAngles.y : 0.0f,
                Sprint = sprinting,
                Jump = keyboard.spaceKey.wasPressedThisFrame,
            };

            _locomotion.Tick(input, deltaTime);

            // AFTER the move, so grounded and height are this frame's, not the
            // previous one's. Checking before would measure the fall one frame
            // short and let a lethal drop land as a survivable one.
            if (_vitals != null)
            {
                _vitals.TickFallDamage(_locomotion.IsGrounded, transform.position.y);
            }
        }
    }
}

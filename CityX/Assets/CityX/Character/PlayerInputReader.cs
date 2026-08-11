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

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1.0f;
            if (keyboard.sKey.isPressed) move.y -= 1.0f;
            if (keyboard.dKey.isPressed) move.x += 1.0f;
            if (keyboard.aKey.isPressed) move.x -= 1.0f;

            Transform yawSource = YawSource != null
                ? YawSource
                : (Camera.main != null ? Camera.main.transform : null);

            var input = new LocomotionInput
            {
                Move = move,
                YawDegrees = yawSource != null ? yawSource.eulerAngles.y : 0.0f,
                // Sprint only forwards. Sprinting sideways or backwards has no
                // animation and reads as a glitch rather than as speed.
                Sprint = keyboard.leftShiftKey.isPressed && move.y > 0.0f,
                Jump = keyboard.spaceKey.wasPressedThisFrame,
            };

            _locomotion.Tick(input, Time.deltaTime);
        }
    }
}

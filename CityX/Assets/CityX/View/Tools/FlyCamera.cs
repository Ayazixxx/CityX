using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.View
{
    /// <summary>
    /// A free camera for looking at the city.
    ///
    /// Uses the Input System package directly rather than an action asset --
    /// this project has activeInputHandler set to the new system only, so the
    /// old UnityEngine.Input calls would compile and then silently do nothing
    /// at runtime, which is a miserable thing to debug.
    ///
    /// WASD to move, Q/E down/up, hold right mouse to look, shift to sprint,
    /// mouse wheel to change speed.
    /// </summary>
    public sealed class FlyCamera : MonoBehaviour
    {
        public float MetresPerSecond = 24.0f;
        public float SprintMultiplier = 5.0f;
        public float LookDegreesPerPixel = 0.12f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null)
            {
                return;
            }

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * LookDegreesPerPixel;
                _pitch -= delta.y * LookDegreesPerPixel;
                // Stop just short of vertical. Passing straight up flips the
                // horizon and the camera rolls, which reads as a broken control
                // rather than a clamped one.
                _pitch = Mathf.Clamp(_pitch, -89.0f, 89.0f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0.0f);
            }

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0.0f))
                {
                    MetresPerSecond = Mathf.Clamp(MetresPerSecond * (scroll > 0 ? 1.15f : 1.0f / 1.15f),
                        1.0f, 500.0f);
                }
            }

            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += transform.forward;
            if (keyboard.sKey.isPressed) move -= transform.forward;
            if (keyboard.dKey.isPressed) move += transform.right;
            if (keyboard.aKey.isPressed) move -= transform.right;
            if (keyboard.eKey.isPressed) move += Vector3.up;
            if (keyboard.qKey.isPressed) move -= Vector3.up;

            if (move.sqrMagnitude > 0.0f)
            {
                float speed = MetresPerSecond;
                if (keyboard.leftShiftKey.isPressed)
                {
                    speed *= SprintMultiplier;
                }
                transform.position += move.normalized * (speed * Time.deltaTime);
            }
        }
    }
}

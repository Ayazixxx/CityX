using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// THE ROPE'S CONTROLS WHILE THE BODY HANGS LIMP FROM IT (GrappleSwing.Rope.cs, docs/GRAPPLE_RAGDOLL.md): the same keys as the upright swing's
    /// -- WASD pushes, Shift reels, Ctrl pays out, Space lets go with a boost, left-click lets go -- read from the limp branch of Update, which
    /// otherwise reads only Space (to stand up, which a hanging body cannot).
    /// </summary>
    public sealed partial class PlayerInputReader
    {
        private void TickRopedSwing(GameControls controls, float deltaTime)
        {
            Vector2 move = Vector2.zero;
            move += controls.Move;
            Transform yawSource = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
            float yaw = yawSource != null ? yawSource.eulerAngles.y : 0.0f;
            bool boostedRelease = controls.Jump.Pressed;
            bool plainRelease = controls.Attack.Pressed;
            _grappleSwing.Tick(move, yaw, boostedRelease, plainRelease, controls.Sprint.Held, controls.Crouch.Held, deltaTime);
        }
    }
}

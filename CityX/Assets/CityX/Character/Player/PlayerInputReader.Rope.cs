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
        private void TickRopedSwing(Keyboard keyboard, float deltaTime)
        {
            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1.0f;
            if (keyboard.sKey.isPressed) move.y -= 1.0f;
            if (keyboard.dKey.isPressed) move.x += 1.0f;
            if (keyboard.aKey.isPressed) move.x -= 1.0f;
            Transform yawSource = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
            float yaw = yawSource != null ? yawSource.eulerAngles.y : 0.0f;
            bool boostedRelease = keyboard.spaceKey.wasPressedThisFrame;
            bool plainRelease = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            _grappleSwing.Tick(move, yaw, boostedRelease, plainRelease, keyboard.leftShiftKey.isPressed, keyboard.leftCtrlKey.isPressed, deltaTime);
        }
    }
}

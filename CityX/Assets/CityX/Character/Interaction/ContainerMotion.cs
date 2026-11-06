using UnityEngine;

namespace CityX.Character
{
    /// <summary>How a container's moving part opens: a drawer slides out, a door swings on its hinge, a lid lifts, an oven door drops.</summary>
    public enum ContainerKind
    {
        Drawer,

        /// <summary>A door hinged on its left edge (seen from the front).</summary>
        DoorLeft,

        /// <summary>A door hinged on its right edge.</summary>
        DoorRight,

        /// <summary>A lid hinged along its back edge.</summary>
        Lid,

        /// <summary>A door hinged along its bottom edge (an oven's).</summary>
        Flap,

        /// <summary>A fallen body searched for what it wears (CorpseLoot): nothing on it opens or moves.</summary>
        Body,
    }

    /// <summary>
    /// A container part's open pose against its closed one, in its own frame (+z its front, the way it opens toward; y up), with the frame's
    /// origin on the hinge: drawers slide along +z, doors turn their far edge out toward +z, lids lift their front edge, a flap drops its top
    /// edge toward +z. Nothing ever moves back toward -z, into the wall behind it.
    /// </summary>
    public static class ContainerMotion
    {
        public const float Seconds = 0.35f;
        public const float DoorDegrees = 100.0f;
        /// <summary>Short of upright: past it a chest's lid would swing back into the wall behind it.</summary>
        public const float LidDegrees = 88.0f;
        public const float FlapDegrees = 85.0f;

        /// <summary>Eased: quick to start, settling at the end.</summary>
        public static float Ease(float t) => 1.0f - (1.0f - Mathf.Clamp01(t)) * (1.0f - Mathf.Clamp01(t));

        public static void PoseAt(ContainerKind kind, float open01, float slide, out Vector3 offset, out Quaternion turn)
        {
            float t = Ease(open01);
            offset = Vector3.zero;
            turn = Quaternion.identity;
            switch (kind)
            {
                case ContainerKind.Drawer:
                    offset = Vector3.forward * (slide * t);
                    break;
                case ContainerKind.DoorLeft:
                    turn = Quaternion.Euler(0.0f, -DoorDegrees * t, 0.0f);   // its +x reach turns out toward +z
                    break;
                case ContainerKind.DoorRight:
                    turn = Quaternion.Euler(0.0f, DoorDegrees * t, 0.0f);    // its -x reach turns out toward +z
                    break;
                case ContainerKind.Lid:
                    turn = Quaternion.Euler(-LidDegrees * t, 0.0f, 0.0f);    // its +z reach lifts toward +y
                    break;
                case ContainerKind.Flap:
                    turn = Quaternion.Euler(FlapDegrees * t, 0.0f, 0.0f);    // its +y reach drops toward +z
                    break;
            }
        }
    }
}

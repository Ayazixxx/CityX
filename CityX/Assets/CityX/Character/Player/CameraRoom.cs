using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE CAMERA INDOORS (owner, 2026-11-16: "is there any way we can improve the indoor cam, how do third person games do it"). Two rules from how
    /// third-person games handle tight spaces (Unreal's spring arm, Cinemachine's deoccluder, Sifu's and God of War's close cameras):
    ///  * IN A ROOM THE SHOT TIGHTENS: under a roof with walls on most sides, the camera comes in closer, lower and a little wider, and eases back
    ///    out under the open sky. A street between buildings has walls but no roof, the space under a footbridge a roof but open sides: neither.
    ///  * THE ARM SNAPS IN, EASES OUT: a wall between the body and the camera pulls it in at once (it must never show the inside of a wall), but
    ///    when the wall is gone it drifts back instead of jumping, so passing a door frame or a pillar does not pop the picture in and out.
    /// Pure maths; ThirdPersonCamera probes the room and applies it.
    /// </summary>
    public static class CameraRoom
    {
        /// <summary>How many directions round the body are probed for walls.</summary>
        public const int WallProbes = 8;

        /// <summary>With a roof overhead, this many of the probes meeting a wall or fewer is still open air (a porch, under a footbridge)...</summary>
        public const int OpenWalls = 1;

        /// <summary>...and this many or more is a room: a doorway and big windows leave a probe or two out of it (Old Guy's den meets four).</summary>
        public const int RoomWalls = 4;

        /// <summary>How enclosed the body is, 0 (outside) to 1 (in a room): only under a roof, and by how many directions meet a wall near by.</summary>
        public static float Enclosure(bool roofed, int wallsNear)
        {
            if (!roofed)
            {
                return 0.0f;
            }
            return Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(OpenWalls, RoomWalls, wallsNear));
        }

        /// <summary>
        /// The arm's length this frame: at once as short as the walls allow (<paramref name="allowed"/>) when that is shorter than it is now; back out
        /// toward <paramref name="wanted"/> at <paramref name="outMetresPerSecond"/> after a wall held it in (it was shorter than
        /// <paramref name="wantedBefore"/>, last frame's wanted length); and with the wanted length itself when nothing held it in (an aim let go, a body
        /// growing: those ease on their own clocks).
        /// </summary>
        public static float Arm(float current, float allowed, float wanted, float wantedBefore, float outMetresPerSecond, float deltaTime)
        {
            float target = Mathf.Min(allowed, wanted);
            if (target <= current || current >= wantedBefore - FreeArmTolerance)
            {
                return target;
            }
            return Mathf.MoveTowards(current, target, Mathf.Max(0.0f, outMetresPerSecond) * Mathf.Max(0.0f, deltaTime));
        }

        /// <summary>An arm within this of its wanted length was not held in by anything, metres.</summary>
        public const float FreeArmTolerance = 0.01f;
    }
}

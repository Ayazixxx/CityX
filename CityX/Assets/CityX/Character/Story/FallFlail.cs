using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How the intro's falling body flies (owner, 2026-10-22: "make it so you are laying flat horizontal during the fall cutscene at the
    /// beginning instead of vertical. and make some amount of flailing in the air like there is wind, so he isnt just still during the whole
    /// fall"). It lies flat on its BACK -- the get-up clip starts lying on its back, and its face is what the camera overhead sees -- and the
    /// rushing air throws its arms and legs about. Pure: the turn that brings the body onto its back and holds it there, and the wind on each
    /// limb at a moment of the fall. IntroCutscene applies them to the ragdoll while it falls.
    ///
    /// The body is let go standing (the HUD portrait is taken off an upright head) and tips over onto its back in its first second: the turn is
    /// a damped spring, so nothing snaps.
    /// </summary>
    public static class FallFlail
    {
        /// <summary>How hard the body is turned onto its back: angular acceleration per radian it is off (rad/s² per rad).</summary>
        public const float RightingStrength = 22.0f;

        /// <summary>How much of its spin the turn takes off meanwhile (per second), so it settles rather than rocks.</summary>
        public const float RightingDamping = 6.0f;

        /// <summary>The rushing air's push up on an arm or a leg at full speed (m/s²): limbs ride up on it more than the body does.</summary>
        public const float WindLift = 12.0f;

        /// <summary>How hard the gusts throw a limb about at full speed (m/s²), and how often they change (per second).</summary>
        public const float GustAcceleration = 30.0f;
        public const float GustFrequency = 1.3f;

        /// <summary>The fall speed the wind is at full strength from (the fall's top speed is about 35 m/s, IntroCutscene.FallDrag).</summary>
        public const float FullWindSpeed = 25.0f;

        /// <summary>The share of a limb's wind its upper half (the upper arm, the thigh) gets: the forearms and shins, freer, flap most.</summary>
        public const float UpperLimbShare = 0.5f;

        /// <summary>The limbs the wind throws about, in the order <see cref="Wind"/> numbers them.</summary>
        public const int LeftArm = 0, RightArm = 1, LeftLeg = 2, RightLeg = 3, LimbCount = 4;

        /// <summary>The way a body's chest faces, from its spine (hips to neck) and its shoulders (left to right).</summary>
        public static Vector3 Front(Vector3 hips, Vector3 neck, Vector3 leftShoulder, Vector3 rightShoulder)
        {
            return Vector3.Cross(rightShoulder - leftShoulder, neck - hips).normalized;
        }

        /// <summary>
        /// The angular acceleration that turns a body whose chest faces <paramref name="front"/> onto its back (chest to the sky), less some
        /// of its spin. Face down, it rolls over along its <paramref name="spine"/>.
        /// </summary>
        public static Vector3 Righting(Vector3 front, Vector3 spine, Vector3 angularVelocity)
        {
            float angle = Vector3.Angle(front, Vector3.up) * Mathf.Deg2Rad;
            Vector3 axis = Vector3.Cross(front, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f)
            {
                axis = angle > 1.0f ? spine : Vector3.zero;
            }
            return axis.normalized * (angle * RightingStrength) - angularVelocity * RightingDamping;
        }

        /// <summary>
        /// The wind on <paramref name="limb"/> (<see cref="LeftArm"/> ...) <paramref name="seconds"/> into the fall, for a body falling at
        /// <paramref name="fallSpeed"/> m/s: a lift, and a gust that wanders every way, each limb its own.
        /// </summary>
        public static Vector3 Wind(int limb, float seconds, float fallSpeed)
        {
            float strength = Mathf.Clamp01(fallSpeed / FullWindSpeed);
            float t = seconds * GustFrequency;
            float seed = 17.3f * (limb + 1);
            var gust = new Vector3(Noise(t, seed), Noise(t, seed + 5.1f), Noise(t, seed + 9.7f));
            return (Vector3.up * WindLift + gust * GustAcceleration) * strength;
        }

        /// <summary>Smooth noise in -1..1.</summary>
        private static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2.0f - 1.0f;
    }
}

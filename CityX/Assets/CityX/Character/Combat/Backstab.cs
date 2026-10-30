using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BACKSTAB (docs/KNIFE.md; owner, 2026-10-29: "backstabs would be a cool critical hit thing"): a one-handed blade's blow on a living
    /// body from behind it is a critical hit -- far more damage, a stagger whatever the blow's own, a longer hit-stop and a gold number. Every
    /// one-handed blade has it a little, as any weapon backstabs in Dark Souls; the knife's is the biggest by far (Elden Ring's daggers crit at
    /// 130 against a straight sword's 100-110, Dark Souls 3's dagger backstab is twice a long sword's, Skyrim's daggers sneak attack for up to
    /// 15 times). It takes getting round someone: face to face it never happens.
    /// </summary>
    public static class Backstab
    {
        /// <summary>A knife's backstab: this many times the blow.</summary>
        public const float KnifeMultiplier = 3.0f;

        /// <summary>Any other one-handed blade's (the combat sword, the crowbar, the bat).</summary>
        public const float BladeMultiplier = 1.5f;

        /// <summary>How far round from straight behind the body the blow may come from, degrees: its back third.</summary>
        public const float BehindDegrees = 60.0f;

        /// <summary>A backstab staggers the body this long at least, seconds (a knife's lights have no stagger of their own).</summary>
        public const float StaggerSeconds = 0.6f;

        /// <summary>A backstab's hit-stop is this many times the blow's own: the moment it lands is held, to be felt.</summary>
        public const float HitStopMultiplier = 2.0f;

        public static float MultiplierFor(bool knife) => knife ? KnifeMultiplier : BladeMultiplier;

        /// <summary>
        /// True when <paramref name="attacker"/> stands behind a body at <paramref name="target"/> facing <paramref name="targetForward"/>:
        /// within <see cref="BehindDegrees"/> of straight behind it, on the ground's plane.
        /// </summary>
        public static bool IsBehind(Vector3 target, Vector3 targetForward, Vector3 attacker)
        {
            Vector3 toAttacker = attacker - target;
            toAttacker.y = 0.0f;
            targetForward.y = 0.0f;
            if (toAttacker.sqrMagnitude < 1e-4f || targetForward.sqrMagnitude < 1e-6f)
            {
                return false;
            }
            return Vector3.Angle(-targetForward, toAttacker) <= BehindDegrees;
        }
    }
}

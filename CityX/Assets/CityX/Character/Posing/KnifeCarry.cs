using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A knife carried at the side, its blade turned out away from the body (owner, 2026-11-21: "the knife clips through the coat a bit during
    /// the idle animation ... just turn his hand so the knife is going the other way"). The one-handed carry clips hang the arm for a sword
    /// pointed forward and down; the knife's own hold (set by the owner in the editor) sits differently in the fist, and in that arm its blade
    /// pointed down and in across the thigh, through a long coat. The forearm is rolled about its own length, as a real one turns the hand, until
    /// the blade points out to the side as near as that roll can bring it. Pure: PlayerLocomotion applies it.
    /// </summary>
    public static class KnifeCarry
    {
        /// <summary>Where the carried blade points, in the body's frame (right, up, forward): out to the side, a little ahead and up.</summary>
        public static readonly Vector3 Point = new Vector3(0.7f, 0.25f, 0.65f).normalized;

        /// <summary>The most the forearm is rolled for it, degrees either way: about as far as a forearm turns.</summary>
        public const float MostRollDegrees = 110.0f;

        /// <summary>
        /// How far to roll about <paramref name="forearm"/> (elbow to hand) to bring <paramref name="blade"/> round toward
        /// <paramref name="wanted"/>, degrees (signed about the forearm, clamped to <paramref name="most"/>).
        /// </summary>
        public static float RollDegrees(Vector3 forearm, Vector3 blade, Vector3 wanted, float most)
        {
            Vector3 from = Vector3.ProjectOnPlane(blade, forearm);
            Vector3 to = Vector3.ProjectOnPlane(wanted, forearm);
            if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f)
            {
                return 0.0f;
            }
            return Mathf.Clamp(Vector3.SignedAngle(from, to, forearm), -most, most);
        }
    }
}

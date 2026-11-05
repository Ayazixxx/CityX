using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What a melee blow does not go through. A fist and a blade find who is in reach by a volume, which knows nothing of what stands in it:
    /// a body on the other side of a wall, a closed door or a parked car is in reach of it all the same.
    /// </summary>
    public static class BlowPath
    {
        /// <summary>The last of the way to the target is not looked at, metres: its own surface is where the way ends.</summary>
        public const float EndSkinMetres = 0.05f;

        /// <summary>
        /// Whether something solid stands between <paramref name="from"/> (the chest of the body striking) and <paramref name="to"/> (the
        /// point on the body struck). Bodies do not count, nor do loose things (BloodSplats.Surface's rule), nor anything of
        /// <paramref name="attacker"/>'s own.
        /// </summary>
        public static bool IsWalled(Vector3 from, Vector3 to, Transform attacker)
        {
            Vector3 way = to - from;
            float distance = way.magnitude;
            return distance > EndSkinMetres && BloodSplats.Surface(from, way / distance, distance - EndSkinMetres, attacker, out _);
        }
    }
}

using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// WHERE A TURNED-ASIDE BULLET GOES (owner, 2026-10-27: "like in lego star wars when you hold block as a jedi it reflects all bullets ...
    /// into another direction ... it would feel really bullshit if your teammate ... reflected a bullet and it ricocheted and hit you ... maybe
    /// make it so it can fly in a random direction but not if its going to hit an ally"; docs/BULLET_DEFLECTION.md). Off at random, back the
    /// way it came within <see cref="ConeDegrees"/> either side and between <see cref="LowestPitch"/> and <see cref="HighestPitch"/> of level;
    /// each way tried is traced first, and one that would hit someone it must not is passed over for another, up to <see cref="Tries"/>. With
    /// no safe way the bullet goes nowhere: it only glances off.
    /// </summary>
    public static class Ricochet
    {
        /// <summary>How far either side of straight back the bullet may fly, degrees.</summary>
        public const float ConeDegrees = 70.0f;

        /// <summary>How far below level it may go (into the street nearby), and how far above, degrees.</summary>
        public const float LowestPitch = -10.0f;
        public const float HighestPitch = 35.0f;

        /// <summary>How many ways are tried before the bullet is given up on.</summary>
        public const int Tries = 10;

        /// <summary>How far a turned-aside bullet flies, metres.</summary>
        public const float Range = 50.0f;

        /// <summary>Traces a way: where the bullet would end and whether what it ends on may be hit.</summary>
        public delegate bool Probe(Vector3 origin, Vector3 direction, out Vector3 end);

        /// <summary>
        /// A way for a bullet that came in along <paramref name="incoming"/> to fly off from <paramref name="origin"/>: the first of up to
        /// <see cref="Tries"/> random ones <paramref name="safe"/> passes, with where it ends. False when none does.
        /// </summary>
        public static bool TryPick(Vector3 origin, Vector3 incoming, System.Random random, Probe safe, out Vector3 direction, out Vector3 end)
        {
            Vector3 back = -incoming;
            back.y = 0.0f;
            if (back.sqrMagnitude < 1e-6f)
            {
                back = Vector3.forward;
            }
            float backYaw = Quaternion.LookRotation(back.normalized, Vector3.up).eulerAngles.y;
            for (int i = 0; i < Tries; i++)
            {
                direction = Way(backYaw, random);
                if (safe(origin, direction, out end))
                {
                    return true;
                }
            }
            direction = Vector3.zero;
            end = origin;
            return false;
        }

        /// <summary>One random way within the cone round <paramref name="backYaw"/> (degrees), unit length.</summary>
        public static Vector3 Way(float backYaw, System.Random random)
        {
            float yaw = backYaw + Mathf.Lerp(-ConeDegrees, ConeDegrees, (float)random.NextDouble());
            float pitch = Mathf.Lerp(LowestPitch, HighestPitch, (float)random.NextDouble());
            return Quaternion.Euler(-pitch, yaw, 0.0f) * Vector3.forward;
        }
    }
}

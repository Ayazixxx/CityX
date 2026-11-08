using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// One smooth curve through timed keys (the Crimson Claw's swipe and grab): cubic between keys, each key passed at a speed taken from its
    /// neighbours, still at the first and last, so a part never stops dead or jumps at a key.
    /// </summary>
    public static class KeyedMotion
    {
        /// <summary>The curve through <paramref name="keys"/> (at <paramref name="times"/>, rising) at <paramref name="t"/>.</summary>
        public static Vector3 Along(float[] times, Vector3[] keys, float t)
        {
            int i = 0;
            while (i < times.Length - 2 && t > times[i + 1])
            {
                i++;
            }
            float span = times[i + 1] - times[i];
            float s = Mathf.Clamp01((t - times[i]) / span);
            Vector3 from = keys[i];
            Vector3 to = keys[i + 1];
            Vector3 leaving = Speed(times, keys, i) * span;
            Vector3 arriving = Speed(times, keys, i + 1) * span;
            float s2 = s * s;
            float s3 = s2 * s;
            return (2.0f * s3 - 3.0f * s2 + 1.0f) * from + (s3 - 2.0f * s2 + s) * leaving
                 + (-2.0f * s3 + 3.0f * s2) * to + (s3 - s2) * arriving;
        }

        /// <summary>How fast the curve passes key <paramref name="i"/>: from its neighbours; still at the ends.</summary>
        private static Vector3 Speed(float[] times, Vector3[] keys, int i)
        {
            if (i == 0 || i == keys.Length - 1)
            {
                return Vector3.zero;
            }
            return (keys[i + 1] - keys[i - 1]) / (times[i + 1] - times[i - 1]);
        }
    }
}

using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// How to throw something at a fixed speed so that it falls onto a point (a hobo's bottle, docs/HOBO.md): the flatter of the two arcs that
    /// reach it, or the furthest-reaching one (45 degrees up) when it is out of reach.
    /// </summary>
    public static class BottleLob
    {
        /// <summary>The velocity, <paramref name="speed"/> long, from <paramref name="from"/> to land on <paramref name="to"/> under <paramref name="gravity"/> (downward, positive).</summary>
        public static Vector3 Velocity(Vector3 from, Vector3 to, float speed, float gravity)
        {
            Vector3 apart = to - from;
            var flat = new Vector3(apart.x, 0.0f, apart.z);
            float across = flat.magnitude;
            if (across < 1e-3f || gravity <= 0.0f)
            {
                return apart.sqrMagnitude > 1e-6f ? apart.normalized * speed : Vector3.up * speed;
            }
            float up = apart.y;
            float squared = speed * speed;
            float root = squared * squared - gravity * (gravity * across * across + 2.0f * up * squared);
            float angle = root < 0.0f ? Mathf.PI * 0.25f : Mathf.Atan2(squared - Mathf.Sqrt(root), gravity * across);
            return flat / across * (Mathf.Cos(angle) * speed) + Vector3.up * (Mathf.Sin(angle) * speed);
        }
    }
}

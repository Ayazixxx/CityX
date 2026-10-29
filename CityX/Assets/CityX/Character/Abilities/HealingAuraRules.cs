using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The Healing Aura's numbers (docs/HEALING.md), with no scene: a circle on the ground round the caster, moving with them, that slowly heals
    /// the caster and every player standing in it, for a while, then waits out its cooldown.
    /// </summary>
    public static class HealingAuraRules
    {
        /// <summary>The circle's radius, metres, on a body of its own size (it grows and shrinks with the caster): room for a few to stand in.</summary>
        public const float Radius = 4.5f;

        /// <summary>How far above or below the circle a body may stand and still be in it, metres (a step, a kerb, a jump).</summary>
        public const float HeightReach = 3.0f;

        public const float DurationSeconds = 12.0f;

        /// <summary>From one cast to the next, seconds.</summary>
        public const float CooldownSeconds = 30.0f;

        /// <summary>Health a second to each body in it, before Spell Power: slowly (owner: "heals yourself and others that go in the circle slowly").</summary>
        public const float HealPerSecond = 5.0f;

        public const float TickSeconds = 0.5f;

        public const float HealPerTick = HealPerSecond * TickSeconds;

        /// <summary>Whether feet at <paramref name="feet"/> stand in a circle at <paramref name="centre"/> on a body of <paramref name="scale"/>.</summary>
        public static bool Inside(Vector3 centre, Vector3 feet, float scale = 1.0f)
        {
            Vector3 flat = feet - centre;
            float up = flat.y;
            flat.y = 0.0f;
            float radius = Radius * Mathf.Max(0.01f, scale);
            return flat.sqrMagnitude <= radius * radius && Mathf.Abs(up) <= HeightReach * Mathf.Max(1.0f, scale);
        }
    }
}

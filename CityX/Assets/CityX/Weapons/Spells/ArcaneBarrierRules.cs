using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// ARCANE BARRIER'S NUMBERS (docs/ARCANE_BARRIER.md). A wall of magenta aura raised in front of the caster, Bartolomeo's Barrier in One Piece:
    /// Burning Blood (owner, 2026-10-27): it stops what comes at its face, it has health, and broken it fades away (never shatters). This first
    /// version is the one the owner described to start with: the caster stands still behind it while its key is held. The size, health and mana
    /// are mine.
    ///
    /// Sizes are on the 2.6 m body (BodyRig.StandingHeight), scaled with it.
    /// </summary>
    public static class ArcaneBarrierRules
    {
        /// <summary>The wall's width and height, metres: a square a good deal taller than its caster (owner: "a large square in front of you").</summary>
        public const float Width = 3.2f;

        public const float Height = 3.2f;

        /// <summary>How far in front of the caster's feet the wall stands, metres: clear of the outstretched hands.</summary>
        public const float Distance = 1.05f;

        /// <summary>Where its bottom edge is against the caster's feet, metres: a little into the ground, so nothing slips under it.</summary>
        public const float Bottom = -0.15f;

        /// <summary>The wall's collider's thickness, metres.</summary>
        public const float Thickness = 0.12f;

        /// <summary>
        /// How far round the wall's edge a blow is still caught, metres: the line from the attacker to the caster's chest may pass this close
        /// outside the wall and the wall still takes it (a shot that clipped the edge, a blade round its side).
        /// </summary>
        public const float EdgeMargin = 0.35f;

        /// <summary>The caster's chest, the point a blow is aimed at, as a share of the body's height.</summary>
        public const float ChestShare = 0.6f;

        /// <summary>Its health at rank 1, before the rank's share: what an AK takes most of a second to burn through, two and a half players.</summary>
        public const float BaseHealth = 250.0f;

        /// <summary>What each rank past the first adds to its health, as a share.</summary>
        public const float HealthPerRank = 0.1f;

        /// <summary>The mana to raise it, and to keep it up a second (mana comes back at PlayerVitals.ManaRegenPerSecond meanwhile).</summary>
        public const float RaiseManaCost = 25.0f;

        public const float UpkeepManaPerSecond = 10.0f;

        /// <summary>How long after it is let go, or after it breaks, before it can be raised again, seconds.</summary>
        public const float CooldownSeconds = 3.0f;

        public const float BrokenCooldownSeconds = 8.0f;

        /// <summary>How long it takes to rise, and to fade away let go or broken (after one last flare), seconds.</summary>
        public const float RiseSeconds = 0.15f;

        public const float LowerFadeSeconds = 0.35f;

        public const float BreakFadeSeconds = 1.0f;

        /// <summary>The wall's health at <paramref name="rank"/>.</summary>
        public static float HealthFor(int rank) => BaseHealth * (1.0f + HealthPerRank * (Mathf.Max(1, rank) - 1));

        /// <summary>
        /// Whether a blow from <paramref name="from"/> at <paramref name="chest"/> comes at the face of a wall whose middle is
        /// <paramref name="centre"/> and whose face looks along <paramref name="facing"/> (away from the caster): <paramref name="from"/> in front of
        /// it, and the line from there to the chest crossing it within <paramref name="halfWidth"/> and <paramref name="halfHeight"/> of its middle
        /// (and <see cref="EdgeMargin"/> more). Behind it, beside it or over it, it covers nothing.
        /// </summary>
        public static bool Covers(Vector3 centre, Vector3 facing, Vector3 up, float halfWidth, float halfHeight, Vector3 from, Vector3 chest)
        {
            Vector3 normal = facing.normalized;
            float fromSide = Vector3.Dot(from - centre, normal);
            float chestSide = Vector3.Dot(chest - centre, normal);
            if (fromSide <= 0.0f || chestSide >= fromSide)
            {
                return false;
            }
            Vector3 crossing = chestSide >= 0.0f ? from : Vector3.Lerp(from, chest, fromSide / (fromSide - chestSide));
            Vector3 along = crossing - centre;
            Vector3 side = Vector3.Cross(up, normal).normalized;
            return Mathf.Abs(Vector3.Dot(along, side)) <= halfWidth + EdgeMargin
                && Mathf.Abs(Vector3.Dot(along, up.normalized)) <= halfHeight + EdgeMargin;
        }

        /// <summary>Where the line from <paramref name="from"/> to <paramref name="chest"/> crosses the wall's plane, kept on the wall: where a blow shows on it.</summary>
        public static Vector3 StruckPoint(Vector3 centre, Vector3 facing, Vector3 up, float halfWidth, float halfHeight, Vector3 from, Vector3 chest)
        {
            Vector3 normal = facing.normalized;
            float fromSide = Vector3.Dot(from - centre, normal);
            float chestSide = Vector3.Dot(chest - centre, normal);
            Vector3 crossing = fromSide - chestSide > 1e-4f ? Vector3.Lerp(from, chest, Mathf.Clamp01(fromSide / (fromSide - chestSide))) : chest;
            Vector3 side = Vector3.Cross(up, normal).normalized;
            Vector3 along = crossing - centre;
            float x = Mathf.Clamp(Vector3.Dot(along, side), -halfWidth, halfWidth);
            float y = Mathf.Clamp(Vector3.Dot(along, up.normalized), -halfHeight, halfHeight);
            return centre + side * x + up.normalized * y;
        }
    }
}

using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// WHERE A SHOTGUN'S PELLETS GO, AND HOW HARD THEY STILL HIT (docs/SHOTGUN.md). The pattern is FIXED, never random, as Team Fortress 2's
    /// competitive spread and Quake's rings are: one pellet down the crosshair (so the owner's rule, a shot lands where the crosshair is, still
    /// holds for its middle), a third of the rest on a ring near it and the others on the cone's edge. A fixed pattern can be learnt, and another
    /// player's copy of the shot draws the same one with nothing sent. Distance thins the damage (Counter-Strike's and Fortnite's shotguns both
    /// fall off), so the gun is a short-range one twice over: fewer pellets land, and each lands softer.
    /// </summary>
    public static class PelletPattern
    {
        /// <summary>The inner ring's radius, as a share of the cone's.</summary>
        public const float InnerRingShare = 0.45f;

        /// <summary>How many of <paramref name="count"/> pellets are on the inner ring: a third of those that are not the middle one.</summary>
        public static int InnerRing(int count) => Mathf.Max(0, count - 1) / 3;

        /// <summary>
        /// Pellet <paramref name="pellet"/> of <paramref name="count"/>: how far off the middle it flies, degrees to the right (x) and up (y), in a
        /// cone <paramref name="coneDegrees"/> from its middle to its edge. Pellet 0 is the middle.
        /// </summary>
        public static Vector2 Offset(int pellet, int count, float coneDegrees)
        {
            if (pellet <= 0 || count <= 1 || coneDegrees <= 0.0f)
            {
                return Vector2.zero;
            }
            int inner = InnerRing(count);
            int outer = count - 1 - inner;
            bool onInner = pellet <= inner;
            int ring = onInner ? inner : outer;
            int place = onInner ? pellet - 1 : pellet - 1 - inner;
            // The inner ring starts straight up; the outer one half a step round, so no pellet hides behind another.
            float turn = (place + (onInner ? 0.0f : 0.5f)) / ring * Mathf.PI * 2.0f;
            float radius = coneDegrees * (onInner ? InnerRingShare : 1.0f);
            return new Vector2(Mathf.Sin(turn), Mathf.Cos(turn)) * radius;
        }

        /// <summary>The way a pellet flies: <paramref name="forward"/> turned by its offset (degrees right and up).</summary>
        public static Vector3 Direction(Vector3 forward, Vector2 offsetDegrees)
        {
            forward = forward.normalized;
            if (offsetDegrees == Vector2.zero)
            {
                return forward;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            Vector3 up = Vector3.Cross(forward, right);
            return Quaternion.AngleAxis(offsetDegrees.x, up) * Quaternion.AngleAxis(-offsetDegrees.y, right) * forward;
        }

        /// <summary>
        /// The share of its damage a hit keeps at <paramref name="metres"/>: all of it up to <paramref name="fullUntil"/>, falling in a straight
        /// line to <paramref name="floorShare"/> at <paramref name="floorFrom"/> and staying there. No falloff (floorFrom not past fullUntil): all of it.
        /// </summary>
        public static float DamageShare(float metres, float fullUntil, float floorFrom, float floorShare)
        {
            if (floorFrom <= fullUntil || metres <= fullUntil)
            {
                return 1.0f;
            }
            return Mathf.Lerp(1.0f, floorShare, Mathf.Clamp01((metres - fullUntil) / (floorFrom - fullUntil)));
        }
    }
}
